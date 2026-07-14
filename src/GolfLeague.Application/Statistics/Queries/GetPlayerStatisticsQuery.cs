using GolfLeague.Application.Common;
using GolfLeague.Domain.Entities;
using GolfLeague.Domain.Enums;
using GolfLeague.Domain.Interfaces;
using GolfLeague.Domain.Services;
using MediatR;

namespace GolfLeague.Application.Statistics.Queries;

// ── DTOs ─────────────────────────────────────────────────────────────────────

public sealed record ScoringDistributionDto(
    int EagleOrBetterCount,
    int BirdieCount,
    int ParCount,
    int BogeyCount,
    int DoubleBogeyOrWorseCount,
    int TotalHolesPlayed);

public sealed record BestWorstRoundDto(
    int RoundId,
    DateOnly RoundDate,
    string CourseName,
    int? GrossStrokes,
    int? NetStrokes,
    int? GrossStablefordPoints,
    int? NetStablefordPoints);

public sealed record PlayerHoleAverageDto(
    int HoleNumber,
    int Par,
    double AverageGrossStrokes,
    double AverageNetStrokes,
    double AverageScoreToPar,
    int TimesPlayed);

public sealed record StrokesGainedPuttingDto(
    double TotalStrokesGained,
    double PerHoleAverage,
    int HolesWithPuttData,
    double? AveragePuttsPerHole,
    double? FlightAveragePuttsPerHole);

public sealed record PlayerStatisticsDto(
    int PlayerId,
    string PlayerName,
    int TotalRoundsPlayed,
    int TotalRoundsFinalized,
    double? AverageGrossStrokes,
    double? AverageNetStrokes,
    double? AverageGrossStablefordPoints,
    double? AverageNetStablefordPoints,
    double? AverageScoreToPar,
    int? BestGrossStrokes,
    int? WorstGrossStrokes,
    int? BestNetStablefordPoints,
    int? WorstNetStablefordPoints,
    BestWorstRoundDto? BestGrossRound,
    BestWorstRoundDto? BestNetPointsRound,
    ScoringDistributionDto ScoringDistribution,
    List<PlayerHoleAverageDto> HoleAverages,
    double? HandicapTrend,
    int TotalBirdiesOrBetter,
    int TotalPars,
    double? ParOrBetterPercentage,
    StrokesGainedPuttingDto? StrokesGainedPutting);

// ── Query + Handler ──────────────────────────────────────────────────────────

public sealed record GetPlayerStatisticsQuery(int PlayerId) : IRequest<Result<PlayerStatisticsDto>>;

