using FluentAssertions;
using GolfLeague.Application.Rounds.Commands;
using GolfLeague.Application.Rounds.Queries;
using GolfLeague.Domain.Entities;
using GolfLeague.Domain.Enums;
using GolfLeague.Domain.Interfaces;
using Moq;
using Xunit;

namespace GolfLeague.Tests.Application;

public class SetClosestToPinWinnersCommandHandlerTests
{
    private static Player MakePlayer(int id) => new() { Id = id, FirstName = "P", LastName = id.ToString() };

    private static List<CourseHole> FrontNineWithPar3sAt(params int[] par3Holes) =>
        Enumerable.Range(1, 9)
            .Select(n => new CourseHole { HoleNumber = n, Par = par3Holes.Contains(n) ? 3 : 4, StrokeIndex = n })
            .ToList();

    private static (Mock<IRoundRepository> Rounds, Mock<ICourseRepository> Courses, SetClosestToPinWinnersCommandHandler Handler)
        BuildSut(Round? round, List<CourseHole>? holes = null, List<RoundParticipant>? participants = null)
    {
        var rounds = new Mock<IRoundRepository>();
        var courses = new Mock<ICourseRepository>();
        rounds.Setup(r => r.GetByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync(round);
        courses.Setup(c => c.GetHolesAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(holes ?? []);
        rounds.Setup(r => r.GetParticipantsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(participants ?? []);
        return (rounds, courses, new SetClosestToPinWinnersCommandHandler(rounds.Object, courses.Object));
    }

    [Fact]
    public async Task Handle_WhenRoundNotFound_ReturnsFail()
    {
        var (_, _, handler) = BuildSut(round: null);

        var result = await handler.Handle(new SetClosestToPinWinnersCommand(1, [], "u"), default);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("not found");
    }

    [Fact]
    public async Task Handle_WhenRoundFinalized_ReturnsFail()
    {
        var round = new Round { Id = 1, Status = RoundStatus.Finalized };
        var (_, _, handler) = BuildSut(round);

        var result = await handler.Handle(new SetClosestToPinWinnersCommand(1, [], "u"), default);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Finalized");
    }

    [Fact]
    public async Task Handle_WhenHoleIsNotPar3_ReturnsFail()
    {
        var round = new Round { Id = 1, CourseId = 5, NineHoleSide = NineHoleSide.Front, Status = RoundStatus.Scheduled };
        var participants = new List<RoundParticipant> { new() { PlayerId = 7, Player = MakePlayer(7) } };
        var (_, _, handler) = BuildSut(round, FrontNineWithPar3sAt(2, 6), participants);

        // Hole 3 is a par 4 on this course
        var cmd = new SetClosestToPinWinnersCommand(1, [new ClosestToPinSelection(3, 7)], "u");
        var result = await handler.Handle(cmd, default);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("not a par 3");
    }

    [Fact]
    public async Task Handle_WhenPlayerNotActiveParticipant_ReturnsFail()
    {
        var round = new Round { Id = 1, CourseId = 5, NineHoleSide = NineHoleSide.Front, Status = RoundStatus.Scheduled };
        var participants = new List<RoundParticipant>
        {
            new() { PlayerId = 7, Player = MakePlayer(7), SkippedWeek = true },
        };
        var (_, _, handler) = BuildSut(round, FrontNineWithPar3sAt(2), participants);

        var cmd = new SetClosestToPinWinnersCommand(1, [new ClosestToPinSelection(2, 7)], "u");
        var result = await handler.Handle(cmd, default);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("not an active participant");
    }

    [Fact]
    public async Task Handle_WithValidSelections_SavesOnlyHolesWithWinners()
    {
        var round = new Round { Id = 1, CourseId = 5, NineHoleSide = NineHoleSide.Front, Status = RoundStatus.Scheduled };
        var participants = new List<RoundParticipant>
        {
            new() { PlayerId = 7, Player = MakePlayer(7) },
            new() { PlayerId = 8, Player = MakePlayer(8) },
        };
        var (rounds, _, handler) = BuildSut(round, FrontNineWithPar3sAt(2, 6), participants);

        // Hole 2 has a winner; hole 6 is explicitly "None"
        var cmd = new SetClosestToPinWinnersCommand(1,
            [new ClosestToPinSelection(2, 7), new ClosestToPinSelection(6, null)], "u");
        var result = await handler.Handle(cmd, default);

        result.IsSuccess.Should().BeTrue();
        rounds.Verify(r => r.SetClosestToPinWinnersAsync(1,
            It.Is<IEnumerable<(int HoleNumber, int PlayerId)>>(w =>
                w.Count() == 1 && w.First().HoleNumber == 2 && w.First().PlayerId == 7),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithAllNoneSelections_ClearsWinners()
    {
        var round = new Round { Id = 1, CourseId = 5, NineHoleSide = NineHoleSide.Front, Status = RoundStatus.Scheduled };
        var (rounds, _, handler) = BuildSut(round, FrontNineWithPar3sAt(2));

        var cmd = new SetClosestToPinWinnersCommand(1, [new ClosestToPinSelection(2, null)], "u");
        var result = await handler.Handle(cmd, default);

        result.IsSuccess.Should().BeTrue();
        rounds.Verify(r => r.SetClosestToPinWinnersAsync(1,
            It.Is<IEnumerable<(int HoleNumber, int PlayerId)>>(w => !w.Any()),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}

public class GetRoundClosestToPinQueryHandlerTests
{
    private static Player MakePlayer(int id) => new() { Id = id, FirstName = "P", LastName = id.ToString() };

    [Fact]
    public async Task Handle_ReturnsPar3HolesWithWinnersAndActiveParticipants()
    {
        var round = new Round { Id = 1, CourseId = 5, NineHoleSide = NineHoleSide.Back, Status = RoundStatus.Scheduled };
        var holes = Enumerable.Range(10, 9)
            .Select(n => new CourseHole { HoleNumber = n, Par = n == 12 || n == 16 ? 3 : 4, StrokeIndex = n - 9 })
            .ToList();
        // A front-nine par 3 that must NOT appear for a back-nine round
        holes.Add(new CourseHole { HoleNumber = 2, Par = 3, StrokeIndex = 10 });

        var rounds = new Mock<IRoundRepository>();
        var courses = new Mock<ICourseRepository>();
        rounds.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(round);
        courses.Setup(c => c.GetHolesAsync(5, It.IsAny<CancellationToken>())).ReturnsAsync(holes);
        rounds.Setup(r => r.GetClosestToPinWinnersAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync([new RoundClosestToPin { RoundId = 1, HoleNumber = 12, PlayerId = 7, Player = MakePlayer(7) }]);
        rounds.Setup(r => r.GetParticipantsAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                new RoundParticipant { PlayerId = 7, Player = MakePlayer(7) },
                new RoundParticipant { PlayerId = 8, Player = MakePlayer(8), IsWithdrawn = true },
                new RoundParticipant { PlayerId = 9, Player = MakePlayer(9), SkippedWeek = true },
            ]);
        var handler = new GetRoundClosestToPinQueryHandler(rounds.Object, courses.Object);

        var result = await handler.Handle(new GetRoundClosestToPinQuery(1), default);

        result.IsSuccess.Should().BeTrue();
        var dto = result.Value!;
        dto.Par3Holes.Select(h => h.HoleNumber).Should().Equal(12, 16);
        dto.Par3Holes.First(h => h.HoleNumber == 12).PlayerId.Should().Be(7);
        dto.Par3Holes.First(h => h.HoleNumber == 16).PlayerId.Should().BeNull();
        dto.Participants.Should().ContainSingle(p => p.PlayerId == 7);
    }
}
