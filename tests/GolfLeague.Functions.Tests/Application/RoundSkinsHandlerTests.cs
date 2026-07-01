using FluentAssertions;
using GolfLeague.Application.Rounds.Queries;
using GolfLeague.Domain.Entities;
using GolfLeague.Domain.Interfaces;
using Moq;
using Xunit;

namespace GolfLeague.Tests.Application;

/// <summary>
/// GetRoundSkinsQueryHandler computes two independent skins games inline:
/// per-flight net skins (ties carry the skin value to the next hole) and a
/// cross-flight gross par-3 skins game whose carryover is computed by
/// replaying every prior round in the half. Neither had any test coverage.
/// </summary>
public class RoundSkinsHandlerTests
{
    private static Season MakeSeason(int year = 2026) => new() { Id = 1, Year = year, Name = "2026" };
    private static SeasonHalf MakeHalf(Season season, int halfNumber = 1) => new()
    {
        Id = 1,
        SeasonId = season.Id,
        HalfNumber = halfNumber,
        Season = season,
    };
    private static Flight MakeFlight(int id, SeasonHalf half, int displayOrder = 0, string name = "A") => new()
    {
        Id = id,
        HalfId = half.Id,
        SeasonId = half.SeasonId,
        Name = name,
        DisplayOrder = displayOrder,
        Half = half,
        Season = half.Season,
    };

    private static Player MakePlayer(int id, string name) => new() { Id = id, FirstName = name, LastName = "P" };

    private static HoleScore MakeHole(int holeNumber, int gross, int net, int par = 4) => new()
    {
        HoleNumber = holeNumber,
        Par = par,
        GrossStrokes = gross,
        NetStrokes = net,
    };

    private static RoundParticipant MakeParticipant(int playerId, string name, int? flightId, params HoleScore[] holes) => new()
    {
        Id = playerId,
        PlayerId = playerId,
        Player = MakePlayer(playerId, name),
        FlightId = flightId,
        HoleScores = holes,
    };

    private static Round MakeRound(int id = 1, int? halfId = 1, int weekNumber = 1) => new()
    {
        Id = id,
        HalfId = halfId,
        WeekNumber = weekNumber,
        RoundDate = new DateOnly(2026, 6, 1),
        CourseId = 1,
    };

    private sealed class Mocks
    {
        public Mock<IRoundRepository> Rounds { get; } = new();
        public Mock<ICourseRepository> Courses { get; } = new();
        public Mock<IFlightRepository> Flights { get; } = new();

        public Mocks()
        {
            Rounds.Setup(r => r.GetPreviousRoundsAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<Round>());
            Courses.Setup(c => c.GetByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Course { Id = 1, Name = "Test Course" });
        }

        public GetRoundSkinsQueryHandler BuildSut() => new(Rounds.Object, Courses.Object, Flights.Object);
    }

