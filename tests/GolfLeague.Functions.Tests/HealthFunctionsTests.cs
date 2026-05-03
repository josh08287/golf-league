using FluentAssertions;
using GolfLeague.Functions.Functions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace GolfLeague.Tests;

public class HealthFunctionsTests
{
    [Fact]
    public void Health_ReturnsOkResult()
    {
        var functions = new HealthFunctions();
        var context = new DefaultHttpContext();
        var result = functions.Health(context.Request);
        Assert.IsType<OkObjectResult>(result);
    }

    [Fact]
    public void Health_ReturnsHealthyStatus()
    {
        var functions = new HealthFunctions();
        var context = new DefaultHttpContext();
        var result = (OkObjectResult)functions.Health(context.Request);
        result.Value.Should().NotBeNull();
        var statusProp = result.Value!.GetType().GetProperty("status");
        statusProp.Should().NotBeNull();
        statusProp!.GetValue(result.Value).Should().Be("healthy");
    }
}
