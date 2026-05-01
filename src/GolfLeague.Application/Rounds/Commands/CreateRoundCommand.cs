using GolfLeague.Application.Common;
using GolfLeague.Application.DTOs;
using GolfLeague.Domain.Entities;
using GolfLeague.Domain.Enums;
using GolfLeague.Domain.Interfaces;
using GolfLeague.Domain.Services;
using MediatR;

namespace GolfLeague.Application.Rounds.Commands;

public sealed record CreateRoundParticipantInput(int PlayerId);

public sealed record CreateRoundCommand(
    int SeasonId,
    int FlightId,
    int CourseId,
    DateOnly RoundDate,
    string? Notes,
    List<CreateRoundParticipantInput> Participants,
    string UserId) : IRequest<Result<RoundDto>>, IAmAuditableCommand;

public sealed class CreateRoundCommandHandler : IRequestHandler<CreateRoundCommand, Result<RoundDto>>
{
    private readonly IRoundRepository _roundRepository;
    private readonly ICourseRepository _courseRepository;
    private readonly IPlayerRepository _playerRepository;
    private readonly IHandicapRepository _handicapRepository;
    private readonly IFlightRepository _flightRepository;

    public CreateRoundCommandHandler(
        IRoundRepository roundRepository,
        ICourseRepository courseRepository,
        IPlayerRepository playerRepository,
        IHandicapRepository handicapRepository,
        IFlightRepository flightRepository)
    {
        _roundRepository = roundRepository;
        _courseRepository = courseRepository;
        _playerRepository = playerRepository;
        _handicapRepository = handicapRepository;
        _flightRepository = flightRepository;
    }

    public async Task<Result<RoundDto>> Handle(CreateRoundCommand request, CancellationToken cancellationToken)
    {
        var course = await _courseRepository.GetByIdAsync(request.CourseId, cancellationToken);
        if (course is null)
            return Result<RoundDto>.Fail($"Course with ID {request.CourseId} not found.");

        var flight = await _flightRepository.GetByIdAsync(request.FlightId, cancellationToken);
        if (flight is null)
            return Result<RoundDto>.Fail($"Flight with ID {request.FlightId} not found.");

        var round = new Round
        {
            SeasonId = request.SeasonId,
            FlightId = request.FlightId,
            CourseId = request.CourseId,
            RoundDate = request.RoundDate,
            Status = RoundStatus.Scheduled,
            Notes = request.Notes
        };

        await _roundRepository.AddAsync(round, cancellationToken);

        var participantDtos = new List<ParticipantDto>();

        foreach (var input in request.Participants)
        {
            var player = await _playerRepository.GetByIdAsync(input.PlayerId, cancellationToken);
            if (player is null)
                continue;

            var currentHandicap = await _handicapRepository.GetCurrentAsync(input.PlayerId, cancellationToken);
            var handicapIndex = currentHandicap?.HandicapIndex ?? 0.0;
            var courseHandicap = StablefordScoringService.CourseHandicap(handicapIndex, course.SlopeRating);

            var participant = new RoundParticipant
            {
                RoundId = round.Id,
                PlayerId = input.PlayerId,
                HandicapIndex = handicapIndex,
                CourseHandicap = courseHandicap,
                IsWithdrawn = false
            };

            await _roundRepository.AddParticipantAsync(participant, cancellationToken);

            participantDtos.Add(new ParticipantDto(
                participant.Id,
                participant.RoundId,
                participant.PlayerId,
                player.FullName,
                player.Initials,
                participant.HandicapIndex,
                participant.CourseHandicap,
                null,
                null,
                null,
                false));
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
            round.Notes,
            participantDtos);

        return Result<RoundDto>.Ok(dto);
    }
}
