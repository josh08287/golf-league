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
    string EntraObjectId,
    double InitialHandicapIndex,
    string UserId) : IRequest<Result<PlayerDto>>, IAmAuditableCommand;

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
        var existing = await _playerRepository.GetByEntraObjectIdAsync(request.EntraObjectId, cancellationToken);
        if (existing is not null)
            return Result<PlayerDto>.Fail($"A player with Entra Object ID '{request.EntraObjectId}' already exists.");

        var player = new Player
        {
            FirstName = request.FirstName,
            LastName = request.LastName,
            Email = request.Email,
            EntraObjectId = request.EntraObjectId,
            IsActive = true
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

        var dto = new PlayerDto(
            player.Id,
            player.FirstName,
            player.LastName,
            player.FullName,
            player.Initials,
            player.Email,
            player.EntraObjectId,
            player.IsActive,
            request.InitialHandicapIndex);

        return Result<PlayerDto>.Ok(dto);
    }
}
