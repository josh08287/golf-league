class Flight {
  const Flight({
    required this.id,
    required this.name,
    required this.seasonId,
    this.minHandicap,
    this.maxHandicap,
    required this.displayOrder,
    required this.playerCount,
  });

  final int id;
  final String name;
  final int seasonId;
  final double? minHandicap;
  final double? maxHandicap;
  final int displayOrder;
  final int playerCount;

  factory Flight.fromJson(Map<String, dynamic> j) => Flight(
    id: (j['id'] as num).toInt(),
    name: j['name'] as String,
    seasonId: (j['seasonId'] as num).toInt(),
    minHandicap: (j['minHandicap'] as num?)?.toDouble(),
    maxHandicap: (j['maxHandicap'] as num?)?.toDouble(),
    displayOrder: (j['displayOrder'] as num).toInt(),
    playerCount: (j['playerCount'] as num).toInt(),
  );
}

enum RoundStatus {
  scheduled,
  inProgress,
  pendingFinalization,
  finalized,
  cancelled,
}

RoundStatus parseRoundStatus(dynamic raw) {
  if (raw is int) {
    const map = [
      RoundStatus.scheduled,
      RoundStatus.inProgress,
      RoundStatus.pendingFinalization,
      RoundStatus.finalized,
      RoundStatus.cancelled,
    ];
    return (raw >= 0 && raw < map.length) ? map[raw] : RoundStatus.scheduled;
  }
  switch (raw?.toString().toLowerCase()) {
    case 'inprogress':
      return RoundStatus.inProgress;
    case 'pendingfinalization':
      return RoundStatus.pendingFinalization;
    case 'finalized':
      return RoundStatus.finalized;
    case 'cancelled':
      return RoundStatus.cancelled;
    default:
      return RoundStatus.scheduled;
  }
}

extension RoundStatusLabel on RoundStatus {
  String get label {
    switch (this) {
      case RoundStatus.scheduled:
        return 'Scheduled';
      case RoundStatus.inProgress:
        return 'In Progress';
      case RoundStatus.pendingFinalization:
        return 'Pending';
      case RoundStatus.finalized:
        return 'Finalized';
      case RoundStatus.cancelled:
        return 'Cancelled';
    }
  }
}

class Round {
  const Round({
    required this.id,
    required this.courseId,
    required this.courseName,
    this.flightId,
    this.flightName,
    required this.scheduledDate,
    required this.status,
    required this.participantCount,
    this.weekNumber,
    this.seasonId,
    this.halfId,
    this.nineHoleSide = 'Front',
  });

  final int id;
  final int courseId;
  final String courseName;
  final int? flightId;
  final String? flightName;
  final DateTime scheduledDate;
  final RoundStatus status;
  final int participantCount;
  final int? weekNumber;
  final int? seasonId;
  final int? halfId;
  final String nineHoleSide;

  factory Round.fromJson(Map<String, dynamic> j) => Round(
    id: (j['id'] as num).toInt(),
    courseId: (j['courseId'] as num).toInt(),
    courseName: j['courseName'] as String? ?? '',
    flightId: (j['flightId'] as num?)?.toInt(),
    flightName: j['flightName'] as String?,
    scheduledDate: DateTime.parse(j['scheduledDate'] as String),
    status: parseRoundStatus(j['status']),
    participantCount: (j['participantCount'] as num? ?? 0).toInt(),
    weekNumber: (j['weekNumber'] as num?)?.toInt(),
    seasonId: (j['seasonId'] as num?)?.toInt(),
    halfId: (j['halfId'] as num?)?.toInt(),
    nineHoleSide: j['nineHoleSide'] as String? ?? 'Front',
  );
}

class HoleScore {
  const HoleScore({
    required this.holeNumber,
    required this.par,
    required this.strokes,
    required this.netStrokes,
    required this.strokeIndex,
  });

  final int holeNumber;
  final int par;
  final int strokes;
  final int netStrokes;
  final int strokeIndex;

  factory HoleScore.fromJson(Map<String, dynamic> j) => HoleScore(
    holeNumber: (j['holeNumber'] as num).toInt(),
    par: (j['par'] as num).toInt(),
    strokes: (j['strokes'] as num).toInt(),
    netStrokes: (j['netStrokes'] as num).toInt(),
    strokeIndex: (j['strokeIndex'] as num? ?? 0).toInt(),
  );
}