    [Fact]
    public async Task Handle_WhenRoundNotFound_ReturnsFail()
    {
        var m = new Mocks();
        m.Rounds.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync((Round?)null);
        var sut = m.BuildSut();

        var result = await sut.Handle(new GetRoundSkinsQuery(1), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_WhenNoParticipants_ReturnsFail()
    {
        var m = new Mocks();
        m.Rounds.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(MakeRound());
        m.Rounds.Setup(r => r.GetParticipantsAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(new List<RoundParticipant>());
        var sut = m.BuildSut();

        var result = await sut.Handle(new GetRoundSkinsQuery(1), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_SingleFlightNoTies_EachHoleAwardsOneSkin()
    {
        var season = MakeSeason();
        var half = MakeHalf(season);
        var flight = MakeFlight(1, half);
        var p1 = MakeParticipant(1, "Alice", 1, MakeHole(1, gross: 4, net: 3), MakeHole(2, gross: 5, net: 4));
        var p2 = MakeParticipant(2, "Bob", 1, MakeHole(1, gross: 5, net: 4), MakeHole(2, gross: 4, net: 3));

        var m = new Mocks();
        m.Rounds.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(MakeRound());
        m.Rounds.Setup(r => r.GetParticipantsAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(new List<RoundParticipant> { p1, p2 });
        m.Flights.Setup(f => f.GetByHalfAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(new List<Flight> { flight });

        var result = await m.BuildSut().Handle(new GetRoundSkinsQuery(1), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var flightSkins = result.Value!.FlightSkins.Single();
        flightSkins.TotalHolesWithSkins.Should().Be(2);
        flightSkins.TotalSkinValueAwarded.Should().Be(2);

        var alice = flightSkins.PlayerSummaries.Single(p => p.PlayerId == 1);
        alice.TotalSkinsWon.Should().Be(1);
        alice.HolesWon.Single().HoleNumber.Should().Be(1);

        var bob = flightSkins.PlayerSummaries.Single(p => p.PlayerId == 2);
        bob.TotalSkinsWon.Should().Be(1);
        bob.HolesWon.Single().HoleNumber.Should().Be(2);
    }

    [Fact]
    public async Task Handle_TieThenWin_CarriesOverSkinValueToNextHole()
    {
        var season = MakeSeason();
        var half = MakeHalf(season);
        var flight = MakeFlight(1, half);
        // Hole 1: tie at net 4 (carries). Hole 2: Alice wins outright -> should be worth 2 (1 + 1 carryover).
        var p1 = MakeParticipant(1, "Alice", 1, MakeHole(1, gross: 5, net: 4), MakeHole(2, gross: 4, net: 3));
        var p2 = MakeParticipant(2, "Bob", 1, MakeHole(1, gross: 5, net: 4), MakeHole(2, gross: 5, net: 5));

        var m = new Mocks();
        m.Rounds.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(MakeRound());
        m.Rounds.Setup(r => r.GetParticipantsAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(new List<RoundParticipant> { p1, p2 });
        m.Flights.Setup(f => f.GetByHalfAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(new List<Flight> { flight });

        var result = await m.BuildSut().Handle(new GetRoundSkinsQuery(1), CancellationToken.None);

        var flightSkins = result.Value!.FlightSkins.Single();
        var hole1 = flightSkins.AllHoleResults.Single(h => h.HoleNumber == 1);
        hole1.SkinValue.Should().Be(0, "a tie awards no skin");
        hole1.WasCarryover.Should().BeFalse();

        var hole2 = flightSkins.AllHoleResults.Single(h => h.HoleNumber == 2);
        hole2.SkinValue.Should().Be(2, "the tied skin from hole 1 carries into hole 2's win");
        hole2.WasCarryover.Should().BeTrue();
        hole2.WinnerPlayerId.Should().Be(1);

        flightSkins.TotalSkinValueAwarded.Should().Be(2);
    }

    [Fact]
    public async Task Handle_UnresolvedTieAtRoundEnd_SkinIsLost()
    {
        var season = MakeSeason();
        var half = MakeHalf(season);
        var flight = MakeFlight(1, half);
        // Only hole played is a tie — nobody ever wins it, so no skins awarded at all.
        var p1 = MakeParticipant(1, "Alice", 1, MakeHole(1, gross: 4, net: 4));
        var p2 = MakeParticipant(2, "Bob", 1, MakeHole(1, gross: 4, net: 4));

        var m = new Mocks();
        m.Rounds.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(MakeRound());
        m.Rounds.Setup(r => r.GetParticipantsAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(new List<RoundParticipant> { p1, p2 });
        m.Flights.Setup(f => f.GetByHalfAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(new List<Flight> { flight });

        var result = await m.BuildSut().Handle(new GetRoundSkinsQuery(1), CancellationToken.None);

        var flightSkins = result.Value!.FlightSkins.Single();
        flightSkins.TotalHolesWithSkins.Should().Be(0);
        flightSkins.TotalSkinValueAwarded.Should().Be(0);
        flightSkins.PlayerSummaries.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_ExcludesWithdrawnAndSkippedAndNoScoreParticipants()
    {
        var season = MakeSeason();
        var half = MakeHalf(season);
        var flight = MakeFlight(1, half);
        var active = MakeParticipant(1, "Alice", 1, MakeHole(1, gross: 4, net: 3));
        var withdrawn = MakeParticipant(2, "Bob", 1, MakeHole(1, gross: 3, net: 2));
        withdrawn.IsWithdrawn = true;
        var skipped = MakeParticipant(3, "Carl", 1, MakeHole(1, gross: 2, net: 1));
        skipped.SkippedWeek = true;
        var noScore = MakeParticipant(4, "Dana", 1);

        var m = new Mocks();
        m.Rounds.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(MakeRound());
        m.Rounds.Setup(r => r.GetParticipantsAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<RoundParticipant> { active, withdrawn, skipped, noScore });
        m.Flights.Setup(f => f.GetByHalfAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(new List<Flight> { flight });

        var result = await m.BuildSut().Handle(new GetRoundSkinsQuery(1), CancellationToken.None);

        var flightSkins = result.Value!.FlightSkins.Single();
        // Only Alice counts, so she wins the hole uncontested.
        flightSkins.PlayerSummaries.Should().ContainSingle(p => p.PlayerId == 1);
    }

    [Fact]
    public async Task Handle_MultipleFlightsAreOrderedByDisplayOrderAndSkinsAreIndependentPerFlight()
    {
        var season = MakeSeason();
        var half = MakeHalf(season);
        var flightB = MakeFlight(2, half, displayOrder: 1, name: "B");
        var flightA = MakeFlight(1, half, displayOrder: 0, name: "A");

        var aPlayer = MakeParticipant(1, "Alice", 1, MakeHole(1, gross: 4, net: 3));
        var bPlayer = MakeParticipant(2, "Bob", 2, MakeHole(1, gross: 5, net: 4));

        var m = new Mocks();
        m.Rounds.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(MakeRound());
        m.Rounds.Setup(r => r.GetParticipantsAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<RoundParticipant> { aPlayer, bPlayer });
        // Repository returns flights out of DisplayOrder — handler must sort.
        m.Flights.Setup(f => f.GetByHalfAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(new List<Flight> { flightB, flightA });

        var result = await m.BuildSut().Handle(new GetRoundSkinsQuery(1), CancellationToken.None);

        result.Value!.FlightSkins.Should().HaveCount(2);
        result.Value!.FlightSkins[0].FlightId.Should().Be(1, "Flight A (DisplayOrder 0) must come first");
        result.Value!.FlightSkins[1].FlightId.Should().Be(2);
    }

    [Fact]
    public async Task Handle_GrossPar3Skins_CombinesAcrossFlightsIndependentlyOfNetSkins()
    {
        var season = MakeSeason();
        var half = MakeHalf(season);
        var flightA = MakeFlight(1, half, name: "A");
        var flightB = MakeFlight(2, half, name: "B");

        // Hole 3 is a par 3. Alice (flight A) shoots gross 2, Bob (flight B) shoots gross 4 — cross-flight comparison.
        var alice = MakeParticipant(1, "Alice", 1, MakeHole(3, gross: 2, net: 1, par: 3));
        var bob = MakeParticipant(2, "Bob", 2, MakeHole(3, gross: 4, net: 3, par: 3));

        var m = new Mocks();
        m.Rounds.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(MakeRound());
        m.Rounds.Setup(r => r.GetParticipantsAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<RoundParticipant> { alice, bob });
        m.Flights.Setup(f => f.GetByHalfAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(new List<Flight> { flightA, flightB });

        var result = await m.BuildSut().Handle(new GetRoundSkinsQuery(1), CancellationToken.None);

        result.Value!.GrossPar3Skins.Should().NotBeNull();
        var par3 = result.Value!.GrossPar3Skins!;
        par3.Par3HoleCount.Should().Be(1);
        var winner = par3.HoleResults.Single();
        winner.WinnerPlayerId.Should().Be(1);
        winner.WinnerFlightId.Should().Be(1);
        winner.SkinValue.Should().Be(1);
    }

    [Fact]
    public async Task Handle_GrossPar3Skins_CarriesOverFromPriorRoundsInHalf()
    {
        var season = MakeSeason();
        var half = MakeHalf(season);
        var flightA = MakeFlight(1, half, name: "A");

        // Current round: one par-3 hole where the two players tie on gross.
        var alice = MakeParticipant(1, "Alice", 1, MakeHole(3, gross: 3, net: 2, par: 3));
        var bob = MakeParticipant(2, "Bob", 1, MakeHole(3, gross: 3, net: 2, par: 3));

        // Previous round: same par-3 hole, also tied — contributes +1 carryover
        // into the current round instead of resolving.
        var prevAlice = MakeParticipant(1, "Alice", 1, MakeHole(3, gross: 4, net: 3, par: 3));
        var prevBob = MakeParticipant(2, "Bob", 1, MakeHole(3, gross: 4, net: 3, par: 3));
        var previousRound = MakeRound(id: 0, halfId: 1, weekNumber: 0);
        previousRound.Participants = new List<RoundParticipant> { prevAlice, prevBob };

        var m = new Mocks();
        m.Rounds.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(MakeRound(weekNumber: 1));
        m.Rounds.Setup(r => r.GetParticipantsAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<RoundParticipant> { alice, bob });
        m.Rounds.Setup(r => r.GetPreviousRoundsAsync(1, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Round> { previousRound });
        m.Flights.Setup(f => f.GetByHalfAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(new List<Flight> { flightA });

        var result = await m.BuildSut().Handle(new GetRoundSkinsQuery(1), CancellationToken.None);

        // Current round is also a tie, so still no winner — but the summary should
        // reflect the incoming carryover of 1 from the unresolved previous-round tie.
        var par3 = result.Value!.GrossPar3Skins!;
        par3.IncomingCarryover.Should().Be(1);
        par3.HoleResults.Single().SkinValue.Should().Be(0, "still tied, so no winner this round either");
    }
}
