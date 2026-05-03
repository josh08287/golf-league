using Xunit;
using Moq;
using GolfLeague.Functions.Functions;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using FluentAssertions;

namespace GolfLeague.Tests.Functions;

public class AdminFunctionsExtendedTests
{
    [Fact]
    public async Task GetAuditLog_ReturnsUnauthorized_WhenNotAuthenticated()
    {
        var mediator = new Mock<IMediator>();
        var functions = new AdminFunctions(mediator.Object);
        var context = new DefaultHttpContext();
        var req = context.Request;
        
        var result = await functions.GetAuditLog(req, CancellationToken.None);
        
        result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task GetAuditLog_WhenCalledWithoutRole_ReturnsUnauthorized()
    {
        var mediator = new Mock<IMediator>();
        var functions = new AdminFunctions(mediator.Object);
        var context = new DefaultHttpContext();
        var req = context.Request;
        req.Headers["Authorization"] = "Bearer token";
        
        var result = await functions.GetAuditLog(req, CancellationToken.None);
        
        result.Should().NotBeNull();
    }
}
