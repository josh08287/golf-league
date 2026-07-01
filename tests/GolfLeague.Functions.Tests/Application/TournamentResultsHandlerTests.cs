using FluentAssertions;
using GolfLeague.Application.Rounds.Queries;
using GolfLeague.Domain.Entities;
using GolfLeague.Domain.Enums;
using GolfLeague.Domain.Interfaces;
using Moq;
using Xunit;

namespace GolfLeague.Tests.Application;

/// <summary>
/// GetTournamentResultsQueryHandler computes gross/net skins with carryover
/// (independently of GetRoundSkinsQuery's flight-scoped version), head-to-head
/// matchup winners/halves, and four tie-aware rankings. None had coverage.
/// </summary>
public class TournamentResultsHandlerTests
{
    private static Player MakePlayer(int id, string name) => new() { Id = id, FirstName = name, LastName = "P" };

    private static HoleScore MakeHole(int holeNumber, int gross, int net, int par = 4) => new()
    {
        HoleNumber = holeNumber,
        Par = par,
        GrossStrokes = gross,
        NetStrokes = net,
    };

    private static RoundParticipant MakeParticipant(
        int playerId, string name, int? totalGross = null, int? totalNet = null,
        int? grossPoints = null, int? netPoints = null, double handicapIndex = 10, int courseHandicap = 8,
        params HoleScore[] holes)
    {
        // Handler only considers participants who have hole scores ("active").
        // When a test sets round totals without explicit holes, add a placeholder
        // so ranking/matchup tests exercise total-based logic.
        var holeList = holes;
        if (holeList.Length == 0 && (totalGross.HasValue || totalNet.HasValue || grossPoints.HasValue || netPoints.HasValue))
            holeList = [MakeHole(1, gross: 4, net: 4)];

        return new()
        {
            Id = playerId,
            PlayerId = playerId,
            Player = MakePlayer(playerId, name),
            TotalGrossStrokes = totalGross,
            TotalNetStrokes = totalNet,
            TotalGrossStablefordPoints = grossPoints,
            TotalNetStablefordPoints = netPoints,
            HandicapIndex = handicapIndex,
            CourseHandicap = courseHandicap,
            HoleScores = holeList,
        };
    }

    private static Round MakeRound(RoundType type = RoundType.Tournament) => new()
    {
        Id = 1,
        RoundType = type,
        CourseId = 1,
        RoundDate = new DateOnly(2026, 6, 1),
    };

    private sealed class Mocks
    {
        public Mock<IRoundRepository> Rounds { get; } = new();
        public Mock<ICourseRepository> Courses { get; } = new();

        public Mocks()
        {
            Courses.Setup(c => c.GetByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Course { Id = 1, Name = "Test Course" });
            Rounds.Setup(r => r.GetTournamentMatchupsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<TournamentMatchup>());
            Rounds.Setup(r => r.GetTournamentHoleExtrasAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<TournamentHoleExtra>());
            Rounds.Setup(r => r.GetLongestDriveWinnersAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<TournamentLongestDriveWinner>());
        }

        public GetTournamentResultsQueryHandler BuildSut() => new(Rounds.Object, Courses.Object);
    }

