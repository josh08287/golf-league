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
/// current roster: regular players are paired by ascending handicap — same
/// pairing rule as tournament creation's default matchups — while
/// substitutes are paired randomly against other substitutes, appended
/// after every regular matchup.
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
        Player = new Player { Id = id, FirstName = "P", LastName = id.ToString(), IsSubstitute = isSubstitute },
    };

    private static Mock<IRoundRepository> MakeRounds(Round round)
    {
        var rounds = new Mock<IRoundRepository>();
        rounds.Setup(r => r.GetByIdAsync(round.Id, It.IsAny<CancellationToken>())).ReturnsAsync(round);
        rounds.Setup(r => r.ReplaceTournamentMatchupsAsync(round.Id, It.IsAny<IEnumerable<TournamentMatchup>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        rounds.Setup(r => r.UpdateParticipantAsync(It.IsAny<RoundParticipant>(), It.IsAny<CancellationToken>()))
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

        // Sub matchup is appended last — pairing is random, not handicap-based,
        // so only membership (not order) is asserted.
        result.Value[1].MatchupNumber.Should().Be(2);
        new[] { result.Value[1].Player1Id, result.Value[1].Player2Id }
            .Should().BeEquivalentTo(new[] { 3, 4 });
    }

    [Fact]
    public async Task Handle_SubstitutePairing_IgnoresHandicapOrder()
    {
        // Regression guard: substitutes must not be paired by ascending
        // handicap. With enough subs, run regeneration repeatedly and assert
        // at least one run does not produce the handicap-sorted pairing.
        var regular1 = MakeParticipant(1, 10.0);
        var regular2 = MakeParticipant(2, 5.0);
        var subLow = MakeParticipant(10, 1.0, isSubstitute: true);
        var subMid1 = MakeParticipant(11, 2.0, isSubstitute: true);
        var subMid2 = MakeParticipant(12, 3.0, isSubstitute: true);
        var subHigh = MakeParticipant(13, 4.0, isSubstitute: true);

        bool sawNonHandicapOrder = false;
        for (int i = 0; i < 50 && !sawNonHandicapOrder; i++)
        {
            var round = MakeTournamentRound(participants: [regular1, regular2, subLow, subMid1, subMid2, subHigh]);
            var rounds = MakeRounds(round);

            var result = await new RegenerateTournamentMatchupsCommandHandler(rounds.Object)
                .Handle(new RegenerateTournamentMatchupsCommand(1, "user1"), CancellationToken.None);

            var subMatchups = result.Value!.Skip(1).ToList(); // after the single regular matchup
            var handicapOrderPairing =
                subMatchups[0].Player1Id == subLow.PlayerId && subMatchups[0].Player2Id == subMid1.PlayerId &&
                subMatchups[1].Player1Id == subMid2.PlayerId && subMatchups[1].Player2Id == subHigh.PlayerId;

            if (!handicapOrderPairing)
                sawNonHandicapOrder = true;
        }

        sawNonHandicapOrder.Should().BeTrue("substitute pairing should be randomized, not handicap-ordered");
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
    public async Task Handle_UsesCurrentPlayerSubstituteStatus_NotStaleParticipantSnapshot()
    {
        // Regression: a player added to the round while a regular, then later
        // flagged as a league substitute (or vice versa), leaves a stale
        // RoundParticipant.IsSubstitute snapshot. Regeneration must re-derive
        // from the live Player.IsSubstitute flag so a substitute never gets
        // paired against a non-substitute.
        var regular1 = MakeParticipant(1, 10.0);
        var regular2 = MakeParticipant(2, 5.0);
        // Snapshot says regular (added before being marked a sub), but the
        // player is now a substitute.
        var nowSub = MakeParticipant(3, 7.0, isSubstitute: false);
        nowSub.Player!.IsSubstitute = true;
        // Snapshot says substitute (added while a sub), but has since been
        // promoted back to a regular roster player.
        var nowRegular = MakeParticipant(4, 6.0, isSubstitute: true);
        nowRegular.Player!.IsSubstitute = false;

        var round = MakeTournamentRound(participants: [regular1, regular2, nowSub, nowRegular]);
        var rounds = MakeRounds(round);

        var result = await new RegenerateTournamentMatchupsCommandHandler(rounds.Object)
            .Handle(new RegenerateTournamentMatchupsCommand(1, "user1"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        // 3 regulars (1 pair + 1 leftover), 1 sub (unmatched) -> one matchup total.
        result.Value.Should().HaveCount(1);

        // Regulars now: regular1(10), regular2(5), nowRegular(6) -> sorted 2,4,1
        result.Value![0].Player1Id.Should().Be(2);
        result.Value[0].Player2Id.Should().Be(4);
        // regular1 is the odd one out; nowSub(3) is the lone sub and unmatched.
        result.Value.Should().NotContain(m => m.Player1Id == 1 || m.Player2Id == 1);
        result.Value.Should().NotContain(m => m.Player1Id == 3 || m.Player2Id == 3);

        // Snapshots on the participants themselves must also be corrected.
        nowSub.IsSubstitute.Should().BeTrue();
        nowRegular.IsSubstitute.Should().BeFalse();
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
