import 'package:freezed_annotation/freezed_annotation.dart';

part 'models.freezed.dart';
part 'models.g.dart';

@freezed
class ScoreEntryHole with _$ScoreEntryHole {
  const factory ScoreEntryHole({
    required int holeNumber,
    required int par,
    required int strokeIndex,
    required int strokesReceived,
    int? grossStrokes,
    int? stablefordPoints,
  }) = _ScoreEntryHole;

  factory ScoreEntryHole.fromJson(Map<String, dynamic> json) =>
      _$ScoreEntryHoleFromJson(json);
}

@freezed
class ScoreSubmission with _$ScoreSubmission {
  const factory ScoreSubmission({
    required int playerId,
    required List<HoleSubmission> holes,
  }) = _ScoreSubmission;

  factory ScoreSubmission.fromJson(Map<String, dynamic> json) =>
      _$ScoreSubmissionFromJson(json);
}

@freezed
class HoleSubmission with _$HoleSubmission {
  const factory HoleSubmission({
    required int holeNumber,
    required int grossStrokes,
  }) = _HoleSubmission;

  factory HoleSubmission.fromJson(Map<String, dynamic> json) =>
      _$HoleSubmissionFromJson(json);
}
