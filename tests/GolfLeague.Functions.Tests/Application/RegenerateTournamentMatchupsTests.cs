using FluentAssertions;
using GolfLeague.Application.Rounds.Commands;
using GolfLeague.Domain.Entities;
using GolfLeague.Domain.Enums;
using GolfLeague.Domain.Interfaces;
using Moq;
using Xunit;

namespace GolfLeague.Tests.Application;

/// <summary>
/// RegenerateTournamentMatchupsCommand re-derives matchups from the round's
/// current roster by ascending handicap — same pairing rule as tournament
/// creation's default matchups, except substitutes are only ever paired
/// against other substitutes, appended after every regular matchup.
/// </summary>
public class RegenerateTournamentMatchupsTests
{
    private static Round MakeTournamentRound(RoundStatus status = RoundStatus.Scheduled, params RoundParticipant[] participants)
    {
        var round = new Round { Id = 1, RoundType = RoundType.Tournament, Status = status };
        foreach (var p in participants) round.Participants.Add(p);
        return round;
    }

    private static RoundParticipant MakeParticipant(int id, double handicapIndex, bool isSubstitute = false) => new()
    {
        Id = id,
        PlayerId = id,
        RoundId = 1,
        HandicapIndex = handicapIndex,
        CourseHandicap = (int)handicapIndex,
        IsSubstitute = isSubstitute,
        Player = new Player { Id = id, FirstName = "P", LastName = id.ToString() },
    };

    private static Mock<IRoundRepository> MakeRounds(Round round)
    {
        var rounds = new Mock<IRoundRepository>();
        rounds.Setup(r => r.GetByIdAsync(round.Id, It.IsAny<CancellationToken>())).ReturnsAsync(round);
        rounds.Setup(r => r.ReplaceTournamentMatchupsAsync(round.Id, It.IsAny<IEnumerable<TournamentMatchup>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        return rounds;
    }

    [Fact]
    public async Task Handle_WhenRoundNotFound_ReturnsFail()
    {
        var rounds = new Mock<IRoundRepository>();
        rounds.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync((Round?)null);

        var result = await new RegenerateTournamentMatchupsCommandHandler(rounds.Object)
            .Handle(new RegenerateTournamentMatchupsCommand(1, "user1"), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_WhenNotTournamentType_ReturnsFail()
    {
        var round = new Round { Id = 1, RoundType = RoundType.NineHole };
        var rounds = MakeRounds(round);

        var result = await new RegenerateTournamentMatchupsCommandHandler(rounds.Object)
            .Handle(new RegenerateTournamentMatchupsCommand(1, "user1"), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("not a tournament round");
    }

    [Fact]
    public async Task Handle_WhenRoundInProgress_ReturnsFail()
    {
        var round = MakeTournamentRound(RoundStatus.InProgress, MakeParticipant(1, 10), MakeParticipant(2, 8));
        var rounds = MakeRounds(round);

        var result = await new RegenerateTournamentMatchupsCommandHandler(rounds.Object)
            .Handle(new RegenerateTournamentMatchupsCommand(1, "user1"), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Scheduled");
    }

    [Fact]
    public async Task Handle_PairsRegularsByAscendingHandicap()
    {
        // Regulars: 3(20), 1(20-ish desc order given), sorted ascending -> 4(2), 2(5), 3(15), 1(20)
        var p1 = MakeParticipant(1, 20.0);
        var p2 = MakeParticipant(2, 5.0);
        var p3 = MakeParticipant(3, 15.0);
        var p4 = MakeParticipant(4, 2.0);
        var round = MakeTournamentRound(participants: [p1, p2, p3, p4]);
        var rounds = MakeRounds(round);

        var result = await new RegenerateTournamentMatchupsCommandHandler(rounds.Object)
            .Handle(new RegenerateTournamentMatchupsCommand(1, "user1"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(2);
        result.Value![0].MatchupNumber.Should().Be(1);
        result.Value[0].Player1Id.Should().Be(4); // lowest handicap (2.0)
        result.Value[0].Player2Id.Should().Be(2); // next lowest (5.0)
        result.Value[1].MatchupNumber.Should().Be(2);
        result.Value[1].Player1Id.Should().Be(3); // (15.0)
        result.Value[1].Player2Id.Should().Be(1); // (20.0)
    }

    [Fact]
    public async Task Handle_SubstitutesOnlyPairAgainstOtherSubstitutes_AndComeLast()
    {
        var regular1 = MakeParticipant(1, 10.0);
        var regular2 = MakeParticipant(2, 5.0);
        var sub1 = MakeParticipant(3, 8.0, isSubstitute: true);
        var sub2 = MakeParticipant(4, 3.0, isSubstitute: true);
        var round = MakeTournamentRound(participants: [regular1, regular2, sub1, sub2]);
        var rounds = MakeRounds(round);

        var result = await new RegenerateTournamentMatchupsCommandHandler(rounds.Object)
            .Handle(new RegenerateTournamentMatchupsCommand(1, "user1"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(2);

        // Regular matchup comes first, regardless of the subs' lower handicaps.
        result.Value![0].MatchupNumber.Should().Be(1);
        result.Value[0].Player1Id.Should().Be(2); // regular2 (5.0)
        result.Value[0].Player2Id.Should().Be(1); // regular1 (10.0)

        // Sub matchup is appended last.
        result.Value[1].MatchupNumber.Should().Be(2);
        result.Value[1].Player1Id.Should().Be(4); // sub2 (3.0)
        result.Value[1].Player2Id.Should().Be(3); // sub1 (8.0)
    }

    [Fact]
    public async Task Handle_OddPlayerOutInEitherGroup_IsLeftUnmatched()
    {
        var regular1 = MakeParticipant(1, 10.0);
        var regular2 = MakeParticipant(2, 5.0);
        var regular3 = MakeParticipant(3, 12.0);
        var sub1 = MakeParticipant(4, 8.0, isSubstitute: true);
        var round = MakeTournamentRound(participants: [regular1, regular2, regular3, sub1]);
        var rounds = MakeRounds(round);

        var result = await new RegenerateTournamentMatchupsCommandHandler(rounds.Object)
            .Handle(new RegenerateTournamentMatchupsCommand(1, "user1"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        // 3 regulars -> 1 pair, 1 leftover unmatched; 1 sub -> no pair possible.
        result.Value.Should().HaveCount(1);
        result.Value![0].Player1Id.Should().Be(2); // regular2 (5.0)
        result.Value[0].Player2Id.Should().Be(1); // regular1 (10.0)
    }

    [Fact]
    public async Task Handle_ReplacesMatchupsInRepository()
    {
        var p1 = MakeParticipant(1, 10.0);
        var p2 = MakeParticipant(2, 5.0);
        var round = MakeTournamentRound(participants: [p1, p2]);
        var rounds = MakeRounds(round);

        await new RegenerateTournamentMatchupsCommandHandler(rounds.Object)
            .Handle(new RegenerateTournamentMatchupsCommand(1, "user1"), CancellationToken.None);

        rounds.Verify(r => r.ReplaceTournamentMatchupsAsync(
            1,
            It.Is<IEnumerable<TournamentMatchup>>(m => m.Count() == 1
                && m.First().Player1Id == 2 && m.First().Player2Id == 1),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
