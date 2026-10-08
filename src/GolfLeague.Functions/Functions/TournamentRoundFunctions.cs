using GolfLeague.Application.Admin;
using GolfLeague.Application.Rounds.Commands;
using GolfLeague.Application.Rounds.Queries;
using GolfLeague.Domain.Interfaces;
using GolfLeague.Functions.Helpers;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;

namespace GolfLeague.Functions.Functions;

public sealed class TournamentRoundFunctions
{
    private readonly IMediator _mediator;
    private readonly IFeatureFlagRepository _featureFlags;

    public TournamentRoundFunctions(IMediator mediator, IFeatureFlagRepository featureFlags)
    {
        _mediator = mediator;
        _featureFlags = featureFlags;
    }

    [Function("CreateTournamentRound")]
    public async Task<IActionResult> CreateTournamentRound(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/tournament-rounds")] HttpRequest req,
        CancellationToken cancellationToken)
    {
        var authError = req.RequireRole("admin");
        if (authError is not null) return authError;

        var body = await req.TryDeserializeAsync<CreateTournamentRoundRequest>(cancellationToken);
        if (body is null)
            return new BadRequestObjectResult(new { error = "Request body is required." });

        var userId = req.GetUserId() ?? "unknown";
        var matchups = body.Matchups?.Select(m => new MatchupInput(m.Player1Id, m.Player2Id)).ToList();

        var command = new CreateTournamentRoundCommand(
            body.SeasonId,
            body.CourseId,
            body.ResolvedDate,
            body.PlayerIds,
            matchups,
            body.Notes,
            userId,
            body.LongestDriveHoleNumber,
            body.GrossSkinsPool,
            body.NetSkinsPool);

        var result = await _mediator.Send(command, cancellationToken);
        return result.ToCreatedResult($"/api/v1/tournament-rounds/{result.Value?.Round.Id}");
    }

    [Function("GetTournamentResults")]
    public async Task<IActionResult> GetTournamentResults(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/tournament-rounds/{id}/results")] HttpRequest req,
        string id,
        CancellationToken cancellationToken)
    {
        if (!int.TryParse(id, out var roundId))
            return new BadRequestObjectResult(new { error = "Invalid round ID." });

        var result = await _mediator.Send(new GetTournamentResultsQuery(roundId), cancellationToken);
        return result.ToOkResult();
    }

    /// <summary>
    /// GET /v1/tournament-rounds/{id}/championship — season-long League
    /// Championship seeding/leaderboard for this tournament round. Gated by
    /// the league_championship_enabled feature flag.
    /// </summary>
    [Function("GetLeagueChampionship")]
    public async Task<IActionResult> GetLeagueChampionship(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/tournament-rounds/{id}/championship")] HttpRequest req,
        string id,
        CancellationToken cancellationToken)
    {
        if (!int.TryParse(id, out var roundId))
            return new BadRequestObjectResult(new { error = "Invalid round ID." });

        var flag = await _featureFlags.GetAsync(KnownFeatureFlags.LeagueChampionshipEnabled, cancellationToken);
        var enabled = flag?.Enabled ?? KnownFeatureFlags.Defaults[KnownFeatureFlags.LeagueChampionshipEnabled];
        if (!enabled)
            return new NotFoundObjectResult(new { error = "League Championship is not enabled." });

        var useGrossPoints = bool.TryParse(req.Query["useGrossPoints"], out var ug) && ug;
        var result = await _mediator.Send(new GetLeagueChampionshipQuery(roundId, useGrossPoints), cancellationToken);
        return result.ToOkResult();
    }

