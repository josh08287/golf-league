using GolfLeague.Application.Common;
using GolfLeague.Application.DTOs;
using GolfLeague.Domain.Interfaces;
using MediatR;

namespace GolfLeague.Application.Players.Queries;

public sealed record GetHandicapHistoryQuery(int PlayerId) : IRequest<Result<List<HandicapDto>>>;

public sealed class GetHandicapHistoryQueryHandler : IRequestHandler<GetHandicapHistoryQuery, Result<List<HandicapDto>>>
{
    private readonly IPlayerRepository _playerRepository;
    private readonly IHandicapRepository _handicapRepository;

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
            .OrderByDescending(h => h.EffectiveDate)
            .Select(h => new HandicapDto(
                h.Id,
                h.PlayerId,
                h.HandicapIndex,
                h.EffectiveDate,
                h.Source,
                h.Notes))
            .ToList();

        return Result<List<HandicapDto>>.Ok(dtos);
    }
}
