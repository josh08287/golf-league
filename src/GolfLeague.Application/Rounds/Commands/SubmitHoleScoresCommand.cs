using GolfLeague.Application.Common;
using GolfLeague.Application.DTOs;
using GolfLeague.Domain.Entities;
using GolfLeague.Domain.Enums;
using GolfLeague.Domain.Interfaces;
using GolfLeague.Domain.Services;
using MediatR;

namespace GolfLeague.Application.Rounds.Commands;

public sealed record HoleScoreInput(int HoleNumber, int GrossStrokes);

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
    private readonly IHandicapRepository _handicapRepository;

    public SubmitHoleScoresCommandHandler(
        IRoundRepository roundRepository,
        ICourseRepository courseRepository,
        IPlayerRepository playerRepository,
        IHandicapRepository handicapRepository)
    {
        _roundRepository = roundRepository;
        _courseRepository = courseRepository;
        _playerRepository = playerRepository;
        _handicapRepository = handicapRepository;
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

        var courseHoles = await _courseRepository.GetHolesAsync(round.CourseId, cancellationToken);
        var course = await _courseRepository.GetByIdAsync(round.CourseId, cancellationToken);
        var player = await _playerRepository.GetByIdAsync(request.PlayerId, cancellationToken);

        if (course is null || player is null)
            return Result<ScorecardDto>.Fail("Course or player data could not be loaded.");

        var holeScoreEntities = new List<HoleScore>();

        foreach (var input in request.HoleScores)
        {
            var hole = courseHoles.FirstOrDefault(h => h.HoleNumber == input.HoleNumber);
            if (hole is null)
                return Result<ScorecardDto>.Fail($"Hole {input.HoleNumber} not found for this course.");

            var strokesOnHole = StablefordScoringService.StrokesOnHole(participant.CourseHandicap, hole.StrokeIndex);
            var maxGross = StablefordScoringService.MaxGross(hole.Par, strokesOnHole);
            var actualGross = Math.Min(input.GrossStrokes, maxGross);
            var isMaxScore = input.GrossStrokes >= maxGross;
            var netStrokes = StablefordScoringService.NetStrokes(actualGross, strokesOnHole);
            var stablefordPoints = StablefordScoringService.StablefordPoints(hole.Par, netStrokes);

            holeScoreEntities.Add(new HoleScore
            {
                ParticipantId = participant.Id,
                HoleNumber = input.HoleNumber,
                Par = hole.Par,
                StrokeIndex = hole.StrokeIndex,
                GrossStrokes = actualGross,
                HandicapStrokes = strokesOnHole,
                NetStrokes = netStrokes,
                StablefordPoints = stablefordPoints,
                IsMaxScore = isMaxScore
            });
        }

        await _roundRepository.AddHoleScoresAsync(holeScoreEntities, cancellationToken);

        participant.TotalGrossStrokes = holeScoreEntities.Sum(h => h.GrossStrokes);
        participant.TotalNetStrokes = holeScoreEntities.Sum(h => h.NetStrokes);
        participant.TotalStablefordPoints = holeScoreEntities.Sum(h => h.StablefordPoints);

        await _roundRepository.UpdateParticipantAsync(participant, cancellationToken);

        if (round.Status == RoundStatus.Scheduled)
        {
            round.Status = RoundStatus.InProgress;
            await _roundRepository.UpdateAsync(round, cancellationToken);
        }

        // --- HANDICAP RECALCULATION FOR 9-HOLE ROUNDS ---
        // After every 2 rounds, combine two 9-hole rounds for handicap calculation
        var playerRounds = await _roundRepository.GetParticipantsAsyncByPlayer(request.PlayerId, cancellationToken);
        var nineHoleRounds = playerRounds
            .Where(rp => rp.HoleScores.Count == 9)
            .OrderBy(rp => rp.Round.RoundDate)
            .ToList();
        if (nineHoleRounds.Count >= 2 && nineHoleRounds.Count % 2 == 0)
        {
            var lastTwo = nineHoleRounds.TakeLast(2).ToList();
            var gross1 = lastTwo[0].HoleScores.Sum(h => h.GrossStrokes);
            var gross2 = lastTwo[1].HoleScores.Sum(h => h.GrossStrokes);
            var diff1 = StablefordScoringService.NineHoleScoreDifferential(gross1, course.CourseRating, course.SlopeRating);
            var diff2 = StablefordScoringService.NineHoleScoreDifferential(gross2, course.CourseRating, course.SlopeRating);
            var combinedDiff = HandicapCalculationService.CombineNineHoleDifferentials(diff1, diff2);
            // Save combinedDiff as a new differential for handicap calculation
            await _handicapRepository.AddDifferentialAsync(request.PlayerId, combinedDiff, round.RoundDate, cancellationToken);
        }
        // --- END HANDICAP RECALCULATION ---

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
                h.StablefordPoints,
                h.IsMaxScore))
            .ToList();

        var participantDto = new ParticipantDto(
            participant.Id,
            participant.RoundId,
            participant.PlayerId,
            player.FullName,
            player.Initials,
            participant.HandicapIndex,
            participant.CourseHandicap,
            participant.TotalGrossStrokes,
            participant.TotalNetStrokes,
            participant.TotalStablefordPoints,
            participant.IsWithdrawn);

        var scorecard = new ScorecardDto(
            round.Id,
            round.RoundDate,
            course.Name,
            course.CourseRating,
            course.SlopeRating,
            participantDto,
            holeScoreDtos,
            holeScoreDtos.Sum(h => h.Par),
            0, // No back nine for 9-hole rounds
            holeScoreDtos.Sum(h => h.Par),
            holeScoreDtos.Sum(h => h.GrossStrokes),
            0,
            holeScoreDtos.Sum(h => h.NetStrokes),
            0,
            holeScoreDtos.Sum(h => h.StablefordPoints),
            0);

        return Result<ScorecardDto>.Ok(scorecard);
    }
}
