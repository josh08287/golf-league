import 'package:freezed_annotation/freezed_annotation.dart';

part 'models.freezed.dart';
part 'models.g.dart';

@freezed
class DashboardData with _$DashboardData {
  const factory DashboardData({
    @Default([]) List<FlightSummary> flightSummaries,
    LatestRoundSummary? latestRound,
  }) = _DashboardData;

  factory DashboardData.fromJson(Map<String, dynamic> json) =>
      _$DashboardDataFromJson(json);
}

@freezed
class FlightSummary with _$FlightSummary {
  const factory FlightSummary({
    required int flightId,
    required String flightName,
    @Default([]) List<FlightTopEntry> topThree,
  }) = _FlightSummary;

  factory FlightSummary.fromJson(Map<String, dynamic> json) =>
      _$FlightSummaryFromJson(json);
}

@freezed
class FlightTopEntry with _$FlightTopEntry {
  const factory FlightTopEntry({
    required int rank,
    required int playerId,
    required String playerName,
    required int totalPoints,
    required double handicap,
  }) = _FlightTopEntry;

  factory FlightTopEntry.fromJson(Map<String, dynamic> json) =>
      _$FlightTopEntryFromJson(json);
}

@freezed
class LatestRoundSummary with _$LatestRoundSummary {
  const factory LatestRoundSummary({
    required int roundId,
    required String courseName,
    required DateTime playedDate,
    required String status,
    int? roundWinnerPlayerId,
    String? roundWinnerName,
    int? roundWinnerPoints,
  }) = _LatestRoundSummary;

  factory LatestRoundSummary.fromJson(Map<String, dynamic> json) =>
      _$LatestRoundSummaryFromJson(json);
}
