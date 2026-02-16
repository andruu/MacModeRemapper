using System.Text.Json;
using MacModeRemapper.Core.Engine;
using MacModeRemapper.Core.Logging;

namespace MacModeRemapper.Core.Profiles;

/// <summary>
/// Loads profile JSON files, compiles mappings, and provides O(1) lookup
/// by process name and trigger key.
/// </summary>
public sealed class ProfileManager : IDisposable
{
    private const double DebounceSeconds = 2.0;
    private const int MaxReloadRetries = 5;
    private const int ReloadRetryBaseDelayMs = 300;
    private const string SpecialActionPrefix = "special:";

    private readonly string _profilesDir;
    private FileSystemWatcher? _watcher;

    /// <summary>
    /// Process name (lowercase) -> compiled lookup dictionary.
    /// The inner dictionary key is (ModifierFlags, VirtualKeyCode).
    /// </summary>
    private Dictionary<string, Dictionary<(ModifierFlags, int), CompiledMapping>> _processMap = new();

    /// <summary>Fallback mappings from default.json.</summary>
    private Dictionary<(ModifierFlags, int), CompiledMapping> _defaultMappings = new();

    public ProfileManager(string profilesDir)
    {
        _profilesDir = profilesDir;
    }

    public void Load()
    {
        var newProcessMap = new Dictionary<string, Dictionary<(ModifierFlags, int), CompiledMapping>>(
            StringComparer.OrdinalIgnoreCase);
        Dictionary<(ModifierFlags, int), CompiledMapping>? newDefault = null;

        if (!Directory.Exists(_profilesDir))
        {
            Logger.Error($"Profiles directory not found: {_profilesDir}");
            return;
        }

        foreach (string file in Directory.GetFiles(_profilesDir, "*.json"))
        {
            try
            {
                string json = File.ReadAllText(file);
                var dto = JsonSerializer.Deserialize<ProfileDto>(json);
                if (dto == null) continue;

                var compiled = CompileMappings(dto.Mappings);
                string fileName = Path.GetFileNameWithoutExtension(file).ToLowerInvariant();

                if (fileName == "default")
                {
                    newDefault = compiled;
                    Logger.Info($"Loaded default profile: {dto.Name} ({dto.Mappings.Count} mappings)");
                    continue;
                }

                foreach (string procName in dto.ProcessNames)
                {
                    string key = procName.ToLowerInvariant();
                    newProcessMap[key] = compiled;
                }

                Logger.Info($"Loaded profile '{dto.Name}' for [{string.Join(", ", dto.ProcessNames)}] ({dto.Mappings.Count} mappings)");
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to load profile {file}: {ex.Message}");
            }
        }

        // Atomic swap
        _processMap = newProcessMap;
        _defaultMappings = newDefault ?? new();
    }

    /// <summary>
    /// Gets the compiled mapping for a given trigger in the context of the given process.
    /// Returns null if no mapping exists.
    /// </summary>
    public CompiledMapping? GetMapping(string processName, ModifierFlags triggerMods, int triggerVk)
    {
        var key = (triggerMods, triggerVk);

        if (_processMap.TryGetValue(processName, out var profileMap) &&
            profileMap.TryGetValue(key, out var mapping))
        {
            return mapping;
        }

        if (_defaultMappings.TryGetValue(key, out var defaultMapping))
            return defaultMapping;

        return null;
    }

    /// <summary>
    /// Returns true if the given trigger has any mapping in the given process context.
    /// </summary>
    public bool HasMapping(string processName, ModifierFlags triggerMods, int triggerVk)
    {
        return GetMapping(processName, triggerMods, triggerVk) != null;
    }

    public void StartWatching()
    {
        if (!Directory.Exists(_profilesDir)) return;

        _watcher = new FileSystemWatcher(_profilesDir, "*.json")
        {
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName,
            EnableRaisingEvents = true
        };

        _watcher.Changed += OnProfileFileChanged;
        _watcher.Created += OnProfileFileChanged;
        _watcher.Deleted += OnProfileFileChanged;
        _watcher.Renamed += (_, _) => ReloadDebounced();
    }

    private DateTime _lastReload = DateTime.MinValue;

    private void OnProfileFileChanged(object sender, FileSystemEventArgs e)
    {
        ReloadDebounced();
    }

    private void ReloadDebounced()
    {
        if ((DateTime.UtcNow - _lastReload).TotalSeconds < DebounceSeconds)
            return;

        _lastReload = DateTime.UtcNow;
        Logger.Info("Profile file change detected, reloading...");

        // Retry with delay to handle file-lock race conditions.
        // The FileSystemWatcher often fires while the writing process still holds the file.
        Task.Run(async () =>
        {
            for (int attempt = 1; attempt <= MaxReloadRetries; attempt++)
            {
                try
                {
                    await Task.Delay(ReloadRetryBaseDelayMs * attempt);
                    Load();
                    return;
                }
                catch (Exception ex)
                {
                    if (attempt == MaxReloadRetries)
                        Logger.Error($"Failed to reload profiles after {MaxReloadRetries} attempts: {ex.Message}");
                    else
                        Logger.Debug($"Reload attempt {attempt} failed (file may be locked), retrying...");
                }
            }
        });
    }

    private static Dictionary<(ModifierFlags, int), CompiledMapping> CompileMappings(List<KeyMappingDto> dtos)
    {
        var result = new Dictionary<(ModifierFlags, int), CompiledMapping>();

        foreach (var dto in dtos)
        {
            try
            {
                var (trigMods, trigVk) = KeyParser.ParseTrigger(dto.Trigger);

                CompiledMapping compiled;
                if (dto.Action.StartsWith(SpecialActionPrefix, StringComparison.OrdinalIgnoreCase))
                {
                    string actionName = dto.Action.Substring(SpecialActionPrefix.Length);
                    compiled = new CompiledMapping
                    {
                        TriggerModifiers = trigMods,
                        TriggerVk = trigVk,
                        SpecialActionName = actionName
                    };
                }
                else
                {
                    var (actModVks, actVk) = KeyParser.ParseAction(dto.Action);
                    compiled = new CompiledMapping
                    {
                        TriggerModifiers = trigMods,
                        TriggerVk = trigVk,
                        ActionModifierVks = actModVks,
                        ActionVk = actVk
                    };
                }

                result[(trigMods, trigVk)] = compiled;
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to compile mapping '{dto.Trigger}' -> '{dto.Action}': {ex.Message}");
            }
        }

        return result;
    }

    public void Dispose()
    {
        _watcher?.Dispose();
    }
}
