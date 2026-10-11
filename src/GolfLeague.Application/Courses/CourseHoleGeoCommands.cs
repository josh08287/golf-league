using GolfLeague.Application.Admin;
using GolfLeague.Application.Common;
using GolfLeague.Application.DTOs;
using GolfLeague.Domain.Entities;
using GolfLeague.Domain.Interfaces;
using MediatR;

namespace GolfLeague.Application.Courses;

internal static class HoleMapFlag
{
    public const string DisabledError = "Hole map not found.";

    public static async Task<bool> IsEnabledAsync(IFeatureFlagRepository flags, CancellationToken cancellationToken)
    {
        var flag = await flags.GetAsync(KnownFeatureFlags.HoleMapEnabled, cancellationToken);
        return flag?.Enabled ?? KnownFeatureFlags.Defaults[KnownFeatureFlags.HoleMapEnabled];
    }

    public static CourseHoleGeoHoleDto ToDto(CourseHoleGeo g) => new(
        g.HoleNumber,
        new GeoPointDto(g.TeeLat, g.TeeLng),
        new GeoPointDto(g.GreenLat, g.GreenLng),
        g.GreenFrontLat is { } fLat && g.GreenFrontLng is { } fLng ? new GeoPointDto(fLat, fLng) : null,
        g.GreenBackLat is { } bLat && g.GreenBackLng is { } bLng ? new GeoPointDto(bLat, bLng) : null,
        g.Source,
        g.OsmWayId);

    public static bool IsValid(GeoPointDto? p) =>
        p is null || (p.Lat is >= -90 and <= 90 && p.Lng is >= -180 and <= 180);
}

// ── Get a course's hole map ───────────────────────────────────────────────────

public sealed record GetCourseHoleGeoQuery(int CourseId) : IRequest<Result<CourseHoleGeoDto>>;

public sealed class GetCourseHoleGeoQueryHandler : IRequestHandler<GetCourseHoleGeoQuery, Result<CourseHoleGeoDto>>
{
    private readonly ICourseRepository _courses;
    private readonly ICourseHoleGeoRepository _geo;
    private readonly IFeatureFlagRepository _flags;

    public GetCourseHoleGeoQueryHandler(ICourseRepository courses, ICourseHoleGeoRepository geo, IFeatureFlagRepository flags)
    {
        _courses = courses;
        _geo = geo;
        _flags = flags;
    }

    public async Task<Result<CourseHoleGeoDto>> Handle(GetCourseHoleGeoQuery request, CancellationToken cancellationToken)
    {
        if (!await HoleMapFlag.IsEnabledAsync(_flags, cancellationToken))
            return Result<CourseHoleGeoDto>.Fail(HoleMapFlag.DisabledError);

        var course = await _courses.GetByIdAsync(request.CourseId, cancellationToken);
        if (course is null)
            return Result<CourseHoleGeoDto>.Fail($"Course with ID {request.CourseId} not found.");

        var (lat, lng) = await _geo.GetCourseCentreAsync(request.CourseId, cancellationToken);
        var holes = await _geo.GetByCourseAsync(request.CourseId, cancellationToken);
        return Result<CourseHoleGeoDto>.Ok(new CourseHoleGeoDto(
            request.CourseId, lat, lng, holes.Select(HoleMapFlag.ToDto).ToList()));
    }
}

// ── Preview an OpenStreetMap import (not saved) ───────────────────────────────

public sealed record PreviewOsmHoleGeoQuery(int CourseId, double Latitude, double Longitude) : IRequest<Result<OsmHoleGeoPreviewDto>>;

public sealed class PreviewOsmHoleGeoQueryHandler : IRequestHandler<PreviewOsmHoleGeoQuery, Result<OsmHoleGeoPreviewDto>>
{
    private readonly ICourseRepository _courses;
    private readonly IFeatureFlagRepository _flags;
    private readonly IOsmGolfImportService _osm;

    public PreviewOsmHoleGeoQueryHandler(ICourseRepository courses, IFeatureFlagRepository flags, IOsmGolfImportService osm)
    {
        _courses = courses;
        _flags = flags;
        _osm = osm;
    }