    [Function("SetTournamentMatchups")]
    public async Task<IActionResult> SetTournamentMatchups(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "v1/tournament-rounds/{id}/matchups")] HttpRequest req,
        string id,
        CancellationToken cancellationToken)
    {
        var authError = req.RequireRole("admin");
        if (authError is not null) return authError;

        if (!int.TryParse(id, out var roundId))
            return new BadRequestObjectResult(new { error = "Invalid round ID." });

        var body = await req.TryDeserializeAsync<SetMatchupsRequest>(cancellationToken);
        if (body is null)
            return new BadRequestObjectResult(new { error = "Request body is required." });

        var userId = req.GetUserId() ?? "unknown";
        var matchups = body.Matchups.Select(m => new MatchupInput(m.Player1Id, m.Player2Id)).ToList();
        var result = await _mediator.Send(new SetTournamentMatchupsCommand(roundId, matchups, userId), cancellationToken);
        return result.ToOkResult();
    }

    [Function("RegenerateTournamentMatchups")]
    public async Task<IActionResult> RegenerateTournamentMatchups(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/tournament-rounds/{id}/matchups/regenerate")] HttpRequest req,
        string id,
        CancellationToken cancellationToken)
    {
        var authError = req.RequireRole("admin");
        if (authError is not null) return authError;

        if (!int.TryParse(id, out var roundId))
            return new BadRequestObjectResult(new { error = "Invalid round ID." });

        var userId = req.GetUserId() ?? "unknown";
        var result = await _mediator.Send(new RegenerateTournamentMatchupsCommand(roundId, userId), cancellationToken);
        return result.ToOkResult();
    }

    [Function("SaveTournamentExtras")]
    public async Task<IActionResult> SaveTournamentExtras(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "v1/tournament-rounds/{id}/extras")] HttpRequest req,
        string id,
        CancellationToken cancellationToken)
    {
        var authError = req.RequireRole("scorer", "admin");
        if (authError is not null) return authError;

        if (!int.TryParse(id, out var roundId))
            return new BadRequestObjectResult(new { error = "Invalid round ID." });

        var body = await req.TryDeserializeAsync<SaveExtrasRequest>(cancellationToken);
        if (body is null)
            return new BadRequestObjectResult(new { error = "Request body is required." });

        var userId = req.GetUserId() ?? "unknown";
        var holeExtras = body.HoleExtras.Select(e => new HoleExtraInput(e.HoleNumber, e.ClosestToPinPlayerId, e.LongestDrivePlayerId)).ToList();
        var result = await _mediator.Send(new SaveTournamentExtrasCommand(roundId, holeExtras, userId), cancellationToken);
        return result.ToOkResult();
    }

    [Function("GetTournamentMatchups")]
    public async Task<IActionResult> GetTournamentMatchups(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/tournament-rounds/{id}/matchups")] HttpRequest req,
        string id,
        CancellationToken cancellationToken)
    {
        if (!int.TryParse(id, out var roundId))
            return new BadRequestObjectResult(new { error = "Invalid round ID." });

        var result = await _mediator.Send(new GetTournamentResultsQuery(roundId), cancellationToken);
        if (!result.IsSuccess)
            return result.ToOkResult();

        return new OkObjectResult(result.Value?.MatchupResults);
    }

    [Function("AddTournamentParticipants")]
    public async Task<IActionResult> AddTournamentParticipants(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/tournament-rounds/{id}/participants")] HttpRequest req,
        string id,
        CancellationToken cancellationToken)
    {
        var authError = req.RequireRole("admin");
        if (authError is not null) return authError;

        if (!int.TryParse(id, out var roundId))
            return new BadRequestObjectResult(new { error = "Invalid round ID." });

        var body = await req.TryDeserializeAsync<AddParticipantsRequest>(cancellationToken);
        if (body is null)
            return new BadRequestObjectResult(new { error = "Request body is required." });

        var userId = req.GetUserId() ?? "unknown";
        var result = await _mediator.Send(new AddTournamentParticipantsCommand(roundId, body.PlayerIds, userId), cancellationToken);
        return result.ToOkResult();
    }

    [Function("RemoveTournamentParticipant")]
    public async Task<IActionResult> RemoveTournamentParticipant(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "v1/tournament-rounds/{id}/participants/{playerId}")] HttpRequest req,
        string id,
        string playerId,
        CancellationToken cancellationToken)
    {
        var authError = req.RequireRole("admin");
        if (authError is not null) return authError;

        if (!int.TryParse(id, out var roundId))
            return new BadRequestObjectResult(new { error = "Invalid round ID." });
        if (!int.TryParse(playerId, out var parsedPlayerId))
            return new BadRequestObjectResult(new { error = "Invalid player ID." });

        var userId = req.GetUserId() ?? "unknown";
        var result = await _mediator.Send(new RemoveTournamentParticipantCommand(roundId, parsedPlayerId, userId), cancellationToken);
        return result.ToOkResult();
    }

    [Function("SetTournamentLongestDriveHole")]
    public async Task<IActionResult> SetTournamentLongestDriveHole(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "v1/tournament-rounds/{id}/longest-drive-hole")] HttpRequest req,
        string id,
        CancellationToken cancellationToken)
    {
        var authError = req.RequireRole("admin");
        if (authError is not null) return authError;

        if (!int.TryParse(id, out var roundId))
            return new BadRequestObjectResult(new { error = "Invalid round ID." });

        var body = await req.TryDeserializeAsync<SetLongestDriveHoleRequest>(cancellationToken);
        if (body is null)
            return new BadRequestObjectResult(new { error = "Request body is required." });

        var userId = req.GetUserId() ?? "unknown";
        var result = await _mediator.Send(new SetTournamentLongestDriveHoleCommand(roundId, body.HoleNumber, userId), cancellationToken);
        return result.ToOkResult();
    }

    [Function("SetTournamentLongestDriveWinner")]
    public async Task<IActionResult> SetTournamentLongestDriveWinner(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "v1/tournament-rounds/{id}/flights/{flightId}/longest-drive")] HttpRequest req,
        string id,
        string flightId,
        CancellationToken cancellationToken)
    {
        var authError = req.RequireRole("admin");
        if (authError is not null) return authError;

        if (!int.TryParse(id, out var roundId))
            return new BadRequestObjectResult(new { error = "Invalid round ID." });
        if (!int.TryParse(flightId, out var tournamentFlightId))
            return new BadRequestObjectResult(new { error = "Invalid flight ID." });

        var body = await req.TryDeserializeAsync<SetLongestDriveWinnerRequest>(cancellationToken);
        if (body is null)
            return new BadRequestObjectResult(new { error = "Request body is required." });

        var userId = req.GetUserId() ?? "unknown";
        var result = await _mediator.Send(
            new SetTournamentLongestDriveWinnerCommand(roundId, tournamentFlightId, body.WinnerPlayerId, userId), cancellationToken);
        return result.ToOkResult();
    }

    [Function("SetTournamentSkinsPool")]
    public async Task<IActionResult> SetTournamentSkinsPool(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "v1/tournament-rounds/{id}/skins-pool")] HttpRequest req,
        string id,
        CancellationToken cancellationToken)
    {
        var authError = req.RequireRole("admin");
        if (authError is not null) return authError;

        if (!int.TryParse(id, out var roundId))
            return new BadRequestObjectResult(new { error = "Invalid round ID." });

        var body = await req.TryDeserializeAsync<SetSkinsPoolRequest>(cancellationToken);
        if (body is null)
            return new BadRequestObjectResult(new { error = "Request body is required." });

        var userId = req.GetUserId() ?? "unknown";
        var result = await _mediator.Send(new SetTournamentSkinsPoolCommand(roundId, body.GrossSkinsPool, body.NetSkinsPool, userId), cancellationToken);
        return result.ToOkResult();
    }

    /// <summary>
    /// PUT /v1/tournament-rounds/{id}/counts-toward-handicap — opts the
    /// tournament's 18-hole scores into players' handicaps. Gated by the
    /// tournament_handicap_toggle_enabled feature flag.
    /// </summary>
    [Function("SetTournamentCountsTowardHandicap")]
    public async Task<IActionResult> SetTournamentCountsTowardHandicap(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "v1/tournament-rounds/{id}/counts-toward-handicap")] HttpRequest req,
        string id,
        CancellationToken cancellationToken)
    {
        var authError = req.RequireRole("admin");
        if (authError is not null) return authError;

        if (!int.TryParse(id, out var roundId))
            return new BadRequestObjectResult(new { error = "Invalid round ID." });

        var flag = await _featureFlags.GetAsync(KnownFeatureFlags.TournamentHandicapToggleEnabled, cancellationToken);
        if (!(flag?.Enabled ?? KnownFeatureFlags.Defaults[KnownFeatureFlags.TournamentHandicapToggleEnabled]))
            return new NotFoundObjectResult(new { error = "Tournament handicap toggle is not enabled." });

        var body = await req.TryDeserializeAsync<SetCountsTowardHandicapRequest>(cancellationToken);
        if (body is null)
            return new BadRequestObjectResult(new { error = "Request body is required." });

        var userId = req.GetUserId() ?? "unknown";
        var result = await _mediator.Send(new SetTournamentCountsTowardHandicapCommand(roundId, body.CountsTowardHandicap, userId), cancellationToken);
        return result.ToOkResult();
    }

    /// <summary>
    /// PUT /v1/tournament-rounds/{id}/substitute-skins — whether substitutes
    /// can win skins in this tournament. Gated by the
    /// tournament_substitute_skins_toggle_enabled feature flag.
    /// </summary>
    [Function("SetTournamentSubstituteSkins")]
    public async Task<IActionResult> SetTournamentSubstituteSkins(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "v1/tournament-rounds/{id}/substitute-skins")] HttpRequest req,
        string id,
        CancellationToken cancellationToken)
    {
        var authError = req.RequireRole("admin");
        if (authError is not null) return authError;

        if (!int.TryParse(id, out var roundId))
            return new BadRequestObjectResult(new { error = "Invalid round ID." });

        var flag = await _featureFlags.GetAsync(KnownFeatureFlags.TournamentSubstituteSkinsToggleEnabled, cancellationToken);
        if (!(flag?.Enabled ?? KnownFeatureFlags.Defaults[KnownFeatureFlags.TournamentSubstituteSkinsToggleEnabled]))
            return new NotFoundObjectResult(new { error = "Substitute skins toggle is not enabled." });

        var body = await req.TryDeserializeAsync<SetSubstituteSkinsRequest>(cancellationToken);
        if (body is null)
            return new BadRequestObjectResult(new { error = "Request body is required." });

        var userId = req.GetUserId() ?? "unknown";
        var result = await _mediator.Send(new SetTournamentSubstituteSkinsCommand(roundId, body.SubstitutesCanWinSkins, userId), cancellationToken);
        return result.ToOkResult();
    }

    [Function("GetTournamentComments")]
    public async Task<IActionResult> GetTournamentComments(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/tournament-rounds/{id}/comments")] HttpRequest req,
        string id,
        CancellationToken cancellationToken)
    {
        if (!int.TryParse(id, out var roundId))
            return new BadRequestObjectResult(new { error = "Invalid round ID." });

        var result = await _mediator.Send(new GetTournamentCommentsQuery(roundId), cancellationToken);
        return result.ToOkResult();
    }

    [Function("PostTournamentComment")]
    public async Task<IActionResult> PostTournamentComment(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/tournament-rounds/{id}/comments")] HttpRequest req,
        string id,
        CancellationToken cancellationToken)
    {
        var authError = req.RequireAuthenticated();
        if (authError is not null) return authError;

        if (!int.TryParse(id, out var roundId))
            return new BadRequestObjectResult(new { error = "Invalid round ID." });

        var playerId = req.GetPlayerId();
        if (playerId is null)
            return new ConflictObjectResult(new { error = "Your account isn't linked to a player profile." });

        var body = await req.TryDeserializeAsync<PostCommentRequest>(cancellationToken);
        if (body is null || string.IsNullOrWhiteSpace(body.Message))
            return new BadRequestObjectResult(new { error = "Message is required." });

        var userId = req.GetUserId() ?? "unknown";
        var result = await _mediator.Send(
            new PostTournamentCommentCommand(roundId, playerId.Value, body.Message, userId), cancellationToken);
        return result.ToCreatedResult();
    }

    // ── Private request DTOs ────────────────────────────────────────────────────

    private sealed record MatchupInputDto(int Player1Id, int? Player2Id);

    private sealed record CreateTournamentRoundRequest(
        int SeasonId,
        int CourseId,
        string? RoundDate,
        List<int> PlayerIds,
        List<MatchupInputDto>? Matchups,
        string? Notes,
        int? LongestDriveHoleNumber,
        decimal? GrossSkinsPool,
        decimal? NetSkinsPool)
    {
        public DateOnly ResolvedDate => RoundDate is not null
            ? DateOnly.ParseExact(RoundDate, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture)
            : DateOnly.FromDateTime(DateTime.UtcNow);
    }

    private sealed record SetMatchupsRequest(List<MatchupInputDto> Matchups);

    private sealed record AddParticipantsRequest(List<int> PlayerIds);

    private sealed record HoleExtraInputDto(int HoleNumber, int? ClosestToPinPlayerId, int? LongestDrivePlayerId);

    private sealed record SaveExtrasRequest(List<HoleExtraInputDto> HoleExtras);
    private sealed record SetLongestDriveHoleRequest(int? HoleNumber);
    private sealed record SetLongestDriveWinnerRequest(int? WinnerPlayerId);
    private sealed record SetSkinsPoolRequest(decimal? GrossSkinsPool, decimal? NetSkinsPool);
    private sealed record SetCountsTowardHandicapRequest(bool CountsTowardHandicap);
    private sealed record SetSubstituteSkinsRequest(bool SubstitutesCanWinSkins);
    private sealed record PostCommentRequest(string Message);
}
