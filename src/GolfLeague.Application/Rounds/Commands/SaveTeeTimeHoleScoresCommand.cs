using GolfLeague.Application.Common;
using GolfLeague.Domain.Entities;
using GolfLeague.Domain.Enums;
using GolfLeague.Domain.Interfaces;
using GolfLeague.Domain.Services;
using MediatR;

namespace GolfLeague.Application.Rounds.Commands;

/// <summary>
/// Saves scores for a single hole for all players in a tee time group.
/// Upserts — safe to call repeatedly as the user advances through holes.
/// </summary>
public sealed record SaveTeeTimeHoleScoresCommand(
    int TeeTimeId,
    int SubmittedByPlayerId,
    int HoleNumber,
    List<PlayerHoleScoresInput> PlayerScores,
    string UserId,
    List<ConfirmedOverwrite>? ConfirmedOverwrites = null) : IRequest<Result<SaveHoleScoresOutcome>>, IAmAuditableCommand
{
    public string AuditEntityType => "TeeTime";
    public string AuditEntityId => TeeTimeId.ToString();
}

/// <summary>
/// Result of a hole-score save attempt. When conflicts are present, Saved is
/// false and nothing was written — the caller must resolve the conflicts
/// (via ConfirmedOverwrites) and retry.
/// </summary>
public sealed record SaveHoleScoresOutcome(bool Saved, List<HoleScoreConflictDto> Conflicts);

