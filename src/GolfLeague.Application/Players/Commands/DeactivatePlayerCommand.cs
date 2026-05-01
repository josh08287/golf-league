using GolfLeague.Application.Common;
using GolfLeague.Domain.Interfaces;
using MediatR;

namespace GolfLeague.Application.Players.Commands;

public sealed record DeactivatePlayerCommand(
    int Id,
    string UserId) : IRequest<Result<bool>>, IAmAuditableCommand;

public sealed class DeactivatePlayerCommandHandler : IRequestHandler<DeactivatePlayerCommand, Result<bool>>
{
    private readonly IPlayerRepository _playerRepository;

    public DeactivatePlayerCommandHandler(IPlayerRepository playerRepository)
    {
        _playerRepository = playerRepository;
    }

    public async Task<Result<bool>> Handle(DeactivatePlayerCommand request, CancellationToken cancellationToken)
    {
        var player = await _playerRepository.GetByIdAsync(request.Id, cancellationToken);
        if (player is null)
            return Result<bool>.Fail($"Player with ID {request.Id} not found.");

        if (!player.IsActive)
            return Result<bool>.Fail($"Player with ID {request.Id} is already inactive.");

        player.IsActive = false;

        await _playerRepository.UpdateAsync(player, cancellationToken);

        return Result<bool>.Ok(true);
    }
}
