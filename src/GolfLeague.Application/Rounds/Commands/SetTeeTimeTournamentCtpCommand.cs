using GolfLeague.Application.Common;
using GolfLeague.Domain.Entities;
using GolfLeague.Domain.Enums;
using GolfLeague.Domain.Interfaces;
using MediatR;

namespace GolfLeague.Application.Rounds.Commands;

/// <summary>
/// Records (or clears) the closest-to-pin winner for one par-3 hole of a
/// tournament round, saved immediately as the player enters it — unlike the
/// admin-only round-wide SaveTournamentExtrasCommand batch editor. Any
/// active member of the submitting tee-time group may call this; the winner
/// must be a member of that same group (foursomes are the only ones who can
/// actually observe the shot).
/// </summary>
public sealed record SetTeeTimeTournamentCtpCommand(
    int TeeTimeId,
    int SubmittedByPlayerId,
    int HoleNumber,
    int? WinnerPlayerId,
    string UserId) : IRequest<Result<TournamentCtpResultDto>>, IAmAuditableCommand
{
    public string AuditEntityType => "TeeTime";
    public string AuditEntityId => TeeTimeId.ToString();
}

public sealed record TournamentCtpResultDto(int HoleNumber, int? WinnerPlayerId, string? WinnerPlayerName);

public sealed class SetTeeTimeTournamentCtpCommandHandler : IRequestHandler<SetTeeTimeTournamentCtpCommand, Result<TournamentCtpResultDto>>
{
    private readonly IRoundRepository _roundRepository;
    private readonly ITeeTimeRepository _teeTimeRepository;
    private readonly ICourseRepository _courseRepository;

    public SetTeeTimeTournamentCtpCommandHandler(
        IRoundRepository roundRepository,
        ITeeTimeRepository teeTimeRepository,
        ICourseRepository courseRepository)
    {
        _roundRepository = roundRepository;
        _teeTimeRepository = teeTimeRepository;
        _courseRepository = courseRepository;
    }

    public async Task<Result<TournamentCtpResultDto>> Handle(SetTeeTimeTournamentCtpCommand request, CancellationToken cancellationToken)
    {
        var teeTime = await _teeTimeRepository.GetByIdAsync(request.TeeTimeId, cancellationToken);
        if (teeTime is null)
            return Result<TournamentCtpResultDto>.Fail($"Tee time {request.TeeTimeId} not found.");

        var submitter = teeTime.Participants.FirstOrDefault(p => p.PlayerId == request.SubmittedByPlayerId);
        if (submitter is null || submitter.IsWithdrawn)
            return Result<TournamentCtpResultDto>.Fail("You must be an active member of this tee time to record closest-to-pin.");

        var round = await _roundRepository.GetByIdAsync(teeTime.RoundId, cancellationToken);
        if (round is null)
            return Result<TournamentCtpResultDto>.Fail($"Round for tee time {request.TeeTimeId} not found.");
        if (round.RoundType != RoundType.Tournament)
            return Result<TournamentCtpResultDto>.Fail("This round is not a tournament round.");
        if (round.Status == RoundStatus.Finalized || round.Status == RoundStatus.Cancelled)
            return Result<TournamentCtpResultDto>.Fail($"Cannot record closest-to-pin on a round with status '{round.Status}'.");

        RoundParticipant? winner = null;
        if (request.WinnerPlayerId is int winnerId)
        {
            winner = teeTime.Participants.FirstOrDefault(p => p.PlayerId == winnerId && !p.IsWithdrawn);
            if (winner is null)
                return Result<TournamentCtpResultDto>.Fail("The closest-to-pin winner must be an active member of this tee time group.");

            var holes = await _courseRepository.GetHolesAsync(round.CourseId, cancellationToken);
            var hole = holes.FirstOrDefault(h => h.HoleNumber == request.HoleNumber);
            if (hole is null || hole.Par != 3)
                return Result<TournamentCtpResultDto>.Fail($"Hole {request.HoleNumber} is not a par 3 on this course.");
        }

        await _roundRepository.UpsertTournamentHoleExtrasAsync(
            [new TournamentHoleExtra { RoundId = round.Id, HoleNumber = request.HoleNumber, ClosestToPinPlayerId = winner?.PlayerId }],
            cancellationToken);

        return Result<TournamentCtpResultDto>.Ok(new TournamentCtpResultDto(request.HoleNumber, winner?.PlayerId, winner?.Player.FullName));
    }
}
