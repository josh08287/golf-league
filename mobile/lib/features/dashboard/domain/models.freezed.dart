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

DashboardData _$DashboardDataFromJson(Map<String, dynamic> json) {
  return _DashboardData.fromJson(json);
}

/// @nodoc
mixin _$DashboardData {
  List<FlightSummary> get flightSummaries => throw _privateConstructorUsedError;
  LatestRoundSummary? get latestRound => throw _privateConstructorUsedError;

  /// Serializes this DashboardData to a JSON map.
  Map<String, dynamic> toJson() => throw _privateConstructorUsedError;

  /// Create a copy of DashboardData
  /// with the given fields replaced by the non-null parameter values.
  @JsonKey(includeFromJson: false, includeToJson: false)
  $DashboardDataCopyWith<DashboardData> get copyWith =>
      throw _privateConstructorUsedError;
}

/// @nodoc
abstract class $DashboardDataCopyWith<$Res> {
  factory $DashboardDataCopyWith(
          DashboardData value, $Res Function(DashboardData) then) =
      _$DashboardDataCopyWithImpl<$Res, DashboardData>;
  @useResult
  $Res call(
      {List<FlightSummary> flightSummaries, LatestRoundSummary? latestRound});

  $LatestRoundSummaryCopyWith<$Res>? get latestRound;
}

/// @nodoc
class _$DashboardDataCopyWithImpl<$Res, $Val extends DashboardData>
    implements $DashboardDataCopyWith<$Res> {
  _$DashboardDataCopyWithImpl(this._value, this._then);

  // ignore: unused_field
  final $Val _value;
  // ignore: unused_field
  final $Res Function($Val) _then;

  /// Create a copy of DashboardData
  /// with the given fields replaced by the non-null parameter values.
  @pragma('vm:prefer-inline')
  @override
  $Res call({
    Object? flightSummaries = null,
    Object? latestRound = freezed,
  }) {
    return _then(_value.copyWith(
      flightSummaries: null == flightSummaries
          ? _value.flightSummaries
          : flightSummaries // ignore: cast_nullable_to_non_nullable
              as List<FlightSummary>,
      latestRound: freezed == latestRound
          ? _value.latestRound
          : latestRound // ignore: cast_nullable_to_non_nullable
              as LatestRoundSummary?,
    ) as $Val);
  }

  /// Create a copy of DashboardData
  /// with the given fields replaced by the non-null parameter values.
  @override
  @pragma('vm:prefer-inline')
  $LatestRoundSummaryCopyWith<$Res>? get latestRound {
    if (_value.latestRound == null) {
      return null;
    }

    return $LatestRoundSummaryCopyWith<$Res>(_value.latestRound!, (value) {
      return _then(_value.copyWith(latestRound: value) as $Val);
    });
  }
}

/// @nodoc
abstract class _$$DashboardDataImplCopyWith<$Res>
    implements $DashboardDataCopyWith<$Res> {
  factory _$$DashboardDataImplCopyWith(
          _$DashboardDataImpl value, $Res Function(_$DashboardDataImpl) then) =
      __$$DashboardDataImplCopyWithImpl<$Res>;
  @override
  @useResult
  $Res call(
      {List<FlightSummary> flightSummaries, LatestRoundSummary? latestRound});

  @override
  $LatestRoundSummaryCopyWith<$Res>? get latestRound;
}

/// @nodoc
class __$$DashboardDataImplCopyWithImpl<$Res>
    extends _$DashboardDataCopyWithImpl<$Res, _$DashboardDataImpl>
    implements _$$DashboardDataImplCopyWith<$Res> {
  __$$DashboardDataImplCopyWithImpl(
      _$DashboardDataImpl _value, $Res Function(_$DashboardDataImpl) _then)
      : super(_value, _then);

  /// Create a copy of DashboardData
  /// with the given fields replaced by the non-null parameter values.
  @pragma('vm:prefer-inline')
  @override
  $Res call({
    Object? flightSummaries = null,
    Object? latestRound = freezed,
  }) {
    return _then(_$DashboardDataImpl(
      flightSummaries: null == flightSummaries
          ? _value._flightSummaries
          : flightSummaries // ignore: cast_nullable_to_non_nullable
              as List<FlightSummary>,
      latestRound: freezed == latestRound
          ? _value.latestRound
          : latestRound // ignore: cast_nullable_to_non_nullable
              as LatestRoundSummary?,
    ));
  }
}

