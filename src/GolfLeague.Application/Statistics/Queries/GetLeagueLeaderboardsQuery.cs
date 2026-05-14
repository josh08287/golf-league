using GolfLeague.Application.Common;
using GolfLeague.Domain.Enums;
using GolfLeague.Domain.Interfaces;
using MediatR;

namespace GolfLeague.Application.Statistics.Queries;

// ── DTOs ─────────────────────────────────────────────────────────────────────

public sealed record PlayerGrossLeaderboardEntryDto(
    int PlayerId,
    string PlayerName,
    int BestGrossScore,
    string RoundDate,
    string CourseName);

public sealed record PlayerNetLeaderboardEntryDto(
    int PlayerId,
    string PlayerName,
    int BestNetScore,
    string RoundDate,
    string CourseName);

public sealed record PlayerBirdiesEaglesDto(
    int PlayerId,
    string PlayerName,
    int TotalBirdies,
    int TotalEaglesOrBetter,
    int Total);

public sealed record PlayerPar3SkinsDto(
    int PlayerId,
    string PlayerName,
    int TotalSkinsWon,
    int TotalSkinValue);

public sealed record LeagueLeaderboardsDto(
    List<PlayerGrossLeaderboardEntryDto> LowGross,
    List<PlayerNetLeaderboardEntryDto> LowNet,
    List<PlayerBirdiesEaglesDto> BirdiesEagles,
    List<PlayerPar3SkinsDto> Par3Skins);

// ── Query + Handler ──────────────────────────────────────────────────────────

public sealed record GetLeagueLeaderboardsQuery : IRequest<Result<LeagueLeaderboardsDto>>;

