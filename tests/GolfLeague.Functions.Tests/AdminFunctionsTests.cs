using FluentAssertions;
using GolfLeague.Application.Admin;
using GolfLeague.Application.Common;
using GolfLeague.Functions.Functions;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using System.Security.Claims;
using Xunit;

namespace GolfLeague.Tests;

public class AdminFunctionsTests
{
    private static AuditLogPageDto MakeAuditLogPageDto() =>
        new([], 0, 1, 25);

    private static HttpRequest MakeRequest(string? role = null, string? query = null)
    {
        var context = new DefaultHttpContext();
        if (role is not null)
        {
            var identity = new ClaimsIdentity(
                [new Claim(ClaimTypes.Role, role), new Claim(ClaimTypes.NameIdentifier, "user1")],
                "Test");
            context.User = new ClaimsPrincipal(identity);
        }
        if (query is not null)
            context.Request.QueryString = new QueryString(query);
        return context.Request;
    }

    [Fact]
    public async Task GetAuditLog_WhenNotAuthenticated_ReturnsUnauthorized()
    {
        var mediator = new Mock<IMediator>();
        var sut = new AdminFunctions(mediator.Object);

        var result = await sut.GetAuditLog(MakeRequest(), CancellationToken.None);

        result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task GetAuditLog_WhenNotAdmin_ReturnsForbidden()
    {
        var mediator = new Mock<IMediator>();
        var sut = new AdminFunctions(mediator.Object);

        var result = await sut.GetAuditLog(MakeRequest(role: "scorer"), CancellationToken.None);

        result.Should().BeOfType<ObjectResult>().Which.StatusCode.Should().Be(403);
    }

    [Fact]
    public async Task GetAuditLog_WhenAdminAndValid_ReturnsOk()
    {
        var mediator = new Mock<IMediator>();
        mediator.Setup(m => m.Send(It.IsAny<GetAuditLogQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<AuditLogPageDto>.Ok(MakeAuditLogPageDto()));

        var sut = new AdminFunctions(mediator.Object);

        var result = await sut.GetAuditLog(MakeRequest(role: "admin"), CancellationToken.None);

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task GetAuditLog_UsesDefaultPageValues_WhenNotProvided()
    {
        var mediator = new Mock<IMediator>();
        GetAuditLogQuery? capturedQuery = null;
        mediator.Setup(m => m.Send(It.IsAny<GetAuditLogQuery>(), It.IsAny<CancellationToken>()))
            .Callback<IRequest<Result<AuditLogPageDto>>, CancellationToken>((q, _) => capturedQuery = (GetAuditLogQuery)q)
            .ReturnsAsync(Result<AuditLogPageDto>.Ok(MakeAuditLogPageDto()));

        var sut = new AdminFunctions(mediator.Object);

        await sut.GetAuditLog(MakeRequest(role: "admin"), CancellationToken.None);

        capturedQuery!.Page.Should().Be(1);
        capturedQuery.PageSize.Should().Be(25);
    }

    [Fact]
    public async Task GetAuditLog_UsesProvidedPageValues()
    {
        var mediator = new Mock<IMediator>();
        GetAuditLogQuery? capturedQuery = null;
        mediator.Setup(m => m.Send(It.IsAny<GetAuditLogQuery>(), It.IsAny<CancellationToken>()))
            .Callback<IRequest<Result<AuditLogPageDto>>, CancellationToken>((q, _) => capturedQuery = (GetAuditLogQuery)q)
            .ReturnsAsync(Result<AuditLogPageDto>.Ok(MakeAuditLogPageDto()));

        var sut = new AdminFunctions(mediator.Object);

        await sut.GetAuditLog(MakeRequest(role: "admin", query: "?page=3&pageSize=10"), CancellationToken.None);

        capturedQuery!.Page.Should().Be(3);
        capturedQuery.PageSize.Should().Be(10);
    }
}
