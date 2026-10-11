using FluentAssertions;
using GolfLeague.Application.Admin;
using GolfLeague.Application.Common;
using GolfLeague.Application.Courses;
using GolfLeague.Application.DTOs;
using GolfLeague.Domain.Entities;
using GolfLeague.Domain.Interfaces;
using Moq;
using Xunit;

namespace GolfLeague.Tests.Application;

public class CourseHoleGeoHandlerTests
{
    private static Mock<IFeatureFlagRepository> Flags(bool enabled)
    {
        var mock = new Mock<IFeatureFlagRepository>();
        mock.Setup(f => f.GetAsync(KnownFeatureFlags.HoleMapEnabled, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FeatureFlag { Key = KnownFeatureFlags.HoleMapEnabled, Enabled = enabled });
        return mock;
    }

    private static Mock<ICourseRepository> Courses(params int[] holeNumbers)
    {
        var course = new Course
        {
            Id = 1,
            Name = "Test GC",
            Holes = holeNumbers.Select(n => new CourseHole { CourseId = 1, HoleNumber = n, Par = 4, StrokeIndex = n }).ToList(),
        };
        var mock = new Mock<ICourseRepository>();
        mock.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(course);
        return mock;
    }

    private static CourseHoleGeoHoleDto Hole(int number, string source = "manual") =>
        new(number, new GeoPointDto(40, -75), new GeoPointDto(40.003, -75), null, null, source, source == "osm" ? 123 : null);

    [Fact]
    public async Task Get_WhenFlagOff_ReturnsNotFound()
    {
        var geo = new Mock<ICourseHoleGeoRepository>();
        var handler = new GetCourseHoleGeoQueryHandler(Courses(1).Object, geo.Object, Flags(false).Object);

        var result = await handler.Handle(new GetCourseHoleGeoQuery(1), default);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("not found");
        geo.Verify(g => g.GetByCourseAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Get_ReturnsSavedHolesAndCentre()
    {
        var geo = new Mock<ICourseHoleGeoRepository>();
        geo.Setup(g => g.GetCourseCentreAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync((40.001, -75.002));
        geo.Setup(g => g.GetByCourseAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(
        [
            new CourseHoleGeo { CourseId = 1, HoleNumber = 1, TeeLat = 40, TeeLng = -75, GreenLat = 40.003, GreenLng = -75,
                GreenFrontLat = 40.0029, GreenFrontLng = -75, Source = "osm", OsmWayId = 9 },
        ]);
        var handler = new GetCourseHoleGeoQueryHandler(Courses(1).Object, geo.Object, Flags(true).Object);

        var result = await handler.Handle(new GetCourseHoleGeoQuery(1), default);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Latitude.Should().Be(40.001);
        var hole = result.Value.Holes.Should().ContainSingle().Subject;
        hole.GreenFront.Should().Be(new GeoPointDto(40.0029, -75));
        hole.GreenBack.Should().BeNull();
        hole.Source.Should().Be("osm");
    }

    [Fact]
    public async Task Preview_FiltersToCourseHoles_AndWarnsAboutMissingAndParMismatch()
    {
        var osm = new Mock<IOsmGolfImportService>();
        osm.Setup(o => o.FetchHolesAsync(40, -75, It.IsAny<CancellationToken>())).ReturnsAsync(new OsmGolfImportResult(
        [
            new OsmGolfHole(1, new GeoPointDto(40, -75), new GeoPointDto(40.003, -75), null, null, 3, 11),
            new OsmGolfHole(19, new GeoPointDto(40, -75), new GeoPointDto(40.003, -75), null, null, null, 12),
        ], []));
        var handler = new PreviewOsmHoleGeoQueryHandler(Courses(1, 2).Object, Flags(true).Object, osm.Object);

        var result = await handler.Handle(new PreviewOsmHoleGeoQuery(1, 40, -75), default);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Holes.Select(h => h.HoleNumber).Should().Equal(1);
        result.Value.Holes[0].Source.Should().Be("osm");
        result.Value.Warnings.Should().Contain(w => w.Contains("par 3") && w.Contains("par 4"));
        result.Value.Warnings.Should().Contain(w => w.StartsWith("Hole 2 wasn't found"));
        result.Value.Warnings.Should().Contain(w => w.Contains("hole 19"));
    }

    [Fact]
    public async Task Preview_WhenOverpassUnreachable_ReturnsFriendlyError()
    {
        var osm = new Mock<IOsmGolfImportService>();
        osm.Setup(o => o.FetchHolesAsync(It.IsAny<double>(), It.IsAny<double>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("boom"));
        var handler = new PreviewOsmHoleGeoQueryHandler(Courses(1).Object, Flags(true).Object, osm.Object);

        var result = await handler.Handle(new PreviewOsmHoleGeoQuery(1, 40, -75), default);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("OpenStreetMap");
    }

    [Fact]
    public async Task Save_ReplacesHolesForCourse()
    {
        var geo = new Mock<ICourseHoleGeoRepository>();
        IEnumerable<CourseHoleGeo>? saved = null;
        geo.Setup(g => g.ReplaceForCourseAsync(1, 40.001, -75.002, It.IsAny<IEnumerable<CourseHoleGeo>>(), It.IsAny<CancellationToken>()))
            .Callback<int, double?, double?, IEnumerable<CourseHoleGeo>, CancellationToken>((_, _, _, h, _) => saved = h.ToList());
        var handler = new SaveCourseHoleGeoCommandHandler(Courses(1, 2).Object, geo.Object, Flags(true).Object);

        var result = await handler.Handle(new SaveCourseHoleGeoCommand(1, 40.001, -75.002, [Hole(2), Hole(1, "osm")], "admin"), default);

        result.IsSuccess.Should().BeTrue();
        saved.Should().NotBeNull();
        saved!.Select(h => h.HoleNumber).Should().Equal(1, 2);
        saved!.First().OsmWayId.Should().Be(123);
        saved!.Last().Source.Should().Be("manual");
    }

    [Fact]
    public async Task Save_RejectsDuplicateAndUnknownHoles()
    {
        var geo = new Mock<ICourseHoleGeoRepository>();
        var handler = new SaveCourseHoleGeoCommandHandler(Courses(1, 2).Object, geo.Object, Flags(true).Object);

        var duplicate = await handler.Handle(new SaveCourseHoleGeoCommand(1, null, null, [Hole(1), Hole(1)], "admin"), default);
        var unknown = await handler.Handle(new SaveCourseHoleGeoCommand(1, null, null, [Hole(7)], "admin"), default);

        duplicate.IsSuccess.Should().BeFalse();
        unknown.IsSuccess.Should().BeFalse();
        geo.Verify(g => g.ReplaceForCourseAsync(It.IsAny<int>(), It.IsAny<double?>(), It.IsAny<double?>(),
            It.IsAny<IEnumerable<CourseHoleGeo>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Save_RejectsOutOfRangeCoordinates()
    {
        var geo = new Mock<ICourseHoleGeoRepository>();
        var handler = new SaveCourseHoleGeoCommandHandler(Courses(1).Object, geo.Object, Flags(true).Object);
        var bad = new CourseHoleGeoHoleDto(1, new GeoPointDto(95, -75), new GeoPointDto(40, -75), null, null, "manual");

        var result = await handler.Handle(new SaveCourseHoleGeoCommand(1, null, null, [bad], "admin"), default);

        result.IsSuccess.Should().BeFalse();
    }
}