/// @nodoc
@JsonSerializable()
class _$DashboardDataImpl implements _DashboardData {
  const _$DashboardDataImpl(
      {final List<FlightSummary> flightSummaries = const [], this.latestRound})
      : _flightSummaries = flightSummaries;

  factory _$DashboardDataImpl.fromJson(Map<String, dynamic> json) =>
      _$$DashboardDataImplFromJson(json);

  final List<FlightSummary> _flightSummaries;
  @override
  @JsonKey()
  List<FlightSummary> get flightSummaries {
    if (_flightSummaries is EqualUnmodifiableListView) return _flightSummaries;
    // ignore: implicit_dynamic_type
    return EqualUnmodifiableListView(_flightSummaries);
  }

  @override
  final LatestRoundSummary? latestRound;

  @override
  String toString() {
    return 'DashboardData(flightSummaries: $flightSummaries, latestRound: $latestRound)';
  }

  @override
  bool operator ==(Object other) {
    return identical(this, other) ||
        (other.runtimeType == runtimeType &&
            other is _$DashboardDataImpl &&
            const DeepCollectionEquality()
                .equals(other._flightSummaries, _flightSummaries) &&
            (identical(other.latestRound, latestRound) ||
                other.latestRound == latestRound));
  }

  @JsonKey(includeFromJson: false, includeToJson: false)
  @override
  int get hashCode => Object.hash(runtimeType,
      const DeepCollectionEquality().hash(_flightSummaries), latestRound);

  /// Create a copy of DashboardData
  /// with the given fields replaced by the non-null parameter values.
  @JsonKey(includeFromJson: false, includeToJson: false)
  @override
  @pragma('vm:prefer-inline')
  _$$DashboardDataImplCopyWith<_$DashboardDataImpl> get copyWith =>
      __$$DashboardDataImplCopyWithImpl<_$DashboardDataImpl>(this, _$identity);

  @override
  Map<String, dynamic> toJson() {
    return _$$DashboardDataImplToJson(
      this,
    );
  }
}

abstract class _DashboardData implements DashboardData {
  const factory _DashboardData(
      {final List<FlightSummary> flightSummaries,
      final LatestRoundSummary? latestRound}) = _$DashboardDataImpl;

  factory _DashboardData.fromJson(Map<String, dynamic> json) =
      _$DashboardDataImpl.fromJson;

  @override
  List<FlightSummary> get flightSummaries;
  @override
  LatestRoundSummary? get latestRound;

  /// Create a copy of DashboardData
  /// with the given fields replaced by the non-null parameter values.
  @override
  @JsonKey(includeFromJson: false, includeToJson: false)
  _$$DashboardDataImplCopyWith<_$DashboardDataImpl> get copyWith =>
      throw _privateConstructorUsedError;
}

FlightSummary _$FlightSummaryFromJson(Map<String, dynamic> json) {
  return _FlightSummary.fromJson(json);
}

/// @nodoc
mixin _$FlightSummary {
  int get flightId => throw _privateConstructorUsedError;
  String get flightName => throw _privateConstructorUsedError;
  List<FlightTopEntry> get topThree => throw _privateConstructorUsedError;

  /// Serializes this FlightSummary to a JSON map.
  Map<String, dynamic> toJson() => throw _privateConstructorUsedError;

  /// Create a copy of FlightSummary
  /// with the given fields replaced by the non-null parameter values.
  @JsonKey(includeFromJson: false, includeToJson: false)
  $FlightSummaryCopyWith<FlightSummary> get copyWith =>
      throw _privateConstructorUsedError;
}

