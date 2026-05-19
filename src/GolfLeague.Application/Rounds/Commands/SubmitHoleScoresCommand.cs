using GolfLeague.Application.Common;
using GolfLeague.Application.DTOs;
using GolfLeague.Domain.Entities;
using GolfLeague.Domain.Enums;
using GolfLeague.Domain.Interfaces;
using GolfLeague.Domain.Services;
using MediatR;

namespace GolfLeague.Application.Rounds.Commands;

public sealed record HoleScoreInput(
    int HoleNumber,
    int GrossStrokes,
    int? Putts = null,
    double? FirstPuttDistanceFeet = null,
    bool? FairwayHit = null);

public sealed record SubmitHoleScoresCommand(
    int RoundId,
    int PlayerId,
    List<HoleScoreInput> HoleScores,
    string UserId) : IRequest<Result<ScorecardDto>>, IAmAuditableCommand;

public sealed class SubmitHoleScoresCommandHandler : IRequestHandler<SubmitHoleScoresCommand, Result<ScorecardDto>>
{
    private readonly IRoundRepository _roundRepository;
    private readonly ICourseRepository _courseRepository;
    private readonly IPlayerRepository _playerRepository;

    public SubmitHoleScoresCommandHandler(
        IRoundRepository roundRepository,
        ICourseRepository courseRepository,
        IPlayerRepository playerRepository)
    {
        _roundRepository = roundRepository;
        _courseRepository = courseRepository;
        _playerRepository = playerRepository;
    }

