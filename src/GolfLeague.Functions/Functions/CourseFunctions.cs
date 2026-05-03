using GolfLeague.Application.Courses.Commands;
using GolfLeague.Application.Courses.Queries;
using GolfLeague.Functions.Helpers;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
namespace GolfLeague.Functions.Functions;

public sealed class CourseFunctions
{
    private readonly IMediator _mediator;

    public CourseFunctions(IMediator mediator)
    {
        _mediator = mediator;
    }

    [Function("GetCourses")]
    public async Task<IActionResult> GetCourses(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/courses")] HttpRequest req,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetCoursesQuery(), cancellationToken);
        return result.ToOkResult();
    }

    [Function("GetCourseById")]
    public async Task<IActionResult> GetCourseById(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/courses/{id}")] HttpRequest req,
        string id,
        CancellationToken cancellationToken)
    {
        if (!int.TryParse(id, out var courseId))
            return new BadRequestObjectResult(new { error = "Invalid course ID." });

        var result = await _mediator.Send(new GetCourseByIdQuery(courseId), cancellationToken);
        return result.ToOkResult();
    }

    [Function("CreateCourse")]
    public async Task<IActionResult> CreateCourse(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/courses")] HttpRequest req,
        CancellationToken cancellationToken)
    {
        var authError = req.RequireRole("admin");
        if (authError is not null) return authError;

        var body = await req.TryDeserializeAsync<CreateCourseRequest>(cancellationToken);

        if (body is null)
            return new BadRequestObjectResult(new { error = "Request body is required." });

        var userId = req.GetUserId() ?? "unknown";
        var result = await _mediator.Send(new CreateCourseCommand(body.Name, body.Rating, body.Slope, userId), cancellationToken);
        return result.ToCreatedResult($"/api/v1/courses/{result.Value?.Id}");
    }

    [Function("UpdateCourseHoles")]
    public async Task<IActionResult> UpdateCourseHoles(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "v1/courses/{id}/holes")] HttpRequest req,
        string id,
        CancellationToken cancellationToken)
    {
        var authError = req.RequireRole("admin");
        if (authError is not null) return authError;

        if (!int.TryParse(id, out var courseId))
            return new BadRequestObjectResult(new { error = "Invalid course ID." });

        var body = await req.TryDeserializeAsync<UpdateHolesRequest>(cancellationToken);

        if (body is null)
            return new BadRequestObjectResult(new { error = "Request body is required." });

        var userId = req.GetUserId() ?? "unknown";
        var holes = body.Holes.Select(h => new HoleInput(h.HoleNumber, h.Par, h.StrokeIndex)).ToList();
        var result = await _mediator.Send(new UpdateCourseHolesCommand(courseId, holes, userId), cancellationToken);
        return result.ToOkResult();
    }

    private sealed record CreateCourseRequest(string Name, double Rating, int Slope);
    private sealed record HoleDto(int HoleNumber, int Par, int StrokeIndex);
    private sealed record UpdateHolesRequest(List<HoleDto> Holes);
}