/// @nodoc
abstract class $FlightSummaryCopyWith<$Res> {
  factory $FlightSummaryCopyWith(
          FlightSummary value, $Res Function(FlightSummary) then) =
      _$FlightSummaryCopyWithImpl<$Res, FlightSummary>;
  @useResult
  $Res call({int flightId, String flightName, List<FlightTopEntry> topThree});
}

/// @nodoc
class _$FlightSummaryCopyWithImpl<$Res, $Val extends FlightSummary>
    implements $FlightSummaryCopyWith<$Res> {
  _$FlightSummaryCopyWithImpl(this._value, this._then);

  // ignore: unused_field
  final $Val _value;
  // ignore: unused_field
  final $Res Function($Val) _then;

  /// Create a copy of FlightSummary
  /// with the given fields replaced by the non-null parameter values.
  @pragma('vm:prefer-inline')
  @override
  $Res call({
    Object? flightId = null,
    Object? flightName = null,
    Object? topThree = null,
  }) {
    return _then(_value.copyWith(
      flightId: null == flightId
          ? _value.flightId
          : flightId // ignore: cast_nullable_to_non_nullable
              as int,
      flightName: null == flightName
          ? _value.flightName
          : flightName // ignore: cast_nullable_to_non_nullable
              as String,
      topThree: null == topThree
          ? _value.topThree
          : topThree // ignore: cast_nullable_to_non_nullable
              as List<FlightTopEntry>,
    ) as $Val);
  }
}

/// @nodoc
abstract class _$$FlightSummaryImplCopyWith<$Res>
    implements $FlightSummaryCopyWith<$Res> {
  factory _$$FlightSummaryImplCopyWith(
          _$FlightSummaryImpl value, $Res Function(_$FlightSummaryImpl) then) =
      __$$FlightSummaryImplCopyWithImpl<$Res>;
  @override
  @useResult
  $Res call({int flightId, String flightName, List<FlightTopEntry> topThree});
}

/// @nodoc
class __$$FlightSummaryImplCopyWithImpl<$Res>
    extends _$FlightSummaryCopyWithImpl<$Res, _$FlightSummaryImpl>
    implements _$$FlightSummaryImplCopyWith<$Res> {
  __$$FlightSummaryImplCopyWithImpl(
      _$FlightSummaryImpl _value, $Res Function(_$FlightSummaryImpl) _then)
      : super(_value, _then);

  /// Create a copy of FlightSummary
  /// with the given fields replaced by the non-null parameter values.
  @pragma('vm:prefer-inline')
  @override
  $Res call({
    Object? flightId = null,
    Object? flightName = null,
    Object? topThree = null,
  }) {
    return _then(_$FlightSummaryImpl(
      flightId: null == flightId
          ? _value.flightId
          : flightId // ignore: cast_nullable_to_non_nullable
              as int,
      flightName: null == flightName
          ? _value.flightName
          : flightName // ignore: cast_nullable_to_non_nullable
              as String,
      topThree: null == topThree
          ? _value._topThree
          : topThree // ignore: cast_nullable_to_non_nullable
              as List<FlightTopEntry>,
    ));
  }
}

/// @nodoc
@JsonSerializable()
class _$FlightSummaryImpl implements _FlightSummary {
  const _$FlightSummaryImpl(
      {required this.flightId,
      required this.flightName,
      final List<FlightTopEntry> topThree = const []})
      : _topThree = topThree;

  factory _$FlightSummaryImpl.fromJson(Map<String, dynamic> json) =>
      _$$FlightSummaryImplFromJson(json);

  @override
  final int flightId;
  @override
  final String flightName;
  final List<FlightTopEntry> _topThree;
  @override
  @JsonKey()
  List<FlightTopEntry> get topThree {
    if (_topThree is EqualUnmodifiableListView) return _topThree;
    // ignore: implicit_dynamic_type
    return EqualUnmodifiableListView(_topThree);
  }

  @override
  String toString() {
    return 'FlightSummary(flightId: $flightId, flightName: $flightName, topThree: $topThree)';
  }