    public async Task<Result<OsmHoleGeoPreviewDto>> Handle(PreviewOsmHoleGeoQuery request, CancellationToken cancellationToken)
    {
        if (!await HoleMapFlag.IsEnabledAsync(_flags, cancellationToken))
            return Result<OsmHoleGeoPreviewDto>.Fail(HoleMapFlag.DisabledError);

        if (!HoleMapFlag.IsValid(new GeoPointDto(request.Latitude, request.Longitude)))
            return Result<OsmHoleGeoPreviewDto>.Fail("Invalid course location.");

        var course = await _courses.GetByIdAsync(request.CourseId, cancellationToken);
        if (course is null)
            return Result<OsmHoleGeoPreviewDto>.Fail($"Course with ID {request.CourseId} not found.");

        OsmGolfImportResult imported;
        try
        {
            imported = await _osm.FetchHolesAsync(request.Latitude, request.Longitude, cancellationToken);
        }
        catch (HttpRequestException)
        {
            return Result<OsmHoleGeoPreviewDto>.Fail("Could not reach OpenStreetMap. Please try again in a minute.");
        }

        var warnings = new List<string>(imported.Warnings);
        var courseHoles = course.Holes.ToDictionary(h => h.HoleNumber);
        var holes = new List<CourseHoleGeoHoleDto>();

        foreach (var hole in imported.Holes.OrderBy(h => h.HoleNumber))
        {
            if (!courseHoles.TryGetValue(hole.HoleNumber, out var courseHole))
            {
                warnings.Add($"OpenStreetMap hole {hole.HoleNumber} isn't on this course's scorecard and was ignored.");
                continue;
            }
            if (hole.Par is { } osmPar && osmPar != courseHole.Par)
                warnings.Add($"Hole {hole.HoleNumber}: OpenStreetMap says par {osmPar}, the scorecard says par {courseHole.Par}. Check the pins.");

            holes.Add(new CourseHoleGeoHoleDto(hole.HoleNumber, hole.Tee, hole.Green, hole.GreenFront, hole.GreenBack, "osm", hole.WayId));
        }

        var found = holes.Select(h => h.HoleNumber).ToHashSet();
        foreach (var missing in courseHoles.Keys.Where(n => !found.Contains(n)).OrderBy(n => n))
            warnings.Add($"Hole {missing} wasn't found on OpenStreetMap. Place its tee and green by hand.");

        return Result<OsmHoleGeoPreviewDto>.Ok(new OsmHoleGeoPreviewDto(holes, warnings));
    }
}

// ── Save a course's hole map ──────────────────────────────────────────────────

public sealed record SaveCourseHoleGeoCommand(
    int CourseId,
    double? Latitude,
    double? Longitude,
    IReadOnlyList<CourseHoleGeoHoleDto> Holes,
    string UserId) : IRequest<Result<CourseHoleGeoDto>>, IAmAuditableCommand
{
    public string AuditEntityType => "CourseHoleGeo";
    public string AuditEntityId => CourseId.ToString();
}

public sealed class SaveCourseHoleGeoCommandHandler : IRequestHandler<SaveCourseHoleGeoCommand, Result<CourseHoleGeoDto>>
{
    private readonly ICourseRepository _courses;
    private readonly ICourseHoleGeoRepository _geo;
    private readonly IFeatureFlagRepository _flags;

    public SaveCourseHoleGeoCommandHandler(ICourseRepository courses, ICourseHoleGeoRepository geo, IFeatureFlagRepository flags)
    {
        _courses = courses;
        _geo = geo;
        _flags = flags;
    }

    public async Task<Result<CourseHoleGeoDto>> Handle(SaveCourseHoleGeoCommand request, CancellationToken cancellationToken)
    {
        if (!await HoleMapFlag.IsEnabledAsync(_flags, cancellationToken))
            return Result<CourseHoleGeoDto>.Fail(HoleMapFlag.DisabledError);

        var course = await _courses.GetByIdAsync(request.CourseId, cancellationToken);
        if (course is null)
            return Result<CourseHoleGeoDto>.Fail($"Course with ID {request.CourseId} not found.");

        if ((request.Latitude is null) != (request.Longitude is null)
            || (request.Latitude is { } lat && request.Longitude is { } lng && !HoleMapFlag.IsValid(new GeoPointDto(lat, lng))))
            return Result<CourseHoleGeoDto>.Fail("Invalid course location.");

        var courseHoleNumbers = course.Holes.Select(h => h.HoleNumber).ToHashSet();
        var seen = new HashSet<int>();
        foreach (var hole in request.Holes)
        {
            if (!courseHoleNumbers.Contains(hole.HoleNumber))
                return Result<CourseHoleGeoDto>.Fail($"Hole {hole.HoleNumber} isn't on this course.");
            if (!seen.Add(hole.HoleNumber))
                return Result<CourseHoleGeoDto>.Fail($"Hole {hole.HoleNumber} appears more than once.");
            if (hole.Tee is null || hole.Green is null
                || !HoleMapFlag.IsValid(hole.Tee) || !HoleMapFlag.IsValid(hole.Green)
                || !HoleMapFlag.IsValid(hole.GreenFront) || !HoleMapFlag.IsValid(hole.GreenBack))
                return Result<CourseHoleGeoDto>.Fail($"Hole {hole.HoleNumber} has invalid coordinates.");
        }

        var entities = request.Holes.Select(h => new CourseHoleGeo
        {
            CourseId = request.CourseId,
            HoleNumber = h.HoleNumber,
            TeeLat = h.Tee.Lat,
            TeeLng = h.Tee.Lng,
            GreenLat = h.Green.Lat,
            GreenLng = h.Green.Lng,
            GreenFrontLat = h.GreenFront?.Lat,
            GreenFrontLng = h.GreenFront?.Lng,
            GreenBackLat = h.GreenBack?.Lat,
            GreenBackLng = h.GreenBack?.Lng,
            Source = h.Source == "osm" ? "osm" : "manual",
            OsmWayId = h.Source == "osm" ? h.OsmWayId : null,
        }).OrderBy(g => g.HoleNumber).ToList();

        await _geo.ReplaceForCourseAsync(request.CourseId, request.Latitude, request.Longitude, entities, cancellationToken);

        return Result<CourseHoleGeoDto>.Ok(new CourseHoleGeoDto(
            request.CourseId, request.Latitude, request.Longitude, entities.Select(HoleMapFlag.ToDto).ToList()));
    }
}
