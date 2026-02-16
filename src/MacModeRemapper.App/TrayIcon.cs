using System.Drawing;
using System.IO;
using System.Windows;
using System.Windows.Threading;
using MacModeRemapper.Core.Engine;
using MacModeRemapper.Core.Hook;
using MacModeRemapper.Core.Logging;
using MacModeRemapper.Core.ProcessDetection;
using MacModeRemapper.Core.Profiles;
using MacModeRemapper.Core.Settings;
using NotifyIcon = System.Windows.Forms.NotifyIcon;
using ToolStripMenuItem = System.Windows.Forms.ToolStripMenuItem;
using ContextMenuStrip = System.Windows.Forms.ContextMenuStrip;
using ToolStripSeparator = System.Windows.Forms.ToolStripSeparator;

namespace MacModeRemapper.App;

/// <summary>
/// Manages the system tray icon, context menu, and orchestrates all core components.
/// </summary>
public sealed class TrayIcon : IDisposable
{
    private readonly NotifyIcon _notifyIcon;
    private readonly ContextMenuStrip _contextMenu;
    private readonly ToolStripMenuItem _toggleItem;
    private readonly ToolStripMenuItem _suspendItem;
    private readonly ToolStripMenuItem _startOnLoginItem;

    private readonly KeyboardHook _hook;
    private readonly MappingEngine _engine;
    private readonly ProfileManager _profiles;
    private readonly ForegroundProcessDetector _processDetector;
    private readonly SettingsManager _settings;

    private DispatcherTimer? _suspendTimer;
    private bool _disposed;

    public TrayIcon()
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

        // Initialize components
        Logger.Initialize(enableDebug: true);

        _settings = new SettingsManager(settingsPath);
        _settings.Load();
        Logger.SetDebugEnabled(_settings.Current.DebugLogging);

        _profiles = new ProfileManager(profilesDir);
        _profiles.Load();
        _profiles.StartWatching();

        _processDetector = new ForegroundProcessDetector();
        _engine = new MappingEngine(_profiles, _processDetector);
        _engine.Enabled = _settings.Current.MacModeEnabled;
        _engine.PanicKeyPressed += OnPanicKey;

        _hook = new KeyboardHook();
        _hook.KeyEvent += OnKeyEvent;

        // Build tray menu
        _contextMenu = new ContextMenuStrip();

        _toggleItem = new ToolStripMenuItem(_engine.Enabled ? "Mac Mode: ON" : "Mac Mode: OFF");
        _toggleItem.Click += OnToggleMacMode;

        _suspendItem = new ToolStripMenuItem("Suspend (10 min)");
        _suspendItem.Click += OnSuspend;

        _startOnLoginItem = new ToolStripMenuItem("Start on Login")
        {
            Checked = StartupManager.IsStartOnLoginEnabled()
        };
        _startOnLoginItem.Click += OnStartOnLogin;

        var exitItem = new ToolStripMenuItem("Exit");
        exitItem.Click += OnExit;

        _contextMenu.Items.Add(_toggleItem);
        _contextMenu.Items.Add(_suspendItem);
        _contextMenu.Items.Add(new ToolStripSeparator());
        _contextMenu.Items.Add(_startOnLoginItem);
        _contextMenu.Items.Add(new ToolStripSeparator());
        _contextMenu.Items.Add(exitItem);

        _notifyIcon = new NotifyIcon
        {
            Text = "Mac Mode Remapper",
            Icon = CreateTrayIcon(_engine.Enabled),
            ContextMenuStrip = _contextMenu,
            Visible = true
        };

        _notifyIcon.DoubleClick += (_, _) => OnToggleMacMode(this, EventArgs.Empty);

