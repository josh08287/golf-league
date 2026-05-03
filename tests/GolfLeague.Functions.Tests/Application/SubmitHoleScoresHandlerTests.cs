using FluentAssertions;
using GolfLeague.Application.Rounds.Commands;
using GolfLeague.Domain.Entities;
using GolfLeague.Domain.Enums;
using GolfLeague.Domain.Interfaces;
using Moq;
using Xunit;

namespace GolfLeague.Tests.Application;

public class SubmitHoleScoresCommandHandlerTests
{
    private static Course MakeCourse() => new()
    {
        Id = 1, Name = "Course", CourseRating = 72.0, SlopeRating = 113
    };

    private static Round MakeScheduledRound() => new()
    {
        Id = 1, SeasonId = 1, FlightId = 1, CourseId = 1,
        RoundDate = DateOnly.FromDateTime(DateTime.UtcNow),
        Status = RoundStatus.Scheduled
    };

    private static RoundParticipant MakeParticipant() => new()
    {
        Id = 1, RoundId = 1, PlayerId = 1, CourseHandicap = 9, IsWithdrawn = false
    };

    private static Player MakePlayer() => new() { Id = 1, FirstName = "John", LastName = "Doe" };

    private static List<CourseHole> MakeHoles() =>
        Enumerable.Range(1, 9).Select(h => new CourseHole { HoleNumber = h, Par = 4, StrokeIndex = h }).ToList();

    [Fact]
    public async Task Handle_WhenRoundNotFound_ReturnsFail()
    {
        var roundRepo = new Mock<IRoundRepository>();
        roundRepo.Setup(r => r.GetByIdAsync(99, default)).ReturnsAsync((Round?)null);
        var handler = new SubmitHoleScoresCommandHandler(roundRepo.Object, Mock.Of<ICourseRepository>(), Mock.Of<IPlayerRepository>(), Mock.Of<IHandicapRepository>());

        var result = await handler.Handle(new SubmitHoleScoresCommand(99, 1, [], "admin"), default);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("not found");
    }

    [Fact]
    public async Task Handle_WhenRoundFinalized_ReturnsFail()
    {
        var round = MakeScheduledRound();
        round.Status = RoundStatus.Finalized;
        var roundRepo = new Mock<IRoundRepository>();
        roundRepo.Setup(r => r.GetByIdAsync(1, default)).ReturnsAsync(round);
        var handler = new SubmitHoleScoresCommandHandler(roundRepo.Object, Mock.Of<ICourseRepository>(), Mock.Of<IPlayerRepository>(), Mock.Of<IHandicapRepository>());

        var result = await handler.Handle(new SubmitHoleScoresCommand(1, 1, [], "admin"), default);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Finalized");
    }

    [Fact]
    public async Task Handle_WhenRoundCancelled_ReturnsFail()
    {
        var round = MakeScheduledRound();
        round.Status = RoundStatus.Cancelled;
        var roundRepo = new Mock<IRoundRepository>();
        roundRepo.Setup(r => r.GetByIdAsync(1, default)).ReturnsAsync(round);
        var handler = new SubmitHoleScoresCommandHandler(roundRepo.Object, Mock.Of<ICourseRepository>(), Mock.Of<IPlayerRepository>(), Mock.Of<IHandicapRepository>());

        var result = await handler.Handle(new SubmitHoleScoresCommand(1, 1, [], "admin"), default);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Cancelled");
    }

    [Fact]
    public async Task Handle_WhenParticipantNotFound_ReturnsFail()
    {
        var round = MakeScheduledRound();
        var roundRepo = new Mock<IRoundRepository>();
        roundRepo.Setup(r => r.GetByIdAsync(1, default)).ReturnsAsync(round);
        roundRepo.Setup(r => r.GetParticipantAsync(1, 99, default)).ReturnsAsync((RoundParticipant?)null);
        var handler = new SubmitHoleScoresCommandHandler(roundRepo.Object, Mock.Of<ICourseRepository>(), Mock.Of<IPlayerRepository>(), Mock.Of<IHandicapRepository>());

        var result = await handler.Handle(new SubmitHoleScoresCommand(1, 99, [], "admin"), default);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("not a participant");
    }

