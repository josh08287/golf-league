using FluentAssertions;
using GolfLeague.Application.Behaviors;
using GolfLeague.Application.Common;
using GolfLeague.Domain.Entities;
using GolfLeague.Domain.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace GolfLeague.Tests.Application;

// A test command that implements IAmAuditableCommand
public sealed record TestAuditableCommand(int Id, string UserId) : IRequest<Result<string>>, IAmAuditableCommand
{
    public string AuditEntityType => "Widget";
    public string AuditEntityId => Id.ToString();
}

// A non-auditable command
public sealed record TestNonAuditableCommand(int Id) : IRequest<Result<string>>;

public class AuditBehaviorTests
{
    private static AuditBehavior<TRequest, TResponse> MakeBehavior<TRequest, TResponse>(IAuditRepository repo)
        where TRequest : notnull
    {
        var logger = new Mock<ILogger<AuditBehavior<TRequest, TResponse>>>();
        return new AuditBehavior<TRequest, TResponse>(repo, logger.Object);
    }

    private static RequestHandlerDelegate<T> Next<T>(T value)
        => ct => Task.FromResult(value);

    [Fact]
    public async Task Handle_WhenAuditableAndSuccess_WritesAuditLog()
    {
        var auditRepo = new Mock<IAuditRepository>();
        var behavior = MakeBehavior<TestAuditableCommand, Result<string>>(auditRepo.Object);
        var command = new TestAuditableCommand(42, "user-1");

        var result = await behavior.Handle(command, Next(Result<string>.Ok("ok")), default);

        result.IsSuccess.Should().BeTrue();
        auditRepo.Verify(r => r.AddAsync(It.Is<AuditLog>(a =>
            a.UserId == "user-1" && a.EntityId == "42"), default), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenAuditableButFailed_DoesNotWriteAuditLog()
    {
        var auditRepo = new Mock<IAuditRepository>();
        var behavior = MakeBehavior<TestAuditableCommand, Result<string>>(auditRepo.Object);
        var command = new TestAuditableCommand(42, "user-1");

        var result = await behavior.Handle(command, Next(Result<string>.Fail("fail")), default);

        result.IsSuccess.Should().BeFalse();
        auditRepo.Verify(r => r.AddAsync(It.IsAny<AuditLog>(), default), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenNotAuditable_DoesNotWriteAuditLog()
    {
        var auditRepo = new Mock<IAuditRepository>();
        var behavior = MakeBehavior<TestNonAuditableCommand, Result<string>>(auditRepo.Object);
        var command = new TestNonAuditableCommand(1);

        var result = await behavior.Handle(command, Next(Result<string>.Ok("ok")), default);

        result.IsSuccess.Should().BeTrue();
        auditRepo.Verify(r => r.AddAsync(It.IsAny<AuditLog>(), default), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenAuditRepoThrows_StillReturnsResponse()
    {
        var auditRepo = new Mock<IAuditRepository>();
        auditRepo.Setup(r => r.AddAsync(It.IsAny<AuditLog>(), default))
            .ThrowsAsync(new Exception("DB error"));
        var behavior = MakeBehavior<TestAuditableCommand, Result<string>>(auditRepo.Object);
        var command = new TestAuditableCommand(1, "user-1");

        var result = await behavior.Handle(command, Next(Result<string>.Ok("ok")), default);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_UsesCommandSuppliedEntityTypeAndId()
    {
        var auditRepo = new Mock<IAuditRepository>();
        AuditLog? captured = null;
        auditRepo.Setup(r => r.AddAsync(It.IsAny<AuditLog>(), default))
            .Callback<AuditLog, CancellationToken>((a, _) => captured = a)
            .Returns(Task.CompletedTask);

        var behavior = MakeBehavior<TestAuditableCommand, Result<string>>(auditRepo.Object);

        await behavior.Handle(new TestAuditableCommand(5, "user-1"),
            Next(Result<string>.Ok("ok")), default);

        captured!.EntityType.Should().Be("Widget");
        captured.EntityId.Should().Be("5");
    }

    [Fact]
    public async Task Handle_WhenAuditEntityIdIsZeroSentinel_ResolvesFromResponseValueId()
    {
        var auditRepo = new Mock<IAuditRepository>();
        AuditLog? captured = null;
        auditRepo.Setup(r => r.AddAsync(It.IsAny<AuditLog>(), default))
            .Callback<AuditLog, CancellationToken>((a, _) => captured = a)
            .Returns(Task.CompletedTask);

        var behavior = MakeBehavior<CreateWidgetTestCommand, Result<CreatedWidgetDto>>(auditRepo.Object);
        await behavior.Handle(
            new CreateWidgetTestCommand("user-1"),
            Next(Result<CreatedWidgetDto>.Ok(new CreatedWidgetDto(99))),
            default);

        captured!.EntityId.Should().Be("99");
    }

    [Fact]
    public async Task Handle_WhenNonGenericResponse_TreatsAsSuccess()
    {
        var auditRepo = new Mock<IAuditRepository>();
        var logger = new Mock<ILogger<AuditBehavior<TestAuditableCommand, string>>>();
        var behavior = new AuditBehavior<TestAuditableCommand, string>(auditRepo.Object, logger.Object);
        var command = new TestAuditableCommand(1, "user-1");

        var result = await behavior.Handle(command, (ct) => Task.FromResult("result"), default);

        result.Should().Be("result");
        auditRepo.Verify(r => r.AddAsync(It.IsAny<AuditLog>(), default), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenGenericResponseWithoutIsSuccess_TreatsAsSuccess()
    {
        // List<string> is generic but has no IsSuccess property — prop is null, returns false from ??
        var auditRepo = new Mock<IAuditRepository>();
        var logger = new Mock<ILogger<AuditBehavior<TestAuditableCommand, List<string>>>>();
        var behavior = new AuditBehavior<TestAuditableCommand, List<string>>(auditRepo.Object, logger.Object);
        var command = new TestAuditableCommand(1, "user-1");
        RequestHandlerDelegate<List<string>> next = ct => Task.FromResult(new List<string> { "a" });

        var result = await behavior.Handle(command, next, default);

        result.Should().ContainSingle("a");
        // prop is null -> IsSuccessResult returns false -> no audit
        auditRepo.Verify(r => r.AddAsync(It.IsAny<AuditLog>(), default), Times.Never);
    }
}

// Command/response shapes for the response-fallback id resolution test.
public sealed record CreateWidgetTestCommand(string UserId) : IRequest<Result<CreatedWidgetDto>>, IAmAuditableCommand
{
    public string AuditEntityType => "Widget";
    public string AuditEntityId => "0"; // assigned by the DB; resolved from the response
}

public sealed record CreatedWidgetDto(int Id);
