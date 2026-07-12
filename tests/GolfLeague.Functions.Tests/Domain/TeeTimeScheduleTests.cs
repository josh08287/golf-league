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
}
