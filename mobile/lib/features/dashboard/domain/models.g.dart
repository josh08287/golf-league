// GENERATED CODE - DO NOT MODIFY BY HAND

part of 'models.dart';

// **************************************************************************
// JsonSerializableGenerator
// **************************************************************************

_$DashboardDataImpl _$$DashboardDataImplFromJson(Map<String, dynamic> json) =>
    _$DashboardDataImpl(
      flightSummaries: (json['flightSummaries'] as List<dynamic>?)
              ?.map((e) => FlightSummary.fromJson(e as Map<String, dynamic>))
              .toList() ??
          const [],
      latestRound: json['latestRound'] == null
          ? null
          : LatestRoundSummary.fromJson(
              json['latestRound'] as Map<String, dynamic>),
    );

Map<String, dynamic> _$$DashboardDataImplToJson(_$DashboardDataImpl instance) =>
    <String, dynamic>{
      'flightSummaries': instance.flightSummaries,
      'latestRound': instance.latestRound,
    };

_$FlightSummaryImpl _$$FlightSummaryImplFromJson(Map<String, dynamic> json) =>
    _$FlightSummaryImpl(
      flightId: (json['flightId'] as num).toInt(),
      flightName: json['flightName'] as String,
      topThree: (json['topThree'] as List<dynamic>?)
              ?.map((e) => FlightTopEntry.fromJson(e as Map<String, dynamic>))
              .toList() ??
          const [],
    );

Map<String, dynamic> _$$FlightSummaryImplToJson(_$FlightSummaryImpl instance) =>
    <String, dynamic>{
      'flightId': instance.flightId,
      'flightName': instance.flightName,
      'topThree': instance.topThree,
    };

_$FlightTopEntryImpl _$$FlightTopEntryImplFromJson(Map<String, dynamic> json) =>
    _$FlightTopEntryImpl(
      rank: (json['rank'] as num).toInt(),
      playerId: (json['playerId'] as num).toInt(),
      playerName: json['playerName'] as String,
      totalPoints: (json['totalPoints'] as num).toInt(),
      handicap: (json['handicap'] as num).toDouble(),
    );

Map<String, dynamic> _$$FlightTopEntryImplToJson(
        _$FlightTopEntryImpl instance) =>
    <String, dynamic>{
      'rank': instance.rank,
      'playerId': instance.playerId,
      'playerName': instance.playerName,
      'totalPoints': instance.totalPoints,
      'handicap': instance.handicap,
    };

_$LatestRoundSummaryImpl _$$LatestRoundSummaryImplFromJson(
        Map<String, dynamic> json) =>
    _$LatestRoundSummaryImpl(
      roundId: (json['roundId'] as num).toInt(),
      courseName: json['courseName'] as String,
      playedDate: DateTime.parse(json['playedDate'] as String),
      status: json['status'] as String,
      roundWinnerPlayerId: (json['roundWinnerPlayerId'] as num?)?.toInt(),
      roundWinnerName: json['roundWinnerName'] as String?,
      roundWinnerPoints: (json['roundWinnerPoints'] as num?)?.toInt(),
    );

Map<String, dynamic> _$$LatestRoundSummaryImplToJson(
        _$LatestRoundSummaryImpl instance) =>
    <String, dynamic>{
      'roundId': instance.roundId,
      'courseName': instance.courseName,
      'playedDate': instance.playedDate.toIso8601String(),
      'status': instance.status,
      'roundWinnerPlayerId': instance.roundWinnerPlayerId,
      'roundWinnerName': instance.roundWinnerName,
      'roundWinnerPoints': instance.roundWinnerPoints,
    };
