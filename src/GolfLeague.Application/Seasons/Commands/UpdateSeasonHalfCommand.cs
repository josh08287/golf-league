using GolfLeague.Application.Common;
using GolfLeague.Application.DTOs;
using GolfLeague.Domain.Interfaces;
using MediatR;

namespace GolfLeague.Application.Seasons.Commands;

public sealed record UpdateSeasonHalfCommand(
    int HalfId,
    DateOnly StartDate,
    DateOnly EndDate,
    string UserId) : IRequest<Result<SeasonHalfDto>>, IAmAuditableCommand;

public sealed class UpdateSeasonHalfCommandHandler : IRequestHandler<UpdateSeasonHalfCommand, Result<SeasonHalfDto>>
{
    private readonly IFlightRepository _flightRepository;

    public UpdateSeasonHalfCommandHandler(IFlightRepository flightRepository)
    {
        _flightRepository = flightRepository;
    }

    public async Task<Result<SeasonHalfDto>> Handle(UpdateSeasonHalfCommand request, CancellationToken cancellationToken)
    {
        if (request.EndDate <= request.StartDate)
            return Result<SeasonHalfDto>.Fail("Half end date must be after start date.");

        var half = await _flightRepository.GetHalfByIdAsync(request.HalfId, cancellationToken);
        if (half is null)
            return Result<SeasonHalfDto>.Fail($"Season half with ID {request.HalfId} not found.");

        half.StartDate = request.StartDate;
        half.EndDate = request.EndDate;

        await _flightRepository.UpdateHalfAsync(half, cancellationToken);

        return Result<SeasonHalfDto>.Ok(new SeasonHalfDto(
            half.Id,
            half.SeasonId,
            half.HalfNumber,
            half.Name,
            half.StartDate.ToString("yyyy-MM-dd"),
            half.EndDate.ToString("yyyy-MM-dd")));
    }
}
