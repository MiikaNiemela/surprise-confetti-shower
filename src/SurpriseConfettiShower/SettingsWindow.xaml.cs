using System.Windows;
using System.Windows.Controls;

namespace SurpriseConfettiShower;

/// <summary>
/// The app's only window: a modal dialog that edits a copy of the settings and hands it
/// back through <see cref="Result"/> if the user saves.
/// </summary>
public partial class SettingsWindow : Window
{
    // "9:00" is accepted as well as "09:00"; the escaped colon keeps the separator culture-independent.
    private static readonly string[] TimeFormats = ["HH\\:mm", "H\\:mm"];

    private readonly Settings _edited;

    public SettingsWindow(Settings current)
    {
        InitializeComponent();

        _edited = current.Clone();
        LoadControls();
    }

    /// <summary>The edited settings, or null if the user cancelled or closed the window.</summary>
    public Settings? Result { get; private set; }

    private EffectKind SelectedEffect =>
        ConfettiOption.IsChecked == true ? EffectKind.Confetti : EffectKind.Fireworks;

    private void LoadControls()
    {
        ConfettiOption.IsChecked = _edited.Effect == EffectKind.Confetti;
        FireworksOption.IsChecked = _edited.Effect == EffectKind.Fireworks;

        SurpriseOption.IsChecked = _edited.Mode == TriggerMode.Surprise;
        ScheduleOption.IsChecked = _edited.Mode == TriggerMode.Scheduled;

        // Every combo box holds one item per allowed value, so the value is the index plus the lowest value.
        PerDayBox.ItemsSource = Enumerable.Range(1, Settings.MaxSurprisesPerDay).ToArray();
        PerDayBox.SelectedIndex = _edited.SurprisesPerDay - 1;
        FromHourBox.ItemsSource = HourLabels(0);
        FromHourBox.SelectedIndex = _edited.ActiveFromHour;
        UntilHourBox.ItemsSource = HourLabels(1);
        UntilHourBox.SelectedIndex = _edited.ActiveUntilHour - 1;

        EveryHourOption.IsChecked = _edited.Schedule == ScheduleKind.EveryFullHour;
        SpecificTimesOption.IsChecked = _edited.Schedule == ScheduleKind.SpecificTimes;
        TimesBox.Text = string.Join(", ", _edited.Times.Select(t => t.ToString(TimeFormats[0])));

        StartWithWindowsBox.IsChecked = _edited.StartWithWindows;
    }

    /// <summary>24 whole hours starting at <paramref name="first"/>, so "until" can offer 24:00 (midnight).</summary>
    private static string[] HourLabels(int first) => [.. Enumerable.Range(first, 24).Select(h => $"{h:00}:00")];

    private void ReadControls(List<TimeOnly> times)
    {
        _edited.Effect = SelectedEffect;
        _edited.Mode = SurpriseOption.IsChecked == true ? TriggerMode.Surprise : TriggerMode.Scheduled;
        _edited.SurprisesPerDay = PerDayBox.SelectedIndex + 1;
        _edited.ActiveFromHour = FromHourBox.SelectedIndex;
        _edited.ActiveUntilHour = UntilHourBox.SelectedIndex + 1;
        _edited.Schedule = EveryHourOption.IsChecked == true ? ScheduleKind.EveryFullHour : ScheduleKind.SpecificTimes;
        _edited.StartWithWindows = StartWithWindowsBox.IsChecked == true;

        if (times.Count > 0)
        {
            // An empty box keeps the times that were stored, so switching modes cannot lose them.
            _edited.Times = times;
        }

        _edited.Normalize();
    }

    private void OnSave(object sender, RoutedEventArgs e)
    {
        var times = ParseTimes(TimesBox.Text);

        // Only the trigger actually in use is validated, so an unused times box cannot block a save.
        if (times.Count == 0 && ScheduleOption.IsChecked == true && SpecificTimesOption.IsChecked == true)
        {
            TimesError.Visibility = Visibility.Visible;
            TimesBox.Focus();
            return;
        }

        ReadControls(times);
        Result = _edited;
        Close();
    }

    private void OnTestEffect(object sender, RoutedEventArgs e) => EffectPlayer.Play(SelectedEffect);

    private void OnTimesChanged(object sender, TextChangedEventArgs e) =>
        TimesError.Visibility = Visibility.Collapsed;

    /// <summary>The times listed in the text box, or none at all if any entry is not an HH:mm time.</summary>
    private static List<TimeOnly> ParseTimes(string text)
    {
        List<TimeOnly> times = [];

        foreach (var part in text.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            if (!TimeOnly.TryParseExact(part, TimeFormats, out var time))
            {
                return [];
            }

            times.Add(time);
        }

        return times;
    }
}
