namespace GolfLeague.Application.DTOs;

public sealed record ScorecardDto(
    int RoundId,
    DateOnly RoundDate,
    string CourseName,
    double CourseRating,
    int SlopeRating,
    ParticipantDto Participant,
    List<HoleScoreDto> HoleScores,
    int FrontNinePar,
    int BackNinePar,
    int TotalPar,
    int FrontNineGross,
    int BackNineGross,
    int TotalGross,
    int FrontNineNet,
    int BackNineNet,
    int TotalNet,
    int FrontNinePoints,
    int BackNinePoints,
    int TotalPoints);
