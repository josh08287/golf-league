using FluentAssertions;
using GolfLeague.Application.Common;
using GolfLeague.Application.Courses.Commands;
using GolfLeague.Application.Courses.Queries;
using GolfLeague.Application.DTOs;
using GolfLeague.Functions.Functions;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using System.IO;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Xunit;

namespace GolfLeague.Tests;

public class CourseFunctionsTests
{
    private static CourseDto MakeCourseDto(int id = 1) =>
        new(id, "Golf Club", 72.0, 113, 18, []);

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
    public async Task GetCourses_ReturnsOkResult()
    {
        var mediator = new Mock<IMediator>();
        var paged = new PagedResult<CourseDto>([MakeCourseDto()], 1, 20, 1);
        mediator.Setup(m => m.Send(It.IsAny<GetCoursesQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<PagedResult<CourseDto>>.Ok(paged));

        var sut = new CourseFunctions(mediator.Object);
        var result = await sut.GetCourses(MakeRequest(), CancellationToken.None);

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task GetCourseById_WhenInvalidId_ReturnsBadRequest()
    {
        var mediator = new Mock<IMediator>();
        var sut = new CourseFunctions(mediator.Object);

        var result = await sut.GetCourseById(MakeRequest(), "abc", CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task GetCourseById_WhenNotFound_ReturnsNotFound()
    {
        var mediator = new Mock<IMediator>();
        mediator.Setup(m => m.Send(It.IsAny<GetCourseByIdQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<CourseDto>.Fail("Course with ID 99 not found."));

        var sut = new CourseFunctions(mediator.Object);

        var result = await sut.GetCourseById(MakeRequest(), "99", CancellationToken.None);

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task GetCourseById_WhenFound_ReturnsOk()
    {
        var mediator = new Mock<IMediator>();
        mediator.Setup(m => m.Send(It.IsAny<GetCourseByIdQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<CourseDto>.Ok(MakeCourseDto()));

        var sut = new CourseFunctions(mediator.Object);

        var result = await sut.GetCourseById(MakeRequest(), "1", CancellationToken.None);

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task CreateCourse_WhenNotAuthenticated_ReturnsUnauthorized()
    {
        var mediator = new Mock<IMediator>();
        var sut = new CourseFunctions(mediator.Object);
        var body = JsonSerializer.Serialize(new { Name = "Golf Club", Rating = 72.0, Slope = 113 });

        var result = await sut.CreateCourse(MakeRequest(body), CancellationToken.None);

        result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task CreateCourse_WhenNoBody_ReturnsBadRequest()
    {
        var mediator = new Mock<IMediator>();
        var sut = new CourseFunctions(mediator.Object);
        var req = MakeRequest(role: "admin");
        req.Body = new MemoryStream();

        var result = await sut.CreateCourse(req, CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task CreateCourse_WhenValid_ReturnsCreated()
    {
        var mediator = new Mock<IMediator>();
        mediator.Setup(m => m.Send(It.IsAny<CreateCourseCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<CourseDto>.Ok(MakeCourseDto()));

        var sut = new CourseFunctions(mediator.Object);
        var body = JsonSerializer.Serialize(new { Name = "Golf Club", Rating = 72.0, Slope = 113 });

        var result = await sut.CreateCourse(MakeRequest(body, "admin"), CancellationToken.None);

        result.Should().BeOfType<CreatedResult>();
    }

    [Fact]
    public async Task UpdateCourseHoles_WhenNotAuthenticated_ReturnsUnauthorized()
    {
        var mediator = new Mock<IMediator>();
        var sut = new CourseFunctions(mediator.Object);
        var body = JsonSerializer.Serialize(new { Holes = new[] { new { HoleNumber = 1, Par = 4, StrokeIndex = 1 } } });

        var result = await sut.UpdateCourseHoles(MakeRequest(body), "1", CancellationToken.None);

        result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task UpdateCourseHoles_WhenInvalidId_ReturnsBadRequest()
    {
        var mediator = new Mock<IMediator>();
        var sut = new CourseFunctions(mediator.Object);
        var body = JsonSerializer.Serialize(new { Holes = new[] { new { HoleNumber = 1, Par = 4, StrokeIndex = 1 } } });

        var result = await sut.UpdateCourseHoles(MakeRequest(body, "admin"), "abc", CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task UpdateCourseHoles_WhenNoBody_ReturnsBadRequest()
    {
        var mediator = new Mock<IMediator>();
        var sut = new CourseFunctions(mediator.Object);
        var req = MakeRequest(role: "admin");
        req.Body = new MemoryStream();

        var result = await sut.UpdateCourseHoles(req, "1", CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task UpdateCourseHoles_WhenValid_ReturnsOk()
    {
        var mediator = new Mock<IMediator>();
        mediator.Setup(m => m.Send(It.IsAny<UpdateCourseHolesCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<CourseDto>.Ok(MakeCourseDto()));

        var sut = new CourseFunctions(mediator.Object);
        var body = JsonSerializer.Serialize(new { Holes = new[] { new { HoleNumber = 1, Par = 4, StrokeIndex = 1 } } });

        var result = await sut.UpdateCourseHoles(MakeRequest(body, "admin"), "1", CancellationToken.None);

        result.Should().BeOfType<OkObjectResult>();
    }
}
