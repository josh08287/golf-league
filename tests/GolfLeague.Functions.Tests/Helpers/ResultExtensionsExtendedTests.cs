using Xunit;
using GolfLeague.Functions.Helpers;
using GolfLeague.Application.Common;
using Microsoft.AspNetCore.Mvc;
using FluentAssertions;

namespace GolfLeague.Tests.Functions;

public class ResultExtensionsExtendedTests
{
    [Fact]
    public void ToOkResult_WhenSuccess_ReturnsOkObjectResult()
    {
        var result = Result<string>.Ok("test data");
        
        var okResult = result.ToOkResult();
        
        okResult.Should().BeOfType<OkObjectResult>();
        ((OkObjectResult)okResult).Value.Should().Be("test data");
    }

    [Fact]
    public void ToOkResult_WhenGenericError_ReturnsBadRequestObjectResult()
    {
        var result = Result<string>.Fail("Generic error");
        
        var okResult = result.ToOkResult();
        
        okResult.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public void ToOkResult_WhenErrorIsNull_ReturnsBadRequestWithDefaultMessage()
    {
        var result = Result<string>.Fail((string)null!);
        
        var okResult = result.ToOkResult();
        
        okResult.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public void ToCreatedResult_WhenSuccess_WithLocation_ReturnsCreatedResult()
    {
        var result = Result<string>.Ok("created");
        
        var createdResult = result.ToCreatedResult("/api/resource/1");
        
        createdResult.Should().BeOfType<CreatedResult>();
        ((CreatedResult)createdResult).Location.Should().Be("/api/resource/1");
    }

    [Fact]
    public void ToCreatedResult_WhenSuccess_WithoutLocation_ReturnsOkObjectResult()
    {
        var result = Result<string>.Ok("created");
        
        var createdResult = result.ToCreatedResult();
        
        createdResult.Should().BeOfType<ObjectResult>();
        ((ObjectResult)createdResult).StatusCode.Should().Be(201);
    }

    [Fact]
    public void ToCreatedResult_WhenGenericError_ReturnsBadRequestObjectResult()
    {
        var result = Result<string>.Fail("Generic error");
        
        var createdResult = result.ToCreatedResult("/api/resource/1");
        
        createdResult.Should().BeOfType<BadRequestObjectResult>();
    }
}
