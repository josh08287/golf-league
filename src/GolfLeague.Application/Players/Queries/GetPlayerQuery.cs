using GolfLeague.Application.Common;
using GolfLeague.Application.DTOs;
using GolfLeague.Application.Interfaces;
using GolfLeague.Domain.Interfaces;
using MediatR;

namespace GolfLeague.Application.Players.Queries;

public sealed record GetPlayerQuery(int Id) : IRequest<Result<PlayerDto>>;

public sealed class GetPlayerQueryHandler : IRequestHandler<GetPlayerQuery, Result<PlayerDto>>
{
    private readonly IPlayerRepository _playerRepository;
    private readonly IHandicapRepository _handicapRepository;
    private readonly IUserRoleService _roleService;

    public GetPlayerQueryHandler(
        IPlayerRepository playerRepository,
        IHandicapRepository handicapRepository,
        IUserRoleService roleService)
    {
        _playerRepository = playerRepository;
        _handicapRepository = handicapRepository;
        _roleService = roleService;
    }

    public async Task<Result<PlayerDto>> Handle(GetPlayerQuery request, CancellationToken cancellationToken)
    {
        var player = await _playerRepository.GetByIdAsync(request.Id, cancellationToken);
        if (player is null)
            return Result<PlayerDto>.Fail($"Player with ID {request.Id} not found.");

        var currentHandicap = await _handicapRepository.GetCurrentAsync(player.Id, cancellationToken);
        var roles = player.AppUserId.HasValue
            ? await _roleService.GetRolesAsync(player.AppUserId.Value, cancellationToken)
            : Array.Empty<string>();

        var dto = GetPlayersQueryHandler.ToDto(player, currentHandicap?.HandicapIndex, roles);
        return Result<PlayerDto>.Ok(dto);
    }
}
