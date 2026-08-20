using GolfLeague.Application.Common;
using GolfLeague.Domain.Enums;
using GolfLeague.Domain.Interfaces;
using MediatR;

namespace GolfLeague.Application.Rounds.Commands;

/// <summary>
/// Records (or clears) the longest-drive winner for one tournament flight,
/// on the round's admin-configured LongestDriveHoleNumber, saved immediately
/// as the player enters it. Any active member of the submitting tee-time
/// group may call this; the winner must be a member of that same group AND
/// share the target tournament flight — a foursome spanning two flights
/// needs one call per flight represented in the group.
/// </summary>
public sealed record SetTeeTimeTournamentLongestDriveCommand(
    int TeeTimeId,
    int SubmittedByPlayerId,
    int TournamentFlightId,
    int? WinnerPlayerId,
    string UserId) : IRequest<Result<TournamentLongestDriveResultDto>>, IAmAuditableCommand
{
    public string AuditEntityType => "TeeTime";
    public string AuditEntityId => TeeTimeId.ToString();
}

public sealed record TournamentLongestDriveResultDto(int TournamentFlightId, string FlightName, int? WinnerPlayerId, string? WinnerPlayerName);

public sealed class SetTeeTimeTournamentLongestDriveCommandHandler
    : IRequestHandler<SetTeeTimeTournamentLongestDriveCommand, Result<TournamentLongestDriveResultDto>>
{
    private readonly IRoundRepository _roundRepository;
    private readonly ITeeTimeRepository _teeTimeRepository;

    public SetTeeTimeTournamentLongestDriveCommandHandler(IRoundRepository roundRepository, ITeeTimeRepository teeTimeRepository)
    {
        _roundRepository = roundRepository;
        _teeTimeRepository = teeTimeRepository;
    }

    public async Task<Result<TournamentLongestDriveResultDto>> Handle(SetTeeTimeTournamentLongestDriveCommand request, CancellationToken cancellationToken)
    {
        var teeTime = await _teeTimeRepository.GetByIdAsync(request.TeeTimeId, cancellationToken);
        if (teeTime is null)
            return Result<TournamentLongestDriveResultDto>.Fail($"Tee time {request.TeeTimeId} not found.");

        var submitter = teeTime.Participants.FirstOrDefault(p => p.PlayerId == request.SubmittedByPlayerId);
        if (submitter is null || submitter.IsWithdrawn)
            return Result<TournamentLongestDriveResultDto>.Fail("You must be an active member of this tee time to record longest drive.");

        var round = await _roundRepository.GetByIdAsync(teeTime.RoundId, cancellationToken);
        if (round is null)
            return Result<TournamentLongestDriveResultDto>.Fail($"Round for tee time {request.TeeTimeId} not found.");
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

        var participants = await _roundRepository.GetParticipantsAsync(round.Id, cancellationToken);

        var submitterFull = participants.FirstOrDefault(p => p.Id == submitter.Id);
        if (submitterFull?.TournamentFlightId != request.TournamentFlightId)
            return Result<TournamentLongestDriveResultDto>.Fail("You are not a member of that tournament flight.");

        string? winnerName = null;
        if (request.WinnerPlayerId is int winnerId)
        {
            var winner = teeTime.Participants.FirstOrDefault(p => p.PlayerId == winnerId && !p.IsWithdrawn);
            var winnerFull = winner is null ? null : participants.FirstOrDefault(p => p.Id == winner.Id);
            if (winner is null || winnerFull?.TournamentFlightId != request.TournamentFlightId)
                return Result<TournamentLongestDriveResultDto>.Fail("The longest-drive winner must be a member of this tee time group in the same flight.");
            winnerName = winnerFull!.Player.FullName;
        }

        await _roundRepository.SetLongestDriveWinnerAsync(round.Id, request.TournamentFlightId, request.WinnerPlayerId, cancellationToken);

        return Result<TournamentLongestDriveResultDto>.Ok(new TournamentLongestDriveResultDto(flight.Id, flight.Name, request.WinnerPlayerId, winnerName));
    }
}
