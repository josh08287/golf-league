using GolfLeague.Application.Common;
using GolfLeague.Application.DTOs;
using GolfLeague.Domain.Entities;
using GolfLeague.Domain.Enums;
using GolfLeague.Domain.Interfaces;
using MediatR;

namespace GolfLeague.Application.Players.Commands;

public sealed record CreatePlayerCommand(
    string FirstName,
    string LastName,
    string Email,
    double InitialHandicapIndex,
    string UserId,
    int? FlightId = null) : IRequest<Result<PlayerDto>>, IAmAuditableCommand;

public sealed class CreatePlayerCommandHandler : IRequestHandler<CreatePlayerCommand, Result<PlayerDto>>
{
    private readonly IPlayerRepository _playerRepository;
    private readonly IHandicapRepository _handicapRepository;

    public CreatePlayerCommandHandler(
        IPlayerRepository playerRepository,
        IHandicapRepository handicapRepository)
    {
        _playerRepository = playerRepository;
        _handicapRepository = handicapRepository;
    }

    public async Task<Result<PlayerDto>> Handle(CreatePlayerCommand request, CancellationToken cancellationToken)
    {
        var existing = await _playerRepository.GetByEmailAsync(request.Email, cancellationToken);
        if (existing is not null)
            return Result<PlayerDto>.Fail($"A player with email '{request.Email}' already exists.");

        var player = new Player
        {
            FirstName = request.FirstName,
            LastName = request.LastName,
            Email = request.Email,
            IsActive = true,
        };

        await _playerRepository.AddAsync(player, cancellationToken);

        var handicap = new Handicap
        {
            PlayerId = player.Id,
            HandicapIndex = request.InitialHandicapIndex,
            EffectiveDate = DateOnly.FromDateTime(DateTime.UtcNow),
            Source = HandicapSource.Initial
        };

        await _handicapRepository.AddAsync(handicap, cancellationToken);

        if (request.FlightId is int flightId)
        {
            try
            {
                await _playerRepository.AssignToFlightAsync(player.Id, flightId, cancellationToken);
            }
            catch (Exception ex)
            {
                return Result<PlayerDto>.Fail(
                    $"Player created but flight assignment failed: {ex.Message}");
            }
        }

        // Newly-created players have no AppUser yet (admin pre-creates the
        // roster row, the user gains an AppUser by accepting an invite).
        // So roles is empty until acceptance.
        var dto = new PlayerDto(
            player.Id,
            player.FullName,
            player.Email,
            player.IsActive,
            request.InitialHandicapIndex,
            null,
            null,
            Array.Empty<string>());

        return Result<PlayerDto>.Ok(dto);
    }
}
