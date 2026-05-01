// GENERATED CODE - DO NOT MODIFY BY HAND

part of 'models.dart';

// **************************************************************************
// JsonSerializableGenerator
// **************************************************************************

_$FlightImpl _$$FlightImplFromJson(Map<String, dynamic> json) => _$FlightImpl(
      id: (json['id'] as num).toInt(),
      name: json['name'] as String,
      description: json['description'] as String?,
      displayOrder: (json['displayOrder'] as num?)?.toInt() ?? 0,
      minHandicap: (json['minHandicap'] as num?)?.toDouble(),
      maxHandicap: (json['maxHandicap'] as num?)?.toDouble(),
    );

Map<String, dynamic> _$$FlightImplToJson(_$FlightImpl instance) =>
    <String, dynamic>{
      'id': instance.id,
      'name': instance.name,
      'description': instance.description,
      'displayOrder': instance.displayOrder,
      'minHandicap': instance.minHandicap,
      'maxHandicap': instance.maxHandicap,
    };

_$LeaderboardEntryImpl _$$LeaderboardEntryImplFromJson(
        Map<String, dynamic> json) =>
    _$LeaderboardEntryImpl(
      playerId: (json['playerId'] as num).toInt(),
      playerName: json['playerName'] as String,
      totalStablefordPoints: (json['totalStablefordPoints'] as num).toInt(),
      roundsPlayed: (json['roundsPlayed'] as num).toInt(),
      currentRank: (json['currentRank'] as num).toInt(),
      previousRank: (json['previousRank'] as num?)?.toInt(),
      currentHandicap: (json['currentHandicap'] as num).toDouble(),
      averagePoints: (json['averagePoints'] as num?)?.toDouble(),
      lastRoundPoints: (json['lastRoundPoints'] as num?)?.toInt(),
    );

Map<String, dynamic> _$$LeaderboardEntryImplToJson(
        _$LeaderboardEntryImpl instance) =>
    <String, dynamic>{
      'playerId': instance.playerId,
      'playerName': instance.playerName,
      'totalStablefordPoints': instance.totalStablefordPoints,
      'roundsPlayed': instance.roundsPlayed,
      'currentRank': instance.currentRank,
      'previousRank': instance.previousRank,
      'currentHandicap': instance.currentHandicap,
      'averagePoints': instance.averagePoints,
      'lastRoundPoints': instance.lastRoundPoints,
    };
