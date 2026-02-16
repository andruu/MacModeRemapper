using MacModeRemapper.Core.Hook;
using MacModeRemapper.Core.Logging;
using MacModeRemapper.Core.ProcessDetection;
using MacModeRemapper.Core.Profiles;

namespace MacModeRemapper.Core.Engine;

/// <summary>
/// Core remapping engine. New architecture: Left Alt is NOT suppressed on keydown.
/// It passes through naturally so Alt+Tab, Alt+F4, Alt+Space all work natively.
/// When a recognized chord key arrives, we cancel the Alt and inject the mapped shortcut.
///
/// States:
///   Idle        - nothing tracked
///   AltPending  - Left Alt is physically held and was passed through
///   ChordActive - a mapping was fired; physical Alt is held but we sent synthetic Alt-up
/// </summary>
public sealed class MappingEngine
{
    private enum State
    {
        Idle,
        AltPending,
        ChordActive
    }

    private readonly ProfileManager _profiles;
    private readonly ForegroundProcessDetector _processDetector;
    private readonly ModifierState _modState = new();

    private State _state = State.Idle;
    private bool _enabled = true;

    /// <summary>Raised when panic key (Ctrl+Alt+Backspace) is pressed.</summary>
    public event Action? PanicKeyPressed;

    /// <summary>Raised when a mapping is activated (for UI feedback).</summary>
    public event Action<string>? MappingActivated;

    public bool Enabled
    {
        get => _enabled;
        set
        {
            if (_enabled == value) return;
            _enabled = value;
            Logger.Info($"Mac Mode {(_enabled ? "enabled" : "disabled")}");

            if (!_enabled)
            {
                _state = State.Idle;
                _modState.Reset();
            }
        }
    }

    public MappingEngine(ProfileManager profiles, ForegroundProcessDetector processDetector)
    {
        _profiles = profiles;
        _processDetector = processDetector;
    }

    /// <summary>
    /// Main entry point called by the keyboard hook for every key event.
    /// Returns true if the event should be suppressed (swallowed).
    /// </summary>
    public bool ProcessKeyEvent(KeyboardHookEventArgs e)
    {
        // Always pass through injected events (our own SendInput) to avoid recursion
        if (e.IsInjected)
            return false;

        // Track physical modifier state regardless of Mac Mode
        UpdateModifierState(e);

        // Check panic key: Ctrl+Alt+Backspace
        if (e.IsKeyDown && e.VirtualKeyCode == NativeMethods.VK_BACK &&
            _modState.CtrlDown && _modState.LeftAltDown)
        {
            Logger.Info("Panic key detected: Ctrl+Alt+Backspace");
            PanicKeyPressed?.Invoke();
            return false;
        }

        if (!_enabled)
            return false;

        // Right Alt (AltGr) is always passed through
        if (e.IsRightAlt)
            return false;

        return _state switch
        {
            State.Idle => HandleIdle(e),
            State.AltPending => HandleAltPending(e),
            State.ChordActive => HandleChordActive(e),
            _ => false
        };
    }

    /// <summary>
    /// Idle: nothing is tracked. Watch for Left Alt keydown.
    /// </summary>
    private bool HandleIdle(KeyboardHookEventArgs e)
    {
        if (e.IsLeftAlt && e.IsKeyDown)
        {
            _state = State.AltPending;
            Logger.Debug("State: Idle -> AltPending (Left Alt down, passed through)");
            // DO NOT suppress. Let the physical Alt pass through.
            return false;
        }

        return false;
    }

