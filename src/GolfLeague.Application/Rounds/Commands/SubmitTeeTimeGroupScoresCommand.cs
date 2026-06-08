using GolfLeague.Application.Common;
using GolfLeague.Application.DTOs;
using GolfLeague.Domain.Entities;
using GolfLeague.Domain.Enums;
using GolfLeague.Domain.Interfaces;
using GolfLeague.Domain.Services;
using MediatR;

namespace GolfLeague.Application.Rounds.Commands;

/// <summary>
/// Input for a single player's hole scores.
/// </summary>
public sealed record PlayerHoleScoresInput(
    int PlayerId,
    List<HoleScoreInput> HoleScores);

/// <summary>
/// Submit scores for all players in a tee time group.
/// This does NOT finalize the round - it only pre-populates scores for admin review.
/// Any authenticated player in the tee time can submit scores for the group.
/// </summary>
public sealed record SubmitTeeTimeGroupScoresCommand(
    int TeeTimeId,
    int SubmittedByPlayerId,
    List<PlayerHoleScoresInput> PlayerScores,
    string UserId) : IRequest<Result<TeeTimeGroupScoresResultDto>>, IAmAuditableCommand;

/// <summary>
/// Result containing summary of submitted scores.
/// </summary>
public sealed record TeeTimeGroupScoresResultDto(
    int TeeTimeId,
    int RoundId,
    int PlayersSubmitted,
    int TotalHolesSubmitted,
    string Message);

