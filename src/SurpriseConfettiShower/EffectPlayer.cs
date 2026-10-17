using System.Windows;
using WinForms = System.Windows.Forms;

namespace SurpriseConfettiShower;

/// <summary>Plays one effect at a time, across every monitor at once.</summary>
public static class EffectPlayer
{
    /// <summary>
    /// Opens a click-through overlay on each monitor, each running its own particle simulation
    /// and closing itself when the animation ends. Does nothing while an effect is still
    /// playing, so two triggers close together cannot stack two showers on one screen.
    /// </summary>
    public static void Play(EffectKind kind)
    {
        // WPF removes a window from this collection as it closes, so it already tracks the overlays.
        if (Application.Current.Windows.OfType<EffectWindow>().Any())
        {
            return;
        }

        foreach (var screen in WinForms.Screen.AllScreens)
        {
            new EffectWindow(screen.Bounds, kind).Show();
        }
    }
}
