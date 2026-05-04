using FluentAssertions;
using GolfLeague.Application.Admin;
using GolfLeague.Application.DTOs;
using GolfLeague.Application.Rounds.Queries;
using GolfLeague.Domain.Enums;
using Xunit;

namespace GolfLeague.Tests;

public class DtoPropertyTests
{
    [Fact]
    public void PlayerDto_AllPropertiesAccessible()
    {
        var dto = new PlayerDto(1, "John Doe", "john@example.com", true, 12.5, 3, "A Flight");
        dto.Id.Should().Be(1);
        dto.FullName.Should().Be("John Doe");
        dto.Email.Should().Be("john@example.com");
        dto.IsActive.Should().BeTrue();
        dto.CurrentHandicap.Should().Be(12.5);
        dto.FlightId.Should().Be(3);
        dto.FlightName.Should().Be("A Flight");
    }

    [Fact]
    public void FlightDto_AllPropertiesAccessible()
    {
        var dto = new FlightDto(1, 2, null, "B Flight", 2, 10);
        dto.Id.Should().Be(1);
        dto.SeasonId.Should().Be(2);
        dto.Name.Should().Be("B Flight");
        dto.DisplayOrder.Should().Be(2);
        dto.PlayerCount.Should().Be(10);
    }

    [Fact]
    public void FlightStandingDto_AllPropertiesAccessible()
    {
        var dto = new FlightStandingDto(1, 5, "Alice Smith", "AS", 8.5, 4, 120, 30.0, 28);
        dto.Rank.Should().Be(1);
        dto.PlayerId.Should().Be(5);
        dto.PlayerName.Should().Be("Alice Smith");
        dto.Initials.Should().Be("AS");
        dto.CurrentHandicapIndex.Should().Be(8.5);
        dto.RoundsPlayed.Should().Be(4);
        dto.SeasonTotalPoints.Should().Be(120);
        dto.AvgPointsPerRound.Should().Be(30.0);
        dto.LastRoundPoints.Should().Be(28);
    }

    [Fact]
    public void HandicapDto_AllPropertiesAccessible()
    {
        var dto = new HandicapDto(1, 42, 10.4, new DateOnly(2026, 1, 1), HandicapSource.Manual, "test note");
        dto.Id.Should().Be(1);
        dto.PlayerId.Should().Be(42);
        dto.HandicapIndex.Should().Be(10.4);
        dto.EffectiveDate.Should().Be(new DateOnly(2026, 1, 1));
        dto.Source.Should().Be(HandicapSource.Manual);
        dto.Notes.Should().Be("test note");
    }

    [Fact]
    public void HoleScoreDto_AllPropertiesAccessible()
    {
        var dto = new HoleScoreDto(5, 3, 4, 7, 5, 1, 4, 2, false);
        dto.Id.Should().Be(5);
        dto.HoleNumber.Should().Be(3);
        dto.Par.Should().Be(4);
        dto.StrokeIndex.Should().Be(7);
        dto.GrossStrokes.Should().Be(5);
        dto.HandicapStrokes.Should().Be(1);
        dto.NetStrokes.Should().Be(4);
        dto.StablefordPoints.Should().Be(2);
        dto.IsMaxScore.Should().BeFalse();
    }

    [Fact]
    public void ParticipantDto_AllPropertiesAccessible()
    {
        var dto = new ParticipantDto(1, 2, 3, "John Doe", "JD", 10.0, 9, 85, 76, 35, false);
        dto.Id.Should().Be(1);
        dto.RoundId.Should().Be(2);
        dto.PlayerId.Should().Be(3);
        dto.PlayerFullName.Should().Be("John Doe");
        dto.PlayerInitials.Should().Be("JD");
        dto.HandicapIndex.Should().Be(10.0);
        dto.CourseHandicap.Should().Be(9);
        dto.TotalGrossStrokes.Should().Be(85);
        dto.TotalNetStrokes.Should().Be(76);
        dto.TotalStablefordPoints.Should().Be(35);
        dto.IsWithdrawn.Should().BeFalse();
    }

    [Fact]
    public void RoundDto_AllPropertiesAccessible()
    {
        var dto = new RoundDto(1, 2, 3, "A Flight", 4, "Course A", new DateOnly(2026, 5, 1), RoundStatus.Scheduled, RoundType.NineHole, NineHoleSide.Front, 8);
        dto.Id.Should().Be(1);
        dto.SeasonId.Should().Be(2);
        dto.FlightId.Should().Be(3);
        dto.FlightName.Should().Be("A Flight");
        dto.CourseId.Should().Be(4);
        dto.CourseName.Should().Be("Course A");
        dto.ScheduledDate.Should().Be(new DateOnly(2026, 5, 1));
        dto.Status.Should().Be(RoundStatus.Scheduled);
        dto.ParticipantCount.Should().Be(8);
    }

