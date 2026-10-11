using GolfLeague.Application.Common;
using GolfLeague.Application.DTOs;
using GolfLeague.Domain.Enums;
using GolfLeague.Domain.Interfaces;
using MediatR;

namespace GolfLeague.Application.Rounds.Commands;

// ── Per-round override (score entry) ──────────────────────────────────────────

/// <summary>
/// Changes the tee one player plays a round from. <see cref="TeeBoxId"/> null
/// reverts to the player's default (per-half default, else the course
/// default) and clears the override. Entered holes are rescored.
/// </summary>
public sealed record SetParticipantTeeBoxCommand(
    int RoundId,
    int PlayerId,
    int? TeeBoxId,
    string UserId) : IRequest<Result<ParticipantTeeDto>>, IAmAuditableCommand
{
    public string AuditEntityType => "Round";
    public string AuditEntityId => RoundId.ToString();
}

public sealed class SetParticipantTeeBoxCommandHandler : IRequestHandler<SetParticipantTeeBoxCommand, Result<ParticipantTeeDto>>
{
    private readonly IRoundRepository _roundRepository;
    private readonly ICourseRepository _courseRepository;
    private readonly ParticipantTeeResolver _teeResolver;
    private readonly ParticipantTeeUpdater _teeUpdater;

    public SetParticipantTeeBoxCommandHandler(
        IRoundRepository roundRepository,
        ICourseRepository courseRepository,
        ParticipantTeeResolver teeResolver,
        ParticipantTeeUpdater teeUpdater)
    {
        _roundRepository = roundRepository;
        _courseRepository = courseRepository;
        _teeResolver = teeResolver;
        _teeUpdater = teeUpdater;
    }

    public async Task<Result<ParticipantTeeDto>> Handle(SetParticipantTeeBoxCommand request, CancellationToken cancellationToken)
    {
        if (!await _teeResolver.IsSelectionEnabledAsync(cancellationToken))
            return Result<ParticipantTeeDto>.Fail("Tee box selection is not enabled.");

        var round = await _roundRepository.GetByIdAsync(request.RoundId, cancellationToken);
        if (round is null)
            return Result<ParticipantTeeDto>.Fail($"Round {request.RoundId} not found.");

        if (round.Status is RoundStatus.Finalized or RoundStatus.Cancelled)
            return Result<ParticipantTeeDto>.Fail($"Cannot change tees for a round with status '{round.Status}'.");

        var participant = round.Participants.FirstOrDefault(p => p.PlayerId == request.PlayerId);
        if (participant is null)
            return Result<ParticipantTeeDto>.Fail($"Player {request.PlayerId} is not a participant in round {request.RoundId}.");

        var course = await _courseRepository.GetByIdAsync(round.CourseId, cancellationToken);
        if (course is null)
            return Result<ParticipantTeeDto>.Fail("Course not found.");

        var tee = request.TeeBoxId is int teeBoxId
            ? course.TeeBoxes.FirstOrDefault(t => t.Id == teeBoxId)
            : await _teeResolver.ResolveDefaultAsync(round, request.PlayerId, cancellationToken);
        if (tee is null)
            return Result<ParticipantTeeDto>.Fail("That tee box isn't on this round's course.");

        var holeScores = await _roundRepository.GetHoleScoresAsync(participant.Id, cancellationToken);
        await _teeUpdater.ApplyAsync(
            participant, tee, overridden: request.TeeBoxId.HasValue, round, holeScores, cancellationToken);

        return Result<ParticipantTeeDto>.Ok(new ParticipantTeeDto(
            participant.PlayerId, tee.Id, tee.Name, participant.CourseRating, participant.SlopeRating,
            participant.CourseHandicap, participant.TeeBoxOverridden));
    }
}

// ── Per-half default (admin player page) ──────────────────────────────────────

/// <summary>
/// Sets (or clears, when <see cref="TeeBoxId"/> is null) a player's default
/// tee for one course in one half, then re-applies it to that player's
/// not-yet-started rounds there: rounds that aren't finalized or cancelled,
/// where the player has no hole scores and no score-entry override.
/// </summary>
public sealed record SetPlayerHalfTeeBoxCommand(
    int PlayerId,
    int HalfId,
    int CourseId,
    int? TeeBoxId,
    string UserId) : IRequest<Result<PlayerTeeBoxSettingsDto>>, IAmAuditableCommand
{
    public string AuditEntityType => "Player";
    public string AuditEntityId => PlayerId.ToString();
}

public sealed class SetPlayerHalfTeeBoxCommandHandler : IRequestHandler<SetPlayerHalfTeeBoxCommand, Result<PlayerTeeBoxSettingsDto>>
{
    private readonly IPlayerRepository _playerRepository;
    private readonly IFlightRepository _flightRepository;
    private readonly ICourseRepository _courseRepository;
    private readonly IRoundRepository _roundRepository;
    private readonly IPlayerHalfTeeBoxRepository _halfTeeBoxes;
    private readonly ParticipantTeeResolver _teeResolver;
    private readonly ParticipantTeeUpdater _teeUpdater;