    /// <summary>
    /// AltPending: Left Alt is physically held and was passed through to the system.
    /// Waiting to see if the next key forms a recognized chord.
    /// Alt+Tab, Alt+F4, Alt+Space all work natively because we never touched Alt.
    /// </summary>
    private bool HandleAltPending(KeyboardHookEventArgs e)
    {
        // Left Alt released without a chord -> natural Alt tap, go to Idle
        if (e.IsLeftAlt && e.IsKeyUp)
        {
            Logger.Debug("State: AltPending -> Idle (Left Alt released, natural behavior)");
            _state = State.Idle;
            return false; // Let the physical Alt up through
        }

        // Left Alt autorepeat
        if (e.IsLeftAlt && e.IsKeyDown)
            return false; // Let autorepeat through

        // Global passthrough keys: let them through naturally (Alt is already held)
        if (e.IsKeyDown && IsGlobalPassthrough(e.VirtualKeyCode))
        {
            Logger.Debug($"Global passthrough VK 0x{e.VirtualKeyCode:X2}, going Idle");
            _state = State.Idle;
            return false;
        }

        // Special: Alt+Q = quit app (macOS Cmd+Q). Uses multiple strategies
        // to close the window reliably across different app types.
        if (e.IsKeyDown && e.VirtualKeyCode == NativeMethods.VK_Q && !_modState.ShiftDown && !_modState.CtrlDown)
        {
            Logger.Debug("Quit app triggered: Alt+Q");
            CancelAltAndTransition();
            CloseActiveWindow();
            return true;
        }

        // Special: Alt+` = cycle windows of the same app (macOS Cmd+`)
        if (e.IsKeyDown && e.VirtualKeyCode == NativeMethods.VK_OEM_3 && !_modState.ShiftDown)
        {
            Logger.Debug("Window cycle triggered: Alt+`");
            CancelAltAndTransition();
            WindowCycler.CycleNextWindow();
            return true;
        }

        // Non-modifier keydown: check for a chord mapping
        if (e.IsKeyDown && !IsModifierKey(e.VirtualKeyCode))
        {
            string processName = _processDetector.GetForegroundProcessName();
            var triggerMods = _modState.ActiveModifiers;
            var mapping = _profiles.GetMapping(processName, triggerMods, e.VirtualKeyCode);

            if (mapping != null)
            {
                bool actionNeedsAlt = mapping.ActionModifierVks.Contains(NativeMethods.VK_LMENU);
                Logger.Debug($"Chord matched: process={processName}, actionNeedsAlt={actionNeedsAlt}, mapping={mapping}");

                if (actionNeedsAlt)
                {
                    // The action wants Alt held (e.g., Alt+Q → Alt+F4).
                    // Physical Alt is already held and passed through, so just
                    // send the key (+ any non-Alt modifiers). Don't touch Alt.
                    ExecuteMappingKeepAlt(mapping);
                    // Stay in AltPending: Alt is still physically held and the
                    // system still sees it. Normal behavior resumes.
                }
                else
                {
                    ExecuteMapping(mapping);
                    _state = State.ChordActive;
                }
                MappingActivated?.Invoke($"{mapping}");
                return true; // Suppress the physical chord key
            }
            else
            {
                // No mapping: let everything through naturally (app gets Alt+key)
                Logger.Debug($"No mapping for VK 0x{e.VirtualKeyCode:X2} in process '{processName}', passthrough");
                _state = State.Idle;
                return false;
            }
        }

        // Modifier key changes (Shift, Ctrl down/up): pass through, stay in AltPending
        return false;
    }

    /// <summary>
    /// ChordActive: a mapping was fired. Physical Alt is still held by the user,
    /// but we already sent a synthetic Alt-up to cancel it. We suppress the
    /// physical Alt-up when it finally comes, and handle additional chord keys.
    /// </summary>
    private bool HandleChordActive(KeyboardHookEventArgs e)
    {
        // Physical Left Alt released: suppress it (we already sent Alt-up), go Idle
        if (e.IsLeftAlt && e.IsKeyUp)
        {
            Logger.Debug("State: ChordActive -> Idle (Left Alt released, suppressed)");
            _state = State.Idle;
            return true; // Suppress: system already thinks Alt is up
        }

        // Left Alt autorepeat: suppress
        if (e.IsLeftAlt && e.IsKeyDown)
            return true;

        // Modifier keys (Shift, Ctrl): let physical changes through
        if (IsModifierKey(e.VirtualKeyCode))
            return false;

        // Global passthrough keys (Tab, F4, Space): re-engage Alt so native
        // behavior works (e.g., user does Alt+` then switches to Alt+Tab)
        if (e.IsKeyDown && IsGlobalPassthrough(e.VirtualKeyCode))
        {
            Logger.Debug($"ChordActive -> Idle: re-engaging Alt for global passthrough VK 0x{e.VirtualKeyCode:X2}");
            KeySender.SendBatch(new[] { KeySender.MakeKeyDown(NativeMethods.VK_LMENU) });
            _state = State.Idle;
            return false; // Let the key through; system now sees Alt+key
        }

        // Another keydown: check for window cycling or a new chord mapping
        if (e.IsKeyDown)
        {
            // Alt+` window cycling (repeatable while Alt is held)
            if (e.VirtualKeyCode == NativeMethods.VK_OEM_3 && !_modState.ShiftDown)
            {
                Logger.Debug("Window cycle (repeat) triggered in ChordActive");
                WindowCycler.CycleNextWindow();
                return true;
            }

            string processName = _processDetector.GetForegroundProcessName();
            var triggerMods = _modState.ActiveModifiers;
            var mapping = _profiles.GetMapping(processName, triggerMods, e.VirtualKeyCode);

            if (mapping != null)
            {
                Logger.Debug($"Sequential chord: process={processName}, mapping={mapping}");
                ExecuteChordInActive(mapping);
                MappingActivated?.Invoke($"{mapping}");
                return true;
            }

            // Unmapped key in chord context: suppress
            return true;
        }

        // Keyup of previously suppressed chord keys: suppress
        if (e.IsKeyUp)
            return true;

        return false;
    }

