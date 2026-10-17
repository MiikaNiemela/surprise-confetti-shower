using System.Windows.Threading;

namespace SurpriseConfettiShower;

/// <summary>
/// The timer around <see cref="ScheduleCalculator"/>: it polls in local time and raises
/// <see cref="Due"/> when an effect should play right now.
/// </summary>
public sealed class Scheduler : IDisposable
{
    private static readonly TimeSpan TickInterval = TimeSpan.FromSeconds(20);

    // Must comfortably exceed TickInterval, or an occurrence could fall between two ticks and be
    // missed; everything older than this was missed while the machine was off and is dropped.
    private static readonly TimeSpan GraceWindow = TimeSpan.FromMinutes(2);

    private readonly DispatcherTimer _timer;
    private readonly Random _rng = new();
    private Settings _settings;
    private DateTime _lastCheck;

    /// <summary>Constructed on the UI thread, so the timer ticks there and so does <see cref="Due"/>.</summary>
    public Scheduler(Settings settings)
    {
        _settings = settings;
        _timer = new DispatcherTimer { Interval = TickInterval };
        _timer.Tick += (_, _) => Tick();
    }

    public event EventHandler? Due;

    public DateTime? Next => ScheduleCalculator.Next(_settings, DateTime.Now);

    public void Start()
    {
        // Nothing from before the app started may fire.
        _lastCheck = DateTime.Now;
        EnsureSchedule(_lastCheck);
        _timer.Start();
    }

    public void Reload(Settings settings)
    {
        _settings = settings;

        // Edited preferences deserve fresh times: the stored ones were rolled for the old ones.
        _settings.SurpriseSchedule.Clear();
        _lastCheck = DateTime.Now;
        EnsureSchedule(_lastCheck);
    }

    public void Dispose() => _timer.Stop();

    private void Tick()
    {
        var now = DateTime.Now;
        EnsureSchedule(now);

        if (ScheduleCalculator.FindDue(_settings, _lastCheck, now, GraceWindow) is not null)
        {
            Due?.Invoke(this, EventArgs.Empty);
        }

        _lastCheck = now;
    }

    private void EnsureSchedule(DateTime now)
    {
        // Persisted immediately, so a restart replays today's rolled times instead of new ones.
        if (ScheduleCalculator.EnsureSurpriseSchedule(_settings, now, _rng))
        {
            _settings.Save();
        }
    }
}
