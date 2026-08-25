using GolfLeague.Application.Common;
using GolfLeague.Application.DTOs;
using GolfLeague.Domain.Enums;
using GolfLeague.Domain.Interfaces;
using MediatR;

namespace GolfLeague.Application.Seasons.Queries;

public sealed record GetSeasonsQuery(SortRequest? Sort = null) : IRequest<Result<List<SeasonDto>>>;

public sealed class GetSeasonsQueryHandler : IRequestHandler<GetSeasonsQuery, Result<List<SeasonDto>>>
{
    private readonly ISeasonRepository _seasonRepository;

    /// <summary>
    /// Default sort: most recent year first (matches the prior repo order).
    /// </summary>
    private static readonly SortMap<SeasonDto> SortMap = new SortMap<SeasonDto>(
            source => source.OrderByDescending(s => s.Year))
        .Add("name", s => s.Name)
        .Add("year", s => s.Year)
        .Add("startDate", s => s.StartDate)
        .Add("endDate", s => s.EndDate)
        .Add("active", s => s.IsActive)
        .Add("isActive", s => s.IsActive);

    private readonly IFlightRepository _flightRepository;

    public GetSeasonsQueryHandler(ISeasonRepository seasonRepository, IFlightRepository flightRepository)
    {
        _seasonRepository = seasonRepository;
        _flightRepository = flightRepository;
    }

    public async Task<Result<List<SeasonDto>>> Handle(GetSeasonsQuery request, CancellationToken cancellationToken)
    {
        var seasons = await _seasonRepository.GetAllAsync(cancellationToken);

        // Compute lock state per half (a half is locked once any of its rounds
        // have started) so the admin UI can prevent removing players from it.
        var lockedHalfIds = new HashSet<int>();
        foreach (var half in seasons.SelectMany(s => s.Halves))
        {
            if (await _flightRepository.IsHalfLockedAsync(half.Id, cancellationToken))
                lockedHalfIds.Add(half.Id);
        }

        var dtos = seasons
            .Select(s => ToDto(s, lockedHalfIds))
            .ToList();
        var sorted = SortMap.Apply(dtos, request.Sort);
        return Result<List<SeasonDto>>.Ok(sorted.ToList());
    }

    internal static SeasonDto ToDto(Domain.Entities.Season s, IReadOnlySet<int>? lockedHalfIds = null) => new(
        s.Id, s.Name, s.Year,
        s.StartDate.ToString("yyyy-MM-dd"),
        s.EndDate.ToString("yyyy-MM-dd"),
        s.IsActive, s.BestNRounds,
        s.Halves
            .OrderBy(h => h.HalfNumber)
            .Select(h => new SeasonHalfDto(
                h.Id, h.SeasonId, h.HalfNumber, h.Name,
                h.StartDate.ToString("yyyy-MM-dd"),
                h.EndDate.ToString("yyyy-MM-dd"),
                lockedHalfIds?.Contains(h.Id) ?? false,
                h.ScoringFormat.ToWireString(),
                h.MatchPlayCustomFormula))
            .ToList());
}
