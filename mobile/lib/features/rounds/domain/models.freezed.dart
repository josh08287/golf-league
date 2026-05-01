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

Round _$RoundFromJson(Map<String, dynamic> json) {
  return _Round.fromJson(json);
}

/// @nodoc
mixin _$Round {
  int get id => throw _privateConstructorUsedError;
  String get courseName => throw _privateConstructorUsedError;
  DateTime get scheduledDate => throw _privateConstructorUsedError;
  DateTime? get playedDate => throw _privateConstructorUsedError;
  String get status => throw _privateConstructorUsedError;
  int get roundNumber => throw _privateConstructorUsedError;
  String? get weatherConditions => throw _privateConstructorUsedError;
  int? get flightId => throw _privateConstructorUsedError;
  String? get flightName => throw _privateConstructorUsedError;

  /// Serializes this Round to a JSON map.
  Map<String, dynamic> toJson() => throw _privateConstructorUsedError;

  /// Create a copy of Round
  /// with the given fields replaced by the non-null parameter values.
  @JsonKey(includeFromJson: false, includeToJson: false)
  $RoundCopyWith<Round> get copyWith => throw _privateConstructorUsedError;
}

/// @nodoc
abstract class $RoundCopyWith<$Res> {
  factory $RoundCopyWith(Round value, $Res Function(Round) then) =
      _$RoundCopyWithImpl<$Res, Round>;
  @useResult
  $Res call(
      {int id,
      String courseName,
      DateTime scheduledDate,
      DateTime? playedDate,
      String status,
      int roundNumber,
      String? weatherConditions,
      int? flightId,
      String? flightName});
}

/// @nodoc
class _$RoundCopyWithImpl<$Res, $Val extends Round>
    implements $RoundCopyWith<$Res> {
  _$RoundCopyWithImpl(this._value, this._then);

  // ignore: unused_field
  final $Val _value;
  // ignore: unused_field
  final $Res Function($Val) _then;

  /// Create a copy of Round
  /// with the given fields replaced by the non-null parameter values.
  @pragma('vm:prefer-inline')
  @override
  $Res call({
    Object? id = null,
    Object? courseName = null,
    Object? scheduledDate = null,
    Object? playedDate = freezed,
    Object? status = null,
    Object? roundNumber = null,
    Object? weatherConditions = freezed,
    Object? flightId = freezed,
    Object? flightName = freezed,
  }) {
    return _then(_value.copyWith(
      id: null == id
          ? _value.id
          : id // ignore: cast_nullable_to_non_nullable
              as int,
      courseName: null == courseName
          ? _value.courseName
          : courseName // ignore: cast_nullable_to_non_nullable
              as String,
      scheduledDate: null == scheduledDate
          ? _value.scheduledDate
          : scheduledDate // ignore: cast_nullable_to_non_nullable
              as DateTime,
      playedDate: freezed == playedDate
          ? _value.playedDate
          : playedDate // ignore: cast_nullable_to_non_nullable
              as DateTime?,
      status: null == status
          ? _value.status
          : status // ignore: cast_nullable_to_non_nullable
              as String,
      roundNumber: null == roundNumber
          ? _value.roundNumber
          : roundNumber // ignore: cast_nullable_to_non_nullable
              as int,
      weatherConditions: freezed == weatherConditions
          ? _value.weatherConditions
          : weatherConditions // ignore: cast_nullable_to_non_nullable
              as String?,
      flightId: freezed == flightId
          ? _value.flightId
          : flightId // ignore: cast_nullable_to_non_nullable
              as int?,
      flightName: freezed == flightName
          ? _value.flightName
          : flightName // ignore: cast_nullable_to_non_nullable
              as String?,
    ) as $Val);
  }
}

/// @nodoc
abstract class _$$RoundImplCopyWith<$Res> implements $RoundCopyWith<$Res> {
  factory _$$RoundImplCopyWith(
          _$RoundImpl value, $Res Function(_$RoundImpl) then) =
      __$$RoundImplCopyWithImpl<$Res>;
  @override
  @useResult
  $Res call(
      {int id,
      String courseName,
      DateTime scheduledDate,
      DateTime? playedDate,
      String status,
      int roundNumber,
      String? weatherConditions,
      int? flightId,
      String? flightName});
}

/// @nodoc
class __$$RoundImplCopyWithImpl<$Res>
    extends _$RoundCopyWithImpl<$Res, _$RoundImpl>
    implements _$$RoundImplCopyWith<$Res> {
  __$$RoundImplCopyWithImpl(
      _$RoundImpl _value, $Res Function(_$RoundImpl) _then)
      : super(_value, _then);

  /// Create a copy of Round
  /// with the given fields replaced by the non-null parameter values.
  @pragma('vm:prefer-inline')
  @override
  $Res call({
    Object? id = null,
    Object? courseName = null,
    Object? scheduledDate = null,
    Object? playedDate = freezed,
    Object? status = null,
    Object? roundNumber = null,
    Object? weatherConditions = freezed,
    Object? flightId = freezed,
    Object? flightName = freezed,
  }) {
    return _then(_$RoundImpl(
      id: null == id
          ? _value.id
          : id // ignore: cast_nullable_to_non_nullable
              as int,
      courseName: null == courseName
          ? _value.courseName
          : courseName // ignore: cast_nullable_to_non_nullable
              as String,
      scheduledDate: null == scheduledDate
          ? _value.scheduledDate
          : scheduledDate // ignore: cast_nullable_to_non_nullable
              as DateTime,
      playedDate: freezed == playedDate
          ? _value.playedDate
          : playedDate // ignore: cast_nullable_to_non_nullable
              as DateTime?,
      status: null == status
          ? _value.status
          : status // ignore: cast_nullable_to_non_nullable
              as String,
      roundNumber: null == roundNumber
          ? _value.roundNumber
          : roundNumber // ignore: cast_nullable_to_non_nullable
              as int,
      weatherConditions: freezed == weatherConditions
          ? _value.weatherConditions
          : weatherConditions // ignore: cast_nullable_to_non_nullable
              as String?,
      flightId: freezed == flightId
          ? _value.flightId
          : flightId // ignore: cast_nullable_to_non_nullable
              as int?,
      flightName: freezed == flightName
          ? _value.flightName
          : flightName // ignore: cast_nullable_to_non_nullable
              as String?,
    ));
  }
}

