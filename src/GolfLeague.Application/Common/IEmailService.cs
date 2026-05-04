namespace GolfLeague.Application.Common;

public interface IEmailService
{
    Task SendInviteAsync(string toEmail, string inviteLink, DateTime expiresAt, CancellationToken cancellationToken = default);
}
