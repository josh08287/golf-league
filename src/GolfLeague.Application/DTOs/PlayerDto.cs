namespace GolfLeague.Application.DTOs;

public sealed record PlayerDto(
    int Id,
    string FirstName,
    string LastName,
    string FullName,
    string Initials,
    string Email,
    string EntraObjectId,
    bool IsActive,
    double? CurrentHandicapIndex);
