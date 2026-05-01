// GENERATED CODE - DO NOT MODIFY BY HAND

part of 'models.dart';

// **************************************************************************
// JsonSerializableGenerator
// **************************************************************************

_$PlayerProfileImpl _$$PlayerProfileImplFromJson(Map<String, dynamic> json) =>
    _$PlayerProfileImpl(
      id: (json['id'] as num).toInt(),
      firstName: json['firstName'] as String,
      lastName: json['lastName'] as String,
      email: json['email'] as String?,
      currentHandicap: (json['currentHandicap'] as num).toDouble(),
      roundsPlayed: (json['roundsPlayed'] as num).toInt(),
      totalStablefordPoints: (json['totalStablefordPoints'] as num).toInt(),
      averagePoints: (json['averagePoints'] as num?)?.toDouble(),
      bestRoundPoints: (json['bestRoundPoints'] as num?)?.toInt(),
      flightName: json['flightName'] as String?,
      isActive: json['isActive'] as bool?,
    );

Map<String, dynamic> _$$PlayerProfileImplToJson(_$PlayerProfileImpl instance) =>
    <String, dynamic>{
      'id': instance.id,
      'firstName': instance.firstName,
      'lastName': instance.lastName,
      'email': instance.email,
      'currentHandicap': instance.currentHandicap,
      'roundsPlayed': instance.roundsPlayed,
      'totalStablefordPoints': instance.totalStablefordPoints,
      'averagePoints': instance.averagePoints,
      'bestRoundPoints': instance.bestRoundPoints,
      'flightName': instance.flightName,
      'isActive': instance.isActive,
    };

_$HandicapHistoryEntryImpl _$$HandicapHistoryEntryImplFromJson(
        Map<String, dynamic> json) =>
    _$HandicapHistoryEntryImpl(
      id: (json['id'] as num).toInt(),
      handicapIndex: (json['handicapIndex'] as num).toDouble(),
      effectiveDate: DateTime.parse(json['effectiveDate'] as String),
      source: json['source'] as String,
      notes: json['notes'] as String?,
      previousHandicap: (json['previousHandicap'] as num?)?.toDouble(),
    );

Map<String, dynamic> _$$HandicapHistoryEntryImplToJson(
        _$HandicapHistoryEntryImpl instance) =>
    <String, dynamic>{
      'id': instance.id,
      'handicapIndex': instance.handicapIndex,
      'effectiveDate': instance.effectiveDate.toIso8601String(),
      'source': instance.source,
      'notes': instance.notes,
      'previousHandicap': instance.previousHandicap,
    };