class Scorecard {
  const Scorecard({
    required this.roundId,
    required this.playerId,
    required this.playerName,
    required this.handicapAtTime,
    this.grossScore,
    this.netScore,
    this.points,
    required this.holes,
  });

  final int roundId;
  final int playerId;
  final String playerName;
  final double handicapAtTime;
  final int? grossScore;
  final int? netScore;
  final int? points;
  final List<HoleScore> holes;

  factory Scorecard.fromJson(Map<String, dynamic> j) => Scorecard(
    roundId: (j['roundId'] as num).toInt(),
    playerId: (j['playerId'] as num).toInt(),
    playerName: j['playerName'] as String? ?? '',
    handicapAtTime: (j['handicapAtTime'] as num? ?? 0).toDouble(),
    grossScore: (j['grossScore'] as num?)?.toInt(),
    netScore: (j['netScore'] as num?)?.toInt(),
    points: (j['points'] as num?)?.toInt(),
    holes: ((j['holes'] as List<dynamic>?) ?? [])
        .map((h) => HoleScore.fromJson(h as Map<String, dynamic>))
        .toList(),
  );
}

// ── Player Models ─────────────────────────────────────────────────────────────

class Player {
  const Player({
    required this.id,
    required this.fullName,
    this.email,
    required this.isActive,
    this.currentHandicap,
    this.flightId,
    this.flightName,
    required this.roles,
    this.preferredTeeTimeSlots = 0,
  });

  final int id;
  final String fullName;
  final String? email;
  final bool isActive;
  final double? currentHandicap;
  final int? flightId;
  final String? flightName;
  final List<String> roles;
  final int preferredTeeTimeSlots;

  factory Player.fromJson(Map<String, dynamic> j) => Player(
    id: (j['id'] as num).toInt(),
    fullName: j['fullName'] as String,
    email: j['email'] as String?,
    isActive: j['isActive'] as bool? ?? true,
    currentHandicap: (j['currentHandicap'] as num?)?.toDouble(),
    flightId: (j['flightId'] as num?)?.toInt(),
    flightName: j['flightName'] as String?,
    roles: (j['roles'] as List<dynamic>?)?.cast<String>() ?? ['player'],
    preferredTeeTimeSlots: (j['preferredTeeTimeSlots'] as num?)?.toInt() ?? 0,
  );
}

class Standing {
  const Standing({
    required this.position,
    required this.playerId,
    required this.playerFullName,
    required this.playerInitials,
    required this.roundsPlayed,
    required this.totalPoints,
    required this.averagePoints,
    required this.currentHandicapIndex,
  });

  final int position;
  final int playerId;
  final String playerFullName;
  final String playerInitials;
  final int roundsPlayed;
  final int totalPoints;
  final double averagePoints;
  final double currentHandicapIndex;

  factory Standing.fromJson(Map<String, dynamic> j) => Standing(
    position: (j['position'] as num).toInt(),
    playerId: (j['playerId'] as num).toInt(),
    playerFullName: j['playerFullName'] as String,
    playerInitials: j['playerInitials'] as String? ?? '',
    roundsPlayed: (j['roundsPlayed'] as num).toInt(),
    totalPoints: (j['totalPoints'] as num).toInt(),
    averagePoints: (j['averagePoints'] as num).toDouble(),
    currentHandicapIndex: (j['currentHandicapIndex'] as num).toDouble(),
  );
}

class HandicapHistoryEntry {
  const HandicapHistoryEntry({
    required this.id,
    required this.playerId,
    required this.handicapIndex,
    required this.nineHoleHandicapIndex,
    required this.effectiveDate,
    required this.source,
    this.notes,
  });

  final int id;
  final int playerId;
  final double handicapIndex;
  final double nineHoleHandicapIndex;
  final DateTime effectiveDate;
  final String source;
  final String? notes;

  factory HandicapHistoryEntry.fromJson(Map<String, dynamic> j) =>
      HandicapHistoryEntry(
        id: (j['id'] as num).toInt(),
        playerId: (j['playerId'] as num).toInt(),
        handicapIndex: (j['handicapIndex'] as num).toDouble(),
        nineHoleHandicapIndex: (j['nineHoleHandicapIndex'] as num).toDouble(),
        effectiveDate: DateTime.parse(j['effectiveDate'] as String),
        source: j['source'] as String,
        notes: j['notes'] as String?,
      );
}

