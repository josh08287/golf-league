using GolfLeague.Application.Common;
using GolfLeague.Application.DTOs;
using GolfLeague.Application.Interfaces;
using GolfLeague.Application.Leagues;
using GolfLeague.Domain.Entities;
using GolfLeague.Domain.Enums;
using GolfLeague.Domain.Interfaces;
using GolfLeague.Domain.Services;
using Microsoft.Extensions.Logging;
using static GolfLeague.Application.Common.FlightDisplayName;

namespace GolfLeague.Application.Rounds;

public sealed class TeeTimeService : ITeeTimeService
{
    private readonly IRoundRepository _rounds;
    private readonly ITeeTimeRepository _teeTimes;
    private readonly ILeagueSettingRepository _leagueSettings;
    private readonly IPlayerRepository _players;
    private readonly IHandicapRepository _handicaps;
    private readonly AuditWriter _auditWriter;
    private readonly ILogger<TeeTimeService> _logger;

    public TeeTimeService(
        IRoundRepository rounds,
        ITeeTimeRepository teeTimes,
        ILeagueSettingRepository leagueSettings,
        IPlayerRepository players,
        IHandicapRepository handicaps,
        AuditWriter auditWriter,
        ILogger<TeeTimeService> logger)
    {
        _rounds = rounds;
        _teeTimes = teeTimes;
        _leagueSettings = leagueSettings;
        _players = players;
        _handicaps = handicaps;
        _auditWriter = auditWriter;
        _logger = logger;
    }

    public async Task<int?> ResolveNextRoundIdAsync(DateOnly today, CancellationToken cancellationToken = default)
    {
        var rounds = await _rounds.GetAllAsync(cancellationToken);
        var now = DateTime.UtcNow;

        bool NotYetPlayed(Domain.Entities.Round r) => now < TeeTimeSchedule.LastTeeTimeUtc(
            r.RoundDate, r.Participants.Count(p => !p.IsWithdrawn && !p.SkippedWeek));

        // Prefer a round that is currently InProgress — but only if its last tee
        // time hasn't passed. A stale InProgress round from a finished half (e.g.
        // never finalized) must NOT pin the page to the old half; we fall through
        // to the date-based pick in that case.
        var inProgress = rounds
            .Where(r => r.Status == RoundStatus.InProgress && NotYetPlayed(r))
            .OrderBy(r => r.RoundDate)
            .ThenBy(r => r.Id)
            .FirstOrDefault();

        if (inProgress is not null)
            return inProgress.Id;

        // Otherwise the "next" round is simply the earliest Scheduled round whose
        // last tee time has NOT yet passed — across ALL seasons/halves, purely by
        // date. This advances to the next round (next week, next half, or next
        // season) the moment the current round's final tee time is in the past.
        return rounds
            .Where(r => r.Status == RoundStatus.Scheduled && NotYetPlayed(r))
            .OrderBy(r => r.RoundDate)
            .ThenBy(r => r.Id)
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

        var (isOpen, _, closesUtc) = await GetSignupWindowDetailAsync(round, DateTime.UtcNow, cancellationToken);
        var isRoundDay = TeeTimeSchedule.IsRoundDay(round.RoundDate, DateTime.UtcNow);

        var substitutesEnabledSetting = await _leagueSettings.GetAsync(round.LeagueId, KnownSettings.SubstitutesEnabled, cancellationToken);
        var substitutesEnabled = substitutesEnabledSetting is not null
            && bool.TryParse(substitutesEnabledSetting.Value, out var parsedEnabled)
            && parsedEnabled;

        // A pool sub who isn't in this round yet can self-join a slot — only
        // worth the extra lookup when substitutes are on and the caller has
        // no participant row.
        var callerIsPoolSub = false;
        if (substitutesEnabled
            && callingPlayerId is int pid
            && !round.Participants.Any(p => p.PlayerId == pid))
        {
            var callerPlayer = await _players.GetByIdAsync(pid, cancellationToken);
            callerIsPoolSub = callerPlayer?.IsSubstitute == true;
        }

        var dto = BuildDto(round, slots, callingPlayerId, isLocked: !isOpen, closesUtc: closesUtc, isRoundDay: isRoundDay, substitutesEnabled: substitutesEnabled, callerIsPoolSub: callerIsPoolSub);
        return Result<RoundTeeTimeScheduleDto>.Ok(dto);
    }

