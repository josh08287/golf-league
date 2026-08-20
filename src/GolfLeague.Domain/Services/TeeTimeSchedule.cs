namespace GolfLeague.Domain.Services;

/// <summary>
/// Pure functions for the tee-time schedule layout: slot times, capacity,
/// and the Sunday-noon-ET sign-up cutoff.
/// </summary>
public static class TeeTimeSchedule
{
    /// <summary>First tee time of the day — 3:28pm.</summary>
    public static readonly TimeOnly FirstTeeTime = new(15, 28);

    /// <summary>Minutes between consecutive tee times.</summary>
    public const int IntervalMinutes = 8;

    /// <summary>Maximum players per tee time.</summary>
    public const int CapacityPerTeeTime = 4;

    /// <summary>
    /// Compute the scheduled time for the Nth slot (1-based). Slot 1 = 3:28pm.
    /// </summary>
    public static TimeOnly TimeForSlot(int teeTimeNumber)
    {
        if (teeTimeNumber < 1) throw new ArgumentOutOfRangeException(nameof(teeTimeNumber));
        return FirstTeeTime.AddMinutes((teeTimeNumber - 1) * IntervalMinutes);
    }

    /// <summary>
    /// Number of tee times needed to seat <paramref name="playerCount"/> players,
    /// at the configured capacity. Returns 0 for zero players.
    /// </summary>
    public static int SlotsNeeded(int playerCount)
    {
        if (playerCount <= 0) return 0;
        return (playerCount + CapacityPerTeeTime - 1) / CapacityPerTeeTime;
    }

    /// <summary>Default sign-up cutoff time of day (US/Eastern) — 6:00pm.</summary>
    public static readonly TimeOnly DefaultCutoffTime = new(18, 0);

    /// <summary>
    /// Sign-up cutoff: <paramref name="cutoffTime"/> US/Eastern (handles DST,
    /// defaults to 6:00pm) on the calendar day immediately preceding the
    /// round date. Returns a UTC instant.
    /// </summary>
    public static DateTime ComputeCutoffUtc(DateOnly roundDate, TimeOnly? cutoffTime = null)
    {
        var time = cutoffTime ?? DefaultCutoffTime;
        var dayBefore = roundDate.AddDays(-1);

        var cutoffLocal = new DateTime(dayBefore.Year, dayBefore.Month, dayBefore.Day, time.Hour, time.Minute, 0, DateTimeKind.Unspecified);
        return TimeZoneInfo.ConvertTimeToUtc(cutoffLocal, EasternTimeZone);
    }

    /// <summary>
    /// True when the current UTC time is past the day-before-round sign-up
    /// cutoff (see <see cref="ComputeCutoffUtc"/>) for the given round date.
    /// </summary>
    public static bool IsAfterCutoff(DateOnly roundDate, DateTime utcNow, TimeOnly? cutoffTime = null)
        => utcNow >= ComputeCutoffUtc(roundDate, cutoffTime);

    /// <summary>Hours before the sign-up cutoff that the reminder email goes out.</summary>
    public const int ReminderLeadHours = 4;

    /// <summary>
    /// UTC instant of the sign-up reminder: <see cref="ReminderLeadHours"/>
    /// hours before the sign-up cutoff (see <see cref="ComputeCutoffUtc"/>).
    /// </summary>
    public static DateTime ComputeReminderTimeUtc(DateOnly roundDate, TimeOnly? cutoffTime = null)
        => ComputeCutoffUtc(roundDate, cutoffTime).AddHours(-ReminderLeadHours);

    /// <summary>
    /// True when <paramref name="utcNow"/> is at or past the reminder time
    /// but strictly before the sign-up cutoff itself for the given round
    /// date. Used to gate the one-time sign-up reminder email — once the
    /// cutoff passes, auto-fill takes over and a reminder no longer makes
    /// sense.
    /// </summary>
    public static bool IsWithinReminderWindow(DateOnly roundDate, DateTime utcNow, TimeOnly? cutoffTime = null)
        => utcNow >= ComputeReminderTimeUtc(roundDate, cutoffTime)
        && utcNow < ComputeCutoffUtc(roundDate, cutoffTime);

    /// <summary>
    /// True when <paramref name="utcNow"/> falls on the same US/Eastern
    /// calendar date as <paramref name="roundDate"/>. Used to gate the
    /// round-day self-service "move to another slot" exception to the
    /// sign-up cutoff.
    /// </summary>
    public static bool IsRoundDay(DateOnly roundDate, DateTime utcNow)
    {
        var easternNow = TimeZoneInfo.ConvertTimeFromUtc(
            DateTime.SpecifyKind(utcNow, DateTimeKind.Utc), EasternTimeZone);
        return DateOnly.FromDateTime(easternNow) == roundDate;
    }

    /// <summary>
    /// UTC instant of the last tee time on the given round date, for the given
    /// player count. Used to decide when a week's play is "over" so the next
    /// week's tee times can open. With zero players we fall back to the first
    /// slot so the window still advances at the nominal start time.
    /// </summary>
    public static DateTime LastTeeTimeUtc(DateOnly roundDate, int participantCount)
    {
        var slot = Math.Max(1, SlotsNeeded(participantCount));
        var localTime = TimeForSlot(slot);
        var local = new DateTime(
            roundDate.Year, roundDate.Month, roundDate.Day,
            localTime.Hour, localTime.Minute, 0, DateTimeKind.Unspecified);
        return TimeZoneInfo.ConvertTimeToUtc(local, EasternTimeZone);
    }

    /// <summary>
    /// UTC instant of <paramref name="localTime"/> US/Eastern on the calendar
    /// day <paramref name="daysBeforeRound"/> days before <paramref name="roundDate"/>.
    /// Used by the daily timers (autofill, reminder, sub-spot email) to convert
    /// their fixed Eastern-clock trigger times to UTC, handling DST.
    /// </summary>
    public static DateTime ComputeDailyTriggerUtc(DateOnly roundDate, int daysBeforeRound, TimeOnly localTime)
    {
        var day = roundDate.AddDays(-daysBeforeRound);
        var local = new DateTime(day.Year, day.Month, day.Day, localTime.Hour, localTime.Minute, 0, DateTimeKind.Unspecified);
        return TimeZoneInfo.ConvertTimeToUtc(local, EasternTimeZone);
    }

    // IANA "America/New_York" on Linux, "Eastern Standard Time" on Windows.
    // Try the IANA name first (works on Azure Functions Linux), fall back
    // to the Windows name for local-dev on Windows.
    public static readonly TimeZoneInfo EasternTimeZone = ResolveEastern();

    private static TimeZoneInfo ResolveEastern()
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById("America/New_York"); }
        catch (TimeZoneNotFoundException) { return TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time"); }
    }
}
