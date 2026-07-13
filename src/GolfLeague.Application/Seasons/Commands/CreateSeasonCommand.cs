using GolfLeague.Application.Common;
using GolfLeague.Application.DTOs;
using GolfLeague.Application.Interfaces;
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
    string UserId) : IRequest<Result<SeasonDto>>, IAmAuditableCommand
{
    public string AuditEntityType => "Season";
    public string AuditEntityId => "0"; // assigned by the DB; resolved from the response
}

public sealed class CreateSeasonCommandHandler : IRequestHandler<CreateSeasonCommand, Result<SeasonDto>>
{
    private readonly ISeasonRepository _seasonRepository;
    private readonly IFlightRepository _flightRepository;
    private readonly ILeagueContext _leagueContext;

    public CreateSeasonCommandHandler(ISeasonRepository seasonRepository, IFlightRepository flightRepository, ILeagueContext leagueContext)
    {
        _seasonRepository = seasonRepository;
        _flightRepository = flightRepository;
        _leagueContext = leagueContext;
    }

    public async Task<Result<SeasonDto>> Handle(CreateSeasonCommand request, CancellationToken cancellationToken)
    {
        if (_leagueContext.LeagueId is null)
            return Result<SeasonDto>.Fail("No league context.");

        if (request.EndDate <= request.StartDate)
            return Result<SeasonDto>.Fail("Season end date must be after start date.");

        var season = new Season
        {
            LeagueId = _leagueContext.LeagueId.Value,
            Name = request.Name,
            Year = request.Year,
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            BestNRounds = request.BestNRounds,
            IsActive = false,
        };

        await _seasonRepository.AddAsync(season, cancellationToken);

        // Auto-create exactly two halves split at the midpoint.
        var totalDays = request.EndDate.DayNumber - request.StartDate.DayNumber;
        var midpoint = request.StartDate.AddDays(totalDays / 2);

        var half1 = new SeasonHalf
        {
            SeasonId = season.Id,
            HalfNumber = 1,
            Name = $"{season.Name} - First Half",
            StartDate = request.StartDate,
            EndDate = midpoint,
            CreatedAt = DateTime.UtcNow,
        };
        var half2 = new SeasonHalf
        {
            SeasonId = season.Id,
            HalfNumber = 2,
            Name = $"{season.Name} - Second Half",
            StartDate = midpoint.AddDays(1),
            EndDate = request.EndDate,
            CreatedAt = DateTime.UtcNow,
        };

        await _flightRepository.AddHalfAsync(half1, cancellationToken);
        await _flightRepository.AddHalfAsync(half2, cancellationToken);

        season.Halves = [half1, half2];

        return Result<SeasonDto>.Ok(GetSeasonsQueryHandler.ToDto(season));
    }
}
