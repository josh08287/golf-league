namespace GolfLeague.Application.Common;

public sealed record TeeTimeEmailRecipient(string Email, string PlayerName, string? SlotTime);

public interface IEmailService
{
    Task SendInviteAsync(string toEmail, string inviteLink, DateTime expiresAt, CancellationToken cancellationToken = default);

    Task SendPasswordResetAsync(string toEmail, string resetLink, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends the weekly tee time schedule to a single player.
    /// <paramref name="roundDate"/> is a display string, e.g. "Thursday, June 12".
    /// <paramref name="allSlots"/> is the full list of slots so the recipient
    /// can see the whole group sheet, not just their own assignment.
    /// </summary>
    Task SendTeeTimeScheduleAsync(
        string toEmail,
        string playerName,
        string leagueName,
        string roundDate,
        string? playerSlotTime,
        IReadOnlyList<TeeTimeEmailSlot> allSlots,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a free-form broadcast message from an admin to a single recipient.
    /// </summary>
    Task SendBroadcastMessageAsync(
        string toEmail,
        string leagueName,
        string subject,
        string body,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends the sign-up reminder to a player who doesn't yet have a tee
    /// time for the upcoming round, a few hours before sign-ups close.
    /// <paramref name="roundDate"/> is a display string, e.g. "Thursday, June 12".
    /// <paramref name="cutoffDisplay"/> is a display string for the cutoff, e.g. "6:00 PM".
    /// </summary>
    Task SendSignUpReminderAsync(
        string toEmail,
        string playerName,
        string leagueName,
        string roundDate,
        string cutoffDisplay,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Notifies a substitute-pool player that open spots exist for an
    /// upcoming round. <paramref name="loginLink"/> points to the
    /// accept-invite flow if the player hasn't yet activated their account,
    /// or to the normal login page otherwise.
    /// </summary>
    Task SendSubSpotAvailableAsync(
        string toEmail,
        string playerName,
        string leagueName,
        string roundDate,
        int openSpots,
        string roundCostDisplay,
        string loginLink,
        CancellationToken cancellationToken = default);
}

public sealed record TeeTimeEmailSlot(string SlotTime, IReadOnlyList<string> PlayerNames);
