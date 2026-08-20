using FluentAssertions;
using GolfLeague.Application.Rounds.Commands;
using GolfLeague.Domain.Entities;
using GolfLeague.Domain.Enums;
using GolfLeague.Domain.Interfaces;
using Moq;
using Xunit;

namespace GolfLeague.Tests.Application;

/// <summary>
/// Coverage for the live, save-immediately CTP/longest-drive entry a player
/// uses from their own tee-time group during a tournament round: CTP is
/// scoped to par-3 holes and the entering group; longest drive is scoped to
/// the round's configured hole and the winner's tournament flight.
/// </summary>
public class TournamentCtpAndLongestDriveTests
{
    private static Round MakeRound(RoundStatus status = RoundStatus.Scheduled, int? ldHole = null) => new()
    {
        Id = 1,
        RoundType = RoundType.Tournament,
        Status = status,
        CourseId = 1,
        LongestDriveHoleNumber = ldHole,
    };

    private static RoundParticipant MakeParticipant(int id, int? flightId = null) => new()
    {
        Id = id,
        PlayerId = id,
        RoundId = 1,
        TournamentFlightId = flightId,
        Player = new Player { Id = id, FirstName = "P", LastName = id.ToString() },
    };

    private static RoundTeeTime MakeTeeTime(int id, params RoundParticipant[] participants)
    {
        var slot = new RoundTeeTime { Id = id, RoundId = 1, TeeTimeNumber = 1 };
        foreach (var p in participants) slot.Participants.Add(p);
        return slot;
    }