    /// <summary>
    /// Whether tee-time sign-ups for <paramref name="round"/> are open at
    /// <paramref name="utcNow"/>. Sign-ups OPEN as soon as the previous week's
    /// round (same half, prior week) has finished — i.e. its last tee time has
    /// passed — and CLOSE at the Sunday-noon-ET cutoff before the round. The
    /// first round of a half has no predecessor, so it opens immediately.
    /// </summary>
    private async Task<(bool IsOpen, string? Reason)> GetSignupWindowAsync(
        Round round, DateTime utcNow, CancellationToken cancellationToken)
    {
        var (isOpen, reason, _) = await GetSignupWindowDetailAsync(round, utcNow, cancellationToken);
        return (isOpen, reason);
    }

    private async Task<(bool IsOpen, string? Reason, DateTime ClosesUtc)> GetSignupWindowDetailAsync(
        Round round, DateTime utcNow, CancellationToken cancellationToken)
    {
        // Sign-ups close at the league's configured cutoff time (ET, default
        // 6pm) the day before the round, when auto-fill takes over assigning
        // the remaining players.
        var cutoffSetting = await _leagueSettings.GetAsync(round.LeagueId, KnownSettings.TeeTimeCutoffTime, cancellationToken);
        var cutoffTime = KnownSettings.ParseCutoffTime(cutoffSetting?.Value);
        var closesUtc = TeeTimeSchedule.ComputeCutoffUtc(round.RoundDate, cutoffTime);

        if (utcNow >= closesUtc)
            return (false, "Tee-time sign-ups are closed for this round.", closesUtc);

        // Opens once the previous week's round (same half) has wrapped up.
        // Rounds with no half (e.g. tournament rounds) have no predecessor to
        // gate on, so they open immediately.
        if (round.HalfId is int halfId)
        {
            var previous = await _rounds.GetPreviousRoundAsync(halfId, round.WeekNumber, cancellationToken);
            if (previous is not null)
            {
                var prevCount = previous.Participants.Count(p => !p.IsWithdrawn && !p.SkippedWeek);
                var opensUtc = TeeTimeSchedule.LastTeeTimeUtc(previous.RoundDate, prevCount);
                if (utcNow < opensUtc)
                    return (false, "Tee-time sign-ups for this round haven't opened yet.", closesUtc);
            }
        }

        return (true, null, closesUtc);
    }

    public async Task<Result<RoundTeeTimeScheduleDto>> JoinAsync(int roundId, int teeTimeId, int callingPlayerId, CancellationToken cancellationToken = default)
    {
        var round = await _rounds.GetByIdAsync(roundId, cancellationToken);
        if (round is null) return Result<RoundTeeTimeScheduleDto>.Fail($"Round {roundId} not found.");

        var (isOpen, lockReason) = await GetSignupWindowAsync(round, DateTime.UtcNow, cancellationToken);
        if (!isOpen)
        {
            // Narrow exception: an already-assigned participant may still move
            // to a different slot on the day of the round, even after the
            // general sign-up window has closed. This does not reopen fresh
            // claims (participant must already have a TeeTimeId) and does not
            // apply to LeaveAsync.
            var existingParticipant = round.Participants
                .FirstOrDefault(p => p.PlayerId == callingPlayerId && !p.IsWithdrawn && !p.SkippedWeek);
            var isRoundDayMove = existingParticipant?.TeeTimeId is not null
                && TeeTimeSchedule.IsRoundDay(round.RoundDate, DateTime.UtcNow);
            if (!isRoundDayMove)
                return Result<RoundTeeTimeScheduleDto>.Fail(lockReason!);
        }

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
            await TryWriteAuditAsync(round, participant, "TeeTimeSelected", cancellationToken);
        }