class PlayerRoundSummary {
  const PlayerRoundSummary({
    required this.roundId,
    required this.roundDate,
    required this.weekNumber,
    required this.courseName,
    required this.nineHoleSide,
    required this.status,
    this.totalGrossStrokes,
    this.totalNetStrokes,
    this.totalGrossStablefordPoints,
    this.totalNetStablefordPoints,
    this.isWithdrawn = false,
    this.skippedWeek = false,
    this.scoreDifferential,
    this.nineHoleScoreDifferential,
  });

  final int roundId;
  final DateTime roundDate;
  final int weekNumber;
  final String courseName;
  final String nineHoleSide;
  final RoundStatus status;
  final int? totalGrossStrokes;
  final int? totalNetStrokes;
  final int? totalGrossStablefordPoints;
  final int? totalNetStablefordPoints;
  final bool isWithdrawn;
  final bool skippedWeek;
  final double? scoreDifferential;
  final double? nineHoleScoreDifferential;

  factory PlayerRoundSummary.fromJson(Map<String, dynamic> j) =>
      PlayerRoundSummary(
        roundId: (j['roundId'] as num).toInt(),
        roundDate: DateTime.parse(j['roundDate'] as String),
        weekNumber: (j['weekNumber'] as num).toInt(),
        courseName: j['courseName'] as String,
        nineHoleSide: j['nineHoleSide'] as String,
        status: parseRoundStatus(j['status']),
        totalGrossStrokes: (j['totalGrossStrokes'] as num?)?.toInt(),
        totalNetStrokes: (j['totalNetStrokes'] as num?)?.toInt(),
        totalGrossStablefordPoints: (j['totalGrossStablefordPoints'] as num?)
            ?.toInt(),
        totalNetStablefordPoints: (j['totalNetStablefordPoints'] as num?)
            ?.toInt(),
        isWithdrawn: j['isWithdrawn'] as bool? ?? false,
        skippedWeek: j['skippedWeek'] as bool? ?? false,
        scoreDifferential: (j['scoreDifferential'] as num?)?.toDouble(),
        nineHoleScoreDifferential: (j['nineHoleScoreDifferential'] as num?)
            ?.toDouble(),
      );
}

// ── Tee Time Models ───────────────────────────────────────────────────────────

class TeeTimeParticipant {
  const TeeTimeParticipant({
    required this.participantId,
    required this.playerId,
    required this.playerName,
    required this.flightId,
    required this.flightName,
  });

  final int participantId;
  final int playerId;
  final String playerName;
  final int flightId;
  final String flightName;

  factory TeeTimeParticipant.fromJson(Map<String, dynamic> j) =>
      TeeTimeParticipant(
        participantId: (j['participantId'] as num).toInt(),
        playerId: (j['playerId'] as num).toInt(),
        playerName: j['playerName'] as String,
        flightId: (j['flightId'] as num).toInt(),
        flightName: j['flightName'] as String,
      );
}

class TeeTimeSlot {
  const TeeTimeSlot({
    required this.id,
    required this.teeTimeNumber,
    required this.scheduledTime,
    required this.autoFilled,
    required this.players,
  });

  final int id;
  final int teeTimeNumber;
  final String scheduledTime;
  final bool autoFilled;
  final List<TeeTimeParticipant> players;

  factory TeeTimeSlot.fromJson(Map<String, dynamic> j) => TeeTimeSlot(
    id: (j['id'] as num).toInt(),
    teeTimeNumber: (j['teeTimeNumber'] as num).toInt(),
    scheduledTime: j['scheduledTime'] as String,
    autoFilled: j['autoFilled'] as bool? ?? false,
    players: ((j['players'] as List<dynamic>?) ?? [])
        .map((p) => TeeTimeParticipant.fromJson(p as Map<String, dynamic>))
        .toList(),
  );
}

class RoundTeeTimeSchedule {
  const RoundTeeTimeSchedule({
    required this.roundId,
    required this.cutoffUtc,
    required this.isLocked,
    required this.participantCount,
    this.currentUserParticipantId,
    this.currentUserTeeTimeId,
    required this.slots,
    this.currentUserPreferredSlots = 0,
  });

