// GENERATED CODE - DO NOT MODIFY BY HAND

part of 'models.dart';

// **************************************************************************
// JsonSerializableGenerator
// **************************************************************************

_$RoundImpl _$$RoundImplFromJson(Map<String, dynamic> json) => _$RoundImpl(
      id: (json['id'] as num).toInt(),
      courseName: json['courseName'] as String,
      scheduledDate: DateTime.parse(json['scheduledDate'] as String),
      playedDate: json['playedDate'] == null
          ? null
          : DateTime.parse(json['playedDate'] as String),
      status: json['status'] as String,
      roundNumber: (json['roundNumber'] as num).toInt(),
      weatherConditions: json['weatherConditions'] as String?,
      flightId: (json['flightId'] as num?)?.toInt(),
      flightName: json['flightName'] as String?,
    );

Map<String, dynamic> _$$RoundImplToJson(_$RoundImpl instance) =>
    <String, dynamic>{
      'id': instance.id,
      'courseName': instance.courseName,
      'scheduledDate': instance.scheduledDate.toIso8601String(),
      'playedDate': instance.playedDate?.toIso8601String(),
      'status': instance.status,
      'roundNumber': instance.roundNumber,
      'weatherConditions': instance.weatherConditions,
      'flightId': instance.flightId,
      'flightName': instance.flightName,
    };

_$RoundDetailImpl _$$RoundDetailImplFromJson(Map<String, dynamic> json) =>
    _$RoundDetailImpl(
      id: (json['id'] as num).toInt(),
      courseName: json['courseName'] as String,
      scheduledDate: DateTime.parse(json['scheduledDate'] as String),
      playedDate: json['playedDate'] == null
          ? null
          : DateTime.parse(json['playedDate'] as String),
      status: json['status'] as String,
      roundNumber: (json['roundNumber'] as num).toInt(),
      weatherConditions: json['weatherConditions'] as String?,
      participants: (json['participants'] as List<dynamic>?)
              ?.map((e) =>
                  RoundParticipantSummary.fromJson(e as Map<String, dynamic>))
              .toList() ??
          const [],
    );

Map<String, dynamic> _$$RoundDetailImplToJson(_$RoundDetailImpl instance) =>
    <String, dynamic>{
      'id': instance.id,
      'courseName': instance.courseName,
      'scheduledDate': instance.scheduledDate.toIso8601String(),
      'playedDate': instance.playedDate?.toIso8601String(),
      'status': instance.status,
      'roundNumber': instance.roundNumber,
      'weatherConditions': instance.weatherConditions,
      'participants': instance.participants,
    };

_$RoundParticipantSummaryImpl _$$RoundParticipantSummaryImplFromJson(
        Map<String, dynamic> json) =>
    _$RoundParticipantSummaryImpl(
      playerId: (json['playerId'] as num).toInt(),
      playerName: json['playerName'] as String,
      totalStablefordPoints: (json['totalStablefordPoints'] as num?)?.toInt(),
      grossTotal: (json['grossTotal'] as num?)?.toInt(),
      rank: (json['rank'] as num?)?.toInt(),
      courseHandicap: (json['courseHandicap'] as num?)?.toDouble(),
    );

Map<String, dynamic> _$$RoundParticipantSummaryImplToJson(
        _$RoundParticipantSummaryImpl instance) =>
    <String, dynamic>{
      'playerId': instance.playerId,
      'playerName': instance.playerName,
      'totalStablefordPoints': instance.totalStablefordPoints,
      'grossTotal': instance.grossTotal,
      'rank': instance.rank,
      'courseHandicap': instance.courseHandicap,
    };

_$HoleScoreImpl _$$HoleScoreImplFromJson(Map<String, dynamic> json) =>
    _$HoleScoreImpl(
      holeNumber: (json['holeNumber'] as num).toInt(),
      par: (json['par'] as num).toInt(),
      strokeIndex: (json['strokeIndex'] as num).toInt(),
      grossStrokes: (json['grossStrokes'] as num?)?.toInt(),
      handicapStrokes: (json['handicapStrokes'] as num).toInt(),
      netStrokes: (json['netStrokes'] as num?)?.toInt(),
      stablefordPoints: (json['stablefordPoints'] as num?)?.toInt(),
      isMaxScore: json['isMaxScore'] as bool? ?? false,
    );

Map<String, dynamic> _$$HoleScoreImplToJson(_$HoleScoreImpl instance) =>
    <String, dynamic>{
      'holeNumber': instance.holeNumber,
      'par': instance.par,
      'strokeIndex': instance.strokeIndex,
      'grossStrokes': instance.grossStrokes,
      'handicapStrokes': instance.handicapStrokes,
      'netStrokes': instance.netStrokes,
      'stablefordPoints': instance.stablefordPoints,
      'isMaxScore': instance.isMaxScore,
    };

_$PlayerScorecardImpl _$$PlayerScorecardImplFromJson(
        Map<String, dynamic> json) =>
    _$PlayerScorecardImpl(
      roundId: (json['roundId'] as num).toInt(),
      playerId: (json['playerId'] as num).toInt(),
      playerName: json['playerName'] as String,
      courseHandicap: (json['courseHandicap'] as num).toDouble(),
      holes: (json['holes'] as List<dynamic>?)
              ?.map((e) => HoleScore.fromJson(e as Map<String, dynamic>))
              .toList() ??
          const [],
      totalGross: (json['totalGross'] as num?)?.toInt(),
      totalNet: (json['totalNet'] as num?)?.toInt(),
      totalStableford: (json['totalStableford'] as num?)?.toInt(),
    );

Map<String, dynamic> _$$PlayerScorecardImplToJson(
        _$PlayerScorecardImpl instance) =>
    <String, dynamic>{
      'roundId': instance.roundId,
      'playerId': instance.playerId,
      'playerName': instance.playerName,
      'courseHandicap': instance.courseHandicap,
      'holes': instance.holes,
      'totalGross': instance.totalGross,
      'totalNet': instance.totalNet,
      'totalStableford': instance.totalStableford,
    };
