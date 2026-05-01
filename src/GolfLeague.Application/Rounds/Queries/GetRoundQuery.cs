using GolfLeague.Application.Common;
using GolfLeague.Application.DTOs;
using GolfLeague.Domain.Interfaces;
using MediatR;

namespace GolfLeague.Application.Rounds.Queries;

public sealed record GetRoundQuery(int Id) : IRequest<Result<RoundDto>>;

public sealed class GetRoundQueryHandler : IRequestHandler<GetRoundQuery, Result<RoundDto>>
{
    private readonly IRoundRepository _roundRepository;
    private readonly ICourseRepository _courseRepository;
    private readonly IFlightRepository _flightRepository;
    private readonly IPlayerRepository _playerRepository;

    public GetRoundQueryHandler(
        IRoundRepository roundRepository,
        ICourseRepository courseRepository,
        IFlightRepository flightRepository,
        IPlayerRepository playerRepository)
    {
        _roundRepository = roundRepository;
        _courseRepository = courseRepository;
        _flightRepository = flightRepository;
        _playerRepository = playerRepository;
    }

    public async Task<Result<RoundDto>> Handle(GetRoundQuery request, CancellationToken cancellationToken)
    {
        var round = await _roundRepository.GetByIdAsync(request.Id, cancellationToken);
        if (round is null)
            return Result<RoundDto>.Fail($"Round with ID {request.Id} not found.");

        var course = await _courseRepository.GetByIdAsync(round.CourseId, cancellationToken);
        var flight = await _flightRepository.GetByIdAsync(round.FlightId, cancellationToken);

        var participantDtos = new List<ParticipantDto>();
        foreach (var participant in round.Participants)
        {
            var player = await _playerRepository.GetByIdAsync(participant.PlayerId, cancellationToken);
            participantDtos.Add(new ParticipantDto(
                participant.Id,
                participant.RoundId,
                participant.PlayerId,
                player?.FullName ?? string.Empty,
                player?.Initials ?? string.Empty,
                participant.HandicapIndex,
                participant.CourseHandicap,
                participant.TotalGrossStrokes,
                participant.TotalNetStrokes,
                participant.TotalStablefordPoints,
                participant.IsWithdrawn));
        }

        var dto = new RoundDto(
            round.Id,
            round.SeasonId,
            round.FlightId,
            flight?.Name ?? string.Empty,
            round.CourseId,
            course?.Name ?? string.Empty,
            round.RoundDate,
            round.Status,
            round.Notes,
            participantDtos);

        return Result<RoundDto>.Ok(dto);
    }
}
