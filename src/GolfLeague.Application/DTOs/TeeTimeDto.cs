using GolfLeague.Domain.Enums;

namespace GolfLeague.Application.DTOs;

public sealed record TeeTimeSlotDto(
    int Id,
    int TeeTimeNumber,
    string ScheduledTime, // "15:28" formatted; UI parses
    bool AutoFilled,
    IReadOnlyList<TeeTimeParticipantDto> Players);

public sealed record TeeTimeParticipantDto(
    int ParticipantId,
    int PlayerId,
    string PlayerName,
    int? FlightId,
    string FlightName);

public sealed record RoundTeeTimeScheduleDto(
    int RoundId,
    DateTime CutoffUtc,
    bool IsLocked,
    int ParticipantCount,
    int? CurrentUserParticipantId,
    int? CurrentUserTeeTimeId,
    IReadOnlyList<TeeTimeSlotDto> Slots,
    TeeTimeSlotPreference CurrentUserPreferredSlots = TeeTimeSlotPreference.None);