    // ── CTP ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task Ctp_RejectsNonMemberSubmitter()
    {
        var p1 = MakeParticipant(1);
        var teeTime = MakeTeeTime(10, p1);
        var rounds = new Mock<IRoundRepository>();
        var teeTimes = new Mock<ITeeTimeRepository>();
        teeTimes.Setup(t => t.GetByIdAsync(10, It.IsAny<CancellationToken>())).ReturnsAsync(teeTime);
        var courses = new Mock<ICourseRepository>();

        var handler = new SetTeeTimeTournamentCtpCommandHandler(rounds.Object, teeTimes.Object, courses.Object);
        var result = await handler.Handle(new SetTeeTimeTournamentCtpCommand(10, SubmittedByPlayerId: 99, HoleNumber: 3, WinnerPlayerId: 1, "user1"), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task Ctp_RejectsNonPar3Hole()
    {
        var p1 = MakeParticipant(1);
        var teeTime = MakeTeeTime(10, p1);
        var round = MakeRound();
        var rounds = new Mock<IRoundRepository>();
        rounds.Setup(r => r.GetByIdAsync(round.Id, It.IsAny<CancellationToken>())).ReturnsAsync(round);
        var teeTimes = new Mock<ITeeTimeRepository>();
        teeTimes.Setup(t => t.GetByIdAsync(10, It.IsAny<CancellationToken>())).ReturnsAsync(teeTime);
        var courses = new Mock<ICourseRepository>();
        courses.Setup(c => c.GetHolesAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<CourseHole> { new() { HoleNumber = 3, Par = 4, StrokeIndex = 1 } });

        var handler = new SetTeeTimeTournamentCtpCommandHandler(rounds.Object, teeTimes.Object, courses.Object);
        var result = await handler.Handle(new SetTeeTimeTournamentCtpCommand(10, SubmittedByPlayerId: 1, HoleNumber: 3, WinnerPlayerId: 1, "user1"), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("par 3");
    }

    [Fact]
    public async Task Ctp_RejectsWinnerOutsideGroup()
    {
        var p1 = MakeParticipant(1);
        var teeTime = MakeTeeTime(10, p1);
        var round = MakeRound();
        var rounds = new Mock<IRoundRepository>();
        rounds.Setup(r => r.GetByIdAsync(round.Id, It.IsAny<CancellationToken>())).ReturnsAsync(round);
        var teeTimes = new Mock<ITeeTimeRepository>();
        teeTimes.Setup(t => t.GetByIdAsync(10, It.IsAny<CancellationToken>())).ReturnsAsync(teeTime);
        var courses = new Mock<ICourseRepository>();

        var handler = new SetTeeTimeTournamentCtpCommandHandler(rounds.Object, teeTimes.Object, courses.Object);
        var result = await handler.Handle(new SetTeeTimeTournamentCtpCommand(10, SubmittedByPlayerId: 1, HoleNumber: 3, WinnerPlayerId: 42, "user1"), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("member of this tee time");
    }

    [Fact]
    public async Task Ctp_SavesImmediately_ForValidPar3AndGroupMember()
    {
        var p1 = MakeParticipant(1);
        var p2 = MakeParticipant(2);
        var teeTime = MakeTeeTime(10, p1, p2);
        var round = MakeRound();
        var rounds = new Mock<IRoundRepository>();
        rounds.Setup(r => r.GetByIdAsync(round.Id, It.IsAny<CancellationToken>())).ReturnsAsync(round);
        rounds.Setup(r => r.UpsertTournamentHoleExtrasAsync(It.IsAny<IEnumerable<TournamentHoleExtra>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var teeTimes = new Mock<ITeeTimeRepository>();
        teeTimes.Setup(t => t.GetByIdAsync(10, It.IsAny<CancellationToken>())).ReturnsAsync(teeTime);
        var courses = new Mock<ICourseRepository>();
        courses.Setup(c => c.GetHolesAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<CourseHole> { new() { HoleNumber = 3, Par = 3, StrokeIndex = 1 } });

        var handler = new SetTeeTimeTournamentCtpCommandHandler(rounds.Object, teeTimes.Object, courses.Object);
        var result = await handler.Handle(new SetTeeTimeTournamentCtpCommand(10, SubmittedByPlayerId: 1, HoleNumber: 3, WinnerPlayerId: 2, "user1"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.WinnerPlayerId.Should().Be(2);
        rounds.Verify(r => r.UpsertTournamentHoleExtrasAsync(
            It.Is<IEnumerable<TournamentHoleExtra>>(e => e.Single().HoleNumber == 3 && e.Single().ClosestToPinPlayerId == 2),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── Longest Drive ────────────────────────────────────────────────────

    [Fact]
    public async Task LongestDrive_RejectsWhenHoleNotConfigured()
    {
        var p1 = MakeParticipant(1, flightId: 900);
        var teeTime = MakeTeeTime(10, p1);
        var round = MakeRound(ldHole: null);
        var rounds = new Mock<IRoundRepository>();
        rounds.Setup(r => r.GetByIdAsync(round.Id, It.IsAny<CancellationToken>())).ReturnsAsync(round);
        var teeTimes = new Mock<ITeeTimeRepository>();
        teeTimes.Setup(t => t.GetByIdAsync(10, It.IsAny<CancellationToken>())).ReturnsAsync(teeTime);

        var handler = new SetTeeTimeTournamentLongestDriveCommandHandler(rounds.Object, teeTimes.Object);
        var result = await handler.Handle(new SetTeeTimeTournamentLongestDriveCommand(10, 1, 900, 1, "user1"), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("hole configured");
    }

    [Fact]
    public async Task LongestDrive_RejectsSubmitterNotInFlight()
    {
        var p1 = MakeParticipant(1, flightId: 800); // different flight than the one targeted
        var teeTime = MakeTeeTime(10, p1);
        var round = MakeRound(ldHole: 7);
        var flight = new TournamentFlight { Id = 900, RoundId = 1, FlightNumber = 1, Name = "A" };
        var rounds = new Mock<IRoundRepository>();
        rounds.Setup(r => r.GetByIdAsync(round.Id, It.IsAny<CancellationToken>())).ReturnsAsync(round);
        rounds.Setup(r => r.GetTournamentFlightsAsync(round.Id, It.IsAny<CancellationToken>())).ReturnsAsync(new List<TournamentFlight> { flight });
        rounds.Setup(r => r.GetParticipantsAsync(round.Id, It.IsAny<CancellationToken>())).ReturnsAsync(new List<RoundParticipant> { p1 });
        var teeTimes = new Mock<ITeeTimeRepository>();
        teeTimes.Setup(t => t.GetByIdAsync(10, It.IsAny<CancellationToken>())).ReturnsAsync(teeTime);

        var handler = new SetTeeTimeTournamentLongestDriveCommandHandler(rounds.Object, teeTimes.Object);
        var result = await handler.Handle(new SetTeeTimeTournamentLongestDriveCommand(10, 1, 900, 1, "user1"), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("not a member of that tournament flight");
    }

    [Fact]
    public async Task LongestDrive_RejectsWinnerInDifferentFlightThanTarget()
    {
        var p1 = MakeParticipant(1, flightId: 900);
        var p2 = MakeParticipant(2, flightId: 800); // different flight
        var teeTime = MakeTeeTime(10, p1, p2);
        var round = MakeRound(ldHole: 7);
        var flight = new TournamentFlight { Id = 900, RoundId = 1, FlightNumber = 1, Name = "A" };
        var rounds = new Mock<IRoundRepository>();
        rounds.Setup(r => r.GetByIdAsync(round.Id, It.IsAny<CancellationToken>())).ReturnsAsync(round);
        rounds.Setup(r => r.GetTournamentFlightsAsync(round.Id, It.IsAny<CancellationToken>())).ReturnsAsync(new List<TournamentFlight> { flight });
        rounds.Setup(r => r.GetParticipantsAsync(round.Id, It.IsAny<CancellationToken>())).ReturnsAsync(new List<RoundParticipant> { p1, p2 });
        var teeTimes = new Mock<ITeeTimeRepository>();
        teeTimes.Setup(t => t.GetByIdAsync(10, It.IsAny<CancellationToken>())).ReturnsAsync(teeTime);

        var handler = new SetTeeTimeTournamentLongestDriveCommandHandler(rounds.Object, teeTimes.Object);
        var result = await handler.Handle(new SetTeeTimeTournamentLongestDriveCommand(10, 1, 900, 2, "user1"), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("same flight");
    }

    [Fact]
    public async Task LongestDrive_SavesImmediately_ForValidFlightMember()
    {
        var p1 = MakeParticipant(1, flightId: 900);
        var p2 = MakeParticipant(2, flightId: 900);
        var teeTime = MakeTeeTime(10, p1, p2);
        var round = MakeRound(ldHole: 7);
        var flight = new TournamentFlight { Id = 900, RoundId = 1, FlightNumber = 1, Name = "A" };
        var rounds = new Mock<IRoundRepository>();
        rounds.Setup(r => r.GetByIdAsync(round.Id, It.IsAny<CancellationToken>())).ReturnsAsync(round);
        rounds.Setup(r => r.GetTournamentFlightsAsync(round.Id, It.IsAny<CancellationToken>())).ReturnsAsync(new List<TournamentFlight> { flight });
        rounds.Setup(r => r.GetParticipantsAsync(round.Id, It.IsAny<CancellationToken>())).ReturnsAsync(new List<RoundParticipant> { p1, p2 });
        rounds.Setup(r => r.SetLongestDriveWinnerAsync(round.Id, 900, 2, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        var teeTimes = new Mock<ITeeTimeRepository>();
        teeTimes.Setup(t => t.GetByIdAsync(10, It.IsAny<CancellationToken>())).ReturnsAsync(teeTime);

        var handler = new SetTeeTimeTournamentLongestDriveCommandHandler(rounds.Object, teeTimes.Object);
        var result = await handler.Handle(new SetTeeTimeTournamentLongestDriveCommand(10, 1, 900, 2, "user1"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.WinnerPlayerId.Should().Be(2);
        rounds.Verify(r => r.SetLongestDriveWinnerAsync(round.Id, 900, 2, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task LongestDrive_Clears_WhenWinnerIsNull()
    {
        var p1 = MakeParticipant(1, flightId: 900);
        var teeTime = MakeTeeTime(10, p1);
        var round = MakeRound(ldHole: 7);
        var flight = new TournamentFlight { Id = 900, RoundId = 1, FlightNumber = 1, Name = "A" };
        var rounds = new Mock<IRoundRepository>();
        rounds.Setup(r => r.GetByIdAsync(round.Id, It.IsAny<CancellationToken>())).ReturnsAsync(round);
        rounds.Setup(r => r.GetTournamentFlightsAsync(round.Id, It.IsAny<CancellationToken>())).ReturnsAsync(new List<TournamentFlight> { flight });
        rounds.Setup(r => r.GetParticipantsAsync(round.Id, It.IsAny<CancellationToken>())).ReturnsAsync(new List<RoundParticipant> { p1 });
        rounds.Setup(r => r.SetLongestDriveWinnerAsync(round.Id, 900, null, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        var teeTimes = new Mock<ITeeTimeRepository>();
        teeTimes.Setup(t => t.GetByIdAsync(10, It.IsAny<CancellationToken>())).ReturnsAsync(teeTime);

        var handler = new SetTeeTimeTournamentLongestDriveCommandHandler(rounds.Object, teeTimes.Object);
        var result = await handler.Handle(new SetTeeTimeTournamentLongestDriveCommand(10, 1, 900, null, "user1"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.WinnerPlayerId.Should().BeNull();
    }
}
