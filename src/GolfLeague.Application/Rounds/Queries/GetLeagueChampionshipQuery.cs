using GolfLeague.Application.Common;
using GolfLeague.Application.Interfaces;
using GolfLeague.Application.Leagues;
using GolfLeague.Domain.Enums;
using GolfLeague.Domain.Interfaces;
using MediatR;

namespace GolfLeague.Application.Rounds.Queries;

// ── Result DTOs ────────────────────────────────────────────────────────────────

/// <summary>
/// One player's row on the League Championship leaderboard: their season-long
/// seed (rank going into this round), the starting stroke advantage that seed
/// earns them, and how that combines with their score in this specific round.
/// </summary>
public sealed record LeagueChampionshipEntryDto(
    int Rank,
    int PlayerId,
    string PlayerName,
    int Seed,
    int SeasonPoints,
    int StartingStrokeAdvantage,
    int? RoundScore,
    int? AdjustedScore,
    bool IsTied);

public sealed record LeagueChampionshipDto(
    int RoundId,
    bool UseGrossPoints,
    int MaxStrokeAdvantage,
    List<LeagueChampionshipEntryDto> Standings);

// ── Query ──────────────────────────────────────────────────────────────────────

/// <summary>
/// Season-long "League Championship" leaderboard for a tournament round, in the
/// style of the PGA Tour Championship: every eligible player's season Stableford
/// points (gross or net, whole active season, all finalized rounds) determine a
/// seed, and seeds earn a starting stroke advantage scaled by how far ahead of
/// last place they are. That advantage is subtracted from the player's own
/// gross/net strokes for THIS round to produce the adjusted score the
/// leaderboard is sorted by. Substitutes are never eligible, matching every
/// other season-standings view in the app.
/// </summary>
public sealed record GetLeagueChampionshipQuery(int RoundId, bool UseGrossPoints = false)
    : IRequest<Result<LeagueChampionshipDto>>;