public sealed class SaveTeeTimeHoleScoresCommandHandler
    : IRequestHandler<SaveTeeTimeHoleScoresCommand, Result<SaveHoleScoresOutcome>>
{
    private readonly IRoundRepository _roundRepository;
    private readonly ITeeTimeRepository _teeTimeRepository;
    private readonly ICourseRepository _courseRepository;

    public SaveTeeTimeHoleScoresCommandHandler(
        IRoundRepository roundRepository,
        ITeeTimeRepository teeTimeRepository,
        ICourseRepository courseRepository)
    {
        _roundRepository = roundRepository;
        _teeTimeRepository = teeTimeRepository;
        _courseRepository = courseRepository;
    }

    public async Task<Result<SaveHoleScoresOutcome>> Handle(SaveTeeTimeHoleScoresCommand request, CancellationToken cancellationToken)
    {
        var teeTime = await _teeTimeRepository.GetByIdAsync(request.TeeTimeId, cancellationToken);
        if (teeTime is null)
            return Result<SaveHoleScoresOutcome>.Fail($"Tee time {request.TeeTimeId} not found.");

        var submitter = teeTime.Participants.FirstOrDefault(p => p.PlayerId == request.SubmittedByPlayerId);
        if (submitter is null)
            return Result<SaveHoleScoresOutcome>.Fail("You must be a member of this tee time to save scores.");

        if (submitter.IsWithdrawn)
            return Result<SaveHoleScoresOutcome>.Fail("You have withdrawn from this round and cannot save scores.");

        var round = await _roundRepository.GetByIdAsync(teeTime.RoundId, cancellationToken);
        if (round is null)
            return Result<SaveHoleScoresOutcome>.Fail($"Round for tee time {request.TeeTimeId} not found.");

        if (round.Status == RoundStatus.Finalized)
            return Result<SaveHoleScoresOutcome>.Fail("Cannot save scores — this round has already been finalized.");

        if (round.Status == RoundStatus.Cancelled)
            return Result<SaveHoleScoresOutcome>.Fail("Cannot save scores — this round has been cancelled.");

        var courseHoles = await _courseRepository.GetHolesAsync(round.CourseId, cancellationToken);
        var relevantHoles = round.NineHoleSide switch
        {
            NineHoleSide.Back => courseHoles.Where(h => h.HoleNumber >= 10).ToList(),
            NineHoleSide.Front => courseHoles.Where(h => h.HoleNumber <= 9).ToList(),
            // NotApplicable (18-hole rounds, e.g. tournaments) plays every hole.
            _ => courseHoles.ToList(),
        };

        var hole = relevantHoles.FirstOrDefault(h => h.HoleNumber == request.HoleNumber);
        if (hole is null)
            return Result<SaveHoleScoresOutcome>.Fail($"Hole {request.HoleNumber} not found for this round.");

        var allStrokeIndices = relevantHoles.Select(h => h.StrokeIndex).ToList();
        var holeScoreEntities = new List<HoleScore>();

        var relevantPlayerInputs = request.PlayerScores
            .Where(p => p.HoleScores.Any(h => h.HoleNumber == request.HoleNumber))
            .ToList();
        var participantIds = relevantPlayerInputs
            .Select(p => teeTime.Participants.FirstOrDefault(pt => pt.PlayerId == p.PlayerId)?.Id)
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .ToList();
        var existingScores = await _roundRepository.GetHoleScoresForParticipantsAsync(request.HoleNumber, participantIds, cancellationToken);
        var confirmedOverwrites = request.ConfirmedOverwrites ?? [];
        var conflicts = new List<HoleScoreConflictDto>();

        foreach (var playerInput in relevantPlayerInputs)
        {
            var participant = teeTime.Participants.FirstOrDefault(p => p.PlayerId == playerInput.PlayerId);
            if (participant is null || participant.IsWithdrawn || participant.SkippedWeek)
                continue;

            var holeInput = playerInput.HoleScores.First(h => h.HoleNumber == request.HoleNumber);
            var existing = existingScores.FirstOrDefault(h => h.ParticipantId == participant.Id);
            var alreadyConfirmed = confirmedOverwrites.Any(c => c.PlayerId == playerInput.PlayerId && c.HoleNumber == request.HoleNumber);

            if (existing is not null
                && existing.LastModifiedByPlayerId.HasValue
                && existing.LastModifiedByPlayerId.Value != request.SubmittedByPlayerId
                && existing.GrossStrokes != holeInput.GrossStrokes
                && !alreadyConfirmed)
            {
                var enteredByName = teeTime.Participants
                    .FirstOrDefault(p => p.PlayerId == existing.LastModifiedByPlayerId.Value)?.Player.FullName;

                conflicts.Add(new HoleScoreConflictDto(
                    playerInput.PlayerId,
                    participant.Player.FullName,
                    request.HoleNumber,
                    existing.GrossStrokes,
                    enteredByName,
                    holeInput.GrossStrokes));
            }
        }

        if (conflicts.Count > 0)
            return Result<SaveHoleScoresOutcome>.Ok(new SaveHoleScoresOutcome(false, conflicts));

        foreach (var playerInput in relevantPlayerInputs)
        {
            var participant = teeTime.Participants.FirstOrDefault(p => p.PlayerId == playerInput.PlayerId);
            if (participant is null || participant.IsWithdrawn || participant.SkippedWeek)
                continue;

            var holeInput = playerInput.HoleScores.First(h => h.HoleNumber == request.HoleNumber);

            var strokesOnHole = StablefordScoringService.StrokesOnHole(participant.CourseHandicap, hole.StrokeIndex, allStrokeIndices);
            var maxGross = StablefordScoringService.MaxGross(hole.Par, strokesOnHole);
            var isMaxScore = holeInput.GrossStrokes >= maxGross;
            var adjustedGross = Math.Min(holeInput.GrossStrokes, maxGross);
            var netStrokes = StablefordScoringService.NetStrokes(adjustedGross, strokesOnHole);
            var netPoints = StablefordScoringService.StablefordPoints(hole.Par, netStrokes);
            var grossPoints = StablefordScoringService.StablefordPoints(hole.Par, holeInput.GrossStrokes);
            var gir = holeInput.Putts.HasValue
                ? (holeInput.GrossStrokes - holeInput.Putts.Value) <= (hole.Par - 2)
                : (bool?)null;

            holeScoreEntities.Add(new HoleScore
            {
                ParticipantId = participant.Id,
                HoleNumber = hole.HoleNumber,
                Par = hole.Par,
                StrokeIndex = hole.StrokeIndex,
                GrossStrokes = holeInput.GrossStrokes,
                HandicapStrokes = strokesOnHole,
                NetStrokes = netStrokes,
                GrossStablefordPoints = grossPoints,
                NetStablefordPoints = netPoints,
                IsMaxScore = isMaxScore,
                Putts = holeInput.Putts,
                FirstPuttDistanceFeet = holeInput.FirstPuttDistanceFeet,
                FairwayHit = holeInput.FairwayHit,
                Gir = gir,
                LastModifiedByPlayerId = request.SubmittedByPlayerId,
            });
        }

        await _roundRepository.UpsertHoleScoresAsync(request.HoleNumber, holeScoreEntities, cancellationToken);

        // Transition round to InProgress on first score save
        if (round.Status == RoundStatus.Scheduled)
            await _roundRepository.UpdateStatusAsync(round.Id, RoundStatus.InProgress, cancellationToken);

        return Result<SaveHoleScoresOutcome>.Ok(new SaveHoleScoresOutcome(true, []));
    }
}
