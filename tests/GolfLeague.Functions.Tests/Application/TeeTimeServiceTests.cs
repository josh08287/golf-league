using FluentAssertions;
using GolfLeague.Application.Common;
using GolfLeague.Application.Rounds;
using GolfLeague.Domain.Entities;
using GolfLeague.Domain.Enums;
using GolfLeague.Domain.Interfaces;
using GolfLeague.Domain.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace GolfLeague.Tests.Application;

/// <summary>
/// Coverage for the round-day self-service tee-time switch: an
/// already-assigned participant may move to a different open slot on the day
/// of their round even after the general 6pm-ET-day-before sign-up cutoff has
/// passed. Everything else about the cutoff (fresh joins, LeaveAsync, and
/// non-round-day joins) is unchanged.
/// </summary>
public class TeeTimeServiceTests
{
    private static DateOnly EasternToday(DateTime utcNow) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(utcNow, TeeTimeSchedule.EasternTimeZone));

    /// <summary>
    /// Today's Eastern date, for use as a "round day" fixture in the
    /// after-cutoff tests. The cutoff for a round dated today is 6pm ET the
    /// day before, which is always in the past by the time "today" exists.
    /// </summary>
    private static DateOnly RoundDayWithPassedCutoff()
    {
        var nowUtc = DateTime.UtcNow;
        return EasternToday(nowUtc);
    }

    private static RoundParticipant MakeParticipant(int id, int? teeTimeId = null) => new()
    {
        Id = id,
        PlayerId = id,
        RoundId = 1,
        TeeTimeId = teeTimeId,
        Player = new Player { Id = id, FirstName = "P", LastName = id.ToString() },
    };

    private static RoundTeeTime MakeSlot(int id, int teeTimeNumber, params RoundParticipant[] participants)
    {
        var slot = new RoundTeeTime { Id = id, RoundId = 1, TeeTimeNumber = teeTimeNumber };
        foreach (var p in participants)
        {
            slot.Participants.Add(p);
        }
        return slot;
    }

    private static (TeeTimeService Sut, Mock<ITeeTimeRepository> TeeTimes) BuildSut(
        Round round, IReadOnlyList<RoundTeeTime> slots)
    {
        var rounds = new Mock<IRoundRepository>();
        var teeTimes = new Mock<ITeeTimeRepository>();
        var auditRepository = new Mock<IAuditRepository>();

        rounds.Setup(r => r.GetByIdAsync(round.Id, It.IsAny<CancellationToken>())).ReturnsAsync(round);
        teeTimes.Setup(t => t.EnsureSlotsAsync(round.Id, It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(slots);
        teeTimes.Setup(t => t.GetByRoundAsync(round.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(slots);
        teeTimes.Setup(t => t.GetByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((int id, CancellationToken _) => slots.FirstOrDefault(s => s.Id == id));
        teeTimes.Setup(t => t.SetParticipantTeeTimeAsync(It.IsAny<int>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var leagueSettings = new Mock<ILeagueSettingRepository>();
        leagueSettings.Setup(s => s.GetAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((LeagueSetting?)null);

        var players = new Mock<IPlayerRepository>();
        var handicaps = new Mock<IHandicapRepository>();

        var auditWriter = new AuditWriter(auditRepository.Object, new Mock<ILogger<AuditWriter>>().Object);
        var sut = new TeeTimeService(rounds.Object, teeTimes.Object, leagueSettings.Object, players.Object, handicaps.Object, auditWriter, new Mock<ILogger<TeeTimeService>>().Object);
        return (sut, teeTimes);
    }

    private static (TeeTimeService Sut, Mock<ITeeTimeRepository> TeeTimes, Mock<IAuditRepository> AuditRepo) BuildSutWithAudit(
        Round round, IReadOnlyList<RoundTeeTime> slots)
    {
        var rounds = new Mock<IRoundRepository>();
        var teeTimes = new Mock<ITeeTimeRepository>();
        var auditRepository = new Mock<IAuditRepository>();

        rounds.Setup(r => r.GetByIdAsync(round.Id, It.IsAny<CancellationToken>())).ReturnsAsync(round);
        teeTimes.Setup(t => t.EnsureSlotsAsync(round.Id, It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(slots);
        teeTimes.Setup(t => t.GetByRoundAsync(round.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(slots);
        teeTimes.Setup(t => t.GetByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((int id, CancellationToken _) => slots.FirstOrDefault(s => s.Id == id));
        teeTimes.Setup(t => t.SetParticipantTeeTimeAsync(It.IsAny<int>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var leagueSettings = new Mock<ILeagueSettingRepository>();
        leagueSettings.Setup(s => s.GetAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((LeagueSetting?)null);

        var players = new Mock<IPlayerRepository>();
        var handicaps = new Mock<IHandicapRepository>();

        var auditWriter = new AuditWriter(auditRepository.Object, new Mock<ILogger<AuditWriter>>().Object);
        var sut = new TeeTimeService(rounds.Object, teeTimes.Object, leagueSettings.Object, players.Object, handicaps.Object, auditWriter, new Mock<ILogger<TeeTimeService>>().Object);
        return (sut, teeTimes, auditRepository);
    }

    [Fact]
    public async Task JoinAsync_RejectsTournamentRound()
    {
        var round = new Round { Id = 1, RoundType = RoundType.Tournament, RoundDate = EasternToday(DateTime.UtcNow).AddDays(10) };
        var participant = MakeParticipant(1);
        round.Participants.Add(participant);
        var slot = MakeSlot(10, 1);
        var (sut, _) = BuildSut(round, [slot]);

        var result = await sut.JoinAsync(1, teeTimeId: 10, callingPlayerId: 1);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("tournament");
    }

    [Fact]
    public async Task SwapAsync_RejectsTournamentRound()
    {
        var round = new Round { Id = 1, RoundType = RoundType.Tournament, RoundDate = EasternToday(DateTime.UtcNow).AddDays(10) };
        var caller = MakeParticipant(1, teeTimeId: 10);
        var other = MakeParticipant(2, teeTimeId: 11);
        round.Participants.Add(caller);
        round.Participants.Add(other);
        var slotA = MakeSlot(10, 1, caller);
        var slotB = MakeSlot(11, 2, other);
        var (sut, teeTimes) = BuildSut(round, [slotA, slotB]);

        var result = await sut.SwapAsync(1, callingPlayerId: 1, otherParticipantId: other.Id);

        result.IsSuccess.Should().BeFalse();
        teeTimes.Verify(t => t.SwapParticipantTeeTimesAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SwapAsync_TwoAssignedParticipantsInDifferentSlots_Swaps()
    {
        var round = new Round { Id = 1, RoundDate = EasternToday(DateTime.UtcNow).AddDays(10) };
        var caller = MakeParticipant(1, teeTimeId: 10);
        var other = MakeParticipant(2, teeTimeId: 11);
        round.Participants.Add(caller);
        round.Participants.Add(other);
        var slotA = MakeSlot(10, 1, caller);
        var slotB = MakeSlot(11, 2, other);
        var (sut, teeTimes) = BuildSut(round, [slotA, slotB]);

        var result = await sut.SwapAsync(1, callingPlayerId: 1, otherParticipantId: other.Id);

        result.IsSuccess.Should().BeTrue();
        teeTimes.Verify(t => t.SwapParticipantTeeTimesAsync(caller.Id, other.Id, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SwapAsync_NotGatedBySignupCutoff()
    {
        // Cutoff long passed and not round day — unlike JoinAsync, swap must still succeed.
        var round = new Round { Id = 1, RoundDate = EasternToday(DateTime.UtcNow).AddDays(-30) };
        var caller = MakeParticipant(1, teeTimeId: 10);
        var other = MakeParticipant(2, teeTimeId: 11);
        round.Participants.Add(caller);
        round.Participants.Add(other);
        var slotA = MakeSlot(10, 1, caller);
        var slotB = MakeSlot(11, 2, other);
        var (sut, teeTimes) = BuildSut(round, [slotA, slotB]);

        var result = await sut.SwapAsync(1, callingPlayerId: 1, otherParticipantId: other.Id);

        result.IsSuccess.Should().BeTrue();
        teeTimes.Verify(t => t.SwapParticipantTeeTimesAsync(caller.Id, other.Id, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SwapAsync_CallerNotInRound_Fails()
    {
        var round = new Round { Id = 1, RoundDate = EasternToday(DateTime.UtcNow).AddDays(10) };
        var other = MakeParticipant(2, teeTimeId: 11);
        round.Participants.Add(other);
        var slotB = MakeSlot(11, 2, other);
        var (sut, teeTimes) = BuildSut(round, [slotB]);

        var result = await sut.SwapAsync(1, callingPlayerId: 1, otherParticipantId: other.Id);

        result.IsSuccess.Should().BeFalse();
        teeTimes.Verify(t => t.SwapParticipantTeeTimesAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SwapAsync_OtherParticipantNotInRound_Fails()
    {
        var round = new Round { Id = 1, RoundDate = EasternToday(DateTime.UtcNow).AddDays(10) };
        var caller = MakeParticipant(1, teeTimeId: 10);
        round.Participants.Add(caller);
        var slotA = MakeSlot(10, 1, caller);
        var (sut, teeTimes) = BuildSut(round, [slotA]);

        var result = await sut.SwapAsync(1, callingPlayerId: 1, otherParticipantId: 999);

        result.IsSuccess.Should().BeFalse();
        teeTimes.Verify(t => t.SwapParticipantTeeTimesAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SwapAsync_SameParticipant_Fails()
    {
        var round = new Round { Id = 1, RoundDate = EasternToday(DateTime.UtcNow).AddDays(10) };
        var caller = MakeParticipant(1, teeTimeId: 10);
        round.Participants.Add(caller);
        var slotA = MakeSlot(10, 1, caller);
        var (sut, teeTimes) = BuildSut(round, [slotA]);

        var result = await sut.SwapAsync(1, callingPlayerId: 1, otherParticipantId: caller.Id);

        result.IsSuccess.Should().BeFalse();
        teeTimes.Verify(t => t.SwapParticipantTeeTimesAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SwapAsync_OtherParticipantAlreadyInSameGroup_Fails()
    {
        var round = new Round { Id = 1, RoundDate = EasternToday(DateTime.UtcNow).AddDays(10) };
        var caller = MakeParticipant(1, teeTimeId: 10);
        var groupmate = MakeParticipant(2, teeTimeId: 10);
        round.Participants.Add(caller);
        round.Participants.Add(groupmate);
        var slotA = MakeSlot(10, 1, caller, groupmate);
        var (sut, teeTimes) = BuildSut(round, [slotA]);

        var result = await sut.SwapAsync(1, callingPlayerId: 1, otherParticipantId: groupmate.Id);

        result.IsSuccess.Should().BeFalse();
        teeTimes.Verify(t => t.SwapParticipantTeeTimesAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SwapAsync_CallerUnassigned_Fails()
    {
        var round = new Round { Id = 1, RoundDate = EasternToday(DateTime.UtcNow).AddDays(10) };
        var caller = MakeParticipant(1, teeTimeId: null);
        var other = MakeParticipant(2, teeTimeId: 11);
        round.Participants.Add(caller);
        round.Participants.Add(other);
        var slotB = MakeSlot(11, 2, other);
        var (sut, teeTimes) = BuildSut(round, [slotB]);

        var result = await sut.SwapAsync(1, callingPlayerId: 1, otherParticipantId: other.Id);

        result.IsSuccess.Should().BeFalse();
        teeTimes.Verify(t => t.SwapParticipantTeeTimesAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SwapAsync_WritesAuditLogForLinkedPlayer()
    {
        var round = new Round { Id = 1, LeagueId = 7, RoundDate = EasternToday(DateTime.UtcNow).AddDays(10) };
        var appUserId = Guid.NewGuid();
        var caller = MakeParticipant(1, teeTimeId: 10);
        caller.Player!.AppUserId = appUserId;
        var other = MakeParticipant(2, teeTimeId: 11);
        round.Participants.Add(caller);
        round.Participants.Add(other);
        var slotA = MakeSlot(10, 1, caller);
        var slotB = MakeSlot(11, 2, other);
        var (sut, _, auditRepo) = BuildSutWithAudit(round, [slotA, slotB]);

        await sut.SwapAsync(1, callingPlayerId: 1, otherParticipantId: other.Id);

        auditRepo.Verify(a => a.AddAsync(
            It.Is<AuditLog>(l =>
                l.Action == "TeeTimeSwapped" &&
                l.EntityType == "Round" &&
                l.EntityId == "1" &&
                l.UserId == appUserId.ToString() &&
                l.LeagueId == 7),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task JoinAsync_BeforeCutoff_AllowsJoinIntoOpenSlot()
    {
        // Round is far enough out that the Sunday-noon-ET cutoff hasn't passed.
        var roundDate = EasternToday(DateTime.UtcNow).AddDays(10);
        var round = new Round { Id = 1, RoundDate = roundDate };
        var participant = MakeParticipant(1);
        round.Participants.Add(participant);
        var slotA = MakeSlot(10, 1);
        var (sut, teeTimes) = BuildSut(round, [slotA]);

        var result = await sut.JoinAsync(1, 10, callingPlayerId: 1);

        result.IsSuccess.Should().BeTrue();
        teeTimes.Verify(t => t.SetParticipantTeeTimeAsync(participant.Id, 10, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task JoinAsync_WritesAuditLogForLinkedPlayer()
    {
        var roundDate = EasternToday(DateTime.UtcNow).AddDays(10);
        var round = new Round { Id = 1, LeagueId = 7, RoundDate = roundDate };
        var appUserId = Guid.NewGuid();
        var participant = MakeParticipant(1);
        participant.Player!.AppUserId = appUserId;
        round.Participants.Add(participant);
        var slotA = MakeSlot(10, 1);
        var (sut, _, auditRepo) = BuildSutWithAudit(round, [slotA]);

        await sut.JoinAsync(1, 10, callingPlayerId: 1);

        auditRepo.Verify(a => a.AddAsync(
            It.Is<AuditLog>(l =>
                l.Action == "TeeTimeSelected" &&
                l.EntityType == "Round" &&
                l.EntityId == "1" &&
                l.UserId == appUserId.ToString() &&
                l.LeagueId == 7),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task JoinAsync_NoLinkedAppUser_SkipsAuditLog()
    {
        var roundDate = EasternToday(DateTime.UtcNow).AddDays(10);
        var round = new Round { Id = 1, RoundDate = roundDate };
        var participant = MakeParticipant(1); // Player.AppUserId left null
        round.Participants.Add(participant);
        var slotA = MakeSlot(10, 1);
        var (sut, _, auditRepo) = BuildSutWithAudit(round, [slotA]);

        await sut.JoinAsync(1, 10, callingPlayerId: 1);

        auditRepo.Verify(a => a.AddAsync(It.IsAny<AuditLog>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task JoinAsync_AfterCutoff_NotRoundDay_Fails()
    {
        // Round date is far in the past (cutoff long gone) and not "today" in ET.
        var roundDate = EasternToday(DateTime.UtcNow).AddDays(-30);
        var round = new Round { Id = 1, RoundDate = roundDate };
        var participant = MakeParticipant(1, teeTimeId: 10);
        round.Participants.Add(participant);
        var slotA = MakeSlot(10, 1, participant);
        var slotB = MakeSlot(11, 2);
        var (sut, teeTimes) = BuildSut(round, [slotA, slotB]);

        var result = await sut.JoinAsync(1, 11, callingPlayerId: 1);

        result.IsSuccess.Should().BeFalse();
        teeTimes.Verify(t => t.SetParticipantTeeTimeAsync(It.IsAny<int>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task JoinAsync_AfterCutoff_RoundDay_ParticipantAlreadyAssigned_MovesIntoOpenSlot()
    {
        var roundDate = RoundDayWithPassedCutoff();
        var round = new Round { Id = 1, RoundDate = roundDate };
        var participant = MakeParticipant(1, teeTimeId: 10);
        round.Participants.Add(participant);
        var slotA = MakeSlot(10, 1, participant);
        var slotB = MakeSlot(11, 2);
        var (sut, teeTimes) = BuildSut(round, [slotA, slotB]);

        var result = await sut.JoinAsync(1, 11, callingPlayerId: 1);

        result.IsSuccess.Should().BeTrue();
        teeTimes.Verify(t => t.SetParticipantTeeTimeAsync(participant.Id, 11, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task JoinAsync_AfterCutoff_RoundDay_ParticipantNotYetAssigned_StillFails()
    {
        var roundDate = RoundDayWithPassedCutoff();
        var round = new Round { Id = 1, RoundDate = roundDate };
        var participant = MakeParticipant(1, teeTimeId: null);
        round.Participants.Add(participant);
        var slotA = MakeSlot(10, 1);
        var (sut, teeTimes) = BuildSut(round, [slotA]);

        var result = await sut.JoinAsync(1, 10, callingPlayerId: 1);

        result.IsSuccess.Should().BeFalse();
        teeTimes.Verify(t => t.SetParticipantTeeTimeAsync(It.IsAny<int>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task JoinAsync_AfterCutoff_RoundDay_TargetSlotFull_Fails()
    {
        var roundDate = RoundDayWithPassedCutoff();
        var round = new Round { Id = 1, RoundDate = roundDate };
        var mover = MakeParticipant(1, teeTimeId: 10);
        round.Participants.Add(mover);
        var full = new[] { MakeParticipant(2), MakeParticipant(3), MakeParticipant(4), MakeParticipant(5) };
        foreach (var p in full) round.Participants.Add(p);

        var slotA = MakeSlot(10, 1, mover);
        var slotB = MakeSlot(11, 2, full);
        var (sut, teeTimes) = BuildSut(round, [slotA, slotB]);

        var result = await sut.JoinAsync(1, 11, callingPlayerId: 1);

        result.IsSuccess.Should().BeFalse();
        teeTimes.Verify(t => t.SetParticipantTeeTimeAsync(It.IsAny<int>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task LeaveAsync_AfterCutoff_RoundDay_StillBlocked()
    {
        var roundDate = RoundDayWithPassedCutoff();
        var round = new Round { Id = 1, RoundDate = roundDate };
        var participant = MakeParticipant(1, teeTimeId: 10);
        round.Participants.Add(participant);
        var slotA = MakeSlot(10, 1, participant);
        var (sut, teeTimes) = BuildSut(round, [slotA]);

        var result = await sut.LeaveAsync(1, callingPlayerId: 1);

        result.IsSuccess.Should().BeFalse();
        teeTimes.Verify(t => t.SetParticipantTeeTimeAsync(It.IsAny<int>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}

/// <summary>
/// Coverage for substitute self-service: a pool sub joining a tee-time slot
/// themselves (JoinAsSubstituteAsync), leaving (LeaveAsync removes their row
/// outright), and the schedule flag that drives the join buttons.
/// </summary>
public class JoinAsSubstituteTests
{
    private static Round MakeRound(params RoundParticipant[] participants)
    {
        var round = new Round
        {
            Id = 1,
            LeagueId = 1,
            // Well in the future so the sign-up window is open.
            RoundDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(10),
            Course = new Course { Id = 1, Name = "Test", SlopeRating = 113, CourseRating = 35.0 },
        };
        foreach (var p in participants) round.Participants.Add(p);
        return round;
    }

    private static RoundParticipant MakeParticipant(
        int id, bool skipped = false, bool isSub = false, int? teeTimeId = null, int? flightId = null)
        => new()
        {
            Id = id,
            PlayerId = id,
            RoundId = 1,
            SkippedWeek = skipped,
            IsSubstitute = isSub,
            TeeTimeId = teeTimeId,
            FlightId = flightId,
            Player = new Player { Id = id, FirstName = "P", LastName = id.ToString() },
        };

    private static RoundTeeTime MakeSlot(int id, params RoundParticipant[] participants)
    {
        var slot = new RoundTeeTime { Id = id, RoundId = 1, TeeTimeNumber = id };
        foreach (var p in participants) slot.Participants.Add(p);
        return slot;
    }

    private static (TeeTimeService Sut, Mock<IRoundRepository> Rounds) BuildSut(
        Round round,
        IReadOnlyList<RoundTeeTime> slots,
        Player? callerPlayer,
        bool substitutesEnabled = true)
    {
        var rounds = new Mock<IRoundRepository>();
        var teeTimes = new Mock<ITeeTimeRepository>();

        rounds.Setup(r => r.GetByIdAsync(round.Id, It.IsAny<CancellationToken>())).ReturnsAsync(round);
        teeTimes.Setup(t => t.EnsureSlotsAsync(round.Id, It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(slots);
        teeTimes.Setup(t => t.GetByRoundAsync(round.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(slots);
        teeTimes.Setup(t => t.GetByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((int id, CancellationToken _) => slots.FirstOrDefault(s => s.Id == id));

        var leagueSettings = new Mock<ILeagueSettingRepository>();
        leagueSettings.Setup(s => s.GetAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((LeagueSetting?)null);
        leagueSettings.Setup(s => s.GetAsync(
                It.IsAny<int>(),
                GolfLeague.Application.Leagues.KnownSettings.SubstitutesEnabled,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LeagueSetting
            {
                Key = GolfLeague.Application.Leagues.KnownSettings.SubstitutesEnabled,
                Value = substitutesEnabled ? "true" : "false",
            });

        var players = new Mock<IPlayerRepository>();
        if (callerPlayer is not null)
        {
            players.Setup(p => p.GetByIdAsync(callerPlayer.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(callerPlayer);
        }

        var handicaps = new Mock<IHandicapRepository>();
        var auditWriter = new AuditWriter(new Mock<IAuditRepository>().Object, new Mock<ILogger<AuditWriter>>().Object);
        var sut = new TeeTimeService(rounds.Object, teeTimes.Object, leagueSettings.Object, players.Object, handicaps.Object, auditWriter, new Mock<ILogger<TeeTimeService>>().Object);
        return (sut, rounds);
    }

    private static Player MakePoolSub(int id = 99) =>
        new() { Id = id, FirstName = "Sub", LastName = "Player", IsSubstitute = true };

    [Fact]
    public async Task JoinAsSubstitute_PoolSubWithOpenSkipSpot_JoinsChosenSlot()
    {
        var skipped = MakeParticipant(1, skipped: true, flightId: 5);
        var seated = MakeParticipant(2, teeTimeId: 10);
        var round = MakeRound(skipped, seated);
        var slot = MakeSlot(10, seated);
        var (sut, rounds) = BuildSut(round, [slot], MakePoolSub());

        var result = await sut.JoinAsSubstituteAsync(1, teeTimeId: 10, callingPlayerId: 99);

        result.IsSuccess.Should().BeTrue();
        rounds.Verify(r => r.AddParticipantAsync(
            It.Is<RoundParticipant>(p =>
                p.PlayerId == 99
                && p.IsSubstitute
                && p.TeeTimeId == 10
                && p.SubstituteForParticipantId == skipped.Id
                && p.FlightId == 5),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task JoinAsSubstitute_SubstitutesDisabled_Fails()
    {
        var round = MakeRound(MakeParticipant(1, skipped: true));
        var (sut, rounds) = BuildSut(round, [MakeSlot(10)], MakePoolSub(), substitutesEnabled: false);

        var result = await sut.JoinAsSubstituteAsync(1, 10, 99);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("enabled");
        rounds.Verify(r => r.AddParticipantAsync(It.IsAny<RoundParticipant>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task JoinAsSubstitute_CallerNotInPool_Fails()
    {
        var round = MakeRound(MakeParticipant(1, skipped: true));
        var notASub = new Player { Id = 99, FirstName = "N", LastName = "S", IsSubstitute = false };
        var (sut, rounds) = BuildSut(round, [MakeSlot(10)], notASub);

        var result = await sut.JoinAsSubstituteAsync(1, 10, 99);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("substitute pool");
        rounds.Verify(r => r.AddParticipantAsync(It.IsAny<RoundParticipant>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task JoinAsSubstitute_AlreadyInRound_Fails()
    {
        var existing = MakeParticipant(99, isSub: true, teeTimeId: 10);
        var round = MakeRound(MakeParticipant(1, skipped: true), existing);
        var (sut, rounds) = BuildSut(round, [MakeSlot(10, existing)], MakePoolSub());

        var result = await sut.JoinAsSubstituteAsync(1, 10, 99);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("already in this round");
        rounds.Verify(r => r.AddParticipantAsync(It.IsAny<RoundParticipant>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task JoinAsSubstitute_NoSkipCapacity_Fails()
    {
        // One skip, already claimed by another substitute.
        var otherSub = MakeParticipant(50, isSub: true, teeTimeId: 10);
        var round = MakeRound(MakeParticipant(1, skipped: true), otherSub);
        var (sut, rounds) = BuildSut(round, [MakeSlot(10, otherSub)], MakePoolSub());

        var result = await sut.JoinAsSubstituteAsync(1, 10, 99);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("No substitute spots");
        rounds.Verify(r => r.AddParticipantAsync(It.IsAny<RoundParticipant>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task JoinAsSubstitute_SlotFull_Fails()
    {
        var seated = Enumerable.Range(2, 4).Select(i => MakeParticipant(i, teeTimeId: 10)).ToArray();
        var round = MakeRound([MakeParticipant(1, skipped: true), .. seated]);
        var (sut, rounds) = BuildSut(round, [MakeSlot(10, seated)], MakePoolSub());

        var result = await sut.JoinAsSubstituteAsync(1, 10, 99);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("full");
        rounds.Verify(r => r.AddParticipantAsync(It.IsAny<RoundParticipant>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Leave_SubstituteParticipant_RemovesRowFromRound()
    {
        var sub = MakeParticipant(99, isSub: true, teeTimeId: 10);
        var round = MakeRound(MakeParticipant(1, skipped: true), sub);
        var (sut, rounds) = BuildSut(round, [MakeSlot(10, sub)], MakePoolSub());

        var result = await sut.LeaveAsync(1, callingPlayerId: 99);

        result.IsSuccess.Should().BeTrue();
        rounds.Verify(r => r.DeleteParticipantAsync(sub.Id, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetSchedule_PoolSubNotInRound_SetsPoolMemberFlag()
    {
        var round = MakeRound(MakeParticipant(1, skipped: true));
        var (sut, _) = BuildSut(round, [MakeSlot(10)], MakePoolSub());

        var result = await sut.GetScheduleAsync(1, callingPlayerId: 99);

        result.IsSuccess.Should().BeTrue();
        result.Value!.CurrentUserIsSubstitutePoolMember.Should().BeTrue();
        result.Value!.CurrentUserIsSubstitute.Should().BeFalse();
    }
}
