using GolfLeague.Application.Common;
using GolfLeague.Application.DTOs;
using GolfLeague.Domain.Interfaces;
using MediatR;

namespace GolfLeague.Application.Players.Queries;

/// <summary>
/// Lists all rounds a player has ever participated in, newest first, as a
/// compact summary suitable for the public player profile page.
/// </summary>
public sealed record GetPlayerRoundsQuery(int PlayerId) : IRequest<Result<List<PlayerRoundSummaryDto>>>;

public sealed class GetPlayerRoundsQueryHandler
    : IRequestHandler<GetPlayerRoundsQuery, Result<List<PlayerRoundSummaryDto>>>
{
    private readonly IRoundRepository _roundRepository;

    public GetPlayerRoundsQueryHandler(IRoundRepository roundRepository)
    {
        _roundRepository = roundRepository;
    }

    public async Task<Result<List<PlayerRoundSummaryDto>>> Handle(
        GetPlayerRoundsQuery request,
        CancellationToken cancellationToken)
    {
        var participants = await _roundRepository.GetParticipantsAsyncByPlayer(
            request.PlayerId, cancellationToken);

        var dtos = participants
            .OrderByDescending(rp => rp.Round.RoundDate)
            .ThenByDescending(rp => rp.Round.Id)
            .Select(rp => new PlayerRoundSummaryDto(
                rp.Round.Id,
                rp.Round.RoundDate,
                rp.Round.WeekNumber,
                rp.Round.Course?.Name ?? string.Empty,
                rp.Round.NineHoleSide,
                rp.Round.Status,
                rp.TotalGrossStrokes,
                rp.TotalNetStrokes,
                rp.TotalGrossStablefordPoints,
                rp.TotalNetStablefordPoints,
                rp.IsWithdrawn,
                rp.SkippedWeek))
            .ToList();

        return Result<List<PlayerRoundSummaryDto>>.Ok(dtos);
    }
}
