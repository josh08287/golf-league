// coverage:ignore-file
// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint
// ignore_for_file: unused_element, deprecated_member_use, deprecated_member_use_from_same_package, use_function_type_syntax_for_parameters, unnecessary_const, avoid_init_to_null, invalid_override_different_default_values_named, prefer_expression_function_bodies, annotate_overrides, invalid_annotation_target, unnecessary_question_mark

part of 'models.dart';

// **************************************************************************
// FreezedGenerator
// **************************************************************************

T _$identity<T>(T value) => value;

final _privateConstructorUsedError = UnsupportedError(
    'It seems like you constructed your class using `MyClass._()`. This constructor is only meant to be used by freezed and you are not supposed to need it nor use it.\nPlease check the documentation here for more information: https://github.com/rrousselGit/freezed#adding-getters-and-methods-to-our-models');

Flight _$FlightFromJson(Map<String, dynamic> json) {
  return _Flight.fromJson(json);
}

/// @nodoc
mixin _$Flight {
  int get id => throw _privateConstructorUsedError;
  String get name => throw _privateConstructorUsedError;
  String? get description => throw _privateConstructorUsedError;
  int get displayOrder => throw _privateConstructorUsedError;
  double? get minHandicap => throw _privateConstructorUsedError;
  double? get maxHandicap => throw _privateConstructorUsedError;

  /// Serializes this Flight to a JSON map.
  Map<String, dynamic> toJson() => throw _privateConstructorUsedError;

  /// Create a copy of Flight
  /// with the given fields replaced by the non-null parameter values.
  @JsonKey(includeFromJson: false, includeToJson: false)
  $FlightCopyWith<Flight> get copyWith => throw _privateConstructorUsedError;
}

/// @nodoc
abstract class $FlightCopyWith<$Res> {
  factory $FlightCopyWith(Flight value, $Res Function(Flight) then) =
      _$FlightCopyWithImpl<$Res, Flight>;
  @useResult
  $Res call(
      {int id,
      String name,
      String? description,
      int displayOrder,
      double? minHandicap,
      double? maxHandicap});
}

/// @nodoc
class _$FlightCopyWithImpl<$Res, $Val extends Flight>
    implements $FlightCopyWith<$Res> {
  _$FlightCopyWithImpl(this._value, this._then);

  // ignore: unused_field
  final $Val _value;
  // ignore: unused_field
  final $Res Function($Val) _then;

  /// Create a copy of Flight
  /// with the given fields replaced by the non-null parameter values.
  @pragma('vm:prefer-inline')
  @override
  $Res call({
    Object? id = null,
    Object? name = null,
    Object? description = freezed,
    Object? displayOrder = null,
    Object? minHandicap = freezed,
    Object? maxHandicap = freezed,
  }) {
    return _then(_value.copyWith(
      id: null == id
          ? _value.id
          : id // ignore: cast_nullable_to_non_nullable
              as int,
      name: null == name
          ? _value.name
          : name // ignore: cast_nullable_to_non_nullable
              as String,
      description: freezed == description
          ? _value.description
          : description // ignore: cast_nullable_to_non_nullable
              as String?,
      displayOrder: null == displayOrder
          ? _value.displayOrder
          : displayOrder // ignore: cast_nullable_to_non_nullable
              as int,
      minHandicap: freezed == minHandicap
          ? _value.minHandicap
          : minHandicap // ignore: cast_nullable_to_non_nullable
              as double?,
      maxHandicap: freezed == maxHandicap
          ? _value.maxHandicap
          : maxHandicap // ignore: cast_nullable_to_non_nullable
              as double?,
    ) as $Val);
  }
}

/// @nodoc
abstract class _$$FlightImplCopyWith<$Res> implements $FlightCopyWith<$Res> {
  factory _$$FlightImplCopyWith(
          _$FlightImpl value, $Res Function(_$FlightImpl) then) =
      __$$FlightImplCopyWithImpl<$Res>;
  @override
  @useResult
  $Res call(
      {int id,
      String name,
      String? description,
      int displayOrder,
      double? minHandicap,
      double? maxHandicap});
}

