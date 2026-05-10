using GolfLeague.Application.Common;
using GolfLeague.Application.Players.Commands;
using GolfLeague.Application.Players.Queries;
using GolfLeague.Domain.Interfaces;
using GolfLeague.Functions.Helpers;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;

namespace GolfLeague.Functions.Functions;

public sealed class PlayerFunctions
{
    private readonly IMediator _mediator;
    private readonly IPlayerRepository _playerRepository;

    public PlayerFunctions(IMediator mediator, IPlayerRepository playerRepository)
    {
        _mediator = mediator;
        _playerRepository = playerRepository;
    }

    [Function("GetPlayers")]
    public async Task<IActionResult> GetPlayers(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/players")] HttpRequest req,
        CancellationToken cancellationToken)
    {
        var page = int.TryParse(req.Query["page"], out var p) ? p : 1;
        var pageSize = int.TryParse(req.Query["pageSize"], out var ps) ? ps : 20;
        var sort = SortRequest.TryParse(req.Query["sortBy"], req.Query["sortDir"]);

        var result = await _mediator.Send(new GetPlayersQuery(page, pageSize, sort), cancellationToken);
        return result.ToOkResult();
    }

    [Function("GetPlayer")]
    public async Task<IActionResult> GetPlayer(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/players/{id}")] HttpRequest req,
        string id,
        CancellationToken cancellationToken)
    {
        if (!int.TryParse(id, out var playerId))
            return new BadRequestObjectResult(new { error = "Invalid player ID." });

        var result = await _mediator.Send(new GetPlayerQuery(playerId), cancellationToken);
        return result.ToOkResult();
    }

    [Function("CreatePlayer")]
    public async Task<IActionResult> CreatePlayer(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/players")] HttpRequest req,
        CancellationToken cancellationToken)
    {
        var authError = req.RequireRole("admin");
        if (authError is not null) return authError;

        var body = await req.TryDeserializeAsync<CreatePlayerRequest>(cancellationToken);

        if (body is null)
            return new BadRequestObjectResult(new { error = "Request body is required." });

        var nameParts = body.Name.Split(' ', 2, StringSplitOptions.TrimEntries);
        var firstName = nameParts[0];
        var lastName = nameParts.Length > 1 ? nameParts[1] : string.Empty;

        int? flightId = null;
        if (!string.IsNullOrEmpty(body.FlightId) && int.TryParse(body.FlightId, out var fid))
            flightId = fid;

        var userId = req.GetUserId() ?? "unknown";
        var command = new CreatePlayerCommand(
            firstName, lastName, body.Email, body.InitialHandicap, userId, flightId,
            body.Role ?? "player");

        var result = await _mediator.Send(command, cancellationToken);
        if (!result.IsSuccess)
            return result.ToOkResult();

        return result.ToCreatedResult($"/api/v1/players/{result.Value?.Id}");
    }

