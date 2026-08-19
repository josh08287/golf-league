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

    private static RoundParticipant MakeParticipant(int id, int? flightId) => new()
    {
        Id = id,
        PlayerId = id,
        FlightId = flightId,
        Player = new Player
        {
            Id = id,
            FirstName = "P",
            LastName = id.ToString(),
            PreferredTeeTimeSlots = TeeTimeSlotPreference.None,
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
    public async Task RunAsync_RejectsTournamentRound()
    {
        var round = new Round { Id = 1, RoundType = RoundType.Tournament };
        round.Participants.Add(MakeParticipant(1, flightId: (int?)null));
        var (sut, assignments) = BuildSut(round, []);

        var result = await sut.RunAsync(1);

        result.IsSuccess.Should().BeFalse();
        assignments.Should().BeEmpty();
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
        // A naive top-off would produce 4+2, but the no-twosome rebalance
        // must turn that into two threesomes (3+3) instead.
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
        bySlot.Values.Should().NotContain(2, "a leftover twosome should be rebalanced into two threesomes");
        bySlot[101].Should().Be(3);
        bySlot[102].Should().Be(3);
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

    [Fact]
    public async Task RunAsync_ToppingOffPartialSlot_PrefersMatchingBandOverMismatched()
    {
        // 3 slots (proportional bands: 1=Early, 2=Middle, 3=Late). Slot 1
        // (Early band) already has 1 occupant from a manual sign-up, leaving
        // 3 open seats. One unassigned player prefers Early, two prefer Late,
        // and there are enough no-preference players that the total (8) packs
        // evenly into two full foursomes with no trailing-twosome rebalance
        // to muddy the result. Topping off slot 1 must seat the Early-preferrer
        // there, not a Late-preferrer, even though the Late-preferrers are
        // queued first and everyone shares the same (no) flight.
        var manual = MakeParticipant(1, TeeTimeSlotPreference.None);
        manual.TeeTimeId = 101;

        var latePreferrers = Enumerable.Range(2, 2)
            .Select(i => MakeParticipant(i, TeeTimeSlotPreference.Late))
            .ToList();
        var earlyPreferrer = MakeParticipant(4, TeeTimeSlotPreference.Early);
        var noPreference = Enumerable.Range(5, 4)
            .Select(i => MakeParticipant(i, TeeTimeSlotPreference.None))
            .ToList();

        var round = new Round
        {
            Id = 1,
            Participants = new List<RoundParticipant> { manual, earlyPreferrer }
                .Concat(latePreferrers)
                .Concat(noPreference)
                .ToList(),
        };
        var slots = new List<RoundTeeTime>
        {
            new() { Id = 101, RoundId = 1, TeeTimeNumber = 1, Participants = [manual] },
            new() { Id = 102, RoundId = 1, TeeTimeNumber = 2, Participants = [] },
            new() { Id = 103, RoundId = 1, TeeTimeNumber = 3, Participants = [] },
        };
        var (sut, assignments) = BuildSut(round, slots);

        var result = await sut.RunAsync(1);

        result.IsSuccess.Should().BeTrue();
        assignments[4].Should().Be(101, "the Early-preferrer should win an open seat in the Early-band slot being topped off");
        assignments[2].Should().NotBe(101, "a Late-preferrer should not be stuffed into the Early-band slot when an Early-preferrer is available");
        assignments[3].Should().NotBe(101, "a Late-preferrer should not be stuffed into the Early-band slot when an Early-preferrer is available");
    }

    [Fact]
    public async Task RunAsync_FragmentedFlights_StillPackIntoFullFoursomes()
    {
        // Two threesomes and a twosome (flights of 3, 3, 2 = 8 players) must
        // pack into two full foursomes, not get stranded as odd-sized groups
        // because a rigid 2+2-per-flight split doesn't divide evenly.
        var participants = Enumerable.Range(1, 3).Select(i => MakeParticipant(i, flightId: 1))
            .Concat(Enumerable.Range(4, 3).Select(i => MakeParticipant(i, flightId: 2)))
            .Concat(Enumerable.Range(7, 2).Select(i => MakeParticipant(i, flightId: 3)))
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
            .Should().OnlyContain(g => g.Count() == 4, "flight fragments must still pack into full foursomes");
    }

    [Fact]
    public async Task RunAsync_ManuallySelectedPlayers_AreNeverReassigned()
    {
        // Player 1 manually joined slot 2 (the later of the two). Autofill
        // must leave that seat alone even though slot-ordering/flight/band
        // rules would otherwise place player 1 elsewhere.
        var manual = MakeParticipant(1, TeeTimeSlotPreference.None);
        manual.TeeTimeId = 102;
        var others = Enumerable.Range(2, 7)
            .Select(i => MakeParticipant(i, TeeTimeSlotPreference.None))
            .ToList();
        var round = new Round { Id = 1, Participants = new List<RoundParticipant> { manual }.Concat(others).ToList() };
        var slots = new List<RoundTeeTime>
        {
            new() { Id = 101, RoundId = 1, TeeTimeNumber = 1, Participants = [] },
            new() { Id = 102, RoundId = 1, TeeTimeNumber = 2, Participants = [manual] },
        };
        var (sut, assignments) = BuildSut(round, slots);

        var result = await sut.RunAsync(1);

        result.IsSuccess.Should().BeTrue();
        assignments.Should().NotContainKey(1, "a manually-selected player must never be reassigned by autofill");
    }

    [Fact]
    public async Task RunAsync_TrailingTwosomeRebalance_PrefersBumpingMismatchedPreferenceOverMatched()
    {
        // 3 slots (bands: 1=Early, 2=Middle, 3=Late). Slot 1 fills to a full
        // foursome this run with 3 Early-preferrers plus 1 no-preference
        // filler. Slot 2 ends up a trailing twosome. The rebalance must
        // borrow the no-preference filler from slot 1, not one of the
        // Early-preferrers who were deliberately matched to the Early slot.
        var earlyPreferrers = Enumerable.Range(1, 3)
            .Select(i => MakeParticipant(i, TeeTimeSlotPreference.Early))
            .ToList();
        var filler = MakeParticipant(4, TeeTimeSlotPreference.None);
        var twosome = Enumerable.Range(5, 2)
            .Select(i => MakeParticipant(i, TeeTimeSlotPreference.None))
            .ToList();

        var round = new Round
        {
            Id = 1,
            Participants = earlyPreferrers.Concat(new[] { filler }).Concat(twosome).ToList(),
        };
        var slots = new List<RoundTeeTime>
        {
            new() { Id = 101, RoundId = 1, TeeTimeNumber = 1, Participants = [] },
            new() { Id = 102, RoundId = 1, TeeTimeNumber = 2, Participants = [] },
            new() { Id = 103, RoundId = 1, TeeTimeNumber = 3, Participants = [] },
        };
        var (sut, assignments) = BuildSut(round, slots);

        var result = await sut.RunAsync(1);

        result.IsSuccess.Should().BeTrue();
        Enumerable.Range(1, 3).Select(id => assignments[id])
            .Should().OnlyContain(slotId => slotId == 101,
                "Early-preferrers seated in the Early slot must not be bumped out by the trailing-twosome rebalance");
        assignments[4].Should().Be(102, "the no-preference filler is the one that should be borrowed to fix the twosome");
    }

    [Fact]
    public async Task RunAsync_NineOrMoreSlots_UsesFixedThreeSlotBands()
    {
        // 12 slots (up to 48 players). With fixed bands, Early = tee times
        // 1-3, Middle = 4-6, Late = 7-12. Seat exactly 12 Early-preferrers
        // (fills bands 1-3 to capacity) plus 4 Middle-preferrers: the
        // Middle-preferrers must NOT be able to displace Early-preferrers
        // out of slots 1-3, and must land in slots 4-6 (the Middle band),
        // not slot 4 alone or spilled into slot 7+ (which would indicate a
        // proportional 4/4/4 split instead of the fixed 3/3/3+ split).
        var earlyPreferrers = Enumerable.Range(1, 12)
            .Select(i => MakeParticipant(i, TeeTimeSlotPreference.Early))
            .ToList();
        var middlePreferrers = Enumerable.Range(13, 4)
            .Select(i => MakeParticipant(i, TeeTimeSlotPreference.Middle))
            .ToList();
        var round = new Round
        {
            Id = 1,
            Participants = earlyPreferrers.Concat(middlePreferrers).ToList(),
        };
        var slots = Enumerable.Range(1, 12)
            .Select(n => new RoundTeeTime { Id = 100 + n, RoundId = 1, TeeTimeNumber = n, Participants = [] })
            .ToList();
        var (sut, assignments) = BuildSut(round, slots);

        await sut.RunAsync(1);

        var earlySlotIds = new[] { 101, 102, 103 };
        var middleSlotIds = new[] { 104, 105, 106 };

        Enumerable.Range(1, 12).Select(id => assignments[id])
            .Should().OnlyContain(id => earlySlotIds.Contains(id),
                "with fixed 3-slot bands, tee times 1-3 are the entire Early band and hold exactly the 12 Early-preferrers");
        Enumerable.Range(13, 4).Select(id => assignments[id])
            .Should().OnlyContain(id => middleSlotIds.Contains(id),
                "tee times 4-6 are the fixed Middle band, not 4-7 (which a proportional 4/4/4 split would use)");
    }

    [Fact]
    public async Task RunAsync_TrailingTwosome_IsRebalancedEvenWhenOtherSlotHasManualPick()
    {
        // Slot 1 has 1 manual pick already seated; 5 more players need seats
        // (6 total), needing 2 slots. Phase 1 tops slot 1 off to 4 (adding 3
        // autofill-placed players around the manual pick), leaving 2
        // newcomers for slot 2 -- a twosome that must be rebalanced by
        // borrowing one AUTOFILL-PLACED player from slot 1, never the
        // manual pick itself.
        var manual = MakeParticipant(1, TeeTimeSlotPreference.None);
        manual.TeeTimeId = 101;
        var newcomers = Enumerable.Range(2, 5)
            .Select(i => MakeParticipant(i, TeeTimeSlotPreference.None))
            .ToList();
        var round = new Round { Id = 1, Participants = new List<RoundParticipant> { manual }.Concat(newcomers).ToList() };
        var slots = new List<RoundTeeTime>
        {
            new() { Id = 101, RoundId = 1, TeeTimeNumber = 1, Participants = [manual] },
            new() { Id = 102, RoundId = 1, TeeTimeNumber = 2, Participants = [] },
        };
        var (sut, assignments) = BuildSut(round, slots);

        var result = await sut.RunAsync(1);

        result.IsSuccess.Should().BeTrue();
        assignments.Should().NotContainKey(1, "the manual pick must stay put");
        var bySlot = assignments.Values.GroupBy(slotId => slotId).ToDictionary(g => g.Key, g => g.Count());
        // Slot 1's manual pick isn't in `assignments`, so its total
        // occupancy is bySlot[101] + 1.
        (bySlot.GetValueOrDefault(101) + 1).Should().Be(3, "one autofill-placed player moves out of slot 1 to avoid a twosome");
        bySlot[102].Should().Be(3, "slot 2 becomes a threesome instead of a lone twosome");
    }
}
