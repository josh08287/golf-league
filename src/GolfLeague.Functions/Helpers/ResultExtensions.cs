using GolfLeague.Application.Common;
using Microsoft.AspNetCore.Mvc;

namespace GolfLeague.Functions.Helpers;

public static class ResultExtensions
{
    public static IActionResult ToOkResult<T>(this Result<T> result)
    {
        if (!result.IsSuccess)
            return ToErrorResult<T>(result);
        return new OkObjectResult(result.Value);
    }

    public static IActionResult ToCreatedResult<T>(this Result<T> result, string? location = null)
    {
        if (!result.IsSuccess)
            return ToErrorResult<T>(result);
        return location is not null
            ? new CreatedResult(location, result.Value)
            : new ObjectResult(result.Value) { StatusCode = 201 };
    }

    private static IActionResult ToErrorResult<T>(Result<T> result)
    {
        var error = result.Error ?? "An error occurred.";

        if (error.Contains("not found", StringComparison.OrdinalIgnoreCase))
            return new NotFoundObjectResult(new { error });

        if (error.Contains("already exists", StringComparison.OrdinalIgnoreCase) ||
            error.Contains("already inactive", StringComparison.OrdinalIgnoreCase) ||
            error.Contains("already finalized", StringComparison.OrdinalIgnoreCase))
            return new ConflictObjectResult(new { error });

        return new BadRequestObjectResult(new { error });
    }
}
