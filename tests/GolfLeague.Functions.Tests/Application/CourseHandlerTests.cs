using FluentAssertions;
using GolfLeague.Application.Courses.Commands;
using GolfLeague.Application.Courses.Queries;
using GolfLeague.Domain.Entities;
using GolfLeague.Domain.Interfaces;
using Moq;
using Xunit;

namespace GolfLeague.Tests.Application;

public class CreateCourseCommandHandlerTests
{
    [Fact]
    public async Task Handle_CreatesCourse_AndReturnsDto()
    {
        var repo = new Mock<ICourseRepository>();
        var handler = new CreateCourseCommandHandler(repo.Object);
        var cmd = new CreateCourseCommand("Golf Club", 72.0, 113, "admin");

        var result = await handler.Handle(cmd, default);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Name.Should().Be("Golf Club");
        result.Value.Rating.Should().Be(72.0);
        result.Value.Slope.Should().Be(113);
        result.Value.HoleCount.Should().Be(0);
        repo.Verify(r => r.AddAsync(It.IsAny<Course>(), default), Times.Once);
    }
}

public class UpdateCourseHolesCommandHandlerTests
{
    [Fact]
    public async Task Handle_WhenCourseNotFound_ReturnsFail()
    {
        var repo = new Mock<ICourseRepository>();
        repo.Setup(r => r.GetByIdAsync(99, default)).ReturnsAsync((Course?)null);
        var handler = new UpdateCourseHolesCommandHandler(repo.Object);
        var cmd = new UpdateCourseHolesCommand(99, [], "admin");

        var result = await handler.Handle(cmd, default);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("not found");
    }

    [Fact]
    public async Task Handle_WhenCourseFound_UpdatesHoles()
    {
        var course = new Course { Id = 1, Name = "Golf Club", CourseRating = 72.0, SlopeRating = 113 };
        var repo = new Mock<ICourseRepository>();
        repo.Setup(r => r.GetByIdAsync(1, default)).ReturnsAsync(course);
        var handler = new UpdateCourseHolesCommandHandler(repo.Object);
        var holes = new List<HoleInput>
        {
            new(1, 4, 1), new(2, 3, 9), new(3, 5, 3)
        };
        var cmd = new UpdateCourseHolesCommand(1, holes, "admin");

        var result = await handler.Handle(cmd, default);

        result.IsSuccess.Should().BeTrue();
        result.Value!.HoleCount.Should().Be(3);
        result.Value.HoleDetails.Should().HaveCount(3);
        repo.Verify(r => r.UpdateHolesAsync(1, It.Is<List<CourseHole>>(h => h.Count == 3), default), Times.Once);
    }
}

public class GetCoursesQueryHandlerTests
{
    [Fact]
    public async Task Handle_ReturnsMappedDtos()
    {
        var courses = new List<Course>
        {
            new() { Id = 1, Name = "Golf Club", CourseRating = 72.0, SlopeRating = 113,
                Holes = [new CourseHole { HoleNumber = 1, Par = 4, StrokeIndex = 1 }] }
        };
        var repo = new Mock<ICourseRepository>();
        repo.Setup(r => r.GetAllAsync(default)).ReturnsAsync(courses);
        var handler = new GetCoursesQueryHandler(repo.Object);

        var result = await handler.Handle(new GetCoursesQuery(), default);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Data.Should().HaveCount(1);
        result.Value.Data[0].Name.Should().Be("Golf Club");
        result.Value.Data[0].HoleCount.Should().Be(1);

    }

    [Fact]
    public async Task Handle_OrdersHolesByHoleNumber()
    {
        var courses = new List<Course>
        {
            new() { Id = 1, Name = "Golf Club", CourseRating = 72.0, SlopeRating = 113,
                Holes = [
                    new CourseHole { HoleNumber = 3, Par = 5, StrokeIndex = 3 },
                    new CourseHole { HoleNumber = 1, Par = 4, StrokeIndex = 1 },
                    new CourseHole { HoleNumber = 2, Par = 3, StrokeIndex = 9 }
                ]}
        };
        var repo = new Mock<ICourseRepository>();
        repo.Setup(r => r.GetAllAsync(default)).ReturnsAsync(courses);
        var handler = new GetCoursesQueryHandler(repo.Object);

        var result = await handler.Handle(new GetCoursesQuery(), default);

        result.Value!.Data[0].HoleDetails[0].HoleNumber.Should().Be(1);
        result.Value.Data[0].HoleDetails[1].HoleNumber.Should().Be(2);
        result.Value.Data[0].HoleDetails[2].HoleNumber.Should().Be(3);
    }
}

public class GetCourseByIdQueryHandlerTests
{
    [Fact]
    public async Task Handle_WhenNotFound_ReturnsFail()
    {
        var repo = new Mock<ICourseRepository>();
        repo.Setup(r => r.GetByIdAsync(99, default)).ReturnsAsync((Course?)null);
        var handler = new GetCourseByIdQueryHandler(repo.Object);

        var result = await handler.Handle(new GetCourseByIdQuery(99), default);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("not found");
    }

    [Fact]
    public async Task Handle_WhenFound_ReturnsDto()
    {
        var course = new Course
        {
            Id = 1, Name = "Golf Club", CourseRating = 72.0, SlopeRating = 113,
            Holes = [new CourseHole { HoleNumber = 1, Par = 4, StrokeIndex = 1 }]
        };
        var repo = new Mock<ICourseRepository>();
        repo.Setup(r => r.GetByIdAsync(1, default)).ReturnsAsync(course);
        var handler = new GetCourseByIdQueryHandler(repo.Object);

        var result = await handler.Handle(new GetCourseByIdQuery(1), default);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Name.Should().Be("Golf Club");
        result.Value.HoleCount.Should().Be(1);
    }
}