/// @nodoc
@JsonSerializable()
class _$RoundImpl implements _Round {
  const _$RoundImpl(
      {required this.id,
      required this.courseName,
      required this.scheduledDate,
      this.playedDate,
      required this.status,
      required this.roundNumber,
      this.weatherConditions,
      this.flightId,
      this.flightName});

  factory _$RoundImpl.fromJson(Map<String, dynamic> json) =>
      _$$RoundImplFromJson(json);

  @override
  final int id;
  @override
  final String courseName;
  @override
  final DateTime scheduledDate;
  @override
  final DateTime? playedDate;
  @override
  final String status;
  @override
  final int roundNumber;
  @override
  final String? weatherConditions;
  @override
  final int? flightId;
  @override
  final String? flightName;

  @override
  String toString() {
    return 'Round(id: $id, courseName: $courseName, scheduledDate: $scheduledDate, playedDate: $playedDate, status: $status, roundNumber: $roundNumber, weatherConditions: $weatherConditions, flightId: $flightId, flightName: $flightName)';
  }

  @override
  bool operator ==(Object other) {
    return identical(this, other) ||
        (other.runtimeType == runtimeType &&
            other is _$RoundImpl &&
            (identical(other.id, id) || other.id == id) &&
            (identical(other.courseName, courseName) ||
                other.courseName == courseName) &&
            (identical(other.scheduledDate, scheduledDate) ||
                other.scheduledDate == scheduledDate) &&
            (identical(other.playedDate, playedDate) ||
                other.playedDate == playedDate) &&
            (identical(other.status, status) || other.status == status) &&
            (identical(other.roundNumber, roundNumber) ||
                other.roundNumber == roundNumber) &&
            (identical(other.weatherConditions, weatherConditions) ||
                other.weatherConditions == weatherConditions) &&
            (identical(other.flightId, flightId) ||
                other.flightId == flightId) &&
            (identical(other.flightName, flightName) ||
                other.flightName == flightName));
  }

  @JsonKey(includeFromJson: false, includeToJson: false)
  @override
  int get hashCode => Object.hash(runtimeType, id, courseName, scheduledDate,
      playedDate, status, roundNumber, weatherConditions, flightId, flightName);

  /// Create a copy of Round
  /// with the given fields replaced by the non-null parameter values.
  @JsonKey(includeFromJson: false, includeToJson: false)
  @override
  @pragma('vm:prefer-inline')
  _$$RoundImplCopyWith<_$RoundImpl> get copyWith =>
      __$$RoundImplCopyWithImpl<_$RoundImpl>(this, _$identity);

  @override
  Map<String, dynamic> toJson() {
    return _$$RoundImplToJson(
      this,
    );
  }
}

abstract class _Round implements Round {
  const factory _Round(
      {required final int id,
      required final String courseName,
      required final DateTime scheduledDate,
      final DateTime? playedDate,
      required final String status,
      required final int roundNumber,
      final String? weatherConditions,
      final int? flightId,
      final String? flightName}) = _$RoundImpl;

  factory _Round.fromJson(Map<String, dynamic> json) = _$RoundImpl.fromJson;

  @override
  int get id;
  @override
  String get courseName;
  @override
  DateTime get scheduledDate;
  @override
  DateTime? get playedDate;
  @override
  String get status;
  @override
  int get roundNumber;
  @override
  String? get weatherConditions;
  @override
  int? get flightId;
  @override
  String? get flightName;

  /// Create a copy of Round
  /// with the given fields replaced by the non-null parameter values.
  @override
  @JsonKey(includeFromJson: false, includeToJson: false)
  _$$RoundImplCopyWith<_$RoundImpl> get copyWith =>
      throw _privateConstructorUsedError;
}

RoundDetail _$RoundDetailFromJson(Map<String, dynamic> json) {
  return _RoundDetail.fromJson(json);
}

/// @nodoc
mixin _$RoundDetail {
  int get id => throw _privateConstructorUsedError;
  String get courseName => throw _privateConstructorUsedError;
  DateTime get scheduledDate => throw _privateConstructorUsedError;
  DateTime? get playedDate => throw _privateConstructorUsedError;
  String get status => throw _privateConstructorUsedError;
  int get roundNumber => throw _privateConstructorUsedError;
  String? get weatherConditions => throw _privateConstructorUsedError;
  List<RoundParticipantSummary> get participants =>
      throw _privateConstructorUsedError;

  /// Serializes this RoundDetail to a JSON map.
  Map<String, dynamic> toJson() => throw _privateConstructorUsedError;

  /// Create a copy of RoundDetail
  /// with the given fields replaced by the non-null parameter values.
  @JsonKey(includeFromJson: false, includeToJson: false)
  $RoundDetailCopyWith<RoundDetail> get copyWith =>
      throw _privateConstructorUsedError;
}

/// @nodoc
abstract class $RoundDetailCopyWith<$Res> {
  factory $RoundDetailCopyWith(
          RoundDetail value, $Res Function(RoundDetail) then) =
      _$RoundDetailCopyWithImpl<$Res, RoundDetail>;
  @useResult
  $Res call(
      {int id,
      String courseName,
      DateTime scheduledDate,
      DateTime? playedDate,
      String status,
      int roundNumber,
      String? weatherConditions,
      List<RoundParticipantSummary> participants});
}

