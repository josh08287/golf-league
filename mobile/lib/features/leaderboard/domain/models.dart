import 'package:freezed_annotation/freezed_annotation.dart';

part 'models.freezed.dart';
part 'models.g.dart';

@freezed
class Flight with _$Flight {
  const factory Flight({
    required int id,
    required String name,
    String? description,
    @Default(0) int displayOrder,
    double? minHandicap,
    double? maxHandicap,
  }) = _Flight;

  factory Flight.fromJson(Map<String, dynamic> json) =>
      _$FlightFromJson(json);
}

@freezed
class LeaderboardEntry with _$LeaderboardEntry {
  const factory LeaderboardEntry({
    required int playerId,
    required String playerName,
    required int totalStablefordPoints,
    required int roundsPlayed,
    required int currentRank,
    int? previousRank,
    required double currentHandicap,
    double? averagePoints,
    int? lastRoundPoints,
  }) = _LeaderboardEntry;

  factory LeaderboardEntry.fromJson(Map<String, dynamic> json) =>
      _$LeaderboardEntryFromJson(json);
}