        // Install the hook
        _hook.Install();
    }

    private void OnKeyEvent(object? sender, KeyboardHookEventArgs e)
    {
        bool suppress = _engine.ProcessKeyEvent(e);
        if (suppress)
            e.Handled = true;
    }

    private void OnPanicKey()
    {
        _engine.Enabled = false;
        _settings.Current.MacModeEnabled = false;
        _settings.Save();
        UpdateTrayState();

        _notifyIcon.ShowBalloonTip(3000, "Mac Mode Remapper",
            "Mac Mode disabled (panic key).", System.Windows.Forms.ToolTipIcon.Warning);
    }

    private void OnToggleMacMode(object? sender, EventArgs e)
    {
        _engine.Enabled = !_engine.Enabled;
        _settings.Current.MacModeEnabled = _engine.Enabled;
        _settings.Save();
        UpdateTrayState();

        // Cancel any active suspend
        _suspendTimer?.Stop();
        _suspendTimer = null;
        _suspendItem.Text = "Suspend (10 min)";
    }

    private void OnSuspend(object? sender, EventArgs e)
    {
        if (_suspendTimer != null)
        {
            // Already suspended, cancel it
            _suspendTimer.Stop();
            _suspendTimer = null;
            _engine.Enabled = true;
            _settings.Current.MacModeEnabled = true;
            _settings.Save();
            _suspendItem.Text = "Suspend (10 min)";
            UpdateTrayState();
            Logger.Info("Suspend cancelled.");
            return;
        }

        _engine.Enabled = false;
        _suspendItem.Text = "Resume (suspended)";
        UpdateTrayState();
        Logger.Info("Mac Mode suspended for 10 minutes.");

        _suspendTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMinutes(10)
        };
        _suspendTimer.Tick += (_, _) =>
        {
            _suspendTimer.Stop();
            _suspendTimer = null;
            _engine.Enabled = true;
            _settings.Current.MacModeEnabled = true;
            _settings.Save();
            _suspendItem.Text = "Suspend (10 min)";
            UpdateTrayState();
            Logger.Info("Suspend expired, Mac Mode re-enabled.");

            _notifyIcon.ShowBalloonTip(2000, "Mac Mode Remapper",
                "Mac Mode re-enabled after suspend.", System.Windows.Forms.ToolTipIcon.Info);
        };
        _suspendTimer.Start();
    }

    private void OnStartOnLogin(object? sender, EventArgs e)
    {
        bool newValue = !_startOnLoginItem.Checked;
        StartupManager.SetStartOnLogin(newValue);
        _startOnLoginItem.Checked = newValue;
        _settings.Current.StartOnLogin = newValue;
        _settings.Save();
    }

    private void OnExit(object? sender, EventArgs e)
    {
        Dispose();
        System.Windows.Application.Current.Shutdown();
    }

    private void UpdateTrayState()
    {
        _toggleItem.Text = _engine.Enabled ? "Mac Mode: ON" : "Mac Mode: OFF";
        _notifyIcon.Icon = CreateTrayIcon(_engine.Enabled);
        _notifyIcon.Text = _engine.Enabled ? "Mac Mode Remapper (ON)" : "Mac Mode Remapper (OFF)";
    }

    /// <summary>
    /// Creates a simple tray icon programmatically (green circle = ON, gray circle = OFF).
    /// </summary>
    private static Icon CreateTrayIcon(bool enabled)
    {
        var bmp = new Bitmap(16, 16);
        using (var g = Graphics.FromImage(bmp))
        {
            g.Clear(Color.Transparent);
            var color = enabled ? Color.FromArgb(0, 200, 83) : Color.FromArgb(158, 158, 158);
            using var brush = new SolidBrush(color);
            g.FillEllipse(brush, 1, 1, 14, 14);

            // Draw a small "M" in white
            using var font = new Font("Segoe UI", 7f, System.Drawing.FontStyle.Bold);
            using var textBrush = new SolidBrush(Color.White);
            var sf = new StringFormat
            {
                Alignment = StringAlignment.Center,
                LineAlignment = StringAlignment.Center
            };
            g.DrawString("M", font, textBrush, new RectangleF(0, 0, 16, 16), sf);
        }

        return Icon.FromHandle(bmp.GetHicon());
    }

    /// <summary>
    /// Walks up from the bin directory to find the profiles folder at solution root.
    /// </summary>
    private static string FindProfilesDir(string startDir)
    {
        string? dir = startDir;
        for (int i = 0; i < 8 && dir != null; i++)
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

        _suspendTimer?.Stop();
        _hook.KeyEvent -= OnKeyEvent;
        _hook.Dispose();
        _profiles.Dispose();
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        _contextMenu.Dispose();
        Logger.Shutdown();
    }
}