/// @nodoc
class _$RoundDetailCopyWithImpl<$Res, $Val extends RoundDetail>
    implements $RoundDetailCopyWith<$Res> {
  _$RoundDetailCopyWithImpl(this._value, this._then);

  // ignore: unused_field
  final $Val _value;
  // ignore: unused_field
  final $Res Function($Val) _then;

  /// Create a copy of RoundDetail
  /// with the given fields replaced by the non-null parameter values.
  @pragma('vm:prefer-inline')
  @override
  $Res call({
    Object? id = null,
    Object? courseName = null,
    Object? scheduledDate = null,
    Object? playedDate = freezed,
    Object? status = null,
    Object? roundNumber = null,
    Object? weatherConditions = freezed,
    Object? participants = null,
  }) {
    return _then(_value.copyWith(
      id: null == id
          ? _value.id
          : id // ignore: cast_nullable_to_non_nullable
              as int,
      courseName: null == courseName
          ? _value.courseName
          : courseName // ignore: cast_nullable_to_non_nullable
              as String,
      scheduledDate: null == scheduledDate
          ? _value.scheduledDate
          : scheduledDate // ignore: cast_nullable_to_non_nullable
              as DateTime,
      playedDate: freezed == playedDate
          ? _value.playedDate
          : playedDate // ignore: cast_nullable_to_non_nullable
              as DateTime?,
      status: null == status
          ? _value.status
          : status // ignore: cast_nullable_to_non_nullable
              as String,
      roundNumber: null == roundNumber
          ? _value.roundNumber
          : roundNumber // ignore: cast_nullable_to_non_nullable
              as int,
      weatherConditions: freezed == weatherConditions
          ? _value.weatherConditions
          : weatherConditions // ignore: cast_nullable_to_non_nullable
              as String?,
      participants: null == participants
          ? _value.participants
          : participants // ignore: cast_nullable_to_non_nullable
              as List<RoundParticipantSummary>,
    ) as $Val);
  }
}

/// @nodoc
abstract class _$$RoundDetailImplCopyWith<$Res>
    implements $RoundDetailCopyWith<$Res> {
  factory _$$RoundDetailImplCopyWith(
          _$RoundDetailImpl value, $Res Function(_$RoundDetailImpl) then) =
      __$$RoundDetailImplCopyWithImpl<$Res>;
  @override
  @useResult
  $Res call(
      {int id,
      String courseName,
      DateTime scheduledDate,
      DateTime? playedDate,
      String status,
      int roundNumber,
      String? weatherConditions,
      List<RoundParticipantSummary> participants});
}

/// @nodoc
class __$$RoundDetailImplCopyWithImpl<$Res>
    extends _$RoundDetailCopyWithImpl<$Res, _$RoundDetailImpl>
    implements _$$RoundDetailImplCopyWith<$Res> {
  __$$RoundDetailImplCopyWithImpl(
      _$RoundDetailImpl _value, $Res Function(_$RoundDetailImpl) _then)
      : super(_value, _then);

  /// Create a copy of RoundDetail
  /// with the given fields replaced by the non-null parameter values.
  @pragma('vm:prefer-inline')
  @override
  $Res call({
    Object? id = null,
    Object? courseName = null,
    Object? scheduledDate = null,
    Object? playedDate = freezed,
    Object? status = null,
    Object? roundNumber = null,
    Object? weatherConditions = freezed,
    Object? participants = null,
  }) {
    return _then(_$RoundDetailImpl(
      id: null == id
          ? _value.id
          : id // ignore: cast_nullable_to_non_nullable
              as int,
      courseName: null == courseName
          ? _value.courseName
          : courseName // ignore: cast_nullable_to_non_nullable
              as String,
      scheduledDate: null == scheduledDate
          ? _value.scheduledDate
          : scheduledDate // ignore: cast_nullable_to_non_nullable
              as DateTime,
      playedDate: freezed == playedDate
          ? _value.playedDate
          : playedDate // ignore: cast_nullable_to_non_nullable
              as DateTime?,
      status: null == status
          ? _value.status
          : status // ignore: cast_nullable_to_non_nullable
              as String,
      roundNumber: null == roundNumber
          ? _value.roundNumber
          : roundNumber // ignore: cast_nullable_to_non_nullable
              as int,
      weatherConditions: freezed == weatherConditions
          ? _value.weatherConditions
          : weatherConditions // ignore: cast_nullable_to_non_nullable
              as String?,
      participants: null == participants
          ? _value._participants
          : participants // ignore: cast_nullable_to_non_nullable
              as List<RoundParticipantSummary>,
    ));
  }
}

/// @nodoc
@JsonSerializable()
class _$RoundDetailImpl implements _RoundDetail {
  const _$RoundDetailImpl(
      {required this.id,
      required this.courseName,
      required this.scheduledDate,
      this.playedDate,
      required this.status,
      required this.roundNumber,
      this.weatherConditions,
      final List<RoundParticipantSummary> participants = const []})
      : _participants = participants;

  factory _$RoundDetailImpl.fromJson(Map<String, dynamic> json) =>
      _$$RoundDetailImplFromJson(json);

  @override
  final int id;
  @override
  final String courseName;
  @override
  final DateTime scheduledDate;
  @override
  final DateTime? playedDate;
  @override
  final String status;
  @override
  final int roundNumber;
  @override
  final String? weatherConditions;
  final List<RoundParticipantSummary> _participants;
  @override
  @JsonKey()
  List<RoundParticipantSummary> get participants {
    if (_participants is EqualUnmodifiableListView) return _participants;
    // ignore: implicit_dynamic_type
    return EqualUnmodifiableListView(_participants);
  }

  @override
  String toString() {
    return 'RoundDetail(id: $id, courseName: $courseName, scheduledDate: $scheduledDate, playedDate: $playedDate, status: $status, roundNumber: $roundNumber, weatherConditions: $weatherConditions, participants: $participants)';
  }

  @override
  bool operator ==(Object other) {
    return identical(this, other) ||
        (other.runtimeType == runtimeType &&
            other is _$RoundDetailImpl &&
            (identical(other.id, id) || other.id == id) &&
            (identical(other.courseName, courseName) ||
                other.courseName == courseName) &&
            (identical(other.scheduledDate, scheduledDate) ||
                other.scheduledDate == scheduledDate) &&
            (identical(other.playedDate, playedDate) ||
                other.playedDate == playedDate) &&
            (identical(other.status, status) || other.status == status) &&
            (identical(other.roundNumber, roundNumber) ||
                other.roundNumber == roundNumber) &&
            (identical(other.weatherConditions, weatherConditions) ||
                other.weatherConditions == weatherConditions) &&
            const DeepCollectionEquality()
                .equals(other._participants, _participants));
  }

  @JsonKey(includeFromJson: false, includeToJson: false)
  @override
  int get hashCode => Object.hash(
      runtimeType,
      id,
      courseName,
      scheduledDate,
      playedDate,
      status,
      roundNumber,
      weatherConditions,
      const DeepCollectionEquality().hash(_participants));