    /// <summary>
    /// Executes a mapping where the action includes Alt as a modifier (e.g., Alt+Q → Alt+F4).
    /// Physical Alt is already held, so we just send the non-Alt modifiers + action key
    /// without touching Alt at all.
    /// </summary>
    private void ExecuteMappingKeepAlt(CompiledMapping mapping)
    {
        var inputs = new List<NativeMethods.INPUT>();

        // Send non-Alt action modifiers
        foreach (int modVk in mapping.ActionModifierVks)
        {
            if (modVk == NativeMethods.VK_LMENU)
                continue; // Alt is already physically held
            if (modVk == NativeMethods.VK_LSHIFT && _modState.ShiftDown)
                continue; // Shift is already physically held
            inputs.Add(KeySender.MakeKeyDown(modVk));
        }

        // Press and release the action key (system sees Alt+key because physical Alt is held)
        inputs.Add(KeySender.MakeKeyDown(mapping.ActionVk));
        inputs.Add(KeySender.MakeKeyUp(mapping.ActionVk));

        // Release non-Alt action modifiers
        foreach (int modVk in mapping.ActionModifierVks.Reverse())
        {
            if (modVk == NativeMethods.VK_LMENU)
                continue;
            if (modVk == NativeMethods.VK_LSHIFT && _modState.ShiftDown)
                continue;
            inputs.Add(KeySender.MakeKeyUp(modVk));
        }

        KeySender.SendBatch(inputs.ToArray());
    }

    /// <summary>
    /// Executes a mapping from AltPending state. Physical Alt was passed through,
    /// so we need to cancel it first, then send the replacement chord.
    /// Sends: [Ctrl down, Alt up, key down, key up, Ctrl up]
    /// Ctrl arrives before Alt up, preventing menu-bar activation.
    /// </summary>
    private void ExecuteMapping(CompiledMapping mapping)
    {
        var inputs = new List<NativeMethods.INPUT>();

        // Determine which action modifiers we need to send synthetically.
        // If Shift is physically held and the action needs Shift, skip synthetic Shift
        // to avoid double-press/release issues.
        bool physicalShiftHeld = _modState.ShiftDown;
        bool actionNeedsShift = mapping.ActionModifierVks.Contains(NativeMethods.VK_LSHIFT);

        // Step 1: Press action modifiers (e.g., Ctrl) BEFORE releasing Alt
        // This prevents menu-bar activation (menu only activates if Alt is released
        // with no other modifier held).
        foreach (int modVk in mapping.ActionModifierVks)
        {
            if (modVk == NativeMethods.VK_LSHIFT && physicalShiftHeld)
                continue; // Shift is already physically down
            inputs.Add(KeySender.MakeKeyDown(modVk));
        }

        // Step 2: Release physical Alt (cancel the Alt we let through)
        inputs.Add(KeySender.MakeKeyUp(NativeMethods.VK_LMENU));

        // Step 3: If Shift is physically held but action does NOT need Shift, release it temporarily
        if (physicalShiftHeld && !actionNeedsShift)
        {
            inputs.Add(KeySender.MakeKeyUp(NativeMethods.VK_LSHIFT));
        }

        // Step 4: Press and release the action key
        inputs.Add(KeySender.MakeKeyDown(mapping.ActionVk));
        inputs.Add(KeySender.MakeKeyUp(mapping.ActionVk));

        // Step 5: Restore Shift if we released it
        if (physicalShiftHeld && !actionNeedsShift)
        {
            inputs.Add(KeySender.MakeKeyDown(NativeMethods.VK_LSHIFT));
        }

        // Step 6: Release action modifiers
        foreach (int modVk in mapping.ActionModifierVks.Reverse())
        {
            if (modVk == NativeMethods.VK_LSHIFT && physicalShiftHeld)
                continue; // Don't release Shift; it's physically held
            inputs.Add(KeySender.MakeKeyUp(modVk));
        }

        KeySender.SendBatch(inputs.ToArray());
    }

    /// <summary>
    /// Executes a mapping while already in ChordActive state (sequential chord).
    /// Alt was already synthetically released, so we just send the replacement chord.
    /// </summary>
    private void ExecuteChordInActive(CompiledMapping mapping)
    {
        var inputs = new List<NativeMethods.INPUT>();

        bool physicalShiftHeld = _modState.ShiftDown;
        bool actionNeedsShift = mapping.ActionModifierVks.Contains(NativeMethods.VK_LSHIFT);

        // Press action modifiers
        foreach (int modVk in mapping.ActionModifierVks)
        {
            if (modVk == NativeMethods.VK_LSHIFT && physicalShiftHeld)
                continue;
            inputs.Add(KeySender.MakeKeyDown(modVk));
        }

        // Release Shift if physically held but not needed
        if (physicalShiftHeld && !actionNeedsShift)
            inputs.Add(KeySender.MakeKeyUp(NativeMethods.VK_LSHIFT));

        // Press and release action key
        inputs.Add(KeySender.MakeKeyDown(mapping.ActionVk));
        inputs.Add(KeySender.MakeKeyUp(mapping.ActionVk));

        // Restore Shift if we released it
        if (physicalShiftHeld && !actionNeedsShift)
            inputs.Add(KeySender.MakeKeyDown(NativeMethods.VK_LSHIFT));

        // Release action modifiers
        foreach (int modVk in mapping.ActionModifierVks.Reverse())
        {
            if (modVk == NativeMethods.VK_LSHIFT && physicalShiftHeld)
                continue;
            inputs.Add(KeySender.MakeKeyUp(modVk));
        }

        KeySender.SendBatch(inputs.ToArray());
    }

