using GolfLeague.Application.Common;
using GolfLeague.Application.DTOs;
using GolfLeague.Application.Rounds.Commands;
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

        return Result<RoundDto>.Ok(RoundDtoMapper.Map(round, round.Course?.Name ?? string.Empty, round.Participants.Count));
    }
}
