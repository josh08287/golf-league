using FluentAssertions;
using GolfLeague.Application.Rounds.Commands;
using GolfLeague.Domain.Entities;
using GolfLeague.Domain.Enums;
using GolfLeague.Domain.Interfaces;
using Moq;
using Xunit;

namespace GolfLeague.Tests.Application;

public class SetTournamentCountsTowardHandicapHandlerTests
{
    private static Round MakeRound(RoundType type = RoundType.Tournament, RoundStatus status = RoundStatus.Scheduled) => new()
    {
        Id = 1,
        RoundType = type,
        Status = status,
        Participants = [],
    };

    private static async Task<(Mock<IRoundRepository> Rounds, GolfLeague.Application.Common.Result<bool> Result)> Run(Round round, bool value)
    {
        var rounds = new Mock<IRoundRepository>();
        rounds.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(round);
        var result = await new SetTournamentCountsTowardHandicapCommandHandler(rounds.Object)
            .Handle(new SetTournamentCountsTowardHandicapCommand(1, value, "admin-1"), CancellationToken.None);
        return (rounds, result);
    }

    [Fact]
    public async Task Handle_ScheduledTournament_SavesSetting()
    {
        var round = MakeRound();

        var (rounds, result) = await Run(round, true);

        result.IsSuccess.Should().BeTrue();
        rounds.Verify(r => r.UpdateAsync(It.Is<Round>(x => x.CountsTowardHandicap), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_FinalizedTournament_IsRejected()
    {
        var (rounds, result) = await Run(MakeRound(status: RoundStatus.Finalized), true);

        result.IsSuccess.Should().BeFalse();
        rounds.Verify(r => r.UpdateAsync(It.IsAny<Round>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WeeklyRound_IsRejected()
    {
        var (_, result) = await Run(MakeRound(type: RoundType.NineHole), true);

        result.IsSuccess.Should().BeFalse();
    }
}
