using FluentAssertions;
using GolfLeague.Application.Common;
using GolfLeague.Functions.Helpers;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace GolfLeague.Tests;

public class ResultExtensionsTests
{
    [Fact]
    public void ToOkResult_WhenSuccess_ReturnsOkObjectResult()
    {
        var result = Result<string>.Ok("hello");
        result.ToOkResult().Should().BeOfType<OkObjectResult>()
            .Which.Value.Should().Be("hello");
    }

    [Fact]
    public void ToOkResult_WhenNotFoundError_ReturnsNotFoundObjectResult()
    {
        var result = Result<string>.Fail("Player with ID 1 not found.");
        result.ToOkResult().Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public void ToOkResult_WhenAlreadyExistsError_ReturnsConflictObjectResult()
    {
        var result = Result<string>.Fail("A player with Entra Object ID '...' already exists.");
        result.ToOkResult().Should().BeOfType<ConflictObjectResult>();
    }

    [Fact]
    public void ToOkResult_WhenAlreadyInactiveError_ReturnsConflictObjectResult()
    {
        var result = Result<string>.Fail("Player with ID 1 is already inactive.");
        result.ToOkResult().Should().BeOfType<ConflictObjectResult>();
    }

    [Fact]
    public void ToOkResult_WhenAlreadyFinalizedError_ReturnsConflictObjectResult()
    {
        var result = Result<string>.Fail("Round 1 is already finalized.");
        result.ToOkResult().Should().BeOfType<ConflictObjectResult>();
    }

    [Fact]
    public void ToOkResult_WhenGenericError_ReturnsBadRequestObjectResult()
    {
        var result = Result<string>.Fail("Some generic error.");
        result.ToOkResult().Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public void ToCreatedResult_WhenSuccess_WithLocation_ReturnsCreatedResult()
    {
        var result = Result<string>.Ok("hello");
        result.ToCreatedResult("/api/v1/resource/1").Should().BeOfType<CreatedResult>()
            .Which.Location.Should().Be("/api/v1/resource/1");
    }

    [Fact]
    public void ToCreatedResult_WhenSuccess_WithoutLocation_Returns201()
    {
        var result = Result<string>.Ok("hello");
        var actionResult = result.ToCreatedResult();
        actionResult.Should().BeOfType<ObjectResult>()
            .Which.StatusCode.Should().Be(201);
    }

    [Fact]
    public void ToCreatedResult_WhenNotFoundError_ReturnsNotFoundObjectResult()
    {
        var result = Result<string>.Fail("Item not found.");
        result.ToCreatedResult("/api/v1/resource/1").Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public void ToCreatedResult_WhenAlreadyExistsError_ReturnsConflictObjectResult()
    {
        var result = Result<string>.Fail("Item already exists.");
        result.ToCreatedResult("/api/v1/resource/1").Should().BeOfType<ConflictObjectResult>();
    }

    [Fact]
    public void ToCreatedResult_WhenGenericError_ReturnsBadRequestObjectResult()
    {
        var result = Result<string>.Fail("Some validation error.");
        result.ToCreatedResult("/api/v1/resource/1").Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public void ToOkResult_WhenErrorIsNull_ReturnsBadRequestWithDefaultMessage()
    {
        // Create a Result via Fail with empty string (edge case)
        var result = Result<string>.Fail(string.Empty);
        var actionResult = result.ToOkResult();
        actionResult.Should().BeOfType<BadRequestObjectResult>();
    }
}
