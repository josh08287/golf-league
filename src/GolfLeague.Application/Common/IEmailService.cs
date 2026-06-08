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
}

public sealed record TeeTimeEmailSlot(string SlotTime, IReadOnlyList<string> PlayerNames);
