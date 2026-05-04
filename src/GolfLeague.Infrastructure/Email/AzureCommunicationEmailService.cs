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
        var subject = "You've been invited to join the Golf League";
        var html = $"""
            <html>
            <body style="font-family: Arial, sans-serif; color: #333; max-width: 600px; margin: 0 auto; padding: 24px;">
              <h2 style="color: #1a5c38;">⛳ You're invited to join the Golf League!</h2>
              <p>You've been invited to join our golf league. Click the button below to accept your invitation and complete your registration.</p>
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

        var message = new EmailMessage(
            senderAddress: _senderAddress,
            recipients: new EmailRecipients([new EmailAddress(toEmail)]),
            content: new EmailContent(subject) { Html = html });

        try
        {
            var operation = await _client.SendAsync(Azure.WaitUntil.Started, message, cancellationToken);
            _logger.LogInformation("Invite email queued for {Email}, operation id: {OperationId}", toEmail, operation.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send invite email to {Email}", toEmail);
            throw;
        }
    }
}
