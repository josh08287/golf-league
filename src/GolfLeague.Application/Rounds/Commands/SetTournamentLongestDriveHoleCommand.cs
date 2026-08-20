using GolfLeague.Application.Common;
using GolfLeague.Domain.Enums;
using GolfLeague.Domain.Interfaces;
using MediatR;

namespace GolfLeague.Application.Rounds.Commands;

/// <summary>
/// Sets (or clears, when HoleNumber is null) the tournament round's
/// longest-drive hole. Only allowed while Scheduled; must not be a par 3.
/// </summary>
public sealed record SetTournamentLongestDriveHoleCommand(
    int RoundId,
    int? HoleNumber,
    string UserId) : IRequest<Result<bool>>, IAmAuditableCommand
{
    public string AuditEntityType => "Round";
    public string AuditEntityId => RoundId.ToString();
}

public sealed class SetTournamentLongestDriveHoleCommandHandler : IRequestHandler<SetTournamentLongestDriveHoleCommand, Result<bool>>
{
    private readonly IRoundRepository _roundRepository;
    private readonly ICourseRepository _courseRepository;

    public SetTournamentLongestDriveHoleCommandHandler(IRoundRepository roundRepository, ICourseRepository courseRepository)
    {
        _roundRepository = roundRepository;
        _courseRepository = courseRepository;
    }

    public async Task<Result<bool>> Handle(SetTournamentLongestDriveHoleCommand request, CancellationToken cancellationToken)
    {
        var round = await _roundRepository.GetByIdAsync(request.RoundId, cancellationToken);
        if (round is null)
            return Result<bool>.Fail($"Round {request.RoundId} not found.");
        if (round.RoundType != RoundType.Tournament)
            return Result<bool>.Fail("This round is not a tournament round.");
        if (round.Status != RoundStatus.Scheduled)
            return Result<bool>.Fail("The longest-drive hole can only be changed while the round is Scheduled.");

        if (request.HoleNumber is int holeNumber)
        {
            var hole = round.Course.Holes.FirstOrDefault(h => h.HoleNumber == holeNumber);
            if (hole is null)
                return Result<bool>.Fail($"Hole {holeNumber} was not found for this course.");
            if (hole.Par == 3)
                return Result<bool>.Fail("The longest-drive hole cannot be a par 3.");
        }

        round.LongestDriveHoleNumber = request.HoleNumber;
        await _roundRepository.UpdateAsync(round, cancellationToken);

        return Result<bool>.Ok(true);
    }
}
