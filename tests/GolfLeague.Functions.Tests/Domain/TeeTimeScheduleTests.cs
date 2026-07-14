using Xunit;
using GolfLeague.Domain.Services;
using FluentAssertions;

namespace GolfLeague.Tests.Domain;

/// <summary>
/// Coverage for <see cref="TeeTimeSchedule.IsRoundDay"/>, the pure helper
/// backing the round-day self-service tee-time switch exception.
/// </summary>
public class TeeTimeScheduleTests
{
    [Fact]
    public void IsRoundDay_SameEasternDate_ReturnsTrue()
    {
        // 3pm UTC is safely within the same Eastern calendar day year-round.
        var utcNow = new DateTime(2026, 6, 10, 15, 0, 0, DateTimeKind.Utc);
        var roundDate = new DateOnly(2026, 6, 10);

        TeeTimeSchedule.IsRoundDay(roundDate, utcNow).Should().BeTrue();
    }

    [Fact]
    public void IsRoundDay_DifferentEasternDate_ReturnsFalse()
    {
        var utcNow = new DateTime(2026, 6, 10, 15, 0, 0, DateTimeKind.Utc);
        var roundDate = new DateOnly(2026, 6, 11);

        TeeTimeSchedule.IsRoundDay(roundDate, utcNow).Should().BeFalse();
    }

    [Fact]
    public void IsRoundDay_UtcDateDiffersFromEasternDate_UsesEasternNotUtc()
    {
        // 2am UTC on June 11 is 10pm Eastern (EDT, UTC-4) on June 10 — the
        // UTC calendar date and the Eastern calendar date disagree. A naive
        // UTC-date comparison would wrongly say "round day" is June 11.
        var utcNow = new DateTime(2026, 6, 11, 2, 0, 0, DateTimeKind.Utc);
        var roundDate = new DateOnly(2026, 6, 10);

        TeeTimeSchedule.IsRoundDay(roundDate, utcNow).Should().BeTrue();
        TeeTimeSchedule.IsRoundDay(roundDate.AddDays(1), utcNow).Should().BeFalse();
    }

    [Fact]
    public void ComputeCutoffUtc_IsSixPmEasternDayBeforeRound_DuringEdt()
    {
        // June 10, 2026 is a Wednesday during EDT (UTC-4). Cutoff should be
        // 6pm ET June 9 = 22:00 UTC June 9.
        var roundDate = new DateOnly(2026, 6, 10);

        var cutoffUtc = TeeTimeSchedule.ComputeCutoffUtc(roundDate);

        cutoffUtc.Should().Be(new DateTime(2026, 6, 9, 22, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public void ComputeCutoffUtc_IsSixPmEasternDayBeforeRound_DuringEst()
    {
        // January 14, 2026 is during EST (UTC-5). Cutoff should be 6pm ET
        // January 13 = 23:00 UTC January 13.
        var roundDate = new DateOnly(2026, 1, 14);

        var cutoffUtc = TeeTimeSchedule.ComputeCutoffUtc(roundDate);

        cutoffUtc.Should().Be(new DateTime(2026, 1, 13, 23, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public void IsAfterCutoff_JustBeforeAndAfterSixPmDayBefore_TogglesCorrectly()
    {
        var roundDate = new DateOnly(2026, 6, 10);
        var cutoffUtc = TeeTimeSchedule.ComputeCutoffUtc(roundDate);

        TeeTimeSchedule.IsAfterCutoff(roundDate, cutoffUtc.AddMinutes(-1)).Should().BeFalse();
        TeeTimeSchedule.IsAfterCutoff(roundDate, cutoffUtc).Should().BeTrue();
        TeeTimeSchedule.IsAfterCutoff(roundDate, cutoffUtc.AddMinutes(1)).Should().BeTrue();
    }

    [Fact]
    public void ComputeCutoffUtc_WithCustomCutoffTime_UsesConfiguredTimeInsteadOfDefault()
    {
        // Same round/day as the default-cutoff test, but an admin-configured
        // 9am cutoff instead of the 6pm default.
        var roundDate = new DateOnly(2026, 6, 10);
        var customTime = new TimeOnly(9, 0);

        var cutoffUtc = TeeTimeSchedule.ComputeCutoffUtc(roundDate, customTime);

        cutoffUtc.Should().Be(new DateTime(2026, 6, 9, 13, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public void IsAfterCutoff_WithCustomCutoffTime_RespectsConfiguredTime()
    {
        var roundDate = new DateOnly(2026, 6, 10);
        var customTime = new TimeOnly(9, 0);
        var cutoffUtc = TeeTimeSchedule.ComputeCutoffUtc(roundDate, customTime);

        TeeTimeSchedule.IsAfterCutoff(roundDate, cutoffUtc.AddMinutes(-1), customTime).Should().BeFalse();
        TeeTimeSchedule.IsAfterCutoff(roundDate, cutoffUtc, customTime).Should().BeTrue();

        // Sanity: at the same instant, the default (6pm) cutoff hasn't passed yet.
        TeeTimeSchedule.IsAfterCutoff(roundDate, cutoffUtc).Should().BeFalse();
    }
}
