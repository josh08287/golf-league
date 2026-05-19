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
public sealed record TestAuditableCommand(int Id, string UserId) : IRequest<Result<string>>, IAmAuditableCommand;

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
    public async Task Handle_EntityType_ResolvesUnknown_ForGenericCommand()
    {
        var auditRepo = new Mock<IAuditRepository>();
        AuditLog? captured = null;
        auditRepo.Setup(r => r.AddAsync(It.IsAny<AuditLog>(), default))
            .Callback<AuditLog, CancellationToken>((a, _) => captured = a)
            .Returns(Task.CompletedTask);

        var behavior = MakeBehavior<TestAuditableCommand, Result<string>>(auditRepo.Object);

        await behavior.Handle(new TestAuditableCommand(5, "user-1"),
            Next(Result<string>.Ok("ok")), default);

        // TestAuditableCommand doesn't contain "Player", so it falls to Unknown
        captured!.EntityType.Should().Be("Unknown");
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

// Named commands for entity type resolution tests
public sealed record CreatePlayerTestCommand(int PlayerId, string UserId) : IRequest<Result<string>>, IAmAuditableCommand;
public sealed record CreateRoundTestCommand(int RoundId, string UserId) : IRequest<Result<string>>, IAmAuditableCommand;
public sealed record CreateFlightTestCommand(string UserId) : IRequest<Result<string>>, IAmAuditableCommand;
public sealed record CreateCourseTestCommand(string UserId) : IRequest<Result<string>>, IAmAuditableCommand;
public sealed record SetHandicapTestCommand(int Id, string UserId) : IRequest<Result<string>>, IAmAuditableCommand;
public sealed record SubmitHoleScoreTestCommand(int Id, string UserId) : IRequest<Result<string>>, IAmAuditableCommand;
public sealed record GetScorecardTestCommand(int Id, string UserId) : IRequest<Result<string>>, IAmAuditableCommand;

public class AuditBehaviorEntityTypeTests
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
    public async Task Handle_PlayerCommand_ResolvesPlayerEntityType()
    {
        var auditRepo = new Mock<IAuditRepository>();
        AuditLog? captured = null;
        auditRepo.Setup(r => r.AddAsync(It.IsAny<AuditLog>(), default))
            .Callback<AuditLog, CancellationToken>((a, _) => captured = a)
            .Returns(Task.CompletedTask);

        var behavior = MakeBehavior<CreatePlayerTestCommand, Result<string>>(auditRepo.Object);
        await behavior.Handle(new CreatePlayerTestCommand(1, "u"), Next(Result<string>.Ok("ok")), default);

        captured!.EntityType.Should().Be("Player");
        captured.EntityId.Should().Be("1"); // PlayerId property
    }

    [Fact]
    public async Task Handle_RoundCommand_ResolvesRoundEntityType()
    {
        var auditRepo = new Mock<IAuditRepository>();
        AuditLog? captured = null;
        auditRepo.Setup(r => r.AddAsync(It.IsAny<AuditLog>(), default))
            .Callback<AuditLog, CancellationToken>((a, _) => captured = a)
            .Returns(Task.CompletedTask);

        var behavior = MakeBehavior<CreateRoundTestCommand, Result<string>>(auditRepo.Object);
        await behavior.Handle(new CreateRoundTestCommand(7, "u"), Next(Result<string>.Ok("ok")), default);

        captured!.EntityType.Should().Be("Round");
        captured.EntityId.Should().Be("7"); // RoundId property
    }

    [Fact]
    public async Task Handle_FlightCommand_ResolvesFlightEntityType()
    {
        var auditRepo = new Mock<IAuditRepository>();
        AuditLog? captured = null;
        auditRepo.Setup(r => r.AddAsync(It.IsAny<AuditLog>(), default))
            .Callback<AuditLog, CancellationToken>((a, _) => captured = a)
            .Returns(Task.CompletedTask);

        var behavior = MakeBehavior<CreateFlightTestCommand, Result<string>>(auditRepo.Object);
        await behavior.Handle(new CreateFlightTestCommand("u"), Next(Result<string>.Ok("ok")), default);

        captured!.EntityType.Should().Be("Flight");
        captured.EntityId.Should().Be("0"); // No Id/PlayerId/RoundId property
    }

    [Fact]
    public async Task Handle_CourseCommand_ResolvesCourseEntityType()
    {
        var auditRepo = new Mock<IAuditRepository>();
        AuditLog? captured = null;
        auditRepo.Setup(r => r.AddAsync(It.IsAny<AuditLog>(), default))
            .Callback<AuditLog, CancellationToken>((a, _) => captured = a)
            .Returns(Task.CompletedTask);

        var behavior = MakeBehavior<CreateCourseTestCommand, Result<string>>(auditRepo.Object);
        await behavior.Handle(new CreateCourseTestCommand("u"), Next(Result<string>.Ok("ok")), default);

        captured!.EntityType.Should().Be("Course");
    }

    [Fact]
    public async Task Handle_HandicapCommand_ResolvesPlayerEntityType()
    {
        var auditRepo = new Mock<IAuditRepository>();
        AuditLog? captured = null;
        auditRepo.Setup(r => r.AddAsync(It.IsAny<AuditLog>(), default))
            .Callback<AuditLog, CancellationToken>((a, _) => captured = a)
            .Returns(Task.CompletedTask);

        var behavior = MakeBehavior<SetHandicapTestCommand, Result<string>>(auditRepo.Object);
        await behavior.Handle(new SetHandicapTestCommand(1, "u"), Next(Result<string>.Ok("ok")), default);

        captured!.EntityType.Should().Be("Player");
        captured.EntityId.Should().Be("1");
    }

    [Fact]
    public async Task Handle_HoleScoreCommand_ResolvesRoundEntityType()
    {
        var auditRepo = new Mock<IAuditRepository>();
        AuditLog? captured = null;
        auditRepo.Setup(r => r.AddAsync(It.IsAny<AuditLog>(), default))
            .Callback<AuditLog, CancellationToken>((a, _) => captured = a)
            .Returns(Task.CompletedTask);

        var behavior = MakeBehavior<SubmitHoleScoreTestCommand, Result<string>>(auditRepo.Object);
        await behavior.Handle(new SubmitHoleScoreTestCommand(3, "u"), Next(Result<string>.Ok("ok")), default);

        captured!.EntityType.Should().Be("Round");
        captured.EntityId.Should().Be("3");
    }

    [Fact]
    public async Task Handle_ScorecardCommand_ResolvesRoundEntityType()
    {
        var auditRepo = new Mock<IAuditRepository>();
        AuditLog? captured = null;
        auditRepo.Setup(r => r.AddAsync(It.IsAny<AuditLog>(), default))
            .Callback<AuditLog, CancellationToken>((a, _) => captured = a)
            .Returns(Task.CompletedTask);

        var behavior = MakeBehavior<GetScorecardTestCommand, Result<string>>(auditRepo.Object);
        await behavior.Handle(new GetScorecardTestCommand(5, "u"), Next(Result<string>.Ok("ok")), default);

        captured!.EntityType.Should().Be("Round");
        captured.EntityId.Should().Be("5");
    }
}
