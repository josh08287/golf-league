using FluentAssertions;
using GolfLeague.Domain.Entities;
using Xunit;

namespace GolfLeague.Tests;

public class PlayerEntityTests
{
    [Fact]
    public void FullName_ConcatenatesFirstAndLastName()
    {
        var player = new Player { FirstName = "John", LastName = "Doe" };
        player.FullName.Should().Be("John Doe");
    }

    [Fact]
    public void Initials_TakesFirstCharOfEachName()
    {
        var player = new Player { FirstName = "John", LastName = "Doe" };
        player.Initials.Should().Be("JD");
    }

    [Fact]
    public void Initials_AreUppercase()
    {
        var player = new Player { FirstName = "alice", LastName = "brown" };
        player.Initials.Should().Be("AB");
    }

    [Fact]
    public void Player_DefaultIsActive_IsTrue()
    {
        var player = new Player();
        player.IsActive.Should().BeTrue();
    }

    [Fact]
    public void Player_DefaultCollections_AreEmpty()
    {
        var player = new Player();
        player.FlightMemberships.Should().BeEmpty();
        player.Handicaps.Should().BeEmpty();
        player.RoundParticipants.Should().BeEmpty();
    }
}

public class DomainEntityPropertyTests
{
    [Fact]
    public void AuditLog_PropertiesRoundTrip()
    {
        var now = DateTime.UtcNow;
        var log = new GolfLeague.Domain.Entities.AuditLog
        {
            Id = 1,
            Action = "Create",
            EntityType = "Player",
            EntityId = "42",
            UserId = "admin",
            Timestamp = now,
            BeforeJson = null,
            AfterJson = "{}"
        };
        log.Id.Should().Be(1);
        log.Action.Should().Be("Create");
        log.EntityType.Should().Be("Player");
        log.EntityId.Should().Be("42");
        log.UserId.Should().Be("admin");
        log.Timestamp.Should().Be(now);
        log.BeforeJson.Should().BeNull();
        log.AfterJson.Should().Be("{}");
    }

    [Fact]
    public void CourseHole_PropertiesRoundTrip()
    {
        var course = new GolfLeague.Domain.Entities.Course { Id = 1, Name = "Test" };
        var hole = new GolfLeague.Domain.Entities.CourseHole
        {
            Id = 1, CourseId = 1, HoleNumber = 3, Par = 4, StrokeIndex = 5, Course = course
        };
        hole.Id.Should().Be(1);
        hole.CourseId.Should().Be(1);
        hole.HoleNumber.Should().Be(3);
        hole.Par.Should().Be(4);
        hole.StrokeIndex.Should().Be(5);
        hole.Course.Should().Be(course);
    }

    [Fact]
    public void FlightMembership_PropertiesRoundTrip()
    {
        var joined = DateTime.UtcNow;
        var player = new GolfLeague.Domain.Entities.Player { Id = 1, FirstName = "John", LastName = "Doe" };
        var flight = new GolfLeague.Domain.Entities.Flight { Id = 2, Name = "A Flight" };
        var season = new GolfLeague.Domain.Entities.Season { Id = 3, Name = "2026" };
        var membership = new GolfLeague.Domain.Entities.FlightMembership
        {
            Id = 1, PlayerId = 1, FlightId = 2, SeasonId = 3,
            JoinedAt = joined, Player = player, Flight = flight, Season = season
        };
        membership.Id.Should().Be(1);
        membership.PlayerId.Should().Be(1);
        membership.FlightId.Should().Be(2);
        membership.SeasonId.Should().Be(3);
        membership.JoinedAt.Should().Be(joined);
        membership.Player.Should().Be(player);
        membership.Flight.Should().Be(flight);
        membership.Season.Should().Be(season);
    }
}

public class CommonTests
{
    [Fact]
    public void Result_Ok_HasIsSuccessTrue()
    {
        var result = GolfLeague.Application.Common.Result<string>.Ok("test");
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be("test");
        result.Error.Should().BeNull();
    }

    [Fact]
    public void Result_Fail_HasIsSuccessFalse()
    {
        var result = GolfLeague.Application.Common.Result<string>.Fail("error msg");
        result.IsSuccess.Should().BeFalse();
        result.Value.Should().BeNull();
        result.Error.Should().Be("error msg");
    }

    [Fact]
    public void PageMeta_StoresPageProperties()
    {
        var meta = new GolfLeague.Application.Common.PageMeta(2, 10, 100);
        meta.Page.Should().Be(2);
        meta.PageSize.Should().Be(10);
        meta.TotalCount.Should().Be(100);
    }

    [Fact]
    public void PagedResult_StoresDataAndMeta()
    {
        var data = new List<string> { "a", "b" };
        var pr = new GolfLeague.Application.Common.PagedResult<string>(data, 1, 20, 2);
        pr.Data.Should().BeEquivalentTo(data);
        pr.Meta.Page.Should().Be(1);
        pr.Meta.PageSize.Should().Be(20);
        pr.Meta.TotalCount.Should().Be(2);
        pr.Errors.Should().BeEmpty();
    }
}