    /// <summary>
    /// Closes the active window using multiple strategies for maximum compatibility.
    /// Tries WM_SYSCOMMAND SC_CLOSE (Alt+F4 equivalent), then WM_CLOSE,
    /// then falls back to synthesized Alt+F4 key presses.
    /// </summary>
    private static void CloseActiveWindow()
    {
        IntPtr hwnd = NativeMethods.GetForegroundWindow();
        if (hwnd == IntPtr.Zero) return;

        // Strategy 1: WM_SYSCOMMAND SC_CLOSE (exact Alt+F4 equivalent)
        NativeMethods.PostMessage(hwnd, NativeMethods.WM_SYSCOMMAND, NativeMethods.SC_CLOSE, IntPtr.Zero);

        // Strategy 2: Also send synthesized Alt+F4 for apps that only respond to input
        Task.Run(async () =>
        {
            await Task.Delay(50);
            var inputs = new[]
            {
                KeySender.MakeKeyDown(NativeMethods.VK_LMENU),
                KeySender.MakeKeyDown(NativeMethods.VK_F4),
                KeySender.MakeKeyUp(NativeMethods.VK_F4),
                KeySender.MakeKeyUp(NativeMethods.VK_LMENU),
            };
            KeySender.SendBatch(inputs);
        });
    }

    /// <summary>
    /// Cancels the physical Alt that was passed through (sends Ctrl down, Alt up, Ctrl up
    /// to suppress menu activation) and transitions to ChordActive state.
    /// Used by special actions like window cycling that aren't regular key mappings.
    /// </summary>
    private void CancelAltAndTransition()
    {
        var inputs = new[]
        {
            KeySender.MakeKeyDown(NativeMethods.VK_LCONTROL),
            KeySender.MakeKeyUp(NativeMethods.VK_LMENU),
            KeySender.MakeKeyUp(NativeMethods.VK_LCONTROL),
        };
        KeySender.SendBatch(inputs);
        _state = State.ChordActive;
    }

    private void UpdateModifierState(KeyboardHookEventArgs e)
    {
        switch (e.VirtualKeyCode)
        {
            case NativeMethods.VK_LMENU:
                _modState.LeftAltDown = e.IsKeyDown;
                break;
            case NativeMethods.VK_MENU when !e.IsExtended:
                _modState.LeftAltDown = e.IsKeyDown;
                break;
            case NativeMethods.VK_LSHIFT:
                _modState.LeftShiftDown = e.IsKeyDown;
                break;
            case NativeMethods.VK_RSHIFT:
                _modState.RightShiftDown = e.IsKeyDown;
                break;
            case NativeMethods.VK_SHIFT:
                if (e.IsExtended)
                    _modState.RightShiftDown = e.IsKeyDown;
                else
                    _modState.LeftShiftDown = e.IsKeyDown;
                break;
            case NativeMethods.VK_LCONTROL:
                _modState.LeftCtrlDown = e.IsKeyDown;
                break;
            case NativeMethods.VK_RCONTROL:
                _modState.RightCtrlDown = e.IsKeyDown;
                break;
            case NativeMethods.VK_CONTROL:
                if (e.IsExtended)
                    _modState.RightCtrlDown = e.IsKeyDown;
                else
                    _modState.LeftCtrlDown = e.IsKeyDown;
                break;
        }
    }

    private static bool IsGlobalPassthrough(int vk)
    {
        return vk == NativeMethods.VK_TAB ||
               vk == NativeMethods.VK_F4 ||
               vk == NativeMethods.VK_SPACE;
    }

    private static bool IsModifierKey(int vk)
    {
        return vk is NativeMethods.VK_LSHIFT or NativeMethods.VK_RSHIFT or NativeMethods.VK_SHIFT
            or NativeMethods.VK_LCONTROL or NativeMethods.VK_RCONTROL or NativeMethods.VK_CONTROL
            or NativeMethods.VK_LMENU or NativeMethods.VK_RMENU or NativeMethods.VK_MENU;
    }
}