  @override
  bool operator ==(Object other) {
    return identical(this, other) ||
        (other.runtimeType == runtimeType &&
            other is _$FlightSummaryImpl &&
            (identical(other.flightId, flightId) ||
                other.flightId == flightId) &&
            (identical(other.flightName, flightName) ||
                other.flightName == flightName) &&
            const DeepCollectionEquality().equals(other._topThree, _topThree));
  }

  @JsonKey(includeFromJson: false, includeToJson: false)
  @override
  int get hashCode => Object.hash(runtimeType, flightId, flightName,
      const DeepCollectionEquality().hash(_topThree));

  /// Create a copy of FlightSummary
  /// with the given fields replaced by the non-null parameter values.
  @JsonKey(includeFromJson: false, includeToJson: false)
  @override
  @pragma('vm:prefer-inline')
  _$$FlightSummaryImplCopyWith<_$FlightSummaryImpl> get copyWith =>
      __$$FlightSummaryImplCopyWithImpl<_$FlightSummaryImpl>(this, _$identity);

  @override
  Map<String, dynamic> toJson() {
    return _$$FlightSummaryImplToJson(
      this,
    );
  }
}

abstract class _FlightSummary implements FlightSummary {
  const factory _FlightSummary(
      {required final int flightId,
      required final String flightName,
      final List<FlightTopEntry> topThree}) = _$FlightSummaryImpl;

  factory _FlightSummary.fromJson(Map<String, dynamic> json) =
      _$FlightSummaryImpl.fromJson;

  @override
  int get flightId;
  @override
  String get flightName;
  @override
  List<FlightTopEntry> get topThree;

  /// Create a copy of FlightSummary
  /// with the given fields replaced by the non-null parameter values.
  @override
  @JsonKey(includeFromJson: false, includeToJson: false)
  _$$FlightSummaryImplCopyWith<_$FlightSummaryImpl> get copyWith =>
      throw _privateConstructorUsedError;
}

FlightTopEntry _$FlightTopEntryFromJson(Map<String, dynamic> json) {
  return _FlightTopEntry.fromJson(json);
}

/// @nodoc
mixin _$FlightTopEntry {
  int get rank => throw _privateConstructorUsedError;
  int get playerId => throw _privateConstructorUsedError;
  String get playerName => throw _privateConstructorUsedError;
  int get totalPoints => throw _privateConstructorUsedError;
  double get handicap => throw _privateConstructorUsedError;

  /// Serializes this FlightTopEntry to a JSON map.
  Map<String, dynamic> toJson() => throw _privateConstructorUsedError;

  /// Create a copy of FlightTopEntry
  /// with the given fields replaced by the non-null parameter values.
  @JsonKey(includeFromJson: false, includeToJson: false)
  $FlightTopEntryCopyWith<FlightTopEntry> get copyWith =>
      throw _privateConstructorUsedError;
}

/// @nodoc
abstract class $FlightTopEntryCopyWith<$Res> {
  factory $FlightTopEntryCopyWith(
          FlightTopEntry value, $Res Function(FlightTopEntry) then) =
      _$FlightTopEntryCopyWithImpl<$Res, FlightTopEntry>;
  @useResult
  $Res call(
      {int rank,
      int playerId,
      String playerName,
      int totalPoints,
      double handicap});
}

/// @nodoc
class _$FlightTopEntryCopyWithImpl<$Res, $Val extends FlightTopEntry>
    implements $FlightTopEntryCopyWith<$Res> {
  _$FlightTopEntryCopyWithImpl(this._value, this._then);

  // ignore: unused_field
  final $Val _value;
  // ignore: unused_field
  final $Res Function($Val) _then;

  /// Create a copy of FlightTopEntry
  /// with the given fields replaced by the non-null parameter values.
  @pragma('vm:prefer-inline')
  @override
  $Res call({
    Object? rank = null,
    Object? playerId = null,
    Object? playerName = null,
    Object? totalPoints = null,
    Object? handicap = null,
  }) {
    return _then(_value.copyWith(
      rank: null == rank
          ? _value.rank
          : rank // ignore: cast_nullable_to_non_nullable
              as int,
      playerId: null == playerId
          ? _value.playerId
          : playerId // ignore: cast_nullable_to_non_nullable
              as int,
      playerName: null == playerName
          ? _value.playerName
          : playerName // ignore: cast_nullable_to_non_nullable
              as String,
      totalPoints: null == totalPoints
          ? _value.totalPoints
          : totalPoints // ignore: cast_nullable_to_non_nullable
              as int,
      handicap: null == handicap
          ? _value.handicap
          : handicap // ignore: cast_nullable_to_non_nullable
              as double,
    ) as $Val);
  }
}

