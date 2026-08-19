using FluentAssertions;
using GolfLeague.Application.Rounds;
using GolfLeague.Application.Rounds.Commands;
using GolfLeague.Domain.Entities;
using GolfLeague.Domain.Enums;
using GolfLeague.Domain.Interfaces;
using GolfLeague.Domain.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace GolfLeague.Tests.Application;

/// <summary>
/// Coverage for admin add/remove of tournament round players: only allowed
/// while Scheduled, and each change re-groups tee-time foursomes by
/// ascending handicap (tournament rounds don't use player self-service
/// sign-up or standings-based autofill).
/// </summary>
public class TournamentParticipantManagementTests
{
    private static Round MakeTournamentRound(RoundStatus status = RoundStatus.Scheduled, params RoundParticipant[] participants)
    {
        var round = new Round
        {
            Id = 1,
            RoundType = RoundType.Tournament,
            Status = status,
            Course = new Course
            {
                Id = 1,
                SlopeRating = 113,
                CourseRating = 72.0,
                Holes = Enumerable.Range(1, 18).Select(n => new CourseHole { HoleNumber = n, Par = 4, StrokeIndex = n }).ToList(),
            },
        };
        foreach (var p in participants) round.Participants.Add(p);
        return round;
    }

    private static RoundParticipant MakeParticipant(int id, double handicapIndex, int? teeTimeId = null) => new()
    {
        Id = id,
        PlayerId = id,
        RoundId = 1,
        HandicapIndex = handicapIndex,
        Player = new Player { Id = id, FirstName = "P", LastName = id.ToString() },
        TeeTimeId = teeTimeId,
    };