  /// Create a copy of RoundDetail
  /// with the given fields replaced by the non-null parameter values.
  @JsonKey(includeFromJson: false, includeToJson: false)
  @override
  @pragma('vm:prefer-inline')
  _$$RoundDetailImplCopyWith<_$RoundDetailImpl> get copyWith =>
      __$$RoundDetailImplCopyWithImpl<_$RoundDetailImpl>(this, _$identity);

  @override
  Map<String, dynamic> toJson() {
    return _$$RoundDetailImplToJson(
      this,
    );
  }
}

abstract class _RoundDetail implements RoundDetail {
  const factory _RoundDetail(
      {required final int id,
      required final String courseName,
      required final DateTime scheduledDate,
      final DateTime? playedDate,
      required final String status,
      required final int roundNumber,
      final String? weatherConditions,
      final List<RoundParticipantSummary> participants}) = _$RoundDetailImpl;

  factory _RoundDetail.fromJson(Map<String, dynamic> json) =
      _$RoundDetailImpl.fromJson;

  @override
  int get id;
  @override
  String get courseName;
  @override
  DateTime get scheduledDate;
  @override
  DateTime? get playedDate;
  @override
  String get status;
  @override
  int get roundNumber;
  @override
  String? get weatherConditions;
  @override
  List<RoundParticipantSummary> get participants;

  /// Create a copy of RoundDetail
  /// with the given fields replaced by the non-null parameter values.
  @override
  @JsonKey(includeFromJson: false, includeToJson: false)
  _$$RoundDetailImplCopyWith<_$RoundDetailImpl> get copyWith =>
      throw _privateConstructorUsedError;
}

RoundParticipantSummary _$RoundParticipantSummaryFromJson(
    Map<String, dynamic> json) {
  return _RoundParticipantSummary.fromJson(json);
}

/// @nodoc
mixin _$RoundParticipantSummary {
  int get playerId => throw _privateConstructorUsedError;
  String get playerName => throw _privateConstructorUsedError;
  int? get totalStablefordPoints => throw _privateConstructorUsedError;
  int? get grossTotal => throw _privateConstructorUsedError;
  int? get rank => throw _privateConstructorUsedError;
  double? get courseHandicap => throw _privateConstructorUsedError;

  /// Serializes this RoundParticipantSummary to a JSON map.
  Map<String, dynamic> toJson() => throw _privateConstructorUsedError;

  /// Create a copy of RoundParticipantSummary
  /// with the given fields replaced by the non-null parameter values.
  @JsonKey(includeFromJson: false, includeToJson: false)
  $RoundParticipantSummaryCopyWith<RoundParticipantSummary> get copyWith =>
      throw _privateConstructorUsedError;
}

/// @nodoc
abstract class $RoundParticipantSummaryCopyWith<$Res> {
  factory $RoundParticipantSummaryCopyWith(RoundParticipantSummary value,
          $Res Function(RoundParticipantSummary) then) =
      _$RoundParticipantSummaryCopyWithImpl<$Res, RoundParticipantSummary>;
  @useResult
  $Res call(
      {int playerId,
      String playerName,
      int? totalStablefordPoints,
      int? grossTotal,
      int? rank,
      double? courseHandicap});
}

/// @nodoc
class _$RoundParticipantSummaryCopyWithImpl<$Res,
        $Val extends RoundParticipantSummary>
    implements $RoundParticipantSummaryCopyWith<$Res> {
  _$RoundParticipantSummaryCopyWithImpl(this._value, this._then);

  // ignore: unused_field
  final $Val _value;
  // ignore: unused_field
  final $Res Function($Val) _then;

  /// Create a copy of RoundParticipantSummary
  /// with the given fields replaced by the non-null parameter values.
  @pragma('vm:prefer-inline')
  @override
  $Res call({
    Object? playerId = null,
    Object? playerName = null,
    Object? totalStablefordPoints = freezed,
    Object? grossTotal = freezed,
    Object? rank = freezed,
    Object? courseHandicap = freezed,
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
      totalStablefordPoints: freezed == totalStablefordPoints
          ? _value.totalStablefordPoints
          : totalStablefordPoints // ignore: cast_nullable_to_non_nullable
              as int?,
      grossTotal: freezed == grossTotal
          ? _value.grossTotal
          : grossTotal // ignore: cast_nullable_to_non_nullable
              as int?,
      rank: freezed == rank
          ? _value.rank
          : rank // ignore: cast_nullable_to_non_nullable
              as int?,
      courseHandicap: freezed == courseHandicap
          ? _value.courseHandicap
          : courseHandicap // ignore: cast_nullable_to_non_nullable
              as double?,
    ) as $Val);
  }
}

/// @nodoc
abstract class _$$RoundParticipantSummaryImplCopyWith<$Res>
    implements $RoundParticipantSummaryCopyWith<$Res> {
  factory _$$RoundParticipantSummaryImplCopyWith(
          _$RoundParticipantSummaryImpl value,
          $Res Function(_$RoundParticipantSummaryImpl) then) =
      __$$RoundParticipantSummaryImplCopyWithImpl<$Res>;
  @override
  @useResult
  $Res call(
      {int playerId,
      String playerName,
      int? totalStablefordPoints,
      int? grossTotal,
      int? rank,
      double? courseHandicap});
}

/// @nodoc
class __$$RoundParticipantSummaryImplCopyWithImpl<$Res>
    extends _$RoundParticipantSummaryCopyWithImpl<$Res,
        _$RoundParticipantSummaryImpl>
    implements _$$RoundParticipantSummaryImplCopyWith<$Res> {
  __$$RoundParticipantSummaryImplCopyWithImpl(
      _$RoundParticipantSummaryImpl _value,
      $Res Function(_$RoundParticipantSummaryImpl) _then)
      : super(_value, _then);

  /// Create a copy of RoundParticipantSummary
  /// with the given fields replaced by the non-null parameter values.
  @pragma('vm:prefer-inline')
  @override
  $Res call({
    Object? playerId = null,
    Object? playerName = null,
    Object? totalStablefordPoints = freezed,
    Object? grossTotal = freezed,
    Object? rank = freezed,
    Object? courseHandicap = freezed,
  }) {
    return _then(_$RoundParticipantSummaryImpl(
      playerId: null == playerId
          ? _value.playerId
          : playerId // ignore: cast_nullable_to_non_nullable
              as int,
      playerName: null == playerName
          ? _value.playerName
          : playerName // ignore: cast_nullable_to_non_nullable
              as String,
      totalStablefordPoints: freezed == totalStablefordPoints
          ? _value.totalStablefordPoints
          : totalStablefordPoints // ignore: cast_nullable_to_non_nullable
              as int?,
      grossTotal: freezed == grossTotal
          ? _value.grossTotal
          : grossTotal // ignore: cast_nullable_to_non_nullable
              as int?,
      rank: freezed == rank
          ? _value.rank
          : rank // ignore: cast_nullable_to_non_nullable
              as int?,
      courseHandicap: freezed == courseHandicap
          ? _value.courseHandicap
          : courseHandicap // ignore: cast_nullable_to_non_nullable
              as double?,
    ));
  }
}