  final int roundId;
  final String cutoffUtc;
  final bool isLocked;
  final int participantCount;
  final int? currentUserParticipantId;
  final int? currentUserTeeTimeId;
  final List<TeeTimeSlot> slots;
  final int currentUserPreferredSlots;

  factory RoundTeeTimeSchedule.fromJson(Map<String, dynamic> j) =>
      RoundTeeTimeSchedule(
        roundId: (j['roundId'] as num).toInt(),
        cutoffUtc: j['cutoffUtc'] as String,
        isLocked: j['isLocked'] as bool,
        participantCount: (j['participantCount'] as num).toInt(),
        currentUserParticipantId: (j['currentUserParticipantId'] as num?)
            ?.toInt(),
        currentUserTeeTimeId: (j['currentUserTeeTimeId'] as num?)?.toInt(),
        slots: ((j['slots'] as List<dynamic>?) ?? [])
            .map((s) => TeeTimeSlot.fromJson(s as Map<String, dynamic>))
            .toList(),
        currentUserPreferredSlots:
            (j['currentUserPreferredSlots'] as num?)?.toInt() ?? 0,
      );
}

class MyTodaysTeeTime {
  const MyTodaysTeeTime({
    required this.roundId,
    required this.roundDate,
    required this.courseName,
    required this.courseId,
    required this.nineHoleSide,
    required this.roundStatus,
    required this.teeTimeId,
    required this.scheduledTime,
    required this.scheduledTimeFormatted,
    required this.teeTimeNumber,
    required this.canEnterScores,
  });

  final int roundId;
  final DateTime roundDate;
  final String courseName;
  final int courseId;
  final String nineHoleSide;
  final RoundStatus roundStatus;
  final int teeTimeId;
  final String scheduledTime;
  final String scheduledTimeFormatted;
  final int teeTimeNumber;
  final bool canEnterScores;

  factory MyTodaysTeeTime.fromJson(Map<String, dynamic> j) => MyTodaysTeeTime(
    roundId: (j['roundId'] as num).toInt(),
    roundDate: DateTime.parse(j['roundDate'] as String),
    courseName: j['courseName'] as String,
    courseId: (j['courseId'] as num).toInt(),
    nineHoleSide: j['nineHoleSide'] as String,
    roundStatus: parseRoundStatus(j['roundStatus']),
    teeTimeId: (j['teeTimeId'] as num).toInt(),
    scheduledTime: j['scheduledTime'] as String,
    scheduledTimeFormatted: j['scheduledTimeFormatted'] as String,
    teeTimeNumber: (j['teeTimeNumber'] as num).toInt(),
    canEnterScores: j['canEnterScores'] as bool,
  );
}

// ── Score Entry Models ────────────────────────────────────────────────────────

class TeeTimeHoleInfo {
  const TeeTimeHoleInfo({
    required this.holeNumber,
    required this.par,
    required this.strokeIndex,
  });

  final int holeNumber;
  final int par;
  final int strokeIndex;

  factory TeeTimeHoleInfo.fromJson(Map<String, dynamic> j) => TeeTimeHoleInfo(
    holeNumber: (j['holeNumber'] as num).toInt(),
    par: (j['par'] as num).toInt(),
    strokeIndex: (j['strokeIndex'] as num).toInt(),
  );
}

class TeeTimePlayerHoleScore {
  const TeeTimePlayerHoleScore({
    required this.holeNumber,
    required this.par,
    required this.strokeIndex,
    this.grossStrokes,
    this.netStrokes,
    this.grossStablefordPoints,
    this.netStablefordPoints,
    this.putts,
    this.firstPuttDistanceFeet,
    this.fairwayHit,
    this.gir,
  });

  final int holeNumber;
  final int par;
  final int strokeIndex;
  final int? grossStrokes;
  final int? netStrokes;
  final int? grossStablefordPoints;
  final int? netStablefordPoints;
  final int? putts;
  final int? firstPuttDistanceFeet;
  final bool? fairwayHit;
  final bool? gir;

