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
    string FlightName,
    bool IsSubstitute = false);

public sealed record RoundTeeTimeScheduleDto(
    int RoundId,
    DateTime CutoffUtc,
    bool IsLocked,
    int ParticipantCount,
    int? CurrentUserParticipantId,
    int? CurrentUserTeeTimeId,
    IReadOnlyList<TeeTimeSlotDto> Slots,
    int WeekNumber,
    string RoundDate, // "yyyy-MM-dd"
    string CourseName,
    TeeTimeSlotPreference CurrentUserPreferredSlots = TeeTimeSlotPreference.None,
    bool CurrentUserSkippedWeek = false,
    bool IsRoundDay = false,
    int SkippedCount = 0,
    int SubstituteCount = 0,
    bool SubstitutesEnabled = false,
    // Caller is flagged IsSubstitute in the pool but isn't in this round —
    // drives the self-service "Join as substitute" buttons.
    bool CurrentUserIsSubstitutePoolMember = false,
    // Caller's participant row in this round is a substitute row — their
    // "leave" removes them from the round entirely.
    bool CurrentUserIsSubstitute = false);
