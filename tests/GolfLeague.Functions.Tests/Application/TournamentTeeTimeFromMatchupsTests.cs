using FluentAssertions;
using GolfLeague.Application.Rounds;
using GolfLeague.Domain.Entities;
using GolfLeague.Domain.Enums;
using GolfLeague.Domain.Interfaces;
using Moq;
using Xunit;

namespace GolfLeague.Tests.Application;

/// <summary>
/// TournamentFoursomeService.RegroupTeeTimesFromMatchupsAsync — the tee-time
/// grouping "regenerate from handicaps" uses: consecutive matchups (1&amp;2,
/// 3&amp;4, ...) always share a tee time so playing partners land together, and
/// bye matchups (a single player, no opponent) pair up with other byes the
/// same way, using the fewest slots.
/// </summary>
public class TournamentTeeTimeFromMatchupsTests
{
    private static Round MakeRound() => new()
    {
        Id = 1,
        SeasonId = 1,
        RoundDate = new DateOnly(2026, 6, 15),
        RoundType = RoundType.Tournament,
        Status = RoundStatus.Scheduled,
    };

    private static RoundParticipant MakeParticipant(int id) => new()
    {
        Id = id,
        PlayerId = id,
        RoundId = 1,
        Player = new Player { Id = id, FirstName = "P", LastName = id.ToString() },
    };

    private static TournamentMatchup Pair(int number, int p1, int p2) =>
        new() { MatchupNumber = number, Player1Id = p1, Player2Id = p2 };

    private static TournamentMatchup Bye(int number, int p1) =>
        new() { MatchupNumber = number, Player1Id = p1, Player2Id = null };