    [Function("UpdatePlayer")]
    public async Task<IActionResult> UpdatePlayer(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "v1/players/{id}")] HttpRequest req,
        string id,
        CancellationToken cancellationToken)
    {
        var authError = req.RequireRole("admin");
        if (authError is not null) return authError;

        if (!int.TryParse(id, out var playerId))
            return new BadRequestObjectResult(new { error = "Invalid player ID." });

        var body = await req.TryDeserializeAsync<UpdatePlayerRequest>(cancellationToken);

        if (body is null)
            return new BadRequestObjectResult(new { error = "Request body is required." });

        var userId = req.GetUserId() ?? "unknown";
        var result = await _mediator.Send(new UpdatePlayerCommand(playerId, body.FirstName, body.LastName, body.Email, userId), cancellationToken);
        return result.ToOkResult();
    }

    [Function("PatchPlayer")]
    public async Task<IActionResult> PatchPlayer(
        [HttpTrigger(AuthorizationLevel.Anonymous, "patch", Route = "v1/players/{id}")] HttpRequest req,
        string id,
        CancellationToken cancellationToken)
    {
        var authError = req.RequireRole("admin");
        if (authError is not null) return authError;

        if (!int.TryParse(id, out var playerId))
            return new BadRequestObjectResult(new { error = "Invalid player ID." });

        var body = await req.TryDeserializeAsync<PatchPlayerRequest>(cancellationToken);

        if (body is null)
            return new BadRequestObjectResult(new { error = "Request body is required." });

        var playerEntity = await _playerRepository.GetByIdAsync(playerId, cancellationToken);
        if (playerEntity is null)
            return new NotFoundObjectResult(new { error = $"Player with ID {playerId} not found." });

        string firstName = playerEntity.FirstName;
        string lastName = playerEntity.LastName;
        string email = playerEntity.Email;

        if (!string.IsNullOrWhiteSpace(body.Name))
        {
            var parts = body.Name.Split(' ', 2);
            firstName = parts[0];
            lastName = parts.Length > 1 ? parts[1] : string.Empty;
        }

        if (!string.IsNullOrWhiteSpace(body.Email))
            email = body.Email;

        if (body.FlightId is not null)
        {
            int? flightId = body.FlightId == "" ? null : int.TryParse(body.FlightId, out var fid) ? fid : null;
            await _playerRepository.AssignToFlightAsync(playerId, flightId, cancellationToken);
        }

        var userId = req.GetUserId() ?? "unknown";
        var result = await _mediator.Send(new UpdatePlayerCommand(playerId, firstName, lastName, email, userId, body.Role), cancellationToken);
        return result.ToOkResult();
    }

    [Function("DeletePlayer")]
    public async Task<IActionResult> DeletePlayer(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "v1/players/{id}")] HttpRequest req,
        string id,
        CancellationToken cancellationToken)
    {
        var authError = req.RequireRole("admin");
        if (authError is not null) return authError;

        if (!int.TryParse(id, out var playerId))
            return new BadRequestObjectResult(new { error = "Invalid player ID." });

        var userId = req.GetUserId() ?? "unknown";
        var result = await _mediator.Send(new DeletePlayerCommand(playerId, userId), cancellationToken);
        return result.ToOkResult();
    }

    [Function("DeactivatePlayerPost")]
    public async Task<IActionResult> DeactivatePlayerPost(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/players/{id}/deactivate")] HttpRequest req,
        string id,
        CancellationToken cancellationToken)
    {
        var authError = req.RequireRole("admin");
        if (authError is not null) return authError;

        if (!int.TryParse(id, out var playerId))
            return new BadRequestObjectResult(new { error = "Invalid player ID." });

        var userId = req.GetUserId() ?? "unknown";
        var result = await _mediator.Send(new DeactivatePlayerCommand(playerId, userId), cancellationToken);
        return result.ToOkResult();
    }

    [Function("GetPlayerRounds")]
    public async Task<IActionResult> GetPlayerRounds(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/players/{id}/rounds")] HttpRequest req,
        string id,
        CancellationToken cancellationToken)
    {
        if (!int.TryParse(id, out var playerId))
            return new BadRequestObjectResult(new { error = "Invalid player ID." });

        var sort = SortRequest.TryParse(req.Query["sortBy"], req.Query["sortDir"]);
        var result = await _mediator.Send(new GetPlayerRoundsQuery(playerId, sort), cancellationToken);
        return result.ToOkResult();
    }

    [Function("GetHandicapHistory")]
    public async Task<IActionResult> GetHandicapHistory(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/players/{id}/handicap-history")] HttpRequest req,
        string id,
        CancellationToken cancellationToken)
    {
        if (!int.TryParse(id, out var playerId))
            return new BadRequestObjectResult(new { error = "Invalid player ID." });

        var sort = SortRequest.TryParse(req.Query["sortBy"], req.Query["sortDir"]);
        var result = await _mediator.Send(new GetHandicapHistoryQuery(playerId, sort), cancellationToken);
        return result.ToOkResult();
    }

    [Function("SetHandicapPost")]
    public async Task<IActionResult> SetHandicapPost(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/players/{id}/handicap")] HttpRequest req,
        string id,
        CancellationToken cancellationToken)
    {
        var authError = req.RequireRole("admin");
        if (authError is not null) return authError;

        if (!int.TryParse(id, out var playerId))
            return new BadRequestObjectResult(new { error = "Invalid player ID." });

        var body = await req.TryDeserializeAsync<SetHandicapRequest>(cancellationToken);

        if (body is null)
            return new BadRequestObjectResult(new { error = "Request body is required." });

        var userId = req.GetUserId() ?? "unknown";
        var result = await _mediator.Send(new SetHandicapCommand(playerId, body.ResolvedIndex, body.Notes, userId), cancellationToken);
        return result.ToOkResult();
    }

    [Function("SetHandicap")]
    public async Task<IActionResult> SetHandicap(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "v1/players/{id}/handicap")] HttpRequest req,
        string id,
        CancellationToken cancellationToken)
    {
        var authError = req.RequireRole("admin");
        if (authError is not null) return authError;

        if (!int.TryParse(id, out var playerId))
            return new BadRequestObjectResult(new { error = "Invalid player ID." });

        var body = await req.TryDeserializeAsync<SetHandicapRequest>(cancellationToken);

        if (body is null)
            return new BadRequestObjectResult(new { error = "Request body is required." });

        var userId = req.GetUserId() ?? "unknown";
        var result = await _mediator.Send(new SetHandicapCommand(playerId, body.ResolvedIndex, body.Notes, userId), cancellationToken);
        return result.ToOkResult();
    }

    private sealed record CreatePlayerRequest(string Name, string Email, double InitialHandicap, string? FlightId, string? Role);
    private sealed record UpdatePlayerRequest(string FirstName, string LastName, string Email);
    private sealed record PatchPlayerRequest(string? Name, string? Email, string? FlightId, string? Role);
    private sealed record SetHandicapRequest(double? NewIndex, double? HandicapIndex, string? Notes)
    {
        public double ResolvedIndex => NewIndex ?? HandicapIndex ?? 0;
    }
}
