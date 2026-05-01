// GENERATED CODE - DO NOT MODIFY BY HAND

part of 'models.dart';

// **************************************************************************
// JsonSerializableGenerator
// **************************************************************************

_$ScoreEntryHoleImpl _$$ScoreEntryHoleImplFromJson(Map<String, dynamic> json) =>
    _$ScoreEntryHoleImpl(
      holeNumber: (json['holeNumber'] as num).toInt(),
      par: (json['par'] as num).toInt(),
      strokeIndex: (json['strokeIndex'] as num).toInt(),
      strokesReceived: (json['strokesReceived'] as num).toInt(),
      grossStrokes: (json['grossStrokes'] as num?)?.toInt(),
      stablefordPoints: (json['stablefordPoints'] as num?)?.toInt(),
    );

Map<String, dynamic> _$$ScoreEntryHoleImplToJson(
        _$ScoreEntryHoleImpl instance) =>
    <String, dynamic>{
      'holeNumber': instance.holeNumber,
      'par': instance.par,
      'strokeIndex': instance.strokeIndex,
      'strokesReceived': instance.strokesReceived,
      'grossStrokes': instance.grossStrokes,
      'stablefordPoints': instance.stablefordPoints,
    };

_$ScoreSubmissionImpl _$$ScoreSubmissionImplFromJson(
        Map<String, dynamic> json) =>
    _$ScoreSubmissionImpl(
      playerId: (json['playerId'] as num).toInt(),
      holes: (json['holes'] as List<dynamic>)
          .map((e) => HoleSubmission.fromJson(e as Map<String, dynamic>))
          .toList(),
    );

Map<String, dynamic> _$$ScoreSubmissionImplToJson(
        _$ScoreSubmissionImpl instance) =>
    <String, dynamic>{
      'playerId': instance.playerId,
      'holes': instance.holes,
    };

_$HoleSubmissionImpl _$$HoleSubmissionImplFromJson(Map<String, dynamic> json) =>
    _$HoleSubmissionImpl(
      holeNumber: (json['holeNumber'] as num).toInt(),
      grossStrokes: (json['grossStrokes'] as num).toInt(),
    );

Map<String, dynamic> _$$HoleSubmissionImplToJson(
        _$HoleSubmissionImpl instance) =>
    <String, dynamic>{
      'holeNumber': instance.holeNumber,
      'grossStrokes': instance.grossStrokes,
    };
