using GolfLeague.Application.Common;
using Microsoft.Extensions.Logging;

namespace GolfLeague.Infrastructure.Email;

public sealed class NoOpEmailService : IEmailService
{
    private readonly ILogger<NoOpEmailService> _logger;

    public NoOpEmailService(ILogger<NoOpEmailService> logger) => _logger = logger;

    public Task SendInviteAsync(string toEmail, string inviteLink, DateTime expiresAt, CancellationToken cancellationToken = default)
    {
        _logger.LogWarning(
            "Email not configured (ACS_CONNECTION_STRING/ACS_SENDER_ADDRESS missing). Skipping invite email to {Email}. Link: {Link}",
            toEmail, inviteLink);
        return Task.CompletedTask;
    }

    public Task SendPasswordResetAsync(string toEmail, string resetLink, CancellationToken cancellationToken = default)
    {
        _logger.LogWarning(
            "Email not configured (ACS_CONNECTION_STRING/ACS_SENDER_ADDRESS missing). Skipping password-reset email to {Email}. Link: {Link}",
            toEmail, resetLink);
        return Task.CompletedTask;
    }

    public Task SendTeeTimeScheduleAsync(
        string toEmail,
        string playerName,
        string leagueName,
        string roundDate,
        string? playerSlotTime,
        IReadOnlyList<TeeTimeEmailSlot> allSlots,
        CancellationToken cancellationToken = default)
    {
        _logger.LogWarning(
            "Email not configured. Skipping tee-time schedule email to {Email} ({Player}) for {Date}.",
            toEmail, playerName, roundDate);
        return Task.CompletedTask;
    }
}