  factory TeeTimePlayerHoleScore.fromJson(Map<String, dynamic> j) =>
      TeeTimePlayerHoleScore(
        holeNumber: (j['holeNumber'] as num).toInt(),
        par: (j['par'] as num).toInt(),
        strokeIndex: (j['strokeIndex'] as num).toInt(),
        grossStrokes: (j['grossStrokes'] as num?)?.toInt(),
        netStrokes: (j['netStrokes'] as num?)?.toInt(),
        grossStablefordPoints: (j['grossStablefordPoints'] as num?)?.toInt(),
        netStablefordPoints: (j['netStablefordPoints'] as num?)?.toInt(),
        putts: (j['putts'] as num?)?.toInt(),
        firstPuttDistanceFeet: (j['firstPuttDistanceFeet'] as num?)?.toInt(),
        fairwayHit: j['fairwayHit'] as bool?,
        gir: j['gir'] as bool?,
      );
}

class TeeTimePlayerScore {
  const TeeTimePlayerScore({
    required this.participantId,
    required this.playerId,
    required this.playerName,
    required this.playerInitials,
    required this.flightId,
    required this.flightName,
    required this.handicapIndex,
    required this.courseHandicap,
    required this.isWithdrawn,
    required this.skippedWeek,
    required this.holeScores,
    this.totalGrossStrokes,
    this.totalNetStrokes,
    this.totalGrossStablefordPoints,
    this.totalNetStablefordPoints,
  });

  final int participantId;
  final int playerId;
  final String playerName;
  final String playerInitials;
  final int flightId;
  final String flightName;
  final double handicapIndex;
  final double courseHandicap;
  final bool isWithdrawn;
  final bool skippedWeek;
  final List<TeeTimePlayerHoleScore> holeScores;
  final int? totalGrossStrokes;
  final int? totalNetStrokes;
  final int? totalGrossStablefordPoints;
  final int? totalNetStablefordPoints;

  factory TeeTimePlayerScore.fromJson(
    Map<String, dynamic> j,
  ) => TeeTimePlayerScore(
    participantId: (j['participantId'] as num).toInt(),
    playerId: (j['playerId'] as num).toInt(),
    playerName: j['playerName'] as String,
    playerInitials: j['playerInitials'] as String? ?? '',
    flightId: (j['flightId'] as num).toInt(),
    flightName: j['flightName'] as String,
    handicapIndex: (j['handicapIndex'] as num).toDouble(),
    courseHandicap: (j['courseHandicap'] as num).toDouble(),
    isWithdrawn: j['isWithdrawn'] as bool? ?? false,
    skippedWeek: j['skippedWeek'] as bool? ?? false,
    holeScores: ((j['holeScores'] as List<dynamic>?) ?? [])
        .map((h) => TeeTimePlayerHoleScore.fromJson(h as Map<String, dynamic>))
        .toList(),
    totalGrossStrokes: (j['totalGrossStrokes'] as num?)?.toInt(),
    totalNetStrokes: (j['totalNetStrokes'] as num?)?.toInt(),
    totalGrossStablefordPoints: (j['totalGrossStablefordPoints'] as num?)
        ?.toInt(),
    totalNetStablefordPoints: (j['totalNetStablefordPoints'] as num?)?.toInt(),
  );
}

class TeeTimeGroupScorecard {
  const TeeTimeGroupScorecard({
    required this.roundId,
    required this.roundDate,
    required this.courseName,
    required this.courseId,
    required this.nineHoleSide,
    required this.roundStatus,
    required this.teeTimeId,
    required this.scheduledTimeFormatted,
    required this.teeTimeNumber,
    required this.holes,
    required this.players,
  });

  final int roundId;
  final DateTime roundDate;
  final String courseName;
  final int courseId;
  final String nineHoleSide;
  final RoundStatus roundStatus;
  final int teeTimeId;
  final String scheduledTimeFormatted;
  final int teeTimeNumber;
  final List<TeeTimeHoleInfo> holes;
  final List<TeeTimePlayerScore> players;

  factory TeeTimeGroupScorecard.fromJson(Map<String, dynamic> j) =>
      TeeTimeGroupScorecard(
        roundId: (j['roundId'] as num).toInt(),
        roundDate: DateTime.parse(j['roundDate'] as String),
        courseName: j['courseName'] as String,
        courseId: (j['courseId'] as num).toInt(),
        nineHoleSide: j['nineHoleSide'] as String,
        roundStatus: parseRoundStatus(j['roundStatus']),
        teeTimeId: (j['teeTimeId'] as num).toInt(),
        scheduledTimeFormatted: j['scheduledTimeFormatted'] as String,
        teeTimeNumber: (j['teeTimeNumber'] as num).toInt(),
        holes: ((j['holes'] as List<dynamic>?) ?? [])
            .map((h) => TeeTimeHoleInfo.fromJson(h as Map<String, dynamic>))
            .toList(),
        players: ((j['players'] as List<dynamic>?) ?? [])
            .map((p) => TeeTimePlayerScore.fromJson(p as Map<String, dynamic>))
            .toList(),
      );
}

