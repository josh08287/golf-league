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
    string UserId) : IRequest<Result<bool>>, IAmAuditableCommand;

public sealed class SaveTeeTimeHoleScoresCommandHandler
    : IRequestHandler<SaveTeeTimeHoleScoresCommand, Result<bool>>
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

    public async Task<Result<bool>> Handle(SaveTeeTimeHoleScoresCommand request, CancellationToken cancellationToken)
    {
        var teeTime = await _teeTimeRepository.GetByIdAsync(request.TeeTimeId, cancellationToken);
        if (teeTime is null)
            return Result<bool>.Fail($"Tee time {request.TeeTimeId} not found.");

        var submitter = teeTime.Participants.FirstOrDefault(p => p.PlayerId == request.SubmittedByPlayerId);
        if (submitter is null)
            return Result<bool>.Fail("You must be a member of this tee time to save scores.");

        if (submitter.IsWithdrawn)
            return Result<bool>.Fail("You have withdrawn from this round and cannot save scores.");

        var round = await _roundRepository.GetByIdAsync(teeTime.RoundId, cancellationToken);
        if (round is null)
            return Result<bool>.Fail($"Round for tee time {request.TeeTimeId} not found.");

        if (round.Status == RoundStatus.Finalized)
            return Result<bool>.Fail("Cannot save scores — this round has already been finalized.");

        if (round.Status == RoundStatus.Cancelled)
            return Result<bool>.Fail("Cannot save scores — this round has been cancelled.");

        var courseHoles = await _courseRepository.GetHolesAsync(round.CourseId, cancellationToken);
        var relevantHoles = round.NineHoleSide == NineHoleSide.Back
            ? courseHoles.Where(h => h.HoleNumber >= 10).ToList()
            : courseHoles.Where(h => h.HoleNumber <= 9).ToList();

        var hole = relevantHoles.FirstOrDefault(h => h.HoleNumber == request.HoleNumber);
        if (hole is null)
            return Result<bool>.Fail($"Hole {request.HoleNumber} not found for this round.");

        var allStrokeIndices = relevantHoles.Select(h => h.StrokeIndex).ToList();
        var holeScoreEntities = new List<HoleScore>();

        foreach (var playerInput in request.PlayerScores)
        {
            var participant = teeTime.Participants.FirstOrDefault(p => p.PlayerId == playerInput.PlayerId);
            if (participant is null || participant.IsWithdrawn || participant.SkippedWeek)
                continue;

            var holeInput = playerInput.HoleScores.FirstOrDefault(h => h.HoleNumber == request.HoleNumber);
            if (holeInput is null)
                continue;

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
            });
        }

        await _roundRepository.UpsertHoleScoresAsync(request.HoleNumber, holeScoreEntities, cancellationToken);

        // Transition round to InProgress on first score save
        if (round.Status == RoundStatus.Scheduled)
            await _roundRepository.UpdateStatusAsync(round.Id, RoundStatus.InProgress, cancellationToken);

        return Result<bool>.Ok(true);
    }
}
