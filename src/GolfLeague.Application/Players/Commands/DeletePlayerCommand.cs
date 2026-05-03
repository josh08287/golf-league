using GolfLeague.Application.Common;
using GolfLeague.Domain.Interfaces;
using MediatR;

namespace GolfLeague.Application.Players.Commands;

public sealed record DeletePlayerCommand(
    int Id,
    string UserId) : IRequest<Result<bool>>, IAmAuditableCommand;

public sealed class DeletePlayerCommandHandler : IRequestHandler<DeletePlayerCommand, Result<bool>>
{
    private readonly IPlayerRepository _playerRepository;

    public DeletePlayerCommandHandler(IPlayerRepository playerRepository)
    {
        _playerRepository = playerRepository;
    }

    public async Task<Result<bool>> Handle(DeletePlayerCommand request, CancellationToken cancellationToken)
    {
        var player = await _playerRepository.GetByIdAsync(request.Id, cancellationToken);
        if (player is null)
            return Result<bool>.Fail($"Player with ID {request.Id} not found.");

        player.IsActive = false;
        await _playerRepository.UpdateAsync(player, cancellationToken);
        return Result<bool>.Ok(true);
    }
}
