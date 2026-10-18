<img src="docs/img/icon.png" alt="" width="88" align="right">

# Surprise Confetti Shower

[![build](https://github.com/MiikaNiemela/surprise-confetti-shower/actions/workflows/build.yml/badge.svg)](https://github.com/MiikaNiemela/surprise-confetti-shower/actions/workflows/build.yml)

A tiny Windows tray app that drops a full-screen confetti shower on you when you
least expect it. Your screen deserves a small celebration now and then, and
nobody else is going to throw one. 🎉

![A confetti shower falling across the desktop, with the settings window open](docs/img/Screenshot-v0.1.1.png)

## Features

- Confetti shower or fireworks, your pick.
- Full screen, across every monitor you have.
- Click-through: the overlay never takes focus and never swallows a click, so it
  cannot interrupt what you are doing. It tidies itself away after a few seconds.
- Surprise mode (random times) or a fixed schedule.
- A Test effect button, for when you need celebrating *right now*.
- Starts with Windows, so it is actually there to surprise you.
- One tiny exe. No telemetry, no network access at all.

## Install

Grab one of these from the
[Releases page](https://github.com/MiikaNiemela/surprise-confetti-shower/releases) and run it.
Windows 10 or 11, x64.

| Download | Size | Needs |
| --- | --- | --- |
| `SurpriseConfettiShower.exe` | ~72 MB | Nothing at all - the runtime is baked in. |
| `SurpriseConfettiShower-framework-dependent.exe` | ~240 KB | The [.NET 10 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/10.0) |

Take the first one unless you already have the .NET Desktop Runtime, or you mind 72 MB for a
confetti cannon. Either way there is no installer: it tucks itself into the notification area
(the tray, next to the clock) and waits.

## Using it

Right-click the tray icon for **Test effect**, **Settings...** and **Exit**.
Double-click opens the settings too, and the tooltip tells you when the next
effect is due.

| Setting | What it does |
| --- | --- |
| Effect | Confetti shower or fireworks. |
| Trigger | Surprise or Scheduled - see below. |
| Surprises per day | How many random effects you get, 1-12. Surprise mode only. |
| Active hours | The window the surprises fall inside, whole hours only, so you are not confettied at 04:00. |
| Schedule | Every full hour, or a list of times you pick. Scheduled mode only. |
| Start with Windows | Adds or removes an `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` entry. On by default. |

The two trigger modes:

- **Surprise** - the app rolls your random times for today *and* tomorrow and
  remembers them, so restarting does not shuffle the deck. Tomorrow's times are
  topped up at the day boundary. Pressing **Save** re-rolls everything, so new
  active hours take effect immediately.
- **Scheduled** - no randomness. Either every full hour, or exactly the times
  you listed, every day.

Your preferences live in `%APPDATA%\SurpriseConfettiShower\settings.json`.
Running `SurpriseConfettiShower.exe --test` plays the configured effect straight
away, which is handy for previewing.

## Events you missed stay missed

If your machine was off or asleep when an effect was due, it is skipped - not
replayed in a pile the moment you wake up. The scheduler only fires events that
are less than two minutes old; anything staler is dropped quietly. Come back to
a laptop after the weekend and you get a calm desktop, not three days of backlog.

## Build it yourself

Needs the .NET 10 SDK on Windows.

```sh
dotnet build                                      # the app and the tests
dotnet test                                       # scheduler unit tests
dotnet run --project src/SurpriseConfettiShower   # try it from source
```

The single-file exe on the Releases page is produced by the same command the
workflow runs:

```sh
dotnet publish src/SurpriseConfettiShower -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true
```

## Licence

MIT - see [LICENSE](LICENSE). Built with love for
[Tiny Tool Town](https://www.tinytooltown.com/).
