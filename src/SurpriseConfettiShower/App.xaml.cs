using System.Windows;
using Drawing = System.Drawing;
using WinForms = System.Windows.Forms;

namespace SurpriseConfettiShower;

/// <summary>
/// The app has no main window: it lives in the notification area, waits for the scheduler,
/// and opens the settings window on demand.
/// </summary>
public partial class App : Application
{
    private const string AppName = "Surprise Confetti Shower";

    private Mutex? _singleInstance;
    private WinForms.NotifyIcon? _tray;
    private Scheduler? _scheduler;
    private Settings _settings = new();
    private bool _settingsOpen;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _singleInstance = new Mutex(initiallyOwned: true, @"Local\SurpriseConfettiShower", out bool isOnlyInstance);
        if (!isOnlyInstance)
        {
            Shutdown();
            return;
        }

        ShutdownMode = ShutdownMode.OnExplicitShutdown;
        _settings = Settings.Load();
        Autostart.Set(_settings.StartWithWindows);

        _tray = CreateTrayIcon();

        _scheduler = new Scheduler(_settings);
        _scheduler.Due += OnDue;
        _scheduler.Start();
        UpdateTooltip();

        // Lets you preview an effect without opening the settings window.
        if (e.Args.Contains("--test"))
        {
            EffectPlayer.Play(_settings.Effect);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _scheduler?.Dispose();

        if (_tray is not null)
        {
            _tray.Visible = false;
            _tray.Dispose();
        }

        _singleInstance?.Dispose();
        base.OnExit(e);
    }

    private WinForms.NotifyIcon CreateTrayIcon()
    {
        var menu = new WinForms.ContextMenuStrip();
        menu.Items.Add("Test effect", null, (_, _) => EffectPlayer.Play(_settings.Effect));
        menu.Items.Add("Settings...", null, (_, _) => ShowSettings());
        menu.Items.Add(new WinForms.ToolStripSeparator());
        menu.Items.Add("Exit", null, (_, _) => Shutdown());

        var tray = new WinForms.NotifyIcon
        {
            Icon = LoadTrayIcon(),
            ContextMenuStrip = menu,
            Visible = true,
        };
        tray.DoubleClick += (_, _) => ShowSettings();
        return tray;
    }

    private static Drawing.Icon LoadTrayIcon()
    {
        using var stream = GetResourceStream(new Uri("app.ico", UriKind.Relative))!.Stream;
        return new Drawing.Icon(stream, WinForms.SystemInformation.SmallIconSize);
    }

    private void ShowSettings()
    {
        if (_settingsOpen)
        {
            return;
        }

        _settingsOpen = true;
        try
        {
            var dialog = new SettingsWindow(_settings);
            dialog.ShowDialog();

            if (dialog.Result is not { } saved)
            {
                return;
            }

            // Any save re-rolls the surprise times, so changed active hours take effect at once.
            saved.SurpriseSchedule.Clear();
            _settings = saved;

            if (!_settings.Save())
            {
                MessageBox.Show(
                    $"Could not write {Settings.FilePath}, so these settings will be forgotten when the app restarts.",
                    AppName, MessageBoxButton.OK, MessageBoxImage.Warning);
            }

            Autostart.Set(_settings.StartWithWindows);
            _scheduler!.Reload(_settings);
            UpdateTooltip();
        }
        finally
        {
            _settingsOpen = false;
        }
    }

    private void OnDue(object? sender, EventArgs e)
    {
        EffectPlayer.Play(_settings.Effect);
        UpdateTooltip();
    }

    private void UpdateTooltip()
    {
        if (_tray is null)
        {
            return;
        }

        _tray.Text = _scheduler?.Next is { } next
            ? $"{AppName} - next {next:ddd HH:mm}"
            : $"{AppName} - nothing scheduled";
    }
}
