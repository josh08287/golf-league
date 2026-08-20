using FluentAssertions;
using GolfLeague.Application.Rounds;
using GolfLeague.Domain.Entities;
using GolfLeague.Domain.Enums;
using GolfLeague.Domain.Interfaces;
using Moq;
using Xunit;

namespace GolfLeague.Tests.Application;

/// <summary>
/// Coverage for TournamentFoursomeService's flight-count derivation: a
/// tournament round has no HalfId of its own, so the flight count it uses
/// comes from whichever season half's date range contains the round date,
/// falling back to the most recently started half before it, and finally
/// to a single flight when the season has no halves yet.
/// </summary>
public class TournamentFlightRegroupTests
{
    private static Round MakeRound(int seasonId, DateOnly roundDate) => new()
    {
        Id = 1,
        SeasonId = seasonId,
        RoundDate = roundDate,
        RoundType = RoundType.Tournament,
        Status = RoundStatus.Scheduled,
    };

    private static RoundParticipant MakeParticipant(int id, double handicapIndex) => new()
    {
        Id = id,
        PlayerId = id,
        RoundId = 1,
        HandicapIndex = handicapIndex,
        Player = new Player { Id = id, FirstName = "P", LastName = id.ToString() },
    };

    private static (TournamentFoursomeService Sut, Mock<IRoundRepository> Rounds, List<TournamentFlight> SavedFlights, Dictionary<int, int?> FlightAssignments)
        BuildSut(Round round, IReadOnlyList<SeasonHalf> halves, Func<int, int> flightCountByHalfId)
    {
        var teeTimes = new Mock<ITeeTimeRepository>();
        teeTimes.Setup(t => t.EnsureSlotsAsync(round.Id, It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((int _, int count, CancellationToken _) =>
                Enumerable.Range(1, Math.Max(count, 1)).Select(n => new RoundTeeTime { Id = 100 + n, RoundId = round.Id, TeeTimeNumber = n }).ToList());
        teeTimes.Setup(t => t.SetParticipantTeeTimeAsync(It.IsAny<int>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var rounds = new Mock<IRoundRepository>();
        rounds.Setup(r => r.GetByIdAsync(round.Id, It.IsAny<CancellationToken>())).ReturnsAsync(round);

        List<TournamentFlight> savedFlights = [];
        rounds.Setup(r => r.ReplaceTournamentFlightsAsync(round.Id, It.IsAny<IEnumerable<TournamentFlight>>(), It.IsAny<CancellationToken>()))
            .Callback<int, IEnumerable<TournamentFlight>, CancellationToken>((_, flights, _) =>
            {
                savedFlights = flights.Select((f, i) => { f.Id = 900 + i; return f; }).ToList();
            })
            .Returns(Task.CompletedTask);
        rounds.Setup(r => r.GetTournamentFlightsAsync(round.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => savedFlights);

        var flightAssignments = new Dictionary<int, int?>();
        rounds.Setup(r => r.SetParticipantTournamentFlightAsync(It.IsAny<int>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .Callback<int, int?, CancellationToken>((pid, fid, _) => flightAssignments[pid] = fid)
            .Returns(Task.CompletedTask);

        var flights = new Mock<IFlightRepository>();
        flights.Setup(f => f.GetHalvesBySeasonAsync(round.SeasonId, It.IsAny<CancellationToken>())).ReturnsAsync(halves);
        foreach (var half in halves)
        {
            var count = flightCountByHalfId(half.Id);
            flights.Setup(f => f.GetByHalfAsync(half.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(Enumerable.Range(1, count).Select(n => new Flight { Id = n, HalfId = half.Id, Name = n.ToString() }).ToList());
        }

        var sut = new TournamentFoursomeService(teeTimes.Object, rounds.Object, flights.Object);
        return (sut, rounds, savedFlights, flightAssignments);
    }

    [Fact]
    public async Task NoHalves_FallsBackToOneFlight()
    {
        var round = MakeRound(seasonId: 1, roundDate: new DateOnly(2026, 7, 1));
        var (sut, rounds, _, _) = BuildSut(round, halves: [], flightCountByHalfId: _ => 0);

        await sut.RegroupAsync(round.Id, [MakeParticipant(1, 10.0), MakeParticipant(2, 5.0)], CancellationToken.None);

        rounds.Verify(r => r.ReplaceTournamentFlightsAsync(round.Id,
            It.Is<IEnumerable<TournamentFlight>>(f => f.Count() == 1), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DateWithinHalf_UsesThatHalfsFlightCount()
    {
        var roundDate = new DateOnly(2026, 6, 15);
        var round = MakeRound(seasonId: 1, roundDate: roundDate);
        var half1 = new SeasonHalf { Id = 10, SeasonId = 1, HalfNumber = 1, StartDate = new DateOnly(2026, 4, 1), EndDate = new DateOnly(2026, 5, 31) };
        var half2 = new SeasonHalf { Id = 20, SeasonId = 1, HalfNumber = 2, StartDate = new DateOnly(2026, 6, 1), EndDate = new DateOnly(2026, 7, 31) };

        var (sut, rounds, _, _) = BuildSut(round, [half1, half2], hid => hid == 10 ? 2 : 4);

        var players = Enumerable.Range(1, 8).Select(i => MakeParticipant(i, i)).ToList();
        await sut.RegroupAsync(round.Id, players, CancellationToken.None);

        // roundDate falls in half2's range -> half2's flight count (4) is used
        rounds.Verify(r => r.ReplaceTournamentFlightsAsync(round.Id,
            It.Is<IEnumerable<TournamentFlight>>(f => f.Count() == 4), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DateOutsideAnyHalf_FallsBackToMostRecentPriorHalf()
    {
        // Off-season tournament: no half contains this date.
        var roundDate = new DateOnly(2026, 8, 15);
        var round = MakeRound(seasonId: 1, roundDate: roundDate);
        var half1 = new SeasonHalf { Id = 10, SeasonId = 1, HalfNumber = 1, StartDate = new DateOnly(2026, 4, 1), EndDate = new DateOnly(2026, 5, 31) };
        var half2 = new SeasonHalf { Id = 20, SeasonId = 1, HalfNumber = 2, StartDate = new DateOnly(2026, 6, 1), EndDate = new DateOnly(2026, 7, 31) };

        var (sut, rounds, _, _) = BuildSut(round, [half1, half2], hid => hid == 10 ? 2 : 3);

        var players = Enumerable.Range(1, 6).Select(i => MakeParticipant(i, i)).ToList();
        await sut.RegroupAsync(round.Id, players, CancellationToken.None);

        // Most recently started half before Aug 15 is half2 (started Jun 1) -> 3 flights
        rounds.Verify(r => r.ReplaceTournamentFlightsAsync(round.Id,
            It.Is<IEnumerable<TournamentFlight>>(f => f.Count() == 3), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Participants_AreSplitAcrossFlightsByAscendingHandicap()
    {
        var round = MakeRound(seasonId: 1, roundDate: new DateOnly(2026, 6, 15));
        var half = new SeasonHalf { Id = 10, SeasonId = 1, HalfNumber = 1, StartDate = new DateOnly(2026, 6, 1), EndDate = new DateOnly(2026, 7, 31) };
        var (sut, _, _, assignments) = BuildSut(round, [half], _ => 2);

        var players = new List<RoundParticipant>
        {
            MakeParticipant(1, 20.0),
            MakeParticipant(2, 5.0),
            MakeParticipant(3, 15.0),
            MakeParticipant(4, 10.0),
        };
        await sut.RegroupAsync(round.Id, players, CancellationToken.None);

        // Ascending: 2(5.0), 4(10.0) -> flight 0 (id 900); 3(15.0), 1(20.0) -> flight 1 (id 901)
        assignments[2].Should().Be(900);
        assignments[4].Should().Be(900);
        assignments[3].Should().Be(901);
        assignments[1].Should().Be(901);
    }
}
