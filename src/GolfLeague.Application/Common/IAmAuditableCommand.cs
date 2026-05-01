namespace GolfLeague.Application.Common;

public interface IAmAuditableCommand
{
    string UserId { get; }
}