// ── Course Models ─────────────────────────────────────────────────────────────

class Course {
  const Course({
    required this.id,
    required this.name,
    required this.rating,
    required this.slope,
    required this.holeCount,
  });

  final int id;
  final String name;
  final double rating;
  final int slope;
  final int holeCount;

  factory Course.fromJson(Map<String, dynamic> j) => Course(
    id: (j['id'] as num).toInt(),
    name: j['name'] as String,
    rating: (j['rating'] as num).toDouble(),
    slope: (j['slope'] as num).toInt(),
    holeCount: (j['holeCount'] as num).toInt(),
  );
}

class CourseHole {
  const CourseHole({
    required this.holeNumber,
    required this.par,
    required this.strokeIndex,
  });

  final int holeNumber;
  final int par;
  final int strokeIndex;

  factory CourseHole.fromJson(Map<String, dynamic> j) => CourseHole(
    holeNumber: (j['holeNumber'] as num).toInt(),
    par: (j['par'] as num).toInt(),
    strokeIndex: (j['strokeIndex'] as num).toInt(),
  );
}

// ── Statistics Models ─────────────────────────────────────────────────────────

class HoleStatistics {
  const HoleStatistics({
    required this.holeNumber,
    required this.par,
    required this.strokeIndex,
    required this.averageGrossStrokes,
    required this.averageNetStrokes,
    required this.averageGrossStablefordPoints,
    required this.averageNetStablefordPoints,
    required this.averageScoreToPar,
    required this.totalScoresRecorded,
    required this.eagleOrBetterCount,
    required this.birdieCount,
    required this.parCount,
    required this.bogeyCount,
    required this.doubleBogeyOrWorseCount,
    required this.difficultyRank,
  });

  final int holeNumber;
  final int par;
  final int strokeIndex;
  final double averageGrossStrokes;
  final double averageNetStrokes;
  final double averageGrossStablefordPoints;
  final double averageNetStablefordPoints;
  final double averageScoreToPar;
  final int totalScoresRecorded;
  final int eagleOrBetterCount;
  final int birdieCount;
  final int parCount;
  final int bogeyCount;
  final int doubleBogeyOrWorseCount;
  final int difficultyRank;

  factory HoleStatistics.fromJson(Map<String, dynamic> j) => HoleStatistics(
    holeNumber: (j['holeNumber'] as num).toInt(),
    par: (j['par'] as num).toInt(),
    strokeIndex: (j['strokeIndex'] as num).toInt(),
    averageGrossStrokes: (j['averageGrossStrokes'] as num).toDouble(),
    averageNetStrokes: (j['averageNetStrokes'] as num).toDouble(),
    averageGrossStablefordPoints: (j['averageGrossStablefordPoints'] as num)
        .toDouble(),
    averageNetStablefordPoints: (j['averageNetStablefordPoints'] as num)
        .toDouble(),
    averageScoreToPar: (j['averageScoreToPar'] as num).toDouble(),
    totalScoresRecorded: (j['totalScoresRecorded'] as num).toInt(),
    eagleOrBetterCount: (j['eagleOrBetterCount'] as num).toInt(),
    birdieCount: (j['birdieCount'] as num).toInt(),
    parCount: (j['parCount'] as num).toInt(),
    bogeyCount: (j['bogeyCount'] as num).toInt(),
    doubleBogeyOrWorseCount: (j['doubleBogeyOrWorseCount'] as num).toInt(),
    difficultyRank: (j['difficultyRank'] as num).toInt(),
  );
}

class CourseStatistics {
  const CourseStatistics({
    required this.courseId,
    required this.courseName,
    required this.courseRating,
    required this.slopeRating,
    required this.totalRoundsPlayed,
    required this.totalScorecardsRecorded,
    this.averageTotalGrossStrokes,
    this.averageTotalNetStrokes,
    this.averageTotalGrossStablefordPoints,
    this.averageTotalNetStablefordPoints,
    this.averageScoreToPar,
    required this.holeStatistics,
  });

