import 'package:freezed_annotation/freezed_annotation.dart';

part 'models.freezed.dart';
part 'models.g.dart';

@freezed
class PlayerProfile with _$PlayerProfile {
  const factory PlayerProfile({
    required int id,
    required String firstName,
    required String lastName,
    String? email,
    required double currentHandicap,
    required int roundsPlayed,
    required int totalStablefordPoints,
    double? averagePoints,
    int? bestRoundPoints,
    String? flightName,
    bool? isActive,
  }) = _PlayerProfile;

  factory PlayerProfile.fromJson(Map<String, dynamic> json) =>
      _$PlayerProfileFromJson(json);

  const PlayerProfile._();

  String get fullName => '$firstName $lastName';

  String get initials {
    final f = firstName.isNotEmpty ? firstName[0].toUpperCase() : '';
    final l = lastName.isNotEmpty ? lastName[0].toUpperCase() : '';
    return '$f$l';
  }
}

@freezed
class HandicapHistoryEntry with _$HandicapHistoryEntry {
  const factory HandicapHistoryEntry({
    required int id,
    required double handicapIndex,
    required DateTime effectiveDate,
    required String source,
    String? notes,
    double? previousHandicap,
  }) = _HandicapHistoryEntry;

  factory HandicapHistoryEntry.fromJson(Map<String, dynamic> json) =>
      _$HandicapHistoryEntryFromJson(json);
}
