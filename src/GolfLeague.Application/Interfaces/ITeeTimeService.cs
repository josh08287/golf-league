using GolfLeague.Application.Common;
using GolfLeague.Application.DTOs;

namespace GolfLeague.Application.Interfaces;

public interface ITeeTimeService
{
    /// <summary>
    /// Return the tee-time schedule for the round, generating empty slots
    /// up-front based on the participant count. Highlights the calling
    /// user's own assignment when <paramref name="callingPlayerId"/> is set.
    /// </summary>
    Task<Result<RoundTeeTimeScheduleDto>> GetScheduleAsync(int roundId, int? callingPlayerId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Caller joins the supplied tee-time slot. If they were already in
    /// another slot in the same round, they're moved (single atomic step).
    /// Blocked once the sign-up window closes, EXCEPT: an already-assigned
    /// participant may still move to a different open slot on the day of
    /// the round itself. Returns the refreshed schedule.
    /// </summary>
    Task<Result<RoundTeeTimeScheduleDto>> JoinAsync(int roundId, int teeTimeId, int callingPlayerId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Caller leaves whatever slot they're in for this round. No-op if not
    /// assigned. Returns the refreshed schedule.
    /// </summary>
    Task<Result<RoundTeeTimeScheduleDto>> LeaveAsync(int roundId, int callingPlayerId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Caller swaps tee-time slots with another participant in the same
    /// round. Both participants must be assigned to different slots. Unlike
    /// <see cref="JoinAsync"/>, this is always capacity-safe (each side takes
    /// the other's seat) and is not gated by the sign-up window. Returns the
    /// refreshed schedule.
    /// </summary>
    Task<Result<RoundTeeTimeScheduleDto>> SwapAsync(int roundId, int callingPlayerId, int otherParticipantId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Resolve the "next" round id — earliest scheduled round whose date is
    /// today or in the future. Returns null if nothing scheduled.
    /// </summary>
    Task<int?> ResolveNextRoundIdAsync(DateOnly today, CancellationToken cancellationToken = default);

    /// <summary>
    /// Caller adds a substitute to their own tee-time slot for this round.
    /// Only allowed while the normal sign-up window is open, and only up to
    /// as many substitutes as players have skipped the round (round-wide
    /// cap). The substitute is seated in the caller's current slot. Returns
    /// the refreshed schedule.
    /// </summary>
    Task<Result<RoundTeeTimeScheduleDto>> AddSubstituteAsync(int roundId, int callingPlayerId, int substitutePlayerId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes a substitute participant the caller (or an admin) added to
    /// this round, deleting the RoundParticipant row outright. Returns the
    /// refreshed schedule.
    /// </summary>
    Task<Result<RoundTeeTimeScheduleDto>> RemoveSubstituteAsync(int roundId, int callingPlayerId, int substituteParticipantId, CancellationToken cancellationToken = default);
}
