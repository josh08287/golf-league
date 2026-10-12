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
        // Tournaments are created as full-course rounds (CreateTournamentRoundCommand).
        NineHoleSide = type == RoundType.Tournament ? NineHoleSide.NotApplicable : NineHoleSide.Front,
        CourseId = 1,
        RoundDate = new DateOnly(2026, 6, 1),
    };

    private sealed class Mocks
    {
        public Mock<IRoundRepository> Rounds { get; } = new();
        public Mock<ICourseRepository> Courses { get; } = new();
        public Mock<ITeeTimeRepository> TeeTimes { get; } = new();

        public Mocks()
        {
            Courses.Setup(c => c.GetByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Course { Id = 1, Name = "Test Course" });
            Courses.Setup(c => c.GetHolesAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<CourseHole>());
            Rounds.Setup(r => r.GetTournamentMatchupsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<TournamentMatchup>());
            Rounds.Setup(r => r.GetTournamentHoleExtrasAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<TournamentHoleExtra>());
            Rounds.Setup(r => r.GetTournamentFlightsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<TournamentFlight>());
            Rounds.Setup(r => r.GetLongestDriveWinnersAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<TournamentLongestDriveWinner>());
            TeeTimes.Setup(t => t.GetByRoundAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<RoundTeeTime>());
        }

        public GetTournamentResultsQueryHandler BuildSut() => new(Rounds.Object, Courses.Object, TeeTimes.Object);

        public void WithCourseHoles(int count) =>
            Courses.Setup(c => c.GetHolesAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Enumerable.Range(1, count).Select(n => new CourseHole { HoleNumber = n, Par = 4, StrokeIndex = n }).ToList());
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
    public async Task Handle_SkinsPoolConfigured_SplitsEvenlyAcrossSkinsWon()
    {
        // Two holes, two different winners — $100 gross pool / 2 skins = $50/skin.
        var alice = MakeParticipant(1, "Alice", holes: [MakeHole(1, gross: 3, net: 4), MakeHole(2, gross: 5, net: 4)]);
        var bob = MakeParticipant(2, "Bob", holes: [MakeHole(1, gross: 4, net: 3), MakeHole(2, gross: 3, net: 4)]);

        var round = MakeRound();
        round.GrossSkinsPool = 100m;
        round.NetSkinsPool = null;

        var m = new Mocks();
        m.Rounds.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(round);
        m.Rounds.Setup(r => r.GetParticipantsAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(new List<RoundParticipant> { alice, bob });

        var result = await m.BuildSut().Handle(new GetTournamentResultsQuery(1), CancellationToken.None);

        result.Value!.GrossSkins.PoolAmount.Should().Be(100m);
        result.Value!.GrossSkins.PerSkinPayout.Should().Be(50m);
        result.Value!.GrossSkins.PlayerSummaries.Single(p => p.PlayerId == 1).PayoutAmount.Should().Be(50m); // Alice: 1 skin
        result.Value!.GrossSkins.PlayerSummaries.Single(p => p.PlayerId == 2).PayoutAmount.Should().Be(50m); // Bob: 1 skin

        // Net pool wasn't configured — no payout, but skins are still tracked.
        result.Value!.NetSkins.PoolAmount.Should().BeNull();
        result.Value!.NetSkins.PerSkinPayout.Should().BeNull();
        result.Value!.NetSkins.PlayerSummaries.Should().OnlyContain(p => p.PayoutAmount == null);
    }

    [Fact]
    public async Task Handle_SkinsPoolConfigured_NoSkinsWon_LeavesPayoutNull()
    {
        // Every hole ties — carryover accrues but nobody wins a skin, so a
        // configured pool shouldn't produce a divide-by-zero payout.
        var alice = MakeParticipant(1, "Alice", holes: MakeHole(1, gross: 4, net: 4));
        var bob = MakeParticipant(2, "Bob", holes: MakeHole(1, gross: 4, net: 4));

        var round = MakeRound();
        round.GrossSkinsPool = 50m;

        var m = new Mocks();
        m.Rounds.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(round);
        m.Rounds.Setup(r => r.GetParticipantsAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(new List<RoundParticipant> { alice, bob });

        var result = await m.BuildSut().Handle(new GetTournamentResultsQuery(1), CancellationToken.None);

        result.Value!.GrossSkins.PoolAmount.Should().Be(50m);
        result.Value!.GrossSkins.PerSkinPayout.Should().BeNull();
        result.Value!.GrossSkins.PlayerSummaries.Should().BeEmpty();
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
    public async Task Handle_ByeMatchup_AlwaysWinsForPlayer1_EvenWithoutScores()
    {
        // Odd player out from regeneration — no opponent, no scores yet.
        var alice = MakeParticipant(1, "Alice", totalNet: null);
        var matchup = new TournamentMatchup { MatchupNumber = 1, Player1Id = 1, Player2Id = null, Player1 = alice.Player };

        var m = new Mocks();
        m.Rounds.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(MakeRound());
        m.Rounds.Setup(r => r.GetParticipantsAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(new List<RoundParticipant> { alice });
        m.Rounds.Setup(r => r.GetTournamentMatchupsAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(new List<TournamentMatchup> { matchup });

        var result = await m.BuildSut().Handle(new GetTournamentResultsQuery(1), CancellationToken.None);

        var mr = result.Value!.MatchupResults.Single();
        mr.Player2Id.Should().BeNull();
        mr.Player2Name.Should().BeNull();
        mr.WinnerPlayerId.Should().Be(1);
        mr.WinnerPlayerName.Should().Be("Alice P");
        mr.IsHalved.Should().BeFalse();
        mr.HoleByHole.Should().BeEmpty();
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
        alice.TournamentFlightId = 900;
        var extra = new TournamentHoleExtra { HoleNumber = 3, RoundId = 1, ClosestToPinPlayerId = 1, ClosestToPinPlayer = alice.Player };
        var flight = new TournamentFlight { Id = 900, RoundId = 1, FlightNumber = 1, Name = "A" };
        var ldWinner = new TournamentLongestDriveWinner { RoundId = 1, TournamentFlightId = 900, PlayerId = 1, Player = alice.Player };

        var m = new Mocks();
        m.Rounds.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(MakeRound());
        m.Rounds.Setup(r => r.GetParticipantsAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(new List<RoundParticipant> { alice });
        m.Rounds.Setup(r => r.GetTournamentHoleExtrasAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(new List<TournamentHoleExtra> { extra });
        m.Rounds.Setup(r => r.GetTournamentFlightsAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(new List<TournamentFlight> { flight });
        m.Rounds.Setup(r => r.GetLongestDriveWinnersAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(new List<TournamentLongestDriveWinner> { ldWinner });

        var result = await m.BuildSut().Handle(new GetTournamentResultsQuery(1), CancellationToken.None);

        result.Value!.HoleExtras.Single().ClosestToPinPlayerName.Should().Be("Alice P");
        result.Value!.LongestDriveWinners.Single().PlayerName.Should().Be("Alice P");
    }

    [Fact]
    public async Task Handle_MatchupHoleByHole_TracksRunningStatusFromPlayer1Perspective()
    {
        // Alice wins hole 1 (lower net), Bob wins hole 2, hole 3 halved (no status change).
        var alice = MakeParticipant(1, "Alice", holes:
        [
            MakeHole(1, gross: 4, net: 3),
            MakeHole(2, gross: 5, net: 5),
            MakeHole(3, gross: 4, net: 4),
        ]);
        var bob = MakeParticipant(2, "Bob", holes:
        [
            MakeHole(1, gross: 5, net: 5),
            MakeHole(2, gross: 4, net: 3),
            MakeHole(3, gross: 4, net: 4),
        ]);
        var matchup = new TournamentMatchup { MatchupNumber = 1, Player1Id = 1, Player2Id = 2, Player1 = alice.Player, Player2 = bob.Player };

        var m = new Mocks();
        m.WithCourseHoles(3);
        m.Rounds.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(MakeRound());
        m.Rounds.Setup(r => r.GetParticipantsAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(new List<RoundParticipant> { alice, bob });
        m.Rounds.Setup(r => r.GetTournamentMatchupsAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(new List<TournamentMatchup> { matchup });

        var result = await m.BuildSut().Handle(new GetTournamentResultsQuery(1), CancellationToken.None);

        var holes = result.Value!.MatchupResults.Single().HoleByHole;
        holes.Should().HaveCount(3);
        holes[0].StatusAfterHole.Should().Be(1, "Alice 1up after winning hole 1");
        holes[1].StatusAfterHole.Should().Be(0, "Bob squares the match on hole 2");
        holes[2].StatusAfterHole.Should().Be(0, "hole 3 halved — no change");
        holes.Should().OnlyContain(h => !h.IsConceded);
    }

    [Fact]
    public async Task Handle_AppendsSubstitutesAsUnflightedGroupAfterRealFlights()
    {
        var flighted = MakeParticipant(1, "Alice", holes: MakeHole(1, gross: 4, net: 4));
        flighted.TournamentFlightId = 900;
        var sub = MakeParticipant(2, "Subby", holes: MakeHole(1, gross: 5, net: 5));
        sub.IsSubstitute = true;
        sub.TournamentFlightId = null;

        var flight = new TournamentFlight { Id = 900, RoundId = 1, FlightNumber = 1, Name = "A" };

        var m = new Mocks();
        m.Rounds.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(MakeRound());
        m.Rounds.Setup(r => r.GetParticipantsAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(new List<RoundParticipant> { flighted, sub });
        m.Rounds.Setup(r => r.GetTournamentFlightsAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(new List<TournamentFlight> { flight });

        var result = await m.BuildSut().Handle(new GetTournamentResultsQuery(1), CancellationToken.None);

        result.Value!.Flights.Should().HaveCount(2);
        result.Value.Flights[0].Name.Should().Be("A");
        result.Value.Flights[0].PlayerIds.Should().ContainSingle().Which.Should().Be(1);

        var subGroup = result.Value.Flights[1];
        subGroup.Name.Should().Be("Substitutes");
        subGroup.PlayerIds.Should().ContainSingle().Which.Should().Be(2);
    }

    [Fact]
    public async Task Handle_NoSubstitutes_DoesNotAddSubstitutesGroup()
    {
        var flighted = MakeParticipant(1, "Alice", holes: MakeHole(1, gross: 4, net: 4));
        flighted.TournamentFlightId = 900;
        var flight = new TournamentFlight { Id = 900, RoundId = 1, FlightNumber = 1, Name = "A" };

        var m = new Mocks();
        m.Rounds.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(MakeRound());
        m.Rounds.Setup(r => r.GetParticipantsAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(new List<RoundParticipant> { flighted });
        m.Rounds.Setup(r => r.GetTournamentFlightsAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(new List<TournamentFlight> { flight });

        var result = await m.BuildSut().Handle(new GetTournamentResultsQuery(1), CancellationToken.None);

        result.Value!.Flights.Should().ContainSingle();
        result.Value.Flights.Should().NotContain(f => f.Name == "Substitutes");
    }

    [Fact]
    public async Task Handle_WithdrawnSubstitute_IsExcludedFromSubstitutesGroup()
    {
        var withdrawnSub = MakeParticipant(2, "Subby", holes: MakeHole(1, gross: 5, net: 5));
        withdrawnSub.IsSubstitute = true;
        withdrawnSub.IsWithdrawn = true;

        var m = new Mocks();
        m.Rounds.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(MakeRound());
        m.Rounds.Setup(r => r.GetParticipantsAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(new List<RoundParticipant> { withdrawnSub });

        var result = await m.BuildSut().Handle(new GetTournamentResultsQuery(1), CancellationToken.None);

        result.Value!.Flights.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_MatchupHoleByHole_ClosesOutOnceLeadExceedsHolesRemaining()
    {
        // Alice wins holes 1-3 of a 3-hole "match" (test uses a short match to keep it
        // simple) — 3up with 0 remaining closes it out; nothing left to decide.
        var alice = MakeParticipant(1, "Alice", holes:
        [
            MakeHole(1, gross: 3, net: 3),
            MakeHole(2, gross: 3, net: 3),
            MakeHole(3, gross: 3, net: 3),
            MakeHole(4, gross: 5, net: 5),
        ]);
        var bob = MakeParticipant(2, "Bob", holes:
        [
            MakeHole(1, gross: 5, net: 5),
            MakeHole(2, gross: 5, net: 5),
            MakeHole(3, gross: 5, net: 5),
            MakeHole(4, gross: 3, net: 3),
        ]);
        var matchup = new TournamentMatchup { MatchupNumber = 1, Player1Id = 1, Player2Id = 2, Player1 = alice.Player, Player2 = bob.Player };

        var m = new Mocks();
        m.WithCourseHoles(4);
        m.Rounds.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(MakeRound());
        m.Rounds.Setup(r => r.GetParticipantsAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(new List<RoundParticipant> { alice, bob });
        m.Rounds.Setup(r => r.GetTournamentMatchupsAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(new List<TournamentMatchup> { matchup });

        var result = await m.BuildSut().Handle(new GetTournamentResultsQuery(1), CancellationToken.None);

        var holes = result.Value!.MatchupResults.Single().HoleByHole;
        holes[2].StatusAfterHole.Should().Be(3, "Alice is 3up through 3 with 1 to play — already closed out (3&1)");
        holes[2].IsConceded.Should().BeFalse();
        holes[3].IsConceded.Should().BeTrue("the match was already decided before the last hole was played");
        holes[3].StatusAfterHole.Should().BeNull();
    }

    private static (RoundParticipant Alice, RoundParticipant Bob, TournamentMatchup Matchup) MakeMatch(
        IEnumerable<int> aliceWinsHoles, IEnumerable<int> scoredHoles)
    {
        var wins = aliceWinsHoles.ToHashSet();
        var holes = scoredHoles.ToList();
        var alice = MakeParticipant(1, "Alice", holes: holes.Select(n => MakeHole(n, gross: wins.Contains(n) ? 3 : 4, net: wins.Contains(n) ? 3 : 4)).ToArray());
        var bob = MakeParticipant(2, "Bob", holes: holes.Select(n => MakeHole(n, gross: 4, net: 4)).ToArray());
        alice.TotalNetStrokes = alice.HoleScores.Sum(h => h.NetStrokes);
        bob.TotalNetStrokes = bob.HoleScores.Sum(h => h.NetStrokes);
        var matchup = new TournamentMatchup { MatchupNumber = 1, Player1Id = 1, Player2Id = 2, Player1 = alice.Player, Player2 = bob.Player };
        return (alice, bob, matchup);
    }

    private static Mocks MockMatch(RoundParticipant alice, RoundParticipant bob, TournamentMatchup matchup, int courseHoles = 18)
    {
        var m = new Mocks();
        m.WithCourseHoles(courseHoles);
        m.Rounds.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(MakeRound());
        m.Rounds.Setup(r => r.GetParticipantsAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(new List<RoundParticipant> { alice, bob });
        m.Rounds.Setup(r => r.GetTournamentMatchupsAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(new List<TournamentMatchup> { matchup });
        return m;
    }

    [Fact]
    public async Task Handle_MatchupHoleByHole_SpansEveryRoundHole_EvenWhenOnlyNineAreScored()
    {
        // Alice wins holes 1-5 of the front nine: 5up with 9 still to play in an
        // 18-hole round, so the match is NOT over (it was wrongly closed out when
        // only the scored holes counted).
        var (alice, bob, matchup) = MakeMatch(aliceWinsHoles: [1, 2, 3, 4, 5], scoredHoles: Enumerable.Range(1, 9));
        var m = MockMatch(alice, bob, matchup);

        var result = await m.BuildSut().Handle(new GetTournamentResultsQuery(1), CancellationToken.None);

        var mr = result.Value!.MatchupResults.Single();
        mr.HoleByHole.Select(h => h.HoleNumber).Should().Equal(Enumerable.Range(1, 18));
        mr.HoleByHole.Should().OnlyContain(h => !h.IsConceded);
        mr.HoleByHole[8].StatusAfterHole.Should().Be(5);
        mr.HoleByHole.Skip(9).Should().OnlyContain(h => h.StatusAfterHole == null, "back nine not played yet");
        mr.WinnerPlayerId.Should().BeNull("the match is not decided until all 18 holes are in");
        mr.IsHalved.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_MatchupHoleByHole_ClosesOutAgainstFullRoundLength()
    {
        // 10up through 10 with 8 to play in an 18-hole round: closed out 10&8.
        var (alice, bob, matchup) = MakeMatch(aliceWinsHoles: Enumerable.Range(1, 10), scoredHoles: Enumerable.Range(1, 18));
        var m = MockMatch(alice, bob, matchup);

        var result = await m.BuildSut().Handle(new GetTournamentResultsQuery(1), CancellationToken.None);

        var holes = result.Value!.MatchupResults.Single().HoleByHole;
        holes[8].IsConceded.Should().BeFalse("9up with 9 to play is dormie, not closed out");
        holes[9].StatusAfterHole.Should().Be(10);
        holes[9].IsConceded.Should().BeFalse();
        holes.Skip(10).Should().OnlyContain(h => h.IsConceded);
        result.Value.MatchupResults.Single().WinnerPlayerId.Should().Be(1);
    }

    [Fact]
    public async Task Handle_MatchupHoleByHole_FollowsShotgunPlayOrder()
    {
        // Group started on hole 10 and has played 10-14.
        var (alice, bob, matchup) = MakeMatch(aliceWinsHoles: [10, 11], scoredHoles: Enumerable.Range(10, 5));
        alice.TeeTimeId = 50;
        bob.TeeTimeId = 50;
        var m = MockMatch(alice, bob, matchup);
        m.TeeTimes.Setup(t => t.GetByRoundAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<RoundTeeTime> { new() { Id = 50, RoundId = 1, StartingHoleNumber = 10 } });

        var result = await m.BuildSut().Handle(new GetTournamentResultsQuery(1), CancellationToken.None);

        var holes = result.Value!.MatchupResults.Single().HoleByHole;
        holes.Select(h => h.HoleNumber).Should().Equal(Enumerable.Range(10, 9).Concat(Enumerable.Range(1, 9)));
        holes[4].StatusAfterHole.Should().Be(2, "2up through hole 14");
        holes.Skip(5).Should().OnlyContain(h => h.StatusAfterHole == null && !h.IsConceded);
    }
}
