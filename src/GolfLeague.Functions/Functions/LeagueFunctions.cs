using GolfLeague.Application.Leagues;
using GolfLeague.Domain.Entities;
using GolfLeague.Domain.Interfaces;
using GolfLeague.Functions.Helpers;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;

namespace GolfLeague.Functions.Functions;

public sealed class LeagueFunctions
{
    private readonly ILeagueRepository _leagueRepository;
    private readonly IMediator _mediator;

    public LeagueFunctions(ILeagueRepository leagueRepository, IMediator mediator)
    {
        _leagueRepository = leagueRepository;
        _mediator = mediator;
    }

    /// <summary>
    /// GET /v1/admin/settings — Returns all league settings for the current league.
    /// Admin only.
    /// </summary>
    [Function("GetLeagueSettings")]
    public async Task<IActionResult> GetSettings(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/admin/settings")] HttpRequest req,
        CancellationToken cancellationToken)
    {
        var authError = req.RequireRole("admin");
        if (authError is not null) return authError;

        var result = await _mediator.Send(new GetLeagueSettingsQuery(), cancellationToken);
        return result.IsSuccess
            ? new OkObjectResult(new { data = result.Value })
            : new BadRequestObjectResult(new { error = result.Error });
    }

    /// <summary>
    /// PUT /v1/admin/settings/{key} — Upserts a league setting. Admin only.
    /// </summary>
    [Function("UpdateLeagueSetting")]
    public async Task<IActionResult> UpdateSetting(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "v1/admin/settings/{key}")] HttpRequest req,
        string key,
        CancellationToken cancellationToken)
    {
        var authError = req.RequireRole("admin");
        if (authError is not null) return authError;

        var body = await req.TryDeserializeAsync<UpdateSettingRequest>(cancellationToken);
        if (body is null)
            return new BadRequestObjectResult(new { error = "Request body is required." });

        var userId = req.GetUserId() ?? "unknown";
        var result = await _mediator.Send(new UpdateLeagueSettingCommand(key, body.Value, userId), cancellationToken);
        return result.IsSuccess
            ? new OkObjectResult(new { data = result.Value })
            : new BadRequestObjectResult(new { error = result.Error });
    }

    private sealed record UpdateSettingRequest(string Value);

    /// <summary>
    /// GET /v1/leagues — Public. Returns active leagues for anonymous callers;
    /// SuperAdmin gets all leagues including inactive ones.
    /// </summary>
    [Function("GetLeagues")]
    public async Task<IActionResult> GetLeagues(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/leagues")] HttpRequest req,
        CancellationToken cancellationToken)
    {
        var isSuperAdmin = req.IsSuperAdmin();
        var leagues = await _leagueRepository.GetAllAsync(cancellationToken);

        var data = leagues
            .Where(l => isSuperAdmin || l.IsActive)
            .Select(l => new LeagueDto(l.Id, l.Name, l.Slug, l.IsActive, l.CreatedAt));

        return new OkObjectResult(new { data });
    }

    /// <summary>
    /// GET /v1/leagues/{slug} — Public. Returns the league matching the slug
    /// (used by the frontend to resolve a subdomain to a league name/config).
    /// </summary>
    [Function("GetLeagueBySlug")]
    public async Task<IActionResult> GetLeagueBySlug(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/leagues/{slug}")] HttpRequest req,
        string slug,
        CancellationToken cancellationToken)
    {
        var league = await _leagueRepository.GetBySlugAsync(slug, cancellationToken);
        if (league is null)
            return new NotFoundObjectResult(new { error = "League not found." });

        return new OkObjectResult(new { data = new LeagueDto(league.Id, league.Name, league.Slug, league.IsActive, league.CreatedAt) });
    }

    /// <summary>
    /// POST /v1/leagues — SuperAdmin only. Creates a new league.
    /// </summary>
    [Function("CreateLeague")]
    public async Task<IActionResult> CreateLeague(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/leagues")] HttpRequest req,
        CancellationToken cancellationToken)
    {
        var authError = req.RequireSuperAdmin();
        if (authError is not null) return authError;

        var body = await req.TryDeserializeAsync<CreateLeagueRequest>(cancellationToken);
        if (body is null || string.IsNullOrWhiteSpace(body.Name) || string.IsNullOrWhiteSpace(body.Slug))
            return new BadRequestObjectResult(new { error = "Name and slug are required." });

        var slug = body.Slug.ToLowerInvariant().Trim();
        if (!IsValidSlug(slug))
            return new BadRequestObjectResult(new { error = "Slug must be lowercase letters, numbers, and hyphens only." });

        var existing = await _leagueRepository.GetBySlugAsync(slug, cancellationToken);
        if (existing is not null)
            return new ConflictObjectResult(new { error = "A league with that slug already exists." });

        var league = new League
        {
            Name = body.Name.Trim(),
            Slug = slug,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        };

        await _leagueRepository.AddAsync(league, cancellationToken);
        return new CreatedResult($"/v1/leagues/{league.Slug}",
            new { data = new LeagueDto(league.Id, league.Name, league.Slug, league.IsActive, league.CreatedAt) });
    }

    /// <summary>
    /// PATCH /v1/leagues/{id} — SuperAdmin only. Updates league name or active status.
    /// </summary>
    [Function("UpdateLeague")]
    public async Task<IActionResult> UpdateLeague(
        [HttpTrigger(AuthorizationLevel.Anonymous, "patch", Route = "v1/leagues/{id:int}")] HttpRequest req,
        int id,
        CancellationToken cancellationToken)
    {
        var authError = req.RequireSuperAdmin();
        if (authError is not null) return authError;

        var league = await _leagueRepository.GetByIdAsync(id, cancellationToken);
        if (league is null)
            return new NotFoundObjectResult(new { error = "League not found." });

        var body = await req.TryDeserializeAsync<UpdateLeagueRequest>(cancellationToken);
        if (body is null)
            return new BadRequestObjectResult(new { error = "Request body is required." });

        if (!string.IsNullOrWhiteSpace(body.Name))
            league.Name = body.Name.Trim();

        if (body.IsActive.HasValue)
            league.IsActive = body.IsActive.Value;

        await _leagueRepository.UpdateAsync(league, cancellationToken);
        return new OkObjectResult(new { data = new LeagueDto(league.Id, league.Name, league.Slug, league.IsActive, league.CreatedAt) });
    }

    private static bool IsValidSlug(string slug)
        => slug.Length > 0 && slug.All(c => char.IsAsciiLetterLower(c) || char.IsAsciiDigit(c) || c == '-');

    private sealed record LeagueDto(int Id, string Name, string Slug, bool IsActive, DateTime CreatedAt);
    private sealed record CreateLeagueRequest(string Name, string Slug);
    private sealed record UpdateLeagueRequest(string? Name, bool? IsActive);
}