    private static (TournamentFoursomeService Sut, Dictionary<int, int?> TeeTimeAssignments, List<int> SlotsCreated)
        BuildSut(Round round)
    {
        var teeTimes = new Mock<ITeeTimeRepository>();
        var slotsCreated = new List<int>();
        teeTimes.Setup(t => t.EnsureSlotsAsync(round.Id, It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((int _, int count, CancellationToken _) =>
            {
                slotsCreated.Add(count);
                return Enumerable.Range(1, Math.Max(count, 1))
                    .Select(n => new RoundTeeTime { Id = 100 + n, RoundId = round.Id, TeeTimeNumber = n })
                    .ToList();
            });

        var teeTimeAssignments = new Dictionary<int, int?>();
        teeTimes.Setup(t => t.SetParticipantTeeTimeAsync(It.IsAny<int>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .Callback<int, int?, CancellationToken>((pid, tid, _) => teeTimeAssignments[pid] = tid)
            .Returns(Task.CompletedTask);

        var rounds = new Mock<IRoundRepository>();
        rounds.Setup(r => r.GetByIdAsync(round.Id, It.IsAny<CancellationToken>())).ReturnsAsync(round);
        List<TournamentFlight> savedFlights = [];
        rounds.Setup(r => r.ReplaceTournamentFlightsAsync(round.Id, It.IsAny<IEnumerable<TournamentFlight>>(), It.IsAny<CancellationToken>()))
            .Callback<int, IEnumerable<TournamentFlight>, CancellationToken>((_, f, _) =>
            {
                savedFlights = f.Select((flight, i) => { flight.Id = 900 + i; return flight; }).ToList();
            })
            .Returns(Task.CompletedTask);
        rounds.Setup(r => r.GetTournamentFlightsAsync(round.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => savedFlights);
        rounds.Setup(r => r.SetParticipantTournamentFlightAsync(It.IsAny<int>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var flights = new Mock<IFlightRepository>();
        flights.Setup(f => f.GetHalvesBySeasonAsync(round.SeasonId, It.IsAny<CancellationToken>())).ReturnsAsync(new List<SeasonHalf>());

        var sut = new TournamentFoursomeService(teeTimes.Object, rounds.Object, flights.Object);
        return (sut, teeTimeAssignments, slotsCreated);
    }

    [Fact]
    public async Task ConsecutiveMatchups_ShareOneTeeTime()
    {
        var round = MakeRound();
        var (sut, assignments, slots) = BuildSut(round);
        var participants = Enumerable.Range(1, 4).Select(MakeParticipant).ToList();
        var matchups = new List<TournamentMatchup> { Pair(1, 1, 2), Pair(2, 3, 4) };

        await sut.RegroupTeeTimesFromMatchupsAsync(round.Id, participants, matchups, CancellationToken.None);

        slots.Should().ContainSingle().Which.Should().Be(1);
        var teeTimeId = assignments[1];
        assignments[2].Should().Be(teeTimeId);
        assignments[3].Should().Be(teeTimeId);
        assignments[4].Should().Be(teeTimeId);
    }

    [Fact]
    public async Task FourMatchups_ProduceTwoTeeTimes_GroupedInPairs()
    {
        var round = MakeRound();
        var (sut, assignments, slots) = BuildSut(round);
        var participants = Enumerable.Range(1, 8).Select(MakeParticipant).ToList();
        var matchups = new List<TournamentMatchup>
        {
            Pair(1, 1, 2), Pair(2, 3, 4), Pair(3, 5, 6), Pair(4, 7, 8),
        };

        await sut.RegroupTeeTimesFromMatchupsAsync(round.Id, participants, matchups, CancellationToken.None);

        slots.Should().ContainSingle().Which.Should().Be(2);
        var teeTime1 = assignments[1];
        var teeTime2 = assignments[5];
        teeTime1.Should().NotBe(teeTime2);
        assignments[2].Should().Be(teeTime1);
        assignments[3].Should().Be(teeTime1);
        assignments[4].Should().Be(teeTime1);
        assignments[6].Should().Be(teeTime2);
        assignments[7].Should().Be(teeTime2);
        assignments[8].Should().Be(teeTime2);
    }

    [Fact]
    public async Task OddNumberOfFullPairs_LastTeeTimeHoldsJustOnePair()
    {
        var round = MakeRound();
        var (sut, assignments, slots) = BuildSut(round);
        var participants = Enumerable.Range(1, 6).Select(MakeParticipant).ToList();
        var matchups = new List<TournamentMatchup> { Pair(1, 1, 2), Pair(2, 3, 4), Pair(3, 5, 6) };

        await sut.RegroupTeeTimesFromMatchupsAsync(round.Id, participants, matchups, CancellationToken.None);

        slots.Should().ContainSingle().Which.Should().Be(2);
        // Matchups 1 & 2 (players 1,2,3,4) share the first tee time; matchup 3
        // (players 5,6) is alone on the second — the grouping is by matchup
        // pairing (1&2, 3&4, ...), not by player-count halves.
        assignments[1].Should().Be(assignments[2]);
        assignments[2].Should().Be(assignments[3]);
        assignments[3].Should().Be(assignments[4]);
        assignments[5].Should().Be(assignments[6]);
        assignments[1].Should().NotBe(assignments[5]);
    }

    [Fact]
    public async Task TwoByes_ShareOneTeeTime_SeparateFromFullPairs()
    {
        var round = MakeRound();
        var (sut, assignments, slots) = BuildSut(round);
        var participants = Enumerable.Range(1, 6).Select(MakeParticipant).ToList();
        var matchups = new List<TournamentMatchup> { Pair(1, 1, 2), Bye(2, 3), Bye(3, 4) };

        await sut.RegroupTeeTimesFromMatchupsAsync(round.Id, participants, matchups, CancellationToken.None);

        // 1 tee time for the full pair + 1 tee time for the two byes = 2, not 3.
        slots.Should().ContainSingle().Which.Should().Be(2);
        assignments[3].Should().Be(assignments[4]);
        assignments[1].Should().NotBe(assignments[3]);
    }

    [Fact]
    public async Task SingleTrailingBye_GetsItsOwnTeeTime()
    {
        var round = MakeRound();
        var (sut, assignments, slots) = BuildSut(round);
        var participants = Enumerable.Range(1, 5).Select(MakeParticipant).ToList();
        var matchups = new List<TournamentMatchup> { Pair(1, 1, 2), Pair(2, 3, 4), Bye(3, 5) };

        await sut.RegroupTeeTimesFromMatchupsAsync(round.Id, participants, matchups, CancellationToken.None);

        slots.Should().ContainSingle().Which.Should().Be(2);
        assignments[5].Should().NotBe(assignments[1]);
        assignments[5].Should().NotBeNull();
    }

    [Fact]
    public async Task RegularByeAndSubByeTogether_MinimizeTeeTimes()
    {
        // One regular bye (from the odd regulars group) and one sub bye
        // (lone substitute) — should still share a tee time since both are
        // byes, regardless of which pairing group they originated from.
        var round = MakeRound();
        var (sut, assignments, slots) = BuildSut(round);
        var participants = Enumerable.Range(1, 5).Select(MakeParticipant).ToList();
        var matchups = new List<TournamentMatchup> { Pair(1, 1, 2), Bye(2, 3), Bye(3, 4) };

        await sut.RegroupTeeTimesFromMatchupsAsync(round.Id, participants, matchups, CancellationToken.None);

        slots.Should().ContainSingle().Which.Should().Be(2);
        assignments[3].Should().Be(assignments[4]);
    }
}
