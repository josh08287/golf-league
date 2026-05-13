using GolfLeague.Domain.Enums;

namespace GolfLeague.Application.DTOs;

/// <summary>
/// One row per round a player has participated in, summarising their
/// totals for the round. Used by the player profile page to render a
/// compact past-rounds table.
/// </summary>
public sealed record PlayerRoundSummaryDto(
    int RoundId,
    DateOnly RoundDate,
    int WeekNumber,
    string CourseName,
    NineHoleSide NineHoleSide,
    RoundStatus Status,
    int? TotalGrossStrokes,
    int? TotalNetStrokes,
    int? TotalGrossStablefordPoints,
    int? TotalNetStablefordPoints,
    bool IsWithdrawn,
    bool SkippedWeek,
    double? NineHoleScoreDifferential = null);