/// @nodoc
@JsonSerializable()
class _$RoundParticipantSummaryImpl implements _RoundParticipantSummary {
  const _$RoundParticipantSummaryImpl(
      {required this.playerId,
      required this.playerName,
      this.totalStablefordPoints,
      this.grossTotal,
      this.rank,
      this.courseHandicap});

  factory _$RoundParticipantSummaryImpl.fromJson(Map<String, dynamic> json) =>
      _$$RoundParticipantSummaryImplFromJson(json);

  @override
  final int playerId;
  @override
  final String playerName;
  @override
  final int? totalStablefordPoints;
  @override
  final int? grossTotal;
  @override
  final int? rank;
  @override
  final double? courseHandicap;

  @override
  String toString() {
    return 'RoundParticipantSummary(playerId: $playerId, playerName: $playerName, totalStablefordPoints: $totalStablefordPoints, grossTotal: $grossTotal, rank: $rank, courseHandicap: $courseHandicap)';
  }

  @override
  bool operator ==(Object other) {
    return identical(this, other) ||
        (other.runtimeType == runtimeType &&
            other is _$RoundParticipantSummaryImpl &&
            (identical(other.playerId, playerId) ||
                other.playerId == playerId) &&
            (identical(other.playerName, playerName) ||
                other.playerName == playerName) &&
            (identical(other.totalStablefordPoints, totalStablefordPoints) ||
                other.totalStablefordPoints == totalStablefordPoints) &&
            (identical(other.grossTotal, grossTotal) ||
                other.grossTotal == grossTotal) &&
            (identical(other.rank, rank) || other.rank == rank) &&
            (identical(other.courseHandicap, courseHandicap) ||
                other.courseHandicap == courseHandicap));
  }

  @JsonKey(includeFromJson: false, includeToJson: false)
  @override
  int get hashCode => Object.hash(runtimeType, playerId, playerName,
      totalStablefordPoints, grossTotal, rank, courseHandicap);

  /// Create a copy of RoundParticipantSummary
  /// with the given fields replaced by the non-null parameter values.
  @JsonKey(includeFromJson: false, includeToJson: false)
  @override
  @pragma('vm:prefer-inline')
  _$$RoundParticipantSummaryImplCopyWith<_$RoundParticipantSummaryImpl>
      get copyWith => __$$RoundParticipantSummaryImplCopyWithImpl<
          _$RoundParticipantSummaryImpl>(this, _$identity);

  @override
  Map<String, dynamic> toJson() {
    return _$$RoundParticipantSummaryImplToJson(
      this,
    );
  }
}

abstract class _RoundParticipantSummary implements RoundParticipantSummary {
  const factory _RoundParticipantSummary(
      {required final int playerId,
      required final String playerName,
      final int? totalStablefordPoints,
      final int? grossTotal,
      final int? rank,
      final double? courseHandicap}) = _$RoundParticipantSummaryImpl;

  factory _RoundParticipantSummary.fromJson(Map<String, dynamic> json) =
      _$RoundParticipantSummaryImpl.fromJson;

  @override
  int get playerId;
  @override
  String get playerName;
  @override
  int? get totalStablefordPoints;
  @override
  int? get grossTotal;
  @override
  int? get rank;
  @override
  double? get courseHandicap;

  /// Create a copy of RoundParticipantSummary
  /// with the given fields replaced by the non-null parameter values.
  @override
  @JsonKey(includeFromJson: false, includeToJson: false)
  _$$RoundParticipantSummaryImplCopyWith<_$RoundParticipantSummaryImpl>
      get copyWith => throw _privateConstructorUsedError;
}

HoleScore _$HoleScoreFromJson(Map<String, dynamic> json) {
  return _HoleScore.fromJson(json);
}

/// @nodoc
mixin _$HoleScore {
  int get holeNumber => throw _privateConstructorUsedError;
  int get par => throw _privateConstructorUsedError;
  int get strokeIndex => throw _privateConstructorUsedError;
  int? get grossStrokes => throw _privateConstructorUsedError;
  int get handicapStrokes => throw _privateConstructorUsedError;
  int? get netStrokes => throw _privateConstructorUsedError;
  int? get stablefordPoints => throw _privateConstructorUsedError;
  bool get isMaxScore => throw _privateConstructorUsedError;

  /// Serializes this HoleScore to a JSON map.
  Map<String, dynamic> toJson() => throw _privateConstructorUsedError;

  /// Create a copy of HoleScore
  /// with the given fields replaced by the non-null parameter values.
  @JsonKey(includeFromJson: false, includeToJson: false)
  $HoleScoreCopyWith<HoleScore> get copyWith =>
      throw _privateConstructorUsedError;
}

/// @nodoc
abstract class $HoleScoreCopyWith<$Res> {
  factory $HoleScoreCopyWith(HoleScore value, $Res Function(HoleScore) then) =
      _$HoleScoreCopyWithImpl<$Res, HoleScore>;
  @useResult
  $Res call(
      {int holeNumber,
      int par,
      int strokeIndex,
      int? grossStrokes,
      int handicapStrokes,
      int? netStrokes,
      int? stablefordPoints,
      bool isMaxScore});
}

