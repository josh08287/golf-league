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

public sealed record BbbPlayerTallyDto(int ParticipantId, string PlayerName, int Points);

public sealed record BbbHolePickDto(int HoleNumber, BbbHonor Honor, int? WinnerParticipantId, string? WinnerPlayerName);

public sealed record BbbStatusDto(List<BbbPlayerTallyDto> Standings, List<BbbHolePickDto> Picks);

public sealed record WolfPlayerTallyDto(int ParticipantId, string PlayerName, int Points);

public sealed record WolfHolePickStatusDto(
    int HoleNumber,
    int WolfParticipantId,
    string WolfPlayerName,
    bool IsLoneWolf,
    bool IsBlindWolf,
    int? PartnerParticipantId,
    string? PartnerPlayerName,
    string? Outcome);

public sealed record WolfStatusDto(
    List<int> RotationParticipantIds,
    List<string> RotationPlayerNames,
    int NextWolfParticipantId,
    string NextWolfPlayerName,
    int NextHoleNumber,
    List<WolfPlayerTallyDto> Standings,
    List<WolfHolePickStatusDto> Picks);

/// <summary>
/// One side game a tee-time group has opted into, with its live-computed
/// status. Status is always derived from currently-entered HoleScore rows
/// (plus any recorded BBB/Wolf picks), never persisted beyond the raw
/// picks, so it's always in sync with the scorecard.
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
    BestBallStatusDto? BestBall,
    BbbStatusDto? Bbb,
    WolfStatusDto? Wolf);

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
        BbbStatusDto? bbbStatus = null;
        WolfStatusDto? wolfStatus = null;

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
        else if (game.GameType == SideGameType.BingoBangoBongo)
        {
            bbbStatus = BuildBbbStatus(game, activeParticipants, participantsById);
        }
        else if (game.GameType == SideGameType.Wolf)
        {
            wolfStatus = BuildWolfStatus(game, activeParticipants, participantsById, holeCount);
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
            bestBallStatus,
            bbbStatus,
            wolfStatus);
    }

    private static BbbStatusDto BuildBbbStatus(
        TeeTimeSideGame game,
        List<RoundParticipant> activeParticipants,
        Dictionary<int, RoundParticipant> participantsById)
    {
        var picks = game.HolePicks
            .Select(p => new BingoBangoBongoScoringService.HolePick(p.HoleNumber, p.Honor, p.WinnerParticipantId))
            .ToList();

        var tally = BingoBangoBongoScoringService.Tally(picks);
        var tallyByParticipant = tally.ToDictionary(t => t.ParticipantId, t => t.Points);

        var standings = activeParticipants
            .Select(p => new BbbPlayerTallyDto(p.Id, p.Player.FullName, tallyByParticipant.GetValueOrDefault(p.Id)))
            .OrderByDescending(s => s.Points)
            .ToList();

        var pickDtos = game.HolePicks
            .OrderBy(p => p.HoleNumber).ThenBy(p => p.Honor)
            .Select(p => new BbbHolePickDto(
                p.HoleNumber,
                p.Honor,
                p.WinnerParticipantId,
                p.WinnerParticipantId.HasValue && participantsById.TryGetValue(p.WinnerParticipantId.Value, out var w) ? w.Player.FullName : null))
            .ToList();

        return new BbbStatusDto(standings, pickDtos);
    }

    private static WolfStatusDto BuildWolfStatus(
        TeeTimeSideGame game,
        List<RoundParticipant> activeParticipants,
        Dictionary<int, RoundParticipant> participantsById,
        int holeCount)
    {
        var rotation = game.Teams.OrderBy(t => t.TeamNumber).Select(t => t.ParticipantId).ToList();
        var rotationNames = rotation.Select(id => participantsById.TryGetValue(id, out var p) ? p.Player.FullName : "Unknown").ToList();

        var picksByHole = game.WolfPicks.ToDictionary(p => p.HoleNumber);

        var outcomes = new List<WolfScoringService.HoleOutcome>();
        var pickDtos = new List<WolfHolePickStatusDto>();
        var activeParticipantIds = activeParticipants.Select(p => p.Id).ToList();

        for (int holeNumber = 1; holeNumber <= holeCount; holeNumber++)
        {
            if (!picksByHole.TryGetValue(holeNumber, out var pick))
                continue;

            var netStrokesByParticipant = activeParticipants
                .Select(p => (p.Id, Hole: p.HoleScores.FirstOrDefault(h => h.HoleNumber == holeNumber)))
                .Where(x => x.Hole is not null)
                .ToDictionary(x => x.Id, x => x.Hole!.NetStrokes);

            var scoringPick = new WolfScoringService.HolePick(holeNumber, pick.WolfParticipantId, pick.IsLoneWolf, pick.IsBlindWolf, pick.PartnerParticipantId);
            var outcome = WolfScoringService.ScoreHole(scoringPick, netStrokesByParticipant, activeParticipantIds);
            outcomes.Add(outcome);

            string? outcomeText = netStrokesByParticipant.Count == 0 || outcome.Winner == BestBallScoringService.HoleWinner.Halved
                ? (netStrokesByParticipant.Count == 0 ? null : "Halved")
                : outcome.IsBlindWolf
                    ? (outcome.Winner == BestBallScoringService.HoleWinner.TeamA
                        ? $"Blind wolf won (+{outcome.PointsAwarded} to the Wolf)"
                        : $"Blind wolf lost (+{WolfScoringService.BlindWolfLossPointsPerOpponent} to each of the other {outcome.OtherSideParticipantIds.Count})")
                    : outcome.Winner == BestBallScoringService.HoleWinner.TeamA
                        ? $"Wolf side won (+{outcome.PointsAwarded})"
                        : $"Other side won (+{outcome.PointsAwarded})";

            pickDtos.Add(new WolfHolePickStatusDto(
                holeNumber,
                pick.WolfParticipantId,
                participantsById.TryGetValue(pick.WolfParticipantId, out var wolfP) ? wolfP.Player.FullName : "Unknown",
                pick.IsLoneWolf,
                pick.IsBlindWolf,
                pick.PartnerParticipantId,
                pick.PartnerParticipantId.HasValue && participantsById.TryGetValue(pick.PartnerParticipantId.Value, out var partnerP) ? partnerP.Player.FullName : null,
                outcomeText));
        }

        var tally = WolfScoringService.Tally(outcomes);
        var tallyByParticipant = tally.ToDictionary(t => t.ParticipantId, t => t.Points);
        var standings = activeParticipants
            .Select(p => new WolfPlayerTallyDto(p.Id, p.Player.FullName, tallyByParticipant.GetValueOrDefault(p.Id)))
            .OrderByDescending(s => s.Points)
            .ToList();

        var nextHoleNumber = Enumerable.Range(1, holeCount).FirstOrDefault(h => !picksByHole.ContainsKey(h), holeCount + 1);
        var nextWolfParticipantId = rotation.Count > 0 && nextHoleNumber <= holeCount ? rotation[(nextHoleNumber - 1) % rotation.Count] : 0;
        var nextWolfName = participantsById.TryGetValue(nextWolfParticipantId, out var nextWolf) ? nextWolf.Player.FullName : "Unknown";

        return new WolfStatusDto(rotation, rotationNames, nextWolfParticipantId, nextWolfName, nextHoleNumber, standings, pickDtos);
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
