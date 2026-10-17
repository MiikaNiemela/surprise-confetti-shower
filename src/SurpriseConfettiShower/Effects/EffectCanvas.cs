using System.Windows;
using System.Windows.Media;

namespace SurpriseConfettiShower;

/// <summary>
/// Runs a particle simulation on the WPF render loop: one <see cref="Update"/> and one
/// <see cref="Draw"/> per frame, stopping at <see cref="IsFinished"/> or
/// <see cref="MaxDuration"/>, whichever comes first.
/// </summary>
public abstract class EffectCanvas : FrameworkElement
{
    private const double MaxFrameStep = 0.05;

    private TimeSpan _previousFrame = TimeSpan.MinValue;
    private double _elapsed;
    private bool _subscribed;
    private bool _finished;

    protected EffectCanvas()
    {
        Loaded += (_, _) => Subscribe();
        // CompositionTarget.Rendering is a static event: a missed unsubscribe keeps this canvas
        // alive and burning a frame's worth of CPU for the rest of the process's life.
        Unloaded += (_, _) => Unsubscribe();
    }

    /// <summary>Raised once, on the UI thread, when the effect has played out.</summary>
    public event EventHandler? Finished;

    /// <summary>Advances the simulation by <paramref name="dt"/> seconds.</summary>
    protected abstract void Update(double dt, Size size);

    protected abstract void Draw(DrawingContext dc, Size size);

    /// <summary>True once nothing visible is left.</summary>
    protected abstract bool IsFinished { get; }

    /// <summary>Hard stop, so an overlay can never be left on screen.</summary>
    protected abstract TimeSpan MaxDuration { get; }

    /// <summary>A frozen brush is shareable and cheap to draw with; a per-particle brush is not.</summary>
    protected static SolidColorBrush Frozen(uint rgb)
    {
        var brush = new SolidColorBrush(Color.FromRgb((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb));
        brush.Freeze();
        return brush;
    }

    protected static double Rand(double min, double max) => min + (Random.Shared.NextDouble() * (max - min));

    protected sealed override void OnRender(DrawingContext drawingContext) =>
        Draw(drawingContext, new Size(ActualWidth, ActualHeight));

    private void OnRendering(object? sender, EventArgs e)
    {
        var frame = ((RenderingEventArgs)e).RenderingTime;

        // WPF raises Rendering more than once per frame, with the same timestamp each time.
        if (frame == _previousFrame)
        {
            return;
        }

        double dt = _previousFrame == TimeSpan.MinValue ? 0 : (frame - _previousFrame).TotalSeconds;
        _previousFrame = frame;

        if (ActualWidth <= 0 || ActualHeight <= 0)
        {
            return;
        }

        // A stall must not teleport every particle across the screen.
        dt = Math.Min(dt, MaxFrameStep);
        _elapsed += dt;

        Update(dt, new Size(ActualWidth, ActualHeight));
        InvalidateVisual();

        if (IsFinished || _elapsed >= MaxDuration.TotalSeconds)
        {
            _finished = true;
            Unsubscribe();
            Finished?.Invoke(this, EventArgs.Empty);
        }
    }

    private void Subscribe()
    {
        if (_subscribed || _finished)
        {
            return;
        }

        _subscribed = true;
        CompositionTarget.Rendering += OnRendering;
    }

    private void Unsubscribe()
    {
        if (!_subscribed)
        {
            return;
        }

        _subscribed = false;
        CompositionTarget.Rendering -= OnRendering;
    }
}