public sealed class GetLeagueLeaderboardsQueryHandler
    : IRequestHandler<GetLeagueLeaderboardsQuery, Result<LeagueLeaderboardsDto>>
{
    private readonly IRoundRepository _roundRepository;
    private readonly IPlayerRepository _playerRepository;

    public GetLeagueLeaderboardsQueryHandler(
        IRoundRepository roundRepository,
        IPlayerRepository playerRepository)
    {
        _roundRepository = roundRepository;
        _playerRepository = playerRepository;
    }

    public async Task<Result<LeagueLeaderboardsDto>> Handle(
        GetLeagueLeaderboardsQuery request,
        CancellationToken cancellationToken)
    {
        var allRounds = await _roundRepository.GetAllAsync(cancellationToken);
        var finalizedRounds = allRounds
            .Where(r => r.Status == RoundStatus.Finalized)
            .OrderBy(r => r.RoundDate)
            .ToList();

        // Accumulate per-player data
        var grossByPlayer = new Dictionary<int, (string Name, int Score, string Date, string Course)>();
        var netByPlayer = new Dictionary<int, (string Name, int Score, string Date, string Course)>();
        var birdiesByPlayer = new Dictionary<int, (string Name, int Birdies, int Eagles)>();
        var skinsByPlayer = new Dictionary<int, (string Name, int Count, int Value)>();

        // Par-3 skins carryover accumulates across rounds in chronological order
        int par3Carryover = 0;

        foreach (var round in finalizedRounds)
        {
            var participants = await _roundRepository.GetParticipantsAsync(round.Id, cancellationToken);
            var active = participants
                .Where(p => !p.IsWithdrawn && !p.SkippedWeek)
                .ToList();

            // Load hole scores per participant
            var participantScores = new List<(Domain.Entities.RoundParticipant Participant, List<Domain.Entities.HoleScore> HoleScores)>();
            foreach (var p in active)
            {
                var holeScores = (await _roundRepository.GetHoleScoresAsync(p.Id, cancellationToken)).ToList();
                participantScores.Add((p, holeScores));
            }

            // Low gross / low net
            foreach (var (p, holeScores) in participantScores)
            {
                if (!p.TotalGrossStrokes.HasValue) continue;

                var playerName = p.Player?.FullName ?? string.Empty;
                var dateStr = round.RoundDate.ToString("yyyy-MM-dd");
                var courseName = round.Course?.Name ?? string.Empty;

                // Low gross — keep best (lowest) gross per player
                if (!grossByPlayer.TryGetValue(p.PlayerId, out var currentGross) ||
                    p.TotalGrossStrokes.Value < currentGross.Score)
                {
                    grossByPlayer[p.PlayerId] = (playerName, p.TotalGrossStrokes.Value, dateStr, courseName);
                }

                // Low net — keep best (lowest) net per player
                if (p.TotalNetStrokes.HasValue)
                {
                    if (!netByPlayer.TryGetValue(p.PlayerId, out var currentNet) ||
                        p.TotalNetStrokes.Value < currentNet.Score)
                    {
                        netByPlayer[p.PlayerId] = (playerName, p.TotalNetStrokes.Value, dateStr, courseName);
                    }
                }

                // Birdies + eagles
                var birdies = holeScores.Count(s => s.GrossStrokes == s.Par - 1);
                var eagles = holeScores.Count(s => s.GrossStrokes <= s.Par - 2);
                if (!birdiesByPlayer.TryGetValue(p.PlayerId, out var current))
                    birdiesByPlayer[p.PlayerId] = (playerName, birdies, eagles);
                else
                    birdiesByPlayer[p.PlayerId] = (playerName, current.Birdies + birdies, current.Eagles + eagles);
            }

            // Par-3 gross skins for this round
            var roundSkins = CalculatePar3Skins(participantScores, par3Carryover);
            par3Carryover = roundSkins.EndingCarryover;

            foreach (var (playerId, playerName, skinsWon, skinValue) in roundSkins.PlayerTotals)
            {
                if (!skinsByPlayer.TryGetValue(playerId, out var cs))
                    skinsByPlayer[playerId] = (playerName, skinsWon, skinValue);
                else
                    skinsByPlayer[playerId] = (playerName, cs.Count + skinsWon, cs.Value + skinValue);
            }
        }

        var lowGross = grossByPlayer
            .Select(kv => new PlayerGrossLeaderboardEntryDto(
                kv.Key, kv.Value.Name, kv.Value.Score, kv.Value.Date, kv.Value.Course))
            .OrderBy(x => x.BestGrossScore)
            .ThenBy(x => x.PlayerName)
            .ToList();

        var lowNet = netByPlayer
            .Select(kv => new PlayerNetLeaderboardEntryDto(
                kv.Key, kv.Value.Name, kv.Value.Score, kv.Value.Date, kv.Value.Course))
            .OrderBy(x => x.BestNetScore)
            .ThenBy(x => x.PlayerName)
            .ToList();

        var birdiesEagles = birdiesByPlayer
            .Select(kv => new PlayerBirdiesEaglesDto(
                kv.Key, kv.Value.Name, kv.Value.Birdies, kv.Value.Eagles, kv.Value.Birdies + kv.Value.Eagles))
            .OrderByDescending(x => x.Total)
            .ThenBy(x => x.PlayerName)
            .ToList();

        var par3Skins = skinsByPlayer
            .Select(kv => new PlayerPar3SkinsDto(kv.Key, kv.Value.Name, kv.Value.Count, kv.Value.Value))
            .OrderByDescending(x => x.TotalSkinValue)
            .ThenByDescending(x => x.TotalSkinsWon)
            .ThenBy(x => x.PlayerName)
            .ToList();

        return Result<LeagueLeaderboardsDto>.Ok(new LeagueLeaderboardsDto(lowGross, lowNet, birdiesEagles, par3Skins));
    }

    private static Par3RoundResult CalculatePar3Skins(
        List<(Domain.Entities.RoundParticipant Participant, List<Domain.Entities.HoleScore> HoleScores)> participantScores,
        int incomingCarryover)
    {
        var par3Entries = participantScores
            .SelectMany(ps => ps.HoleScores
                .Where(h => h.Par == 3)
                .Select(h => new
                {
                    ps.Participant.PlayerId,
                    PlayerName = ps.Participant.Player?.FullName ?? string.Empty,
                    h.HoleNumber,
                    h.GrossStrokes,
                }))
            .ToList();

        if (par3Entries.Count == 0)
            return new Par3RoundResult([], incomingCarryover);

        var holesPlayed = par3Entries.Select(x => x.HoleNumber).Distinct().OrderBy(h => h).ToList();
        var playerTotals = new Dictionary<int, (string Name, int Count, int Value)>();
        int carryover = incomingCarryover;

        foreach (var holeNumber in holesPlayed)
        {
            var holeScores = par3Entries.Where(x => x.HoleNumber == holeNumber).ToList();
            var minScore = holeScores.Min(x => x.GrossStrokes);
            var winners = holeScores.Where(x => x.GrossStrokes == minScore).ToList();
            int skinValue = 1 + carryover;

            if (winners.Count == 1)
            {
                var w = winners[0];
                if (!playerTotals.TryGetValue(w.PlayerId, out var pt))
                    playerTotals[w.PlayerId] = (w.PlayerName, 1, skinValue);
                else
                    playerTotals[w.PlayerId] = (w.PlayerName, pt.Count + 1, pt.Value + skinValue);
                carryover = 0;
            }
            else
            {
                carryover += 1;
            }
        }

        var resultList = playerTotals
            .Select(kv => (kv.Key, kv.Value.Name, kv.Value.Count, kv.Value.Value))
            .ToList();

        return new Par3RoundResult(resultList, carryover);
    }

    private sealed record Par3RoundResult(
        List<(int PlayerId, string PlayerName, int SkinsWon, int SkinValue)> PlayerTotals,
        int EndingCarryover);
}
