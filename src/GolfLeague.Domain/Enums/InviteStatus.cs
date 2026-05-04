namespace GolfLeague.Domain.Enums;

public enum InviteStatus
{
    Pending,   // sent, not yet accepted
    Accepted,  // user clicked link and linked their account
    Revoked    // admin cancelled before acceptance
}
