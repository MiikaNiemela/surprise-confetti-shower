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
    private SettingsWindow? _settingsWindow;

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
        _scheduler.Due += (_, _) => EffectPlayer.Play(_settings.Effect);
        _scheduler.Start();

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
            Text = AppName,
            Visible = true,
        };
        tray.DoubleClick += (_, _) => ShowSettings();

        // Read when the pointer arrives rather than pushed from a handful of places, so it cannot
        // go stale - including after an effect was skipped because the machine was asleep.
        tray.MouseMove += (_, _) => tray.Text = TooltipText();
        return tray;
    }

    private static Drawing.Icon LoadTrayIcon()
    {
        using var stream = GetResourceStream(new Uri("app.ico", UriKind.Relative))!.Stream;
        return new Drawing.Icon(stream, WinForms.SystemInformation.SmallIconSize);
    }

    private void ShowSettings()
    {
        // The tray menu is not an owned window, so ShowDialog alone does not stop a second click.
        if (_settingsWindow is not null)
        {
            _settingsWindow.Activate();
            return;
        }

        _settingsWindow = new SettingsWindow(_settings);
        try
        {
            _settingsWindow.ShowDialog();

            if (_settingsWindow.Result is not { } saved)
            {
                return;
            }

            _settings = saved;

            if (!_settings.Save())
            {
                MessageBox.Show(
                    $"Could not write {Settings.FilePath}, so these settings will be forgotten when the app restarts.",
                    AppName, MessageBoxButton.OK, MessageBoxImage.Warning);
            }

            Autostart.Set(_settings.StartWithWindows);
            _scheduler!.Reload(_settings);
        }
        finally
        {
            _settingsWindow = null;
        }
    }

    private string TooltipText() =>
        _scheduler?.Next is { } next
            ? $"{AppName} - next {next:ddd HH:mm}"
            : $"{AppName} - nothing scheduled";
}
