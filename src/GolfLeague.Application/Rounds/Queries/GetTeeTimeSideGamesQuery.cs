using GolfLeague.Application.Common;
using GolfLeague.Domain.Entities;
using GolfLeague.Domain.Enums;
using GolfLeague.Domain.Interfaces;
using GolfLeague.Domain.Services;
using MediatR;

namespace GolfLeague.Application.Rounds.Queries;

public sealed record SideGameTeamDto(int TeamNumber, List<int> ParticipantIds, List<string> PlayerNames);

/// <summary>One head-to-head match's Nassau standing (front/back/overall), for individual Nassau this is one pair; for team Nassau there's exactly one.</summary>
public sealed record NassauMatchDto(
    string SideAName,
    string SideBName,
    string FrontStatus,
    string BackStatus,
    string OverallStatus);

public sealed record NassauStatusDto(List<NassauMatchDto> Matches);

public sealed record BestBallStatusDto(
    string TeamAName,
    string TeamBName,
    int TeamAHolesWon,
    int TeamBHolesWon,
    int HolesHalved,
    string Status);

/// <summary>
/// One side game a tee-time group has opted into, with its live-computed
/// status. Status is always derived from currently-entered HoleScore rows,
/// never persisted, so it's always in sync with the scorecard.
/// </summary>
public sealed record TeeTimeSideGameDto(
    int Id,
    SideGameType GameType,
    ScoringBasis ScoringBasis,
    NassauFormat? NassauFormat,
    List<SideGameTeamDto> Teams,
    int HolesEntered,
    int TotalHoles,
    NassauStatusDto? Nassau,
    BestBallStatusDto? BestBall);

public sealed record TeeTimeSideGamesDto(
    int TeeTimeId,
    List<SideGameType> EligibleGameTypes,
    List<TeeTimeSideGameDto> ConfiguredGames);

public sealed record GetTeeTimeSideGamesQuery(int TeeTimeId) : IRequest<Result<TeeTimeSideGamesDto>>;

