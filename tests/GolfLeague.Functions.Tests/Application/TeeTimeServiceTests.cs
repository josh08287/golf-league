using FluentAssertions;
using GolfLeague.Application.Rounds;
using GolfLeague.Domain.Entities;
using GolfLeague.Domain.Interfaces;
using GolfLeague.Domain.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace GolfLeague.Tests.Application;

/// <summary>
/// Coverage for the round-day self-service tee-time switch: an
/// already-assigned participant may move to a different open slot on the day
/// of their round even after the general Sunday-noon-ET sign-up cutoff has
/// passed. Everything else about the cutoff (fresh joins, LeaveAsync, and
/// non-round-day joins) is unchanged.
/// </summary>
public class TeeTimeServiceTests
{
    private static DateOnly EasternToday(DateTime utcNow) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(utcNow, TeeTimeSchedule.EasternTimeZone));

    /// <summary>
    /// Today's Eastern date, for use as a "round day" fixture in the
    /// after-cutoff tests. The cutoff for a round dated today is the most
    /// recent Sunday noon ET, which is always in the past UNLESS today is
    /// itself a Sunday and it's not yet noon ET — an edge case explicitly
    /// asserted against below so the suite fails loudly (rather than
    /// flaking) if it's ever run in that window.
    /// </summary>
    private static DateOnly RoundDayWithPassedCutoff()
    {
        var nowUtc = DateTime.UtcNow;
        var today = EasternToday(nowUtc);
        if (TeeTimeSchedule.ComputeSundayNoonCutoffUtc(today) > nowUtc)
        {
            throw new InvalidOperationException(
                "Test run during the Sunday-before-noon-ET window, where 'round day' has no " +
                "passed cutoff to exercise. Re-run after noon ET.");
        }
        return today;
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

        rounds.Setup(r => r.GetByIdAsync(round.Id, It.IsAny<CancellationToken>())).ReturnsAsync(round);
        teeTimes.Setup(t => t.EnsureSlotsAsync(round.Id, It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(slots);
        teeTimes.Setup(t => t.GetByRoundAsync(round.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(slots);
        teeTimes.Setup(t => t.GetByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((int id, CancellationToken _) => slots.FirstOrDefault(s => s.Id == id));
        teeTimes.Setup(t => t.SetParticipantTeeTimeAsync(It.IsAny<int>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var sut = new TeeTimeService(rounds.Object, teeTimes.Object, new Mock<ILogger<TeeTimeService>>().Object);
        return (sut, teeTimes);
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
