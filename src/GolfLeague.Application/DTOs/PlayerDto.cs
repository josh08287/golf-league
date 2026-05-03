namespace GolfLeague.Application.DTOs;

public sealed record PlayerDto(
    int Id,
    string FullName,
    string Email,
    bool IsActive,
    double? CurrentHandicap,
    int? FlightId,
    string? FlightName);
