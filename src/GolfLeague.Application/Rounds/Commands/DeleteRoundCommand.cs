using GolfLeague.Application.Common;
using GolfLeague.Domain.Interfaces;
using MediatR;

namespace GolfLeague.Application.Rounds.Commands;

public sealed record DeleteRoundCommand(
    int Id,
    string UserId) : IRequest<Result<bool>>, IAmAuditableCommand
{
    public string AuditEntityType => "Round";
    public string AuditEntityId => Id.ToString();
}

public sealed class DeleteRoundCommandHandler : IRequestHandler<DeleteRoundCommand, Result<bool>>
{
    private readonly IRoundRepository _roundRepository;

    public DeleteRoundCommandHandler(IRoundRepository roundRepository)
    {
        _roundRepository = roundRepository;
    }

    public async Task<Result<bool>> Handle(DeleteRoundCommand request, CancellationToken cancellationToken)
    {
        var round = await _roundRepository.GetByIdAsync(request.Id, cancellationToken);
        if (round is null)
            return Result<bool>.Fail($"Round with ID {request.Id} not found.");

        await _roundRepository.DeleteAsync(request.Id, cancellationToken);
        return Result<bool>.Ok(true);
    }
}
