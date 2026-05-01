using GolfLeague.Application.Players.Commands;
using GolfLeague.Application.Players.Queries;
using GolfLeague.Functions.Helpers;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using System.Text.Json;

namespace GolfLeague.Functions.Functions;

public sealed class PlayerFunctions
{
    private readonly IMediator _mediator;

    public PlayerFunctions(IMediator mediator)
    {
        _mediator = mediator;
    }

    [Function("GetPlayers")]
    public async Task<IActionResult> GetPlayers(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/players")] HttpRequest req,
        CancellationToken cancellationToken)
    {
        var page = int.TryParse(req.Query["page"], out var p) ? p : 1;
        var pageSize = int.TryParse(req.Query["pageSize"], out var ps) ? ps : 20;

        var result = await _mediator.Send(new GetPlayersQuery(page, pageSize), cancellationToken);
        return result.ToOkResult();
    }

    [Function("GetPlayer")]
    public async Task<IActionResult> GetPlayer(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/players/{id:int}")] HttpRequest req,
        int id,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetPlayerQuery(id), cancellationToken);
        return result.ToOkResult();
    }

    [Function("CreatePlayer")]
    public async Task<IActionResult> CreatePlayer(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/players")] HttpRequest req,
        CancellationToken cancellationToken)
    {
        var authError = req.RequireRole("admin");
        if (authError is not null) return authError;

        var body = await JsonSerializer.DeserializeAsync<CreatePlayerRequest>(
            req.Body,
            JsonSerializerOptions.Web,
            cancellationToken);

        if (body is null)
            return new BadRequestObjectResult(new { error = "Request body is required." });

        var userId = req.GetUserId() ?? "unknown";
        var command = new CreatePlayerCommand(
            body.FirstName,
            body.LastName,
            body.Email,
            body.EntraObjectId,
            body.InitialHandicapIndex,
            userId);

        var result = await _mediator.Send(command, cancellationToken);
        return result.ToCreatedResult($"/api/v1/players/{result.Value?.Id}");
    }

    [Function("UpdatePlayer")]
    public async Task<IActionResult> UpdatePlayer(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "v1/players/{id:int}")] HttpRequest req,
        int id,
        CancellationToken cancellationToken)
    {
        var authError = req.RequireRole("admin");
        if (authError is not null) return authError;

        var body = await JsonSerializer.DeserializeAsync<UpdatePlayerRequest>(
            req.Body,
            JsonSerializerOptions.Web,
            cancellationToken);

        if (body is null)
            return new BadRequestObjectResult(new { error = "Request body is required." });

        var userId = req.GetUserId() ?? "unknown";
        var command = new UpdatePlayerCommand(id, body.FirstName, body.LastName, body.Email, userId);

        var result = await _mediator.Send(command, cancellationToken);
        return result.ToOkResult();
    }

    [Function("DeactivatePlayer")]
    public async Task<IActionResult> DeactivatePlayer(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "v1/players/{id:int}")] HttpRequest req,
        int id,
        CancellationToken cancellationToken)
    {
        var authError = req.RequireRole("admin");
        if (authError is not null) return authError;

        var userId = req.GetUserId() ?? "unknown";
        var result = await _mediator.Send(new DeactivatePlayerCommand(id, userId), cancellationToken);
        return result.ToOkResult();
    }

    [Function("GetHandicapHistory")]
    public async Task<IActionResult> GetHandicapHistory(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/players/{id:int}/handicap-history")] HttpRequest req,
        int id,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetHandicapHistoryQuery(id), cancellationToken);
        return result.ToOkResult();
    }

    [Function("SetHandicap")]
    public async Task<IActionResult> SetHandicap(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "v1/players/{id:int}/handicap")] HttpRequest req,
        int id,
        CancellationToken cancellationToken)
    {
        var authError = req.RequireRole("admin");
        if (authError is not null) return authError;

        var body = await JsonSerializer.DeserializeAsync<SetHandicapRequest>(
            req.Body,
            JsonSerializerOptions.Web,
            cancellationToken);

        if (body is null)
            return new BadRequestObjectResult(new { error = "Request body is required." });

        var userId = req.GetUserId() ?? "unknown";
        var command = new SetHandicapCommand(id, body.HandicapIndex, body.Notes, userId);

        var result = await _mediator.Send(command, cancellationToken);
        return result.ToOkResult();
    }

    private sealed record CreatePlayerRequest(
        string FirstName,
        string LastName,
        string Email,
        string EntraObjectId,
        double InitialHandicapIndex);

    private sealed record UpdatePlayerRequest(
        string FirstName,
        string LastName,
        string Email);

    private sealed record SetHandicapRequest(
        double HandicapIndex,
        string? Notes);
}
