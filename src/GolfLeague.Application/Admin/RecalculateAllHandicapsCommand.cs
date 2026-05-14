using GolfLeague.Application.Common;
using GolfLeague.Domain.Entities;
using GolfLeague.Domain.Enums;
using GolfLeague.Domain.Interfaces;
using GolfLeague.Domain.Services;
using MediatR;
using Microsoft.Extensions.Logging;

namespace GolfLeague.Application.Admin;

public sealed record RecalculateAllHandicapsCommand(string UserId) : IRequest<Result<RecalculateAllHandicapsResult>>;

public sealed record RecalculateAllHandicapsResult(int PlayersProcessed, int HandicapsCreated);

public sealed class RecalculateAllHandicapsCommandHandler
    : IRequestHandler<RecalculateAllHandicapsCommand, Result<RecalculateAllHandicapsResult>>
{
    private readonly IHandicapRepository _handicapRepository;
    private readonly ILogger<RecalculateAllHandicapsCommandHandler> _logger;

    public RecalculateAllHandicapsCommandHandler(
        IHandicapRepository handicapRepository,
        ILogger<RecalculateAllHandicapsCommandHandler> logger)
    {
        _handicapRepository = handicapRepository;
        _logger = logger;
    }

    public async Task<Result<RecalculateAllHandicapsResult>> Handle(
        RecalculateAllHandicapsCommand request,
        CancellationToken cancellationToken)
    {
        await _handicapRepository.DeleteAllCalculatedAsync(cancellationToken);

        var playerIds = await _handicapRepository.GetAllPlayerIdsWithFinalizedRoundsAsync(cancellationToken);

        var created = 0;

        foreach (var playerId in playerIds)
        {
            var roundDates = await _handicapRepository
                .GetFinalizedRoundDatesForPlayerAsync(playerId, cancellationToken);

            foreach (var roundDate in roundDates)
            {
                var differentials = await _handicapRepository
                    .GetLastNNineHoleDifferentialsAsync(
                        playerId,
                        HandicapCalculationService.RollingWindowSize,
                        asOfDate: roundDate,
                        cancellationToken);

                if (differentials.Count == 0)
                    continue;

                // CalculateNewIndex returns the average 9-hole differential.
                // HandicapIndex stores the full 18-hole index = 9-hole diff × 2.
                var nineHoleIndex = HandicapCalculationService.CalculateNewIndex(differentials);
                var newIndex = Math.Round(nineHoleIndex * 2, 1, MidpointRounding.ToEven);

                await _handicapRepository.AddAsync(new Handicap
                {
                    PlayerId = playerId,
                    HandicapIndex = newIndex,
                    EffectiveDate = roundDate,
                    Source = HandicapSource.Calculated,
                    Notes = $"Recalculated from last {differentials.Count} 9-hole round(s)",
                }, cancellationToken);

                created++;
            }

            _logger.LogInformation(
                "Recalculated handicaps for player {PlayerId}: {Count} round(s) processed",
                playerId, roundDates.Count);
        }

        return Result<RecalculateAllHandicapsResult>.Ok(
            new RecalculateAllHandicapsResult(playerIds.Count, created));
    }
}
