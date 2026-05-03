using GolfLeague.Application.Common;
using GolfLeague.Application.DTOs;
using GolfLeague.Domain.Interfaces;
using MediatR;

namespace GolfLeague.Application.Seasons.Queries;

public sealed record GetSeasonsQuery : IRequest<Result<List<SeasonDto>>>;

public sealed class GetSeasonsQueryHandler : IRequestHandler<GetSeasonsQuery, Result<List<SeasonDto>>>
{
    private readonly ISeasonRepository _seasonRepository;

    public GetSeasonsQueryHandler(ISeasonRepository seasonRepository)
    {
        _seasonRepository = seasonRepository;
    }

    public async Task<Result<List<SeasonDto>>> Handle(GetSeasonsQuery request, CancellationToken cancellationToken)
    {
        var seasons = await _seasonRepository.GetAllAsync(cancellationToken);
        var dtos = seasons.Select(ToDto).ToList();
        return Result<List<SeasonDto>>.Ok(dtos);
    }

    internal static SeasonDto ToDto(Domain.Entities.Season s) => new(
        s.Id, s.Name, s.Year,
        s.StartDate.ToString("yyyy-MM-dd"),
        s.EndDate.ToString("yyyy-MM-dd"),
        s.IsActive, s.BestNRounds);
}
