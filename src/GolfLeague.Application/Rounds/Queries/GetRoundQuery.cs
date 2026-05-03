using GolfLeague.Application.Common;
using GolfLeague.Application.DTOs;
using GolfLeague.Domain.Interfaces;
using MediatR;

namespace GolfLeague.Application.Rounds.Queries;

public sealed record GetRoundQuery(int Id) : IRequest<Result<RoundDto>>;

public sealed class GetRoundQueryHandler : IRequestHandler<GetRoundQuery, Result<RoundDto>>
{
    private readonly IRoundRepository _roundRepository;

    public GetRoundQueryHandler(IRoundRepository roundRepository)
    {
        _roundRepository = roundRepository;
    }

    public async Task<Result<RoundDto>> Handle(GetRoundQuery request, CancellationToken cancellationToken)
    {
        var round = await _roundRepository.GetByIdAsync(request.Id, cancellationToken);
        if (round is null)
            return Result<RoundDto>.Fail($"Round with ID {request.Id} not found.");

        var dto = new RoundDto(
            round.Id,
            round.SeasonId,
            round.FlightId,
            round.Flight?.Name ?? string.Empty,
            round.CourseId,
            round.Course?.Name ?? string.Empty,
            round.RoundDate,
            round.Status,
            round.RoundType,
            round.NineHoleSide,
            round.Participants.Count);

        return Result<RoundDto>.Ok(dto);
    }
}
