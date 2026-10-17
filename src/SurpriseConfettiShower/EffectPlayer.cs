using WinForms = System.Windows.Forms;

namespace SurpriseConfettiShower;

/// <summary>Plays one effect at a time, across every monitor at once.</summary>
public static class EffectPlayer
{
    private static int _openOverlays;

    /// <summary>True from the moment the overlays open until the last one has closed itself.</summary>
    public static bool IsPlaying => _openOverlays > 0;

    /// <summary>
    /// Opens a click-through overlay on each monitor, each running its own particle simulation
    /// and closing itself when the animation ends. Does nothing while an effect is still
    /// playing, so two triggers close together cannot stack two showers on one screen.
    /// </summary>
    public static void Play(EffectKind kind)
    {
        if (IsPlaying)
        {
            return;
        }

        foreach (var screen in WinForms.Screen.AllScreens)
        {
            var window = new EffectWindow(screen.Bounds, kind);
            window.Closed += (_, _) => _openOverlays--;
            _openOverlays++;
            window.Show();
        }
    }
}
