using FluentAssertions;
using GolfLeague.Application.Players.Commands;
using GolfLeague.Domain.Entities;
using GolfLeague.Domain.Interfaces;
using Moq;
using Xunit;

namespace GolfLeague.Tests.Application;

public class DeletePlayerCommandHandlerTests
{
    private readonly Mock<IPlayerRepository> _playerRepo = new();

    [Fact]
    public async Task Handle_WhenPlayerNotFound_ReturnsFail()
    {
        // Arrange
        const int playerId = 42;
        _playerRepo.Setup(r => r.GetByIdAsync(playerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Player?)null);

        var handler = new DeletePlayerCommandHandler(_playerRepo.Object);
        var command = new DeletePlayerCommand(playerId, "admin-user");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain($"Player with ID {playerId} not found.");
        _playerRepo.Verify(r => r.DeleteAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenPlayerExistsWithLinkedUser_DeletesPlayerFromRepositoryWithoutDeletingUserAccount()
    {
        // Arrange
        const int playerId = 10;
        var linkedUserId = Guid.NewGuid();
        var player = new Player
        {
            Id = playerId,
            FirstName = "Jane",
            LastName = "Doe",
            Email = "jane@example.com",
            AppUserId = linkedUserId,
            IsActive = true
        };

        _playerRepo.Setup(r => r.GetByIdAsync(playerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(player);
        _playerRepo.Setup(r => r.DeleteAsync(playerId, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Notice DeletePlayerCommandHandler does not take IAdminUserService,
        // ensuring global user account is not deleted.
        var handler = new DeletePlayerCommandHandler(_playerRepo.Object);
        var command = new DeletePlayerCommand(playerId, "admin-user");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeTrue();
        _playerRepo.Verify(r => r.DeleteAsync(playerId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenPlayerExistsWithoutLinkedUser_DeletesPlayerFromRepository()
    {
        // Arrange
        const int playerId = 11;
        var player = new Player
        {
            Id = playerId,
            FirstName = "John",
            LastName = "Smith",
            Email = "john@example.com",
            AppUserId = null,
            IsActive = true
        };

        _playerRepo.Setup(r => r.GetByIdAsync(playerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(player);
        _playerRepo.Setup(r => r.DeleteAsync(playerId, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var handler = new DeletePlayerCommandHandler(_playerRepo.Object);
        var command = new DeletePlayerCommand(playerId, "admin-user");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeTrue();
        _playerRepo.Verify(r => r.DeleteAsync(playerId, It.IsAny<CancellationToken>()), Times.Once);
    }
}
