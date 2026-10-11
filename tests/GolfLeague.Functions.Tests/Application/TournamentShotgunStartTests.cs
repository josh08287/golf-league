using FluentAssertions;
using GolfLeague.Application.Rounds.Commands;
using GolfLeague.Domain.Entities;
using GolfLeague.Domain.Enums;
using GolfLeague.Domain.Interfaces;
using GolfLeague.Domain.Services;
using Moq;
using Xunit;

namespace GolfLeague.Tests.Application;

public class TournamentShotgunStartTests
{
    [Fact]
    public void TimeForSlot_ShotgunStart_AllSlotsSayTheSameConfiguredTime()
    {
        var start = new TimeOnly(8, 30);

        TeeTimeSchedule.TimeForSlot(1, start, isShotgun: true).Should().Be(start);
        TeeTimeSchedule.TimeForSlot(2, start, isShotgun: true).Should().Be(start);
        TeeTimeSchedule.TimeForSlot(5, start, isShotgun: true).Should().Be(start);
    }

    [Fact]
    public void TimeForSlot_ShotgunStart_NoConfiguredStart_UsesDefaultStartForAllSlots()
    {
        TeeTimeSchedule.TimeForSlot(1, null, isShotgun: true).Should().Be(TeeTimeSchedule.FirstTeeTime);
        TeeTimeSchedule.TimeForSlot(2, null, isShotgun: true).Should().Be(TeeTimeSchedule.FirstTeeTime);
        TeeTimeSchedule.TimeForSlot(4, null, isShotgun: true).Should().Be(TeeTimeSchedule.FirstTeeTime);
    }

    [Fact]
    public void TimeForSlot_NotShotgun_StaggersSlotsAtInterval()
    {
        var start = new TimeOnly(8, 30);

        TeeTimeSchedule.TimeForSlot(1, start, isShotgun: false).Should().Be(new TimeOnly(8, 30));
        TeeTimeSchedule.TimeForSlot(2, start, isShotgun: false).Should().Be(new TimeOnly(8, 38));
        TeeTimeSchedule.TimeForSlot(3, start, isShotgun: false).Should().Be(new TimeOnly(8, 46));
    }

    [Fact]
    public void LastTeeTimeUtc_ShotgunStart_MatchesFirstSlotTime()
    {
        var date = new DateOnly(2026, 10, 10);
        var start = new TimeOnly(8, 0);

        var shotgunLast = TeeTimeSchedule.LastTeeTimeUtc(date, participantCount: 16, firstTeeTime: start, isShotgun: true);
        var firstSlotTime = TeeTimeSchedule.TimeForSlot(1, start, isShotgun: true);

        var easternZone = TeeTimeSchedule.EasternTimeZone;
        var localShotgunLast = TimeZoneInfo.ConvertTimeFromUtc(shotgunLast, easternZone);

        TimeOnly.FromDateTime(localShotgunLast).Should().Be(firstSlotTime);
    }

    private static (Mock<IRoundRepository> Rounds, Mock<ITeeTimeRepository> TeeTimes) Mocks(Round round)
    {
        var rounds = new Mock<IRoundRepository>();
        rounds.Setup(r => r.GetByIdAsync(round.Id, It.IsAny<CancellationToken>())).ReturnsAsync(round);
        return (rounds, new Mock<ITeeTimeRepository>());
    }

    [Fact]
    public async Task SetTournamentShotgun_SavesFlagAndRetimesSlots()
    {
        var start = new TimeOnly(8, 30);
        var round = new Round
        {
            Id = 1,
            RoundType = RoundType.Tournament,
            Status = RoundStatus.Scheduled,
            FirstTeeTime = start,
            IsShotgun = false,
            Participants = []
        };
        var (rounds, teeTimes) = Mocks(round);

        var result = await new SetTournamentShotgunCommandHandler(rounds.Object, teeTimes.Object)
            .Handle(new SetTournamentShotgunCommand(1, true, "admin-1"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        rounds.Verify(r => r.UpdateAsync(It.Is<Round>(x => x.IsShotgun == true), It.IsAny<CancellationToken>()), Times.Once);
        teeTimes.Verify(t => t.RetimeSlotsAsync(1, start, true, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SetTournamentShotgun_FinalizedRound_IsRejected()
    {
        var round = new Round { Id = 1, RoundType = RoundType.Tournament, Status = RoundStatus.Finalized, Participants = [] };
        var (rounds, teeTimes) = Mocks(round);

        var result = await new SetTournamentShotgunCommandHandler(rounds.Object, teeTimes.Object)
            .Handle(new SetTournamentShotgunCommand(1, true, "admin-1"), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        teeTimes.Verify(t => t.RetimeSlotsAsync(It.IsAny<int>(), It.IsAny<TimeOnly?>(), It.IsAny<bool?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SetTournamentShotgun_WeeklyRound_IsRejected()
    {
        var round = new Round { Id = 1, RoundType = RoundType.NineHole, Status = RoundStatus.Scheduled, Participants = [] };
        var (rounds, teeTimes) = Mocks(round);

        var result = await new SetTournamentShotgunCommandHandler(rounds.Object, teeTimes.Object)
            .Handle(new SetTournamentShotgunCommand(1, true, "admin-1"), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task SetFirstTeeTime_ShotgunRound_PassesShotgunFlagToRetime()
    {
        var round = new Round
        {
            Id = 1,
            RoundType = RoundType.Tournament,
            Status = RoundStatus.Scheduled,
            IsShotgun = true,
            Participants = []
        };
        var (rounds, teeTimes) = Mocks(round);
        var start = new TimeOnly(9, 0);

        var result = await new SetTournamentFirstTeeTimeCommandHandler(rounds.Object, teeTimes.Object)
            .Handle(new SetTournamentFirstTeeTimeCommand(1, start, "admin-1"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        teeTimes.Verify(t => t.RetimeSlotsAsync(1, start, true, It.IsAny<CancellationToken>()), Times.Once);
    }
}
