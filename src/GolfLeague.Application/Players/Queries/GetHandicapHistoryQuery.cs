using GolfLeague.Application.Common;
using GolfLeague.Application.DTOs;
using GolfLeague.Domain.Interfaces;
using MediatR;

namespace GolfLeague.Application.Players.Queries;

public sealed record GetHandicapHistoryQuery(int PlayerId, SortRequest? Sort = null)
    : IRequest<Result<List<HandicapDto>>>;

public sealed class GetHandicapHistoryQueryHandler : IRequestHandler<GetHandicapHistoryQuery, Result<List<HandicapDto>>>
{
    private readonly IPlayerRepository _playerRepository;
    private readonly IHandicapRepository _handicapRepository;

    /// <summary>
    /// Default sort: most recent effective date first (audit-log style).
    /// </summary>
    private static readonly SortMap<HandicapDto> SortMap = new SortMap<HandicapDto>(
            source => source.OrderByDescending(h => h.EffectiveDate).ThenByDescending(h => h.Id))
        .Add("date", h => h.EffectiveDate)
        .Add("effectiveDate", h => h.EffectiveDate)
        .Add("source", h => h.Source.ToString())
        .Add("index", h => h.HandicapIndex)
        .Add("handicapIndex", h => h.HandicapIndex)
        .Add("nineHole", h => h.NineHoleHandicapIndex)
        .Add("nineHoleHandicapIndex", h => h.NineHoleHandicapIndex)
        .Add("notes", h => h.Notes);

    public GetHandicapHistoryQueryHandler(
        IPlayerRepository playerRepository,
        IHandicapRepository handicapRepository)
    {
        _playerRepository = playerRepository;
        _handicapRepository = handicapRepository;
    }

    public async Task<Result<List<HandicapDto>>> Handle(GetHandicapHistoryQuery request, CancellationToken cancellationToken)
    {
        var player = await _playerRepository.GetByIdAsync(request.PlayerId, cancellationToken);
        if (player is null)
            return Result<List<HandicapDto>>.Fail($"Player with ID {request.PlayerId} not found.");

        var history = await _handicapRepository.GetHistoryAsync(request.PlayerId, cancellationToken);

        var dtos = history
            .Select(h => new HandicapDto(
                h.Id,
                h.PlayerId,
                h.HandicapIndex,
                h.NineHoleHandicapIndex,
                h.EffectiveDate,
                h.Source,
                h.Notes))
            .ToList();

        var sorted = SortMap.Apply(dtos, request.Sort);
        return Result<List<HandicapDto>>.Ok(sorted.ToList());
    }
}