/// @nodoc
class __$$FlightImplCopyWithImpl<$Res>
    extends _$FlightCopyWithImpl<$Res, _$FlightImpl>
    implements _$$FlightImplCopyWith<$Res> {
  __$$FlightImplCopyWithImpl(
      _$FlightImpl _value, $Res Function(_$FlightImpl) _then)
      : super(_value, _then);

  /// Create a copy of Flight
  /// with the given fields replaced by the non-null parameter values.
  @pragma('vm:prefer-inline')
  @override
  $Res call({
    Object? id = null,
    Object? name = null,
    Object? description = freezed,
    Object? displayOrder = null,
    Object? minHandicap = freezed,
    Object? maxHandicap = freezed,
  }) {
    return _then(_$FlightImpl(
      id: null == id
          ? _value.id
          : id // ignore: cast_nullable_to_non_nullable
              as int,
      name: null == name
          ? _value.name
          : name // ignore: cast_nullable_to_non_nullable
              as String,
      description: freezed == description
          ? _value.description
          : description // ignore: cast_nullable_to_non_nullable
              as String?,
      displayOrder: null == displayOrder
          ? _value.displayOrder
          : displayOrder // ignore: cast_nullable_to_non_nullable
              as int,
      minHandicap: freezed == minHandicap
          ? _value.minHandicap
          : minHandicap // ignore: cast_nullable_to_non_nullable
              as double?,
      maxHandicap: freezed == maxHandicap
          ? _value.maxHandicap
          : maxHandicap // ignore: cast_nullable_to_non_nullable
              as double?,
    ));
  }
}

/// @nodoc
@JsonSerializable()
class _$FlightImpl implements _Flight {
  const _$FlightImpl(
      {required this.id,
      required this.name,
      this.description,
      this.displayOrder = 0,
      this.minHandicap,
      this.maxHandicap});

  factory _$FlightImpl.fromJson(Map<String, dynamic> json) =>
      _$$FlightImplFromJson(json);

  @override
  final int id;
  @override
  final String name;
  @override
  final String? description;
  @override
  @JsonKey()
  final int displayOrder;
  @override
  final double? minHandicap;
  @override
  final double? maxHandicap;

  @override
  String toString() {
    return 'Flight(id: $id, name: $name, description: $description, displayOrder: $displayOrder, minHandicap: $minHandicap, maxHandicap: $maxHandicap)';
  }

  @override
  bool operator ==(Object other) {
    return identical(this, other) ||
        (other.runtimeType == runtimeType &&
            other is _$FlightImpl &&
            (identical(other.id, id) || other.id == id) &&
            (identical(other.name, name) || other.name == name) &&
            (identical(other.description, description) ||
                other.description == description) &&
            (identical(other.displayOrder, displayOrder) ||
                other.displayOrder == displayOrder) &&
            (identical(other.minHandicap, minHandicap) ||
                other.minHandicap == minHandicap) &&
            (identical(other.maxHandicap, maxHandicap) ||
                other.maxHandicap == maxHandicap));
  }

  @JsonKey(includeFromJson: false, includeToJson: false)
  @override
  int get hashCode => Object.hash(runtimeType, id, name, description,
      displayOrder, minHandicap, maxHandicap);

  /// Create a copy of Flight
  /// with the given fields replaced by the non-null parameter values.
  @JsonKey(includeFromJson: false, includeToJson: false)
  @override
  @pragma('vm:prefer-inline')
  _$$FlightImplCopyWith<_$FlightImpl> get copyWith =>
      __$$FlightImplCopyWithImpl<_$FlightImpl>(this, _$identity);

  @override
  Map<String, dynamic> toJson() {
    return _$$FlightImplToJson(
      this,
    );
  }
}

abstract class _Flight implements Flight {
  const factory _Flight(
      {required final int id,
      required final String name,
      final String? description,
      final int displayOrder,
      final double? minHandicap,
      final double? maxHandicap}) = _$FlightImpl;

  factory _Flight.fromJson(Map<String, dynamic> json) = _$FlightImpl.fromJson;

