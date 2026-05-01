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
    int FrontNineNet,
    int BackNineNet,
    int FrontNinePoints,
    int BackNinePoints);
