using GolfLeague.Application.Common;
using GolfLeague.Domain.Entities;
using GolfLeague.Domain.Interfaces;
using MediatR;

namespace GolfLeague.Application.Admin;

public sealed record AuditLogEntryDto(
    int Id,
    string Timestamp,
    string Action,
    string EntityType,
    string Entity,
    string User,
    string? Details);

public sealed record AuditLogPageDto(
    List<AuditLogEntryDto> Items,
    int TotalCount,
    int Page,
    int PageSize);

public sealed record GetAuditLogQuery(int Page, int PageSize, SortRequest? Sort = null)
    : IRequest<Result<AuditLogPageDto>>;

public sealed class GetAuditLogQueryHandler : IRequestHandler<GetAuditLogQuery, Result<AuditLogPageDto>>
{
    private readonly IAuditRepository _auditRepository;
    private readonly IAppUserRepository _appUserRepository;
    private readonly IPlayerRepository _playerRepository;
    private readonly IRoundRepository _roundRepository;
    private readonly IFlightRepository _flightRepository;
    private readonly ICourseRepository _courseRepository;

    /// <summary>
    /// Default sort: newest entry first (matches the repo's existing default).
    /// </summary>
    private static readonly SortMap<AuditLogEntryDto> SortMap = new SortMap<AuditLogEntryDto>(
            source => source.OrderByDescending(a => a.Timestamp))
        .Add("timestamp", a => a.Timestamp)
        .Add("action", a => a.Action)
        .Add("entityType", a => a.EntityType)
        .Add("entity", a => a.Entity)
        .Add("user", a => a.User);

    public GetAuditLogQueryHandler(
        IAuditRepository auditRepository,
        IAppUserRepository appUserRepository,
        IPlayerRepository playerRepository,
        IRoundRepository roundRepository,
        IFlightRepository flightRepository,
        ICourseRepository courseRepository)
    {
        _auditRepository = auditRepository;
        _appUserRepository = appUserRepository;
        _playerRepository = playerRepository;
        _roundRepository = roundRepository;
        _flightRepository = flightRepository;
        _courseRepository = courseRepository;
    }

    public async Task<Result<AuditLogPageDto>> Handle(GetAuditLogQuery request, CancellationToken cancellationToken)
    {
        // The repo paginates server-side already, but to support arbitrary
        // sort columns we need the full set in memory. League scale (audit
        // entries grow ~slow) makes this fine; if it becomes a problem we'd
        // push ORDER BY into SQL via a per-column expression map.
        var (items, totalCount) = await _auditRepository.GetPagedAsync(1, int.MaxValue, cancellationToken);

        var userNamesById = await ResolveUserNamesAsync(items, cancellationToken);
        var entityNamesByTypeAndId = await ResolveEntityNamesAsync(items, cancellationToken);

        var dtos = items.Select(a => new AuditLogEntryDto(
            a.Id,
            a.Timestamp.ToString("O"),
            a.Action,
            a.EntityType,
            entityNamesByTypeAndId.TryGetValue((a.EntityType, a.EntityId), out var entityName)
                ? entityName
                : $"{a.EntityType} #{a.EntityId}",
            userNamesById.TryGetValue(a.UserId, out var userName) ? userName : "Unknown user",
            a.AfterJson
        )).ToList();

        var sorted = SortMap.Apply(dtos, request.Sort);
        var paged = sorted
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToList();

        return Result<AuditLogPageDto>.Ok(new AuditLogPageDto(paged, totalCount, request.Page, request.PageSize));
    }

    /// <summary>
    /// Resolves each distinct UserId to a display name: the linked Player's
    /// full name if one exists in any league, else the AppUser's email,
    /// else "Unknown user" (covers accounts since deleted).
    /// </summary>
    private async Task<Dictionary<string, string>> ResolveUserNamesAsync(
        IReadOnlyList<AuditLog> items, CancellationToken cancellationToken)
    {
        var userIds = items
            .Select(a => a.UserId)
            .Distinct()
            .Where(id => Guid.TryParse(id, out _))
            .Select(Guid.Parse)
            .ToList();

        var users = await _appUserRepository.GetByIdsAsync(userIds, cancellationToken);
        var players = await _playerRepository.GetAllAsync(cancellationToken);
        var playerNameByAppUserId = players
            .Where(p => p.AppUserId.HasValue)
            .GroupBy(p => p.AppUserId!.Value)
            .ToDictionary(g => g.Key, g => $"{g.First().FirstName} {g.First().LastName}".Trim());

        var result = new Dictionary<string, string>();
        foreach (var user in users)
        {
            var name = playerNameByAppUserId.TryGetValue(user.Id, out var playerName) && playerName.Length > 0
                ? playerName
                : user.Email ?? "Unknown user";
            result[user.Id.ToString()] = name;
        }
        return result;
    }

    /// <summary>
    /// Resolves each distinct (EntityType, EntityId) pair to a display label.
    /// Falls back to "{EntityType} #{EntityId}" in the caller when nothing
    /// matches (entity since deleted, or an EntityType this method doesn't
    /// yet know how to label).
    /// </summary>
    private async Task<Dictionary<(string EntityType, string EntityId), string>> ResolveEntityNamesAsync(
        IReadOnlyList<AuditLog> items, CancellationToken cancellationToken)
    {
        var result = new Dictionary<(string, string), string>();

        var playerIds = IdsFor(items, "Player");
        if (playerIds.Count > 0)
        {
            var players = await _playerRepository.GetAllAsync(cancellationToken);
            foreach (var p in players.Where(p => playerIds.Contains(p.Id)))
                result[("Player", p.Id.ToString())] = $"{p.FirstName} {p.LastName}".Trim();
        }

        var roundIds = IdsFor(items, "Round");
        if (roundIds.Count > 0)
        {
            var rounds = await _roundRepository.GetAllAsync(cancellationToken);
            foreach (var r in rounds.Where(r => roundIds.Contains(r.Id)))
                result[("Round", r.Id.ToString())] = $"Week {r.WeekNumber} — {r.RoundDate:MMM d, yyyy}";
        }

        var flightIds = IdsFor(items, "Flight");
        if (flightIds.Count > 0)
        {
            var flights = await _flightRepository.GetAllAsync(cancellationToken);
            foreach (var f in flights.Where(f => flightIds.Contains(f.Id)))
                result[("Flight", f.Id.ToString())] = f.Name;
        }

        var courseIds = IdsFor(items, "Course");
        if (courseIds.Count > 0)
        {
            var courses = await _courseRepository.GetAllAsync(cancellationToken);
            foreach (var c in courses.Where(c => courseIds.Contains(c.Id)))
                result[("Course", c.Id.ToString())] = c.Name;
        }

        return result;
    }

    private static HashSet<int> IdsFor(IReadOnlyList<AuditLog> items, string entityType) =>
        items
            .Where(a => a.EntityType == entityType && int.TryParse(a.EntityId, out _))
            .Select(a => int.Parse(a.EntityId))
            .ToHashSet();
}