public sealed class GetPlayerStatisticsQueryHandler
    : IRequestHandler<GetPlayerStatisticsQuery, Result<PlayerStatisticsDto>>
{
    private readonly IPlayerRepository _playerRepository;
    private readonly IRoundRepository _roundRepository;
    private readonly IHandicapRepository _handicapRepository;

    public GetPlayerStatisticsQueryHandler(
        IPlayerRepository playerRepository,
        IRoundRepository roundRepository,
        IHandicapRepository handicapRepository)
    {
        _playerRepository = playerRepository;
        _roundRepository = roundRepository;
        _handicapRepository = handicapRepository;
    }

    public async Task<Result<PlayerStatisticsDto>> Handle(
        GetPlayerStatisticsQuery request,
        CancellationToken cancellationToken)
    {
        var player = await _playerRepository.GetByIdAsync(request.PlayerId, cancellationToken);
        if (player is null)
            return Result<PlayerStatisticsDto>.Fail($"Player with ID {request.PlayerId} not found.");

        var allParticipants = await _roundRepository.GetParticipantsAsyncByPlayer(
            request.PlayerId, cancellationToken);

        var totalRounds = allParticipants.Count;
        var finalized = allParticipants
            .Where(p => p.Round?.Status == RoundStatus.Finalized && !p.IsWithdrawn && !p.SkippedWeek)
            .ToList();

        var scored = finalized
            .Where(p => p.TotalGrossStrokes.HasValue)
            .ToList();

        // Gather all hole scores
        var allHoleScores = new List<Domain.Entities.HoleScore>();
        foreach (var p in finalized)
        {
            var scores = await _roundRepository.GetHoleScoresAsync(p.Id, cancellationToken);
            allHoleScores.AddRange(scores);
        }

        // Scoring distribution (gross-based)
        var eagleOrBetter = allHoleScores.Count(s => s.GrossStrokes <= s.Par - 2);
        var birdies = allHoleScores.Count(s => s.GrossStrokes == s.Par - 1);
        var pars = allHoleScores.Count(s => s.GrossStrokes == s.Par);
        var bogeys = allHoleScores.Count(s => s.GrossStrokes == s.Par + 1);
        var doublePlus = allHoleScores.Count(s => s.GrossStrokes >= s.Par + 2);

        var scoringDist = new ScoringDistributionDto(
            eagleOrBetter, birdies, pars, bogeys, doublePlus, allHoleScores.Count);

        // Best/worst rounds
        BestWorstRoundDto? bestGrossRound = null;
        BestWorstRoundDto? bestNetPointsRound = null;

        if (scored.Count > 0)
        {
            var bestGross = scored.OrderBy(p => p.TotalGrossStrokes).First();
            bestGrossRound = new BestWorstRoundDto(
                bestGross.Round.Id, bestGross.Round.RoundDate,
                bestGross.Round.Course?.Name ?? string.Empty,
                bestGross.TotalGrossStrokes, bestGross.TotalNetStrokes,
                bestGross.TotalGrossStablefordPoints, bestGross.TotalNetStablefordPoints);

            var bestNetPts = scored.OrderByDescending(p => p.TotalNetStablefordPoints ?? 0).First();
            bestNetPointsRound = new BestWorstRoundDto(
                bestNetPts.Round.Id, bestNetPts.Round.RoundDate,
                bestNetPts.Round.Course?.Name ?? string.Empty,
                bestNetPts.TotalGrossStrokes, bestNetPts.TotalNetStrokes,
                bestNetPts.TotalGrossStablefordPoints, bestNetPts.TotalNetStablefordPoints);
        }

        // Per-hole averages (across all holes ever played)
        var holeAverages = allHoleScores
            .GroupBy(s => s.HoleNumber)
            .Select(g => new PlayerHoleAverageDto(
                g.Key,
                g.First().Par,
                Math.Round(g.Average(s => s.GrossStrokes), 2),
                Math.Round(g.Average(s => s.NetStrokes), 2),
                Math.Round(g.Average(s => s.GrossStrokes - s.Par), 2),
                g.Count()))
            .OrderBy(h => h.HoleNumber)
            .ToList();

        // Handicap trend — difference between first and latest handicap
        double? handicapTrend = null;
        var handicapHistory = await _handicapRepository.GetHistoryAsync(request.PlayerId, cancellationToken);
        if (handicapHistory.Count >= 2)
        {
            var ordered = handicapHistory.OrderBy(h => h.EffectiveDate).ToList();
            handicapTrend = Math.Round(ordered.Last().HandicapIndex - ordered.First().HandicapIndex, 1);
        }

        var parOrBetter = allHoleScores.Count(s => s.GrossStrokes <= s.Par);
        double? parOrBetterPct = allHoleScores.Count > 0
            ? Math.Round(100.0 * parOrBetter / allHoleScores.Count, 1)
            : null;

        // Strokes Gained: Putting vs flight
        StrokesGainedPuttingDto? sgPutting = null;
        var playerPuttScores = allHoleScores.Where(h => h.Putts.HasValue).ToList();
        if (playerPuttScores.Count > 0)
        {
            // Gather flight hole scores for the same flight(s) this player was in
            var flightIds = finalized.Select(p => p.FlightId).Distinct().ToList();
            var flightHoleScores = new List<HoleScore>();
            foreach (var rp in finalized)
            {
                var roundParticipants = await _roundRepository.GetParticipantsAsync(rp.RoundId, cancellationToken);
                foreach (var otherP in roundParticipants)
                {
                    if (otherP.PlayerId == request.PlayerId) continue;
                    if (!flightIds.Contains(otherP.FlightId)) continue;
                    if (otherP.IsWithdrawn || otherP.SkippedWeek || otherP.IsSubstitute) continue;
                    var otherScores = await _roundRepository.GetHoleScoresAsync(otherP.Id, cancellationToken);
                    flightHoleScores.AddRange(otherScores);
                }
            }

            var sgResult = StrokesGainedPuttingService.Calculate(allHoleScores, flightHoleScores);

            var playerAvgPutts = playerPuttScores.Average(h => h.Putts!.Value);
            var flightPuttScores = flightHoleScores.Where(h => h.Putts.HasValue).ToList();
            double? flightAvgPutts = flightPuttScores.Count > 0
                ? flightPuttScores.Average(h => h.Putts!.Value)
                : null;

            sgPutting = new StrokesGainedPuttingDto(
                Math.Round(sgResult.TotalStrokesGained, 2),
                Math.Round(sgResult.PerHoleAverage, 3),
                sgResult.HolesWithPuttData,
                Math.Round(playerAvgPutts, 2),
                flightAvgPutts.HasValue ? Math.Round(flightAvgPutts.Value, 2) : null);
        }

        var dto = new PlayerStatisticsDto(
            player.Id,
            player.FullName,
            totalRounds,
            finalized.Count,
            scored.Count > 0 ? Math.Round(scored.Average(p => p.TotalGrossStrokes!.Value), 1) : null,
            scored.Count > 0 ? Math.Round(scored.Average(p => p.TotalNetStrokes ?? 0), 1) : null,
            scored.Count > 0 ? Math.Round(scored.Average(p => p.TotalGrossStablefordPoints ?? 0), 1) : null,
            scored.Count > 0 ? Math.Round(scored.Average(p => p.TotalNetStablefordPoints ?? 0), 1) : null,
            scored.Count > 0
                ? Math.Round(scored.Average(p =>
                    p.HoleScores.Count > 0
                        ? p.HoleScores.Sum(h => h.GrossStrokes - h.Par)
                        : 0), 1)
                : null,
            scored.Count > 0 ? scored.Min(p => p.TotalGrossStrokes) : null,
            scored.Count > 0 ? scored.Max(p => p.TotalGrossStrokes) : null,
            scored.Count > 0 ? scored.Max(p => p.TotalNetStablefordPoints) : null,
            scored.Count > 0 ? scored.Min(p => p.TotalNetStablefordPoints) : null,
            bestGrossRound,
            bestNetPointsRound,
            scoringDist,
            holeAverages,
            handicapTrend,
            eagleOrBetter + birdies,
            pars,
            parOrBetterPct,
            sgPutting);

        return Result<PlayerStatisticsDto>.Ok(dto);
    }
}
