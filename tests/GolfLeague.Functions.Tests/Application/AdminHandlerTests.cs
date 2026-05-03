using FluentAssertions;
using GolfLeague.Application.Admin;
using GolfLeague.Domain.Entities;
using GolfLeague.Domain.Interfaces;
using Moq;
using Xunit;

namespace GolfLeague.Tests.Application;

public class GetAuditLogQueryHandlerTests
{
    [Fact]
    public async Task Handle_ReturnsMappedPagedResult()
    {
        var items = new List<AuditLog>
        {
            new()
            {
                Id = 1,
                Action = "Create",
                EntityType = "Player",
                EntityId = "42",
                UserId = "admin",
                Timestamp = new DateTime(2026, 1, 15, 10, 0, 0, DateTimeKind.Utc),
                AfterJson = "{}"
            }
        };

        var repo = new Mock<IAuditRepository>();
        repo.Setup(r => r.GetPagedAsync(1, 25, default)).ReturnsAsync((items, 1));

        var handler = new GetAuditLogQueryHandler(repo.Object);
        var result = await handler.Handle(new GetAuditLogQuery(1, 25), default);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Items.Should().HaveCount(1);
        result.Value.TotalCount.Should().Be(1);
        result.Value.Page.Should().Be(1);
        result.Value.PageSize.Should().Be(25);

        var entry = result.Value.Items[0];
        entry.Id.Should().Be(1);
        entry.Action.Should().Be("Create");
        entry.EntityType.Should().Be("Player");
        entry.EntityId.Should().Be("42");
        entry.UserId.Should().Be("admin");
        entry.Timestamp.Should().Contain("2026-01-15");
        entry.Details.Should().Be("{}");
    }

    [Fact]
    public async Task Handle_WithNoItems_ReturnsEmptyList()
    {
        var repo = new Mock<IAuditRepository>();
        repo.Setup(r => r.GetPagedAsync(1, 25, default)).ReturnsAsync((new List<AuditLog>(), 0));

        var handler = new GetAuditLogQueryHandler(repo.Object);
        var result = await handler.Handle(new GetAuditLogQuery(1, 25), default);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Items.Should().BeEmpty();
        result.Value.TotalCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_MapsTimestampToIso8601()
    {
        var timestamp = new DateTime(2026, 5, 1, 12, 0, 0, DateTimeKind.Utc);
        var items = new List<AuditLog>
        {
            new() { Id = 1, Action = "Update", EntityType = "Round", EntityId = "1",
                    UserId = "admin", Timestamp = timestamp, AfterJson = null }
        };

        var repo = new Mock<IAuditRepository>();
        repo.Setup(r => r.GetPagedAsync(2, 10, default)).ReturnsAsync((items, 1));

        var handler = new GetAuditLogQueryHandler(repo.Object);
        var result = await handler.Handle(new GetAuditLogQuery(2, 10), default);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Page.Should().Be(2);
        result.Value.PageSize.Should().Be(10);
        result.Value.Items[0].Timestamp.Should().Be(timestamp.ToString("O"));
        result.Value.Items[0].Details.Should().BeNull();
    }
}
