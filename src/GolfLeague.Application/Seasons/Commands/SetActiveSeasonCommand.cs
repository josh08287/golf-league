using GolfLeague.Application.Common;
using GolfLeague.Application.DTOs;
using GolfLeague.Application.Seasons.Queries;
using GolfLeague.Domain.Interfaces;
using MediatR;

namespace GolfLeague.Application.Seasons.Commands;

public sealed record SetActiveSeasonCommand(int SeasonId, string UserId) : IRequest<Result<SeasonDto>>, IAmAuditableCommand
{
    public string AuditEntityType => "Season";
    public string AuditEntityId => SeasonId.ToString();
}

public sealed class SetActiveSeasonCommandHandler : IRequestHandler<SetActiveSeasonCommand, Result<SeasonDto>>
{
    private readonly ISeasonRepository _seasonRepository;

    public SetActiveSeasonCommandHandler(ISeasonRepository seasonRepository)
    {
        _seasonRepository = seasonRepository;
    }

    public async Task<Result<SeasonDto>> Handle(SetActiveSeasonCommand request, CancellationToken cancellationToken)
    {
        await _seasonRepository.SetActiveAsync(request.SeasonId, cancellationToken);
        var season = await _seasonRepository.GetActiveAsync(cancellationToken);
        if (season is null)
            return Result<SeasonDto>.Fail("Season not found.");
        return Result<SeasonDto>.Ok(GetSeasonsQueryHandler.ToDto(season));
    }
}