    [Fact]
    public async Task AddTournamentParticipants_Fails_WhenRoundInProgress()
    {
        var round = MakeTournamentRound(RoundStatus.InProgress, MakeParticipant(1, 10));
        var rounds = new Mock<IRoundRepository>();
        rounds.Setup(r => r.GetByIdAsync(round.Id, It.IsAny<CancellationToken>())).ReturnsAsync(round);
        var players = new Mock<IPlayerRepository>();
        var handicaps = new Mock<IHandicapRepository>();
        var teeTimes = new Mock<ITeeTimeRepository>();

        var handler = new AddTournamentParticipantsCommandHandler(rounds.Object, players.Object, handicaps.Object, new TournamentFoursomeService(teeTimes.Object));

        var result = await handler.Handle(new AddTournamentParticipantsCommand(round.Id, new List<int> { 2 }, "user1"), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Scheduled");
    }

    [Fact]
    public async Task AddTournamentParticipants_AddsPlayer_AndRegroupsFoursomes()
    {
        var round = MakeTournamentRound(RoundStatus.Scheduled, MakeParticipant(1, 5.0));
        var rounds = new Mock<IRoundRepository>();
        rounds.Setup(r => r.GetByIdAsync(round.Id, It.IsAny<CancellationToken>())).ReturnsAsync(round);

        var newParticipant = MakeParticipant(2, 8.0);
        rounds.Setup(r => r.GetParticipantsAsync(round.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<RoundParticipant> { round.Participants.First(), newParticipant });

        var players = new Mock<IPlayerRepository>();
        players.Setup(p => p.GetByIdAsync(2, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Player { Id = 2, FirstName = "P", LastName = "2", IsActive = true });

        var handicaps = new Mock<IHandicapRepository>();
        handicaps.Setup(h => h.GetCurrentAsync(2, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Handicap?)null);

        var slots = new List<RoundTeeTime> { new() { Id = 100, RoundId = round.Id, TeeTimeNumber = 1 } };
        var teeTimes = new Mock<ITeeTimeRepository>();
        teeTimes.Setup(t => t.EnsureSlotsAsync(round.Id, It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(slots);
        teeTimes.Setup(t => t.SetParticipantTeeTimeAsync(It.IsAny<int>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var handler = new AddTournamentParticipantsCommandHandler(rounds.Object, players.Object, handicaps.Object, new TournamentFoursomeService(teeTimes.Object));

        var result = await handler.Handle(new AddTournamentParticipantsCommand(round.Id, new List<int> { 2 }, "user1"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().ContainSingle(p => p.PlayerId == 2);
        teeTimes.Verify(t => t.SetParticipantTeeTimeAsync(It.IsAny<int>(), 100, It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task RemoveTournamentParticipant_Fails_WhenRoundInProgress()
    {
        var round = MakeTournamentRound(RoundStatus.InProgress, MakeParticipant(1, 10));
        var rounds = new Mock<IRoundRepository>();
        rounds.Setup(r => r.GetByIdAsync(round.Id, It.IsAny<CancellationToken>())).ReturnsAsync(round);
        var teeTimes = new Mock<ITeeTimeRepository>();

        var handler = new RemoveTournamentParticipantCommandHandler(rounds.Object, new TournamentFoursomeService(teeTimes.Object));

        var result = await handler.Handle(new RemoveTournamentParticipantCommand(round.Id, 1, "user1"), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Scheduled");
    }

    [Fact]
    public async Task RemoveTournamentParticipant_RemovesPlayer_AndDropsTheirMatchup()
    {
        var p1 = MakeParticipant(1, 5.0);
        var p2 = MakeParticipant(2, 8.0);
        var round = MakeTournamentRound(RoundStatus.Scheduled, p1, p2);
        var rounds = new Mock<IRoundRepository>();
        rounds.Setup(r => r.GetByIdAsync(round.Id, It.IsAny<CancellationToken>())).ReturnsAsync(round);

        var matchup = new TournamentMatchup { Id = 1, RoundId = round.Id, MatchupNumber = 1, Player1Id = 1, Player2Id = 2 };
        rounds.Setup(r => r.GetTournamentMatchupsAsync(round.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<TournamentMatchup> { matchup });
        rounds.Setup(r => r.DeleteParticipantAsync(p1.Id, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        rounds.Setup(r => r.ReplaceTournamentMatchupsAsync(round.Id, It.IsAny<IEnumerable<TournamentMatchup>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var teeTimes = new Mock<ITeeTimeRepository>();
        teeTimes.Setup(t => t.EnsureSlotsAsync(round.Id, It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<RoundTeeTime> { new() { Id = 100, RoundId = round.Id, TeeTimeNumber = 1 } });
        teeTimes.Setup(t => t.SetParticipantTeeTimeAsync(It.IsAny<int>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var handler = new RemoveTournamentParticipantCommandHandler(rounds.Object, new TournamentFoursomeService(teeTimes.Object));

        var result = await handler.Handle(new RemoveTournamentParticipantCommand(round.Id, 1, "user1"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        rounds.Verify(r => r.DeleteParticipantAsync(p1.Id, It.IsAny<CancellationToken>()), Times.Once);
        rounds.Verify(r => r.ReplaceTournamentMatchupsAsync(round.Id, It.Is<IEnumerable<TournamentMatchup>>(m => !m.Any()), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RegroupAsync_OrdersFoursomesByAscendingHandicap()
    {
        var participants = new List<RoundParticipant>
        {
            MakeParticipant(1, 20.0),
            MakeParticipant(2, 5.0),
            MakeParticipant(3, 15.0),
            MakeParticipant(4, 10.0),
            MakeParticipant(5, 2.0),
        };

        var slots = new List<RoundTeeTime>
        {
            new() { Id = 100, RoundId = 1, TeeTimeNumber = 1 },
            new() { Id = 101, RoundId = 1, TeeTimeNumber = 2 },
        };

        var teeTimes = new Mock<ITeeTimeRepository>();
        teeTimes.Setup(t => t.EnsureSlotsAsync(1, It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(slots);
        var assignments = new Dictionary<int, int?>();
        teeTimes.Setup(t => t.SetParticipantTeeTimeAsync(It.IsAny<int>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .Callback<int, int?, CancellationToken>((pid, tid, _) => assignments[pid] = tid)
            .Returns(Task.CompletedTask);

        var sut = new TournamentFoursomeService(teeTimes.Object);
        await sut.RegroupAsync(1, participants, CancellationToken.None);

        // Ascending order: 5(2.0), 2(5.0), 4(10.0), 3(15.0) -> slot 100; 1(20.0) -> slot 101
        assignments[5].Should().Be(100);
        assignments[2].Should().Be(100);
        assignments[4].Should().Be(100);
        assignments[3].Should().Be(100);
        assignments[1].Should().Be(101);
    }
}
