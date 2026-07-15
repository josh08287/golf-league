using GolfLeague.Domain.Enums;
using GolfLeague.Application.Common;
using GolfLeague.Domain.Interfaces;
using MediatR;

namespace GolfLeague.Application.Statistics.Queries;

// ── DTOs ─────────────────────────────────────────────────────────────────────

public sealed record GroupStatisticsDto(
    string GroupName,
    int PlayerCount,
    int TotalRoundsPlayed,
    double? AverageGrossStrokes,
    double? AverageNetStrokes,
    double? AverageGrossStablefordPoints,
    double? AverageNetStablefordPoints,
    double? AverageScoreToPar,
    int EagleOrBetterCount,
    int BirdieCount,
    int ParCount,
    int BogeyCount,
    int DoubleBogeyOrWorseCount,
    double? ParOrBetterPercentage);

public sealed record JoesVsOthersStatisticsDto(
    GroupStatisticsDto Joes,
    GroupStatisticsDto Others);

// ── Query + Handler ──────────────────────────────────────────────────────────

public sealed record GetJoesVsOthersStatisticsQuery(int? SeasonId = null, int? HalfId = null)
    : IRequest<Result<JoesVsOthersStatisticsDto>>;

public sealed class GetJoesVsOthersStatisticsQueryHandler
    : IRequestHandler<GetJoesVsOthersStatisticsQuery, Result<JoesVsOthersStatisticsDto>>
{
    private static readonly string[] JoeFirstNames = ["joe", "joseph"];

    private readonly IRoundRepository _roundRepository;

    public GetJoesVsOthersStatisticsQueryHandler(IRoundRepository roundRepository)
    {
        _roundRepository = roundRepository;
    }

    public async Task<Result<JoesVsOthersStatisticsDto>> Handle(
        GetJoesVsOthersStatisticsQuery request,
        CancellationToken cancellationToken)
    {
        var allRounds = request.HalfId is int halfId
            ? await _roundRepository.GetByHalfAsync(halfId, cancellationToken)
            : request.SeasonId is int seasonId
                ? await _roundRepository.GetBySeasonAsync(seasonId, cancellationToken)
                : await _roundRepository.GetAllAsync(cancellationToken);

        var finalizedRounds = allRounds.Where(r => r.Status == RoundStatus.Finalized).ToList();

        var joeParticipants = new List<Domain.Entities.RoundParticipant>();
        var otherParticipants = new List<Domain.Entities.RoundParticipant>();
        var joeHoleScores = new List<Domain.Entities.HoleScore>();
        var otherHoleScores = new List<Domain.Entities.HoleScore>();

        foreach (var round in finalizedRounds)
        {
            var participants = await _roundRepository.GetParticipantsAsync(round.Id, cancellationToken);
            foreach (var p in participants)
            {
                if (p.IsWithdrawn || p.SkippedWeek) continue;

                var isJoe = JoeFirstNames.Contains(p.Player.FirstName.Trim(), StringComparer.OrdinalIgnoreCase);
                var scores = await _roundRepository.GetHoleScoresAsync(p.Id, cancellationToken);

                if (isJoe)
                {
                    joeParticipants.Add(p);
                    joeHoleScores.AddRange(scores);
                }
                else
                {
                    otherParticipants.Add(p);
                    otherHoleScores.AddRange(scores);
                }
            }
        }

        var joes = BuildGroupStatistics("Joes", joeParticipants, joeHoleScores);
        var others = BuildGroupStatistics("Non-Joes", otherParticipants, otherHoleScores);

        return Result<JoesVsOthersStatisticsDto>.Ok(new JoesVsOthersStatisticsDto(joes, others));
    }

    private static GroupStatisticsDto BuildGroupStatistics(
        string groupName,
        List<Domain.Entities.RoundParticipant> participants,
        List<Domain.Entities.HoleScore> holeScores)
    {
        var playerCount = participants.Select(p => p.PlayerId).Distinct().Count();
        var scored = participants.Where(p => p.TotalGrossStrokes.HasValue).ToList();

        var eagleOrBetter = holeScores.Count(s => s.GrossStrokes <= s.Par - 2);
        var birdies = holeScores.Count(s => s.GrossStrokes == s.Par - 1);
        var pars = holeScores.Count(s => s.GrossStrokes == s.Par);
        var bogeys = holeScores.Count(s => s.GrossStrokes == s.Par + 1);
        var doublePlus = holeScores.Count(s => s.GrossStrokes >= s.Par + 2);

        var parOrBetter = holeScores.Count(s => s.GrossStrokes <= s.Par);

        return new GroupStatisticsDto(
            groupName,
            playerCount,
            participants.Count,
            scored.Count > 0 ? Math.Round(scored.Average(p => p.TotalGrossStrokes!.Value), 1) : null,
            scored.Count > 0 ? Math.Round(scored.Average(p => p.TotalNetStrokes ?? 0), 1) : null,
            scored.Count > 0 ? Math.Round(scored.Average(p => p.TotalGrossStablefordPoints ?? 0), 1) : null,
            scored.Count > 0 ? Math.Round(scored.Average(p => p.TotalNetStablefordPoints ?? 0), 1) : null,
            holeScores.Count > 0 ? Math.Round(holeScores.Average(s => s.GrossStrokes - s.Par), 2) : null,
            eagleOrBetter,
            birdies,
            pars,
            bogeys,
            doublePlus,
            holeScores.Count > 0 ? Math.Round(100.0 * parOrBetter / holeScores.Count, 1) : null);
    }
}
