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

public sealed record HoleComparisonDto(
    int HoleNumber,
    double? JoesAverageScoreToPar,
    double? OthersAverageScoreToPar,
    int JoesScoresRecorded,
    int OthersScoresRecorded);

public sealed record BestRoundDto(
    int PlayerId,
    string PlayerName,
    DateOnly RoundDate,
    string CourseName,
    int TotalGrossStrokes,
    int? TotalNetStrokes);

public sealed record HeadToHeadDto(
    int SharedRoundsCount,
    int JoesWins,
    int OthersWins,
    int Halves);

public sealed record JoesVsOthersStatisticsDto(
    GroupStatisticsDto Joes,
    GroupStatisticsDto Others,
    List<HoleComparisonDto> HoleComparisons,
    BestRoundDto? JoesBestRound,
    BestRoundDto? OthersBestRound,
    HeadToHeadDto HeadToHead);

// ── Query + Handler ──────────────────────────────────────────────────────────

public sealed record GetJoesVsOthersStatisticsQuery(int? SeasonId = null, int? HalfId = null)
    : IRequest<Result<JoesVsOthersStatisticsDto>>;

public sealed class GetJoesVsOthersStatisticsQueryHandler
    : IRequestHandler<GetJoesVsOthersStatisticsQuery, Result<JoesVsOthersStatisticsDto>>
{
    private static readonly string[] JoeFirstNames = ["joe", "joseph"];

    private readonly IRoundRepository _roundRepository;
    private readonly ICourseRepository _courseRepository;

    public GetJoesVsOthersStatisticsQueryHandler(
        IRoundRepository roundRepository,
        ICourseRepository courseRepository)
    {
        _roundRepository = roundRepository;
        _courseRepository = courseRepository;
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
        var participantRounds = new Dictionary<int, Domain.Entities.Round>();

        // For head-to-head: per round, the average net strokes of each group's
        // participants (only rounds where both groups actually played).
        var roundGroupNetAverages = new Dictionary<int, (List<int> Joes, List<int> Others)>();

        var roundsById = finalizedRounds.ToDictionary(r => r.Id);
        var finalizedRoundIds = finalizedRounds.Select(r => r.Id).ToList();
        var allParticipants = await _roundRepository.GetParticipantsForRoundsAsync(finalizedRoundIds, cancellationToken);

        foreach (var p in allParticipants)
        {
            if (p.IsWithdrawn || p.SkippedWeek) continue;

            var round = roundsById[p.RoundId];
            var isJoe = JoeFirstNames.Contains(p.Player.FirstName.Trim(), StringComparer.OrdinalIgnoreCase);
            participantRounds[p.Id] = round;

            if (isJoe)
            {
                joeParticipants.Add(p);
                joeHoleScores.AddRange(p.HoleScores);
            }
            else
            {
                otherParticipants.Add(p);
                otherHoleScores.AddRange(p.HoleScores);
            }

            if (p.TotalNetStrokes is int netStrokes)
            {
                if (!roundGroupNetAverages.TryGetValue(round.Id, out var lists))
                {
                    lists = ([], []);
                    roundGroupNetAverages[round.Id] = lists;
                }
                (isJoe ? lists.Joes : lists.Others).Add(netStrokes);
            }
        }

        var joes = BuildGroupStatistics("Joes", joeParticipants, joeHoleScores);
        var others = BuildGroupStatistics("Non-Joes", otherParticipants, otherHoleScores);

        var holeComparisons = BuildHoleComparisons(joeHoleScores, otherHoleScores);

        var courses = (await _courseRepository.GetAllAsync(cancellationToken))
            .ToDictionary(c => c.Id, c => c.Name);

        var joesBestRound = BuildBestRound(joeParticipants, participantRounds, courses);
        var othersBestRound = BuildBestRound(otherParticipants, participantRounds, courses);

        var sharedRounds = roundGroupNetAverages.Values
            .Where(v => v.Joes.Count > 0 && v.Others.Count > 0)
            .ToList();
        var joesWins = sharedRounds.Count(v => v.Joes.Average() < v.Others.Average());
        var othersWins = sharedRounds.Count(v => v.Others.Average() < v.Joes.Average());
        var headToHead = new HeadToHeadDto(sharedRounds.Count, joesWins, othersWins, sharedRounds.Count - joesWins - othersWins);

        return Result<JoesVsOthersStatisticsDto>.Ok(new JoesVsOthersStatisticsDto(
            joes, others, holeComparisons, joesBestRound, othersBestRound, headToHead));
    }

    private static List<HoleComparisonDto> BuildHoleComparisons(
        List<Domain.Entities.HoleScore> joeHoleScores,
        List<Domain.Entities.HoleScore> otherHoleScores)
    {
        var comparisons = new List<HoleComparisonDto>();
        for (var holeNumber = 1; holeNumber <= 18; holeNumber++)
        {
            var joeScores = joeHoleScores.Where(s => s.HoleNumber == holeNumber).ToList();
            var otherScores = otherHoleScores.Where(s => s.HoleNumber == holeNumber).ToList();

            if (joeScores.Count == 0 && otherScores.Count == 0) continue;

            comparisons.Add(new HoleComparisonDto(
                holeNumber,
                joeScores.Count > 0 ? Math.Round(joeScores.Average(s => s.GrossStrokes - s.Par), 2) : null,
                otherScores.Count > 0 ? Math.Round(otherScores.Average(s => s.GrossStrokes - s.Par), 2) : null,
                joeScores.Count,
                otherScores.Count));
        }
        return comparisons;
    }

    private static BestRoundDto? BuildBestRound(
        List<Domain.Entities.RoundParticipant> participants,
        Dictionary<int, Domain.Entities.Round> participantRounds,
        Dictionary<int, string> courses)
    {
        var best = participants
            .Where(p => p.TotalGrossStrokes.HasValue)
            .OrderBy(p => p.TotalGrossStrokes!.Value)
            .FirstOrDefault();

        if (best is null) return null;

        var round = participantRounds[best.Id];
        return new BestRoundDto(
            best.PlayerId,
            best.Player.FullName,
            round.RoundDate,
            courses.GetValueOrDefault(round.CourseId, "Unknown Course"),
            best.TotalGrossStrokes!.Value,
            best.TotalNetStrokes);
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