/// @nodoc
class _$HoleScoreCopyWithImpl<$Res, $Val extends HoleScore>
    implements $HoleScoreCopyWith<$Res> {
  _$HoleScoreCopyWithImpl(this._value, this._then);

  // ignore: unused_field
  final $Val _value;
  // ignore: unused_field
  final $Res Function($Val) _then;

  /// Create a copy of HoleScore
  /// with the given fields replaced by the non-null parameter values.
  @pragma('vm:prefer-inline')
  @override
  $Res call({
    Object? holeNumber = null,
    Object? par = null,
    Object? strokeIndex = null,
    Object? grossStrokes = freezed,
    Object? handicapStrokes = null,
    Object? netStrokes = freezed,
    Object? stablefordPoints = freezed,
    Object? isMaxScore = null,
  }) {
    return _then(_value.copyWith(
      holeNumber: null == holeNumber
          ? _value.holeNumber
          : holeNumber // ignore: cast_nullable_to_non_nullable
              as int,
      par: null == par
          ? _value.par
          : par // ignore: cast_nullable_to_non_nullable
              as int,
      strokeIndex: null == strokeIndex
          ? _value.strokeIndex
          : strokeIndex // ignore: cast_nullable_to_non_nullable
              as int,
      grossStrokes: freezed == grossStrokes
          ? _value.grossStrokes
          : grossStrokes // ignore: cast_nullable_to_non_nullable
              as int?,
      handicapStrokes: null == handicapStrokes
          ? _value.handicapStrokes
          : handicapStrokes // ignore: cast_nullable_to_non_nullable
              as int,
      netStrokes: freezed == netStrokes
          ? _value.netStrokes
          : netStrokes // ignore: cast_nullable_to_non_nullable
              as int?,
      stablefordPoints: freezed == stablefordPoints
          ? _value.stablefordPoints
          : stablefordPoints // ignore: cast_nullable_to_non_nullable
              as int?,
      isMaxScore: null == isMaxScore
          ? _value.isMaxScore
          : isMaxScore // ignore: cast_nullable_to_non_nullable
              as bool,
    ) as $Val);
  }
}

/// @nodoc
abstract class _$$HoleScoreImplCopyWith<$Res>
    implements $HoleScoreCopyWith<$Res> {
  factory _$$HoleScoreImplCopyWith(
          _$HoleScoreImpl value, $Res Function(_$HoleScoreImpl) then) =
      __$$HoleScoreImplCopyWithImpl<$Res>;
  @override
  @useResult
  $Res call(
      {int holeNumber,
      int par,
      int strokeIndex,
      int? grossStrokes,
      int handicapStrokes,
      int? netStrokes,
      int? stablefordPoints,
      bool isMaxScore});
}

/// @nodoc
class __$$HoleScoreImplCopyWithImpl<$Res>
    extends _$HoleScoreCopyWithImpl<$Res, _$HoleScoreImpl>
    implements _$$HoleScoreImplCopyWith<$Res> {
  __$$HoleScoreImplCopyWithImpl(
      _$HoleScoreImpl _value, $Res Function(_$HoleScoreImpl) _then)
      : super(_value, _then);

  /// Create a copy of HoleScore
  /// with the given fields replaced by the non-null parameter values.
  @pragma('vm:prefer-inline')
  @override
  $Res call({
    Object? holeNumber = null,
    Object? par = null,
    Object? strokeIndex = null,
    Object? grossStrokes = freezed,
    Object? handicapStrokes = null,
    Object? netStrokes = freezed,
    Object? stablefordPoints = freezed,
    Object? isMaxScore = null,
  }) {
    return _then(_$HoleScoreImpl(
      holeNumber: null == holeNumber
          ? _value.holeNumber
          : holeNumber // ignore: cast_nullable_to_non_nullable
              as int,
      par: null == par
          ? _value.par
          : par // ignore: cast_nullable_to_non_nullable
              as int,
      strokeIndex: null == strokeIndex
          ? _value.strokeIndex
          : strokeIndex // ignore: cast_nullable_to_non_nullable
              as int,
      grossStrokes: freezed == grossStrokes
          ? _value.grossStrokes
          : grossStrokes // ignore: cast_nullable_to_non_nullable
              as int?,
      handicapStrokes: null == handicapStrokes
          ? _value.handicapStrokes
          : handicapStrokes // ignore: cast_nullable_to_non_nullable
              as int,
      netStrokes: freezed == netStrokes
          ? _value.netStrokes
          : netStrokes // ignore: cast_nullable_to_non_nullable
              as int?,
      stablefordPoints: freezed == stablefordPoints
          ? _value.stablefordPoints
          : stablefordPoints // ignore: cast_nullable_to_non_nullable
              as int?,
      isMaxScore: null == isMaxScore
          ? _value.isMaxScore
          : isMaxScore // ignore: cast_nullable_to_non_nullable
              as bool,
    ));
  }
}

/// @nodoc
@JsonSerializable()
class _$HoleScoreImpl implements _HoleScore {
  const _$HoleScoreImpl(
      {required this.holeNumber,
      required this.par,
      required this.strokeIndex,
      this.grossStrokes,
      required this.handicapStrokes,
      this.netStrokes,
      this.stablefordPoints,
      this.isMaxScore = false});

  factory _$HoleScoreImpl.fromJson(Map<String, dynamic> json) =>
      _$$HoleScoreImplFromJson(json);

  @override
  final int holeNumber;
  @override
  final int par;
  @override
  final int strokeIndex;
  @override
  final int? grossStrokes;
  @override
  final int handicapStrokes;
  @override
  final int? netStrokes;
  @override
  final int? stablefordPoints;
  @override
  @JsonKey()
  final bool isMaxScore;

  @override
  String toString() {
    return 'HoleScore(holeNumber: $holeNumber, par: $par, strokeIndex: $strokeIndex, grossStrokes: $grossStrokes, handicapStrokes: $handicapStrokes, netStrokes: $netStrokes, stablefordPoints: $stablefordPoints, isMaxScore: $isMaxScore)';
  }

  @override
  bool operator ==(Object other) {
    return identical(this, other) ||
        (other.runtimeType == runtimeType &&
            other is _$HoleScoreImpl &&
            (identical(other.holeNumber, holeNumber) ||
                other.holeNumber == holeNumber) &&
            (identical(other.par, par) || other.par == par) &&
            (identical(other.strokeIndex, strokeIndex) ||
                other.strokeIndex == strokeIndex) &&
            (identical(other.grossStrokes, grossStrokes) ||
                other.grossStrokes == grossStrokes) &&
            (identical(other.handicapStrokes, handicapStrokes) ||
                other.handicapStrokes == handicapStrokes) &&
            (identical(other.netStrokes, netStrokes) ||
                other.netStrokes == netStrokes) &&
            (identical(other.stablefordPoints, stablefordPoints) ||
                other.stablefordPoints == stablefordPoints) &&
            (identical(other.isMaxScore, isMaxScore) ||
                other.isMaxScore == isMaxScore));
  }

