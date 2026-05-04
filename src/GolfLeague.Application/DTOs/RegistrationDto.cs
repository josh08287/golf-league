namespace GolfLeague.Application.DTOs;

public sealed record RegistrationDto(
    int Id,
    string EntraObjectId,
    string FirstName,
    string LastName,
    string Email,
    string? Phone,
    string Status,
    DateTime RequestedAt,
    DateTime? ReviewedAt,
    string? RejectionReason,
    int? PlayerId);
