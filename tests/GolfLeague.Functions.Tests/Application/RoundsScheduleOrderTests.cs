using FluentAssertions;
using GolfLeague.Application.DTOs;
using GolfLeague.Application.Rounds.Queries;
using GolfLeague.Domain.Enums;
using Xunit;

namespace GolfLeague.Tests.Application;

public class RoundsScheduleOrderTests
{
    private static readonly DateOnly Today = new(2026, 10, 9);

    private static RoundDto Round(int id, int daysFromToday, RoundStatus status) => new(
        id, 1, null, 1, "Course", id, Today.AddDays(daysFromToday), status,
        NineHoleSide.Front, RoundType.NineHole, 0, null, null, null, false, false, null);

    [Fact]
    public void NextRoundFirst_ThenPastNewestFirst_ThenRemainingUpcomingSoonestFirst()
    {
        var rounds = new[]
        {
            Round(1, -21, RoundStatus.Finalized),
            Round(2, -14, RoundStatus.Finalized),
            Round(3, -7, RoundStatus.Finalized),
            Round(4, 7, RoundStatus.Scheduled),
            Round(5, 14, RoundStatus.Scheduled),
            Round(6, 21, RoundStatus.Scheduled),
        };

        var ordered = GetRoundsQueryHandler.ScheduleOrder(rounds, Today);

        ordered.Select(r => r.Id).Should().Equal(4, 3, 2, 1, 5, 6);
    }

    [Fact]
    public void RoundInProgressToday_IsTheNextRound()
    {
        var rounds = new[]
        {
            Round(1, -7, RoundStatus.Finalized),
            Round(2, 0, RoundStatus.InProgress),
            Round(3, 7, RoundStatus.Scheduled),
        };

        GetRoundsQueryHandler.ScheduleOrder(rounds, Today).Select(r => r.Id).Should().Equal(2, 1, 3);
    }

    [Fact]
    public void FinalizedOrCancelledFutureRounds_CountAsPast()
    {
        var rounds = new[]
        {
            Round(1, -7, RoundStatus.Finalized),
            Round(2, 7, RoundStatus.Cancelled),
            Round(3, 14, RoundStatus.Scheduled),
        };

        // Next = round 3; past newest-first = 2 (cancelled, +7), then 1.
        GetRoundsQueryHandler.ScheduleOrder(rounds, Today).Select(r => r.Id).Should().Equal(3, 2, 1);
    }

    [Fact]
    public void NoUpcomingRounds_IsJustPastNewestFirst()
    {
        var rounds = new[] { Round(1, -14, RoundStatus.Finalized), Round(2, -7, RoundStatus.Finalized) };

        GetRoundsQueryHandler.ScheduleOrder(rounds, Today).Select(r => r.Id).Should().Equal(2, 1);
    }
}
