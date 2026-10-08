using FluentAssertions;
using GolfLeague.Application.Rounds.Commands;
using GolfLeague.Application.Rounds.Queries;
using GolfLeague.Domain.Entities;
using GolfLeague.Domain.Enums;
using GolfLeague.Domain.Interfaces;
using Moq;
using Xunit;

namespace GolfLeague.Tests.Application;

/// <summary>
/// Tournament skins exclude substitutes unless the round's
/// SubstitutesCanWinSkins setting is on.
/// </summary>
public class TournamentSubstituteSkinsTests
{
    private static RoundParticipant MakeParticipant(int playerId, int holeOneGross, bool isSubstitute = false) => new()
    {
        Id = playerId,
        PlayerId = playerId,
        IsSubstitute = isSubstitute,
        Player = new Player { Id = playerId, FirstName = $"Player{playerId}", LastName = "P" },
        HoleScores =
        [
            new HoleScore { ParticipantId = playerId, HoleNumber = 1, Par = 4, GrossStrokes = holeOneGross, NetStrokes = holeOneGross },
        ],
    };

    private static async Task<TournamentResultsDto> GetResults(bool substitutesCanWinSkins)
    {
        var round = new Round
        {
            Id = 1,
            CourseId = 1,
            RoundType = RoundType.Tournament,
            RoundDate = new DateOnly(2026, 10, 1),
            SubstitutesCanWinSkins = substitutesCanWinSkins,
            Participants = [],
        };

        // Player 3 is a substitute with the outright best score on hole 1;
        // players 1 and 2 tie for the best non-substitute score.
        var participants = new List<RoundParticipant>
        {
            MakeParticipant(1, holeOneGross: 4),
            MakeParticipant(2, holeOneGross: 4),
            MakeParticipant(3, holeOneGross: 3, isSubstitute: true),
        };

        var rounds = new Mock<IRoundRepository>();
        rounds.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(round);
        rounds.Setup(r => r.GetParticipantsAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(participants);
        rounds.Setup(r => r.GetTournamentMatchupsAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(new List<TournamentMatchup>());
        rounds.Setup(r => r.GetTournamentHoleExtrasAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(new List<TournamentHoleExtra>());
        rounds.Setup(r => r.GetTournamentFlightsAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(new List<TournamentFlight>());
        rounds.Setup(r => r.GetLongestDriveWinnersAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(new List<TournamentLongestDriveWinner>());

        var courses = new Mock<ICourseRepository>();
        courses.Setup(c => c.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(new Course { Id = 1, Name = "Test Course" });
        courses.Setup(c => c.GetHolesAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<CourseHole> { new() { HoleNumber = 1, Par = 4, StrokeIndex = 1 } });

        var result = await new GetTournamentResultsQueryHandler(rounds.Object, courses.Object)
            .Handle(new GetTournamentResultsQuery(1), CancellationToken.None);
        result.IsSuccess.Should().BeTrue();
        return result.Value!;
    }

    [Fact]
    public async Task Skins_SubstitutesNotEligible_ExcludesSubstitute()
    {
        var results = await GetResults(substitutesCanWinSkins: false);

        // With the substitute excluded, players 1 and 2 tie — no gross skin.
        results.GrossSkins.HoleResults.Single().IsTie.Should().BeTrue();
        results.GrossSkins.PlayerSummaries.Should().BeEmpty();
    }

    [Fact]
    public async Task Skins_SubstitutesEligible_SubstituteCanWin()
    {
        var results = await GetResults(substitutesCanWinSkins: true);

        results.GrossSkins.HoleResults.Single().WinnerPlayerId.Should().Be(3);
        results.GrossSkins.PlayerSummaries.Should().ContainSingle(s => s.PlayerId == 3);
    }

    [Fact]
    public async Task Rankings_ExcludeSubstitutes_EvenWhenTheyCanWinSkins()
    {
        var results = await GetResults(substitutesCanWinSkins: true);

        results.GrossStrokeRanking.Should().NotContain(e => e.PlayerId == 3);
    }

    [Fact]
    public async Task SetSubstituteSkins_FinalizedRound_IsRejected()
    {
        var rounds = new Mock<IRoundRepository>();
        rounds.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Round { Id = 1, RoundType = RoundType.Tournament, Status = RoundStatus.Finalized, Participants = [] });

        var result = await new SetTournamentSubstituteSkinsCommandHandler(rounds.Object)
            .Handle(new SetTournamentSubstituteSkinsCommand(1, true, "admin-1"), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        rounds.Verify(r => r.UpdateAsync(It.IsAny<Round>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
