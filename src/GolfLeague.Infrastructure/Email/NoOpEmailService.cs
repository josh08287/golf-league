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
        // Logged at Warning so the link is visible in App Insights even when
        // email isn't configured — useful for bootstrapping prod for the very
        // first admin before ACS is fully wired.
        _logger.LogWarning(
            "Email not configured (ACS_CONNECTION_STRING/ACS_SENDER_ADDRESS missing). Skipping password-reset email to {Email}. Link: {Link}",
            toEmail, resetLink);
        return Task.CompletedTask;
    }
}