    public SetPlayerHalfTeeBoxCommandHandler(
        IPlayerRepository playerRepository,
        IFlightRepository flightRepository,
        ICourseRepository courseRepository,
        IRoundRepository roundRepository,
        IPlayerHalfTeeBoxRepository halfTeeBoxes,
        ParticipantTeeResolver teeResolver,
        ParticipantTeeUpdater teeUpdater)
    {
        _playerRepository = playerRepository;
        _flightRepository = flightRepository;
        _courseRepository = courseRepository;
        _roundRepository = roundRepository;
        _halfTeeBoxes = halfTeeBoxes;
        _teeResolver = teeResolver;
        _teeUpdater = teeUpdater;
    }

    public async Task<Result<PlayerTeeBoxSettingsDto>> Handle(SetPlayerHalfTeeBoxCommand request, CancellationToken cancellationToken)
    {
        if (!await _teeResolver.IsSelectionEnabledAsync(cancellationToken))
            return Result<PlayerTeeBoxSettingsDto>.Fail("Tee box selection is not enabled.");

        var player = await _playerRepository.GetByIdAsync(request.PlayerId, cancellationToken);
        if (player is null)
            return Result<PlayerTeeBoxSettingsDto>.Fail($"Player with ID {request.PlayerId} not found.");

        var half = await _flightRepository.GetHalfByIdAsync(request.HalfId, cancellationToken);
        if (half is null)
            return Result<PlayerTeeBoxSettingsDto>.Fail($"Half with ID {request.HalfId} not found.");

        var course = await _courseRepository.GetByIdAsync(request.CourseId, cancellationToken);
        if (course is null)
            return Result<PlayerTeeBoxSettingsDto>.Fail($"Course with ID {request.CourseId} not found.");

        var tee = request.TeeBoxId is int teeBoxId
            ? course.TeeBoxes.FirstOrDefault(t => t.Id == teeBoxId)
            : ParticipantTeeResolver.DefaultTee(course);
        if (tee is null)
            return Result<PlayerTeeBoxSettingsDto>.Fail("That tee box isn't on this course.");

        await _halfTeeBoxes.SetAsync(request.PlayerId, request.HalfId, request.CourseId, request.TeeBoxId, cancellationToken);

        var participants = await _roundRepository.GetOpenParticipantsForPlayerAsync(
            request.PlayerId, half, request.CourseId, cancellationToken);
        foreach (var participant in participants)
        {
            if (participant.TeeBoxOverridden || participant.HoleScores.Count > 0 || participant.TeeBoxId == tee.Id)
                continue;
            await _teeUpdater.ApplyAsync(participant, tee, overridden: false, participant.Round, [], cancellationToken);
        }

        return Result<PlayerTeeBoxSettingsDto>.Ok(await GetPlayerTeeBoxSettingsQueryHandler.BuildAsync(
            _courseRepository, _halfTeeBoxes, request.PlayerId, cancellationToken));
    }
}

// ── Query ─────────────────────────────────────────────────────────────────────

public sealed record GetPlayerTeeBoxSettingsQuery(int PlayerId) : IRequest<Result<PlayerTeeBoxSettingsDto>>;

public sealed class GetPlayerTeeBoxSettingsQueryHandler : IRequestHandler<GetPlayerTeeBoxSettingsQuery, Result<PlayerTeeBoxSettingsDto>>
{
    private readonly ICourseRepository _courseRepository;
    private readonly IPlayerHalfTeeBoxRepository _halfTeeBoxes;

    public GetPlayerTeeBoxSettingsQueryHandler(ICourseRepository courseRepository, IPlayerHalfTeeBoxRepository halfTeeBoxes)
    {
        _courseRepository = courseRepository;
        _halfTeeBoxes = halfTeeBoxes;
    }

    public async Task<Result<PlayerTeeBoxSettingsDto>> Handle(GetPlayerTeeBoxSettingsQuery request, CancellationToken cancellationToken)
        => Result<PlayerTeeBoxSettingsDto>.Ok(await BuildAsync(_courseRepository, _halfTeeBoxes, request.PlayerId, cancellationToken));

    public static async Task<PlayerTeeBoxSettingsDto> BuildAsync(
        ICourseRepository courseRepository, IPlayerHalfTeeBoxRepository halfTeeBoxes, int playerId, CancellationToken cancellationToken)
    {
        // GetAllAsync doesn't load tee boxes; the by-ID lookups are cached and
        // a league only has a handful of courses.
        var courses = new List<CourseTeeOptionsDto>();
        foreach (var summary in await courseRepository.GetAllAsync(cancellationToken))
        {
            var course = await courseRepository.GetByIdAsync(summary.Id, cancellationToken);
            if (course is null || course.TeeBoxes.Count == 0) continue;
            courses.Add(new CourseTeeOptionsDto(course.Id, course.Name, TeeBoxOptionMapper.Map(course.TeeBoxes)));
        }

        var defaults = (await halfTeeBoxes.GetForPlayerAsync(playerId, cancellationToken))
            .Select(d => new PlayerHalfTeeBoxDto(d.HalfId, d.CourseId, d.TeeBoxId))
            .ToList();

        return new PlayerTeeBoxSettingsDto(courses, defaults);
    }
}