  @JsonKey(includeFromJson: false, includeToJson: false)
  @override
  int get hashCode => Object.hash(runtimeType, holeNumber, par, strokeIndex,
      grossStrokes, handicapStrokes, netStrokes, stablefordPoints, isMaxScore);

  /// Create a copy of HoleScore
  /// with the given fields replaced by the non-null parameter values.
  @JsonKey(includeFromJson: false, includeToJson: false)
  @override
  @pragma('vm:prefer-inline')
  _$$HoleScoreImplCopyWith<_$HoleScoreImpl> get copyWith =>
      __$$HoleScoreImplCopyWithImpl<_$HoleScoreImpl>(this, _$identity);

  @override
  Map<String, dynamic> toJson() {
    return _$$HoleScoreImplToJson(
      this,
    );
  }
}

abstract class _HoleScore implements HoleScore {
  const factory _HoleScore(
      {required final int holeNumber,
      required final int par,
      required final int strokeIndex,
      final int? grossStrokes,
      required final int handicapStrokes,
      final int? netStrokes,
      final int? stablefordPoints,
      final bool isMaxScore}) = _$HoleScoreImpl;

  factory _HoleScore.fromJson(Map<String, dynamic> json) =
      _$HoleScoreImpl.fromJson;

  @override
  int get holeNumber;
  @override
  int get par;
  @override
  int get strokeIndex;
  @override
  int? get grossStrokes;
  @override
  int get handicapStrokes;
  @override
  int? get netStrokes;
  @override
  int? get stablefordPoints;
  @override
  bool get isMaxScore;

  /// Create a copy of HoleScore
  /// with the given fields replaced by the non-null parameter values.
  @override
  @JsonKey(includeFromJson: false, includeToJson: false)
  _$$HoleScoreImplCopyWith<_$HoleScoreImpl> get copyWith =>
      throw _privateConstructorUsedError;
}

PlayerScorecard _$PlayerScorecardFromJson(Map<String, dynamic> json) {
  return _PlayerScorecard.fromJson(json);
}

/// @nodoc
mixin _$PlayerScorecard {
  int get roundId => throw _privateConstructorUsedError;
  int get playerId => throw _privateConstructorUsedError;
  String get playerName => throw _privateConstructorUsedError;
  double get courseHandicap => throw _privateConstructorUsedError;
  List<HoleScore> get holes => throw _privateConstructorUsedError;
  int? get totalGross => throw _privateConstructorUsedError;
  int? get totalNet => throw _privateConstructorUsedError;
  int? get totalStableford => throw _privateConstructorUsedError;

  /// Serializes this PlayerScorecard to a JSON map.
  Map<String, dynamic> toJson() => throw _privateConstructorUsedError;

  /// Create a copy of PlayerScorecard
  /// with the given fields replaced by the non-null parameter values.
  @JsonKey(includeFromJson: false, includeToJson: false)
  $PlayerScorecardCopyWith<PlayerScorecard> get copyWith =>
      throw _privateConstructorUsedError;
}

/// @nodoc
abstract class $PlayerScorecardCopyWith<$Res> {
  factory $PlayerScorecardCopyWith(
          PlayerScorecard value, $Res Function(PlayerScorecard) then) =
      _$PlayerScorecardCopyWithImpl<$Res, PlayerScorecard>;
  @useResult
  $Res call(
      {int roundId,
      int playerId,
      String playerName,
      double courseHandicap,
      List<HoleScore> holes,
      int? totalGross,
      int? totalNet,
      int? totalStableford});
}

/// @nodoc
class _$PlayerScorecardCopyWithImpl<$Res, $Val extends PlayerScorecard>
    implements $PlayerScorecardCopyWith<$Res> {
  _$PlayerScorecardCopyWithImpl(this._value, this._then);

  // ignore: unused_field
  final $Val _value;
  // ignore: unused_field
  final $Res Function($Val) _then;

  /// Create a copy of PlayerScorecard
  /// with the given fields replaced by the non-null parameter values.
  @pragma('vm:prefer-inline')
  @override
  $Res call({
    Object? roundId = null,
    Object? playerId = null,
    Object? playerName = null,
    Object? courseHandicap = null,
    Object? holes = null,
    Object? totalGross = freezed,
    Object? totalNet = freezed,
    Object? totalStableford = freezed,
  }) {
    return _then(_value.copyWith(
      roundId: null == roundId
          ? _value.roundId
          : roundId // ignore: cast_nullable_to_non_nullable
              as int,
      playerId: null == playerId
          ? _value.playerId
          : playerId // ignore: cast_nullable_to_non_nullable
              as int,
      playerName: null == playerName
          ? _value.playerName
          : playerName // ignore: cast_nullable_to_non_nullable
              as String,
      courseHandicap: null == courseHandicap
          ? _value.courseHandicap
          : courseHandicap // ignore: cast_nullable_to_non_nullable
              as double,
      holes: null == holes
          ? _value.holes
          : holes // ignore: cast_nullable_to_non_nullable
              as List<HoleScore>,
      totalGross: freezed == totalGross
          ? _value.totalGross
          : totalGross // ignore: cast_nullable_to_non_nullable
              as int?,
      totalNet: freezed == totalNet
          ? _value.totalNet
          : totalNet // ignore: cast_nullable_to_non_nullable
              as int?,
      totalStableford: freezed == totalStableford
          ? _value.totalStableford
          : totalStableford // ignore: cast_nullable_to_non_nullable
              as int?,
    ) as $Val);
  }
}

/// @nodoc
abstract class _$$PlayerScorecardImplCopyWith<$Res>
    implements $PlayerScorecardCopyWith<$Res> {
  factory _$$PlayerScorecardImplCopyWith(_$PlayerScorecardImpl value,
          $Res Function(_$PlayerScorecardImpl) then) =
      __$$PlayerScorecardImplCopyWithImpl<$Res>;
  @override
  @useResult
  $Res call(
      {int roundId,
      int playerId,
      String playerName,
      double courseHandicap,
      List<HoleScore> holes,
      int? totalGross,
      int? totalNet,
      int? totalStableford});
}