/// @nodoc
abstract class _$$FlightTopEntryImplCopyWith<$Res>
    implements $FlightTopEntryCopyWith<$Res> {
  factory _$$FlightTopEntryImplCopyWith(_$FlightTopEntryImpl value,
          $Res Function(_$FlightTopEntryImpl) then) =
      __$$FlightTopEntryImplCopyWithImpl<$Res>;
  @override
  @useResult
  $Res call(
      {int rank,
      int playerId,
      String playerName,
      int totalPoints,
      double handicap});
}

/// @nodoc
class __$$FlightTopEntryImplCopyWithImpl<$Res>
    extends _$FlightTopEntryCopyWithImpl<$Res, _$FlightTopEntryImpl>
    implements _$$FlightTopEntryImplCopyWith<$Res> {
  __$$FlightTopEntryImplCopyWithImpl(
      _$FlightTopEntryImpl _value, $Res Function(_$FlightTopEntryImpl) _then)
      : super(_value, _then);

  /// Create a copy of FlightTopEntry
  /// with the given fields replaced by the non-null parameter values.
  @pragma('vm:prefer-inline')
  @override
  $Res call({
    Object? rank = null,
    Object? playerId = null,
    Object? playerName = null,
    Object? totalPoints = null,
    Object? handicap = null,
  }) {
    return _then(_$FlightTopEntryImpl(
      rank: null == rank
          ? _value.rank
          : rank // ignore: cast_nullable_to_non_nullable
              as int,
      playerId: null == playerId
          ? _value.playerId
          : playerId // ignore: cast_nullable_to_non_nullable
              as int,
      playerName: null == playerName
          ? _value.playerName
          : playerName // ignore: cast_nullable_to_non_nullable
              as String,
      totalPoints: null == totalPoints
          ? _value.totalPoints
          : totalPoints // ignore: cast_nullable_to_non_nullable
              as int,
      handicap: null == handicap
          ? _value.handicap
          : handicap // ignore: cast_nullable_to_non_nullable
              as double,
    ));
  }
}

/// @nodoc
@JsonSerializable()
class _$FlightTopEntryImpl implements _FlightTopEntry {
  const _$FlightTopEntryImpl(
      {required this.rank,
      required this.playerId,
      required this.playerName,
      required this.totalPoints,
      required this.handicap});

  factory _$FlightTopEntryImpl.fromJson(Map<String, dynamic> json) =>
      _$$FlightTopEntryImplFromJson(json);

  @override
  final int rank;
  @override
  final int playerId;
  @override
  final String playerName;
  @override
  final int totalPoints;
  @override
  final double handicap;

  @override
  String toString() {
    return 'FlightTopEntry(rank: $rank, playerId: $playerId, playerName: $playerName, totalPoints: $totalPoints, handicap: $handicap)';
  }

  @override
  bool operator ==(Object other) {
    return identical(this, other) ||
        (other.runtimeType == runtimeType &&
            other is _$FlightTopEntryImpl &&
            (identical(other.rank, rank) || other.rank == rank) &&
            (identical(other.playerId, playerId) ||
                other.playerId == playerId) &&
            (identical(other.playerName, playerName) ||
                other.playerName == playerName) &&
            (identical(other.totalPoints, totalPoints) ||
                other.totalPoints == totalPoints) &&
            (identical(other.handicap, handicap) ||
                other.handicap == handicap));
  }