    [Fact]
    public async Task Handle_WhenParticipantWithdrawn_ReturnsFail()
    {
        var round = MakeScheduledRound();
        var participant = MakeParticipant();
        participant.IsWithdrawn = true;

        var roundRepo = new Mock<IRoundRepository>();
        roundRepo.Setup(r => r.GetByIdAsync(1, default)).ReturnsAsync(round);
        roundRepo.Setup(r => r.GetParticipantAsync(1, 1, default)).ReturnsAsync(participant);
        var handler = new SubmitHoleScoresCommandHandler(roundRepo.Object, Mock.Of<ICourseRepository>(), Mock.Of<IPlayerRepository>(), Mock.Of<IHandicapRepository>());

        var result = await handler.Handle(new SubmitHoleScoresCommand(1, 1, [], "admin"), default);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("withdrawn");
    }

    [Fact]
    public async Task Handle_WhenCourseOrPlayerNotFound_ReturnsFail()
    {
        var round = MakeScheduledRound();
        var participant = MakeParticipant();

        var roundRepo = new Mock<IRoundRepository>();
        roundRepo.Setup(r => r.GetByIdAsync(1, default)).ReturnsAsync(round);
        roundRepo.Setup(r => r.GetParticipantAsync(1, 1, default)).ReturnsAsync(participant);
        var courseRepo = new Mock<ICourseRepository>();
        courseRepo.Setup(r => r.GetHolesAsync(1, default)).ReturnsAsync(new List<CourseHole>());
        courseRepo.Setup(r => r.GetByIdAsync(1, default)).ReturnsAsync((Course?)null);
        var playerRepo = new Mock<IPlayerRepository>();
        playerRepo.Setup(r => r.GetByIdAsync(1, default)).ReturnsAsync(MakePlayer());

        var handler = new SubmitHoleScoresCommandHandler(roundRepo.Object, courseRepo.Object, playerRepo.Object, Mock.Of<IHandicapRepository>());
        var result = await handler.Handle(new SubmitHoleScoresCommand(1, 1, [], "admin"), default);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Course or player");
    }

    [Fact]
    public async Task Handle_WhenHoleNotFound_ReturnsFail()
    {
        var round = MakeScheduledRound();
        var participant = MakeParticipant();
        var player = MakePlayer();
        var course = MakeCourse();

        var roundRepo = new Mock<IRoundRepository>();
        roundRepo.Setup(r => r.GetByIdAsync(1, default)).ReturnsAsync(round);
        roundRepo.Setup(r => r.GetParticipantAsync(1, 1, default)).ReturnsAsync(participant);
        var courseRepo = new Mock<ICourseRepository>();
        courseRepo.Setup(r => r.GetHolesAsync(1, default)).ReturnsAsync(new List<CourseHole>());
        courseRepo.Setup(r => r.GetByIdAsync(1, default)).ReturnsAsync(course);
        var playerRepo = new Mock<IPlayerRepository>();
        playerRepo.Setup(r => r.GetByIdAsync(1, default)).ReturnsAsync(player);
        var handicapRepo = new Mock<IHandicapRepository>();

        var handler = new SubmitHoleScoresCommandHandler(roundRepo.Object, courseRepo.Object, playerRepo.Object, handicapRepo.Object);
        var scores = new List<HoleScoreInput> { new(1, 5) };

        var result = await handler.Handle(new SubmitHoleScoresCommand(1, 1, scores, "admin"), default);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Hole 1 not found");
    }

    [Fact]
    public async Task Handle_WhenValid_SavesHoleScoresAndUpdatesParticipant()
    {
        var round = MakeScheduledRound();
        var participant = MakeParticipant();
        var player = MakePlayer();
        var course = MakeCourse();
        var holes = MakeHoles();

        var roundRepo = new Mock<IRoundRepository>();
        roundRepo.Setup(r => r.GetByIdAsync(1, default)).ReturnsAsync(round);
        roundRepo.Setup(r => r.GetParticipantAsync(1, 1, default)).ReturnsAsync(participant);
        roundRepo.Setup(r => r.GetParticipantsAsyncByPlayer(1, default)).ReturnsAsync(new List<RoundParticipant>());
        var courseRepo = new Mock<ICourseRepository>();
        courseRepo.Setup(r => r.GetHolesAsync(1, default)).ReturnsAsync(holes);
        courseRepo.Setup(r => r.GetByIdAsync(1, default)).ReturnsAsync(course);
        var playerRepo = new Mock<IPlayerRepository>();
        playerRepo.Setup(r => r.GetByIdAsync(1, default)).ReturnsAsync(player);
        var handicapRepo = new Mock<IHandicapRepository>();

        var handler = new SubmitHoleScoresCommandHandler(roundRepo.Object, courseRepo.Object, playerRepo.Object, handicapRepo.Object);
        var scores = holes.Select(h => new HoleScoreInput(h.HoleNumber, 5)).ToList();

        var result = await handler.Handle(new SubmitHoleScoresCommand(1, 1, scores, "admin"), default);

        result.IsSuccess.Should().BeTrue();
        roundRepo.Verify(r => r.AddHoleScoresAsync(It.Is<IEnumerable<HoleScore>>(h => h.Count() == 9), default), Times.Once);
        roundRepo.Verify(r => r.UpdateParticipantAsync(participant, default), Times.Once);
    }