  @override
  int get id;
  @override
  String get name;
  @override
  String? get description;
  @override
  int get displayOrder;
  @override
  double? get minHandicap;
  @override
  double? get maxHandicap;

  /// Create a copy of Flight
  /// with the given fields replaced by the non-null parameter values.
  @override
  @JsonKey(includeFromJson: false, includeToJson: false)
  _$$FlightImplCopyWith<_$FlightImpl> get copyWith =>
      throw _privateConstructorUsedError;
}

LeaderboardEntry _$LeaderboardEntryFromJson(Map<String, dynamic> json) {
  return _LeaderboardEntry.fromJson(json);
}

/// @nodoc
mixin _$LeaderboardEntry {
  int get playerId => throw _privateConstructorUsedError;
  String get playerName => throw _privateConstructorUsedError;
  int get totalStablefordPoints => throw _privateConstructorUsedError;
  int get roundsPlayed => throw _privateConstructorUsedError;
  int get currentRank => throw _privateConstructorUsedError;
  int? get previousRank => throw _privateConstructorUsedError;
  double get currentHandicap => throw _privateConstructorUsedError;
  double? get averagePoints => throw _privateConstructorUsedError;
  int? get lastRoundPoints => throw _privateConstructorUsedError;

  /// Serializes this LeaderboardEntry to a JSON map.
  Map<String, dynamic> toJson() => throw _privateConstructorUsedError;

  /// Create a copy of LeaderboardEntry
  /// with the given fields replaced by the non-null parameter values.
  @JsonKey(includeFromJson: false, includeToJson: false)
  $LeaderboardEntryCopyWith<LeaderboardEntry> get copyWith =>
      throw _privateConstructorUsedError;
}

/// @nodoc
abstract class $LeaderboardEntryCopyWith<$Res> {
  factory $LeaderboardEntryCopyWith(
          LeaderboardEntry value, $Res Function(LeaderboardEntry) then) =
      _$LeaderboardEntryCopyWithImpl<$Res, LeaderboardEntry>;
  @useResult
  $Res call(
      {int playerId,
      String playerName,
      int totalStablefordPoints,
      int roundsPlayed,
      int currentRank,
      int? previousRank,
      double currentHandicap,
      double? averagePoints,
      int? lastRoundPoints});
}

/// @nodoc
class _$LeaderboardEntryCopyWithImpl<$Res, $Val extends LeaderboardEntry>
    implements $LeaderboardEntryCopyWith<$Res> {
  _$LeaderboardEntryCopyWithImpl(this._value, this._then);

  // ignore: unused_field
  final $Val _value;
  // ignore: unused_field
  final $Res Function($Val) _then;

  /// Create a copy of LeaderboardEntry
  /// with the given fields replaced by the non-null parameter values.
  @pragma('vm:prefer-inline')
  @override
  $Res call({
    Object? playerId = null,
    Object? playerName = null,
    Object? totalStablefordPoints = null,
    Object? roundsPlayed = null,
    Object? currentRank = null,
    Object? previousRank = freezed,
    Object? currentHandicap = null,
    Object? averagePoints = freezed,
    Object? lastRoundPoints = freezed,
  }) {
    return _then(_value.copyWith(
      playerId: null == playerId
          ? _value.playerId
          : playerId // ignore: cast_nullable_to_non_nullable
              as int,
      playerName: null == playerName
          ? _value.playerName
          : playerName // ignore: cast_nullable_to_non_nullable
              as String,
      totalStablefordPoints: null == totalStablefordPoints
          ? _value.totalStablefordPoints
          : totalStablefordPoints // ignore: cast_nullable_to_non_nullable
              as int,
      roundsPlayed: null == roundsPlayed
          ? _value.roundsPlayed
          : roundsPlayed // ignore: cast_nullable_to_non_nullable
              as int,
      currentRank: null == currentRank
          ? _value.currentRank
          : currentRank // ignore: cast_nullable_to_non_nullable
              as int,
      previousRank: freezed == previousRank
          ? _value.previousRank
          : previousRank // ignore: cast_nullable_to_non_nullable
              as int?,
      currentHandicap: null == currentHandicap
          ? _value.currentHandicap
          : currentHandicap // ignore: cast_nullable_to_non_nullable
              as double,
      averagePoints: freezed == averagePoints
          ? _value.averagePoints
          : averagePoints // ignore: cast_nullable_to_non_nullable
              as double?,
      lastRoundPoints: freezed == lastRoundPoints
          ? _value.lastRoundPoints
          : lastRoundPoints // ignore: cast_nullable_to_non_nullable
              as int?,
    ) as $Val);
  }
}

