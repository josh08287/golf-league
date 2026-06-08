using Azure.Communication.Email;
using GolfLeague.Application.Common;
using Microsoft.Extensions.Logging;

namespace GolfLeague.Infrastructure.Email;

public sealed class AzureCommunicationEmailService : IEmailService
{
    private readonly EmailClient _client;
    private readonly string _senderAddress;
    private readonly ILogger<AzureCommunicationEmailService> _logger;

    public AzureCommunicationEmailService(
        EmailClient client,
        string senderAddress,
        ILogger<AzureCommunicationEmailService> logger)
    {
        _client = client;
        _senderAddress = senderAddress;
        _logger = logger;
    }

    public async Task SendInviteAsync(string toEmail, string inviteLink, DateTime expiresAt, CancellationToken cancellationToken = default)
    {
        var expiry = expiresAt.ToString("MMMM d, yyyy");
        var subject = "You've been invited to join the Capital Golf League";
        var html = $"""
            <html>
            <body style="font-family: Arial, sans-serif; color: #333; max-width: 600px; margin: 0 auto; padding: 24px;">
              <h2 style="color: #1a5c38;">⛳ You're invited to join the Capital Golf League!</h2>
              <p>You've been invited to join the Capital Golf League. Click the button below to accept your invitation and complete your registration.</p>
              <p style="margin: 32px 0;">
                <a href="{inviteLink}"
                   style="background-color: #1a5c38; color: white; padding: 12px 24px; text-decoration: none; border-radius: 6px; font-weight: bold;">
                  Accept Invitation
                </a>
              </p>
              <p style="color: #666; font-size: 14px;">This invitation expires on <strong>{expiry}</strong>.</p>
              <p style="color: #666; font-size: 14px;">If you weren't expecting this email, you can safely ignore it.</p>
            </body>
            </html>
            """;

        await SendAsync(toEmail, subject, html, "invite", cancellationToken);
    }

    public async Task SendPasswordResetAsync(string toEmail, string resetLink, CancellationToken cancellationToken = default)
    {
        const string subject = "Reset your Capital Golf League password";
        var html = $"""
            <html>
            <body style="font-family: Arial, sans-serif; color: #333; max-width: 600px; margin: 0 auto; padding: 24px;">
              <h2 style="color: #1a5c38;">⛳ Password reset</h2>
              <p>We received a request to reset the password on your Capital Golf League account. Click the button below to set a new password.</p>
              <p style="margin: 32px 0;">
                <a href="{resetLink}"
                   style="background-color: #1a5c38; color: white; padding: 12px 24px; text-decoration: none; border-radius: 6px; font-weight: bold;">
                  Set a new password
                </a>
              </p>
              <p style="color: #666; font-size: 14px;">This link expires in 1 hour and can only be used once.</p>
              <p style="color: #666; font-size: 14px;">If you didn't request this, you can safely ignore this email — your password won't change.</p>
            </body>
            </html>
            """;

        await SendAsync(toEmail, subject, html, "password-reset", cancellationToken);
    }

    public async Task SendTeeTimeScheduleAsync(
        string toEmail,
        string playerName,
        string leagueName,
        string roundDate,
        string? playerSlotTime,
        IReadOnlyList<TeeTimeEmailSlot> allSlots,
        CancellationToken cancellationToken = default)
    {
        var subject = $"{leagueName} — Tee times for {roundDate}";

        var yourSlotHtml = playerSlotTime is not null
            ? $"<p>Your tee time is <strong>{playerSlotTime}</strong>.</p>"
            : "<p>You do not have a tee time assigned for this round.</p>";

        var slotsHtml = string.Join("\n", allSlots.Select(slot =>
        {
            var players = string.Join(", ", slot.PlayerNames);
            return $"<tr><td style=\"padding:6px 12px;font-weight:bold;white-space:nowrap\">{slot.SlotTime}</td><td style=\"padding:6px 12px\">{players}</td></tr>";
        }));

        var html = $"""
            <html>
            <body style="font-family: Arial, sans-serif; color: #333; max-width: 600px; margin: 0 auto; padding: 24px;">
              <h2 style="color: #1a5c38;">⛳ {leagueName} — Tee Times for {roundDate}</h2>
              <p>Hi {playerName},</p>
              {yourSlotHtml}
              <h3 style="color: #1a5c38; margin-top: 24px;">Full Tee Sheet</h3>
              <table style="border-collapse:collapse; width:100%">
                <thead>
                  <tr style="background:#1a5c38; color:white">
                    <th style="padding:8px 12px; text-align:left">Time</th>
                    <th style="padding:8px 12px; text-align:left">Players</th>
                  </tr>
                </thead>
                <tbody>
                  {slotsHtml}
                </tbody>
              </table>
              <p style="color:#666; font-size:14px; margin-top:24px;">See you on the course!</p>
            </body>
            </html>
            """;

        await SendAsync(toEmail, subject, html, "tee-time-schedule", cancellationToken);
    }

    private async Task SendAsync(string toEmail, string subject, string html, string kind, CancellationToken cancellationToken)
    {
        var message = new EmailMessage(
            senderAddress: _senderAddress,
            recipients: new EmailRecipients([new EmailAddress(toEmail)]),
            content: new EmailContent(subject) { Html = html });

        try
        {
            var operation = await _client.SendAsync(Azure.WaitUntil.Started, message, cancellationToken);
            _logger.LogInformation("{Kind} email queued for {Email}, operation id: {OperationId}", kind, toEmail, operation.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send {Kind} email to {Email}", kind, toEmail);
            throw;
        }
    }
}