    [Fact]
    public async Task Handle_CapsGrossScoreAtMaxGross()
    {
        var round = MakeScheduledRound();
        var participant = MakeParticipant();
        participant.CourseHandicap = 18; // gets 1 stroke on every hole
        var player = MakePlayer();
        var course = MakeCourse();
        // Single hole: par 4, SI 1 => with CH=18 strokes on hole=1, maxGross=4+2+1=7
        var holes = new List<CourseHole> { new() { HoleNumber = 1, Par = 4, StrokeIndex = 1 } };

        var roundRepo = new Mock<IRoundRepository>();
        roundRepo.Setup(r => r.GetByIdAsync(1, default)).ReturnsAsync(round);
        roundRepo.Setup(r => r.GetParticipantAsync(1, 1, default)).ReturnsAsync(participant);
        roundRepo.Setup(r => r.GetParticipantsAsyncByPlayer(1, default)).ReturnsAsync(new List<RoundParticipant>());

        List<HoleScore>? capturedScores = null;
        roundRepo.Setup(r => r.AddHoleScoresAsync(It.IsAny<IEnumerable<HoleScore>>(), default))
            .Callback<IEnumerable<HoleScore>, CancellationToken>((s, _) => capturedScores = s.ToList())
            .Returns(Task.CompletedTask);

        var courseRepo = new Mock<ICourseRepository>();
        courseRepo.Setup(r => r.GetHolesAsync(1, default)).ReturnsAsync(holes);
        courseRepo.Setup(r => r.GetByIdAsync(1, default)).ReturnsAsync(course);
        var playerRepo = new Mock<IPlayerRepository>();
        playerRepo.Setup(r => r.GetByIdAsync(1, default)).ReturnsAsync(player);
        var handicapRepo = new Mock<IHandicapRepository>();

        var handler = new SubmitHoleScoresCommandHandler(roundRepo.Object, courseRepo.Object, playerRepo.Object, handicapRepo.Object);
        // Submit gross=10, should be capped at 7
        var scores = new List<HoleScoreInput> { new(1, 10) };

        await handler.Handle(new SubmitHoleScoresCommand(1, 1, scores, "admin"), default);

        capturedScores.Should().NotBeNull();
        capturedScores![0].GrossStrokes.Should().Be(7);
        capturedScores[0].IsMaxScore.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_ScoresNotCapped_WhenBelowMax()
    {
        var round = MakeScheduledRound();
        var participant = MakeParticipant();
        participant.CourseHandicap = 0;
        var player = MakePlayer();
        var course = MakeCourse();
        var holes = new List<CourseHole> { new() { HoleNumber = 1, Par = 4, StrokeIndex = 1 } };

        var roundRepo = new Mock<IRoundRepository>();
        roundRepo.Setup(r => r.GetByIdAsync(1, default)).ReturnsAsync(round);
        roundRepo.Setup(r => r.GetParticipantAsync(1, 1, default)).ReturnsAsync(participant);
        roundRepo.Setup(r => r.GetParticipantsAsyncByPlayer(1, default)).ReturnsAsync(new List<RoundParticipant>());

        List<HoleScore>? capturedScores = null;
        roundRepo.Setup(r => r.AddHoleScoresAsync(It.IsAny<IEnumerable<HoleScore>>(), default))
            .Callback<IEnumerable<HoleScore>, CancellationToken>((s, _) => capturedScores = s.ToList())
            .Returns(Task.CompletedTask);

        var courseRepo = new Mock<ICourseRepository>();
        courseRepo.Setup(r => r.GetHolesAsync(1, default)).ReturnsAsync(holes);
        courseRepo.Setup(r => r.GetByIdAsync(1, default)).ReturnsAsync(course);
        var playerRepo = new Mock<IPlayerRepository>();
        playerRepo.Setup(r => r.GetByIdAsync(1, default)).ReturnsAsync(player);
        var handicapRepo = new Mock<IHandicapRepository>();

        var handler = new SubmitHoleScoresCommandHandler(roundRepo.Object, courseRepo.Object, playerRepo.Object, handicapRepo.Object);
        var scores = new List<HoleScoreInput> { new(1, 5) };

        await handler.Handle(new SubmitHoleScoresCommand(1, 1, scores, "admin"), default);

        capturedScores![0].GrossStrokes.Should().Be(5);
        capturedScores[0].IsMaxScore.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_WhenScheduled_SetsStatusToInProgress()
    {
        var round = MakeScheduledRound();
        round.Status = RoundStatus.Scheduled;
        var participant = MakeParticipant();
        var player = MakePlayer();
        var course = MakeCourse();
        var holes = new List<CourseHole> { new() { HoleNumber = 1, Par = 4, StrokeIndex = 1 } };

        var roundRepo = new Mock<IRoundRepository>();
        roundRepo.Setup(r => r.GetByIdAsync(1, default)).ReturnsAsync(round);
        roundRepo.Setup(r => r.GetParticipantAsync(1, 1, default)).ReturnsAsync(participant);
        roundRepo.Setup(r => r.GetParticipantsAsyncByPlayer(1, default)).ReturnsAsync(new List<RoundParticipant>());
        var courseRepo = new Mock<ICourseRepository>();
        courseRepo.Setup(r => r.GetHolesAsync(1, default)).ReturnsAsync(holes);
        courseRepo.Setup(r => r.GetByIdAsync(1, default)).ReturnsAsync(course);
        var playerRepo = new Mock<IPlayerRepository>();
        playerRepo.Setup(r => r.GetByIdAsync(1, default)).ReturnsAsync(player);
        var handicapRepo = new Mock<IHandicapRepository>();

        var handler = new SubmitHoleScoresCommandHandler(roundRepo.Object, courseRepo.Object, playerRepo.Object, handicapRepo.Object);
        await handler.Handle(new SubmitHoleScoresCommand(1, 1, [new(1, 4)], "admin"), default);

        roundRepo.Verify(r => r.UpdateAsync(It.Is<Round>(r => r.Status == RoundStatus.InProgress), default), Times.Once);
    }

    [Fact]
    public async Task Handle_With9HoleRoundsPair_CombinesDifferentialsForHandicap()
    {
        var round = MakeScheduledRound();
        var participant = MakeParticipant();
        var player = MakePlayer();
        var course = MakeCourse();
        var holes = MakeHoles(); // 9 holes

        // Simulate 2 existing 9-hole rounds — count is even, so combining is triggered
        var prevHoleScores = Enumerable.Range(1, 9).Select(h => new HoleScore { HoleNumber = h, GrossStrokes = 40 }).ToList();
        var prevParticipant1 = new RoundParticipant
        {
            Id = 2, PlayerId = 1,
            Round = new Round { RoundDate = new DateOnly(2026, 4, 1) },
            HoleScores = prevHoleScores
        };
        var prevParticipant2 = new RoundParticipant
        {
            Id = 3, PlayerId = 1,
            Round = new Round { RoundDate = new DateOnly(2026, 4, 8) },
            HoleScores = Enumerable.Range(1, 9).Select(h => new HoleScore { HoleNumber = h, GrossStrokes = 42 }).ToList()
        };

        var roundRepo = new Mock<IRoundRepository>();
        roundRepo.Setup(r => r.GetByIdAsync(1, default)).ReturnsAsync(round);
        roundRepo.Setup(r => r.GetParticipantAsync(1, 1, default)).ReturnsAsync(participant);
        roundRepo.Setup(r => r.GetParticipantsAsyncByPlayer(1, default))
            .ReturnsAsync(new List<RoundParticipant> { prevParticipant1, prevParticipant2 });

        var courseRepo = new Mock<ICourseRepository>();
        courseRepo.Setup(r => r.GetHolesAsync(1, default)).ReturnsAsync(holes);
        courseRepo.Setup(r => r.GetByIdAsync(1, default)).ReturnsAsync(course);
        var playerRepo = new Mock<IPlayerRepository>();
        playerRepo.Setup(r => r.GetByIdAsync(1, default)).ReturnsAsync(player);
        var handicapRepo = new Mock<IHandicapRepository>();

        var handler = new SubmitHoleScoresCommandHandler(roundRepo.Object, courseRepo.Object, playerRepo.Object, handicapRepo.Object);
        var scores = holes.Select(h => new HoleScoreInput(h.HoleNumber, 40)).ToList();

        await handler.Handle(new SubmitHoleScoresCommand(1, 1, scores, "admin"), default);

        // After 2 nine-hole rounds (count is even), combined differential should be saved
        handicapRepo.Verify(r => r.AddDifferentialAsync(1, It.IsAny<double>(), It.IsAny<DateOnly>(), default), Times.Once);
    }
}