  @JsonKey(includeFromJson: false, includeToJson: false)
  @override
  int get hashCode => Object.hash(
      runtimeType, rank, playerId, playerName, totalPoints, handicap);

  /// Create a copy of FlightTopEntry
  /// with the given fields replaced by the non-null parameter values.
  @JsonKey(includeFromJson: false, includeToJson: false)
  @override
  @pragma('vm:prefer-inline')
  _$$FlightTopEntryImplCopyWith<_$FlightTopEntryImpl> get copyWith =>
      __$$FlightTopEntryImplCopyWithImpl<_$FlightTopEntryImpl>(
          this, _$identity);

  @override
  Map<String, dynamic> toJson() {
    return _$$FlightTopEntryImplToJson(
      this,
    );
  }
}

abstract class _FlightTopEntry implements FlightTopEntry {
  const factory _FlightTopEntry(
      {required final int rank,
      required final int playerId,
      required final String playerName,
      required final int totalPoints,
      required final double handicap}) = _$FlightTopEntryImpl;

  factory _FlightTopEntry.fromJson(Map<String, dynamic> json) =
      _$FlightTopEntryImpl.fromJson;

  @override
  int get rank;
  @override
  int get playerId;
  @override
  String get playerName;
  @override
  int get totalPoints;
  @override
  double get handicap;

  /// Create a copy of FlightTopEntry
  /// with the given fields replaced by the non-null parameter values.
  @override
  @JsonKey(includeFromJson: false, includeToJson: false)
  _$$FlightTopEntryImplCopyWith<_$FlightTopEntryImpl> get copyWith =>
      throw _privateConstructorUsedError;
}

LatestRoundSummary _$LatestRoundSummaryFromJson(Map<String, dynamic> json) {
  return _LatestRoundSummary.fromJson(json);
}

/// @nodoc
mixin _$LatestRoundSummary {
  int get roundId => throw _privateConstructorUsedError;
  String get courseName => throw _privateConstructorUsedError;
  DateTime get playedDate => throw _privateConstructorUsedError;
  String get status => throw _privateConstructorUsedError;
  int? get roundWinnerPlayerId => throw _privateConstructorUsedError;
  String? get roundWinnerName => throw _privateConstructorUsedError;
  int? get roundWinnerPoints => throw _privateConstructorUsedError;

  /// Serializes this LatestRoundSummary to a JSON map.
  Map<String, dynamic> toJson() => throw _privateConstructorUsedError;

  /// Create a copy of LatestRoundSummary
  /// with the given fields replaced by the non-null parameter values.
  @JsonKey(includeFromJson: false, includeToJson: false)
  $LatestRoundSummaryCopyWith<LatestRoundSummary> get copyWith =>
      throw _privateConstructorUsedError;
}

/// @nodoc
abstract class $LatestRoundSummaryCopyWith<$Res> {
  factory $LatestRoundSummaryCopyWith(
          LatestRoundSummary value, $Res Function(LatestRoundSummary) then) =
      _$LatestRoundSummaryCopyWithImpl<$Res, LatestRoundSummary>;
  @useResult
  $Res call(
      {int roundId,
      String courseName,
      DateTime playedDate,
      String status,
      int? roundWinnerPlayerId,
      String? roundWinnerName,
      int? roundWinnerPoints});
}