    [Fact]
    public async Task Handle_WhenRoundNotFound_ReturnsFail()
    {
        var m = new Mocks();
        m.Rounds.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync((Round?)null);

        var result = await m.BuildSut().Handle(new GetTournamentResultsQuery(1), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_WhenNotTournamentType_ReturnsFail()
    {
        var m = new Mocks();
        m.Rounds.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(MakeRound(RoundType.NineHole));

        var result = await m.BuildSut().Handle(new GetTournamentResultsQuery(1), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("not a tournament round");
    }

    [Fact]
    public async Task Handle_GrossAndNetSkins_AreCalculatedIndependently()
    {
        // Alice wins gross (lower gross), Bob wins net (lower net) on the same hole.
        var alice = MakeParticipant(1, "Alice", holes: MakeHole(1, gross: 3, net: 4));
        var bob = MakeParticipant(2, "Bob", holes: MakeHole(1, gross: 4, net: 3));

        var m = new Mocks();
        m.Rounds.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(MakeRound());
        m.Rounds.Setup(r => r.GetParticipantsAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(new List<RoundParticipant> { alice, bob });

        var result = await m.BuildSut().Handle(new GetTournamentResultsQuery(1), CancellationToken.None);

        result.Value!.GrossSkins.HoleResults.Single().WinnerPlayerId.Should().Be(1);
        result.Value!.NetSkins.HoleResults.Single().WinnerPlayerId.Should().Be(2);
    }

    [Fact]
    public async Task Handle_SkinsTie_CarriesOverAndMarksIsTie()
    {
        var alice = MakeParticipant(1, "Alice", holes: [MakeHole(1, gross: 4, net: 4), MakeHole(2, gross: 3, net: 3)]);
        var bob = MakeParticipant(2, "Bob", holes: [MakeHole(1, gross: 4, net: 4), MakeHole(2, gross: 5, net: 5)]);

        var m = new Mocks();
        m.Rounds.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(MakeRound());
        m.Rounds.Setup(r => r.GetParticipantsAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(new List<RoundParticipant> { alice, bob });

        var result = await m.BuildSut().Handle(new GetTournamentResultsQuery(1), CancellationToken.None);

        var hole1 = result.Value!.GrossSkins.HoleResults.Single(h => h.HoleNumber == 1);
        hole1.IsTie.Should().BeTrue();
        hole1.SkinValue.Should().Be(0);

        var hole2 = result.Value!.GrossSkins.HoleResults.Single(h => h.HoleNumber == 2);
        hole2.SkinValue.Should().Be(2, "hole 1's carryover rolls into hole 2");
        hole2.WasCarryover.Should().BeTrue();
        hole2.WinnerPlayerId.Should().Be(1);
    }

    [Fact]
    public async Task Handle_ExcludesWithdrawnSkippedAndNoScoreParticipantsFromSkins()
    {
        var active = MakeParticipant(1, "Alice", holes: MakeHole(1, gross: 4, net: 4));
        var withdrawn = MakeParticipant(2, "Bob", holes: MakeHole(1, gross: 2, net: 2));
        withdrawn.IsWithdrawn = true;
        var noScore = MakeParticipant(3, "Carl");

        var m = new Mocks();
        m.Rounds.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(MakeRound());
        m.Rounds.Setup(r => r.GetParticipantsAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<RoundParticipant> { active, withdrawn, noScore });

        var result = await m.BuildSut().Handle(new GetTournamentResultsQuery(1), CancellationToken.None);

        result.Value!.GrossSkins.HoleResults.Single().WinnerPlayerId.Should().Be(1);
    }

    [Fact]
    public async Task Handle_Matchup_LowerNetStrokesWins()
    {
        var alice = MakeParticipant(1, "Alice", totalNet: 70);
        var bob = MakeParticipant(2, "Bob", totalNet: 75);
        var matchup = new TournamentMatchup { MatchupNumber = 1, Player1Id = 1, Player2Id = 2, Player1 = alice.Player, Player2 = bob.Player };

        var m = new Mocks();
        m.Rounds.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(MakeRound());
        m.Rounds.Setup(r => r.GetParticipantsAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(new List<RoundParticipant> { alice, bob });
        m.Rounds.Setup(r => r.GetTournamentMatchupsAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(new List<TournamentMatchup> { matchup });

        var result = await m.BuildSut().Handle(new GetTournamentResultsQuery(1), CancellationToken.None);

        var mr = result.Value!.MatchupResults.Single();
        mr.WinnerPlayerId.Should().Be(1);
        mr.WinnerPlayerName.Should().Be("Alice P");
        mr.IsHalved.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_Matchup_EqualNetStrokes_IsHalved()
    {
        var alice = MakeParticipant(1, "Alice", totalNet: 72);
        var bob = MakeParticipant(2, "Bob", totalNet: 72);
        var matchup = new TournamentMatchup { MatchupNumber = 1, Player1Id = 1, Player2Id = 2, Player1 = alice.Player, Player2 = bob.Player };

        var m = new Mocks();
        m.Rounds.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(MakeRound());
        m.Rounds.Setup(r => r.GetParticipantsAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(new List<RoundParticipant> { alice, bob });
        m.Rounds.Setup(r => r.GetTournamentMatchupsAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(new List<TournamentMatchup> { matchup });

        var result = await m.BuildSut().Handle(new GetTournamentResultsQuery(1), CancellationToken.None);

        var mr = result.Value!.MatchupResults.Single();
        mr.IsHalved.Should().BeTrue();
        mr.WinnerPlayerId.Should().Be(0, "halved matchups use 0 as the sentinel, not null");
    }

    [Fact]
    public async Task Handle_Matchup_MissingScores_NoWinnerAndNotHalved()
    {
        // A participant hasn't submitted scores yet — matchup can't be decided.
        var alice = MakeParticipant(1, "Alice", totalNet: null);
        var bob = MakeParticipant(2, "Bob", totalNet: 75);
        var matchup = new TournamentMatchup { MatchupNumber = 1, Player1Id = 1, Player2Id = 2, Player1 = alice.Player, Player2 = bob.Player };

        var m = new Mocks();
        m.Rounds.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(MakeRound());
        m.Rounds.Setup(r => r.GetParticipantsAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(new List<RoundParticipant> { alice, bob });
        m.Rounds.Setup(r => r.GetTournamentMatchupsAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(new List<TournamentMatchup> { matchup });

        var result = await m.BuildSut().Handle(new GetTournamentResultsQuery(1), CancellationToken.None);

        var mr = result.Value!.MatchupResults.Single();
        mr.WinnerPlayerId.Should().BeNull();
        mr.IsHalved.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_MatchupsAreOrderedByMatchupNumber()
    {
        var alice = MakeParticipant(1, "Alice", totalNet: 70);
        var bob = MakeParticipant(2, "Bob", totalNet: 75);
        var carl = MakeParticipant(3, "Carl", totalNet: 80);
        var dana = MakeParticipant(4, "Dana", totalNet: 85);
        var matchup2 = new TournamentMatchup { MatchupNumber = 2, Player1Id = 3, Player2Id = 4, Player1 = carl.Player, Player2 = dana.Player };
        var matchup1 = new TournamentMatchup { MatchupNumber = 1, Player1Id = 1, Player2Id = 2, Player1 = alice.Player, Player2 = bob.Player };

        var m = new Mocks();
        m.Rounds.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(MakeRound());
        m.Rounds.Setup(r => r.GetParticipantsAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<RoundParticipant> { alice, bob, carl, dana });
        m.Rounds.Setup(r => r.GetTournamentMatchupsAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<TournamentMatchup> { matchup2, matchup1 });

        var result = await m.BuildSut().Handle(new GetTournamentResultsQuery(1), CancellationToken.None);

        result.Value!.MatchupResults.Select(r => r.MatchupNumber).Should().ContainInOrder(1, 2);
    }

    [Fact]
    public async Task Handle_NetStrokeRanking_AscendingWithTieDetection()
    {
        var alice = MakeParticipant(1, "Alice", totalNet: 70);
        var bob = MakeParticipant(2, "Bob", totalNet: 70);
        var carl = MakeParticipant(3, "Carl", totalNet: 75);

        var m = new Mocks();
        m.Rounds.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(MakeRound());
        m.Rounds.Setup(r => r.GetParticipantsAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<RoundParticipant> { alice, bob, carl });

        var result = await m.BuildSut().Handle(new GetTournamentResultsQuery(1), CancellationToken.None);

        var ranking = result.Value!.NetStrokeRanking;
        ranking.Where(r => r.Score == 70).Should().OnlyContain(r => r.Rank == 1 && r.IsTied);
        ranking.Single(r => r.Score == 75).Rank.Should().Be(3, "the two tied for rank 1 both occupy that rank, so the next distinct score is rank 3");
        ranking.Single(r => r.Score == 75).IsTied.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_StablefordRankings_AreDescendingUnlikeStrokeRankings()
    {
        var alice = MakeParticipant(1, "Alice", grossPoints: 30, netPoints: 32);
        var bob = MakeParticipant(2, "Bob", grossPoints: 20, netPoints: 22);

        var m = new Mocks();
        m.Rounds.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(MakeRound());
        m.Rounds.Setup(r => r.GetParticipantsAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(new List<RoundParticipant> { alice, bob });

        var result = await m.BuildSut().Handle(new GetTournamentResultsQuery(1), CancellationToken.None);

        result.Value!.GrossStablefordRanking.First().PlayerId.Should().Be(1, "higher Stableford points rank first");
        result.Value!.NetStablefordRanking.First().PlayerId.Should().Be(1);
    }

    [Fact]
    public async Task Handle_RankingExcludesParticipantsWithNoScoreForThatMetric()
    {
        var scored = MakeParticipant(1, "Alice", totalNet: 70);
        var notYetScored = MakeParticipant(2, "Bob", totalNet: null);

        var m = new Mocks();
        m.Rounds.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(MakeRound());
        m.Rounds.Setup(r => r.GetParticipantsAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(new List<RoundParticipant> { scored, notYetScored });

        var result = await m.BuildSut().Handle(new GetTournamentResultsQuery(1), CancellationToken.None);

        result.Value!.NetStrokeRanking.Should().ContainSingle();
        result.Value!.NetStrokeRanking.Single().PlayerId.Should().Be(1);
    }

    [Fact]
    public async Task Handle_LongestDriveWinnersAndHoleExtras_AreMappedThrough()
    {
        var alice = MakeParticipant(1, "Alice", holes: MakeHole(1, gross: 4, net: 4));
        var extra = new TournamentHoleExtra { HoleNumber = 3, RoundId = 1, ClosestToPinPlayerId = 1, ClosestToPinPlayer = alice.Player };
        var ldWinner = new TournamentLongestDriveWinner { RoundId = 1, PlayerId = 1, Player = alice.Player };

        var m = new Mocks();
        m.Rounds.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(MakeRound());
        m.Rounds.Setup(r => r.GetParticipantsAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(new List<RoundParticipant> { alice });
        m.Rounds.Setup(r => r.GetTournamentHoleExtrasAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(new List<TournamentHoleExtra> { extra });
        m.Rounds.Setup(r => r.GetLongestDriveWinnersAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(new List<TournamentLongestDriveWinner> { ldWinner });

        var result = await m.BuildSut().Handle(new GetTournamentResultsQuery(1), CancellationToken.None);

        result.Value!.HoleExtras.Single().ClosestToPinPlayerName.Should().Be("Alice P");
        result.Value!.LongestDriveWinners.Single().PlayerName.Should().Be("Alice P");
    }
}
