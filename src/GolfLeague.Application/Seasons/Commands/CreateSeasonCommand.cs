using GolfLeague.Application.Common;
using GolfLeague.Application.DTOs;
using GolfLeague.Application.Seasons.Queries;
using GolfLeague.Domain.Entities;
using GolfLeague.Domain.Interfaces;
using MediatR;

namespace GolfLeague.Application.Seasons.Commands;

public sealed record CreateSeasonCommand(
    string Name,
    int Year,
    DateOnly StartDate,
    DateOnly EndDate,
    int? BestNRounds,
    string UserId) : IRequest<Result<SeasonDto>>, IAmAuditableCommand;

public sealed class CreateSeasonCommandHandler : IRequestHandler<CreateSeasonCommand, Result<SeasonDto>>
{
    private readonly ISeasonRepository _seasonRepository;

    public CreateSeasonCommandHandler(ISeasonRepository seasonRepository)
    {
        _seasonRepository = seasonRepository;
    }

    public async Task<Result<SeasonDto>> Handle(CreateSeasonCommand request, CancellationToken cancellationToken)
    {
        var season = new Season
        {
            Name = request.Name,
            Year = request.Year,
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            BestNRounds = request.BestNRounds,
            IsActive = false,
        };

        await _seasonRepository.AddAsync(season, cancellationToken);
        return Result<SeasonDto>.Ok(GetSeasonsQueryHandler.ToDto(season));
    }
}