    [Fact]
    public void ScorecardDto_AllPropertiesAccessible()
    {
        var participant = new ParticipantDto(1, 1, 1, "John Doe", "JD", 10.0, 9, 85, 76, 35, false);
        var dto = new ScorecardDto(1, new DateOnly(2026, 5, 1), "Course A", 72.0, 113,
            participant, [], 36, 36, 72, 40, 40, 80, 36, 36, 72, 18, 18, 36);
        dto.RoundId.Should().Be(1);
        dto.RoundDate.Should().Be(new DateOnly(2026, 5, 1));
        dto.CourseName.Should().Be("Course A");
        dto.CourseRating.Should().Be(72.0);
        dto.SlopeRating.Should().Be(113);
        dto.Participant.Should().Be(participant);
        dto.HoleScores.Should().BeEmpty();
        dto.FrontNinePar.Should().Be(36);
        dto.BackNinePar.Should().Be(36);
        dto.TotalPar.Should().Be(72);
        dto.FrontNineGross.Should().Be(40);
        dto.BackNineGross.Should().Be(40);
        dto.FrontNineNet.Should().Be(36);
        dto.BackNineNet.Should().Be(36);
        dto.FrontNinePoints.Should().Be(18);
        dto.BackNinePoints.Should().Be(18);
    }

    [Fact]
    public void StandingDto_AllPropertiesAccessible()
    {
        var dto = new StandingDto(1, 5, "Alice Smith", "AS", 4, 120, 30.0, 8.5);
        dto.Position.Should().Be(1);
        dto.PlayerId.Should().Be(5);
        dto.PlayerFullName.Should().Be("Alice Smith");
        dto.PlayerInitials.Should().Be("AS");
        dto.RoundsPlayed.Should().Be(4);
        dto.TotalPoints.Should().Be(120);
        dto.AveragePoints.Should().Be(30.0);
        dto.CurrentHandicapIndex.Should().Be(8.5);
    }

    [Fact]
    public void RoundParticipantDto_AllPropertiesAccessible()
    {
        var dto = new RoundParticipantDto(1, 2, 3, "John Doe", 10.5, 9, false);
        dto.Id.Should().Be(1);
        dto.RoundId.Should().Be(2);
        dto.PlayerId.Should().Be(3);
        dto.PlayerName.Should().Be("John Doe");
        dto.HandicapAtTime.Should().Be(10.5);
        dto.CourseHandicap.Should().Be(9);
        dto.IsWithdrawn.Should().BeFalse();
    }

    [Fact]
    public void RoundScorecardHoleDto_AllPropertiesAccessible()
    {
        var dto = new RoundScorecardHoleDto(3, 4, 5, 4, 7);
        dto.HoleNumber.Should().Be(3);
        dto.Par.Should().Be(4);
        dto.Strokes.Should().Be(5);
        dto.NetStrokes.Should().Be(4);
        dto.StrokeIndex.Should().Be(7);
    }

    [Fact]
    public void RoundScorecardDto_AllPropertiesAccessible()
    {
        var dto = new RoundScorecardDto(1, 2, "John Doe", "Course A",
            new DateOnly(2026, 5, 1), 10.5, 85, 76, 35, []);
        dto.RoundId.Should().Be(1);
        dto.PlayerId.Should().Be(2);
        dto.PlayerName.Should().Be("John Doe");
        dto.CourseName.Should().Be("Course A");
        dto.ScheduledDate.Should().Be(new DateOnly(2026, 5, 1));
        dto.HandicapAtTime.Should().Be(10.5);
        dto.GrossScore.Should().Be(85);
        dto.NetScore.Should().Be(76);
        dto.Points.Should().Be(35);
        dto.Holes.Should().BeEmpty();
    }

    [Fact]
    public void AuditLogEntryDto_AllPropertiesAccessible()
    {
        var dto = new AuditLogEntryDto(1, "2026-01-15T10:00:00Z", "Create", "Player", "42", "admin", "{}");
        dto.Id.Should().Be(1);
        dto.Timestamp.Should().Be("2026-01-15T10:00:00Z");
        dto.Action.Should().Be("Create");
        dto.EntityType.Should().Be("Player");
        dto.EntityId.Should().Be("42");
        dto.UserId.Should().Be("admin");
        dto.Details.Should().Be("{}");
    }

    [Fact]
    public void AuditLogPageDto_AllPropertiesAccessible()
    {
        var items = new List<AuditLogEntryDto>();
        var dto = new AuditLogPageDto(items, 100, 2, 25);
        dto.Items.Should().BeSameAs(items);
        dto.TotalCount.Should().Be(100);
        dto.Page.Should().Be(2);
        dto.PageSize.Should().Be(25);
    }
}
