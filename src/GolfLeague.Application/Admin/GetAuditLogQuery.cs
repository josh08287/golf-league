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
    private readonly ISeasonRepository _seasonRepository;
    private readonly IInviteRepository _inviteRepository;
    private readonly ITeeTimeRepository _teeTimeRepository;

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
        ICourseRepository courseRepository,
        ISeasonRepository seasonRepository,
        IInviteRepository inviteRepository,
        ITeeTimeRepository teeTimeRepository)
    {
        _auditRepository = auditRepository;
        _appUserRepository = appUserRepository;
        _playerRepository = playerRepository;
        _roundRepository = roundRepository;
        _flightRepository = flightRepository;
        _courseRepository = courseRepository;
        _seasonRepository = seasonRepository;
        _inviteRepository = inviteRepository;
        _teeTimeRepository = teeTimeRepository;
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
                : DefaultEntityLabel(a.EntityType, a.EntityId),
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
    /// yet know how to label). EntityTypes whose EntityId is already
    /// human-readable at write time (FeatureFlag/LeagueSetting keys, a
    /// Broadcast subject, an Invite's email list) need no lookup here — the
    /// raw id passes straight through as the display label.
    /// </summary>
    private async Task<Dictionary<(string EntityType, string EntityId), string>> ResolveEntityNamesAsync(
        IReadOnlyList<AuditLog> items, CancellationToken cancellationToken)
    {
        var result = new Dictionary<(string, string), string>();

        // Rounds are needed both for "Round" entities and to label "TeeTime"
        // entities below — load once, lazily, and share between both.
        Dictionary<int, Domain.Entities.Round>? roundsById = null;
        async Task<Dictionary<int, Domain.Entities.Round>> GetRoundsByIdAsync()
            => roundsById ??= (await _roundRepository.GetAllAsync(cancellationToken)).ToDictionary(r => r.Id);

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
            var rounds = await GetRoundsByIdAsync();
            foreach (var r in rounds.Values.Where(r => roundIds.Contains(r.Id)))
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

        var seasonIds = IdsFor(items, "Season");
        if (seasonIds.Count > 0)
        {
            var seasons = await _seasonRepository.GetAllAsync(cancellationToken);
            foreach (var s in seasons.Where(s => seasonIds.Contains(s.Id)))
                result[("Season", s.Id.ToString())] = $"{s.Name} ({s.Year})";
        }

        var halfIds = IdsFor(items, "SeasonHalf");
        if (halfIds.Count > 0)
        {
            var halves = await _flightRepository.GetHalvesByIdsAsync(halfIds, cancellationToken);
            foreach (var half in halves)
                result[("SeasonHalf", half.Id.ToString())] = half.Name;
        }

        var inviteIds = IdsFor(items, "Invite");
        if (inviteIds.Count > 0)
        {
            var invites = await _inviteRepository.GetAllAsync(cancellationToken);
            foreach (var i in invites.Where(i => inviteIds.Contains(i.Id)))
                result[("Invite", i.Id.ToString())] = i.Email;
        }

        var teeTimeIds = IdsFor(items, "TeeTime");
        if (teeTimeIds.Count > 0)
        {
            var slots = await _teeTimeRepository.GetByIdsAsync(teeTimeIds, cancellationToken);
            var rounds = await GetRoundsByIdAsync();

            foreach (var slot in slots)
            {
                result[("TeeTime", slot.Id.ToString())] = rounds.TryGetValue(slot.RoundId, out var round)
                    ? $"Week {round.WeekNumber} — {round.RoundDate:MMM d, yyyy}, {slot.ScheduledTime:h:mm tt}"
                    : $"Tee time at {slot.ScheduledTime:h:mm tt}";
            }
        }

        // AdminUser/Session EntityIds are the AppUser's Guid — same lookup as
        // the User column, just keyed under a different EntityType.
        var accountEntityIds = items
            .Where(a => a.EntityType is "AdminUser" or "Session" && Guid.TryParse(a.EntityId, out _))
            .Select(a => (a.EntityType, a.EntityId))
            .Distinct()
            .ToList();
        if (accountEntityIds.Count > 0)
        {
            var userIds = accountEntityIds.Select(x => Guid.Parse(x.EntityId)).Distinct().ToList();
            var users = await _appUserRepository.GetByIdsAsync(userIds, cancellationToken);
            var players = await _playerRepository.GetAllAsync(cancellationToken);
            var playerNameByAppUserId = players
                .Where(p => p.AppUserId.HasValue)
                .GroupBy(p => p.AppUserId!.Value)
                .ToDictionary(g => g.Key, g => $"{g.First().FirstName} {g.First().LastName}".Trim());

            foreach (var user in users)
            {
                var name = playerNameByAppUserId.TryGetValue(user.Id, out var playerName) && playerName.Length > 0
                    ? playerName
                    : user.Email ?? "Unknown user";
                foreach (var (entityType, entityId) in accountEntityIds.Where(x => x.EntityId == user.Id.ToString()))
                    result[(entityType, entityId)] = name;
            }
        }

        return result;
    }

    private static HashSet<int> IdsFor(IReadOnlyList<AuditLog> items, string entityType) =>
        items
            .Where(a => a.EntityType == entityType && int.TryParse(a.EntityId, out _))
            .Select(a => int.Parse(a.EntityId))
            .ToHashSet();

    /// <summary>
    /// Entity types whose EntityId is already a human-readable value at
    /// write time (a setting/flag key, a broadcast subject, an invite's
    /// email list) render as-is instead of the generic "{Type} #{Id}" form.
    /// </summary>
    private static string DefaultEntityLabel(string entityType, string entityId) => entityType switch
    {
        "FeatureFlag" or "LeagueSetting" or "Broadcast" => entityId,
        "Invite" when !int.TryParse(entityId, out _) => entityId, // bulk-create: comma-joined emails
        _ => $"{entityType} #{entityId}",
    };
}
