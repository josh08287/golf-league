using GolfLeague.Application.Common;
using GolfLeague.Application.DTOs;
using GolfLeague.Application.Players.Queries;
using GolfLeague.Domain.Interfaces;
using MediatR;

namespace GolfLeague.Application.Players.Commands;

/// <summary>
/// Flags (or unflags) a player as part of the league's substitute pool.
/// Substitute status is kept mutually exclusive with regular roster status:
/// a player can't be flagged as a substitute while they still hold a flight
/// membership in the active season's current or upcoming half — a membership
/// in an already-completed half doesn't block, so someone who played the
/// first half can sub in the second. Works regardless of IsActive, since
/// deactivated players remain sub-eligible.
/// </summary>
public sealed record SetPlayerSubstituteCommand(
    int PlayerId,
    bool IsSubstitute,
    string UserId) : IRequest<Result<PlayerDto>>, IAmAuditableCommand
{
    public string AuditEntityType => "Player";
    public string AuditEntityId => PlayerId.ToString();
}

public sealed class SetPlayerSubstituteCommandHandler : IRequestHandler<SetPlayerSubstituteCommand, Result<PlayerDto>>
{
    private readonly IPlayerRepository _playerRepository;
    private readonly IHandicapRepository _handicapRepository;

    public SetPlayerSubstituteCommandHandler(
        IPlayerRepository playerRepository,
        IHandicapRepository handicapRepository)
    {
        _playerRepository = playerRepository;
        _handicapRepository = handicapRepository;
    }

    public async Task<Result<PlayerDto>> Handle(SetPlayerSubstituteCommand request, CancellationToken cancellationToken)
    {
        var player = await _playerRepository.GetByIdAsync(request.PlayerId, cancellationToken);
        if (player is null)
            return Result<PlayerDto>.Fail($"Player with ID {request.PlayerId} not found.");

        if (request.IsSubstitute)
        {
            // Only a flight membership in the current (or an upcoming) half
            // blocks — a membership in an already-completed half of the active
            // season doesn't make someone a roster player anymore. Upcoming
            // halves still block so the sub/roster exclusivity doesn't break
            // the day the next half starts. UTC date is close enough here:
            // half boundaries are week-scale.
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var hasCurrentFlightMembership = player.FlightMemberships
                .Any(fm => fm.Season.IsActive && fm.Half.EndDate >= today);
            if (hasCurrentFlightMembership)
                return Result<PlayerDto>.Fail(
                    "This player is still assigned to a flight for the current half. Remove them from their flight before marking them as a substitute.");
        }

        player.IsSubstitute = request.IsSubstitute;
        await _playerRepository.UpdateAsync(player, cancellationToken);

        var currentHandicap = await _handicapRepository.GetCurrentAsync(request.PlayerId, cancellationToken);
        return Result<PlayerDto>.Ok(GetPlayersQueryHandler.ToDto(player, currentHandicap?.HandicapIndex));
    }
}
