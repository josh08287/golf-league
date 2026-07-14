using GolfLeague.Application.Common;
using GolfLeague.Application.DTOs;
using GolfLeague.Domain.Interfaces;
using MediatR;

namespace GolfLeague.Application.Players.Queries;

/// <summary>
/// Lists every player flagged as a substitute (active or not — deactivated
/// players remain sub-eligible), active substitutes first. Backs the admin
/// Substitutes section, kept separate from the regular roster list
/// (GetPlayersQuery, which excludes substitutes).
/// </summary>
public sealed record GetSubstitutesQuery : IRequest<Result<IReadOnlyList<PlayerDto>>>;

public sealed class GetSubstitutesQueryHandler : IRequestHandler<GetSubstitutesQuery, Result<IReadOnlyList<PlayerDto>>>
{
    private readonly IPlayerRepository _playerRepository;
    private readonly IHandicapRepository _handicapRepository;
    private readonly IAppUserRepository _appUserRepository;

    public GetSubstitutesQueryHandler(
        IPlayerRepository playerRepository,
        IHandicapRepository handicapRepository,
        IAppUserRepository appUserRepository)
    {
        _playerRepository = playerRepository;
        _handicapRepository = handicapRepository;
        _appUserRepository = appUserRepository;
    }

    public async Task<Result<IReadOnlyList<PlayerDto>>> Handle(GetSubstitutesQuery request, CancellationToken cancellationToken)
    {
        var players = (await _playerRepository.GetAllAsync(cancellationToken))
            .Where(p => p.IsSubstitute)
            .OrderByDescending(p => p.IsActive)
            .ThenBy(p => p.LastName)
            .ThenBy(p => p.FirstName)
            .ToList();

        var appUserIds = players
            .Where(p => p.AppUserId.HasValue)
            .Select(p => p.AppUserId!.Value)
            .ToList();
        var rolesByUserId = await _appUserRepository.GetRolesAsync(appUserIds, cancellationToken);

        var dtos = new List<PlayerDto>(players.Count);
        foreach (var player in players)
        {
            var currentHandicap = await _handicapRepository.GetCurrentAsync(player.Id, cancellationToken);
            IReadOnlyList<string> roles = player.AppUserId.HasValue
                && rolesByUserId.TryGetValue(player.AppUserId.Value, out var r)
                ? r
                : Array.Empty<string>();
            dtos.Add(GetPlayersQueryHandler.ToDto(player, currentHandicap?.HandicapIndex, roles));
        }

        return Result<IReadOnlyList<PlayerDto>>.Ok(dtos);
    }
}
