using GolfLeague.Application.Common;
using GolfLeague.Application.DTOs;
using GolfLeague.Domain.Interfaces;
using MediatR;
using static GolfLeague.Application.Common.FlightDisplayName;

namespace GolfLeague.Application.Flights.Queries;

public sealed record GetFlightsQuery(
    int? HalfId = null,
    int? SeasonId = null,
    SortRequest? Sort = null) : IRequest<Result<PagedResult<FlightDto>>>;

public sealed class GetFlightsQueryHandler : IRequestHandler<GetFlightsQuery, Result<PagedResult<FlightDto>>>
{
    private readonly IFlightRepository _flightRepository;

    /// <summary>
    /// Default sort: half then display order (the natural admin-defined order).
    /// </summary>
    private static readonly SortMap<FlightDto> SortMap = new SortMap<FlightDto>(
            source => source.OrderBy(f => f.HalfId).ThenBy(f => f.DisplayOrder))
        .Add("name", f => f.Name)
        .Add("half", f => f.HalfId)
        .Add("halfId", f => f.HalfId)
        .Add("season", f => f.SeasonId)
        .Add("seasonId", f => f.SeasonId)
        .Add("order", f => f.DisplayOrder)
        .Add("displayOrder", f => f.DisplayOrder)
        .Add("players", f => f.PlayerCount)
        .Add("playerCount", f => f.PlayerCount);

    public GetFlightsQueryHandler(IFlightRepository flightRepository)
    {
        _flightRepository = flightRepository;
    }

    public async Task<Result<PagedResult<FlightDto>>> Handle(GetFlightsQuery request, CancellationToken cancellationToken)
    {
        var flights = request.HalfId.HasValue
            ? await _flightRepository.GetByHalfAsync(request.HalfId.Value, cancellationToken)
            : request.SeasonId.HasValue
                ? await _flightRepository.GetBySeasonAsync(request.SeasonId.Value, cancellationToken)
                : await _flightRepository.GetAllAsync(cancellationToken);

        // Cache lock status per half to avoid N+1 queries
        var halfLockCache = new Dictionary<int, bool>();
        async Task<bool> IsLockedAsync(int halfId)
        {
            if (!halfLockCache.TryGetValue(halfId, out var locked))
            {
                locked = await _flightRepository.IsHalfLockedAsync(halfId, cancellationToken);
                halfLockCache[halfId] = locked;
            }
            return locked;
        }

        var dtos = new List<FlightDto>(flights.Count);
        foreach (var f in flights)
        {
            dtos.Add(new FlightDto(
                f.Id,
                f.SeasonId,
                f.HalfId,
                Format(f),
                f.DisplayOrder,
                f.Memberships.Count,
                await IsLockedAsync(f.HalfId)));
        }

        var sorted = SortMap.Apply(dtos, request.Sort);
        return Result<PagedResult<FlightDto>>.Ok(new PagedResult<FlightDto>(sorted.ToList(), 1, sorted.Count, sorted.Count));
    }
}
