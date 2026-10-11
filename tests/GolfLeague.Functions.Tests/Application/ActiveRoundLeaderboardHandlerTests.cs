using FluentAssertions;
using GolfLeague.Application.Rounds.Queries;
using GolfLeague.Domain.Entities;
using GolfLeague.Domain.Enums;
using GolfLeague.Domain.Interfaces;
using GolfLeague.Domain.Services;
using Moq;
using Xunit;

namespace GolfLeague.Tests.Application;

/// <summary>
/// The leaderboard shows the most recently played round until
/// <see cref="GetActiveRoundLeaderboardQueryHandler.SwitchToNextRoundDaysBefore"/> days
/// before the next scheduled round, then switches to that upcoming round. A round being
/// scored right now always takes priority.
/// </summary>
public class ActiveRoundLeaderboardHandlerTests
{
    private static readonly DateOnly Today =
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TeeTimeSchedule.EasternTimeZone));

    private readonly Mock<IRoundRepository> _rounds = new();

    public ActiveRoundLeaderboardHandlerTests()
    {
        _rounds.Setup(r => r.GetParticipantsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
    }

    private static Round MakeRound(int id, DateOnly date, RoundStatus status) => new()
    {
        Id = id,
        CourseId = 1,
        RoundDate = date,
        Status = status,
    };

    private void Setup(Round? inProgress = null, Round? recent = null, Round? next = null)
    {
        _rounds.Setup(r => r.GetInProgressRoundAsync(It.IsAny<CancellationToken>())).ReturnsAsync(inProgress);
        _rounds.Setup(r => r.GetMostRecentPlayedRoundAsync(It.IsAny<DateOnly>(), It.IsAny<CancellationToken>())).ReturnsAsync(recent);
        _rounds.Setup(r => r.GetNextScheduledRoundAsync(It.IsAny<DateOnly>(), It.IsAny<CancellationToken>())).ReturnsAsync(next);
    }

    private async Task<ActiveRoundLeaderboardDto?> Run()
    {
        var handler = new GetActiveRoundLeaderboardQueryHandler(
            _rounds.Object, new Mock<ICourseRepository>().Object, new Mock<IFlightRepository>().Object);
        var result = await handler.Handle(new GetActiveRoundLeaderboardQuery(), CancellationToken.None);
        result.IsSuccess.Should().BeTrue();
        return result.Value;
    }

    [Fact]
    public async Task InProgressRound_AlwaysWins()
    {
        Setup(
            inProgress: MakeRound(1, Today, RoundStatus.InProgress),
            recent: MakeRound(2, Today.AddDays(-7), RoundStatus.Finalized),
            next: MakeRound(3, Today.AddDays(1), RoundStatus.Scheduled));

        var dto = await Run();

        dto!.RoundId.Should().Be(1);
        dto.Status.Should().Be(RoundStatus.InProgress);
    }

    [Fact]
    public async Task NextRoundMoreThanThreeDaysAway_ShowsMostRecentRound()
    {
        Setup(
            recent: MakeRound(2, Today.AddDays(-3), RoundStatus.Finalized),
            next: MakeRound(3, Today.AddDays(4), RoundStatus.Scheduled));

        (await Run())!.RoundId.Should().Be(2);
    }

    [Fact]
    public async Task NextRoundExactlyThreeDaysAway_ShowsNextRound()
    {
        Setup(
            recent: MakeRound(2, Today.AddDays(-4), RoundStatus.Finalized),
            next: MakeRound(3, Today.AddDays(3), RoundStatus.Scheduled));

        var dto = await Run();

        dto!.RoundId.Should().Be(3);
        dto.Status.Should().Be(RoundStatus.Scheduled);
    }

    [Fact]
    public async Task NoPlayedRound_FallsBackToNextRound()
    {
        Setup(next: MakeRound(3, Today.AddDays(20), RoundStatus.Scheduled));

        (await Run())!.RoundId.Should().Be(3);
    }

    [Fact]
    public async Task NoNextRound_ShowsMostRecentRound()
    {
        Setup(recent: MakeRound(2, Today.AddDays(-30), RoundStatus.Finalized));

        (await Run())!.RoundId.Should().Be(2);
    }

    [Fact]
    public async Task NoRounds_ReturnsNull()
    {
        Setup();

        (await Run()).Should().BeNull();
    }
}
