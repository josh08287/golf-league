using FluentAssertions;
using GolfLeague.Application.Rounds.Queries;
using GolfLeague.Domain.Entities;
using GolfLeague.Domain.Enums;
using GolfLeague.Domain.Interfaces;
using Moq;
using Xunit;

namespace GolfLeague.Tests.Application;

/// <summary>
/// Coverage for the tournament-only fields GetTeeTimeGroupScorecardQuery adds
/// to the shared scorecard DTO: par-3 CTP state and per-flight longest-drive
/// state, both scoped to holes/flights actually relevant to the requested
/// tee-time group — and both empty for non-tournament rounds.
/// </summary>
public class GetTeeTimeGroupScorecardTournamentTests
{
    private static RoundParticipant MakeParticipant(int id, int? tournamentFlightId = null)
    {
        var p = new RoundParticipant
        {
            Id = id,
            PlayerId = id,
            RoundId = 1,
            TournamentFlightId = tournamentFlightId,
            Player = new Player { Id = id, FirstName = "P", LastName = id.ToString() },
        };
        return p;
    }

    private static (GetTeeTimeGroupScorecardQueryHandler Sut, Mock<IRoundRepository> Rounds) BuildSut(
        Round round, RoundTeeTime teeTime, List<CourseHole> holes)
    {
        var rounds = new Mock<IRoundRepository>();
        rounds.Setup(r => r.GetByIdAsync(round.Id, It.IsAny<CancellationToken>())).ReturnsAsync(round);
        rounds.Setup(r => r.GetHoleScoresAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<HoleScore>());
        rounds.Setup(r => r.GetTournamentHoleExtrasAsync(round.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<TournamentHoleExtra>());
        rounds.Setup(r => r.GetTournamentFlightsAsync(round.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<TournamentFlight>());
        rounds.Setup(r => r.GetLongestDriveWinnersAsync(round.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<TournamentLongestDriveWinner>());

        var teeTimes = new Mock<ITeeTimeRepository>();
        teeTimes.Setup(t => t.GetByIdAsync(teeTime.Id, It.IsAny<CancellationToken>())).ReturnsAsync(teeTime);

        var courses = new Mock<ICourseRepository>();
        courses.Setup(c => c.GetByIdAsync(round.CourseId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Course { Id = round.CourseId, Name = "Test Course" });
        courses.Setup(c => c.GetHolesAsync(round.CourseId, It.IsAny<CancellationToken>())).ReturnsAsync(holes);

        return (new GetTeeTimeGroupScorecardQueryHandler(rounds.Object, teeTimes.Object, courses.Object), rounds);
    }

    private static List<CourseHole> Make18Holes() =>
        Enumerable.Range(1, 18)
            .Select(n => new CourseHole { HoleNumber = n, Par = n % 6 == 0 ? 3 : 4, StrokeIndex = n })
            .ToList();

    [Fact]
    public async Task NonTournamentRound_ReturnsNoCtpOrLongestDriveState()
    {
        var round = new Round { Id = 1, RoundType = RoundType.NineHole, NineHoleSide = NineHoleSide.Front, CourseId = 1 };
        var teeTime = new RoundTeeTime { Id = 10, RoundId = 1, TeeTimeNumber = 1 };
        teeTime.Participants.Add(MakeParticipant(1));

        var (sut, _) = BuildSut(round, teeTime, Make18Holes());
        var result = await sut.Handle(new GetTeeTimeGroupScorecardQuery(10), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.TournamentCtp.Should().BeEmpty();
        result.Value!.TournamentLongestDrive.Should().BeEmpty();
    }

    [Fact]
    public async Task TournamentRound_ReturnsCtpForEveryPar3()
    {
        var round = new Round { Id = 1, RoundType = RoundType.Tournament, NineHoleSide = NineHoleSide.NotApplicable, CourseId = 1 };
        var teeTime = new RoundTeeTime { Id = 10, RoundId = 1, TeeTimeNumber = 1 };
        teeTime.Participants.Add(MakeParticipant(1));

        var (sut, _) = BuildSut(round, teeTime, Make18Holes());
        var result = await sut.Handle(new GetTeeTimeGroupScorecardQuery(10), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        // Par-3 holes in Make18Holes are multiples of 6: 6, 12, 18
        result.Value!.TournamentCtp.Select(c => c.HoleNumber).Should().BeEquivalentTo([6, 12, 18]);
        result.Value!.TournamentCtp.Should().OnlyContain(c => c.WinnerPlayerId == null);
    }

    [Fact]
    public async Task TournamentRound_ReturnsExistingCtpWinner()
    {
        var round = new Round { Id = 1, RoundType = RoundType.Tournament, NineHoleSide = NineHoleSide.NotApplicable, CourseId = 1 };
        var teeTime = new RoundTeeTime { Id = 10, RoundId = 1, TeeTimeNumber = 1 };
        teeTime.Participants.Add(MakeParticipant(1));

        var (sut, rounds) = BuildSut(round, teeTime, Make18Holes());
        rounds.Setup(r => r.GetTournamentHoleExtrasAsync(round.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<TournamentHoleExtra> { new() { RoundId = 1, HoleNumber = 6, ClosestToPinPlayerId = 1 } });

        var result = await sut.Handle(new GetTeeTimeGroupScorecardQuery(10), CancellationToken.None);

        result.Value!.TournamentCtp.Single(c => c.HoleNumber == 6).WinnerPlayerId.Should().Be(1);
    }

    [Fact]
    public async Task TournamentRound_LongestDrive_OnlyIncludesFlightsPresentInGroup()
    {
        var round = new Round { Id = 1, RoundType = RoundType.Tournament, NineHoleSide = NineHoleSide.NotApplicable, CourseId = 1, LongestDriveHoleNumber = 4 };
        var teeTime = new RoundTeeTime { Id = 10, RoundId = 1, TeeTimeNumber = 1 };
        teeTime.Participants.Add(MakeParticipant(1, tournamentFlightId: 900));

        var (sut, rounds) = BuildSut(round, teeTime, Make18Holes());
        rounds.Setup(r => r.GetTournamentFlightsAsync(round.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<TournamentFlight>
            {
                new() { Id = 900, RoundId = 1, FlightNumber = 1, Name = "A" },
                new() { Id = 901, RoundId = 1, FlightNumber = 2, Name = "B" }, // not represented in this group
            });

        var result = await sut.Handle(new GetTeeTimeGroupScorecardQuery(10), CancellationToken.None);

        result.Value!.TournamentLongestDrive.Should().ContainSingle(f => f.TournamentFlightId == 900);
    }
}
