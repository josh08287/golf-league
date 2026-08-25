using GolfLeague.Application.Common;
using GolfLeague.Application.Handicaps;
using GolfLeague.Application.Interfaces;
using GolfLeague.Domain.Entities;
using GolfLeague.Domain.Enums;
using GolfLeague.Domain.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;

namespace GolfLeague.Application.Admin;

public sealed record RecalculateAllHandicapsCommand(string UserId)
    : IRequest<Result<RecalculateAllHandicapsResult>>, IAmAuditableCommand
{
    public string AuditEntityType => "Player";
    public string AuditEntityId => "all";
}

public sealed record RecalculateAllHandicapsResult(int PlayersProcessed, int HandicapsCreated);

public sealed class RecalculateAllHandicapsCommandHandler
    : IRequestHandler<RecalculateAllHandicapsCommand, Result<RecalculateAllHandicapsResult>>
{
    private readonly IHandicapRepository _handicapRepository;
    private readonly HandicapRecalculationService _handicapCalc;
    private readonly ILeagueContext _leagueContext;
    private readonly ILogger<RecalculateAllHandicapsCommandHandler> _logger;

    public RecalculateAllHandicapsCommandHandler(
        IHandicapRepository handicapRepository,
        HandicapRecalculationService handicapCalc,
        ILeagueContext leagueContext,
        ILogger<RecalculateAllHandicapsCommandHandler> logger)
    {
        _handicapRepository = handicapRepository;
        _handicapCalc = handicapCalc;
        _leagueContext = leagueContext;
        _logger = logger;
    }

    public async Task<Result<RecalculateAllHandicapsResult>> Handle(
        RecalculateAllHandicapsCommand request,
        CancellationToken cancellationToken)
    {
        await _handicapRepository.DeleteAllCalculatedAsync(cancellationToken);

        var settings = await _handicapCalc.LoadSettingsAsync(_leagueContext.LeagueId ?? 0, cancellationToken);
        var playerIds = await _handicapRepository.GetAllPlayerIdsWithFinalizedRoundsAsync(cancellationToken);

        var created = 0;

        foreach (var playerId in playerIds)
        {
            var roundDates = await _handicapRepository
                .GetFinalizedRoundDatesForPlayerAsync(playerId, cancellationToken);

            foreach (var roundDate in roundDates)
            {
                var roundInputs = await _handicapRepository
                    .GetLastNRoundInputsAsync(
                        playerId,
                        settings.WindowY,
                        asOfDate: roundDate,
                        cancellationToken);

                var newIndex = _handicapCalc.CalculateNewIndex(roundInputs, settings);
                if (newIndex is null)
                    continue;

                await _handicapRepository.AddAsync(new Handicap
                {
                    PlayerId = playerId,
                    HandicapIndex = newIndex.Value,
                    EffectiveDate = roundDate,
                    Source = HandicapSource.Calculated,
                    Notes = $"Recalculated from last {roundInputs.Count} 9-hole round(s)",
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
