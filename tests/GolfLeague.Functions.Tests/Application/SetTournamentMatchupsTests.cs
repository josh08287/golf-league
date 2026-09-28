using FluentAssertions;
using GolfLeague.Application.Rounds.Commands;
using GolfLeague.Domain.Entities;
using GolfLeague.Domain.Enums;
using GolfLeague.Domain.Interfaces;
using Moq;
using Xunit;

namespace GolfLeague.Tests.Application;

/// <summary>
/// SetTournamentMatchupsCommand is the admin's manual matchup editor
/// (ManageTournamentPage) — it replaces the round's whole matchup list with
/// exactly what's submitted, including a "bye" row (Player2Id null) so a
/// player who was left unmatched by "regenerate from handicaps" doesn't get
/// silently dropped the next time the admin saves manual edits.
/// </summary>
public class SetTournamentMatchupsTests
{
    private static Round MakeRound(RoundStatus status = RoundStatus.Scheduled) =>
        new() { Id = 1, RoundType = RoundType.Tournament, Status = status };

    private static RoundParticipant MakeParticipant(int id, double handicapIndex = 10) => new()
    {
        Id = id,
        PlayerId = id,
        RoundId = 1,
        HandicapIndex = handicapIndex,
        CourseHandicap = (int)handicapIndex,
        Player = new Player { Id = id, FirstName = "P", LastName = id.ToString() },
    };

    private static (SetTournamentMatchupsCommandHandler Handler, Mock<IRoundRepository> Rounds) BuildSut(
        Round round, params RoundParticipant[] participants)
    {
        var rounds = new Mock<IRoundRepository>();
        rounds.Setup(r => r.GetByIdAsync(round.Id, It.IsAny<CancellationToken>())).ReturnsAsync(round);
        rounds.Setup(r => r.GetParticipantsAsync(round.Id, It.IsAny<CancellationToken>())).ReturnsAsync(participants.ToList());
        rounds.Setup(r => r.ReplaceTournamentMatchupsAsync(round.Id, It.IsAny<IEnumerable<TournamentMatchup>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var players = new Mock<IPlayerRepository>();
        var handicaps = new Mock<IHandicapRepository>();
        var handler = new SetTournamentMatchupsCommandHandler(rounds.Object, players.Object, handicaps.Object);
        return (handler, rounds);
    }

    [Fact]
    public async Task Handle_BuildsTwoPlayerMatchup()
    {
        var round = MakeRound();
        var p1 = MakeParticipant(1);
        var p2 = MakeParticipant(2);
        var (handler, _) = BuildSut(round, p1, p2);

        var result = await handler.Handle(
            new SetTournamentMatchupsCommand(1, [new MatchupInput(1, 2)], "admin1"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var m = result.Value!.Single();
        m.Player1Id.Should().Be(1);
        m.Player2Id.Should().Be(2);
    }

    [Fact]
    public async Task Handle_NullPlayer2Id_CreatesByeMatchup()
    {
        var round = MakeRound();
        var p1 = MakeParticipant(1);
        var (handler, rounds) = BuildSut(round, p1);

        var result = await handler.Handle(
            new SetTournamentMatchupsCommand(1, [new MatchupInput(1, null)], "admin1"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var m = result.Value!.Single();
        m.Player1Id.Should().Be(1);
        m.Player1Name.Should().Be("P 1");
        m.Player2Id.Should().BeNull();
        m.Player2Name.Should().BeNull();

        rounds.Verify(r => r.ReplaceTournamentMatchupsAsync(
            1,
            It.Is<IEnumerable<TournamentMatchup>>(ms => ms.Single().Player1Id == 1 && ms.Single().Player2Id == null),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_MixOfPairsAndBye_PreservesOrderAndNumbering()
    {
        var round = MakeRound();
        var p1 = MakeParticipant(1);
        var p2 = MakeParticipant(2);
        var p3 = MakeParticipant(3);
        var (handler, _) = BuildSut(round, p1, p2, p3);

        var result = await handler.Handle(
            new SetTournamentMatchupsCommand(1, [new MatchupInput(1, 2), new MatchupInput(3, null)], "admin1"),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(2);
        result.Value![0].MatchupNumber.Should().Be(1);
        result.Value[0].Player2Id.Should().Be(2);
        result.Value[1].MatchupNumber.Should().Be(2);
        result.Value[1].Player1Id.Should().Be(3);
        result.Value[1].Player2Id.Should().BeNull();
    }

    [Fact]
    public async Task Handle_Player1NotAParticipant_ReturnsFail()
    {
        var round = MakeRound();
        var p1 = MakeParticipant(1);
        var (handler, _) = BuildSut(round, p1);

        var result = await handler.Handle(
            new SetTournamentMatchupsCommand(1, [new MatchupInput(99, null)], "admin1"), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_Player2NotAParticipant_ReturnsFail()
    {
        var round = MakeRound();
        var p1 = MakeParticipant(1);
        var (handler, _) = BuildSut(round, p1);

        var result = await handler.Handle(
            new SetTournamentMatchupsCommand(1, [new MatchupInput(1, 99)], "admin1"), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_FinalizedRound_ReturnsFail()
    {
        var round = MakeRound(RoundStatus.Finalized);
        var p1 = MakeParticipant(1);
        var (handler, _) = BuildSut(round, p1);

        var result = await handler.Handle(
            new SetTournamentMatchupsCommand(1, [new MatchupInput(1, null)], "admin1"), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
    }
}