/// @nodoc
class _$LatestRoundSummaryCopyWithImpl<$Res, $Val extends LatestRoundSummary>
    implements $LatestRoundSummaryCopyWith<$Res> {
  _$LatestRoundSummaryCopyWithImpl(this._value, this._then);

  // ignore: unused_field
  final $Val _value;
  // ignore: unused_field
  final $Res Function($Val) _then;

  /// Create a copy of LatestRoundSummary
  /// with the given fields replaced by the non-null parameter values.
  @pragma('vm:prefer-inline')
  @override
  $Res call({
    Object? roundId = null,
    Object? courseName = null,
    Object? playedDate = null,
    Object? status = null,
    Object? roundWinnerPlayerId = freezed,
    Object? roundWinnerName = freezed,
    Object? roundWinnerPoints = freezed,
  }) {
    return _then(_value.copyWith(
      roundId: null == roundId
          ? _value.roundId
          : roundId // ignore: cast_nullable_to_non_nullable
              as int,
      courseName: null == courseName
          ? _value.courseName
          : courseName // ignore: cast_nullable_to_non_nullable
              as String,
      playedDate: null == playedDate
          ? _value.playedDate
          : playedDate // ignore: cast_nullable_to_non_nullable
              as DateTime,
      status: null == status
          ? _value.status
          : status // ignore: cast_nullable_to_non_nullable
              as String,
      roundWinnerPlayerId: freezed == roundWinnerPlayerId
          ? _value.roundWinnerPlayerId
          : roundWinnerPlayerId // ignore: cast_nullable_to_non_nullable
              as int?,
      roundWinnerName: freezed == roundWinnerName
          ? _value.roundWinnerName
          : roundWinnerName // ignore: cast_nullable_to_non_nullable
              as String?,
      roundWinnerPoints: freezed == roundWinnerPoints
          ? _value.roundWinnerPoints
          : roundWinnerPoints // ignore: cast_nullable_to_non_nullable
              as int?,
    ) as $Val);
  }
}

/// @nodoc
abstract class _$$LatestRoundSummaryImplCopyWith<$Res>
    implements $LatestRoundSummaryCopyWith<$Res> {
  factory _$$LatestRoundSummaryImplCopyWith(_$LatestRoundSummaryImpl value,
          $Res Function(_$LatestRoundSummaryImpl) then) =
      __$$LatestRoundSummaryImplCopyWithImpl<$Res>;
  @override
  @useResult
  $Res call(
      {int roundId,
      String courseName,
      DateTime playedDate,
      String status,
      int? roundWinnerPlayerId,
      String? roundWinnerName,
      int? roundWinnerPoints});
}

/// @nodoc
class __$$LatestRoundSummaryImplCopyWithImpl<$Res>
    extends _$LatestRoundSummaryCopyWithImpl<$Res, _$LatestRoundSummaryImpl>
    implements _$$LatestRoundSummaryImplCopyWith<$Res> {
  __$$LatestRoundSummaryImplCopyWithImpl(_$LatestRoundSummaryImpl _value,
      $Res Function(_$LatestRoundSummaryImpl) _then)
      : super(_value, _then);

  /// Create a copy of LatestRoundSummary
  /// with the given fields replaced by the non-null parameter values.
  @pragma('vm:prefer-inline')
  @override
  $Res call({
    Object? roundId = null,
    Object? courseName = null,
    Object? playedDate = null,
    Object? status = null,
    Object? roundWinnerPlayerId = freezed,
    Object? roundWinnerName = freezed,
    Object? roundWinnerPoints = freezed,
  }) {
    return _then(_$LatestRoundSummaryImpl(
      roundId: null == roundId
          ? _value.roundId
          : roundId // ignore: cast_nullable_to_non_nullable
              as int,
      courseName: null == courseName
          ? _value.courseName
          : courseName // ignore: cast_nullable_to_non_nullable
              as String,
      playedDate: null == playedDate
          ? _value.playedDate
          : playedDate // ignore: cast_nullable_to_non_nullable
              as DateTime,
      status: null == status
          ? _value.status
          : status // ignore: cast_nullable_to_non_nullable
              as String,
      roundWinnerPlayerId: freezed == roundWinnerPlayerId
          ? _value.roundWinnerPlayerId
          : roundWinnerPlayerId // ignore: cast_nullable_to_non_nullable
              as int?,
      roundWinnerName: freezed == roundWinnerName
          ? _value.roundWinnerName
          : roundWinnerName // ignore: cast_nullable_to_non_nullable
              as String?,
      roundWinnerPoints: freezed == roundWinnerPoints
          ? _value.roundWinnerPoints
          : roundWinnerPoints // ignore: cast_nullable_to_non_nullable
              as int?,
    ));
  }
}

