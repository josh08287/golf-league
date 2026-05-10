namespace GolfLeague.Application.Common;

public interface IEmailService
{
    Task SendInviteAsync(string toEmail, string inviteLink, DateTime expiresAt, CancellationToken cancellationToken = default);

    Task SendPasswordResetAsync(string toEmail, string resetLink, CancellationToken cancellationToken = default);
}