public sealed class SubmitTeeTimeGroupScoresCommandHandler
    : IRequestHandler<SubmitTeeTimeGroupScoresCommand, Result<TeeTimeGroupScoresResultDto>>
{
    private readonly IRoundRepository _roundRepository;
    private readonly ITeeTimeRepository _teeTimeRepository;
    private readonly ICourseRepository _courseRepository;
    private readonly IPlayerRepository _playerRepository;

    public SubmitTeeTimeGroupScoresCommandHandler(
        IRoundRepository roundRepository,
        ITeeTimeRepository teeTimeRepository,
        ICourseRepository courseRepository,
        IPlayerRepository playerRepository)
    {
        _roundRepository = roundRepository;
        _teeTimeRepository = teeTimeRepository;
        _courseRepository = courseRepository;
        _playerRepository = playerRepository;
    }

    public async Task<Result<TeeTimeGroupScoresResultDto>> Handle(
        SubmitTeeTimeGroupScoresCommand request,
        CancellationToken cancellationToken)
    {
        // Get the tee time
        var teeTime = await _teeTimeRepository.GetByIdAsync(request.TeeTimeId, cancellationToken);
        if (teeTime is null)
            return Result<TeeTimeGroupScoresResultDto>.Fail($"Tee time with ID {request.TeeTimeId} not found.");

        // Verify the submitting player is in this tee time
        var submitterParticipant = teeTime.Participants
            .FirstOrDefault(p => p.PlayerId == request.SubmittedByPlayerId);
        if (submitterParticipant is null)
            return Result<TeeTimeGroupScoresResultDto>.Fail("You must be a member of this tee time to submit scores.");

        if (submitterParticipant.IsWithdrawn)
            return Result<TeeTimeGroupScoresResultDto>.Fail("You have withdrawn from this round and cannot submit scores.");

        // Get the round
        var round = await _roundRepository.GetByIdAsync(teeTime.RoundId, cancellationToken);
        if (round is null)
            return Result<TeeTimeGroupScoresResultDto>.Fail($"Round for tee time {request.TeeTimeId} not found.");

        // Check round status - only allow score entry for Scheduled or InProgress rounds
        if (round.Status == RoundStatus.Finalized)
            return Result<TeeTimeGroupScoresResultDto>.Fail("Cannot submit scores - this round has already been finalized.");

        if (round.Status == RoundStatus.Cancelled)
            return Result<TeeTimeGroupScoresResultDto>.Fail("Cannot submit scores - this round has been cancelled.");

        // Get course data
        var courseHoles = await _courseRepository.GetHolesAsync(round.CourseId, cancellationToken);
        var relevantHoles = round.NineHoleSide == NineHoleSide.Back
            ? courseHoles.Where(h => h.HoleNumber >= 10).ToList()
            : courseHoles.Where(h => h.HoleNumber <= 9).ToList();

        var validHoleNumbers = relevantHoles.Select(h => h.HoleNumber).ToHashSet();

        // Process each player's scores
        var totalHolesSubmitted = 0;
        var playersSubmitted = 0;

        foreach (var playerScoreInput in request.PlayerScores)
        {
            // Find the participant for this player
            var participant = teeTime.Participants
                .FirstOrDefault(p => p.PlayerId == playerScoreInput.PlayerId);

            if (participant is null)
                continue; // Skip players not in this tee time

            if (participant.IsWithdrawn || participant.SkippedWeek)
                continue; // Skip withdrawn/skipped players

            // Validate the submitted holes
            var submittedHoleNumbers = playerScoreInput.HoleScores
                .Select(h => h.HoleNumber)
                .ToHashSet();

            if (submittedHoleNumbers.Count != 9)
                return Result<TeeTimeGroupScoresResultDto>.Fail(
                    $"Player {participant.Player.FullName}: Expected 9 holes, received {submittedHoleNumbers.Count}.");

            var invalidHoles = submittedHoleNumbers.Except(validHoleNumbers).ToList();
            if (invalidHoles.Count > 0)
                return Result<TeeTimeGroupScoresResultDto>.Fail(
                    $"Player {participant.Player.FullName}: Invalid hole numbers: {string.Join(", ", invalidHoles)}.");

            // Clear existing scores and add new ones
            await _roundRepository.ClearHoleScoresAsync(participant.Id, cancellationToken);

            var holeScoreEntities = new List<HoleScore>();
            var allStrokeIndicesInNine = relevantHoles.Select(h => h.StrokeIndex).ToList();
            foreach (var input in playerScoreInput.HoleScores)
            {
                var hole = relevantHoles.FirstOrDefault(h => h.HoleNumber == input.HoleNumber);
                if (hole is null)
                {
                    return Result<TeeTimeGroupScoresResultDto>.Fail(
                        $"Player {participant.Player.FullName}: Hole {input.HoleNumber} not found for this round's 9-hole side.");
                }

                var strokesOnHole = StablefordScoringService.StrokesOnHole(participant.CourseHandicap, hole.StrokeIndex, allStrokeIndicesInNine);
                var maxGross = StablefordScoringService.MaxGross(hole.Par, strokesOnHole);
                var isMaxScore = input.GrossStrokes >= maxGross;
                // Gross is stored as-entered. Net uses adjusted gross (capped at max double bogey) for Stableford.
                var adjustedGross = Math.Min(input.GrossStrokes, maxGross);
                var netStrokes = StablefordScoringService.NetStrokes(adjustedGross, strokesOnHole);
                var netPoints = StablefordScoringService.StablefordPoints(hole.Par, netStrokes);
                var grossPoints = StablefordScoringService.StablefordPoints(hole.Par, input.GrossStrokes);

                // Calculate GIR: reached green in regulation means (strokes - putts) <= (par - 2)
                // Only calculable if we have putts data
                var gir = input.Putts.HasValue
                    ? (input.GrossStrokes - input.Putts.Value) <= (hole.Par - 2)
                    : (bool?)null;

                holeScoreEntities.Add(new HoleScore
                {
                    ParticipantId = participant.Id,
                    HoleNumber = input.HoleNumber,
                    Par = hole.Par,
                    StrokeIndex = hole.StrokeIndex,
                    GrossStrokes = input.GrossStrokes,
                    HandicapStrokes = strokesOnHole,
                    NetStrokes = netStrokes,
                    GrossStablefordPoints = grossPoints,
                    NetStablefordPoints = netPoints,
                    IsMaxScore = isMaxScore,
                    Putts = input.Putts,
                    FirstPuttDistanceFeet = input.FirstPuttDistanceFeet,
                    FairwayHit = input.FairwayHit,
                    Gir = gir,
                });
            }

            await _roundRepository.AddHoleScoresAsync(holeScoreEntities, cancellationToken);

            // Update participant totals
            participant.TotalGrossStrokes = holeScoreEntities.Sum(h => h.GrossStrokes);
            participant.TotalNetStrokes = holeScoreEntities.Sum(h => h.NetStrokes);
            participant.TotalGrossStablefordPoints = holeScoreEntities.Sum(h => h.GrossStablefordPoints);
            participant.TotalNetStablefordPoints = holeScoreEntities.Sum(h => h.NetStablefordPoints);

            await _roundRepository.UpdateParticipantAsync(participant, cancellationToken);

            totalHolesSubmitted += holeScoreEntities.Count;
            playersSubmitted++;
        }

        // Update round status to InProgress if it was Scheduled
        if (round.Status == RoundStatus.Scheduled)
        {
            await _roundRepository.UpdateStatusAsync(round.Id, RoundStatus.InProgress, cancellationToken);
        }

        var result = new TeeTimeGroupScoresResultDto(
            request.TeeTimeId,
            round.Id,
            playersSubmitted,
            totalHolesSubmitted,
            $"Successfully submitted scores for {playersSubmitted} player(s). An admin will review and finalize the round.");

        return Result<TeeTimeGroupScoresResultDto>.Ok(result);
    }
}