    public async Task<Result<ScorecardDto>> Handle(SubmitHoleScoresCommand request, CancellationToken cancellationToken)
    {
        var round = await _roundRepository.GetByIdAsync(request.RoundId, cancellationToken);
        if (round is null)
            return Result<ScorecardDto>.Fail($"Round with ID {request.RoundId} not found.");

        if (round.Status == RoundStatus.Finalized || round.Status == RoundStatus.Cancelled)
            return Result<ScorecardDto>.Fail($"Cannot submit scores for a round with status '{round.Status}'.");

        var participant = await _roundRepository.GetParticipantAsync(request.RoundId, request.PlayerId, cancellationToken);
        if (participant is null)
            return Result<ScorecardDto>.Fail($"Player {request.PlayerId} is not a participant in round {request.RoundId}.");

        if (participant.IsWithdrawn)
            return Result<ScorecardDto>.Fail($"Player {request.PlayerId} has withdrawn from round {request.RoundId}.");

        if (participant.SkippedWeek)
            return Result<ScorecardDto>.Fail($"Player {request.PlayerId} is marked as skipping round {request.RoundId}. Clear the skip flag before entering scores.");

        var courseHoles = await _courseRepository.GetHolesAsync(round.CourseId, cancellationToken);
        var course = await _courseRepository.GetByIdAsync(round.CourseId, cancellationToken);
        var player = await _playerRepository.GetByIdAsync(request.PlayerId, cancellationToken);

        if (course is null || player is null)
            return Result<ScorecardDto>.Fail("Course or player data could not be loaded.");

        bool isTournament = round.RoundType == RoundType.Tournament;
        var relevantHoles = isTournament
            ? courseHoles.ToList()
            : round.NineHoleSide == NineHoleSide.Back
                ? courseHoles.Where(h => h.HoleNumber >= 10).ToList()
                : courseHoles.Where(h => h.HoleNumber <= 9).ToList();

        int expectedHoles = isTournament ? 18 : 9;
        var validHoleNumbers = relevantHoles.Select(h => h.HoleNumber).ToHashSet();
        var submittedHoleNumbers = request.HoleScores.Select(h => h.HoleNumber).ToHashSet();

        if (submittedHoleNumbers.Count != expectedHoles)
            return Result<ScorecardDto>.Fail(isTournament
                ? $"Expected 18 holes for tournament round, but received {submittedHoleNumbers.Count}."
                : $"Expected 9 holes for the {round.NineHoleSide} nine, but received {submittedHoleNumbers.Count}.");

        var invalidHoles = submittedHoleNumbers.Except(validHoleNumbers).ToList();
        if (invalidHoles.Count > 0)
            return Result<ScorecardDto>.Fail(isTournament
                ? $"Invalid hole numbers for 18-hole round: {string.Join(", ", invalidHoles)}."
                : $"Invalid hole numbers for this {round.NineHoleSide} nine: {string.Join(", ", invalidHoles)}.");

        await _roundRepository.ClearHoleScoresAsync(participant.Id, cancellationToken);

        var holeScoreEntities = new List<HoleScore>();
        var allStrokeIndicesInNine = relevantHoles.Select(h => h.StrokeIndex).ToList();

        foreach (var input in request.HoleScores)
        {
            var hole = relevantHoles.FirstOrDefault(h => h.HoleNumber == input.HoleNumber);
            if (hole is null)
            {
                return Result<ScorecardDto>.Fail(isTournament
                    ? $"Hole {input.HoleNumber} not found for this 18-hole round."
                    : $"Hole {input.HoleNumber} not found for this round's {round.NineHoleSide} nine.");
            }

            var strokesOnHole = isTournament
                ? StablefordScoringService.StrokesOnHole(participant.CourseHandicap, hole.StrokeIndex, RoundType.Tournament)
                : StablefordScoringService.StrokesOnHole(participant.CourseHandicap, hole.StrokeIndex, allStrokeIndicesInNine);
            var maxGross = StablefordScoringService.MaxGross(hole.Par, strokesOnHole);
            var isMaxScore = input.GrossStrokes >= maxGross;
            // Gross is stored as-entered. Net uses adjusted gross (capped at max double bogey) for Stableford.
            var adjustedGross = Math.Min(input.GrossStrokes, maxGross);
            var netStrokes = StablefordScoringService.NetStrokes(adjustedGross, strokesOnHole);
            var netPoints = StablefordScoringService.StablefordPoints(hole.Par, netStrokes);
            var grossPoints = StablefordScoringService.StablefordPoints(hole.Par, input.GrossStrokes);

            // Calculate GIR: on the green putting for birdie means (strokes - putts) <= (par - 1)
            // Only calculable if we have putts data
            var gir = input.Putts.HasValue
                ? (input.GrossStrokes - input.Putts.Value) <= (hole.Par - 1)
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

        participant.TotalGrossStrokes = holeScoreEntities.Sum(h => h.GrossStrokes);
        participant.TotalNetStrokes = holeScoreEntities.Sum(h => h.NetStrokes);
        participant.TotalGrossStablefordPoints = holeScoreEntities.Sum(h => h.GrossStablefordPoints);
        participant.TotalNetStablefordPoints = holeScoreEntities.Sum(h => h.NetStablefordPoints);

        await _roundRepository.UpdateParticipantAsync(participant, cancellationToken);

        if (round.Status == RoundStatus.Scheduled)
        {
            // Use the dedicated status updater so we don't reattach the whole
            // Round graph (which already has its Participants loaded) on top of
            // the participant we just tracked via UpdateParticipantAsync — that
            // would throw a duplicate-key tracking error.
            await _roundRepository.UpdateStatusAsync(round.Id, RoundStatus.InProgress, cancellationToken);
        }

        var holeScoreDtos = holeScoreEntities
            .OrderBy(h => h.HoleNumber)
            .Select(h => new HoleScoreDto(
                h.Id,
                h.HoleNumber,
                h.Par,
                h.StrokeIndex,
                h.GrossStrokes,
                h.HandicapStrokes,
                h.NetStrokes,
                h.GrossStablefordPoints,
                h.NetStablefordPoints,
                h.IsMaxScore,
                h.Putts,
                h.FirstPuttDistanceFeet,
                h.FairwayHit,
                h.Gir))
            .ToList();

        var participantDto = new ParticipantDto(
            participant.Id,
            participant.RoundId,
            participant.PlayerId,
            player.FullName,
            player.Initials,
            participant.FlightId,
            participant.HandicapIndex,
            participant.CourseHandicap,
            participant.TotalGrossStrokes,
            participant.TotalNetStrokes,
            participant.TotalGrossStablefordPoints,
            participant.TotalNetStablefordPoints,
            participant.IsWithdrawn,
            participant.SkippedWeek);

        var scorecard = new ScorecardDto(
            round.Id,
            round.RoundDate,
            course.Name,
            course.CourseRating,
            course.SlopeRating,
            participantDto,
            holeScoreDtos,
            holeScoreDtos.Sum(h => h.Par),
            holeScoreDtos.Sum(h => h.GrossStrokes),
            holeScoreDtos.Sum(h => h.NetStrokes),
            holeScoreDtos.Sum(h => h.GrossStablefordPoints),
            holeScoreDtos.Sum(h => h.NetStablefordPoints));

        return Result<ScorecardDto>.Ok(scorecard);
    }
}