/// @nodoc
abstract class _$$LeaderboardEntryImplCopyWith<$Res>
    implements $LeaderboardEntryCopyWith<$Res> {
  factory _$$LeaderboardEntryImplCopyWith(_$LeaderboardEntryImpl value,
          $Res Function(_$LeaderboardEntryImpl) then) =
      __$$LeaderboardEntryImplCopyWithImpl<$Res>;
  @override
  @useResult
  $Res call(
      {int playerId,
      String playerName,
      int totalStablefordPoints,
      int roundsPlayed,
      int currentRank,
      int? previousRank,
      double currentHandicap,
      double? averagePoints,
      int? lastRoundPoints});
}

/// @nodoc
class __$$LeaderboardEntryImplCopyWithImpl<$Res>
    extends _$LeaderboardEntryCopyWithImpl<$Res, _$LeaderboardEntryImpl>
    implements _$$LeaderboardEntryImplCopyWith<$Res> {
  __$$LeaderboardEntryImplCopyWithImpl(_$LeaderboardEntryImpl _value,
      $Res Function(_$LeaderboardEntryImpl) _then)
      : super(_value, _then);

  /// Create a copy of LeaderboardEntry
  /// with the given fields replaced by the non-null parameter values.
  @pragma('vm:prefer-inline')
  @override
  $Res call({
    Object? playerId = null,
    Object? playerName = null,
    Object? totalStablefordPoints = null,
    Object? roundsPlayed = null,
    Object? currentRank = null,
    Object? previousRank = freezed,
    Object? currentHandicap = null,
    Object? averagePoints = freezed,
    Object? lastRoundPoints = freezed,
  }) {
    return _then(_$LeaderboardEntryImpl(
      playerId: null == playerId
          ? _value.playerId
          : playerId // ignore: cast_nullable_to_non_nullable
              as int,
      playerName: null == playerName
          ? _value.playerName
          : playerName // ignore: cast_nullable_to_non_nullable
              as String,
      totalStablefordPoints: null == totalStablefordPoints
          ? _value.totalStablefordPoints
          : totalStablefordPoints // ignore: cast_nullable_to_non_nullable
              as int,
      roundsPlayed: null == roundsPlayed
          ? _value.roundsPlayed
          : roundsPlayed // ignore: cast_nullable_to_non_nullable
              as int,
      currentRank: null == currentRank
          ? _value.currentRank
          : currentRank // ignore: cast_nullable_to_non_nullable
              as int,
      previousRank: freezed == previousRank
          ? _value.previousRank
          : previousRank // ignore: cast_nullable_to_non_nullable
              as int?,
      currentHandicap: null == currentHandicap
          ? _value.currentHandicap
          : currentHandicap // ignore: cast_nullable_to_non_nullable
              as double,
      averagePoints: freezed == averagePoints
          ? _value.averagePoints
          : averagePoints // ignore: cast_nullable_to_non_nullable
              as double?,
      lastRoundPoints: freezed == lastRoundPoints
          ? _value.lastRoundPoints
          : lastRoundPoints // ignore: cast_nullable_to_non_nullable
              as int?,
    ));
  }
}

/// @nodoc
@JsonSerializable()
class _$LeaderboardEntryImpl implements _LeaderboardEntry {
  const _$LeaderboardEntryImpl(
      {required this.playerId,
      required this.playerName,
      required this.totalStablefordPoints,
      required this.roundsPlayed,
      required this.currentRank,
      this.previousRank,
      required this.currentHandicap,
      this.averagePoints,
      this.lastRoundPoints});

