using GolfLeague.Domain.Enums;

namespace GolfLeague.Application.DTOs;

/// <summary>A player's flight assignment for a specific half.</summary>
public sealed record HalfFlightMembership(int HalfId, int FlightId, string FlightName);

public sealed record PlayerDto(
    int Id,
    string FullName,
    string? Email,
    bool IsActive,
    double? CurrentHandicap,
    int? FlightId,
    string? FlightName,
    IReadOnlyList<string> Roles,
    Guid? AppUserId = null,
    TeeTimeSlotPreference PreferredTeeTimeSlots = TeeTimeSlotPreference.None,
    IReadOnlyList<HalfFlightMembership>? FlightMemberships = null,
    bool TeeTimeEmailOptOut = false);
