namespace SurpriseConfettiShower;

/// <summary>
/// All scheduling arithmetic, in local wall-clock time. Pure on purpose: the current time and the
/// random source are parameters, so every rule below can be unit-tested without a clock.
/// </summary>
public static class ScheduleCalculator
{
    /// <summary>Occurrences in <c>(after, until]</c>, ascending.</summary>
    public static IEnumerable<DateTime> Occurrences(Settings s, DateTime after, DateTime until) =>
        s.Mode == TriggerMode.Surprise
            ? s.SurpriseSchedule.Where(t => t > after && t <= until).Order()
            : ScheduledOccurrences(s, after, until);

    /// <summary>
    /// The newest occurrence in <c>(lastCheck, now]</c> that is still younger than
    /// <paramref name="grace"/>, or null. This one rule implements "missed effects are not
    /// replayed": after sleep or a shutdown the gap between the checks is large, so every
    /// occurrence inside it is older than the grace window and is dropped. At most one occurrence
    /// is ever returned, and a caller that advances <paramref name="lastCheck"/> to
    /// <paramref name="now"/> can never see the same one twice.
    /// </summary>
    public static DateTime? FindDue(Settings s, DateTime lastCheck, DateTime now, TimeSpan grace) =>
        Occurrences(s, lastCheck, now)
            .Where(t => now - t <= grace)
            .Select(t => (DateTime?)t)
            .LastOrDefault();

    /// <summary>
    /// Tops up <see cref="Settings.SurpriseSchedule"/> so it covers today and tomorrow, and drops
    /// the days that are over. Returns true if it changed <paramref name="s"/>.
    /// </summary>
    public static bool EnsureSurpriseSchedule(Settings s, DateTime now, Random rng)
    {
        if (s.Mode != TriggerMode.Surprise)
        {
            return false;
        }

        var today = now.Date;
        var changed = s.SurpriseSchedule.RemoveAll(t => t < today) > 0;
        var earliestToday = NextMinute(now);

        foreach (var day in new[] { today, today.AddDays(1) })
        {
            // Elapsed times count, so the day's surprises are rolled once and never re-rolled.
            if (s.SurpriseSchedule.Any(t => t.Date == day))
            {
                continue;
            }

            var from = day.AddHours(s.ActiveFromHour);
            var until = day.AddHours(s.ActiveUntilHour);
            if (day == today && earliestToday > from)
            {
                from = earliestToday;
            }

            // Whole minutes left in the window; fewer than wanted simply means fewer surprises.
            var slots = (int)(until - from).TotalMinutes;
            if (slots <= 0)
            {
                continue;
            }

            var wanted = Math.Min(s.SurprisesPerDay, slots);
            var minutes = new HashSet<int>();
            while (minutes.Count < wanted)
            {
                minutes.Add(rng.Next(slots));
            }

            s.SurpriseSchedule.AddRange(minutes.Select(m => from.AddMinutes(m)));
            changed = true;
        }

        if (changed)
        {
            s.SurpriseSchedule.Sort();
        }

        return changed;
    }

    /// <summary>The next occurrence strictly after <paramref name="now"/>, or null.</summary>
    public static DateTime? Next(Settings s, DateTime now) =>
        // Three days of look-ahead is plenty: the recurring modes repeat daily and the surprise
        // schedule never reaches past tomorrow.
        Occurrences(s, now, now.AddDays(3)).Select(t => (DateTime?)t).FirstOrDefault();

    private static IEnumerable<DateTime> ScheduledOccurrences(Settings s, DateTime after, DateTime until)
    {
        if (s.Schedule == ScheduleKind.EveryFullHour)
        {
            for (var t = after.Date.AddHours(after.Hour + 1); t <= until; t = t.AddHours(1))
            {
                yield return t;
            }

            yield break;
        }

        var times = s.Times.Order().ToList();
        for (var day = after.Date; day <= until.Date; day = day.AddDays(1))
        {
            foreach (var time in times)
            {
                var t = day + time.ToTimeSpan();
                if (t > after && t <= until)
                {
                    yield return t;
                }
            }
        }
    }

    /// <summary>The first whole minute strictly after <paramref name="now"/>.</summary>
    private static DateTime NextMinute(DateTime now) =>
        now.Date.AddMinutes(Math.Floor(now.TimeOfDay.TotalMinutes) + 1);
}