        return await GetScheduleAsync(roundId, callingPlayerId, cancellationToken);
    }

    public async Task<Result<RoundTeeTimeScheduleDto>> LeaveAsync(int roundId, int callingPlayerId, CancellationToken cancellationToken = default)
    {
        var round = await _rounds.GetByIdAsync(roundId, cancellationToken);
        if (round is null) return Result<RoundTeeTimeScheduleDto>.Fail($"Round {roundId} not found.");

        var (isOpen, lockReason) = await GetSignupWindowAsync(round, DateTime.UtcNow, cancellationToken);
        if (!isOpen)
            return Result<RoundTeeTimeScheduleDto>.Fail(lockReason!);

        var participant = round.Participants.FirstOrDefault(p => p.PlayerId == callingPlayerId);
        if (participant is null)
            return Result<RoundTeeTimeScheduleDto>.Fail("You're not a participant in this round.");

        // A substitute leaving their slot leaves the round entirely — a sub
        // row without a tee time has no meaning (they're only in the round
        // to fill a seat).
        if (participant.IsSubstitute)
        {
            await _rounds.DeleteParticipantAsync(participant.Id, cancellationToken);
            await TryWriteAuditAsync(round, participant, "SubstituteRemoved", cancellationToken);
            return await GetScheduleAsync(roundId, callingPlayerId, cancellationToken);
        }

        if (participant.TeeTimeId is not null)
        {
            await _teeTimes.SetParticipantTeeTimeAsync(participant.Id, null, cancellationToken);
            await TryWriteAuditAsync(round, participant, "TeeTimeLeft", cancellationToken);
        }

        return await GetScheduleAsync(roundId, callingPlayerId, cancellationToken);
    }

    public async Task<Result<RoundTeeTimeScheduleDto>> SwapAsync(int roundId, int callingPlayerId, int otherParticipantId, CancellationToken cancellationToken = default)
    {
        var round = await _rounds.GetByIdAsync(roundId, cancellationToken);
        if (round is null) return Result<RoundTeeTimeScheduleDto>.Fail($"Round {roundId} not found.");

        var participant = round.Participants
            .FirstOrDefault(p => p.PlayerId == callingPlayerId && !p.IsWithdrawn && !p.SkippedWeek);
        if (participant is null)
            return Result<RoundTeeTimeScheduleDto>.Fail("You're not a participant in this round.");

        var otherParticipant = round.Participants
            .FirstOrDefault(p => p.Id == otherParticipantId && !p.IsWithdrawn && !p.SkippedWeek);
        if (otherParticipant is null)
            return Result<RoundTeeTimeScheduleDto>.Fail("That player isn't a participant in this round.");

        if (participant.Id == otherParticipant.Id)
            return Result<RoundTeeTimeScheduleDto>.Fail("You can't switch with yourself.");

        if (participant.TeeTimeId is null || participant.TeeTimeId == otherParticipant.TeeTimeId)
            return Result<RoundTeeTimeScheduleDto>.Fail("That player is already in your group.");

        await _teeTimes.SwapParticipantTeeTimesAsync(participant.Id, otherParticipant.Id, cancellationToken);
        await TryWriteAuditAsync(round, participant, "TeeTimeSwapped", cancellationToken);

        return await GetScheduleAsync(roundId, callingPlayerId, cancellationToken);
    }

    public async Task<Result<RoundTeeTimeScheduleDto>> AddSubstituteAsync(int roundId, int callingPlayerId, int substitutePlayerId, CancellationToken cancellationToken = default)
    {
        var round = await _rounds.GetByIdAsync(roundId, cancellationToken);
        if (round is null) return Result<RoundTeeTimeScheduleDto>.Fail($"Round {roundId} not found.");

        var substitutesEnabledSetting = await _leagueSettings.GetAsync(round.LeagueId, KnownSettings.SubstitutesEnabled, cancellationToken);
        if (substitutesEnabledSetting is null || !bool.TryParse(substitutesEnabledSetting.Value, out var substitutesEnabled) || !substitutesEnabled)
            return Result<RoundTeeTimeScheduleDto>.Fail("Substitutes aren't enabled for this league.");

        var (isOpen, lockReason) = await GetSignupWindowAsync(round, DateTime.UtcNow, cancellationToken);
        if (!isOpen)
            return Result<RoundTeeTimeScheduleDto>.Fail(lockReason!);

        var participant = round.Participants
            .FirstOrDefault(p => p.PlayerId == callingPlayerId && !p.IsWithdrawn && !p.SkippedWeek);
        if (participant is null)
            return Result<RoundTeeTimeScheduleDto>.Fail("You're not a participant in this round.");

        if (participant.TeeTimeId is null)
            return Result<RoundTeeTimeScheduleDto>.Fail("Pick your own tee time before adding a substitute.");

        var substitutePlayer = await _players.GetByIdAsync(substitutePlayerId, cancellationToken);
        if (substitutePlayer is null || !substitutePlayer.IsSubstitute)
            return Result<RoundTeeTimeScheduleDto>.Fail("That player isn't in the substitute pool.");

        if (round.Participants.Any(p => p.PlayerId == substitutePlayerId))
            return Result<RoundTeeTimeScheduleDto>.Fail("That substitute is already in this round.");

        // Round-wide cap: no more substitutes than players who've skipped.
        var skippedCount = round.Participants.Count(p => p.SkippedWeek);
        var substituteCount = round.Participants.Count(p => p.IsSubstitute);
        if (substituteCount >= skippedCount)
            return Result<RoundTeeTimeScheduleDto>.Fail(
                "No more substitute spots available for this round — every skipped player's spot is filled.");

        var slot = await _teeTimes.GetByIdAsync(participant.TeeTimeId.Value, cancellationToken);
        if (slot is null)
            return Result<RoundTeeTimeScheduleDto>.Fail("Your tee time slot could not be found.");
        if (slot.Participants.Count >= TeeTimeSchedule.CapacityPerTeeTime)
            return Result<RoundTeeTimeScheduleDto>.Fail("Your tee time is full.");

        await CreateSubstituteParticipantAsync(
            round, substitutePlayerId, participant.TeeTimeId.Value, participant.FlightId, cancellationToken);
        await TryWriteAuditAsync(round, participant, "SubstituteAdded", cancellationToken);

        return await GetScheduleAsync(roundId, callingPlayerId, cancellationToken);
    }

    public async Task<Result<RoundTeeTimeScheduleDto>> JoinAsSubstituteAsync(int roundId, int teeTimeId, int callingPlayerId, CancellationToken cancellationToken = default)
    {
        var round = await _rounds.GetByIdAsync(roundId, cancellationToken);
        if (round is null) return Result<RoundTeeTimeScheduleDto>.Fail($"Round {roundId} not found.");

        var substitutesEnabledSetting = await _leagueSettings.GetAsync(round.LeagueId, KnownSettings.SubstitutesEnabled, cancellationToken);
        if (substitutesEnabledSetting is null || !bool.TryParse(substitutesEnabledSetting.Value, out var substitutesEnabled) || !substitutesEnabled)
            return Result<RoundTeeTimeScheduleDto>.Fail("Substitutes aren't enabled for this league.");

        var (isOpen, lockReason) = await GetSignupWindowAsync(round, DateTime.UtcNow, cancellationToken);
        if (!isOpen)
            return Result<RoundTeeTimeScheduleDto>.Fail(lockReason!);

        var caller = await _players.GetByIdAsync(callingPlayerId, cancellationToken);
        if (caller is null || !caller.IsSubstitute)
            return Result<RoundTeeTimeScheduleDto>.Fail("You're not in the substitute pool.");

        if (round.Participants.Any(p => p.PlayerId == callingPlayerId))
            return Result<RoundTeeTimeScheduleDto>.Fail("You're already in this round.");

        // Round-wide cap: no more substitutes than players who've skipped.
        var skippedCount = round.Participants.Count(p => p.SkippedWeek);
        var substituteCount = round.Participants.Count(p => p.IsSubstitute);
        if (substituteCount >= skippedCount)
            return Result<RoundTeeTimeScheduleDto>.Fail(
                "No substitute spots available for this round — a spot opens when a player skips.");

        var slot = await _teeTimes.GetByIdAsync(teeTimeId, cancellationToken);
        if (slot is null || slot.RoundId != roundId)
            return Result<RoundTeeTimeScheduleDto>.Fail("That tee time doesn't belong to this round.");
        if (slot.Participants.Count >= TeeTimeSchedule.CapacityPerTeeTime)
            return Result<RoundTeeTimeScheduleDto>.Fail("That tee time is full.");

        await CreateSubstituteParticipantAsync(
            round, callingPlayerId, teeTimeId, flightId: null, cancellationToken);

        // TryWriteAuditAsync needs the participant's Player nav, which the
        // freshly created row doesn't carry — use the caller we already loaded.
        if (caller.AppUserId is Guid appUserId)
        {
            await _auditWriter.WriteAsync(
                "SubstituteJoined", "Round", round.Id.ToString(), appUserId.ToString(),
                leagueId: round.LeagueId, cancellationToken: cancellationToken);
        }

        return await GetScheduleAsync(roundId, callingPlayerId, cancellationToken);
    }

    /// <summary>
    /// Creates and persists the IsSubstitute participant row shared by both
    /// entry paths (a participant inviting a sub into their slot, and a
    /// pool sub joining a slot themselves). When <paramref name="flightId"/>
    /// is null, the sub inherits the flight of the skipped participant whose
    /// spot they're bookkeeping-linked to.
    /// </summary>
    private async Task CreateSubstituteParticipantAsync(
        Round round, int substitutePlayerId, int teeTimeId, int? flightId, CancellationToken cancellationToken)
    {
        var current = await _handicaps.GetCurrentAsync(substitutePlayerId, cancellationToken);
        var index = current?.HandicapIndex ?? 0.0;
        var courseHandicap = StablefordScoringService.CourseHandicap(
            index, round.Course.SlopeRating, round.Course.CourseRating, round.Course.Holes.Sum(h => h.Par), round.RoundType);

        // Bookkeeping link to a skipped participant not yet claimed by another sub this round.
        var claimedSkipIds = round.Participants
            .Where(p => p.IsSubstitute)
            .Select(p => p.SubstituteForParticipantId)
            .ToHashSet();
        var unclaimedSkip = round.Participants
            .Where(p => p.SkippedWeek && !claimedSkipIds.Contains(p.Id))
            .OrderBy(p => p.Id)
            .FirstOrDefault();

        var newParticipant = new RoundParticipant
        {
            RoundId = round.Id,
            PlayerId = substitutePlayerId,
            FlightId = flightId ?? unclaimedSkip?.FlightId,
            TeeTimeId = teeTimeId,
            HandicapIndex = index,
            CourseHandicap = courseHandicap,
            IsWithdrawn = false,
            IsSubstitute = true,
            SubstituteForParticipantId = unclaimedSkip?.Id,
        };
        await _rounds.AddParticipantAsync(newParticipant, cancellationToken);
    }

    public async Task<Result<RoundTeeTimeScheduleDto>> RemoveSubstituteAsync(int roundId, int callingPlayerId, int substituteParticipantId, CancellationToken cancellationToken = default)
    {
        var round = await _rounds.GetByIdAsync(roundId, cancellationToken);
        if (round is null) return Result<RoundTeeTimeScheduleDto>.Fail($"Round {roundId} not found.");

        var substitute = round.Participants.FirstOrDefault(p => p.Id == substituteParticipantId && p.IsSubstitute);
        if (substitute is null)
            return Result<RoundTeeTimeScheduleDto>.Fail("That substitute isn't in this round.");

        // Only the player who brought the substitute in (same tee time) — or
        // an admin, who calls a separate endpoint — may remove them here.
        var caller = round.Participants
            .FirstOrDefault(p => p.PlayerId == callingPlayerId && !p.IsWithdrawn && !p.SkippedWeek);
        if (caller is null || caller.TeeTimeId != substitute.TeeTimeId)
            return Result<RoundTeeTimeScheduleDto>.Fail("You can only remove a substitute from your own tee time.");

        await _rounds.DeleteParticipantAsync(substituteParticipantId, cancellationToken);
        await TryWriteAuditAsync(round, caller, "SubstituteRemoved", cancellationToken);

        return await GetScheduleAsync(roundId, callingPlayerId, cancellationToken);
    }

    /// <summary>
    /// Best-effort audit write for tee-time self-service, which bypasses
    /// MediatR's AuditBehavior (this is a direct service call, not a command).
    /// </summary>
    private async Task TryWriteAuditAsync(
        Round round, RoundParticipant participant, string action, CancellationToken cancellationToken)
    {
        if (participant.Player?.AppUserId is not Guid appUserId)
            return;

        await _auditWriter.WriteAsync(
            action, "Round", round.Id.ToString(), appUserId.ToString(),
            leagueId: round.LeagueId, cancellationToken: cancellationToken);
    }

    private static RoundTeeTimeScheduleDto BuildDto(
        Round round,
        IReadOnlyList<RoundTeeTime> slots,
        int? callingPlayerId,
        bool isLocked,
        DateTime closesUtc,
        bool isRoundDay,
        bool substitutesEnabled = false,
        bool callerIsPoolSub = false)
    {
        var participantCount = round.Participants.Count(p => !p.IsWithdrawn && !p.SkippedWeek);
        // The DTO's "cutoff" is now the moment sign-ups close for this round
        // (its last tee time), replacing the old Sunday-noon cutoff.
        var cutoffUtc = closesUtc;

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
                    p.Flight is null ? string.Empty : Format(p.Flight),
                    p.IsSubstitute))
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
            round.WeekNumber,
            round.RoundDate.ToString("yyyy-MM-dd"),
            round.Course?.Name ?? string.Empty,
            callerParticipant?.Player.PreferredTeeTimeSlots ?? Domain.Enums.TeeTimeSlotPreference.None,
            callerParticipant?.SkippedWeek ?? false,
            isRoundDay,
            round.Participants.Count(p => p.SkippedWeek),
            round.Participants.Count(p => p.IsSubstitute),
            substitutesEnabled,
            callerIsPoolSub,
            callerParticipant?.IsSubstitute ?? false);
    }
}
