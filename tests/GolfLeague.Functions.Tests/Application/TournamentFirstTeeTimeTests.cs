using FluentAssertions;
using GolfLeague.Application.Rounds.Commands;
using GolfLeague.Domain.Entities;
using GolfLeague.Domain.Enums;
using GolfLeague.Domain.Interfaces;
using GolfLeague.Domain.Services;
using Moq;
using Xunit;

namespace GolfLeague.Tests.Application;

public class TournamentFirstTeeTimeTests
{
    [Fact]
    public void TimeForSlot_CustomStart_SpacesGroupsAtTheUsualInterval()
    {
        var start = new TimeOnly(8, 30);

        TeeTimeSchedule.TimeForSlot(1, start).Should().Be(new TimeOnly(8, 30));
        TeeTimeSchedule.TimeForSlot(3, start).Should().Be(new TimeOnly(8, 46));
    }

    [Fact]
    public void TimeForSlot_NoStart_UsesDefault()
    {
        TeeTimeSchedule.TimeForSlot(1).Should().Be(TeeTimeSchedule.FirstTeeTime);
        TeeTimeSchedule.TimeForSlot(2, null).Should().Be(TeeTimeSchedule.FirstTeeTime.AddMinutes(TeeTimeSchedule.IntervalMinutes));
    }

    [Fact]
    public void LastTeeTimeUtc_UsesTheRoundsOwnStart()
    {
        var date = new DateOnly(2026, 10, 10);

        var morning = TeeTimeSchedule.LastTeeTimeUtc(date, participantCount: 8, firstTeeTime: new TimeOnly(8, 0));
        var defaultStart = TeeTimeSchedule.LastTeeTimeUtc(date, participantCount: 8);

        // 8 players = 2 groups; last group 8:08 vs 3:36pm on the default schedule.
        (defaultStart - morning).Should().Be(TimeSpan.FromMinutes((15 * 60 + 28) - (8 * 60)));
    }

    private static (Mock<IRoundRepository> Rounds, Mock<ITeeTimeRepository> TeeTimes) Mocks(Round round)
    {
        var rounds = new Mock<IRoundRepository>();
        rounds.Setup(r => r.GetByIdAsync(round.Id, It.IsAny<CancellationToken>())).ReturnsAsync(round);
        return (rounds, new Mock<ITeeTimeRepository>());
    }

    [Fact]
    public async Task SetFirstTeeTime_SavesStartAndRetimesExistingTeeTimes()
    {
        var round = new Round { Id = 1, RoundType = RoundType.Tournament, Status = RoundStatus.Scheduled, Participants = [] };
        var (rounds, teeTimes) = Mocks(round);
        var start = new TimeOnly(9, 0);

        var result = await new SetTournamentFirstTeeTimeCommandHandler(rounds.Object, teeTimes.Object)
            .Handle(new SetTournamentFirstTeeTimeCommand(1, start, "admin-1"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        rounds.Verify(r => r.UpdateAsync(It.Is<Round>(x => x.FirstTeeTime == start), It.IsAny<CancellationToken>()), Times.Once);
        teeTimes.Verify(t => t.RetimeSlotsAsync(1, start, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SetFirstTeeTime_FinalizedRound_IsRejected()
    {
        var round = new Round { Id = 1, RoundType = RoundType.Tournament, Status = RoundStatus.Finalized, Participants = [] };
        var (rounds, teeTimes) = Mocks(round);

        var result = await new SetTournamentFirstTeeTimeCommandHandler(rounds.Object, teeTimes.Object)
            .Handle(new SetTournamentFirstTeeTimeCommand(1, new TimeOnly(9, 0), "admin-1"), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        teeTimes.Verify(t => t.RetimeSlotsAsync(It.IsAny<int>(), It.IsAny<TimeOnly?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SetFirstTeeTime_WeeklyRound_IsRejected()
    {
        var round = new Round { Id = 1, RoundType = RoundType.NineHole, Status = RoundStatus.Scheduled, Participants = [] };
        var (rounds, teeTimes) = Mocks(round);

        var result = await new SetTournamentFirstTeeTimeCommandHandler(rounds.Object, teeTimes.Object)
            .Handle(new SetTournamentFirstTeeTimeCommand(1, new TimeOnly(9, 0), "admin-1"), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
    }
}
