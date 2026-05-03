using GolfLeague.Application.Common;
using GolfLeague.Application.DTOs;
using GolfLeague.Domain.Interfaces;
using MediatR;

namespace GolfLeague.Application.Players.Queries;

public sealed record GetPlayerQuery(int Id) : IRequest<Result<PlayerDto>>;

public sealed class GetPlayerQueryHandler : IRequestHandler<GetPlayerQuery, Result<PlayerDto>>
{
    private readonly IPlayerRepository _playerRepository;
    private readonly IHandicapRepository _handicapRepository;

    public GetPlayerQueryHandler(
        IPlayerRepository playerRepository,
        IHandicapRepository handicapRepository)
    {
        _playerRepository = playerRepository;
        _handicapRepository = handicapRepository;
    }

    public async Task<Result<PlayerDto>> Handle(GetPlayerQuery request, CancellationToken cancellationToken)
    {
        var player = await _playerRepository.GetByIdAsync(request.Id, cancellationToken);
        if (player is null)
            return Result<PlayerDto>.Fail($"Player with ID {request.Id} not found.");

        var currentHandicap = await _handicapRepository.GetCurrentAsync(player.Id, cancellationToken);
        var dto = GetPlayersQueryHandler.ToDto(player, currentHandicap?.HandicapIndex);
        return Result<PlayerDto>.Ok(dto);
    }
}
