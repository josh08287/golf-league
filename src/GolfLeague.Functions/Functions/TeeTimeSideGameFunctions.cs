using GolfLeague.Application.Admin;
using GolfLeague.Application.Rounds.Commands;
using GolfLeague.Application.Rounds.Queries;
using GolfLeague.Domain.Enums;
using GolfLeague.Domain.Interfaces;
using GolfLeague.Functions.Helpers;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;

namespace GolfLeague.Functions.Functions;

/// <summary>
/// Optional per-foursome side games (Nassau, 2v2 best ball). The whole
/// feature is gated by the side_games_enabled feature flag.
/// </summary>
public sealed class TeeTimeSideGameFunctions
{
    private readonly IMediator _mediator;
    private readonly IFeatureFlagRepository _featureFlags;

    public TeeTimeSideGameFunctions(IMediator mediator, IFeatureFlagRepository featureFlags)
    {
        _mediator = mediator;
        _featureFlags = featureFlags;
    }

    private async Task<bool> FeatureEnabledAsync(CancellationToken cancellationToken)
    {
        var flag = await _featureFlags.GetAsync(KnownFeatureFlags.SideGamesEnabled, cancellationToken);
        return flag?.Enabled
            ?? KnownFeatureFlags.Defaults[KnownFeatureFlags.SideGamesEnabled];
    }

    /// <summary>
    /// GET /v1/tee-times/{teeTimeId}/side-games — Games this group is
    /// eligible for and has opted into, with live-computed status. Any
    /// authenticated user may view.
    /// </summary>
    [Function("GetTeeTimeSideGames")]
    public async Task<IActionResult> GetSideGames(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/tee-times/{teeTimeId:int}/side-games")] HttpRequest req,
        int teeTimeId,
        CancellationToken cancellationToken)
    {
        var authError = req.RequireAuthenticated();
        if (authError is not null) return authError;

        if (!await FeatureEnabledAsync(cancellationToken))
            return new NotFoundObjectResult(new { error = "Side games are not enabled." });

        var result = await _mediator.Send(new GetTeeTimeSideGamesQuery(teeTimeId), cancellationToken);
        return result.ToOkResult();
    }

    /// <summary>
    /// POST /v1/tee-times/{teeTimeId}/side-games — Opts the group into a
    /// side game. Any active player in the group may opt in.
    /// </summary>
    [Function("OptInTeeTimeSideGame")]
    public async Task<IActionResult> OptIn(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/tee-times/{teeTimeId:int}/side-games")] HttpRequest req,
        int teeTimeId,
        CancellationToken cancellationToken)
    {
        var authError = req.RequireAuthenticated();
        if (authError is not null) return authError;

        if (!await FeatureEnabledAsync(cancellationToken))
            return new BadRequestObjectResult(new { error = "Side games are not enabled." });

        var playerId = req.GetPlayerId();
        if (playerId is null)
            return new ConflictObjectResult(new { error = "Your account isn't linked to a player profile." });

        var body = await req.TryDeserializeAsync<OptInSideGameRequest>(cancellationToken);
        if (body is null)
            return new BadRequestObjectResult(new { error = "Request body is required." });

        var teams = body.Teams?
            .Select(t => new SideGameTeamAssignment(t.ParticipantId, t.TeamNumber))
            .ToList();

        var userId = req.GetUserId() ?? "unknown";
        var command = new OptInTeeTimeSideGameCommand(
            teeTimeId,
            body.GameType,
            body.ScoringBasis,
            body.NassauFormat,
            teams,
            playerId.Value,
            userId);

        var result = await _mediator.Send(command, cancellationToken);
        return result.ToOkResult();
    }

    /// <summary>
    /// DELETE /v1/tee-times/{teeTimeId}/side-games/{sideGameId} — Opts the
    /// group out of a side game. Any player in the group may opt out.
    /// </summary>
    [Function("OptOutTeeTimeSideGame")]
    public async Task<IActionResult> OptOut(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "v1/tee-times/{teeTimeId:int}/side-games/{sideGameId:int}")] HttpRequest req,
        int teeTimeId,
        int sideGameId,
        CancellationToken cancellationToken)
    {
        var authError = req.RequireAuthenticated();
        if (authError is not null) return authError;

        if (!await FeatureEnabledAsync(cancellationToken))
            return new BadRequestObjectResult(new { error = "Side games are not enabled." });

        var playerId = req.GetPlayerId();
        if (playerId is null)
            return new ConflictObjectResult(new { error = "Your account isn't linked to a player profile." });

        var userId = req.GetUserId() ?? "unknown";
        var result = await _mediator.Send(new OptOutTeeTimeSideGameCommand(teeTimeId, sideGameId, playerId.Value, userId), cancellationToken);
        return result.ToOkResult();
    }

    private sealed record SideGameTeamAssignmentRequest(int ParticipantId, int TeamNumber);

    private sealed record OptInSideGameRequest(
        SideGameType GameType,
        ScoringBasis? ScoringBasis,
        NassauFormat? NassauFormat,
        List<SideGameTeamAssignmentRequest>? Teams);
}
