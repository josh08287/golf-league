using FluentAssertions;
using GolfLeague.Application.Seasons.Commands;
using GolfLeague.Application.Seasons.Queries;
using GolfLeague.Domain.Entities;
using GolfLeague.Domain.Interfaces;
using Moq;
using Xunit;

namespace GolfLeague.Tests.Application;

public class CreateSeasonCommandHandlerTests
{
    [Fact]
    public async Task Handle_CreatesSeason_AndReturnsDto()
    {
        var repo = new Mock<ISeasonRepository>();
        var handler = new CreateSeasonCommandHandler(repo.Object);
        var cmd = new CreateSeasonCommand("2026 Season", 2026,
            new DateOnly(2026, 4, 1), new DateOnly(2026, 10, 31), 10, "admin");

        var result = await handler.Handle(cmd, default);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Name.Should().Be("2026 Season");
        result.Value.Year.Should().Be(2026);
        result.Value.IsActive.Should().BeFalse();
        repo.Verify(r => r.AddAsync(It.IsAny<Season>(), default), Times.Once);
    }

    [Fact]
    public async Task Handle_SetsBestNRounds_WhenProvided()
    {
        var repo = new Mock<ISeasonRepository>();
        var handler = new CreateSeasonCommandHandler(repo.Object);
        var cmd = new CreateSeasonCommand("S", 2026,
            new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31), 8, "admin");

        var result = await handler.Handle(cmd, default);

        result.Value!.BestNRounds.Should().Be(8);
    }
}

public class SetActiveSeasonCommandHandlerTests
{
    [Fact]
    public async Task Handle_WhenSeasonNotFoundAfterSet_ReturnsFail()
    {
        var repo = new Mock<ISeasonRepository>();
        repo.Setup(r => r.GetActiveAsync(default)).ReturnsAsync((Season?)null);
        var handler = new SetActiveSeasonCommandHandler(repo.Object);

        var result = await handler.Handle(new SetActiveSeasonCommand(99, "admin"), default);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("not found");
    }

    [Fact]
    public async Task Handle_WhenSeasonFound_ReturnsDto()
    {
        var season = new Season { Id = 1, Name = "2026", Year = 2026, IsActive = true,
            StartDate = new DateOnly(2026, 4, 1), EndDate = new DateOnly(2026, 10, 31) };
        var repo = new Mock<ISeasonRepository>();
        repo.Setup(r => r.GetActiveAsync(default)).ReturnsAsync(season);
        var handler = new SetActiveSeasonCommandHandler(repo.Object);

        var result = await handler.Handle(new SetActiveSeasonCommand(1, "admin"), default);

        result.IsSuccess.Should().BeTrue();
        result.Value!.IsActive.Should().BeTrue();
        repo.Verify(r => r.SetActiveAsync(1, default), Times.Once);
    }
}

public class GetSeasonsQueryHandlerTests
{
    [Fact]
    public async Task Handle_ReturnsMappedDtos()
    {
        var seasons = new List<Season>
        {
            new() { Id = 1, Name = "2026", Year = 2026, StartDate = new DateOnly(2026, 4, 1), EndDate = new DateOnly(2026, 10, 31), IsActive = true }
        };
        var repo = new Mock<ISeasonRepository>();
        repo.Setup(r => r.GetAllAsync(default)).ReturnsAsync(seasons);
        var handler = new GetSeasonsQueryHandler(repo.Object);

        var result = await handler.Handle(new GetSeasonsQuery(), default);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Should().HaveCount(1);
        result.Value[0].Name.Should().Be("2026");
    }

    [Fact]
    public async Task Handle_MapsAllSeasonFields()
    {
        var seasons = new List<Season>
        {
            new() { Id = 5, Name = "Test", Year = 2026,
                StartDate = new DateOnly(2026, 4, 1), EndDate = new DateOnly(2026, 10, 31),
                IsActive = true, BestNRounds = 6 }
        };
        var repo = new Mock<ISeasonRepository>();
        repo.Setup(r => r.GetAllAsync(default)).ReturnsAsync(seasons);
        var handler = new GetSeasonsQueryHandler(repo.Object);

        var result = await handler.Handle(new GetSeasonsQuery(), default);

        var dto = result.Value![0];
        dto.Id.Should().Be(5);
        dto.Name.Should().Be("Test");
        dto.StartDate.Should().Be("2026-04-01");
        dto.EndDate.Should().Be("2026-10-31");
        dto.IsActive.Should().BeTrue();
        dto.BestNRounds.Should().Be(6);
    }
}
