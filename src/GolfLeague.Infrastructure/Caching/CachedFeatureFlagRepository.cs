using GolfLeague.Domain.Entities;
using GolfLeague.Domain.Interfaces;
using Microsoft.Extensions.Caching.Memory;

namespace GolfLeague.Infrastructure.Caching;

/// <summary>
/// Caches feature flags in-process — checked on nearly every request to gate
/// UI/behavior, but change only when an admin flips one. Same TTL-plus-
/// same-instance-invalidation approach as <see cref="CachedLeagueSettingRepository"/>.
/// </summary>
public sealed class CachedFeatureFlagRepository : IFeatureFlagRepository
{
    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(5);
    private const string AllKey = "feature-flags:all";
    private static string OneKey(string key) => $"feature-flags:one:{key}";

    private readonly IFeatureFlagRepository _inner;
    private readonly IMemoryCache _cache;

    public CachedFeatureFlagRepository(IFeatureFlagRepository inner, IMemoryCache cache)
    {
        _inner = inner;
        _cache = cache;
    }

    public async Task<IReadOnlyList<FeatureFlag>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        if (_cache.TryGetValue<IReadOnlyList<FeatureFlag>>(AllKey, out var cached) && cached is not null)
            return cached;

        var value = await _inner.GetAllAsync(cancellationToken);
        _cache.Set(AllKey, value, Ttl);
        return value;
    }

    public async Task<FeatureFlag?> GetAsync(string key, CancellationToken cancellationToken = default)
    {
        var cacheKey = OneKey(key);
        if (_cache.TryGetValue<FeatureFlag?>(cacheKey, out var cached))
            return cached;

        var value = await _inner.GetAsync(key, cancellationToken);
        _cache.Set(cacheKey, value, Ttl);
        return value;
    }

    public async Task UpsertAsync(string key, bool enabled, CancellationToken cancellationToken = default)
    {
        await _inner.UpsertAsync(key, enabled, cancellationToken);
        _cache.Remove(OneKey(key));
        _cache.Remove(AllKey);
    }
}
