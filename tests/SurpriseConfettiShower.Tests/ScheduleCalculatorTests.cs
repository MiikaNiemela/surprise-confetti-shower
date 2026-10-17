using Xunit;

namespace SurpriseConfettiShower.Tests;

/// <summary>
/// Fixed dates and seeded randomness throughout: the calculator takes "now" and the RNG as
/// parameters, so none of these tests depends on the wall clock.
/// </summary>
public class ScheduleCalculatorTests
{
    private static readonly TimeSpan Grace = TimeSpan.FromMinutes(2);

    [Fact]
    public void FullHourOccurrencesExcludeAfterAndIncludeUntil()
    {
        var hits = ScheduleCalculator.Occurrences(
            FullHours(),
            new DateTime(2026, 3, 10, 10, 0, 0),
            new DateTime(2026, 3, 10, 13, 0, 0));

        DateTime[] expected =
        [
            new(2026, 3, 10, 11, 0, 0),
            new(2026, 3, 10, 12, 0, 0),
            new(2026, 3, 10, 13, 0, 0),
        ];
        Assert.Equal(expected, hits);
    }

    [Fact]
    public void FullHourOccurrencesStartAtTheNextHourAndCrossMidnight()
    {
        var hits = ScheduleCalculator.Occurrences(
            FullHours(),
            new DateTime(2026, 3, 10, 22, 40, 0),
            new DateTime(2026, 3, 11, 1, 0, 0));

        DateTime[] expected =
        [
            new(2026, 3, 10, 23, 0, 0),
            new(2026, 3, 11, 0, 0, 0),
            new(2026, 3, 11, 1, 0, 0),
        ];
        Assert.Equal(expected, hits);
    }

    [Fact]
    public void SpecificTimesOccurOncePerTimePerDayInAscendingOrder()
    {
        // Deliberately unsorted input: the output must still be ascending.
        var s = AtTimes(new TimeOnly(20, 45), new TimeOnly(8, 15));

        var hits = ScheduleCalculator.Occurrences(
            s,
            new DateTime(2026, 3, 10, 12, 0, 0),
            new DateTime(2026, 3, 12, 9, 0, 0));

        DateTime[] expected =
        [
            new(2026, 3, 10, 20, 45, 0),
            new(2026, 3, 11, 8, 15, 0),
            new(2026, 3, 11, 20, 45, 0),
            new(2026, 3, 12, 8, 15, 0),
        ];
        Assert.Equal(expected, hits);
    }

    [Fact]
    public void AnEmptyTimesListSchedulesNothing()
    {
        var s = AtTimes();

        Assert.Empty(ScheduleCalculator.Occurrences(
            s,
            new DateTime(2026, 3, 10, 0, 0, 0),
            new DateTime(2026, 3, 20, 0, 0, 0)));
        Assert.Null(ScheduleCalculator.Next(s, new DateTime(2026, 3, 10, 12, 0, 0)));
    }

    [Fact]
    public void FindDueFiresAnOccurrenceThatJustPassed()
    {
        var due = ScheduleCalculator.FindDue(
            FullHours(),
            new DateTime(2026, 3, 10, 10, 59, 50),
            new DateTime(2026, 3, 10, 11, 0, 10),
            Grace);

        Assert.Equal(new DateTime(2026, 3, 10, 11, 0, 0), due);
    }

    [Fact]
    public void ScheduledOccurrencesMissedWhileAsleepAreNotReplayed()
    {
        // Eight hours of full hours sit between the two checks; every one is older than the grace
        // window, so nothing plays when the machine comes back.
        var due = ScheduleCalculator.FindDue(
            FullHours(),
            new DateTime(2026, 3, 10, 9, 30, 0),
            new DateTime(2026, 3, 10, 17, 30, 0),
            Grace);

        Assert.Null(due);
    }

    [Fact]
    public void SurprisesMissedWhileAsleepAreNotReplayed()
    {
        var s = WithSurprises(
            new DateTime(2026, 3, 10, 10, 17, 0),
            new DateTime(2026, 3, 10, 13, 42, 0),
            new DateTime(2026, 3, 10, 16, 5, 0));

        var due = ScheduleCalculator.FindDue(
            s,
            new DateTime(2026, 3, 10, 9, 30, 0),
            new DateTime(2026, 3, 10, 17, 30, 0),
            Grace);

        Assert.Null(due);
    }

