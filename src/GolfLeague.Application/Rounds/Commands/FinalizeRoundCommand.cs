using GolfLeague.Application.Common;
using GolfLeague.Application.DTOs;
using GolfLeague.Domain.Entities;
using GolfLeague.Domain.Enums;
using GolfLeague.Domain.Interfaces;
using GolfLeague.Domain.Services;
using MediatR;
using static GolfLeague.Domain.Services.StablefordScoringService;

namespace GolfLeague.Application.Rounds.Commands;

public sealed record FinalizeRoundCommand(
    int RoundId,
    string UserId) : IRequest<Result<RoundDto>>, IAmAuditableCommand;

public sealed class FinalizeRoundCommandHandler : IRequestHandler<FinalizeRoundCommand, Result<RoundDto>>
{
    private readonly IRoundRepository _roundRepository;
    private readonly ICourseRepository _courseRepository;
    private readonly IHandicapRepository _handicapRepository;
    private readonly IPlayerRepository _playerRepository;
    private readonly IFlightRepository _flightRepository;

    public FinalizeRoundCommandHandler(
        IRoundRepository roundRepository,
        ICourseRepository courseRepository,
        IHandicapRepository handicapRepository,
        IPlayerRepository playerRepository,
        IFlightRepository flightRepository)
    {
        _roundRepository = roundRepository;
        _courseRepository = courseRepository;
        _handicapRepository = handicapRepository;
        _playerRepository = playerRepository;
        _flightRepository = flightRepository;
    }

    public async Task<Result<RoundDto>> Handle(FinalizeRoundCommand request, CancellationToken cancellationToken)
    {
        var round = await _roundRepository.GetByIdAsync(request.RoundId, cancellationToken);
        if (round is null)
            return Result<RoundDto>.Fail($"Round with ID {request.RoundId} not found.");

        if (round.Status == RoundStatus.Finalized)
            return Result<RoundDto>.Fail($"Round {request.RoundId} is already finalized.");

        if (round.Status == RoundStatus.Cancelled)
            return Result<RoundDto>.Fail($"Cannot finalize a cancelled round.");

        var course = await _courseRepository.GetByIdAsync(round.CourseId, cancellationToken);
        if (course is null)
            return Result<RoundDto>.Fail($"Course with ID {round.CourseId} not found.");

        var flight = await _flightRepository.GetByIdAsync(round.FlightId, cancellationToken);
        if (flight is null)
            return Result<RoundDto>.Fail($"Flight with ID {round.FlightId} not found.");

        round.Status = RoundStatus.Finalized;
        await _roundRepository.UpdateAsync(round, cancellationToken);

        foreach (var participant in round.Participants.Where(p => !p.IsWithdrawn && p.TotalGrossStrokes.HasValue))
        {
            double newDiff;

            if (round.RoundType == RoundType.NineHole)
            {
                // For 9-hole rounds, calculate 9-hole differential and store it
                // The differential will be combined with another 9-hole round later
                newDiff = NineHoleScoreDifferential(
                    participant.TotalGrossStrokes!.Value,
                    course.CourseRating,
                    course.SlopeRating);
            }
            else
            {
                // For 18-hole rounds, calculate standard differential
                newDiff = ScoreDifferential(
                    participant.TotalGrossStrokes!.Value,
                    course.CourseRating,
                    course.SlopeRating);
            }

            // Store the differential for handicap calculation
            await _handicapRepository.AddDifferentialAsync(
                participant.PlayerId,
                newDiff,
                round.RoundDate,
                cancellationToken);

            // For 18-hole rounds, recalculate handicap immediately
            // For 9-hole rounds, wait until we have combined differentials
            if (round.RoundType == RoundType.EighteenHole)
            {
                var differentials = await _handicapRepository.GetLast20DifferentialsAsync(participant.PlayerId, cancellationToken);
                var newIndex = HandicapCalculationService.CalculateNewIndex(differentials);

                var updatedHandicap = new Handicap
                {
                    PlayerId = participant.PlayerId,
                    HandicapIndex = newIndex,
                    EffectiveDate = DateOnly.FromDateTime(DateTime.UtcNow),
                    Source = HandicapSource.Calculated,
                    Notes = $"Calculated after round {round.Id} on {round.RoundDate}"
                };

                await _handicapRepository.AddAsync(updatedHandicap, cancellationToken);
            }
        }

        var dto = new RoundDto(
            round.Id,
            round.SeasonId,
            round.FlightId,
            flight.Name,
            round.CourseId,
            course.Name,
            round.RoundDate,
            round.Status,
            round.RoundType,
            round.NineHoleSide,
            round.Participants.Count);

        return Result<RoundDto>.Ok(dto);
    }
}
