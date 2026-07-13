using GolfLeague.Application.Common;
using GolfLeague.Application.DTOs;
using GolfLeague.Application.Interfaces;
using GolfLeague.Domain.Entities;
using GolfLeague.Domain.Enums;
using GolfLeague.Domain.Interfaces;
using MediatR;

namespace GolfLeague.Application.Players.Commands;

public sealed record CreatePlayerCommand(
    string FirstName,
    string LastName,
    string? Email,
    double InitialHandicapIndex,
    string UserId,
    int? FlightId = null) : IRequest<Result<PlayerDto>>, IAmAuditableCommand
{
    public string AuditEntityType => "Player";
    public string AuditEntityId => "0"; // assigned by the DB; unknown until Handle runs
}

public sealed class CreatePlayerCommandHandler : IRequestHandler<CreatePlayerCommand, Result<PlayerDto>>
{
    private readonly IPlayerRepository _playerRepository;
    private readonly IHandicapRepository _handicapRepository;
    private readonly ILeagueContext _leagueContext;

    public CreatePlayerCommandHandler(
        IPlayerRepository playerRepository,
        IHandicapRepository handicapRepository,
        ILeagueContext leagueContext)
    {
        _playerRepository = playerRepository;
        _handicapRepository = handicapRepository;
        _leagueContext = leagueContext;
    }

    public async Task<Result<PlayerDto>> Handle(CreatePlayerCommand request, CancellationToken cancellationToken)
    {
        if (_leagueContext.LeagueId is null)
            return Result<PlayerDto>.Fail("No league context.");

        // Duplicate-email check is only meaningful when an email is provided.
        // Players without an email (e.g. guest entries) can be created freely.
        if (!string.IsNullOrWhiteSpace(request.Email))
        {
            var existing = await _playerRepository.GetByEmailAsync(request.Email, _leagueContext.LeagueId.Value, cancellationToken);
            if (existing is not null)
                return Result<PlayerDto>.Fail($"A player with email '{request.Email}' already exists.");
        }

        var player = new Player
        {
            LeagueId = _leagueContext.LeagueId.Value,
            FirstName = request.FirstName,
            LastName = request.LastName,
            Email = string.IsNullOrWhiteSpace(request.Email) ? null : request.Email,
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
