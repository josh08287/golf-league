using FluentAssertions;
using GolfLeague.Application.Rounds;
using Xunit;

namespace GolfLeague.Tests.Application;

/// <summary>
/// TournamentMatchupPairing is the shared default-pairing algorithm used by
/// both CreateTournamentRoundCommand (new rounds) and
/// RegenerateTournamentMatchupsCommand ("regenerate from handicaps"):
/// regulars paired by ascending handicap, subs paired randomly against other
/// subs and numbered last, with a "bye" matchup (Player2 null) for an odd
/// player out in either group.
/// </summary>
public class TournamentMatchupPairingTests
{
    private static PairablePlayer Player(int id, double handicapIndex, bool isSubstitute = false) =>
        new(id, $"P{id}", handicapIndex, (int)handicapIndex, isSubstitute);

    [Fact]
    public void EvenRegulars_PairByAscendingHandicap_NoByes()
    {
        var players = new[] { Player(1, 20.0), Player(2, 5.0), Player(3, 15.0), Player(4, 2.0) };

        var (entities, dtos) = TournamentMatchupPairing.Build(1, players);

        entities.Should().HaveCount(2);
        dtos.Should().OnlyContain(d => d.Player2Id != null);
        dtos[0].Player1Id.Should().Be(4); // 2.0
        dtos[0].Player2Id.Should().Be(2); // 5.0
        dtos[1].Player1Id.Should().Be(3); // 15.0
        dtos[1].Player2Id.Should().Be(1); // 20.0
    }

    [Fact]
    public void OddRegulars_HighestHandicapGetsBye()
    {
        var players = new[] { Player(1, 10.0), Player(2, 5.0), Player(3, 12.0) };

        var (entities, dtos) = TournamentMatchupPairing.Build(1, players);

        entities.Should().HaveCount(2);
        dtos[0].Player1Id.Should().Be(2); // 5.0
        dtos[0].Player2Id.Should().Be(1); // 10.0
        dtos[1].Player1Id.Should().Be(3); // 12.0, highest -> bye
        dtos[1].Player2Id.Should().BeNull();
        dtos[1].Player2Name.Should().BeNull();
        dtos[1].Player2HandicapIndex.Should().BeNull();
        dtos[1].Player2CourseHandicap.Should().BeNull();
    }

    [Fact]
    public void SingleSubstitute_GetsBye_NumberedAfterRegulars()
    {
        var players = new[] { Player(1, 10.0), Player(2, 5.0), Player(3, 8.0, isSubstitute: true) };

        var (entities, dtos) = TournamentMatchupPairing.Build(1, players);

        entities.Should().HaveCount(2);
        dtos[0].MatchupNumber.Should().Be(1);
        dtos[0].Player1Id.Should().Be(2);
        dtos[0].Player2Id.Should().Be(1);
        dtos[1].MatchupNumber.Should().Be(2);
        dtos[1].Player1Id.Should().Be(3);
        dtos[1].Player2Id.Should().BeNull();
    }

    [Fact]
    public void OddRegularsAndOddSubstitutes_BothGetOwnBye_RegularByeBeforeSubBye()
    {
        var players = new[]
        {
            Player(1, 10.0), Player(2, 5.0), Player(3, 12.0), // 3 regulars
            Player(10, 3.0, isSubstitute: true),               // 1 sub
        };

        var (entities, dtos) = TournamentMatchupPairing.Build(1, players);

        entities.Should().HaveCount(3);
        dtos.Select(d => d.MatchupNumber).Should().ContainInOrder(1, 2, 3);
        dtos[0].Player2Id.Should().NotBeNull("first matchup is the regular pair");
        dtos[1].Player1Id.Should().Be(3);
        dtos[1].Player2Id.Should().BeNull("regular bye is numbered right after the regular pairs");
        dtos[2].Player1Id.Should().Be(10);
        dtos[2].Player2Id.Should().BeNull("sub bye comes last");
    }

    [Fact]
    public void NoPlayers_ProducesNoMatchups()
    {
        var (entities, dtos) = TournamentMatchupPairing.Build(1, []);

        entities.Should().BeEmpty();
        dtos.Should().BeEmpty();
    }

    [Fact]
    public void SinglePlayer_ProducesOneByeMatchup()
    {
        var players = new[] { Player(1, 10.0) };

        var (entities, dtos) = TournamentMatchupPairing.Build(1, players);

        entities.Should().ContainSingle();
        dtos.Should().ContainSingle();
        dtos[0].Player1Id.Should().Be(1);
        dtos[0].Player2Id.Should().BeNull();
    }

    [Fact]
    public void SubstitutePairing_IgnoresHandicapOrder()
    {
        // Regression guard: substitutes must not be paired by ascending
        // handicap despite that being the tiebreak for regulars.
        var players = new[]
        {
            Player(1, 1.0, isSubstitute: true),
            Player(2, 2.0, isSubstitute: true),
            Player(3, 3.0, isSubstitute: true),
            Player(4, 4.0, isSubstitute: true),
        };

        bool sawNonHandicapOrder = false;
        for (int i = 0; i < 50 && !sawNonHandicapOrder; i++)
        {
            var (_, dtos) = TournamentMatchupPairing.Build(1, players);
            var handicapOrderPairing = dtos[0].Player1Id == 1 && dtos[0].Player2Id == 2;
            if (!handicapOrderPairing) sawNonHandicapOrder = true;
        }

        sawNonHandicapOrder.Should().BeTrue("substitute pairing should be randomized, not handicap-ordered");
    }

    [Fact]
    public void MatchupEntities_MirrorDtoPairingAndByeState()
    {
        var players = new[] { Player(1, 10.0), Player(2, 5.0), Player(3, 12.0) };

        var (entities, dtos) = TournamentMatchupPairing.Build(42, players);

        for (int i = 0; i < entities.Count; i++)
        {
            entities[i].RoundId.Should().Be(42);
            entities[i].MatchupNumber.Should().Be(dtos[i].MatchupNumber);
            entities[i].Player1Id.Should().Be(dtos[i].Player1Id);
            entities[i].Player2Id.Should().Be(dtos[i].Player2Id);
        }
    }
}