public sealed class GetLeagueChampionshipQueryHandler
    : IRequestHandler<GetLeagueChampionshipQuery, Result<LeagueChampionshipDto>>
{
    /// <summary>
    /// The top seed's starting stroke advantage, mirroring the real Tour
    /// Championship's starting-strokes spread. Lower seeds scale down from
    /// this toward 0 proportional to their season points gap from last place.
    /// </summary>
    private const int MaxStrokeAdvantage = 10;

    private readonly IRoundRepository _roundRepository;
    private readonly ILeagueSettingRepository _settings;
    private readonly ILeagueContext _leagueContext;

    public GetLeagueChampionshipQueryHandler(
        IRoundRepository roundRepository,
        ILeagueSettingRepository settings,
        ILeagueContext leagueContext)
    {
        _roundRepository = roundRepository;
        _settings = settings;
        _leagueContext = leagueContext;
    }

    public async Task<Result<LeagueChampionshipDto>> Handle(GetLeagueChampionshipQuery request, CancellationToken cancellationToken)
    {
        var round = await _roundRepository.GetByIdAsync(request.RoundId, cancellationToken);
        if (round is null)
            return Result<LeagueChampionshipDto>.Fail($"Round {request.RoundId} not found.");
        if (round.RoundType != RoundType.Tournament)
            return Result<LeagueChampionshipDto>.Fail("This round is not a tournament round.");

        // Season-long standings: every finalized round in the active season,
        // both halves combined, same eligibility rules as every other
        // season-standings view (not withdrawn, not a substitute). Within
        // each half, the worst N rounds (per the league's standings-drop-count
        // setting) are dropped from a player's point total, same as the
        // flight standings page — rounds with no half (e.g. tournament
        // rounds themselves) are never dropped, since the setting is scoped
        // "per half."
        var dropCount = 1;
        if (_leagueContext.LeagueId.HasValue)
        {
            var dropSetting = await _settings.GetAsync(_leagueContext.LeagueId.Value, KnownSettings.StandingsDropCount, cancellationToken);
            if (dropSetting is not null && int.TryParse(dropSetting.Value, out var parsed) && parsed >= 0)
                dropCount = parsed;
        }

        var seasonRounds = await _roundRepository.GetBySeasonAsync(round.SeasonId, cancellationToken);
        var roundHalfById = seasonRounds.ToDictionary(r => r.Id, r => r.HalfId);
        var finalizedRoundIds = seasonRounds
            .Where(r => r.Status == RoundStatus.Finalized)
            .Select(r => r.Id)
            .ToList();

        var seasonParticipants = await _roundRepository.GetParticipantsForRoundsAsync(finalizedRoundIds, cancellationToken);
        var eligibleSeasonParticipants = seasonParticipants
            .Where(p => !p.IsWithdrawn && !p.IsSubstitute)
            .ToList();

        int PointsOf(Domain.Entities.RoundParticipant p) =>
            request.UseGrossPoints ? p.TotalGrossStablefordPoints ?? 0 : p.TotalNetStablefordPoints ?? 0;

        var seasonPointsByPlayer = eligibleSeasonParticipants
            .GroupBy(p => p.PlayerId)
            .ToDictionary(g => g.Key, playerRounds =>
            {
                var byHalf = playerRounds.GroupBy(p => roundHalfById.GetValueOrDefault(p.RoundId));
                var total = 0;
                foreach (var halfGroup in byHalf)
                {
                    var roundsInHalf = halfGroup.ToList();

                    // Rounds with no half (tournament rounds) are always
                    // counted in full — the drop-count setting is per half.
                    if (halfGroup.Key is null)
                    {
                        total += roundsInHalf.Sum(PointsOf);
                        continue;
                    }

                    var effectiveDrop = Math.Min(dropCount, Math.Max(0, roundsInHalf.Count - 1));
                    var droppedIds = roundsInHalf
                        .OrderBy(PointsOf)
                        .Take(effectiveDrop)
                        .Select(p => p.Id)
                        .ToHashSet();
                    total += roundsInHalf.Where(p => !droppedIds.Contains(p.Id)).Sum(PointsOf);
                }
                return total;
            });

        // This round's participants — only non-substitute players who are
        // actually in this tournament round are shown on the Championship
        // leaderboard, even if they have season points from other rounds.
        var roundParticipants = await _roundRepository.GetParticipantsAsync(request.RoundId, cancellationToken);
        var eligibleRoundParticipants = roundParticipants
            .Where(p => !p.IsWithdrawn && !p.IsSubstitute)
            .ToList();

        if (eligibleRoundParticipants.Count == 0)
            return Result<LeagueChampionshipDto>.Ok(new LeagueChampionshipDto(round.Id, request.UseGrossPoints, MaxStrokeAdvantage, []));

        var seasonPointsForField = eligibleRoundParticipants
            .Select(p => seasonPointsByPlayer.GetValueOrDefault(p.PlayerId, 0))
            .ToList();
        var minPoints = seasonPointsForField.Min();
        var maxPoints = seasonPointsForField.Max();
        var pointsRange = maxPoints - minPoints;

        int StrokeAdvantageFor(int points) =>
            pointsRange > 0
                ? (int)Math.Round(MaxStrokeAdvantage * (points - minPoints) / (double)pointsRange, MidpointRounding.AwayFromZero)
                : MaxStrokeAdvantage;

        // Seed = rank by season points among this round's field (1 = most points).
        var seedOrder = eligibleRoundParticipants
            .Select(p => new { p.PlayerId, Points = seasonPointsByPlayer.GetValueOrDefault(p.PlayerId, 0) })
            .OrderByDescending(x => x.Points)
            .ToList();

        var seedByPlayer = new Dictionary<int, int>();
        int seed = 1;
        for (int i = 0; i < seedOrder.Count; i++)
        {
            if (i > 0 && seedOrder[i].Points != seedOrder[i - 1].Points)
                seed = i + 1;
            seedByPlayer[seedOrder[i].PlayerId] = seed;
        }

        var entries = eligibleRoundParticipants.Select(p =>
        {
            var points = seasonPointsByPlayer.GetValueOrDefault(p.PlayerId, 0);
            var advantage = StrokeAdvantageFor(points);
            var roundScore = request.UseGrossPoints ? p.TotalGrossStrokes : p.TotalNetStrokes;
            var adjustedScore = roundScore.HasValue ? roundScore.Value - advantage : (int?)null;

            return new
            {
                p.PlayerId,
                PlayerName = p.Player.FullName,
                Seed = seedByPlayer[p.PlayerId],
                SeasonPoints = points,
                StrokeAdvantage = advantage,
                RoundScore = roundScore,
                AdjustedScore = adjustedScore,
            };
        }).ToList();

        // Players with no score yet for this round sort to the bottom by seed
        // (lowest stroke advantage first within the "no score" group), rather
        // than by a meaningless null adjusted score.
        var ordered = entries
            .OrderBy(e => e.AdjustedScore.HasValue ? 0 : 1)
            .ThenBy(e => e.AdjustedScore ?? int.MaxValue)
            .ThenBy(e => e.Seed)
            .ToList();

        var standings = new List<LeagueChampionshipEntryDto>();
        int rank = 1;
        for (int i = 0; i < ordered.Count; i++)
        {
            var e = ordered[i];
            if (i > 0 && !(ordered[i].AdjustedScore.HasValue && ordered[i - 1].AdjustedScore.HasValue
                           && ordered[i].AdjustedScore == ordered[i - 1].AdjustedScore))
            {
                rank = i + 1;
            }

            var isTied = e.AdjustedScore.HasValue &&
                ((i > 0 && ordered[i - 1].AdjustedScore == e.AdjustedScore) ||
                 (i < ordered.Count - 1 && ordered[i + 1].AdjustedScore == e.AdjustedScore));

            standings.Add(new LeagueChampionshipEntryDto(
                rank,
                e.PlayerId,
                e.PlayerName,
                e.Seed,
                e.SeasonPoints,
                e.StrokeAdvantage,
                e.RoundScore,
                e.AdjustedScore,
                isTied));
        }

        return Result<LeagueChampionshipDto>.Ok(new LeagueChampionshipDto(round.Id, request.UseGrossPoints, MaxStrokeAdvantage, standings));
    }
}
