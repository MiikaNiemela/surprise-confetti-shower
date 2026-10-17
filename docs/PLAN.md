# Surprise Confetti Shower — implementation plan

A tiny Windows tray app that drops a full-screen confetti shower (or fireworks) on you —
either at random "surprise" moments or on a schedule you pick. Built for
[Tiny Tool Town](https://www.tinytooltown.com/).

## 1. Requirements (from the brief)

| # | Requirement | Where it lands |
|---|---|---|
| R1 | Full-screen effect: confetti shower or fireworks | `Effects/*`, `EffectWindow` |
| R2 | Lives as a minimised tray icon | `App.xaml.cs` (`NotifyIcon`) |
| R3 | Setting: which effect | `Settings.Effect` |
| R4 | Trigger A: random "surprise" times, rolled for the next two days | `ScheduleCalculator.EnsureSurpriseSchedule` |
| R5 | Trigger B: scheduled — every full hour, or at times you pick | `ScheduleCalculator.Occurrences` |
| R6 | Events missed while the machine was off/asleep are **not** replayed | `ScheduleCalculator.FindDue` (grace window) |
| R7 | Settings view has a manual "Test effect" button | `SettingsWindow` |
| R8 | Open source, releasable on Tiny Tool Town | MIT `LICENSE`, `README.md`, GitHub Actions release |

## 2. Stack

**C# / .NET 10 (LTS) / WPF**, single project, `UseWPF` + `UseWindowsForms`.

- WPF gives a transparent, per-pixel-alpha full-screen overlay and a `DrawingContext`
  particle loop with no manual bitmap/interop work (a WinForms equivalent needs
  `UpdateLayeredWindow` plumbing).
- `UseWindowsForms` is on purely for `NotifyIcon` (tray) and `Screen.AllScreens`
  (monitor bounds in device pixels) — both ship in the same Desktop runtime, so it
  costs nothing in download size and saves ~150 lines of Shell_NotifyIcon interop.
- No NuGet dependencies at all.

## 3. Behaviour decisions

- **Overlay**: one borderless, topmost, transparent, click-through window **per monitor**
  (`WS_EX_TRANSPARENT | WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW`). It never takes focus and
  never swallows a click, so it cannot interrupt what you are doing. It closes itself when
  the animation ends (confetti ~6 s, fireworks ~7 s). There is no dismiss key — a
  non-activating window cannot receive one, and adding a global keyboard hook for a
  6-second animation is not worth it.
- **Missed events are skipped** (R6) by construction: a timer ticks every 20 s and asks
  "did an occurrence fall in `(lastCheck, now]`, and is it younger than the 2-minute grace
  window?". After sleep or shutdown the gap is large, so everything inside it is older than
  the grace window and is silently dropped. No wake/resume event handling needed.
- **Surprise schedule** (R4) is rolled for today + tomorrow, persisted in `settings.json`
  so a restart does not re-roll it, topped up at the day boundary, and re-rolled whenever
  you press Save. Times fall inside the configured active hours.
- **Active hours** are whole hours (combo boxes, 0–23). Avoids free-text time validation
  for a setting that nobody needs at minute precision.
- **Next effect time** is shown in the tray tooltip, not in the settings dialog — the
  tooltip always reflects saved state, so it can never show a stale value for unsaved edits.
  It is read when the pointer reaches the icon rather than pushed, so it stays correct even
  when an effect was skipped rather than played.
- **Who writes `settings.json`**: whoever edits a preference saves it (and reports a failed
  write); the `Scheduler` saves the surprise times it rolls, because it is the only thing that
  rolls them. `Scheduler.Reload` clears them so edited preferences get fresh times.
- **Single instance** via a named mutex: two trays would mean two overlays per event.
- **Autostart** is an `HKCU\...\Run` value, toggled by a checkbox (on by default — an app
  that surprises you is useless if it is not running).
- Settings live in `%APPDATA%\SurpriseConfettiShower\settings.json`.

## 4. Layout

```
src/SurpriseConfettiShower/
  SurpriseConfettiShower.csproj
  App.xaml / App.xaml.cs        tray icon, menu, wiring            [integrator]
  Settings.cs                   model + JSON load/save             [integrator]
  Autostart.cs                  HKCU Run key                       [agent: core]
  ScheduleCalculator.cs         pure scheduling logic               [agent: core]
  Scheduler.cs                  timer around the calculator        [agent: core]
  EffectPlayer.cs               opens/closes overlay windows       [agent: effects]
  EffectWindow.xaml(.cs)        per-monitor click-through overlay   [agent: effects]
  Effects/EffectCanvas.cs       render-loop base class             [agent: effects]
  Effects/ConfettiEffect.cs     confetti particles                 [agent: effects]
  Effects/FireworksEffect.cs    shells + bursts                    [agent: effects]
  SettingsWindow.xaml(.cs)      settings UI + Test button          [agent: ui]
  app.ico
tests/SurpriseConfettiShower.Tests/   ScheduleCalculator tests     [agent: core]
.github/workflows/build.yml           build + tagged release       [agent: docs]
README.md  LICENSE  .gitignore                                     [agent: docs]
```

## 5. Contracts (fixed; every agent codes against these)

```csharp
namespace SurpriseConfettiShower;

enum EffectKind { Confetti, Fireworks }
enum TriggerMode { Surprise, Scheduled }
enum ScheduleKind { EveryFullHour, SpecificTimes }

sealed class Settings {
    EffectKind Effect { get; set; }            // default Confetti
    TriggerMode Mode { get; set; }             // default Surprise
    int SurprisesPerDay { get; set; }          // 1..12, default 3
    int ActiveFromHour { get; set; }           // 0..23, default 9
    int ActiveUntilHour { get; set; }          // 1..24, default 21 (exclusive)
    ScheduleKind Schedule { get; set; }        // default EveryFullHour
    List<TimeOnly> Times { get; set; }         // used when Schedule == SpecificTimes
    bool StartWithWindows { get; set; }        // default true
    List<DateTime> SurpriseSchedule { get; set; }  // generated local times, persisted
    static string FilePath { get; }
    static Settings Load();                    // never throws; returns defaults on any error
    bool Save();                               // never throws; false if it could not be written
    Settings Clone();
}

static class ScheduleCalculator {
    IEnumerable<DateTime> Occurrences(Settings s, DateTime after, DateTime until); // ascending
    DateTime? FindDue(Settings s, DateTime lastCheck, DateTime now, TimeSpan grace);
    bool EnsureSurpriseSchedule(Settings s, DateTime now, Random rng); // true if mutated
    DateTime? Next(Settings s, DateTime now);
}

sealed class Scheduler : IDisposable {           // 20 s DispatcherTimer, 2 min grace
    Scheduler(Settings settings);
    event EventHandler? Due;                     // UI thread
    void Start();
    void Reload(Settings settings);              // after a save: re-rolls the surprise times
    DateTime? Next { get; }
}

static class Autostart { void Set(bool enabled); }

static class EffectPlayer { void Play(EffectKind kind); }

partial class SettingsWindow : Window {
    SettingsWindow(Settings current);  // edits a clone; Test button calls EffectPlayer.Play
    Settings? Result { get; }          // null unless the user saved
}
```

## 6. Work split

Four agents work in parallel on disjoint files; the integrator owns `App.xaml(.cs)`,
`Settings.cs`, the csproj and the icon, then compiles, runs and tunes the result.
Agents do not run builds (one builder avoids `obj/` contention).

## 7. Verification

1. `dotnet build -c Release` clean, `dotnet test` green.
2. `SurpriseConfettiShower.exe --test` plays the effect immediately; screen is captured
   during playback and inspected on both monitors.
3. Scheduler unit tests cover: full-hour occurrences, specific times, surprise generation
   inside active hours, two-day coverage, skipping of events older than the grace window
   (the sleep/shutdown case), and no double-fire.
4. Final pass with `/simplify` to strip anything unnecessary.
