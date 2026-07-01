class Flight {
  const Flight({
    required this.id,
    required this.name,
    required this.seasonId,
    this.halfId,
    this.minHandicap,
    this.maxHandicap,
    required this.displayOrder,
    required this.playerCount,
    this.isLocked = false,
  });

  final int id;
  final String name;
  final int seasonId;
  final int? halfId;
  final double? minHandicap;
  final double? maxHandicap;
  final int displayOrder;
  final int playerCount;
  final bool isLocked;

  factory Flight.fromJson(Map<String, dynamic> j) => Flight(
    id: (j['id'] as num).toInt(),
    name: j['name'] as String,
    seasonId: (j['seasonId'] as num).toInt(),
    halfId: (j['halfId'] as num?)?.toInt(),
    minHandicap: (j['minHandicap'] as num?)?.toDouble(),
    maxHandicap: (j['maxHandicap'] as num?)?.toDouble(),
    displayOrder: (j['displayOrder'] as num).toInt(),
    playerCount: (j['playerCount'] as num).toInt(),
    isLocked: j['isLocked'] as bool? ?? false,
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

/// "NineHole" | "EighteenHole" | "Tournament" — defaults to NineHole when
/// the API sends an int enum or omits the field.
String parseRoundType(dynamic raw) {
  if (raw is int) {
    const map = ['NineHole', 'EighteenHole', 'Tournament'];
    return (raw >= 0 && raw < map.length) ? map[raw] : 'NineHole';
  }
  final s = raw?.toString() ?? '';
  switch (s.toLowerCase()) {
    case 'tournament':
      return 'Tournament';
    case 'eighteenhole':
      return 'EighteenHole';
    default:
      return 'NineHole';
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
    this.roundType = 'NineHole',
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
  final String roundType;

  bool get isTournament => roundType == 'Tournament';

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
    roundType: parseRoundType(j['roundType']),
  );
}

class HoleScore {
  const HoleScore({
    required this.holeNumber,
    required this.par,
    required this.strokes,
    required this.netStrokes,
    required this.strokeIndex,
    this.grossPoints = 0,
    this.netPoints = 0,
  });

  final int holeNumber;
  final int par;
  final int strokes;
  final int netStrokes;
  final int strokeIndex;
  final int grossPoints;
  final int netPoints;

  factory HoleScore.fromJson(Map<String, dynamic> j) => HoleScore(
    holeNumber: (j['holeNumber'] as num).toInt(),
    par: (j['par'] as num).toInt(),
    strokes: (j['strokes'] as num).toInt(),
    netStrokes: (j['netStrokes'] as num).toInt(),
    strokeIndex: (j['strokeIndex'] as num? ?? 0).toInt(),
    grossPoints: (j['grossPoints'] as num? ?? 0).toInt(),
    netPoints: (j['netPoints'] as num? ?? 0).toInt(),
  );
}

class Scorecard {
  const Scorecard({
    required this.roundId,
    required this.playerId,
    required this.playerName,
    required this.handicapAtTime,
    this.flightId,
    this.flightName,
    this.courseHandicap,
    this.grossScore,
    this.netScore,
    this.points,
    this.grossPoints,
    required this.holes,
  });

  final int roundId;
  final int playerId;
  final String playerName;
  final double handicapAtTime;
  final int? flightId;
  final String? flightName;
  final double? courseHandicap;
  final int? grossScore;
  final int? netScore;
  final int? points; // net Stableford points
  final int? grossPoints;
  final List<HoleScore> holes;

  factory Scorecard.fromJson(Map<String, dynamic> j) => Scorecard(
    roundId: (j['roundId'] as num).toInt(),
    playerId: (j['playerId'] as num).toInt(),
    playerName: j['playerName'] as String? ?? '',
    handicapAtTime: (j['handicapAtTime'] as num? ?? 0).toDouble(),
    flightId: (j['flightId'] as num?)?.toInt(),
    flightName: j['flightName'] as String?,
    courseHandicap: (j['courseHandicap'] as num?)?.toDouble(),
    grossScore: (j['grossScore'] as num?)?.toInt(),
    netScore: (j['netScore'] as num?)?.toInt(),
    points: ((j['netPoints'] ?? j['points']) as num?)?.toInt(),
    grossPoints: (j['grossPoints'] as num?)?.toInt(),
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

class RoundScore {
  const RoundScore({
    required this.roundId,
    required this.weekNumber,
    this.points,
    this.grossStrokes,
    this.netStrokes,
    this.isSkipped = false,
    this.isDropped = false,
  });

  final int roundId;
  final int weekNumber;
  final int? points;
  final int? grossStrokes;
  final int? netStrokes;
  final bool isSkipped;
  final bool isDropped;

  factory RoundScore.fromJson(Map<String, dynamic> j) => RoundScore(
    roundId: (j['roundId'] as num).toInt(),
    weekNumber: (j['weekNumber'] as num).toInt(),
    points: (j['points'] as num?)?.toInt(),
    grossStrokes: (j['grossStrokes'] as num?)?.toInt(),
    netStrokes: (j['netStrokes'] as num?)?.toInt(),
    isSkipped: j['isSkipped'] as bool? ?? false,
    isDropped: j['isDropped'] as bool? ?? false,
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
    this.averageScore,
    this.roundScores = const [],
  });

  final int position;
  final int playerId;
  final String playerFullName;
  final String playerInitials;
  final int roundsPlayed;
  final int totalPoints;
  final double averagePoints;
  final double currentHandicapIndex;
  final double? averageScore;
  final List<RoundScore> roundScores;

  factory Standing.fromJson(Map<String, dynamic> j) => Standing(
    position: (j['position'] as num).toInt(),
    playerId: (j['playerId'] as num).toInt(),
    playerFullName: j['playerFullName'] as String,
    playerInitials: j['playerInitials'] as String? ?? '',
    roundsPlayed: (j['roundsPlayed'] as num).toInt(),
    totalPoints: (j['totalPoints'] as num).toInt(),
    averagePoints: (j['averagePoints'] as num).toDouble(),
    currentHandicapIndex: (j['currentHandicapIndex'] as num).toDouble(),
    averageScore: (j['averageScore'] as num?)?.toDouble(),
    roundScores: ((j['roundScores'] as List<dynamic>?) ?? [])
        .map((r) => RoundScore.fromJson(r as Map<String, dynamic>))
        .toList(),
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
    this.currentUserSkippedWeek = false,
    required this.slots,
    this.currentUserPreferredSlots = 0,
  });

  final int roundId;
  final String cutoffUtc;
  final bool isLocked;
  final int participantCount;
  final int? currentUserParticipantId;
  final int? currentUserTeeTimeId;
  final bool currentUserSkippedWeek;
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
        currentUserSkippedWeek: j['currentUserSkippedWeek'] as bool? ?? false,
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

// ── Seasons ───────────────────────────────────────────────────────────────────

class SeasonHalf {
  const SeasonHalf({
    required this.id,
    required this.seasonId,
    required this.halfNumber,
    required this.name,
  });

  final int id;
  final int seasonId;
  final int halfNumber;
  final String name;

  factory SeasonHalf.fromJson(Map<String, dynamic> j) => SeasonHalf(
    id: (j['id'] as num).toInt(),
    seasonId: (j['seasonId'] as num).toInt(),
    halfNumber: (j['halfNumber'] as num? ?? 0).toInt(),
    name: j['name'] as String? ?? '',
  );
}

class Season {
  const Season({
    required this.id,
    required this.name,
    required this.year,
    required this.isActive,
    this.halves = const [],
  });

  final int id;
  final String name;
  final int year;
  final bool isActive;
  final List<SeasonHalf> halves;

  factory Season.fromJson(Map<String, dynamic> j) => Season(
    id: (j['id'] as num).toInt(),
    name: j['name'] as String? ?? '',
    year: (j['year'] as num? ?? 0).toInt(),
    isActive: j['isActive'] as bool? ?? false,
    halves: ((j['halves'] as List<dynamic>?) ?? [])
        .map((h) => SeasonHalf.fromJson(h as Map<String, dynamic>))
        .toList(),
  );
}

// ── Skins ─────────────────────────────────────────────────────────────────────

class HoleSkin {
  const HoleSkin({
    required this.holeNumber,
    required this.skinValue,
    required this.winnerPlayerId,
    required this.winnerPlayerName,
    required this.wasCarryover,
  });

  final int holeNumber;
  final int skinValue;
  final int winnerPlayerId;
  final String winnerPlayerName;
  final bool wasCarryover;

  factory HoleSkin.fromJson(Map<String, dynamic> j) => HoleSkin(
    holeNumber: (j['holeNumber'] as num).toInt(),
    skinValue: (j['skinValue'] as num? ?? 0).toInt(),
    winnerPlayerId: (j['winnerPlayerId'] as num? ?? 0).toInt(),
    winnerPlayerName: j['winnerPlayerName'] as String? ?? '',
    wasCarryover: j['wasCarryover'] as bool? ?? false,
  );
}

class PlayerSkinSummary {
  const PlayerSkinSummary({
    required this.playerId,
    required this.playerName,
    required this.totalSkinsWon,
    required this.totalSkinValue,
    this.holesWon = const [],
  });

  final int playerId;
  final String playerName;
  final int totalSkinsWon;
  final int totalSkinValue;
  final List<HoleSkin> holesWon;

  factory PlayerSkinSummary.fromJson(Map<String, dynamic> j) =>
      PlayerSkinSummary(
        playerId: (j['playerId'] as num).toInt(),
        playerName: j['playerName'] as String? ?? '',
        totalSkinsWon: (j['totalSkinsWon'] as num? ?? 0).toInt(),
        totalSkinValue: (j['totalSkinValue'] as num? ?? 0).toInt(),
        holesWon: ((j['holesWon'] as List<dynamic>?) ?? [])
            .map((h) => HoleSkin.fromJson(h as Map<String, dynamic>))
            .toList(),
      );
}

class FlightSkins {
  const FlightSkins({
    required this.flightId,
    required this.flightName,
    required this.totalHolesWithSkins,
    required this.totalSkinValueAwarded,
    this.playerSummaries = const [],
    this.allHoleResults = const [],
  });

  final int flightId;
  final String flightName;
  final int totalHolesWithSkins;
  final int totalSkinValueAwarded;
  final List<PlayerSkinSummary> playerSummaries;
  final List<HoleSkin> allHoleResults;

  factory FlightSkins.fromJson(Map<String, dynamic> j) => FlightSkins(
    flightId: (j['flightId'] as num).toInt(),
    flightName: j['flightName'] as String? ?? '',
    totalHolesWithSkins: (j['totalHolesWithSkins'] as num? ?? 0).toInt(),
    totalSkinValueAwarded: (j['totalSkinValueAwarded'] as num? ?? 0).toInt(),
    playerSummaries: ((j['playerSummaries'] as List<dynamic>?) ?? [])
        .map((p) => PlayerSkinSummary.fromJson(p as Map<String, dynamic>))
        .toList(),
    allHoleResults: ((j['allHoleResults'] as List<dynamic>?) ?? [])
        .map((h) => HoleSkin.fromJson(h as Map<String, dynamic>))
        .toList(),
  );
}

class GrossPar3Skin {
  const GrossPar3Skin({
    required this.holeNumber,
    required this.skinValue,
    required this.winnerPlayerId,
    required this.winnerPlayerName,
    required this.winnerFlightName,
    required this.winningGrossScore,
    required this.wasCarryover,
  });

  final int holeNumber;
  final int skinValue;
  final int winnerPlayerId;
  final String winnerPlayerName;
  final String winnerFlightName;
  final int winningGrossScore;
  final bool wasCarryover;

  factory GrossPar3Skin.fromJson(Map<String, dynamic> j) => GrossPar3Skin(
    holeNumber: (j['holeNumber'] as num).toInt(),
    skinValue: (j['skinValue'] as num? ?? 0).toInt(),
    winnerPlayerId: (j['winnerPlayerId'] as num? ?? 0).toInt(),
    winnerPlayerName: j['winnerPlayerName'] as String? ?? '',
    winnerFlightName: j['winnerFlightName'] as String? ?? '',
    winningGrossScore: (j['winningGrossScore'] as num? ?? 0).toInt(),
    wasCarryover: j['wasCarryover'] as bool? ?? false,
  );
}

class GrossPar3SkinsSummary {
  const GrossPar3SkinsSummary({
    required this.totalHolesWithSkins,
    required this.totalSkinValueAwarded,
    required this.incomingCarryover,
    this.holeResults = const [],
    this.playerSummaries = const [],
  });

  final int totalHolesWithSkins;
  final int totalSkinValueAwarded;
  final int incomingCarryover;
  final List<GrossPar3Skin> holeResults;
  final List<PlayerSkinSummary> playerSummaries;

  factory GrossPar3SkinsSummary.fromJson(Map<String, dynamic> j) =>
      GrossPar3SkinsSummary(
        totalHolesWithSkins: (j['totalHolesWithSkins'] as num? ?? 0).toInt(),
        totalSkinValueAwarded: (j['totalSkinValueAwarded'] as num? ?? 0)
            .toInt(),
        incomingCarryover: (j['incomingCarryover'] as num? ?? 0).toInt(),
        holeResults: ((j['holeResults'] as List<dynamic>?) ?? [])
            .map((h) => GrossPar3Skin.fromJson(h as Map<String, dynamic>))
            .toList(),
        playerSummaries: ((j['playerSummaries'] as List<dynamic>?) ?? [])
            .map((p) => PlayerSkinSummary.fromJson(p as Map<String, dynamic>))
            .toList(),
      );
}

class RoundSkins {
  const RoundSkins({
    required this.roundId,
    this.flightSkins = const [],
    this.grossPar3Skins,
  });

  final int roundId;
  final List<FlightSkins> flightSkins;
  final GrossPar3SkinsSummary? grossPar3Skins;

  factory RoundSkins.fromJson(Map<String, dynamic> j) => RoundSkins(
    roundId: (j['roundId'] as num).toInt(),
    flightSkins: ((j['flightSkins'] as List<dynamic>?) ?? [])
        .map((f) => FlightSkins.fromJson(f as Map<String, dynamic>))
        .toList(),
    grossPar3Skins: j['grossPar3Skins'] != null
        ? GrossPar3SkinsSummary.fromJson(
            j['grossPar3Skins'] as Map<String, dynamic>,
          )
        : null,
  );
}

// ── Tournament Results ────────────────────────────────────────────────────────

class TournamentSkinHole {
  const TournamentSkinHole({
    required this.holeNumber,
    required this.par,
    required this.skinValue,
    this.winnerPlayerName,
    this.winningScore,
    required this.wasCarryover,
    required this.isTie,
  });

  final int holeNumber;
  final int par;
  final int skinValue;
  final String? winnerPlayerName;
  final int? winningScore;
  final bool wasCarryover;
  final bool isTie;

  factory TournamentSkinHole.fromJson(Map<String, dynamic> j) =>
      TournamentSkinHole(
        holeNumber: (j['holeNumber'] as num).toInt(),
        par: (j['par'] as num? ?? 0).toInt(),
        skinValue: (j['skinValue'] as num? ?? 0).toInt(),
        winnerPlayerName: j['winnerPlayerName'] as String?,
        winningScore: (j['winningScore'] as num?)?.toInt(),
        wasCarryover: j['wasCarryover'] as bool? ?? false,
        isTie: j['isTie'] as bool? ?? false,
      );
}

class TournamentPlayerSkin {
  const TournamentPlayerSkin({
    required this.playerId,
    required this.playerName,
    required this.totalSkinsWon,
    required this.totalSkinValue,
  });

  final int playerId;
  final String playerName;
  final int totalSkinsWon;
  final int totalSkinValue;

  factory TournamentPlayerSkin.fromJson(Map<String, dynamic> j) =>
      TournamentPlayerSkin(
        playerId: (j['playerId'] as num).toInt(),
        playerName: j['playerName'] as String? ?? '',
        totalSkinsWon: (j['totalSkinsWon'] as num? ?? 0).toInt(),
        totalSkinValue: (j['totalSkinValue'] as num? ?? 0).toInt(),
      );
}

class TournamentSkinsResult {
  const TournamentSkinsResult({
    required this.skinType,
    this.holeResults = const [],
    this.playerSummaries = const [],
  });

  final String skinType;
  final List<TournamentSkinHole> holeResults;
  final List<TournamentPlayerSkin> playerSummaries;

  factory TournamentSkinsResult.fromJson(Map<String, dynamic> j) =>
      TournamentSkinsResult(
        skinType: j['skinType'] as String? ?? '',
        holeResults: ((j['holeResults'] as List<dynamic>?) ?? [])
            .map((h) => TournamentSkinHole.fromJson(h as Map<String, dynamic>))
            .toList(),
        playerSummaries: ((j['playerSummaries'] as List<dynamic>?) ?? [])
            .map(
              (p) => TournamentPlayerSkin.fromJson(p as Map<String, dynamic>),
            )
            .toList(),
      );
}

class TournamentHoleExtra {
  const TournamentHoleExtra({
    required this.holeNumber,
    this.closestToPinPlayerId,
    this.closestToPinPlayerName,
  });

  final int holeNumber;
  final int? closestToPinPlayerId;
  final String? closestToPinPlayerName;

  factory TournamentHoleExtra.fromJson(Map<String, dynamic> j) =>
      TournamentHoleExtra(
        holeNumber: (j['holeNumber'] as num).toInt(),
        closestToPinPlayerId: (j['closestToPinPlayerId'] as num?)?.toInt(),
        closestToPinPlayerName: j['closestToPinPlayerName'] as String?,
      );
}

class LongestDriveWinner {
  const LongestDriveWinner({required this.playerId, required this.playerName});

  final int playerId;
  final String playerName;

  factory LongestDriveWinner.fromJson(Map<String, dynamic> j) =>
      LongestDriveWinner(
        playerId: (j['playerId'] as num).toInt(),
        playerName: j['playerName'] as String? ?? '',
      );
}

class TournamentMatchupResult {
  const TournamentMatchupResult({
    required this.matchupNumber,
    required this.player1Id,
    required this.player1Name,
    required this.player1HandicapIndex,
    required this.player1CourseHandicap,
    this.player1NetStrokes,
    required this.player2Id,
    required this.player2Name,
    required this.player2HandicapIndex,
    required this.player2CourseHandicap,
    this.player2NetStrokes,
    this.winnerPlayerId,
    required this.isHalved,
  });

  final int matchupNumber;
  final int player1Id;
  final String player1Name;
  final double player1HandicapIndex;
  final int player1CourseHandicap;
  final int? player1NetStrokes;
  final int player2Id;
  final String player2Name;
  final double player2HandicapIndex;
  final int player2CourseHandicap;
  final int? player2NetStrokes;
  final int? winnerPlayerId;
  final bool isHalved;

  factory TournamentMatchupResult.fromJson(Map<String, dynamic> j) =>
      TournamentMatchupResult(
        matchupNumber: (j['matchupNumber'] as num).toInt(),
        player1Id: (j['player1Id'] as num).toInt(),
        player1Name: j['player1Name'] as String? ?? '',
        player1HandicapIndex: (j['player1HandicapIndex'] as num? ?? 0)
            .toDouble(),
        player1CourseHandicap: (j['player1CourseHandicap'] as num? ?? 0)
            .toInt(),
        player1NetStrokes: (j['player1NetStrokes'] as num?)?.toInt(),
        player2Id: (j['player2Id'] as num).toInt(),
        player2Name: j['player2Name'] as String? ?? '',
        player2HandicapIndex: (j['player2HandicapIndex'] as num? ?? 0)
            .toDouble(),
        player2CourseHandicap: (j['player2CourseHandicap'] as num? ?? 0)
            .toInt(),
        player2NetStrokes: (j['player2NetStrokes'] as num?)?.toInt(),
        winnerPlayerId: (j['winnerPlayerId'] as num?)?.toInt(),
        isHalved: j['isHalved'] as bool? ?? false,
      );
}

class TournamentRankingEntry {
  const TournamentRankingEntry({
    required this.rank,
    required this.playerId,
    required this.playerName,
    required this.handicapIndex,
    this.score,
    required this.isTied,
  });

  final int rank;
  final int playerId;
  final String playerName;
  final double handicapIndex;
  final int? score;
  final bool isTied;

  factory TournamentRankingEntry.fromJson(Map<String, dynamic> j) =>
      TournamentRankingEntry(
        rank: (j['rank'] as num).toInt(),
        playerId: (j['playerId'] as num).toInt(),
        playerName: j['playerName'] as String? ?? '',
        handicapIndex: (j['handicapIndex'] as num? ?? 0).toDouble(),
        score: (j['score'] as num?)?.toInt(),
        isTied: j['isTied'] as bool? ?? false,
      );
}

class TournamentResults {
  const TournamentResults({
    required this.roundId,
    required this.roundDate,
    required this.courseName,
    required this.grossSkins,
    required this.netSkins,
    this.holeExtras = const [],
    this.longestDriveWinners = const [],
    this.matchupResults = const [],
    this.grossStrokeRanking = const [],
    this.netStrokeRanking = const [],
    this.grossStablefordRanking = const [],
    this.netStablefordRanking = const [],
  });

  final int roundId;
  final DateTime roundDate;
  final String courseName;
  final TournamentSkinsResult grossSkins;
  final TournamentSkinsResult netSkins;
  final List<TournamentHoleExtra> holeExtras;
  final List<LongestDriveWinner> longestDriveWinners;
  final List<TournamentMatchupResult> matchupResults;
  final List<TournamentRankingEntry> grossStrokeRanking;
  final List<TournamentRankingEntry> netStrokeRanking;
  final List<TournamentRankingEntry> grossStablefordRanking;
  final List<TournamentRankingEntry> netStablefordRanking;

  factory TournamentResults.fromJson(Map<String, dynamic> j) {
    List<TournamentRankingEntry> ranking(String key) =>
        ((j[key] as List<dynamic>?) ?? [])
            .map(
              (e) => TournamentRankingEntry.fromJson(e as Map<String, dynamic>),
            )
            .toList();
    return TournamentResults(
      roundId: (j['roundId'] as num).toInt(),
      roundDate: DateTime.parse(j['roundDate'] as String),
      courseName: j['courseName'] as String? ?? '',
      grossSkins: TournamentSkinsResult.fromJson(
        (j['grossSkins'] as Map<String, dynamic>?) ?? const {},
      ),
      netSkins: TournamentSkinsResult.fromJson(
        (j['netSkins'] as Map<String, dynamic>?) ?? const {},
      ),
      holeExtras: ((j['holeExtras'] as List<dynamic>?) ?? [])
          .map((e) => TournamentHoleExtra.fromJson(e as Map<String, dynamic>))
          .toList(),
      longestDriveWinners: ((j['longestDriveWinners'] as List<dynamic>?) ?? [])
          .map((e) => LongestDriveWinner.fromJson(e as Map<String, dynamic>))
          .toList(),
      matchupResults: ((j['matchupResults'] as List<dynamic>?) ?? [])
          .map(
            (e) => TournamentMatchupResult.fromJson(e as Map<String, dynamic>),
          )
          .toList(),
      grossStrokeRanking: ranking('grossStrokeRanking'),
      netStrokeRanking: ranking('netStrokeRanking'),
      grossStablefordRanking: ranking('grossStablefordRanking'),
      netStablefordRanking: ranking('netStablefordRanking'),
    );
  }
}

// ── League Leaderboards ───────────────────────────────────────────────────────

class LeaderboardScoreEntry {
  const LeaderboardScoreEntry({
    required this.playerId,
    required this.playerName,
    required this.average,
    required this.roundsPlayed,
  });

  final int playerId;
  final String playerName;
  final double average;
  final int roundsPlayed;

  factory LeaderboardScoreEntry.fromJson(
    Map<String, dynamic> j,
    String avgKey,
  ) => LeaderboardScoreEntry(
    playerId: (j['playerId'] as num).toInt(),
    playerName: j['playerName'] as String? ?? '',
    average: (j[avgKey] as num? ?? 0).toDouble(),
    roundsPlayed: (j['roundsPlayed'] as num? ?? 0).toInt(),
  );
}

class BirdiesEaglesEntry {
  const BirdiesEaglesEntry({
    required this.playerId,
    required this.playerName,
    required this.totalBirdies,
    required this.totalEaglesOrBetter,
    required this.total,
  });

  final int playerId;
  final String playerName;
  final int totalBirdies;
  final int totalEaglesOrBetter;
  final int total;

  factory BirdiesEaglesEntry.fromJson(Map<String, dynamic> j) =>
      BirdiesEaglesEntry(
        playerId: (j['playerId'] as num).toInt(),
        playerName: j['playerName'] as String? ?? '',
        totalBirdies: (j['totalBirdies'] as num? ?? 0).toInt(),
        totalEaglesOrBetter: (j['totalEaglesOrBetter'] as num? ?? 0).toInt(),
        total: (j['total'] as num? ?? 0).toInt(),
      );
}

class Par3SkinsEntry {
  const Par3SkinsEntry({
    required this.playerId,
    required this.playerName,
    required this.totalSkinsWon,
    required this.totalSkinValue,
  });

  final int playerId;
  final String playerName;
  final int totalSkinsWon;
  final int totalSkinValue;

  factory Par3SkinsEntry.fromJson(Map<String, dynamic> j) => Par3SkinsEntry(
    playerId: (j['playerId'] as num).toInt(),
    playerName: j['playerName'] as String? ?? '',
    totalSkinsWon: (j['totalSkinsWon'] as num? ?? 0).toInt(),
    totalSkinValue: (j['totalSkinValue'] as num? ?? 0).toInt(),
  );
}

class LeagueLeaderboards {
  const LeagueLeaderboards({
    this.lowGross = const [],
    this.lowNet = const [],
    this.birdiesEagles = const [],
    this.par3Skins = const [],
  });

  final List<LeaderboardScoreEntry> lowGross;
  final List<LeaderboardScoreEntry> lowNet;
  final List<BirdiesEaglesEntry> birdiesEagles;
  final List<Par3SkinsEntry> par3Skins;

  factory LeagueLeaderboards.fromJson(Map<String, dynamic> j) =>
      LeagueLeaderboards(
        lowGross: ((j['lowGross'] as List<dynamic>?) ?? [])
            .map(
              (e) => LeaderboardScoreEntry.fromJson(
                e as Map<String, dynamic>,
                'averageGrossScore',
              ),
            )
            .toList(),
        lowNet: ((j['lowNet'] as List<dynamic>?) ?? [])
            .map(
              (e) => LeaderboardScoreEntry.fromJson(
                e as Map<String, dynamic>,
                'averageNetScore',
              ),
            )
            .toList(),
        birdiesEagles: ((j['birdiesEagles'] as List<dynamic>?) ?? [])
            .map((e) => BirdiesEaglesEntry.fromJson(e as Map<String, dynamic>))
            .toList(),
        par3Skins: ((j['par3Skins'] as List<dynamic>?) ?? [])
            .map((e) => Par3SkinsEntry.fromJson(e as Map<String, dynamic>))
            .toList(),
      );
}

// ── Player Statistics ─────────────────────────────────────────────────────────

class ScoringDistribution {
  const ScoringDistribution({
    required this.eagleOrBetterCount,
    required this.birdieCount,
    required this.parCount,
    required this.bogeyCount,
    required this.doubleBogeyOrWorseCount,
    required this.totalHolesPlayed,
  });

  final int eagleOrBetterCount;
  final int birdieCount;
  final int parCount;
  final int bogeyCount;
  final int doubleBogeyOrWorseCount;
  final int totalHolesPlayed;

  factory ScoringDistribution.fromJson(Map<String, dynamic> j) =>
      ScoringDistribution(
        eagleOrBetterCount: (j['eagleOrBetterCount'] as num? ?? 0).toInt(),
        birdieCount: (j['birdieCount'] as num? ?? 0).toInt(),
        parCount: (j['parCount'] as num? ?? 0).toInt(),
        bogeyCount: (j['bogeyCount'] as num? ?? 0).toInt(),
        doubleBogeyOrWorseCount: (j['doubleBogeyOrWorseCount'] as num? ?? 0)
            .toInt(),
        totalHolesPlayed: (j['totalHolesPlayed'] as num? ?? 0).toInt(),
      );
}

class BestWorstRound {
  const BestWorstRound({
    required this.roundId,
    required this.roundDate,
    required this.courseName,
    this.grossStrokes,
    this.netStablefordPoints,
  });

  final int roundId;
  final DateTime roundDate;
  final String courseName;
  final int? grossStrokes;
  final int? netStablefordPoints;

  factory BestWorstRound.fromJson(Map<String, dynamic> j) => BestWorstRound(
    roundId: (j['roundId'] as num).toInt(),
    roundDate: DateTime.parse(j['roundDate'] as String),
    courseName: j['courseName'] as String? ?? '',
    grossStrokes: (j['grossStrokes'] as num?)?.toInt(),
    netStablefordPoints: (j['netStablefordPoints'] as num?)?.toInt(),
  );
}

class StrokesGainedPutting {
  const StrokesGainedPutting({
    required this.totalStrokesGained,
    required this.perHoleAverage,
    required this.holesWithPuttData,
    this.averagePuttsPerHole,
    this.flightAveragePuttsPerHole,
  });

  final double totalStrokesGained;
  final double perHoleAverage;
  final int holesWithPuttData;
  final double? averagePuttsPerHole;
  final double? flightAveragePuttsPerHole;

  factory StrokesGainedPutting.fromJson(Map<String, dynamic> j) =>
      StrokesGainedPutting(
        totalStrokesGained: (j['totalStrokesGained'] as num? ?? 0).toDouble(),
        perHoleAverage: (j['perHoleAverage'] as num? ?? 0).toDouble(),
        holesWithPuttData: (j['holesWithPuttData'] as num? ?? 0).toInt(),
        averagePuttsPerHole: (j['averagePuttsPerHole'] as num?)?.toDouble(),
        flightAveragePuttsPerHole: (j['flightAveragePuttsPerHole'] as num?)
            ?.toDouble(),
      );
}

class PlayerStatistics {
  const PlayerStatistics({
    required this.playerId,
    required this.playerName,
    required this.totalRoundsPlayed,
    this.averageGrossStrokes,
    this.averageNetStrokes,
    this.averageNetStablefordPoints,
    this.averageScoreToPar,
    this.bestGrossStrokes,
    this.bestNetStablefordPoints,
    this.bestGrossRound,
    this.bestNetPointsRound,
    required this.scoringDistribution,
    this.handicapTrend,
    required this.totalBirdiesOrBetter,
    required this.totalPars,
    this.parOrBetterPercentage,
    this.strokesGainedPutting,
  });

  final int playerId;
  final String playerName;
  final int totalRoundsPlayed;
  final double? averageGrossStrokes;
  final double? averageNetStrokes;
  final double? averageNetStablefordPoints;
  final double? averageScoreToPar;
  final int? bestGrossStrokes;
  final int? bestNetStablefordPoints;
  final BestWorstRound? bestGrossRound;
  final BestWorstRound? bestNetPointsRound;
  final ScoringDistribution scoringDistribution;
  final double? handicapTrend;
  final int totalBirdiesOrBetter;
  final int totalPars;
  final double? parOrBetterPercentage;
  final StrokesGainedPutting? strokesGainedPutting;

  factory PlayerStatistics.fromJson(Map<String, dynamic> j) => PlayerStatistics(
    playerId: (j['playerId'] as num).toInt(),
    playerName: j['playerName'] as String? ?? '',
    totalRoundsPlayed: (j['totalRoundsPlayed'] as num? ?? 0).toInt(),
    averageGrossStrokes: (j['averageGrossStrokes'] as num?)?.toDouble(),
    averageNetStrokes: (j['averageNetStrokes'] as num?)?.toDouble(),
    averageNetStablefordPoints: (j['averageNetStablefordPoints'] as num?)
        ?.toDouble(),
    averageScoreToPar: (j['averageScoreToPar'] as num?)?.toDouble(),
    bestGrossStrokes: (j['bestGrossStrokes'] as num?)?.toInt(),
    bestNetStablefordPoints: (j['bestNetStablefordPoints'] as num?)?.toInt(),
    bestGrossRound: j['bestGrossRound'] != null
        ? BestWorstRound.fromJson(j['bestGrossRound'] as Map<String, dynamic>)
        : null,
    bestNetPointsRound: j['bestNetPointsRound'] != null
        ? BestWorstRound.fromJson(
            j['bestNetPointsRound'] as Map<String, dynamic>,
          )
        : null,
    scoringDistribution: ScoringDistribution.fromJson(
      (j['scoringDistribution'] as Map<String, dynamic>?) ?? const {},
    ),
    handicapTrend: (j['handicapTrend'] as num?)?.toDouble(),
    totalBirdiesOrBetter: (j['totalBirdiesOrBetter'] as num? ?? 0).toInt(),
    totalPars: (j['totalPars'] as num? ?? 0).toInt(),
    parOrBetterPercentage: (j['parOrBetterPercentage'] as num?)?.toDouble(),
    strokesGainedPutting: j['strokesGainedPutting'] != null
        ? StrokesGainedPutting.fromJson(
            j['strokesGainedPutting'] as Map<String, dynamic>,
          )
        : null,
  );
}

// ── Admin: round participants & course detail ────────────────────────────────

class RoundParticipant {
  const RoundParticipant({
    required this.id,
    required this.roundId,
    required this.playerId,
    required this.playerName,
    required this.flightId,
    required this.handicapAtTime,
    required this.courseHandicap,
    required this.isWithdrawn,
    required this.skippedWeek,
  });

  final int id;
  final int roundId;
  final int playerId;
  final String playerName;
  final int flightId;
  final double handicapAtTime;
  final int courseHandicap;
  final bool isWithdrawn;
  final bool skippedWeek;

  factory RoundParticipant.fromJson(Map<String, dynamic> j) =>
      RoundParticipant(
        id: (j['id'] as num).toInt(),
        roundId: (j['roundId'] as num? ?? 0).toInt(),
        playerId: (j['playerId'] as num).toInt(),
        playerName: j['playerName'] as String? ?? '',
        flightId: (j['flightId'] as num? ?? 0).toInt(),
        handicapAtTime: (j['handicapAtTime'] as num? ?? 0).toDouble(),
        courseHandicap: (j['courseHandicap'] as num? ?? 0).toInt(),
        isWithdrawn: j['isWithdrawn'] as bool? ?? false,
        skippedWeek: j['skippedWeek'] as bool? ?? false,
      );
}

class AdminTeeTimeParticipant {
  const AdminTeeTimeParticipant({
    required this.id,
    required this.playerId,
    required this.fullName,
    this.teeTimeId,
    this.teeTimeNumber,
    required this.skippedWeek,
  });

  final int id;
  final int playerId;
  final String fullName;
  final int? teeTimeId;
  final int? teeTimeNumber;
  final bool skippedWeek;

  factory AdminTeeTimeParticipant.fromJson(Map<String, dynamic> j) =>
      AdminTeeTimeParticipant(
        id: (j['id'] as num).toInt(),
        playerId: (j['playerId'] as num).toInt(),
        fullName: j['fullName'] as String? ?? '',
        teeTimeId: (j['teeTimeId'] as num?)?.toInt(),
        teeTimeNumber: (j['teeTimeNumber'] as num?)?.toInt(),
        skippedWeek: j['skippedWeek'] as bool? ?? false,
      );
}

class CourseDetail {
  const CourseDetail({
    required this.id,
    required this.name,
    this.holeDetails = const [],
  });

  final int id;
  final String name;
  final List<CourseHole> holeDetails;

  factory CourseDetail.fromJson(Map<String, dynamic> j) => CourseDetail(
    id: (j['id'] as num).toInt(),
    name: j['name'] as String? ?? '',
    holeDetails: ((j['holeDetails'] as List<dynamic>?) ?? [])
        .map((h) => CourseHole.fromJson(h as Map<String, dynamic>))
        .toList(),
  );
}
