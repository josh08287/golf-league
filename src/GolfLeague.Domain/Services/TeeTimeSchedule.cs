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

    /// <summary>
    /// Sign-up cutoff: 12:00 noon US/Eastern (handles DST) on the Sunday
    /// immediately preceding the round date. Returns a UTC instant.
    /// </summary>
    public static DateTime ComputeSundayNoonCutoffUtc(DateOnly roundDate)
    {
        // Sunday before the round: if round is a Sunday, the cutoff is the
        // Sunday *of* the round at noon (effectively no early-bird window —
        // shouldn't matter for a league that plays mid-week).
        var daysBack = ((int)roundDate.DayOfWeek + 7) % 7;
        if (daysBack == 0) daysBack = 0; // Sunday → same day's noon
        var sunday = roundDate.AddDays(-daysBack);

        var noonLocal = new DateTime(sunday.Year, sunday.Month, sunday.Day, 12, 0, 0, DateTimeKind.Unspecified);
        return TimeZoneInfo.ConvertTimeToUtc(noonLocal, EasternTimeZone);
    }

    /// <summary>
    /// True when the current UTC time is past the Sunday-noon cutoff for
    /// the given round date.
    /// </summary>
    public static bool IsAfterCutoff(DateOnly roundDate, DateTime utcNow)
        => utcNow >= ComputeSundayNoonCutoffUtc(roundDate);

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
