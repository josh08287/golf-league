using GolfLeague.Application.Common;
using GolfLeague.Application.DTOs;
using GolfLeague.Domain.Entities;
using GolfLeague.Domain.Enums;
using GolfLeague.Domain.Interfaces;
using MediatR;

namespace GolfLeague.Application.Players.Commands;

public sealed record SetHandicapCommand(
    int PlayerId,
    double HandicapIndex,
    string? Notes,
    string UserId) : IRequest<Result<HandicapDto>>, IAmAuditableCommand;

public sealed class SetHandicapCommandHandler : IRequestHandler<SetHandicapCommand, Result<HandicapDto>>
{
    private readonly IPlayerRepository _playerRepository;
    private readonly IHandicapRepository _handicapRepository;

    public SetHandicapCommandHandler(
        IPlayerRepository playerRepository,
        IHandicapRepository handicapRepository)
    {
        _playerRepository = playerRepository;
        _handicapRepository = handicapRepository;
    }

    public async Task<Result<HandicapDto>> Handle(SetHandicapCommand request, CancellationToken cancellationToken)
    {
        var player = await _playerRepository.GetByIdAsync(request.PlayerId, cancellationToken);
        if (player is null)
            return Result<HandicapDto>.Fail($"Player with ID {request.PlayerId} not found.");

        var handicap = new Handicap
        {
            PlayerId = request.PlayerId,
            HandicapIndex = request.HandicapIndex,
            EffectiveDate = DateOnly.FromDateTime(DateTime.UtcNow),
            Source = HandicapSource.Manual,
            Notes = request.Notes
        };

        await _handicapRepository.AddAsync(handicap, cancellationToken);

        var dto = new HandicapDto(
            handicap.Id,
            handicap.PlayerId,
            handicap.HandicapIndex,
            handicap.EffectiveDate,
            handicap.Source,
            handicap.Notes);

        return Result<HandicapDto>.Ok(dto);
    }
}
