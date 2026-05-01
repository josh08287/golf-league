using GolfLeague.Application.Common;
using GolfLeague.Application.DTOs;
using GolfLeague.Domain.Interfaces;
using MediatR;

namespace GolfLeague.Application.Rounds.Queries;

public sealed record GetRoundsQuery(int SeasonId, int Page = 1, int PageSize = 20) : IRequest<Result<PagedResult<RoundDto>>>;

public sealed class GetRoundsQueryHandler : IRequestHandler<GetRoundsQuery, Result<PagedResult<RoundDto>>>
{
    private readonly IRoundRepository _roundRepository;
    private readonly ICourseRepository _courseRepository;
    private readonly IFlightRepository _flightRepository;
    private readonly IPlayerRepository _playerRepository;

    public GetRoundsQueryHandler(
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

    public async Task<Result<PagedResult<RoundDto>>> Handle(GetRoundsQuery request, CancellationToken cancellationToken)
    {
        var rounds = await _roundRepository.GetBySeasonAsync(request.SeasonId, cancellationToken);

        var totalCount = rounds.Count;
        var paged = rounds
            .OrderByDescending(r => r.RoundDate)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToList();

        var dtos = new List<RoundDto>(paged.Count);

        foreach (var round in paged)
        {
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

            dtos.Add(new RoundDto(
                round.Id,
                round.SeasonId,
                round.FlightId,
                flight?.Name ?? string.Empty,
                round.CourseId,
                course?.Name ?? string.Empty,
                round.RoundDate,
                round.Status,
                round.Notes,
                participantDtos));
        }

        var result = new PagedResult<RoundDto>(dtos, request.Page, request.PageSize, totalCount);
        return Result<PagedResult<RoundDto>>.Ok(result);
    }
}