    [Fact]
    public void FindDueIsNullWhenNothingFallsInTheWindow()
    {
        var due = ScheduleCalculator.FindDue(
            FullHours(),
            new DateTime(2026, 3, 10, 11, 29, 40),
            new DateTime(2026, 3, 10, 11, 30, 0),
            Grace);

        Assert.Null(due);
    }

    [Fact]
    public void FindDueDoesNotFireTheSameOccurrenceTwice()
    {
        var s = FullHours();
        var firstTick = new DateTime(2026, 3, 10, 11, 0, 10);

        Assert.Equal(
            new DateTime(2026, 3, 10, 11, 0, 0),
            ScheduleCalculator.FindDue(s, firstTick.AddSeconds(-20), firstTick, Grace));

        // The next tick is still inside the grace window, but lastCheck has moved past 11:00.
        Assert.Null(ScheduleCalculator.FindDue(s, firstTick, firstTick.AddSeconds(20), Grace));
    }

    [Fact]
    public void FindDueReturnsOnlyTheNewestOccurrenceInTheWindow()
    {
        var s = WithSurprises(
            new DateTime(2026, 3, 10, 11, 0, 0),
            new DateTime(2026, 3, 10, 11, 0, 30));

        var due = ScheduleCalculator.FindDue(
            s,
            new DateTime(2026, 3, 10, 10, 59, 50),
            new DateTime(2026, 3, 10, 11, 1, 0),
            Grace);

        Assert.Equal(new DateTime(2026, 3, 10, 11, 0, 30), due);
    }

    [Fact]
    public void EnsureSurpriseScheduleCoversTodayAndTomorrowAndDropsOlderDays()
    {
        var now = new DateTime(2026, 3, 10, 8, 0, 0);
        var s = new Settings
        {
            SurprisesPerDay = 3,
            ActiveFromHour = 9,
            ActiveUntilHour = 21,
            SurpriseSchedule = [new DateTime(2026, 3, 9, 10, 0, 0)],
        };

        Assert.True(ScheduleCalculator.EnsureSurpriseSchedule(s, now, new Random(1234)));

        Assert.Equal(3, s.SurpriseSchedule.Count(t => t.Date == now.Date));
        Assert.Equal(3, s.SurpriseSchedule.Count(t => t.Date == now.Date.AddDays(1)));
        Assert.Equal(6, s.SurpriseSchedule.Count);
        Assert.All(s.SurpriseSchedule, t =>
        {
            Assert.InRange(t, t.Date.AddHours(9), t.Date.AddHours(21).AddMinutes(-1));
            Assert.Equal(0, t.Second);
            Assert.Equal(0, t.Millisecond);
        });
        Assert.Equal(s.SurpriseSchedule.Order().ToArray(), s.SurpriseSchedule);
        Assert.Equal(s.SurpriseSchedule.Count, s.SurpriseSchedule.Distinct().Count());
    }

    [Fact]
    public void EnsureSurpriseScheduleIsIdempotentLaterTheSameDay()
    {
        var now = new DateTime(2026, 3, 10, 10, 0, 0);
        var s = new Settings { SurprisesPerDay = 4, ActiveFromHour = 9, ActiveUntilHour = 21 };
        ScheduleCalculator.EnsureSurpriseSchedule(s, now, new Random(7));
        var rolled = s.SurpriseSchedule.ToArray();

        // Four hours later some of today's times have elapsed; the day must not be re-rolled.
        Assert.False(ScheduleCalculator.EnsureSurpriseSchedule(s, now.AddHours(4), new Random(99)));
        Assert.Equal(rolled, s.SurpriseSchedule);
    }