  factory _$LeaderboardEntryImpl.fromJson(Map<String, dynamic> json) =>
      _$$LeaderboardEntryImplFromJson(json);

  @override
  final int playerId;
  @override
  final String playerName;
  @override
  final int totalStablefordPoints;
  @override
  final int roundsPlayed;
  @override
  final int currentRank;
  @override
  final int? previousRank;
  @override
  final double currentHandicap;
  @override
  final double? averagePoints;
  @override
  final int? lastRoundPoints;

  @override
  String toString() {
    return 'LeaderboardEntry(playerId: $playerId, playerName: $playerName, totalStablefordPoints: $totalStablefordPoints, roundsPlayed: $roundsPlayed, currentRank: $currentRank, previousRank: $previousRank, currentHandicap: $currentHandicap, averagePoints: $averagePoints, lastRoundPoints: $lastRoundPoints)';
  }

  @override
  bool operator ==(Object other) {
    return identical(this, other) ||
        (other.runtimeType == runtimeType &&
            other is _$LeaderboardEntryImpl &&
            (identical(other.playerId, playerId) ||
                other.playerId == playerId) &&
            (identical(other.playerName, playerName) ||
                other.playerName == playerName) &&
            (identical(other.totalStablefordPoints, totalStablefordPoints) ||
                other.totalStablefordPoints == totalStablefordPoints) &&
            (identical(other.roundsPlayed, roundsPlayed) ||
                other.roundsPlayed == roundsPlayed) &&
            (identical(other.currentRank, currentRank) ||
                other.currentRank == currentRank) &&
            (identical(other.previousRank, previousRank) ||
                other.previousRank == previousRank) &&
            (identical(other.currentHandicap, currentHandicap) ||
                other.currentHandicap == currentHandicap) &&
            (identical(other.averagePoints, averagePoints) ||
                other.averagePoints == averagePoints) &&
            (identical(other.lastRoundPoints, lastRoundPoints) ||
                other.lastRoundPoints == lastRoundPoints));
  }

  @JsonKey(includeFromJson: false, includeToJson: false)
  @override
  int get hashCode => Object.hash(
      runtimeType,
      playerId,
      playerName,
      totalStablefordPoints,
      roundsPlayed,
      currentRank,
      previousRank,
      currentHandicap,
      averagePoints,
      lastRoundPoints);

  /// Create a copy of LeaderboardEntry
  /// with the given fields replaced by the non-null parameter values.
  @JsonKey(includeFromJson: false, includeToJson: false)
  @override
  @pragma('vm:prefer-inline')
  _$$LeaderboardEntryImplCopyWith<_$LeaderboardEntryImpl> get copyWith =>
      __$$LeaderboardEntryImplCopyWithImpl<_$LeaderboardEntryImpl>(
          this, _$identity);

  @override
  Map<String, dynamic> toJson() {
    return _$$LeaderboardEntryImplToJson(
      this,
    );
  }
}

abstract class _LeaderboardEntry implements LeaderboardEntry {
  const factory _LeaderboardEntry(
      {required final int playerId,
      required final String playerName,
      required final int totalStablefordPoints,
      required final int roundsPlayed,
      required final int currentRank,
      final int? previousRank,
      required final double currentHandicap,
      final double? averagePoints,
      final int? lastRoundPoints}) = _$LeaderboardEntryImpl;

  factory _LeaderboardEntry.fromJson(Map<String, dynamic> json) =
      _$LeaderboardEntryImpl.fromJson;

  @override
  int get playerId;
  @override
  String get playerName;
  @override
  int get totalStablefordPoints;
  @override
  int get roundsPlayed;
  @override
  int get currentRank;
  @override
  int? get previousRank;
  @override
  double get currentHandicap;
  @override
  double? get averagePoints;
  @override
  int? get lastRoundPoints;

  /// Create a copy of LeaderboardEntry
  /// with the given fields replaced by the non-null parameter values.
  @override
  @JsonKey(includeFromJson: false, includeToJson: false)
  _$$LeaderboardEntryImplCopyWith<_$LeaderboardEntryImpl> get copyWith =>
      throw _privateConstructorUsedError;
}
