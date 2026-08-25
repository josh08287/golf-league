using FluentAssertions;
using GolfLeague.Application.Flights.Commands;
using GolfLeague.Application.Interfaces;
using GolfLeague.Domain.Entities;
using GolfLeague.Domain.Enums;
using GolfLeague.Domain.Interfaces;
using Moq;
using Xunit;

namespace GolfLeague.Tests.Application;

public class GenerateMatchPlayScheduleCommandHandlerTests
{
    private static SeasonHalf MakeHalf(ScoringFormat format = ScoringFormat.MatchPlay) => new()
    {
        Id = 1,
        SeasonId = 1,
        HalfNumber = 1,
        Name = "First Half",
        ScoringFormat = format,
    };

    private static Flight MakeFlight(int id, params int[] playerIds) => new()
    {
        Id = id,
        HalfId = 1,
        SeasonId = 1,
        Name = $"Flight{id}",
        Memberships = playerIds.Select(pid => new FlightMembership { PlayerId = pid, FlightId = id, HalfId = 1, SeasonId = 1 }).ToList(),
    };

    private static Round MakeRound(int id, int weekNumber) => new() { Id = id, HalfId = 1, WeekNumber = weekNumber, CourseId = 1 };

    private sealed class Mocks
    {
        public Mock<IFlightRepository> FlightRepo { get; } = new();
        public Mock<IRoundRepository> RoundRepo { get; } = new();
        public Mock<IFlightMatchRepository> FlightMatchRepo { get; } = new();
        public Mock<ILeagueContext> LeagueContext { get; } = new();

        public Mocks()
        {
            LeagueContext.Setup(c => c.LeagueId).Returns(1);
            FlightRepo.Setup(f => f.GetHalfByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(MakeHalf());
            FlightRepo.Setup(f => f.IsHalfLockedAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(false);
            RoundRepo.Setup(r => r.GetByHalfAsync(1, It.IsAny<CancellationToken>()))
                .ReturnsAsync((IReadOnlyList<Round>)[MakeRound(10, 1), MakeRound(11, 2), MakeRound(12, 3)]);
        }

        public GenerateMatchPlayScheduleCommandHandler BuildSut() =>
            new(FlightRepo.Object, RoundRepo.Object, FlightMatchRepo.Object, LeagueContext.Object);
    }

    [Fact]
    public async Task Handle_HalfNotFound_ReturnsFail()
    {
        var m = new Mocks();
        m.FlightRepo.Setup(f => f.GetHalfByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync((SeasonHalf?)null);

        var result = await m.BuildSut().Handle(new GenerateMatchPlayScheduleCommand(1, "user"), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_HalfLocked_ReturnsFail()
    {
        var m = new Mocks();
        m.FlightRepo.Setup(f => f.IsHalfLockedAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        m.FlightRepo.Setup(f => f.GetByHalfAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync((IReadOnlyList<Flight>)[MakeFlight(1, 100, 200)]);

        var result = await m.BuildSut().Handle(new GenerateMatchPlayScheduleCommand(1, "user"), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_HalfIsStableford_ReturnsFail()
    {
        var m = new Mocks();
        m.FlightRepo.Setup(f => f.GetHalfByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(MakeHalf(ScoringFormat.Stableford));

        var result = await m.BuildSut().Handle(new GenerateMatchPlayScheduleCommand(1, "user"), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("not configured for match play");
    }

    [Fact]
    public async Task Handle_RegenerationDeletesPriorRowsBeforeCreatingNew()
    {
        var m = new Mocks();
        m.FlightRepo.Setup(f => f.GetByHalfAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync((IReadOnlyList<Flight>)[MakeFlight(1, 100, 200, 300, 400)]);

        var result = await m.BuildSut().Handle(new GenerateMatchPlayScheduleCommand(1, "user"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        m.FlightMatchRepo.Verify(r => r.DeleteByHalfAsync(1, It.IsAny<CancellationToken>()), Times.Once);
        m.FlightMatchRepo.Verify(r => r.AddRangeAsync(It.IsAny<IEnumerable<FlightMatch>>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_MultipleFlights_EachScheduledIndependently()
    {
        var m = new Mocks();
        m.FlightRepo.Setup(f => f.GetByHalfAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(
            (IReadOnlyList<Flight>)[MakeFlight(1, 100, 200, 300, 400), MakeFlight(2, 500, 600)]);

        var addedRanges = new List<List<FlightMatch>>();
        m.FlightMatchRepo.Setup(r => r.AddRangeAsync(It.IsAny<IEnumerable<FlightMatch>>(), It.IsAny<CancellationToken>()))
            .Callback<IEnumerable<FlightMatch>, CancellationToken>((matches, _) => addedRanges.Add(matches.ToList()))
            .Returns(Task.CompletedTask);

        var result = await m.BuildSut().Handle(new GenerateMatchPlayScheduleCommand(1, "user"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.FlightSummaries.Should().HaveCount(2);
        addedRanges.Should().HaveCount(2);
        addedRanges[0].Should().OnlyContain(fm => fm.FlightId == 1);
        addedRanges[1].Should().OnlyContain(fm => fm.FlightId == 2);
    }

    [Fact]
    public async Task Handle_OddFlightSizeAndWeekMismatch_SurfacesWarnings()
    {
        var m = new Mocks();
        // 3 players -> 3-round circle (1 bye each round); only 3 weeks available -> exact fit for player count 3,
        // but flight of 5 needs 5 rounds with only 3 weeks -> fewer-weeks-than-needed warning.
        m.FlightRepo.Setup(f => f.GetByHalfAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(
            (IReadOnlyList<Flight>)[MakeFlight(1, 100, 200, 300, 400, 500)]);

        var result = await m.BuildSut().Handle(new GenerateMatchPlayScheduleCommand(1, "user"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Warnings.Should().NotBeEmpty();
        result.Value.FlightSummaries.Should().ContainSingle(s => s.HasBye);
    }
}