public sealed class GetTeeTimeSideGamesQueryHandler
    : IRequestHandler<GetTeeTimeSideGamesQuery, Result<TeeTimeSideGamesDto>>
{
    private readonly ITeeTimeRepository _teeTimeRepository;
    private readonly IRoundRepository _roundRepository;
    private readonly ITeeTimeSideGameRepository _sideGameRepository;

    public GetTeeTimeSideGamesQueryHandler(
        ITeeTimeRepository teeTimeRepository,
        IRoundRepository roundRepository,
        ITeeTimeSideGameRepository sideGameRepository)
    {
        _teeTimeRepository = teeTimeRepository;
        _roundRepository = roundRepository;
        _sideGameRepository = sideGameRepository;
    }

    public async Task<Result<TeeTimeSideGamesDto>> Handle(GetTeeTimeSideGamesQuery request, CancellationToken cancellationToken)
    {
        var teeTime = await _teeTimeRepository.GetByIdAsync(request.TeeTimeId, cancellationToken);
        if (teeTime is null)
            return Result<TeeTimeSideGamesDto>.Fail($"Tee time {request.TeeTimeId} not found.");

        var round = await _roundRepository.GetByIdAsync(teeTime.RoundId, cancellationToken);
        if (round is null)
            return Result<TeeTimeSideGamesDto>.Fail($"Round for tee time {request.TeeTimeId} not found.");

        var holeCount = round.NineHoleSide == NineHoleSide.NotApplicable ? 18 : 9;
        var eligible = SideGameEligibility.EligibleGames(holeCount).ToList();

        var configured = await _sideGameRepository.GetForTeeTimeAsync(request.TeeTimeId, cancellationToken);

        var activeParticipants = teeTime.Participants.Where(p => !p.IsWithdrawn && !p.SkippedWeek).ToList();
        var participantsById = activeParticipants.ToDictionary(p => p.Id);

        var dtos = configured
            .Select(g => BuildDto(g, activeParticipants, participantsById, holeCount))
            .ToList();

        return Result<TeeTimeSideGamesDto>.Ok(new TeeTimeSideGamesDto(request.TeeTimeId, eligible, dtos));
    }

    private static TeeTimeSideGameDto BuildDto(
        TeeTimeSideGame game,
        List<RoundParticipant> activeParticipants,
        Dictionary<int, RoundParticipant> participantsById,
        int holeCount)
    {
        var teamDtos = game.Teams
            .GroupBy(t => t.TeamNumber)
            .OrderBy(g => g.Key)
            .Select(g => new SideGameTeamDto(
                g.Key,
                g.Select(t => t.ParticipantId).ToList(),
                g.Select(t => participantsById.TryGetValue(t.ParticipantId, out var p) ? p.Player.FullName : "Unknown").ToList()))
            .ToList();

        int StrokesFor(RoundParticipant p, int holeNumber, ScoringBasis basis)
        {
            var hole = p.HoleScores.FirstOrDefault(h => h.HoleNumber == holeNumber);
            if (hole is null) return int.MinValue;
            return basis == ScoringBasis.Gross ? hole.GrossStrokes : hole.NetStrokes;
        }

        int holesEntered = activeParticipants
            .SelectMany(p => p.HoleScores)
            .Select(h => h.HoleNumber)
            .Distinct()
            .Count();

        NassauStatusDto? nassauStatus = null;
        BestBallStatusDto? bestBallStatus = null;

        if (game.GameType == SideGameType.Nassau)
        {
            var matches = new List<NassauMatchDto>();

            if (game.NassauFormat == NassauFormat.TeamVsTeam && teamDtos.Count == 2)
            {
                var teamA = teamDtos[0].ParticipantIds.Select(id => participantsById[id]).ToList();
                var teamB = teamDtos[1].ParticipantIds.Select(id => participantsById[id]).ToList();
                matches.Add(BuildNassauMatch(
                    string.Join(" & ", teamDtos[0].PlayerNames),
                    string.Join(" & ", teamDtos[1].PlayerNames),
                    holeNumber => NassauScoringService.BestBallStrokes(teamA.Select(p => StrokesFor(p, holeNumber, game.ScoringBasis)).Where(s => s != int.MinValue).DefaultIfEmpty(int.MinValue)),
                    holeNumber => NassauScoringService.BestBallStrokes(teamB.Select(p => StrokesFor(p, holeNumber, game.ScoringBasis)).Where(s => s != int.MinValue).DefaultIfEmpty(int.MinValue)),
                    holeCount));
            }
            else
            {
                // Individual Nassau: every pairwise combination of active players.
                for (int i = 0; i < activeParticipants.Count; i++)
                {
                    for (int j = i + 1; j < activeParticipants.Count; j++)
                    {
                        var pa = activeParticipants[i];
                        var pb = activeParticipants[j];
                        matches.Add(BuildNassauMatch(
                            pa.Player.FullName,
                            pb.Player.FullName,
                            holeNumber => StrokesFor(pa, holeNumber, game.ScoringBasis),
                            holeNumber => StrokesFor(pb, holeNumber, game.ScoringBasis),
                            holeCount));
                    }
                }
            }

            nassauStatus = new NassauStatusDto(matches);
        }
        else if (game.GameType == SideGameType.TwoVTwoBestBall && teamDtos.Count == 2)
        {
            var teamA = teamDtos[0].ParticipantIds.Select(id => participantsById[id]).ToList();
            var teamB = teamDtos[1].ParticipantIds.Select(id => participantsById[id]).ToList();

            var holeResults = new List<BestBallScoringService.HoleResult>();
            for (int holeNumber = 1; holeNumber <= 18; holeNumber++)
            {
                var aStrokes = teamA.Select(p => StrokesFor(p, holeNumber, ScoringBasis.Net)).Where(s => s != int.MinValue).ToList();
                var bStrokes = teamB.Select(p => StrokesFor(p, holeNumber, ScoringBasis.Net)).Where(s => s != int.MinValue).ToList();
                if (aStrokes.Count == 0 || bStrokes.Count == 0) continue;

                holeResults.Add(BestBallScoringService.ScoreHole(holeNumber, aStrokes, bStrokes));
            }

            var teamAName = string.Join(" & ", teamDtos[0].PlayerNames);
            var teamBName = string.Join(" & ", teamDtos[1].PlayerNames);

            var status = BestBallScoringService.Summarize(holeResults, holeCount);
            var statusText = status.HolesRemaining == 0
                ? DescribeFinalMatch(status.TeamAHolesWon, status.TeamBHolesWon, teamAName, teamBName)
                : DescribeRunningMatch(status.TeamAHolesWon, status.TeamBHolesWon, holeCount - status.HolesRemaining, teamAName, teamBName);

            bestBallStatus = new BestBallStatusDto(
                teamAName,
                teamBName,
                status.TeamAHolesWon,
                status.TeamBHolesWon,
                status.HolesHalved,
                statusText);
        }

        return new TeeTimeSideGameDto(
            game.Id,
            game.GameType,
            game.ScoringBasis,
            game.NassauFormat,
            teamDtos,
            holesEntered,
            holeCount,
            nassauStatus,
            bestBallStatus);
    }

    private static NassauMatchDto BuildNassauMatch(
        string sideAName,
        string sideBName,
        Func<int, int> sideAStrokes,
        Func<int, int> sideBStrokes,
        int holeCount)
    {
        var holes = new List<NassauScoringService.HoleResult>();
        for (int holeNumber = 1; holeNumber <= 18; holeNumber++)
        {
            var a = sideAStrokes(holeNumber);
            var b = sideBStrokes(holeNumber);
            if (a == int.MinValue || b == int.MinValue) continue;
            holes.Add(new NassauScoringService.HoleResult(holeNumber, a, b));
        }

        var result = NassauScoringService.Score(holes);

        return new NassauMatchDto(
            sideAName,
            sideBName,
            DescribeSegment(result.Front, 9, sideAName, sideBName),
            DescribeSegment(result.Back, 9, sideAName, sideBName),
            DescribeSegment(result.Overall, 18, sideAName, sideBName));
    }

    private static string DescribeSegment(NassauScoringService.SegmentResult segment, int totalHolesInSegment, string sideAName, string sideBName)
    {
        var holesPlayed = totalHolesInSegment - segment.HolesRemaining;
        if (holesPlayed == 0)
            return "Not started";

        var suffix = segment.HolesRemaining == 0 ? " (final)" : $" thru {holesPlayed}";

        return segment.Leader switch
        {
            NassauScoringService.Leader.Tied => $"All square{suffix}",
            NassauScoringService.Leader.SideA => $"{sideAName} {segment.HolesUp}up{suffix}",
            NassauScoringService.Leader.SideB => $"{sideBName} {segment.HolesUp}up{suffix}",
            _ => "Not started",
        };
    }

    private static string DescribeRunningMatch(int aWon, int bWon, int holesPlayed, string teamAName, string teamBName)
    {
        if (aWon == 0 && bWon == 0) return "Not started";
        if (aWon == bWon) return $"All square thru {holesPlayed}";
        return aWon > bWon
            ? $"{teamAName} leads {aWon - bWon}up thru {holesPlayed}"
            : $"{teamBName} leads {bWon - aWon}up thru {holesPlayed}";
    }

    private static string DescribeFinalMatch(int aWon, int bWon, string teamAName, string teamBName)
    {
        if (aWon == bWon) return "Match halved";
        return aWon > bWon ? $"{teamAName} wins {aWon - bWon}up" : $"{teamBName} wins {bWon - aWon}up";
    }
}
