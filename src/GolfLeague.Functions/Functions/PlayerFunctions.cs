using GolfLeague.Application.Common;
using GolfLeague.Application.DTOs;
using GolfLeague.Application.Interfaces;
using GolfLeague.Application.Players.Commands;
using GolfLeague.Application.Players.Queries;
using GolfLeague.Domain.Enums;
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
    private readonly IFlightRepository _flightRepository;
    private readonly IAdminUserService _adminUserService;

    public PlayerFunctions(
        IMediator mediator,
        IPlayerRepository playerRepository,
        IFlightRepository flightRepository,
        IAdminUserService adminUserService)
    {
        _mediator = mediator;
        _playerRepository = playerRepository;
        _flightRepository = flightRepository;
        _adminUserService = adminUserService;
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
            firstName, lastName, body.Email, body.InitialHandicap, userId, flightId);

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
        string? email = playerEntity.Email;

        if (!string.IsNullOrWhiteSpace(body.Name))
        {
            var parts = body.Name.Split(' ', 2);
            firstName = parts[0];
            lastName = parts.Length > 1 ? parts[1] : string.Empty;
        }

        if (body.Email is not null)
            email = string.IsNullOrWhiteSpace(body.Email) ? null : body.Email;

        if (body.FlightId is not null)
        {
            int? flightId = body.FlightId == "" ? null : int.TryParse(body.FlightId, out var fid) ? fid : null;

            // Reject reassignment if the target flight's half already has started rounds
            if (flightId is not null)
            {
                var targetFlight = await _flightRepository.GetByIdAsync(flightId.Value, cancellationToken);
                if (targetFlight is not null && await _flightRepository.IsHalfLockedAsync(targetFlight.HalfId, cancellationToken))
                    return new ObjectResult(new { error = "Flight assignments are locked once rounds have started for this half." }) { StatusCode = 409 };
            }

            await _playerRepository.AssignToFlightAsync(playerId, flightId, cancellationToken);
        }

        var userId = req.GetUserId() ?? "unknown";
        var result = await _mediator.Send(
            new UpdatePlayerCommand(playerId, firstName, lastName, email, userId, body.Roles),
            cancellationToken);
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

    /// <summary>
    /// GET /v1/admin/players/unlinked — admin-only. Lists active players that
    /// aren't yet attached to an AppUser. Powers the "link to existing user"
    /// picker and the invite pre-attach dropdown.
    ///
    /// Lives under /v1/admin/ rather than /v1/players/unlinked to avoid
    /// route collision with /v1/players/{id} — the framework would otherwise
    /// match "unlinked" as a non-numeric id and 400 on parse.
    /// </summary>
    [Function("GetUnlinkedPlayers")]
    public async Task<IActionResult> GetUnlinkedPlayers(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/admin/players/unlinked")] HttpRequest req,
        CancellationToken cancellationToken)
    {
        var authError = req.RequireRole("admin");
        if (authError is not null) return authError;

        var players = await _playerRepository.GetUnlinkedActiveAsync(cancellationToken);
        var data = players.Select(p => new
        {
            id = p.Id,
            fullName = p.FullName,
            email = p.Email,
        }).ToList();

        return new OkObjectResult(new { data });
    }

    /// <summary>
    /// POST /v1/players/{id}/link-user — admin-only. Map an existing unlinked
    /// Player to an existing AppUser.
    /// </summary>
    [Function("LinkPlayerToUser")]
    public async Task<IActionResult> LinkPlayerToUser(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/players/{id}/link-user")] HttpRequest req,
        string id,
        CancellationToken cancellationToken)
    {
        var authError = req.RequireRole("admin");
        if (authError is not null) return authError;

        if (!int.TryParse(id, out var playerId))
            return new BadRequestObjectResult(new { error = "Invalid player ID." });

        var body = await req.TryDeserializeAsync<LinkUserBody>(cancellationToken);
        if (body is null || !Guid.TryParse(body.UserId, out var userId))
            return new BadRequestObjectResult(new { error = "userId (Guid) is required." });

        var result = await _adminUserService.LinkPlayerToUserAsync(playerId, userId, cancellationToken);
        if (!result.IsSuccess)
            return new ConflictObjectResult(new { error = result.Error });

        return new OkObjectResult(new { data = result.Value });
    }

    /// <summary>
    /// PUT /v1/players/{id}/tee-time-preference
    /// Authenticated players may update their own preference; admins may
    /// update any player's preference.
    /// Body: { "preferredSlots": ["Early", "Late"] } — pass empty array for None.
    /// </summary>
    [Function("SetTeeTimePreference")]
    public async Task<IActionResult> SetTeeTimePreference(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "v1/players/{id}/tee-time-preference")] HttpRequest req,
        string id,
        CancellationToken cancellationToken)
    {
        var authError = req.RequireAuthenticated();
        if (authError is not null) return authError;

        if (!int.TryParse(id, out var playerId))
            return new BadRequestObjectResult(new { error = "Invalid player ID." });

        // Allow admin or the player themselves.
        var callingPlayerId = req.GetPlayerId();
        var isAdmin = req.HttpContext.User.IsInRole("admin")
            || req.HttpContext.User.Claims.Any(c =>
                (c.Type == System.Security.Claims.ClaimTypes.Role || c.Type == "roles")
                && c.Value.Equals("admin", StringComparison.OrdinalIgnoreCase));

        if (!isAdmin && callingPlayerId != playerId)
            return new ObjectResult(new { error = "Forbidden: you may only update your own preference." }) { StatusCode = 403 };

        var body = await req.TryDeserializeAsync<SetTeeTimePreferenceRequest>(cancellationToken);
        if (body is null)
            return new BadRequestObjectResult(new { error = "Request body is required." });

        var preference = TeeTimeSlotPreference.None;
        foreach (var slot in body.PreferredSlots ?? [])
        {
            if (Enum.TryParse<TeeTimeSlotPreference>(slot, ignoreCase: true, out var flag))
                preference |= flag;
        }

        var player = await _playerRepository.GetByIdAsync(playerId, cancellationToken);
        if (player is null)
            return new NotFoundObjectResult(new { error = $"Player {playerId} not found." });

        player.PreferredTeeTimeSlots = preference;
        await _playerRepository.UpdateAsync(player, cancellationToken);

        return new OkObjectResult(new { data = new { playerId, preferredTeeTimeSlots = (int)preference } });
    }

    /// <summary>
    /// PUT /v1/players/{id}/tee-time-email-opt-out
    /// Authenticated players may update their own opt-out preference only.
    /// Body: { "optOut": true|false }
    /// </summary>
    [Function("SetTeeTimeEmailOptOut")]
    public async Task<IActionResult> SetTeeTimeEmailOptOut(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "v1/players/{id}/tee-time-email-opt-out")] HttpRequest req,
        string id,
        CancellationToken cancellationToken)
    {
        var authError = req.RequireAuthenticated();
        if (authError is not null) return authError;

        if (!int.TryParse(id, out var playerId))
            return new BadRequestObjectResult(new { error = "Invalid player ID." });

        // Only the player themselves may change this setting.
        var callingPlayerId = req.GetPlayerId();
        if (callingPlayerId != playerId)
            return new ObjectResult(new { error = "Forbidden: you may only update your own email preference." }) { StatusCode = 403 };

        var body = await req.TryDeserializeAsync<SetTeeTimeEmailOptOutRequest>(cancellationToken);
        if (body is null)
            return new BadRequestObjectResult(new { error = "Request body is required." });

        var player = await _playerRepository.GetByIdAsync(playerId, cancellationToken);
        if (player is null)
            return new NotFoundObjectResult(new { error = $"Player {playerId} not found." });

        player.TeeTimeEmailOptOut = body.OptOut;
        await _playerRepository.UpdateAsync(player, cancellationToken);

        return new OkObjectResult(new { data = new { playerId, teeTimeEmailOptOut = body.OptOut } });
    }

    private sealed record LinkUserBody(string UserId);
    private sealed record CreatePlayerRequest(string Name, string? Email, double InitialHandicap, string? FlightId);
    private sealed record UpdatePlayerRequest(string FirstName, string LastName, string? Email);
    private sealed record PatchPlayerRequest(string? Name, string? Email, string? FlightId, IReadOnlyList<string>? Roles);
    private sealed record SetHandicapRequest(double? NewIndex, double? HandicapIndex, string? Notes)
    {
        public double ResolvedIndex => NewIndex ?? HandicapIndex ?? 0;
    }
    private sealed record SetTeeTimePreferenceRequest(IReadOnlyList<string>? PreferredSlots);
    private sealed record SetTeeTimeEmailOptOutRequest(bool OptOut);
}
