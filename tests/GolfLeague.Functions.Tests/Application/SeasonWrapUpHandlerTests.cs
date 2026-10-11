using FluentAssertions;
using GolfLeague.Application.Flights.Queries;
using GolfLeague.Application.Statistics.Queries;
using GolfLeague.Domain.Entities;
using GolfLeague.Domain.Enums;
using GolfLeague.Domain.Interfaces;
using MediatR;
using Moq;
using Xunit;

namespace GolfLeague.Tests.Application;

public class SeasonWrapUpHandlerTests
{
    private static Season MakeSeason(int id = 1, string name = "2026 Season") => new()
    {
        Id = id,
        Name = name,
        Year = 2026,
        Halves = [new SeasonHalf { Id = 1, SeasonId = id, HalfNumber = 1, Name = "Half 1" }]
    };

    private static Player MakePlayer(int id, string name, bool isActive = true) => new()
    {
        Id = id,
        FirstName = name,
        LastName = "P",
        IsActive = isActive,
    };

    private static Round MakeRound(int id, int seasonId = 1, int halfId = 1) => new()
    {
        Id = id,
        SeasonId = seasonId,
        HalfId = halfId,
        RoundType = RoundType.NineHole,
        Status = RoundStatus.Finalized,
        RoundDate = new DateOnly(2026, 6, id),
        WeekNumber = id,
    };

    [Fact]
    public async Task Handle_SeasonWrapUp_ExcludesInactivePlayersFromAllCategories()
    {
        var season = MakeSeason();
        var rounds = (IReadOnlyList<Round>)[MakeRound(1), MakeRound(2)];

        var activePlayer1 = MakePlayer(1, "Active1", isActive: true);
        var activePlayer2 = MakePlayer(2, "Active2", isActive: true);
        var inactivePlayer = MakePlayer(3, "Inactive", isActive: false);

        var participants = new List<RoundParticipant>
        {
            // Inactive player had the lowest gross, lowest net, and highest Stableford points
            new()
            {
                Id = 101,
                RoundId = 1,
                PlayerId = 3,
                Player = inactivePlayer,
                TotalGrossStrokes = 30,
                TotalNetStrokes = 28,
                TotalGrossStablefordPoints = 30,
                TotalNetStablefordPoints = 35,
            },
            new()
            {
                Id = 102,
                RoundId = 1,
                PlayerId = 1,
                Player = activePlayer1,
                TotalGrossStrokes = 38,
                TotalNetStrokes = 34,
                TotalGrossStablefordPoints = 20,
                TotalNetStablefordPoints = 22,
            },
            new()
            {
                Id = 103,
                RoundId = 1,
                PlayerId = 2,
                Player = activePlayer2,
                TotalGrossStrokes = 42,
                TotalNetStrokes = 36,
                TotalGrossStablefordPoints = 16,
                TotalNetStablefordPoints = 18,
            },
        };

        var seasonRepo = new Mock<ISeasonRepository>();
        seasonRepo.Setup(s => s.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(season);

        var roundRepo = new Mock<IRoundRepository>();
        roundRepo.Setup(r => r.GetBySeasonAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(rounds);
        roundRepo.Setup(r => r.GetParticipantsForRoundsAsync(It.IsAny<IEnumerable<int>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(participants);

        var flightRepo = new Mock<IFlightRepository>();
        flightRepo.Setup(f => f.GetByHalfAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<Flight>)[]);

        var mediator = new Mock<IMediator>();
        mediator.Setup(m => m.Send(It.IsAny<GetMostImprovedPlayerQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(GolfLeague.Application.Common.Result<MostImprovedResultDto>.Ok(
                new MostImprovedResultDto(null, [], "Half 1", 2)));

        var handler = new GetSeasonWrapUpQueryHandler(
            seasonRepo.Object, roundRepo.Object, flightRepo.Object, mediator.Object);

        var result = await handler.Handle(new GetSeasonWrapUpQuery(1), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var dto = result.Value!;

        // Season Low Gross: inactive player 3 should NOT be present
        dto.SeasonLowGross.Should().NotContain(s => s.PlayerId == 3);
        dto.SeasonLowGross.First().PlayerId.Should().Be(1);

        // Season Low Net: inactive player 3 should NOT be present
        dto.SeasonLowNet.Should().NotContain(s => s.PlayerId == 3);
        dto.SeasonLowNet.First().PlayerId.Should().Be(1);

        // Season High Gross Points: inactive player 3 should NOT win
        dto.SeasonHighGrossPoints.Should().NotBeNull();
        dto.SeasonHighGrossPoints!.PlayerId.Should().Be(1);

        // Season High Net Points: inactive player 3 should NOT win
        dto.SeasonHighNetPoints.Should().NotBeNull();
        dto.SeasonHighNetPoints!.PlayerId.Should().Be(1);
    }

    [Fact]
    public async Task Handle_MostImproved_ExcludesInactivePlayers()
    {
        var season = MakeSeason();
        var rounds = (IReadOnlyList<Round>)[MakeRound(1), MakeRound(2)];

        var activePlayer = MakePlayer(1, "Active", isActive: true);
        var inactivePlayer = MakePlayer(2, "Inactive", isActive: false);

        var participants = new List<RoundParticipant>
        {
            new() { Id = 1, RoundId = 1, PlayerId = 1, Player = activePlayer, TotalGrossStrokes = 45 },
            new() { Id = 2, RoundId = 2, PlayerId = 1, Player = activePlayer, TotalGrossStrokes = 40 },
            // Inactive player with 2 rounds
            new() { Id = 3, RoundId = 1, PlayerId = 2, Player = inactivePlayer, TotalGrossStrokes = 50 },
            new() { Id = 4, RoundId = 2, PlayerId = 2, Player = inactivePlayer, TotalGrossStrokes = 35 },
        };

        var seasonRepo = new Mock<ISeasonRepository>();
        seasonRepo.Setup(s => s.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(season);

        var roundRepo = new Mock<IRoundRepository>();
        roundRepo.Setup(r => r.GetBySeasonAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(rounds);
        roundRepo.Setup(r => r.GetParticipantsForRoundsAsync(It.IsAny<IEnumerable<int>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(participants);

        var handicapRepo = new Mock<IHandicapRepository>();
        handicapRepo.Setup(h => h.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(
            new List<Handicap>
            {
                new() { Id = 1, PlayerId = 1, HandicapIndex = 15.0, EffectiveDate = new DateOnly(2026, 5, 1) },
                new() { Id = 2, PlayerId = 1, HandicapIndex = 10.0, EffectiveDate = new DateOnly(2026, 6, 20) },
                new() { Id = 3, PlayerId = 2, HandicapIndex = 25.0, EffectiveDate = new DateOnly(2026, 5, 1) },
                new() { Id = 4, PlayerId = 2, HandicapIndex = 10.0, EffectiveDate = new DateOnly(2026, 6, 20) },
            });

        var handler = new GetMostImprovedPlayerQueryHandler(
            seasonRepo.Object, roundRepo.Object, handicapRepo.Object);

        var result = await handler.Handle(new GetMostImprovedPlayerQuery(SeasonId: 1), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var dto = result.Value!;

        // Inactive player 2 must NOT be the winner or on the leaderboard
        dto.Winner.Should().NotBeNull();
        dto.Winner!.PlayerId.Should().Be(1);
        dto.Leaderboard.Should().NotContain(p => p.PlayerId == 2);
    }
}
