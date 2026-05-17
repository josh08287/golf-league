using GolfLeague.Application.Common;
using GolfLeague.Application.DTOs;
using GolfLeague.Application.Interfaces;
using GolfLeague.Domain.Entities;
using GolfLeague.Domain.Enums;
using GolfLeague.Domain.Interfaces;
using GolfLeague.Domain.Services;
using Microsoft.Extensions.Logging;

namespace GolfLeague.Application.Rounds;

public sealed class TeeTimeService : ITeeTimeService
{
    private readonly IRoundRepository _rounds;
    private readonly ITeeTimeRepository _teeTimes;
    private readonly ILogger<TeeTimeService> _logger;

    public TeeTimeService(
        IRoundRepository rounds,
        ITeeTimeRepository teeTimes,
        ILogger<TeeTimeService> logger)
    {
        _rounds = rounds;
        _teeTimes = teeTimes;
        _logger = logger;
    }

    public async Task<int?> ResolveNextRoundIdAsync(DateOnly today, CancellationToken cancellationToken = default)
    {
        var rounds = await _rounds.GetAllAsync(cancellationToken);
        return rounds
            .Where(r => r.Status == RoundStatus.Scheduled && r.RoundDate >= today)
            .OrderBy(r => r.RoundDate)
            .Select(r => (int?)r.Id)
            .FirstOrDefault();
    }

    public async Task<Result<RoundTeeTimeScheduleDto>> GetScheduleAsync(int roundId, int? callingPlayerId, CancellationToken cancellationToken = default)
    {
        var round = await _rounds.GetByIdAsync(roundId, cancellationToken);
        if (round is null) return Result<RoundTeeTimeScheduleDto>.Fail($"Round {roundId} not found.");

        var participantCount = round.Participants.Count(p => !p.IsWithdrawn && !p.SkippedWeek);
        var slotsNeeded = TeeTimeSchedule.SlotsNeeded(participantCount);

        // Generate empty slots so the UI always has something to render.
        // Idempotent — skips already-existing slots.
        if (slotsNeeded > 0)
        {
            await _teeTimes.EnsureSlotsAsync(roundId, slotsNeeded, cancellationToken);
        }

        var slots = await _teeTimes.GetByRoundAsync(roundId, cancellationToken);

        var dto = BuildDto(round, slots, callingPlayerId);
        return Result<RoundTeeTimeScheduleDto>.Ok(dto);
    }

    public async Task<Result<RoundTeeTimeScheduleDto>> JoinAsync(int roundId, int teeTimeId, int callingPlayerId, CancellationToken cancellationToken = default)
    {
        var round = await _rounds.GetByIdAsync(roundId, cancellationToken);
        if (round is null) return Result<RoundTeeTimeScheduleDto>.Fail($"Round {roundId} not found.");

        if (TeeTimeSchedule.IsAfterCutoff(round.RoundDate, DateTime.UtcNow))
            return Result<RoundTeeTimeScheduleDto>.Fail("Tee-time sign-ups are locked for this round.");

        var participant = round.Participants
            .FirstOrDefault(p => p.PlayerId == callingPlayerId && !p.IsWithdrawn && !p.SkippedWeek);
        if (participant is null)
            return Result<RoundTeeTimeScheduleDto>.Fail("You're not a participant in this round.");

        var slot = await _teeTimes.GetByIdAsync(teeTimeId, cancellationToken);
        if (slot is null || slot.RoundId != roundId)
            return Result<RoundTeeTimeScheduleDto>.Fail("That tee time doesn't belong to this round.");

        // Capacity check: exclude the caller's current row if they're moving
        // within the same slot (no-op) or to a different one (still leaves a
        // seat free).
        var occupants = slot.Participants.Count(p => p.Id != participant.Id);
        if (occupants >= TeeTimeSchedule.CapacityPerTeeTime)
            return Result<RoundTeeTimeScheduleDto>.Fail("That tee time is full.");

        if (participant.TeeTimeId != teeTimeId)
        {
            await _teeTimes.SetParticipantTeeTimeAsync(participant.Id, teeTimeId, cancellationToken);
        }

        return await GetScheduleAsync(roundId, callingPlayerId, cancellationToken);
    }

    public async Task<Result<RoundTeeTimeScheduleDto>> LeaveAsync(int roundId, int callingPlayerId, CancellationToken cancellationToken = default)
    {
        var round = await _rounds.GetByIdAsync(roundId, cancellationToken);
        if (round is null) return Result<RoundTeeTimeScheduleDto>.Fail($"Round {roundId} not found.");

        if (TeeTimeSchedule.IsAfterCutoff(round.RoundDate, DateTime.UtcNow))
            return Result<RoundTeeTimeScheduleDto>.Fail("Tee-time sign-ups are locked for this round.");

        var participant = round.Participants.FirstOrDefault(p => p.PlayerId == callingPlayerId);
        if (participant is null)
            return Result<RoundTeeTimeScheduleDto>.Fail("You're not a participant in this round.");

        if (participant.TeeTimeId is not null)
        {
            await _teeTimes.SetParticipantTeeTimeAsync(participant.Id, null, cancellationToken);
        }

        return await GetScheduleAsync(roundId, callingPlayerId, cancellationToken);
    }

    private static RoundTeeTimeScheduleDto BuildDto(Round round, IReadOnlyList<RoundTeeTime> slots, int? callingPlayerId)
    {
        var participantCount = round.Participants.Count(p => !p.IsWithdrawn && !p.SkippedWeek);
        var cutoffUtc = TeeTimeSchedule.ComputeSundayNoonCutoffUtc(round.RoundDate);
        var isLocked = DateTime.UtcNow >= cutoffUtc;

        var callerParticipant = callingPlayerId is null
            ? null
            : round.Participants.FirstOrDefault(p => p.PlayerId == callingPlayerId.Value);

        var slotDtos = slots.Select(s => new TeeTimeSlotDto(
            s.Id,
            s.TeeTimeNumber,
            s.ScheduledTime.ToString("HH:mm"),
            s.AutoFilledAt is not null,
            s.Participants
                .OrderBy(p => p.Player.LastName)
                .ThenBy(p => p.Player.FirstName)
                .Select(p => new TeeTimeParticipantDto(
                    p.Id,
                    p.PlayerId,
                    p.Player.FullName,
                    p.FlightId,
                    p.Flight?.Name ?? string.Empty))
                .ToList()))
            .ToList();

        return new RoundTeeTimeScheduleDto(
            round.Id,
            cutoffUtc,
            isLocked,
            participantCount,
            callerParticipant?.Id,
            callerParticipant?.TeeTimeId,
            slotDtos,
            callerParticipant?.Player.PreferredTeeTimeSlots ?? Domain.Enums.TeeTimeSlotPreference.None);
    }
}
