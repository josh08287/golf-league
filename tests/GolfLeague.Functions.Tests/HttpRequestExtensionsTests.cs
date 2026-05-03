using FluentAssertions;
using GolfLeague.Functions.Helpers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using Xunit;

namespace GolfLeague.Tests;

public class HttpRequestExtensionsTests
{
    private static HttpRequest MakeAuthenticatedRequest(params Claim[] claims)
    {
        var context = new DefaultHttpContext();
        var identity = new ClaimsIdentity(claims, "Test");
        context.User = new ClaimsPrincipal(identity);
        return context.Request;
    }

    private static HttpRequest MakeUnauthenticatedRequest()
    {
        var context = new DefaultHttpContext();
        return context.Request;
    }

    [Fact]
    public void GetUserId_WhenNameIdentifierClaim_ReturnsIt()
    {
        var req = MakeAuthenticatedRequest(new Claim(ClaimTypes.NameIdentifier, "user-123"));
        req.GetUserId().Should().Be("user-123");
    }

    [Fact]
    public void GetUserId_WhenOidClaim_ReturnsIt()
    {
        var req = MakeAuthenticatedRequest(new Claim("oid", "oid-value"));
        req.GetUserId().Should().Be("oid-value");
    }

    [Fact]
    public void GetUserId_WhenSubClaim_ReturnsIt()
    {
        var req = MakeAuthenticatedRequest(new Claim("sub", "sub-value"));
        req.GetUserId().Should().Be("sub-value");
    }

    [Fact]
    public void GetUserId_WhenNoClaims_ReturnsNull()
    {
        var req = MakeUnauthenticatedRequest();
        req.GetUserId().Should().BeNull();
    }

    [Fact]
    public void RequireRole_WhenNoIdentity_ReturnsUnauthorized()
    {
        var context = new DefaultHttpContext();
        context.User = new System.Security.Claims.ClaimsPrincipal(); // no identity at all
        var result = context.Request.RequireRole("admin");
        result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public void RequireRole_WhenNotAuthenticated_ReturnsUnauthorized()
    {
        var req = MakeUnauthenticatedRequest();
        var result = req.RequireRole("admin");
        result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public void RequireRole_WhenAuthenticatedButWrongRole_ReturnsForbidden()
    {
        var req = MakeAuthenticatedRequest(new Claim(ClaimTypes.Role, "player"));
        var result = req.RequireRole("admin");
        result.Should().BeOfType<ObjectResult>()
            .Which.StatusCode.Should().Be(403);
    }

    [Fact]
    public void RequireRole_WhenAuthenticatedWithCorrectRole_ReturnsNull()
    {
        var req = MakeAuthenticatedRequest(new Claim(ClaimTypes.Role, "admin"));
        req.RequireRole("admin").Should().BeNull();
    }

    [Fact]
    public void RequireRole_WhenAuthenticatedWithOneOfMultipleAllowedRoles_ReturnsNull()
    {
        var req = MakeAuthenticatedRequest(new Claim(ClaimTypes.Role, "scorer"));
        req.RequireRole("scorer", "admin").Should().BeNull();
    }

    [Fact]
    public void RequireAuthenticated_WhenNoIdentity_ReturnsUnauthorized()
    {
        var context = new DefaultHttpContext();
        context.User = new System.Security.Claims.ClaimsPrincipal(); // no identity
        context.Request.RequireAuthenticated().Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public void RequireAuthenticated_WhenNotAuthenticated_ReturnsUnauthorized()
    {
        var req = MakeUnauthenticatedRequest();
        req.RequireAuthenticated().Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public void RequireAuthenticated_WhenAuthenticated_ReturnsNull()
    {
        var req = MakeAuthenticatedRequest(new Claim(ClaimTypes.Role, "player"));
        req.RequireAuthenticated().Should().BeNull();
    }
}
