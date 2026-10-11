using FluentAssertions;
using GolfLeague.Application.Common;
using GolfLeague.Application.Courses;
using GolfLeague.Application.DTOs;
using GolfLeague.Functions.Functions;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using System.IO;
using System.Security.Claims;
using System.Text;
using Xunit;

namespace GolfLeague.Tests;

public class CourseGeoFunctionsTests
{
    private static HttpRequest MakeRequest(string? body = null, string? role = null)
    {
        var context = new DefaultHttpContext();
        if (role is not null)
        {
            var identity = new ClaimsIdentity(
                [new Claim(ClaimTypes.Role, role), new Claim(ClaimTypes.NameIdentifier, "user1")],
                "Test");
            context.User = new ClaimsPrincipal(identity);
        }
        if (body is not null)
        {
            var bytes = Encoding.UTF8.GetBytes(body);
            context.Request.Body = new MemoryStream(bytes);
            context.Request.ContentType = "application/json";
            context.Request.ContentLength = bytes.Length;
        }
        return context.Request;
    }

    [Fact]
    public async Task GetCourseHoleGeo_WhenAnonymous_ReturnsUnauthorized()
    {
        var sut = new CourseGeoFunctions(new Mock<IMediator>().Object);

        var result = await sut.GetCourseHoleGeo(MakeRequest(), "1", CancellationToken.None);

        result.Should().NotBeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task GetCourseHoleGeo_WhenFlagOff_ReturnsNotFound()
    {
        var mediator = new Mock<IMediator>();
        mediator.Setup(m => m.Send(It.IsAny<GetCourseHoleGeoQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<CourseHoleGeoDto>.Fail("Hole map not found."));
        var sut = new CourseGeoFunctions(mediator.Object);

        var result = await sut.GetCourseHoleGeo(MakeRequest(role: "player"), "1", CancellationToken.None);

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task GetCourseHoleGeo_AsPlayer_ReturnsOk()
    {
        var mediator = new Mock<IMediator>();
        mediator.Setup(m => m.Send(It.IsAny<GetCourseHoleGeoQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<CourseHoleGeoDto>.Ok(new CourseHoleGeoDto(1, null, null, [])));
        var sut = new CourseGeoFunctions(mediator.Object);

        var result = await sut.GetCourseHoleGeo(MakeRequest(role: "player"), "1", CancellationToken.None);

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task PreviewOsm_AsPlayer_IsForbidden()
    {
        var mediator = new Mock<IMediator>();
        var sut = new CourseGeoFunctions(mediator.Object);

        var result = await sut.PreviewOsmCourseHoleGeo(MakeRequest("""{"lat":40,"lng":-75}""", "player"), "1", CancellationToken.None);

        result.Should().NotBeOfType<OkObjectResult>();
        mediator.Verify(m => m.Send(It.IsAny<PreviewOsmHoleGeoQuery>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SaveCourseHoleGeo_AsAdmin_SendsCommand()
    {
        var mediator = new Mock<IMediator>();
        mediator.Setup(m => m.Send(It.IsAny<SaveCourseHoleGeoCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<CourseHoleGeoDto>.Ok(new CourseHoleGeoDto(1, 40, -75, [])));
        var sut = new CourseGeoFunctions(mediator.Object);
        var body = """
            {"latitude":40,"longitude":-75,"holes":[{"holeNumber":1,"tee":{"lat":40,"lng":-75},"green":{"lat":40.003,"lng":-75},"source":"manual"}]}
            """;

        var result = await sut.SaveCourseHoleGeo(MakeRequest(body, "admin"), "1", CancellationToken.None);

        result.Should().BeOfType<OkObjectResult>();
        mediator.Verify(m => m.Send(
            It.Is<SaveCourseHoleGeoCommand>(c => c.CourseId == 1 && c.Holes.Count == 1 && c.Holes[0].Green.Lat == 40.003),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SaveCourseHoleGeo_WithoutBody_ReturnsBadRequest()
    {
        var sut = new CourseGeoFunctions(new Mock<IMediator>().Object);

        var result = await sut.SaveCourseHoleGeo(MakeRequest(role: "admin"), "1", CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>();
    }
}
