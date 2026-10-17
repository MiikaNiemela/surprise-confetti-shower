using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SurpriseConfettiShower;

public enum EffectKind { Confetti, Fireworks }

public enum TriggerMode { Surprise, Scheduled }

public enum ScheduleKind { EveryFullHour, SpecificTimes }

/// <summary>User preferences, persisted as JSON in %APPDATA%\SurpriseConfettiShower.</summary>
public sealed class Settings
{
    public EffectKind Effect { get; set; } = EffectKind.Confetti;

    public TriggerMode Mode { get; set; } = TriggerMode.Surprise;

    // Surprise mode: this many random effects per day, inside these whole hours.
    public int SurprisesPerDay { get; set; } = 3;
    public int ActiveFromHour { get; set; } = 9;
    public int ActiveUntilHour { get; set; } = 21;

    // Scheduled mode.
    public ScheduleKind Schedule { get; set; } = ScheduleKind.EveryFullHour;
    public List<TimeOnly> Times { get; set; } = [new(9, 0), new(12, 0), new(17, 0)];

    public bool StartWithWindows { get; set; } = true;

    /// <summary>Rolled surprise times for today and tomorrow, kept so a restart does not re-roll them.</summary>
    public List<DateTime> SurpriseSchedule { get; set; } = [];

    public const int MaxSurprisesPerDay = 12;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public static string FilePath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "SurpriseConfettiShower",
        "settings.json");

    /// <summary>Reads the settings file, falling back to defaults if it is missing or unreadable.</summary>
    public static Settings Load()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                var loaded = JsonSerializer.Deserialize<Settings>(File.ReadAllText(FilePath), JsonOptions);
                if (loaded is not null)
                {
                    loaded.Normalize();
                    return loaded;
                }
            }
        }
        catch (Exception)
        {
            // A hand-edited or half-written file should not stop the app from starting.
        }

        return new Settings();
    }

    /// <summary>Writes the settings file. Returns false if it could not be saved.</summary>
    public bool Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(this, JsonOptions));
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    public Settings Clone() =>
        JsonSerializer.Deserialize<Settings>(JsonSerializer.Serialize(this, JsonOptions), JsonOptions)!;

    /// <summary>Clamps values that came from the file, so odd input cannot break scheduling.</summary>
    public void Normalize()
    {
        SurprisesPerDay = Math.Clamp(SurprisesPerDay, 1, MaxSurprisesPerDay);
        ActiveFromHour = Math.Clamp(ActiveFromHour, 0, 23);
        ActiveUntilHour = Math.Clamp(ActiveUntilHour, ActiveFromHour + 1, 24);
        Times = [.. Times.Distinct().Order()];
        SurpriseSchedule ??= [];
    }
}
