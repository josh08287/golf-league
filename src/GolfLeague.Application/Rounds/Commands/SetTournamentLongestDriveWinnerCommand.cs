using GolfLeague.Application.Common;
using GolfLeague.Domain.Enums;
using GolfLeague.Domain.Interfaces;
using MediatR;

namespace GolfLeague.Application.Rounds.Commands;

/// <summary>
/// Admin-facing counterpart to SetTeeTimeTournamentLongestDriveCommand: sets
/// (or clears) the longest-drive winner for one tournament flight without the
/// tee-time-group membership restriction that command enforces for players.
/// Lets an admin review and correct every flight's winner from the round's
/// scores page before finalizing, regardless of who submitted scores live.
/// </summary>
public sealed record SetTournamentLongestDriveWinnerCommand(
    int RoundId,
    int TournamentFlightId,
    int? WinnerPlayerId,
    string UserId) : IRequest<Result<TournamentLongestDriveResultDto>>, IAmAuditableCommand
{
    public string AuditEntityType => "Round";
    public string AuditEntityId => RoundId.ToString();
}

public sealed class SetTournamentLongestDriveWinnerCommandHandler
    : IRequestHandler<SetTournamentLongestDriveWinnerCommand, Result<TournamentLongestDriveResultDto>>
{
    private readonly IRoundRepository _roundRepository;

    public SetTournamentLongestDriveWinnerCommandHandler(IRoundRepository roundRepository)
    {
        _roundRepository = roundRepository;
    }

    public async Task<Result<TournamentLongestDriveResultDto>> Handle(
        SetTournamentLongestDriveWinnerCommand request, CancellationToken cancellationToken)
    {
        var round = await _roundRepository.GetByIdAsync(request.RoundId, cancellationToken);
        if (round is null)
            return Result<TournamentLongestDriveResultDto>.Fail($"Round {request.RoundId} not found.");
        if (round.RoundType != RoundType.Tournament)
            return Result<TournamentLongestDriveResultDto>.Fail("This round is not a tournament round.");
        if (round.Status == RoundStatus.Finalized || round.Status == RoundStatus.Cancelled)
            return Result<TournamentLongestDriveResultDto>.Fail($"Cannot record longest drive on a round with status '{round.Status}'.");
        if (round.LongestDriveHoleNumber is null)
            return Result<TournamentLongestDriveResultDto>.Fail("This round doesn't have a longest-drive hole configured.");

        var flights = await _roundRepository.GetTournamentFlightsAsync(round.Id, cancellationToken);
        var flight = flights.FirstOrDefault(f => f.Id == request.TournamentFlightId);
        if (flight is null)
            return Result<TournamentLongestDriveResultDto>.Fail("That tournament flight was not found for this round.");

        string? winnerName = null;
        if (request.WinnerPlayerId is int winnerId)
        {
            var participants = await _roundRepository.GetParticipantsAsync(round.Id, cancellationToken);
            var winner = participants.FirstOrDefault(p => p.PlayerId == winnerId && !p.IsWithdrawn);
            if (winner is null || winner.TournamentFlightId != request.TournamentFlightId)
                return Result<TournamentLongestDriveResultDto>.Fail("The longest-drive winner must be an active participant in this flight.");
            winnerName = winner.Player.FullName;
        }

        await _roundRepository.SetLongestDriveWinnerAsync(round.Id, request.TournamentFlightId, request.WinnerPlayerId, cancellationToken);

        return Result<TournamentLongestDriveResultDto>.Ok(new TournamentLongestDriveResultDto(flight.Id, flight.Name, request.WinnerPlayerId, winnerName));
    }
}