/// @nodoc
class __$$PlayerScorecardImplCopyWithImpl<$Res>
    extends _$PlayerScorecardCopyWithImpl<$Res, _$PlayerScorecardImpl>
    implements _$$PlayerScorecardImplCopyWith<$Res> {
  __$$PlayerScorecardImplCopyWithImpl(
      _$PlayerScorecardImpl _value, $Res Function(_$PlayerScorecardImpl) _then)
      : super(_value, _then);

  /// Create a copy of PlayerScorecard
  /// with the given fields replaced by the non-null parameter values.
  @pragma('vm:prefer-inline')
  @override
  $Res call({
    Object? roundId = null,
    Object? playerId = null,
    Object? playerName = null,
    Object? courseHandicap = null,
    Object? holes = null,
    Object? totalGross = freezed,
    Object? totalNet = freezed,
    Object? totalStableford = freezed,
  }) {
    return _then(_$PlayerScorecardImpl(
      roundId: null == roundId
          ? _value.roundId
          : roundId // ignore: cast_nullable_to_non_nullable
              as int,
      playerId: null == playerId
          ? _value.playerId
          : playerId // ignore: cast_nullable_to_non_nullable
              as int,
      playerName: null == playerName
          ? _value.playerName
          : playerName // ignore: cast_nullable_to_non_nullable
              as String,
      courseHandicap: null == courseHandicap
          ? _value.courseHandicap
          : courseHandicap // ignore: cast_nullable_to_non_nullable
              as double,
      holes: null == holes
          ? _value._holes
          : holes // ignore: cast_nullable_to_non_nullable
              as List<HoleScore>,
      totalGross: freezed == totalGross
          ? _value.totalGross
          : totalGross // ignore: cast_nullable_to_non_nullable
              as int?,
      totalNet: freezed == totalNet
          ? _value.totalNet
          : totalNet // ignore: cast_nullable_to_non_nullable
              as int?,
      totalStableford: freezed == totalStableford
          ? _value.totalStableford
          : totalStableford // ignore: cast_nullable_to_non_nullable
              as int?,
    ));
  }
}

/// @nodoc
@JsonSerializable()
class _$PlayerScorecardImpl implements _PlayerScorecard {
  const _$PlayerScorecardImpl(
      {required this.roundId,
      required this.playerId,
      required this.playerName,
      required this.courseHandicap,
      final List<HoleScore> holes = const [],
      this.totalGross,
      this.totalNet,
      this.totalStableford})
      : _holes = holes;

  factory _$PlayerScorecardImpl.fromJson(Map<String, dynamic> json) =>
      _$$PlayerScorecardImplFromJson(json);

  @override
  final int roundId;
  @override
  final int playerId;
  @override
  final String playerName;
  @override
  final double courseHandicap;
  final List<HoleScore> _holes;
  @override
  @JsonKey()
  List<HoleScore> get holes {
    if (_holes is EqualUnmodifiableListView) return _holes;
    // ignore: implicit_dynamic_type
    return EqualUnmodifiableListView(_holes);
  }

  @override
  final int? totalGross;
  @override
  final int? totalNet;
  @override
  final int? totalStableford;

  @override
  String toString() {
    return 'PlayerScorecard(roundId: $roundId, playerId: $playerId, playerName: $playerName, courseHandicap: $courseHandicap, holes: $holes, totalGross: $totalGross, totalNet: $totalNet, totalStableford: $totalStableford)';
  }

  @override
  bool operator ==(Object other) {
    return identical(this, other) ||
        (other.runtimeType == runtimeType &&
            other is _$PlayerScorecardImpl &&
            (identical(other.roundId, roundId) || other.roundId == roundId) &&
            (identical(other.playerId, playerId) ||
                other.playerId == playerId) &&
            (identical(other.playerName, playerName) ||
                other.playerName == playerName) &&
            (identical(other.courseHandicap, courseHandicap) ||
                other.courseHandicap == courseHandicap) &&
            const DeepCollectionEquality().equals(other._holes, _holes) &&
            (identical(other.totalGross, totalGross) ||
                other.totalGross == totalGross) &&
            (identical(other.totalNet, totalNet) ||
                other.totalNet == totalNet) &&
            (identical(other.totalStableford, totalStableford) ||
                other.totalStableford == totalStableford));
  }

  @JsonKey(includeFromJson: false, includeToJson: false)
  @override
  int get hashCode => Object.hash(
      runtimeType,
      roundId,
      playerId,
      playerName,
      courseHandicap,
      const DeepCollectionEquality().hash(_holes),
      totalGross,
      totalNet,
      totalStableford);

  /// Create a copy of PlayerScorecard
  /// with the given fields replaced by the non-null parameter values.
  @JsonKey(includeFromJson: false, includeToJson: false)
  @override
  @pragma('vm:prefer-inline')
  _$$PlayerScorecardImplCopyWith<_$PlayerScorecardImpl> get copyWith =>
      __$$PlayerScorecardImplCopyWithImpl<_$PlayerScorecardImpl>(
          this, _$identity);

  @override
  Map<String, dynamic> toJson() {
    return _$$PlayerScorecardImplToJson(
      this,
    );
  }
}

abstract class _PlayerScorecard implements PlayerScorecard {
  const factory _PlayerScorecard(
      {required final int roundId,
      required final int playerId,
      required final String playerName,
      required final double courseHandicap,
      final List<HoleScore> holes,
      final int? totalGross,
      final int? totalNet,
      final int? totalStableford}) = _$PlayerScorecardImpl;

  factory _PlayerScorecard.fromJson(Map<String, dynamic> json) =
      _$PlayerScorecardImpl.fromJson;

  @override
  int get roundId;
  @override
  int get playerId;
  @override
  String get playerName;
  @override
  double get courseHandicap;
  @override
  List<HoleScore> get holes;
  @override
  int? get totalGross;
  @override
  int? get totalNet;
  @override
  int? get totalStableford;

  /// Create a copy of PlayerScorecard
  /// with the given fields replaced by the non-null parameter values.
  @override
  @JsonKey(includeFromJson: false, includeToJson: false)
  _$$PlayerScorecardImplCopyWith<_$PlayerScorecardImpl> get copyWith =>
      throw _privateConstructorUsedError;
}
