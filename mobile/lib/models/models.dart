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

enum RoundStatus { scheduled, inProgress, pendingFinalization, finalized, cancelled }

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
    case 'inprogress': return RoundStatus.inProgress;
    case 'pendingfinalization': return RoundStatus.pendingFinalization;
    case 'finalized': return RoundStatus.finalized;
    case 'cancelled': return RoundStatus.cancelled;
    default: return RoundStatus.scheduled;
  }
}

extension RoundStatusLabel on RoundStatus {
  String get label {
    switch (this) {
      case RoundStatus.scheduled: return 'Scheduled';
      case RoundStatus.inProgress: return 'In Progress';
      case RoundStatus.pendingFinalization: return 'Pending';
      case RoundStatus.finalized: return 'Finalized';
      case RoundStatus.cancelled: return 'Cancelled';
    }
  }
}

class Round {
  const Round({
    required this.id,
    required this.courseId,
    required this.courseName,
    required this.flightId,
    required this.flightName,
    required this.scheduledDate,
    required this.status,
    required this.participantCount,
  });

  final int id;
  final int courseId;
  final String courseName;
  final int flightId;
  final String flightName;
  final DateTime scheduledDate;
  final RoundStatus status;
  final int participantCount;

  factory Round.fromJson(Map<String, dynamic> j) => Round(
        id: (j['id'] as num).toInt(),
        courseId: (j['courseId'] as num).toInt(),
        courseName: j['courseName'] as String? ?? '',
        flightId: (j['flightId'] as num).toInt(),
        flightName: j['flightName'] as String? ?? '',
        scheduledDate: DateTime.parse(j['scheduledDate'] as String),
        status: parseRoundStatus(j['status']),
        participantCount: (j['participantCount'] as num? ?? 0).toInt(),
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
