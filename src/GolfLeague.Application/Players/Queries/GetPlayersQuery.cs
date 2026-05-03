using GolfLeague.Application.Common;
using GolfLeague.Application.DTOs;
using GolfLeague.Domain.Entities;
using GolfLeague.Domain.Interfaces;
using MediatR;

namespace GolfLeague.Application.Players.Queries;

public sealed record GetPlayersQuery(int Page = 1, int PageSize = 20) : IRequest<Result<PagedResult<PlayerDto>>>;

public sealed class GetPlayersQueryHandler : IRequestHandler<GetPlayersQuery, Result<PagedResult<PlayerDto>>>
{
    private readonly IPlayerRepository _playerRepository;
    private readonly IHandicapRepository _handicapRepository;

    public GetPlayersQueryHandler(
        IPlayerRepository playerRepository,
        IHandicapRepository handicapRepository)
    {
        _playerRepository = playerRepository;
        _handicapRepository = handicapRepository;
    }

    public async Task<Result<PagedResult<PlayerDto>>> Handle(GetPlayersQuery request, CancellationToken cancellationToken)
    {
        var players = await _playerRepository.GetAllActiveAsync(cancellationToken);

        var totalCount = players.Count;
        var paged = players
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToList();

        var dtos = new List<PlayerDto>(paged.Count);
        foreach (var player in paged)
        {
            var currentHandicap = await _handicapRepository.GetCurrentAsync(player.Id, cancellationToken);
            dtos.Add(ToDto(player, currentHandicap?.HandicapIndex));
        }

        var result = new PagedResult<PlayerDto>(dtos, request.Page, request.PageSize, totalCount);
        return Result<PagedResult<PlayerDto>>.Ok(result);
    }

    internal static PlayerDto ToDto(Player player, double? currentHandicap)
    {
        var activeMembership = player.FlightMemberships
            .Where(fm => fm.Season.IsActive)
            .OrderByDescending(fm => fm.JoinedAt)
            .FirstOrDefault();

        return new PlayerDto(
            player.Id,
            player.FullName,
            player.Email,
            player.IsActive,
            currentHandicap,
            activeMembership?.FlightId,
            activeMembership?.Flight.Name);
    }
}
