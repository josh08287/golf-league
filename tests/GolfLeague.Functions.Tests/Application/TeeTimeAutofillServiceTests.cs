using FluentAssertions;
using GolfLeague.Application.Rounds;
using GolfLeague.Domain.Entities;
using GolfLeague.Domain.Enums;
using GolfLeague.Domain.Interfaces;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace GolfLeague.Tests.Application;

/// <summary>
/// Regression coverage for the autofill capacity bug: assignments during a
/// run go straight to the DB while the in-memory slot.Participants lists
/// stay stale, so without per-run occupancy tracking a slot filled by the
/// preference pass got refilled by the no-preference pass (8+ players in
/// one tee time).
/// </summary>
public class TeeTimeAutofillServiceTests
{
    private static RoundParticipant MakeParticipant(int id, TeeTimeSlotPreference preference) => new()
    {
        Id = id,
        PlayerId = id,
        Player = new Player
        {
            Id = id,
            FirstName = "P",
            LastName = id.ToString(),
            PreferredTeeTimeSlots = preference,
        },
    };

    private static (TeeTimeAutofillService Sut, Dictionary<int, int> Assignments) BuildSut(
        Round round, List<RoundTeeTime> slots)
    {
        var teeTimes = new Mock<ITeeTimeRepository>();
        var rounds = new Mock<IRoundRepository>();
        var flights = new Mock<IFlightRepository>();

        rounds.Setup(r => r.GetByIdAsync(round.Id, It.IsAny<CancellationToken>())).ReturnsAsync(round);
        teeTimes.Setup(t => t.EnsureSlotsAsync(round.Id, It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(slots);
        // Same stale instances as production: Participants lists never
        // reflect assignments made during the run.
        teeTimes.Setup(t => t.GetByRoundAsync(round.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(slots);

        var assignments = new Dictionary<int, int>(); // participantId -> teeTimeId
        teeTimes.Setup(t => t.SetParticipantTeeTimeAsync(It.IsAny<int>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .Callback<int, int?, CancellationToken>((pid, slotId, _) => assignments[pid] = slotId!.Value)
            .Returns(Task.CompletedTask);

        var sut = new TeeTimeAutofillService(
            teeTimes.Object, rounds.Object, flights.Object,
            new Mock<ILogger<TeeTimeAutofillService>>().Object);
        return (sut, assignments);
    }

    [Fact]
    public async Task RunAsync_PreferenceAndNoPreferencePlayers_NeverExceedsSlotCapacity()
    {
        // 4 players prefer Early (all land in slot 1 via the preference pass),
        // 4 have no preference. Before the fix, the no-preference pass saw
        // slot 1's stale empty Participants list and stuffed all 4 in there
        // too — 8 players in one tee time.
        var participants = Enumerable.Range(1, 4).Select(i => MakeParticipant(i, TeeTimeSlotPreference.Early))
            .Concat(Enumerable.Range(5, 4).Select(i => MakeParticipant(i, TeeTimeSlotPreference.None)))
            .ToList();
        var round = new Round { Id = 1, Participants = participants };
        var slots = new List<RoundTeeTime>
        {
            new() { Id = 101, RoundId = 1, TeeTimeNumber = 1, Participants = [] },
            new() { Id = 102, RoundId = 1, TeeTimeNumber = 2, Participants = [] },
        };
        var (sut, assignments) = BuildSut(round, slots);

        var result = await sut.RunAsync(1);

        result.IsSuccess.Should().BeTrue();
        assignments.Should().HaveCount(8, "every participant should get a tee time");
        assignments.Values.GroupBy(slotId => slotId)
            .Should().OnlyContain(g => g.Count() <= 4, "no tee time may hold more than 4 players");
    }

    [Fact]
    public async Task RunAsync_PartialPreferenceFill_TopsOffSlotInsteadOfStrandingPlayers()
    {
        // Only 2 players prefer Early, 4 have none; 6 players need 2 slots.
        // The no-preference pass must top slot 1 off to 4 (not skip it and
        // strand players, and not overfill it).
        var participants = Enumerable.Range(1, 2).Select(i => MakeParticipant(i, TeeTimeSlotPreference.Early))
            .Concat(Enumerable.Range(3, 4).Select(i => MakeParticipant(i, TeeTimeSlotPreference.None)))
            .ToList();
        var round = new Round { Id = 1, Participants = participants };
        var slots = new List<RoundTeeTime>
        {
            new() { Id = 101, RoundId = 1, TeeTimeNumber = 1, Participants = [] },
            new() { Id = 102, RoundId = 1, TeeTimeNumber = 2, Participants = [] },
        };
        var (sut, assignments) = BuildSut(round, slots);

        var result = await sut.RunAsync(1);

        result.IsSuccess.Should().BeTrue();
        assignments.Should().HaveCount(6, "every participant should get a tee time");
        var bySlot = assignments.Values.GroupBy(slotId => slotId).ToDictionary(g => g.Key, g => g.Count());
        bySlot.Values.Should().OnlyContain(c => c <= 4, "no tee time may hold more than 4 players");
        bySlot[101].Should().Be(4, "slot 1 should be topped off to capacity");
        bySlot[102].Should().Be(2);
    }

    [Fact]
    public async Task RunAsync_PreferenceActsAsWeight_BandSeatsGoToPlayersWhoPreferThem()
    {
        // 4 Early-preferrers and 4 no-preference players, 2 slots. Slot 1 is
        // the Early band — the weight should seat the Early-preferrers there
        // ahead of the no-preference players.
        var participants = Enumerable.Range(1, 4).Select(i => MakeParticipant(i, TeeTimeSlotPreference.Early))
            .Concat(Enumerable.Range(5, 4).Select(i => MakeParticipant(i, TeeTimeSlotPreference.None)))
            .ToList();
        var round = new Round { Id = 1, Participants = participants };
        var slots = new List<RoundTeeTime>
        {
            new() { Id = 101, RoundId = 1, TeeTimeNumber = 1, Participants = [] },
            new() { Id = 102, RoundId = 1, TeeTimeNumber = 2, Participants = [] },
        };
        var (sut, assignments) = BuildSut(round, slots);

        await sut.RunAsync(1);

        Enumerable.Range(1, 4).Select(id => assignments[id])
            .Should().OnlyContain(slotId => slotId == 101, "Early-preferrers should win the Early-band seats");
        Enumerable.Range(5, 4).Select(id => assignments[id])
            .Should().OnlyContain(slotId => slotId == 102);
    }

    [Fact]
    public async Task RunAsync_PreferenceIsNotAGuarantee_PlayersArePlacedOutsideBandWhenSeatsDemandIt()
    {
        // Everyone prefers Late, but with 2 slots there is no Late band at
        // all (bands are Early/Middle). Preferences must not block placement:
        // all 8 players still get seats, 4 per slot.
        var participants = Enumerable.Range(1, 8)
            .Select(i => MakeParticipant(i, TeeTimeSlotPreference.Late))
            .ToList();
        var round = new Round { Id = 1, Participants = participants };
        var slots = new List<RoundTeeTime>
        {
            new() { Id = 101, RoundId = 1, TeeTimeNumber = 1, Participants = [] },
            new() { Id = 102, RoundId = 1, TeeTimeNumber = 2, Participants = [] },
        };
        var (sut, assignments) = BuildSut(round, slots);

        var result = await sut.RunAsync(1);

        result.IsSuccess.Should().BeTrue();
        assignments.Should().HaveCount(8, "a preference never bars a player from a seat");
        assignments.Values.GroupBy(s => s).Should().OnlyContain(g => g.Count() == 4);
    }

    [Fact]
    public async Task RunAsync_PartiallyOccupiedSlotFromEarlierRun_IsToppedOffNotOverfilled()
    {
        // Slot 1 already holds 3 players from a previous run/sign-ups; 5 more
        // players need seats. Phase 1 may add exactly one to slot 1; the rest
        // go to slot 2.
        var existing = Enumerable.Range(101, 3)
            .Select(i => MakeParticipant(i, TeeTimeSlotPreference.None))
            .ToList();
        foreach (var p in existing) p.TeeTimeId = 201;

        var newcomers = Enumerable.Range(1, 5)
            .Select(i => MakeParticipant(i, TeeTimeSlotPreference.None))
            .ToList();
        var round = new Round { Id = 1, Participants = existing.Concat(newcomers).ToList() };
        var slots = new List<RoundTeeTime>
        {
            new() { Id = 201, RoundId = 1, TeeTimeNumber = 1, Participants = existing },
            new() { Id = 202, RoundId = 1, TeeTimeNumber = 2, Participants = [] },
        };
        var (sut, assignments) = BuildSut(round, slots);

        var result = await sut.RunAsync(1);

        result.IsSuccess.Should().BeTrue();
        assignments.Should().HaveCount(5);
        var addedToSlot1 = assignments.Values.Count(s => s == 201);
        addedToSlot1.Should().Be(1, "slot 1 already had 3 occupants so only 1 seat was free");
        assignments.Values.Count(s => s == 202).Should().Be(4);
    }
}
