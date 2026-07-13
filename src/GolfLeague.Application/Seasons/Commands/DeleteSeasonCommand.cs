using GolfLeague.Application.Common;
using GolfLeague.Domain.Interfaces;
using MediatR;

namespace GolfLeague.Application.Seasons.Commands;

public sealed record DeleteSeasonCommand(
    int Id,
    string UserId) : IRequest<Result<bool>>, IAmAuditableCommand
{
    public string AuditEntityType => "Season";
    public string AuditEntityId => Id.ToString();
}

public sealed class DeleteSeasonCommandHandler : IRequestHandler<DeleteSeasonCommand, Result<bool>>
{
    private readonly ISeasonRepository _seasonRepository;

    public DeleteSeasonCommandHandler(ISeasonRepository seasonRepository)
    {
        _seasonRepository = seasonRepository;
    }

    public async Task<Result<bool>> Handle(DeleteSeasonCommand request, CancellationToken cancellationToken)
    {
        var season = await _seasonRepository.GetByIdAsync(request.Id, cancellationToken);
        if (season is null)
            return Result<bool>.Fail($"Season with ID {request.Id} not found.");

        await _seasonRepository.DeleteAsync(request.Id, cancellationToken);
        return Result<bool>.Ok(true);
    }
}