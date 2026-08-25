using FluentAssertions;
using GolfLeague.Application.Common;
using GolfLeague.Application.Flights.Queries;
using GolfLeague.Domain.Entities;
using GolfLeague.Domain.Interfaces;
using Moq;
using Xunit;

namespace GolfLeague.Tests.Application;

public class GetMatchPlayStandingsQueryHandlerTests
{
    private static Flight MakeFlight(int id = 1) => new() { Id = id, Name = "A" };

    private static Player MakePlayer(int id, string name) => new() { Id = id, FirstName = name, LastName = "P" };

    private static Round MakeRound(int id, int weekNumber) => new() { Id = id, WeekNumber = weekNumber, RoundDate = new DateOnly(2026, 6, weekNumber), CourseId = 1 };

    private static FlightMatch MakeMatch(
        int id, int weekNumber, int player1Id, int? player2Id,
        int? p1Points, int? p2Points, int? p1HolesWon = null, int? p2HolesWon = null) => new()
    {
        Id = id,
        FlightId = 1,
        HalfId = 1,
        RoundId = id,
        Round = MakeRound(id, weekNumber),
        WeekNumber = weekNumber,
        Player1Id = player1Id,
        Player2Id = player2Id,
        Player1Points = p1Points,
        Player2Points = p2Points,
        Player1HolesWon = p1HolesWon,
        Player2HolesWon = p2HolesWon,
    };

    private sealed class Mocks
    {
        public Mock<IFlightRepository> FlightRepo { get; } = new();
        public Mock<IFlightMatchRepository> FlightMatchRepo { get; } = new();
        public Mock<IHandicapRepository> HandicapRepo { get; } = new();
        public Mock<IPlayerRepository> PlayerRepo { get; } = new();

        public Mocks()
        {
            FlightRepo.Setup(f => f.GetByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync(MakeFlight());
            HandicapRepo.Setup(h => h.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<Handicap>());
            PlayerRepo.Setup(p => p.GetAllAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync((IReadOnlyList<Player>)new[] { 100, 200, 300, 400 }.Select(id => MakePlayer(id, $"Player{id}")).ToList());
        }

        public GetMatchPlayStandingsQueryHandler BuildSut() =>
            new(FlightRepo.Object, FlightMatchRepo.Object, HandicapRepo.Object, PlayerRepo.Object);
    }

    [Fact]
    public async Task Handle_FlightNotFound_ReturnsFail()
    {
        var m = new Mocks();
        m.FlightRepo.Setup(f => f.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync((Flight?)null);

        var result = await m.BuildSut().Handle(new GetMatchPlayStandingsQuery(1, 1), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_RanksByTotalPointsThenAverageAsTiebreak()
    {
        var m = new Mocks();
        // Player 100 beats 200 (2-0, 1 hole), player 300 halves 400 (1-1).
        m.FlightMatchRepo.Setup(f => f.GetByFlightAsync(1, 1, It.IsAny<CancellationToken>())).ReturnsAsync(
            (IReadOnlyList<FlightMatch>)[
                MakeMatch(1, 1, 100, 200, p1Points: 6, p2Points: 0, p1HolesWon: 1, p2HolesWon: 0),
                MakeMatch(2, 1, 300, 400, p1Points: 5, p2Points: 5, p1HolesWon: 1, p2HolesWon: 1),
            ]);

        var result = await m.BuildSut().Handle(new GetMatchPlayStandingsQuery(1, 1), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var standings = result.Value!;
        standings.Should().Contain(s => s.PlayerId == 100 && s.Position == 1 && s.TotalPoints == 6);
        var byPlayer = standings.ToDictionary(s => s.PlayerId);
        byPlayer[100].Wins.Should().Be(1);
        byPlayer[200].Losses.Should().Be(1);
        byPlayer[300].Halves.Should().Be(1);
    }

    [Fact]
    public async Task Handle_ByeMatch_CountsPointsAndMatchesPlayedForPresentPlayerOnly()
    {
        var m = new Mocks();
        m.FlightMatchRepo.Setup(f => f.GetByFlightAsync(1, 1, It.IsAny<CancellationToken>())).ReturnsAsync(
            (IReadOnlyList<FlightMatch>)[
                MakeMatch(1, 1, 100, null, p1Points: 12, p2Points: null, p1HolesWon: 5, p2HolesWon: 0),
            ]);

        var result = await m.BuildSut().Handle(new GetMatchPlayStandingsQuery(1, 1), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var standings = result.Value!;
        standings.Should().ContainSingle();
        standings[0].PlayerId.Should().Be(100);
        standings[0].TotalPoints.Should().Be(12);
        standings[0].MatchesPlayed.Should().Be(1);
    }

    [Fact]
    public async Task Handle_SortByColumn_AppliesRequestedSort()
    {
        var m = new Mocks();
        m.FlightMatchRepo.Setup(f => f.GetByFlightAsync(1, 1, It.IsAny<CancellationToken>())).ReturnsAsync(
            (IReadOnlyList<FlightMatch>)[
                MakeMatch(1, 1, 100, 200, p1Points: 3, p2Points: 9, p1HolesWon: 0, p2HolesWon: 2),
            ]);

        var sort = new SortRequest("points", SortDirection.Ascending);
        var result = await m.BuildSut().Handle(new GetMatchPlayStandingsQuery(1, 1, sort), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Select(s => s.PlayerId).Should().Equal(100, 200);
    }
}
