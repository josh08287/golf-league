using GolfLeague.Domain.Entities;
using GolfLeague.Domain.Interfaces;
using Microsoft.Extensions.Caching.Memory;

namespace GolfLeague.Infrastructure.Caching;

/// <summary>
/// Caches league settings in-process — feature flags, the tee-time cutoff
/// time, etc. are read on nearly every request but change rarely, so this
/// was one of the highest-volume repeated-SQL-round-trip sources in the app.
/// A short TTL (rather than no expiry) bounds how long a stale value can
/// survive a write from another Function App instance; same-instance writes
/// invalidate immediately via <see cref="UpsertAsync"/>.
/// </summary>
public sealed class CachedLeagueSettingRepository : ILeagueSettingRepository
{
    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(5);

    private readonly ILeagueSettingRepository _inner;
    private readonly IMemoryCache _cache;

    public CachedLeagueSettingRepository(ILeagueSettingRepository inner, IMemoryCache cache)
    {
        _inner = inner;
        _cache = cache;
    }

    private static string AllKey(int leagueId) => $"league-settings:all:{leagueId}";
    private static string OneKey(int leagueId, string key) => $"league-settings:one:{leagueId}:{key}";

    public async Task<IReadOnlyList<LeagueSetting>> GetAllAsync(int leagueId, CancellationToken cancellationToken = default)
    {
        if (_cache.TryGetValue<IReadOnlyList<LeagueSetting>>(AllKey(leagueId), out var cached) && cached is not null)
            return cached;

        var value = await _inner.GetAllAsync(leagueId, cancellationToken);
        _cache.Set(AllKey(leagueId), value, Ttl);
        return value;
    }

    public async Task<LeagueSetting?> GetAsync(int leagueId, string key, CancellationToken cancellationToken = default)
    {
        var cacheKey = OneKey(leagueId, key);
        if (_cache.TryGetValue<LeagueSetting?>(cacheKey, out var cached))
            return cached;

        var value = await _inner.GetAsync(leagueId, key, cancellationToken);
        _cache.Set(cacheKey, value, Ttl);
        return value;
    }

    public async Task UpsertAsync(int leagueId, string key, string value, CancellationToken cancellationToken = default)
    {
        await _inner.UpsertAsync(leagueId, key, value, cancellationToken);

        // Invalidate rather than update in place, so the next read re-fetches
        // the row as SaveChangesAsync actually persisted it.
        _cache.Remove(OneKey(leagueId, key));
        _cache.Remove(AllKey(leagueId));
    }
}