    [Fact]
    public void EnsureSurpriseScheduleOnlyRollsFutureTimesForToday()
    {
        var now = new DateTime(2026, 3, 10, 18, 30, 20);
        var s = new Settings { SurprisesPerDay = 6, ActiveFromHour = 9, ActiveUntilHour = 21 };

        Assert.True(ScheduleCalculator.EnsureSurpriseSchedule(s, now, new Random(42)));

        Assert.Equal(6, s.SurpriseSchedule.Count(t => t.Date == now.Date));
        Assert.All(s.SurpriseSchedule.Where(t => t.Date == now.Date), t => Assert.True(t > now));
        // Tomorrow still gets the whole active window.
        Assert.Equal(6, s.SurpriseSchedule.Count(t => t.Date == now.Date.AddDays(1)));
    }

    [Fact]
    public void EnsureSurpriseScheduleRollsAsManyAsFitInWhatIsLeftOfToday()
    {
        var now = new DateTime(2026, 3, 10, 16, 56, 0);
        var s = new Settings { SurprisesPerDay = 8, ActiveFromHour = 9, ActiveUntilHour = 17 };

        Assert.True(ScheduleCalculator.EnsureSurpriseSchedule(s, now, new Random(3)));

        // Only 16:57, 16:58 and 16:59 are left before the window closes at 17:00.
        DateTime[] expected =
        [
            new(2026, 3, 10, 16, 57, 0),
            new(2026, 3, 10, 16, 58, 0),
            new(2026, 3, 10, 16, 59, 0),
        ];
        Assert.Equal(expected, s.SurpriseSchedule.Where(t => t.Date == now.Date).ToArray());
    }

    [Fact]
    public void EnsureSurpriseScheduleRollsNothingForTodayAfterTheActiveWindow()
    {
        var now = new DateTime(2026, 3, 10, 22, 0, 0);
        var s = new Settings { SurprisesPerDay = 3, ActiveFromHour = 9, ActiveUntilHour = 17 };

        Assert.True(ScheduleCalculator.EnsureSurpriseSchedule(s, now, new Random(5)));

        Assert.DoesNotContain(s.SurpriseSchedule, t => t.Date == now.Date);
        Assert.Equal(3, s.SurpriseSchedule.Count(t => t.Date == now.Date.AddDays(1)));
    }

    [Fact]
    public void NextIsTheUpcomingFullHour()
    {
        Assert.Equal(
            new DateTime(2026, 3, 10, 12, 0, 0),
            ScheduleCalculator.Next(FullHours(), new DateTime(2026, 3, 10, 11, 15, 0)));
    }

    [Fact]
    public void NextRollsOverToTomorrowForSpecificTimes()
    {
        var s = AtTimes(new TimeOnly(9, 0), new TimeOnly(17, 30));

        Assert.Equal(
            new DateTime(2026, 3, 10, 17, 30, 0),
            ScheduleCalculator.Next(s, new DateTime(2026, 3, 10, 12, 0, 0)));
        Assert.Equal(
            new DateTime(2026, 3, 11, 9, 0, 0),
            ScheduleCalculator.Next(s, new DateTime(2026, 3, 10, 18, 0, 0)));
    }

    [Fact]
    public void NextIsTheUpcomingSurpriseOrNullWhenNoneAreLeft()
    {
        var s = WithSurprises(
            new DateTime(2026, 3, 10, 10, 0, 0),
            new DateTime(2026, 3, 10, 15, 0, 0));

        Assert.Equal(
            new DateTime(2026, 3, 10, 15, 0, 0),
            ScheduleCalculator.Next(s, new DateTime(2026, 3, 10, 11, 0, 0)));
        Assert.Null(ScheduleCalculator.Next(s, new DateTime(2026, 3, 10, 16, 0, 0)));
    }

    private static Settings FullHours() =>
        new() { Mode = TriggerMode.Scheduled, Schedule = ScheduleKind.EveryFullHour };

    private static Settings AtTimes(params TimeOnly[] times) =>
        new() { Mode = TriggerMode.Scheduled, Schedule = ScheduleKind.SpecificTimes, Times = [.. times] };

    private static Settings WithSurprises(params DateTime[] times) =>
        new() { Mode = TriggerMode.Surprise, SurpriseSchedule = [.. times] };
}
