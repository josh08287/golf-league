using GolfLeague.Application.Common;
using GolfLeague.Application.Interfaces;
using GolfLeague.Domain.Interfaces;
using MediatR;

namespace GolfLeague.Application.Players.Commands;

public sealed record DeletePlayerCommand(
    int Id,
    string UserId) : IRequest<Result<bool>>, IAmAuditableCommand;

public sealed class DeletePlayerCommandHandler : IRequestHandler<DeletePlayerCommand, Result<bool>>
{
    private readonly IPlayerRepository _playerRepository;
    private readonly IAdminUserService _adminUserService;

    public DeletePlayerCommandHandler(IPlayerRepository playerRepository, IAdminUserService adminUserService)
    {
        _playerRepository = playerRepository;
        _adminUserService = adminUserService;
    }

    public async Task<Result<bool>> Handle(DeletePlayerCommand request, CancellationToken cancellationToken)
    {
        var player = await _playerRepository.GetByIdAsync(request.Id, cancellationToken);
        if (player is null)
            return Result<bool>.Fail($"Player with ID {request.Id} not found.");

        if (player.AppUserId is Guid linkedUserId)
        {
            var userDelete = await _adminUserService.DeleteAsync(linkedUserId, cancellationToken);
            if (!userDelete.IsSuccess)
                return Result<bool>.Fail(userDelete.Error!);
        }

        await _playerRepository.DeleteAsync(request.Id, cancellationToken);
        return Result<bool>.Ok(true);
    }
}