  final int courseId;
  final String courseName;
  final double courseRating;
  final int slopeRating;
  final int totalRoundsPlayed;
  final int totalScorecardsRecorded;
  final double? averageTotalGrossStrokes;
  final double? averageTotalNetStrokes;
  final double? averageTotalGrossStablefordPoints;
  final double? averageTotalNetStablefordPoints;
  final double? averageScoreToPar;
  final List<HoleStatistics> holeStatistics;

  factory CourseStatistics.fromJson(Map<String, dynamic> j) => CourseStatistics(
    courseId: (j['courseId'] as num).toInt(),
    courseName: j['courseName'] as String,
    courseRating: (j['courseRating'] as num).toDouble(),
    slopeRating: (j['slopeRating'] as num).toInt(),
    totalRoundsPlayed: (j['totalRoundsPlayed'] as num).toInt(),
    totalScorecardsRecorded: (j['totalScorecardsRecorded'] as num).toInt(),
    averageTotalGrossStrokes: (j['averageTotalGrossStrokes'] as num?)
        ?.toDouble(),
    averageTotalNetStrokes: (j['averageTotalNetStrokes'] as num?)?.toDouble(),
    averageTotalGrossStablefordPoints:
        (j['averageTotalGrossStablefordPoints'] as num?)?.toDouble(),
    averageTotalNetStablefordPoints:
        (j['averageTotalNetStablefordPoints'] as num?)?.toDouble(),
    averageScoreToPar: (j['averageScoreToPar'] as num?)?.toDouble(),
    holeStatistics: ((j['holeStatistics'] as List<dynamic>?) ?? [])
        .map((h) => HoleStatistics.fromJson(h as Map<String, dynamic>))
        .toList(),
  );
}

class MostImprovedPlayer {
  const MostImprovedPlayer({
    required this.playerId,
    required this.playerName,
    required this.seasonHalfName,
    required this.startingHandicapIndex,
    required this.currentHandicapIndex,
    required this.improvementFactor,
    required this.handicapReduction,
    required this.roundsPlayedInHalf,
  });

  final int playerId;
  final String playerName;
  final String seasonHalfName;
  final double startingHandicapIndex;
  final double currentHandicapIndex;
  final double improvementFactor;
  final double handicapReduction;
  final int roundsPlayedInHalf;

  factory MostImprovedPlayer.fromJson(Map<String, dynamic> j) =>
      MostImprovedPlayer(
        playerId: (j['playerId'] as num).toInt(),
        playerName: j['playerName'] as String,
        seasonHalfName: j['seasonHalfName'] as String,
        startingHandicapIndex: (j['startingHandicapIndex'] as num).toDouble(),
        currentHandicapIndex: (j['currentHandicapIndex'] as num).toDouble(),
        improvementFactor: (j['improvementFactor'] as num).toDouble(),
        handicapReduction: (j['handicapReduction'] as num).toDouble(),
        roundsPlayedInHalf: (j['roundsPlayedInHalf'] as num).toInt(),
      );
}

class MostImprovedResult {
  const MostImprovedResult({
    this.winner,
    required this.leaderboard,
    required this.seasonHalfName,
    required this.minRoundsRequired,
  });

  final MostImprovedPlayer? winner;
  final List<MostImprovedPlayer> leaderboard;
  final String seasonHalfName;
  final int minRoundsRequired;

  factory MostImprovedResult.fromJson(Map<String, dynamic> j) {
    final winnerData = j['winner'] as Map<String, dynamic>?;
    return MostImprovedResult(
      winner: winnerData != null
          ? MostImprovedPlayer.fromJson(winnerData)
          : null,
      leaderboard: ((j['leaderboard'] as List<dynamic>?) ?? [])
          .map((p) => MostImprovedPlayer.fromJson(p as Map<String, dynamic>))
          .toList(),
      seasonHalfName: j['seasonHalfName'] as String,
      minRoundsRequired: (j['minRoundsRequired'] as num).toInt(),
    );
  }
}

// ── Tee Time Slots Constants ──────────────────────────────────────────────────

const teeTimeSlots = ['Early', 'Middle', 'Late'];
const teeTimeSlotFlags = {'Early': 1, 'Middle': 2, 'Late': 4};
