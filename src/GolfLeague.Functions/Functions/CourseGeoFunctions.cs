using GolfLeague.Application.Courses;
using GolfLeague.Application.DTOs;
using GolfLeague.Functions.Helpers;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;

namespace GolfLeague.Functions.Functions;

/// <summary>
/// Course hole map (tee/green coordinates) for the score-entry hole map.
/// All endpoints are gated on the hole_map_enabled feature flag inside
/// their handlers and return 404 while it is off.
/// </summary>
public sealed class CourseGeoFunctions
{
    private readonly IMediator _mediator;

    public CourseGeoFunctions(IMediator mediator)
    {
        _mediator = mediator;
    }

    [Function("GetCourseHoleGeo")]
    public async Task<IActionResult> GetCourseHoleGeo(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/courses/{id}/hole-geo")] HttpRequest req,
        string id,
        CancellationToken cancellationToken)
    {
        var authError = req.RequireAuthenticated();
        if (authError is not null) return authError;

        if (!int.TryParse(id, out var courseId))
            return new BadRequestObjectResult(new { error = "Invalid course ID." });

        var result = await _mediator.Send(new GetCourseHoleGeoQuery(courseId), cancellationToken);
        return result.ToOkResult();
    }

    [Function("PreviewOsmCourseHoleGeo")]
    public async Task<IActionResult> PreviewOsmCourseHoleGeo(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/courses/{id}/hole-geo/osm-preview")] HttpRequest req,
        string id,
        CancellationToken cancellationToken)
    {
        var authError = req.RequireRole("admin");
        if (authError is not null) return authError;

        if (!int.TryParse(id, out var courseId))
            return new BadRequestObjectResult(new { error = "Invalid course ID." });

        var body = await req.TryDeserializeAsync<OsmPreviewRequest>(cancellationToken);
        if (body is null)
            return new BadRequestObjectResult(new { error = "Request body is required." });

        var result = await _mediator.Send(new PreviewOsmHoleGeoQuery(courseId, body.Lat, body.Lng), cancellationToken);
        return result.ToOkResult();
    }

    [Function("SaveCourseHoleGeo")]
    public async Task<IActionResult> SaveCourseHoleGeo(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "v1/courses/{id}/hole-geo")] HttpRequest req,
        string id,
        CancellationToken cancellationToken)
    {
        var authError = req.RequireRole("admin");
        if (authError is not null) return authError;

        if (!int.TryParse(id, out var courseId))
            return new BadRequestObjectResult(new { error = "Invalid course ID." });

        var body = await req.TryDeserializeAsync<SaveHoleGeoRequest>(cancellationToken);
        if (body?.Holes is null)
            return new BadRequestObjectResult(new { error = "Request body is required." });

        var userId = req.GetUserId() ?? "unknown";
        var result = await _mediator.Send(
            new SaveCourseHoleGeoCommand(courseId, body.Latitude, body.Longitude, body.Holes, userId), cancellationToken);
        return result.ToOkResult();
    }

    private sealed record OsmPreviewRequest(double Lat, double Lng);
    private sealed record SaveHoleGeoRequest(double? Latitude, double? Longitude, List<CourseHoleGeoHoleDto>? Holes);
}