/// @nodoc
@JsonSerializable()
class _$LatestRoundSummaryImpl implements _LatestRoundSummary {
  const _$LatestRoundSummaryImpl(
      {required this.roundId,
      required this.courseName,
      required this.playedDate,
      required this.status,
      this.roundWinnerPlayerId,
      this.roundWinnerName,
      this.roundWinnerPoints});

  factory _$LatestRoundSummaryImpl.fromJson(Map<String, dynamic> json) =>
      _$$LatestRoundSummaryImplFromJson(json);

  @override
  final int roundId;
  @override
  final String courseName;
  @override
  final DateTime playedDate;
  @override
  final String status;
  @override
  final int? roundWinnerPlayerId;
  @override
  final String? roundWinnerName;
  @override
  final int? roundWinnerPoints;

  @override
  String toString() {
    return 'LatestRoundSummary(roundId: $roundId, courseName: $courseName, playedDate: $playedDate, status: $status, roundWinnerPlayerId: $roundWinnerPlayerId, roundWinnerName: $roundWinnerName, roundWinnerPoints: $roundWinnerPoints)';
  }

  @override
  bool operator ==(Object other) {
    return identical(this, other) ||
        (other.runtimeType == runtimeType &&
            other is _$LatestRoundSummaryImpl &&
            (identical(other.roundId, roundId) || other.roundId == roundId) &&
            (identical(other.courseName, courseName) ||
                other.courseName == courseName) &&
            (identical(other.playedDate, playedDate) ||
                other.playedDate == playedDate) &&
            (identical(other.status, status) || other.status == status) &&
            (identical(other.roundWinnerPlayerId, roundWinnerPlayerId) ||
                other.roundWinnerPlayerId == roundWinnerPlayerId) &&
            (identical(other.roundWinnerName, roundWinnerName) ||
                other.roundWinnerName == roundWinnerName) &&
            (identical(other.roundWinnerPoints, roundWinnerPoints) ||
                other.roundWinnerPoints == roundWinnerPoints));
  }

  @JsonKey(includeFromJson: false, includeToJson: false)
  @override
  int get hashCode => Object.hash(runtimeType, roundId, courseName, playedDate,
      status, roundWinnerPlayerId, roundWinnerName, roundWinnerPoints);

  /// Create a copy of LatestRoundSummary
  /// with the given fields replaced by the non-null parameter values.
  @JsonKey(includeFromJson: false, includeToJson: false)
  @override
  @pragma('vm:prefer-inline')
  _$$LatestRoundSummaryImplCopyWith<_$LatestRoundSummaryImpl> get copyWith =>
      __$$LatestRoundSummaryImplCopyWithImpl<_$LatestRoundSummaryImpl>(
          this, _$identity);

  @override
  Map<String, dynamic> toJson() {
    return _$$LatestRoundSummaryImplToJson(
      this,
    );
  }
}

abstract class _LatestRoundSummary implements LatestRoundSummary {
  const factory _LatestRoundSummary(
      {required final int roundId,
      required final String courseName,
      required final DateTime playedDate,
      required final String status,
      final int? roundWinnerPlayerId,
      final String? roundWinnerName,
      final int? roundWinnerPoints}) = _$LatestRoundSummaryImpl;

  factory _LatestRoundSummary.fromJson(Map<String, dynamic> json) =
      _$LatestRoundSummaryImpl.fromJson;

  @override
  int get roundId;
  @override
  String get courseName;
  @override
  DateTime get playedDate;
  @override
  String get status;
  @override
  int? get roundWinnerPlayerId;
  @override
  String? get roundWinnerName;
  @override
  int? get roundWinnerPoints;

  /// Create a copy of LatestRoundSummary
  /// with the given fields replaced by the non-null parameter values.
  @override
  @JsonKey(includeFromJson: false, includeToJson: false)
  _$$LatestRoundSummaryImplCopyWith<_$LatestRoundSummaryImpl> get copyWith =>
      throw _privateConstructorUsedError;
}
