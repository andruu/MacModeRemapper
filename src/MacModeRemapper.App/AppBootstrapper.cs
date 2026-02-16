using System.IO;
using MacModeRemapper.Core.Engine;
using MacModeRemapper.Core.Hook;
using MacModeRemapper.Core.Logging;
using MacModeRemapper.Core.ProcessDetection;
using MacModeRemapper.Core.Profiles;
using MacModeRemapper.Core.Settings;

namespace MacModeRemapper.App;

/// <summary>
/// Composition root: resolves paths, initializes logging, and creates all
/// core services. Separates dependency wiring from UI concerns (TrayIcon).
/// </summary>
public sealed class AppBootstrapper : IDisposable
{
    private const int MaxDirTraversalDepth = 8;
    private bool _disposed;

    public KeyboardHook Hook { get; }
    public MappingEngine Engine { get; }
    public ProfileManager Profiles { get; }
    public SettingsManager Settings { get; }

    public AppBootstrapper()
    {
        // Resolve paths
        string baseDir = AppContext.BaseDirectory;
        string profilesDir = Path.Combine(baseDir, "profiles");
        string settingsPath = Path.Combine(baseDir, "settings.json");

        // If running from dev (bin/Debug/...), look for profiles at solution root
        if (!Directory.Exists(profilesDir))
        {
            string devProfilesDir = FindProfilesDir(baseDir);
            if (Directory.Exists(devProfilesDir))
                profilesDir = devProfilesDir;
        }

        // Initialize logging
        Logger.Initialize(enableDebug: true);

        // Initialize settings
        Settings = new SettingsManager(settingsPath);
        Settings.Load();
        Logger.SetDebugEnabled(Settings.Current.DebugLogging);

        // Initialize profiles
        Profiles = new ProfileManager(profilesDir);
        Profiles.Load();
        Profiles.StartWatching();

        // Initialize engine
        var processDetector = new ForegroundProcessDetector();
        Engine = new MappingEngine(Profiles, processDetector);
        Engine.Enabled = Settings.Current.MacModeEnabled;

        // Initialize hook
        Hook = new KeyboardHook();
    }

    /// <summary>
    /// Walks up from the bin directory to find the profiles folder at solution root.
    /// </summary>
    private static string FindProfilesDir(string startDir)
    {
        string? dir = startDir;
        for (int i = 0; i < MaxDirTraversalDepth && dir != null; i++)
        {
            string candidate = Path.Combine(dir, "profiles");
            if (Directory.Exists(candidate))
                return candidate;
            dir = Path.GetDirectoryName(dir);
        }
        return Path.Combine(startDir, "profiles");
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        Hook.Dispose();
        Profiles.Dispose();
        Logger.Shutdown();
    }
}
