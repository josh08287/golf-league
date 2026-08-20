using GolfLeague.Domain.Entities;
using GolfLeague.Domain.Interfaces;
using Microsoft.Extensions.Caching.Memory;

namespace GolfLeague.Infrastructure.Caching;

/// <summary>
/// Caches course/hole/tee-box data in-process — near-static reference data
/// (courses are edited rarely, by an admin) that was otherwise re-queried on
/// every round-creation, statistics, and scorecard request. Writes are rare
/// enough that a full-cache clear on any write is simpler and safe, rather
/// than tracking per-course invalidation through tee-box/hole mutations that
/// don't carry the owning course ID directly.
/// </summary>
public sealed class CachedCourseRepository : ICourseRepository
{
    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(15);
    private const string AllKey = "courses:all";
    private static string ByIdKey(int id) => $"courses:byid:{id}";
    private static string HolesKey(int courseId) => $"courses:holes:{courseId}";

    private readonly ICourseRepository _inner;
    private readonly IMemoryCache _cache;

    // IMemoryCache has no key-enumeration API, so course IDs ever cached are
    // tracked here to invalidate their by-ID/holes entries precisely on any
    // write, instead of leaving them to expire on the TTL after an edit.
    private static readonly HashSet<int> KnownCourseIds = [];
    private static readonly object KnownCourseIdsLock = new();

    public CachedCourseRepository(ICourseRepository inner, IMemoryCache cache)
    {
        _inner = inner;
        _cache = cache;
    }

    public async Task<Course?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var cacheKey = ByIdKey(id);
        if (_cache.TryGetValue<Course?>(cacheKey, out var cached))
            return cached;

        var value = await _inner.GetByIdAsync(id, cancellationToken);
        _cache.Set(cacheKey, value, Ttl);
        TrackCourseId(id);
        return value;
    }

    public async Task<IReadOnlyList<Course>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        if (_cache.TryGetValue<IReadOnlyList<Course>>(AllKey, out var cached) && cached is not null)
            return cached;

        var value = await _inner.GetAllAsync(cancellationToken);
        _cache.Set(AllKey, value, Ttl);
        foreach (var course in value) TrackCourseId(course.Id);
        return value;
    }

    public async Task<IReadOnlyList<CourseHole>> GetHolesAsync(int courseId, CancellationToken cancellationToken = default)
    {
        var cacheKey = HolesKey(courseId);
        if (_cache.TryGetValue<IReadOnlyList<CourseHole>>(cacheKey, out var cached) && cached is not null)
            return cached;

        var value = await _inner.GetHolesAsync(courseId, cancellationToken);
        _cache.Set(cacheKey, value, Ttl);
        TrackCourseId(courseId);
        return value;
    }

    public async Task AddAsync(Course course, CancellationToken cancellationToken = default)
    {
        await _inner.AddAsync(course, cancellationToken);
        InvalidateAll();
    }

    public async Task UpdateHolesAsync(int courseId, IEnumerable<CourseHole> holes, CancellationToken cancellationToken = default)
    {
        await _inner.UpdateHolesAsync(courseId, holes, cancellationToken);
        InvalidateAll();
    }

    public async Task DeleteAsync(int courseId, CancellationToken cancellationToken = default)
    {
        await _inner.DeleteAsync(courseId, cancellationToken);
        InvalidateAll();
    }

    public async Task AddTeeBoxAsync(TeeBox teeBox, CancellationToken cancellationToken = default)
    {
        await _inner.AddTeeBoxAsync(teeBox, cancellationToken);
        InvalidateAll();
    }

    public async Task UpdateHoleTeeBoxesAsync(int teeBoxId, IEnumerable<HoleTeeBox> holeTeeBoxes, CancellationToken cancellationToken = default)
    {
        await _inner.UpdateHoleTeeBoxesAsync(teeBoxId, holeTeeBoxes, cancellationToken);
        InvalidateAll();
    }

    private static void TrackCourseId(int id)
    {
        lock (KnownCourseIdsLock) { KnownCourseIds.Add(id); }
    }

    private void InvalidateAll()
    {
        _cache.Remove(AllKey);
        int[] ids;
        lock (KnownCourseIdsLock) { ids = [.. KnownCourseIds]; }
        foreach (var id in ids)
        {
            _cache.Remove(ByIdKey(id));
            _cache.Remove(HolesKey(id));
        }
    }
}
