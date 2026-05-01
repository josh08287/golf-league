import 'package:freezed_annotation/freezed_annotation.dart';

part 'models.freezed.dart';
part 'models.g.dart';

@freezed
class Round with _$Round {
  const factory Round({
    required int id,
    required String courseName,
    required DateTime scheduledDate,
    DateTime? playedDate,
    required String status,
    required int roundNumber,
    String? weatherConditions,
    int? flightId,
    String? flightName,
  }) = _Round;

  factory Round.fromJson(Map<String, dynamic> json) =>
      _$RoundFromJson(json);
}

@freezed
class RoundDetail with _$RoundDetail {
  const factory RoundDetail({
    required int id,
    required String courseName,
    required DateTime scheduledDate,
    DateTime? playedDate,
    required String status,
    required int roundNumber,
    String? weatherConditions,
    @Default([]) List<RoundParticipantSummary> participants,
  }) = _RoundDetail;

  factory RoundDetail.fromJson(Map<String, dynamic> json) =>
      _$RoundDetailFromJson(json);
}

@freezed
class RoundParticipantSummary with _$RoundParticipantSummary {
  const factory RoundParticipantSummary({
    required int playerId,
    required String playerName,
    int? totalStablefordPoints,
    int? grossTotal,
    int? rank,
    double? courseHandicap,
  }) = _RoundParticipantSummary;

  factory RoundParticipantSummary.fromJson(Map<String, dynamic> json) =>
      _$RoundParticipantSummaryFromJson(json);
}

@freezed
class HoleScore with _$HoleScore {
  const factory HoleScore({
    required int holeNumber,
    required int par,
    required int strokeIndex,
    int? grossStrokes,
    required int handicapStrokes,
    int? netStrokes,
    int? stablefordPoints,
    @Default(false) bool isMaxScore,
  }) = _HoleScore;

  factory HoleScore.fromJson(Map<String, dynamic> json) =>
      _$HoleScoreFromJson(json);
}

@freezed
class PlayerScorecard with _$PlayerScorecard {
  const factory PlayerScorecard({
    required int roundId,
    required int playerId,
    required String playerName,
    required double courseHandicap,
    @Default([]) List<HoleScore> holes,
    int? totalGross,
    int? totalNet,
    int? totalStableford,
  }) = _PlayerScorecard;

  factory PlayerScorecard.fromJson(Map<String, dynamic> json) =>
      _$PlayerScorecardFromJson(json);
}
