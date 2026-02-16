# Mac Mode Remapper for Windows

A lightweight Windows system-tray app that translates macOS keyboard shortcuts into their Windows equivalents. If you've switched from Mac to Windows and your muscle memory keeps reaching for **Cmd+C**, **Cmd+V**, **Cmd+Tab** -- this tool makes your Left Alt key behave like the Mac Command key.

---

## Table of Contents

- [How It Works](#how-it-works)
- [Installation](#installation)
- [Usage](#usage)
- [Shortcut Reference](#shortcut-reference)
  - [Global (All Apps)](#global-all-apps)
  - [Chrome / Edge / Brave / Firefox](#chrome--edge--brave--firefox)
  - [VS Code / Cursor](#vs-code--cursor)
  - [Warp Terminal](#warp-terminal)
  - [Windows Terminal / PowerShell](#windows-terminal--powershell)
  - [Windows Explorer](#windows-explorer)
- [Adding Custom Shortcuts](#adding-custom-shortcuts)
- [Creating a New App Profile](#creating-a-new-app-profile)
- [Architecture](#architecture)
- [Settings](#settings)
- [Logs](#logs)
- [Limitations](#limitations)
- [Building from Source](#building-from-source)
- [License](#license)

---

## How It Works

Mac Mode Remapper installs a low-level keyboard hook that intercepts keystrokes system-wide. When **Left Alt** is pressed with another key, the app checks the active foreground process, looks up the matching profile, and translates the shortcut.

**Key principles:**

| Behavior | Detail |
|---|---|
| **Left Alt** | Acts as the Mac **Cmd** key |
| **Right Alt (AltGr)** | Completely untouched -- works normally for international characters |
| **Win key** | Unchanged |
| **Ctrl key** | Unchanged |
| **Alt alone** | Preserved -- tapping Alt still opens menu bars |
| **Alt+Tab** | Never translated -- works natively |
| **Alt+F4** | Never translated -- works natively |
| **Alt+Space** | Never translated -- window menu works normally |

The app uses per-process profiles. When you're in Chrome, browser-specific shortcuts are active. When you switch to VS Code, editor shortcuts take over. Unrecognized apps fall back to the default profile.

---

## Installation

### Option 1: Download Release

Download the latest release from the [Releases](../../releases) page. Extract the zip and run `MacModeRemapper.App.exe`.

### Option 2: Build from Source

```powershell
git clone https://github.com/youruser/MacModeRemapper.git
cd MacModeRemapper
dotnet build
dotnet run --project src/MacModeRemapper.App
```

### Option 3: Publish Self-Contained Exe

```powershell
.\publish.ps1
# Output: ./dist/MacModeRemapper.App.exe (self-contained, no .NET install required)
```

---

## Usage

1. Run `MacModeRemapper.App.exe`
2. A tray icon appears in the system tray (bottom-right near the clock)
   - **Green M** = Mac Mode ON
   - **Gray M** = Mac Mode OFF
3. Right-click the tray icon for options:
   - **Mac Mode: ON/OFF** -- toggle remapping
   - **Suspend (10 min)** -- temporarily disable, auto-re-enables
   - **Start on Login** -- launch at Windows startup
   - **Exit** -- quit the app
4. Double-click the tray icon to quickly toggle ON/OFF

### Panic Key

Press **Ctrl+Alt+Backspace** at any time to immediately disable Mac Mode. A notification confirms the action.

> **Tip:** If the tray icon is hidden, click the `^` overflow arrow in the taskbar to find it, then drag it out to pin it permanently.

---

## Shortcut Reference

Below is every shortcut mapped in the default profiles. In the tables, **Mac Shortcut** is what you press (using Left Alt as Cmd), and **Windows Action** is what gets sent to the app.

### Global (All Apps)

These are the default fallback mappings. They apply to any app that doesn't have its own profile.

#### Essentials

| Mac Shortcut | Windows Action | Description |
|---|---|---|
| Cmd+C | Ctrl+C | Copy |
| Cmd+V | Ctrl+V | Paste |
| Cmd+X | Ctrl+X | Cut |
| Cmd+A | Ctrl+A | Select all |
| Cmd+Z | Ctrl+Z | Undo |
| Cmd+Shift+Z | Ctrl+Y | Redo |
| Cmd+S | Ctrl+S | Save |
| Cmd+Shift+S | Ctrl+Shift+S | Save as |
| Cmd+Shift+V | Ctrl+Shift+V | Paste without formatting |
| Cmd+Q | Alt+F4 | Quit app |

#### Text Navigation

| Mac Shortcut | Windows Action | Description |
|---|---|---|
| Cmd+Left | Home | Beginning of line |
| Cmd+Right | End | End of line |
| Cmd+Up | Ctrl+Home | Beginning of document |
| Cmd+Down | Ctrl+End | End of document |
| Cmd+Shift+Left | Shift+Home | Select to beginning of line |
| Cmd+Shift+Right | Shift+End | Select to end of line |
| Cmd+Shift+Up | Ctrl+Shift+Home | Select to beginning of document |
| Cmd+Shift+Down | Ctrl+Shift+End | Select to end of document |
| Cmd+Backspace | Ctrl+Backspace | Delete word left |

#### Common App Shortcuts

| Mac Shortcut | Windows Action | Description |
|---|---|---|
| Cmd+F | Ctrl+F | Find |
| Cmd+N | Ctrl+N | New |
| Cmd+O | Ctrl+O | Open |
| Cmd+W | Ctrl+W | Close tab/window |
| Cmd+T | Ctrl+T | New tab |
| Cmd+P | Ctrl+P | Print / Quick open |
| Cmd+B | Ctrl+B | Bold |
| Cmd+I | Ctrl+I | Italic |
| Cmd+U | Ctrl+U | Underline |
| Cmd+K | Ctrl+K | Insert link |

#### Screenshots

| Mac Shortcut | Windows Action | Description |
|---|---|---|
| Cmd+Shift+3 | Win+Shift+S | Screenshot (opens Snipping Tool) |
| Cmd+Shift+4 | Win+Shift+S | Screenshot (opens Snipping Tool) |
| Cmd+Shift+5 | Win+Shift+S | Screenshot (opens Snipping Tool) |

---

### Chrome / Edge / Brave / Firefox

Process names: `chrome`, `msedge`, `brave`, `firefox`, `opera`

Includes all global shortcuts plus:

| Mac Shortcut | Windows Action | Description |
|---|---|---|
| Cmd+T | Ctrl+T | New tab |
| Cmd+W | Ctrl+W | Close tab |
| Cmd+Shift+T | Ctrl+Shift+T | Reopen closed tab |
| Cmd+L | Ctrl+L | Focus address bar |
| Cmd+R | Ctrl+R | Reload page |
| Cmd+Shift+[ | Ctrl+Shift+Tab | Previous tab |
| Cmd+Shift+] | Ctrl+Tab | Next tab |
| Cmd+1 through Cmd+9 | Ctrl+1 through Ctrl+9 | Switch to tab N |
| Cmd+N | Ctrl+N | New window |
| Cmd+Shift+N | Ctrl+Shift+N | New incognito/private window |
| Cmd+D | Ctrl+D | Bookmark page |
| Cmd+G | Ctrl+G | Find next |
| Cmd+[ | Alt+Left | Browser back |
| Cmd+] | Alt+Right | Browser forward |

> **Note:** Alt+Left/Right in the Chrome address bar or text fields maps to Home/End (beginning/end of line), not browser back/forward. Cmd+[ and Cmd+] are the Mac-style back/forward shortcuts.

---

### VS Code / Cursor

Process names: `code`, `cursor`

Includes all global shortcuts plus:

#### Editor

| Mac Shortcut | Windows Action | Description |
|---|---|---|
| Cmd+P | Ctrl+P | Quick open file |
| Cmd+Shift+P | Ctrl+Shift+P | Command palette |
| Cmd+G | Ctrl+G | Go to line |
| Cmd+D | Ctrl+D | Add selection to next find match |
| Cmd+L | Ctrl+L | Select line |
| Cmd+/ | Ctrl+/ | Toggle comment |
| Cmd+H | Ctrl+H | Find and replace |
| Cmd+Shift+F | Ctrl+Shift+F | Search across files |
| Cmd+Shift+K | Ctrl+Shift+K | Delete line |
| Cmd+, | Ctrl+, | Open settings |
| Cmd+` | Ctrl+` | Toggle integrated terminal |
| Cmd+J | Ctrl+J | Toggle bottom panel |
| Cmd+= / Cmd+- | Ctrl+= / Ctrl+- | Zoom in / out |
| Cmd+Enter | Ctrl+Enter | Accept suggestion |

#### Sidebar Panels

| Mac Shortcut | Windows Action | Description |
|---|---|---|
| Cmd+B | Ctrl+B | Toggle sidebar |
| Cmd+Shift+E | Ctrl+Shift+E | Explorer panel |
| Cmd+Shift+G | Ctrl+Shift+G | Source control panel |
| Cmd+Shift+X | Ctrl+Shift+X | Extensions panel |
| Cmd+Shift+D | Ctrl+Shift+D | Debug panel |

#### Cursor AI (Cursor-specific)

| Mac Shortcut | Windows Action | Description |
|---|---|---|
| Cmd+K | Ctrl+K | Inline AI edit |
| Cmd+L | Ctrl+L | Open AI chat panel |
| Cmd+I | Ctrl+I | AI Agent / Composer |
| Cmd+Shift+I | Ctrl+Shift+I | Fullscreen Composer |

#### Tab Navigation

| Mac Shortcut | Windows Action | Description |
|---|---|---|
| Cmd+Shift+[ | Ctrl+Shift+Tab | Previous editor tab |
| Cmd+Shift+] | Ctrl+Tab | Next editor tab |
| Cmd+1 / 2 / 3 | Ctrl+1 / 2 / 3 | Focus editor group |

---

### Warp Terminal

Process names: `warp`

A dedicated profile for the [Warp](https://www.warp.dev/) AI terminal.

#### General

| Mac Shortcut | Windows Action | Description |
|---|---|---|
| Cmd+C | Ctrl+C | Copy |
| Cmd+V | Ctrl+V | Paste |
| Cmd+A | Ctrl+A | Select all |
| Cmd+F | Ctrl+F | Find |
| Cmd+Z | Ctrl+Z | Undo |
| Cmd+Q | Alt+F4 | Quit |
| Cmd+N | Ctrl+N | New window |
| Cmd+W | Ctrl+W | Close tab |
| Cmd+Shift+C | Ctrl+Shift+C | Copy command |
| Cmd+Shift+S | Ctrl+Shift+S | Share block |

#### Tabs & Panes

| Mac Shortcut | Windows Action | Description |
|---|---|---|
| Cmd+T | Ctrl+T | New tab |
| Cmd+Shift+T | Ctrl+Shift+T | Reopen closed tab |
| Cmd+1 through Cmd+9 | Ctrl+1 through Ctrl+9 | Switch to tab N |
| Cmd+D | Ctrl+D | Split pane right |
| Cmd+Shift+D | Ctrl+Shift+D | Split pane down |
| Cmd+[ | Ctrl+[ | Previous pane |
| Cmd+] | Ctrl+] | Next pane |

#### Warp Features

| Mac Shortcut | Windows Action | Description |
|---|---|---|
| Cmd+P | Ctrl+P | Command palette |
| Cmd+O | Ctrl+O | File search |
| Cmd+L | Ctrl+L | Focus terminal input |
| Cmd+K | Ctrl+K | Clear blocks |
| Cmd+B | Ctrl+B | Bookmark block |
| Cmd+= / Cmd+- / Cmd+0 | Ctrl+= / Ctrl+- / Ctrl+0 | Font size controls |

#### Text Navigation

| Mac Shortcut | Windows Action | Description |
|---|---|---|
| Cmd+Left | Home | Beginning of line |
| Cmd+Right | End | End of line |
| Cmd+Shift+Left | Shift+Home | Select to beginning of line |
| Cmd+Shift+Right | Shift+End | Select to end of line |
| Cmd+Backspace | Ctrl+Backspace | Delete word left |

---

### Windows Terminal / PowerShell

Process names: `windowsterminal`, `powershell`, `cmd`, `wt`, `pwsh`

A conservative profile -- **Cmd+C is intentionally NOT mapped to Ctrl+C** in terminals because Ctrl+C means "interrupt/cancel" in terminal contexts, not "copy."

| Mac Shortcut | Windows Action | Description |
|---|---|---|
| Cmd+Shift+C | Ctrl+Shift+C | Copy (terminal-safe) |
| Cmd+Shift+V | Ctrl+Shift+V | Paste (terminal-safe) |
| Cmd+A | Ctrl+A | Select all |
| Cmd+Z | Ctrl+Z | Undo (in input) |
| Cmd+F | Ctrl+F | Find |
| Cmd+Q | Alt+F4 | Quit |
| Cmd+Left | Home | Beginning of line |
| Cmd+Right | End | End of line |
| Cmd+Backspace | Ctrl+Backspace | Delete word left |

---

### Windows Explorer

Process names: `explorer`

| Mac Shortcut | Windows Action | Description |
|---|---|---|
| Cmd+C | Ctrl+C | Copy |
| Cmd+V | Ctrl+V | Paste |
| Cmd+X | Ctrl+X | Cut |
| Cmd+Z | Ctrl+Z | Undo |
| Cmd+Shift+Z | Ctrl+Y | Redo |
| Cmd+A | Ctrl+A | Select all |
| Cmd+S | Ctrl+S | Save |
| Cmd+N | Ctrl+N | New window |
| Cmd+Shift+N | Ctrl+Shift+N | New folder |
| Cmd+F | Ctrl+F | Search |
| Cmd+W | Ctrl+W | Close window |
| Cmd+Q | Alt+F4 | Quit |
| Cmd+Backspace | Delete | Move to Recycle Bin |

> **Note:** Alt+Left/Right are intentionally NOT remapped in Explorer, since their native behavior (back/forward navigation) is useful.

---

## Adding Custom Shortcuts

### Editing an Existing Profile

1. Open the profile JSON file in `profiles/` (e.g., `profiles/chrome.json`)
2. Add a new entry to the `"mappings"` array:

```json
{ "trigger": "Alt+E", "action": "Ctrl+E" }
```

3. Save the file. **Changes are picked up automatically** -- no restart needed. The app watches for file changes.

### Shortcut String Format

Shortcuts are written as modifier keys joined with `+`, ending with the main key:

```
[Alt+][Shift+][Ctrl+]<Key>
```

#### Available Modifier Names

| Name | Key |
|---|---|
| `Alt` | Left Alt (only in triggers -- implicit "Cmd") |
| `Shift` | Shift |
| `Ctrl` | Control |
| `Win` | Windows key (actions only) |

#### Available Key Names

| Name | Key | Name | Key |
|---|---|---|---|
| `A`-`Z` | Letter keys | `Left` | Left arrow |
| `0`-`9` | Number keys | `Right` | Right arrow |
| `F1`-`F24` | Function keys | `Up` | Up arrow |
| `Tab` | Tab | `Down` | Down arrow |
| `Space` | Spacebar | `Home` | Home |
| `Backspace` | Backspace | `End` | End |
| `Enter` | Enter | `Delete` | Delete |
| `Escape` | Escape | `PageUp` | Page Up |
| `[` | Open bracket | `PageDown` | Page Down |
| `]` | Close bracket | `/` | Forward slash |
| `-` | Minus | `=` | Equals |
| `,` | Comma | `.` | Period |
| `;` | Semicolon | `'` | Quote |
| `` ` `` | Backtick | `\` | Backslash |

### Trigger Rules

- **Triggers must start with `Alt+`** -- the Alt is your "Cmd" key.
- Additional modifiers (`Shift`, `Ctrl`) can be combined: `Alt+Shift+P`
- The `Alt` in triggers is always Left Alt. Right Alt (AltGr) is never intercepted.

### Action Rules

- Actions define what gets sent to the system.
- Can use any combination of modifiers: `Ctrl+Shift+Tab`, `Alt+F4`, `Win+Shift+S`
- A bare key name with no modifiers is valid: `Home`, `Delete`, `End`
- If the action includes `Alt+`, the physical Alt key stays held (useful for `Alt+F4`, `Alt+Left` browser back, etc.)

### Examples

```json
// Simple: Cmd+E → Ctrl+E
{ "trigger": "Alt+E", "action": "Ctrl+E" }

// With Shift: Cmd+Shift+P → Ctrl+Shift+P
{ "trigger": "Alt+Shift+P", "action": "Ctrl+Shift+P" }

// To a bare key: Cmd+Left → Home
{ "trigger": "Alt+Left", "action": "Home" }

// Keep Alt held: Cmd+Q → Alt+F4 (quit)
{ "trigger": "Alt+Q", "action": "Alt+F4" }

// Using Win key: Cmd+Shift+4 → Win+Shift+S (screenshot)
{ "trigger": "Alt+Shift+4", "action": "Win+Shift+S" }
```

---

## Creating a New App Profile

1. Create a new JSON file in `profiles/`, e.g., `profiles/slack.json`
2. Use this template:

```json
{
  "name": "Slack",
  "processNames": ["slack"],
  "mappings": [
    { "trigger": "Alt+K", "action": "Ctrl+K" },
    { "trigger": "Alt+Shift+A", "action": "Ctrl+Shift+A" }
  ]
}
```

3. Save the file. The app picks it up automatically.

### Finding the Process Name

To find the process name of any app:

1. Open the app
2. Open Task Manager (Ctrl+Shift+Esc)
3. Find the app in the **Processes** tab
4. Right-click and select **Go to details**
5. The **Name** column shows the `.exe` name -- use the name without `.exe`, lowercase

Or run in PowerShell:

```powershell
Get-Process | Where-Object { $_.MainWindowTitle -ne "" } | Select-Object ProcessName, MainWindowTitle
```

### Profile Priority

- If a process matches a specific profile, that profile's mappings are used exclusively.
- If no profile matches, the `default.json` profile is used as a fallback.
- Per-process profiles **do not** inherit from `default.json`. If you want common shortcuts in a custom profile, include them explicitly.

---

## Architecture

```
MacModeRemapper.App (WPF Tray App)
  TrayIcon.cs              System tray, context menu, orchestration
  App.xaml.cs              Entry point, single-instance mutex

MacModeRemapper.Core (Class Library)
  Hook/
    KeyboardHook.cs        WH_KEYBOARD_LL install/uninstall
    NativeMethods.cs       Win32 P/Invoke (SendInput, SetWindowsHookEx, etc.)
    KeyboardHookEventArgs  Left Alt vs Right Alt detection
  Engine/
    MappingEngine.cs       State machine: Idle -> AltPending -> ChordActive
    KeySender.cs           SendInput wrapper (atomic batches)
    ModifierState.cs       Tracks physical Shift/Ctrl/Alt state
  Profiles/
    ProfileManager.cs      Loads JSON profiles, O(1) lookup, FileSystemWatcher
    KeyParser.cs           Parses "Alt+Shift+T" -> VK codes
    Profile.cs / KeyMapping.cs   Data models
  ProcessDetection/
    ForegroundProcessDetector.cs   Cached GetForegroundWindow + process name
  Settings/
    SettingsManager.cs     JSON persistence
    StartupManager.cs      Registry Run key for auto-start
  Logging/
    Logger.cs              Rolling daily file logger
```

### State Machine

The core engine uses a three-state machine:

```
                                  Alt+Tab, Alt+F4, Alt+Space
                                  pass through naturally
                                         |
[IDLE] --Left Alt down--> [ALT PENDING] -+
  ^                            |         |
  |                            |         +-- Unmapped key: pass through, -> IDLE
  |                            |
  |                   Mapped chord key detected
  |                   Suppress key, inject Ctrl+<mapped>
  |                            |
  |                            v
  +--Left Alt released-- [CHORD ACTIVE]
     (suppress Alt up)         |
                               +-- Another mapped key: fire new mapping
                               +-- Unmapped key: suppress
```

**Key design decision:** Left Alt is NOT suppressed when first pressed. It passes through to the system naturally. This means Alt+Tab, Alt+F4, and Alt+Space work without any special handling. When a chord key is detected, the engine cancels the Alt (sends Alt-up) and injects the mapped shortcut in a single atomic `SendInput` batch.

---

## Settings

Settings are stored in `settings.json` in the app directory:

```json
{
  "macModeEnabled": true,
  "startOnLogin": false,
  "debugLogging": false
}
```

| Setting | Default | Description |
|---|---|---|
| `macModeEnabled` | `true` | Whether Mac Mode is active |
| `startOnLogin` | `false` | Auto-start with Windows (uses Registry Run key) |
| `debugLogging` | `false` | Enable verbose logging of every key event |

---

## Logs

Logs are written to:

```
%LocalAppData%\MacModeRemapper\logs\macmode-YYYY-MM-DD.log
```

Set `"debugLogging": true` in `settings.json` to see detailed key event traces for troubleshooting.

---

## Limitations

| Limitation | Detail |
|---|---|
| **Elevated apps** | Cannot remap inside admin/elevated processes unless Mac Mode Remapper is also run as admin |
| **AltGr / International layouts** | Right Alt (AltGr) is explicitly excluded via the extended-key flag. No interference with international characters |
| **Games** | Some fullscreen DirectX/Vulkan games may not respond to SendInput |
| **Hook timeout** | Windows enforces a ~300ms timeout on low-level hook callbacks. Extreme system load could cause the OS to silently remove the hook |
| **Secure desktop** | The hook does not operate on the UAC secure desktop. This is by design |
| **Profile inheritance** | Per-process profiles do not inherit from default.json. Common shortcuts must be duplicated |
| **Menu bar flash** | Since Alt passes through before being cancelled, a very brief menu bar highlight may occasionally appear when using chord shortcuts. This is usually imperceptible |

---

## Building from Source

### Prerequisites

- Windows 10/11
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)

### Build

```powershell
cd MacModeRemapper
dotnet build
```

### Run (Development)

```powershell
dotnet run --project src/MacModeRemapper.App
```

### Publish Self-Contained Exe

```powershell
.\publish.ps1
```

This produces a single-file self-contained executable in `./dist/` that includes the .NET runtime -- no .NET installation required on the target machine.

### Project Structure

```
MacModeRemapper/
  MacModeRemapper.sln
  src/
    MacModeRemapper.App/     WPF tray application
    MacModeRemapper.Core/    Core library (hook, engine, profiles)
  profiles/                  Default shortcut profiles (JSON)
  settings.json              App settings
  publish.ps1                Build script
  README.md
```

---

## License

MIT
