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

ScoreEntryHole _$ScoreEntryHoleFromJson(Map<String, dynamic> json) {
  return _ScoreEntryHole.fromJson(json);
}

/// @nodoc
mixin _$ScoreEntryHole {
  int get holeNumber => throw _privateConstructorUsedError;
  int get par => throw _privateConstructorUsedError;
  int get strokeIndex => throw _privateConstructorUsedError;
  int get strokesReceived => throw _privateConstructorUsedError;
  int? get grossStrokes => throw _privateConstructorUsedError;
  int? get stablefordPoints => throw _privateConstructorUsedError;

  /// Serializes this ScoreEntryHole to a JSON map.
  Map<String, dynamic> toJson() => throw _privateConstructorUsedError;

  /// Create a copy of ScoreEntryHole
  /// with the given fields replaced by the non-null parameter values.
  @JsonKey(includeFromJson: false, includeToJson: false)
  $ScoreEntryHoleCopyWith<ScoreEntryHole> get copyWith =>
      throw _privateConstructorUsedError;
}

/// @nodoc
abstract class $ScoreEntryHoleCopyWith<$Res> {
  factory $ScoreEntryHoleCopyWith(
          ScoreEntryHole value, $Res Function(ScoreEntryHole) then) =
      _$ScoreEntryHoleCopyWithImpl<$Res, ScoreEntryHole>;
  @useResult
  $Res call(
      {int holeNumber,
      int par,
      int strokeIndex,
      int strokesReceived,
      int? grossStrokes,
      int? stablefordPoints});
}

/// @nodoc
class _$ScoreEntryHoleCopyWithImpl<$Res, $Val extends ScoreEntryHole>
    implements $ScoreEntryHoleCopyWith<$Res> {
  _$ScoreEntryHoleCopyWithImpl(this._value, this._then);

  // ignore: unused_field
  final $Val _value;
  // ignore: unused_field
  final $Res Function($Val) _then;

  /// Create a copy of ScoreEntryHole
  /// with the given fields replaced by the non-null parameter values.
  @pragma('vm:prefer-inline')
  @override
  $Res call({
    Object? holeNumber = null,
    Object? par = null,
    Object? strokeIndex = null,
    Object? strokesReceived = null,
    Object? grossStrokes = freezed,
    Object? stablefordPoints = freezed,
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
      strokesReceived: null == strokesReceived
          ? _value.strokesReceived
          : strokesReceived // ignore: cast_nullable_to_non_nullable
              as int,
      grossStrokes: freezed == grossStrokes
          ? _value.grossStrokes
          : grossStrokes // ignore: cast_nullable_to_non_nullable
              as int?,
      stablefordPoints: freezed == stablefordPoints
          ? _value.stablefordPoints
          : stablefordPoints // ignore: cast_nullable_to_non_nullable
              as int?,
    ) as $Val);
  }
}

/// @nodoc
abstract class _$$ScoreEntryHoleImplCopyWith<$Res>
    implements $ScoreEntryHoleCopyWith<$Res> {
  factory _$$ScoreEntryHoleImplCopyWith(_$ScoreEntryHoleImpl value,
          $Res Function(_$ScoreEntryHoleImpl) then) =
      __$$ScoreEntryHoleImplCopyWithImpl<$Res>;
  @override
  @useResult
  $Res call(
      {int holeNumber,
      int par,
      int strokeIndex,
      int strokesReceived,
      int? grossStrokes,
      int? stablefordPoints});
}

/// @nodoc
class __$$ScoreEntryHoleImplCopyWithImpl<$Res>
    extends _$ScoreEntryHoleCopyWithImpl<$Res, _$ScoreEntryHoleImpl>
    implements _$$ScoreEntryHoleImplCopyWith<$Res> {
  __$$ScoreEntryHoleImplCopyWithImpl(
      _$ScoreEntryHoleImpl _value, $Res Function(_$ScoreEntryHoleImpl) _then)
      : super(_value, _then);

  /// Create a copy of ScoreEntryHole
  /// with the given fields replaced by the non-null parameter values.
  @pragma('vm:prefer-inline')
  @override
  $Res call({
    Object? holeNumber = null,
    Object? par = null,
    Object? strokeIndex = null,
    Object? strokesReceived = null,
    Object? grossStrokes = freezed,
    Object? stablefordPoints = freezed,
  }) {
    return _then(_$ScoreEntryHoleImpl(
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
      strokesReceived: null == strokesReceived
          ? _value.strokesReceived
          : strokesReceived // ignore: cast_nullable_to_non_nullable
              as int,
      grossStrokes: freezed == grossStrokes
          ? _value.grossStrokes
          : grossStrokes // ignore: cast_nullable_to_non_nullable
              as int?,
      stablefordPoints: freezed == stablefordPoints
          ? _value.stablefordPoints
          : stablefordPoints // ignore: cast_nullable_to_non_nullable
              as int?,
    ));
  }
}

/// @nodoc
@JsonSerializable()
class _$ScoreEntryHoleImpl implements _ScoreEntryHole {
  const _$ScoreEntryHoleImpl(
      {required this.holeNumber,
      required this.par,
      required this.strokeIndex,
      required this.strokesReceived,
      this.grossStrokes,
      this.stablefordPoints});

  factory _$ScoreEntryHoleImpl.fromJson(Map<String, dynamic> json) =>
      _$$ScoreEntryHoleImplFromJson(json);

  @override
  final int holeNumber;
  @override
  final int par;
  @override
  final int strokeIndex;
  @override
  final int strokesReceived;
  @override
  final int? grossStrokes;
  @override
  final int? stablefordPoints;

  @override
  String toString() {
    return 'ScoreEntryHole(holeNumber: $holeNumber, par: $par, strokeIndex: $strokeIndex, strokesReceived: $strokesReceived, grossStrokes: $grossStrokes, stablefordPoints: $stablefordPoints)';
  }

  @override
  bool operator ==(Object other) {
    return identical(this, other) ||
        (other.runtimeType == runtimeType &&
            other is _$ScoreEntryHoleImpl &&
            (identical(other.holeNumber, holeNumber) ||
                other.holeNumber == holeNumber) &&
            (identical(other.par, par) || other.par == par) &&
            (identical(other.strokeIndex, strokeIndex) ||
                other.strokeIndex == strokeIndex) &&
            (identical(other.strokesReceived, strokesReceived) ||
                other.strokesReceived == strokesReceived) &&
            (identical(other.grossStrokes, grossStrokes) ||
                other.grossStrokes == grossStrokes) &&
            (identical(other.stablefordPoints, stablefordPoints) ||
                other.stablefordPoints == stablefordPoints));
  }

  @JsonKey(includeFromJson: false, includeToJson: false)
  @override
  int get hashCode => Object.hash(runtimeType, holeNumber, par, strokeIndex,
      strokesReceived, grossStrokes, stablefordPoints);

  /// Create a copy of ScoreEntryHole
  /// with the given fields replaced by the non-null parameter values.
  @JsonKey(includeFromJson: false, includeToJson: false)
  @override
  @pragma('vm:prefer-inline')
  _$$ScoreEntryHoleImplCopyWith<_$ScoreEntryHoleImpl> get copyWith =>
      __$$ScoreEntryHoleImplCopyWithImpl<_$ScoreEntryHoleImpl>(
          this, _$identity);

  @override
  Map<String, dynamic> toJson() {
    return _$$ScoreEntryHoleImplToJson(
      this,
    );
  }
}

abstract class _ScoreEntryHole implements ScoreEntryHole {
  const factory _ScoreEntryHole(
      {required final int holeNumber,
      required final int par,
      required final int strokeIndex,
      required final int strokesReceived,
      final int? grossStrokes,
      final int? stablefordPoints}) = _$ScoreEntryHoleImpl;

  factory _ScoreEntryHole.fromJson(Map<String, dynamic> json) =
      _$ScoreEntryHoleImpl.fromJson;

  @override
  int get holeNumber;
  @override
  int get par;
  @override
  int get strokeIndex;
  @override
  int get strokesReceived;
  @override
  int? get grossStrokes;
  @override
  int? get stablefordPoints;

  /// Create a copy of ScoreEntryHole
  /// with the given fields replaced by the non-null parameter values.
  @override
  @JsonKey(includeFromJson: false, includeToJson: false)
  _$$ScoreEntryHoleImplCopyWith<_$ScoreEntryHoleImpl> get copyWith =>
      throw _privateConstructorUsedError;
}

ScoreSubmission _$ScoreSubmissionFromJson(Map<String, dynamic> json) {
  return _ScoreSubmission.fromJson(json);
}

/// @nodoc
mixin _$ScoreSubmission {
  int get playerId => throw _privateConstructorUsedError;
  List<HoleSubmission> get holes => throw _privateConstructorUsedError;

  /// Serializes this ScoreSubmission to a JSON map.
  Map<String, dynamic> toJson() => throw _privateConstructorUsedError;

  /// Create a copy of ScoreSubmission
  /// with the given fields replaced by the non-null parameter values.
  @JsonKey(includeFromJson: false, includeToJson: false)
  $ScoreSubmissionCopyWith<ScoreSubmission> get copyWith =>
      throw _privateConstructorUsedError;
}

/// @nodoc
abstract class $ScoreSubmissionCopyWith<$Res> {
  factory $ScoreSubmissionCopyWith(
          ScoreSubmission value, $Res Function(ScoreSubmission) then) =
      _$ScoreSubmissionCopyWithImpl<$Res, ScoreSubmission>;
  @useResult
  $Res call({int playerId, List<HoleSubmission> holes});
}

/// @nodoc
class _$ScoreSubmissionCopyWithImpl<$Res, $Val extends ScoreSubmission>
    implements $ScoreSubmissionCopyWith<$Res> {
  _$ScoreSubmissionCopyWithImpl(this._value, this._then);

  // ignore: unused_field
  final $Val _value;
  // ignore: unused_field
  final $Res Function($Val) _then;

  /// Create a copy of ScoreSubmission
  /// with the given fields replaced by the non-null parameter values.
  @pragma('vm:prefer-inline')
  @override
  $Res call({
    Object? playerId = null,
    Object? holes = null,
  }) {
    return _then(_value.copyWith(
      playerId: null == playerId
          ? _value.playerId
          : playerId // ignore: cast_nullable_to_non_nullable
              as int,
      holes: null == holes
          ? _value.holes
          : holes // ignore: cast_nullable_to_non_nullable
              as List<HoleSubmission>,
    ) as $Val);
  }
}

/// @nodoc
abstract class _$$ScoreSubmissionImplCopyWith<$Res>
    implements $ScoreSubmissionCopyWith<$Res> {
  factory _$$ScoreSubmissionImplCopyWith(_$ScoreSubmissionImpl value,
          $Res Function(_$ScoreSubmissionImpl) then) =
      __$$ScoreSubmissionImplCopyWithImpl<$Res>;
  @override
  @useResult
  $Res call({int playerId, List<HoleSubmission> holes});
}

/// @nodoc
class __$$ScoreSubmissionImplCopyWithImpl<$Res>
    extends _$ScoreSubmissionCopyWithImpl<$Res, _$ScoreSubmissionImpl>
    implements _$$ScoreSubmissionImplCopyWith<$Res> {
  __$$ScoreSubmissionImplCopyWithImpl(
      _$ScoreSubmissionImpl _value, $Res Function(_$ScoreSubmissionImpl) _then)
      : super(_value, _then);

  /// Create a copy of ScoreSubmission
  /// with the given fields replaced by the non-null parameter values.
  @pragma('vm:prefer-inline')
  @override
  $Res call({
    Object? playerId = null,
    Object? holes = null,
  }) {
    return _then(_$ScoreSubmissionImpl(
      playerId: null == playerId
          ? _value.playerId
          : playerId // ignore: cast_nullable_to_non_nullable
              as int,
      holes: null == holes
          ? _value._holes
          : holes // ignore: cast_nullable_to_non_nullable
              as List<HoleSubmission>,
    ));
  }
}

/// @nodoc
@JsonSerializable()
class _$ScoreSubmissionImpl implements _ScoreSubmission {
  const _$ScoreSubmissionImpl(
      {required this.playerId, required final List<HoleSubmission> holes})
      : _holes = holes;

  factory _$ScoreSubmissionImpl.fromJson(Map<String, dynamic> json) =>
      _$$ScoreSubmissionImplFromJson(json);

  @override
  final int playerId;
  final List<HoleSubmission> _holes;
  @override
  List<HoleSubmission> get holes {
    if (_holes is EqualUnmodifiableListView) return _holes;
    // ignore: implicit_dynamic_type
    return EqualUnmodifiableListView(_holes);
  }

  @override
  String toString() {
    return 'ScoreSubmission(playerId: $playerId, holes: $holes)';
  }

  @override
  bool operator ==(Object other) {
    return identical(this, other) ||
        (other.runtimeType == runtimeType &&
            other is _$ScoreSubmissionImpl &&
            (identical(other.playerId, playerId) ||
                other.playerId == playerId) &&
            const DeepCollectionEquality().equals(other._holes, _holes));
  }

  @JsonKey(includeFromJson: false, includeToJson: false)
  @override
  int get hashCode => Object.hash(
      runtimeType, playerId, const DeepCollectionEquality().hash(_holes));

  /// Create a copy of ScoreSubmission
  /// with the given fields replaced by the non-null parameter values.
  @JsonKey(includeFromJson: false, includeToJson: false)
  @override
  @pragma('vm:prefer-inline')
  _$$ScoreSubmissionImplCopyWith<_$ScoreSubmissionImpl> get copyWith =>
      __$$ScoreSubmissionImplCopyWithImpl<_$ScoreSubmissionImpl>(
          this, _$identity);

  @override
  Map<String, dynamic> toJson() {
    return _$$ScoreSubmissionImplToJson(
      this,
    );
  }
}

abstract class _ScoreSubmission implements ScoreSubmission {
  const factory _ScoreSubmission(
      {required final int playerId,
      required final List<HoleSubmission> holes}) = _$ScoreSubmissionImpl;

  factory _ScoreSubmission.fromJson(Map<String, dynamic> json) =
      _$ScoreSubmissionImpl.fromJson;

  @override
  int get playerId;
  @override
  List<HoleSubmission> get holes;

  /// Create a copy of ScoreSubmission
  /// with the given fields replaced by the non-null parameter values.
  @override
  @JsonKey(includeFromJson: false, includeToJson: false)
  _$$ScoreSubmissionImplCopyWith<_$ScoreSubmissionImpl> get copyWith =>
      throw _privateConstructorUsedError;
}

HoleSubmission _$HoleSubmissionFromJson(Map<String, dynamic> json) {
  return _HoleSubmission.fromJson(json);
}

/// @nodoc
mixin _$HoleSubmission {
  int get holeNumber => throw _privateConstructorUsedError;
  int get grossStrokes => throw _privateConstructorUsedError;

  /// Serializes this HoleSubmission to a JSON map.
  Map<String, dynamic> toJson() => throw _privateConstructorUsedError;

  /// Create a copy of HoleSubmission
  /// with the given fields replaced by the non-null parameter values.
  @JsonKey(includeFromJson: false, includeToJson: false)
  $HoleSubmissionCopyWith<HoleSubmission> get copyWith =>
      throw _privateConstructorUsedError;
}

/// @nodoc
abstract class $HoleSubmissionCopyWith<$Res> {
  factory $HoleSubmissionCopyWith(
          HoleSubmission value, $Res Function(HoleSubmission) then) =
      _$HoleSubmissionCopyWithImpl<$Res, HoleSubmission>;
  @useResult
  $Res call({int holeNumber, int grossStrokes});
}

/// @nodoc
class _$HoleSubmissionCopyWithImpl<$Res, $Val extends HoleSubmission>
    implements $HoleSubmissionCopyWith<$Res> {
  _$HoleSubmissionCopyWithImpl(this._value, this._then);

  // ignore: unused_field
  final $Val _value;
  // ignore: unused_field
  final $Res Function($Val) _then;

  /// Create a copy of HoleSubmission
  /// with the given fields replaced by the non-null parameter values.
  @pragma('vm:prefer-inline')
  @override
  $Res call({
    Object? holeNumber = null,
    Object? grossStrokes = null,
  }) {
    return _then(_value.copyWith(
      holeNumber: null == holeNumber
          ? _value.holeNumber
          : holeNumber // ignore: cast_nullable_to_non_nullable
              as int,
      grossStrokes: null == grossStrokes
          ? _value.grossStrokes
          : grossStrokes // ignore: cast_nullable_to_non_nullable
              as int,
    ) as $Val);
  }
}

/// @nodoc
abstract class _$$HoleSubmissionImplCopyWith<$Res>
    implements $HoleSubmissionCopyWith<$Res> {
  factory _$$HoleSubmissionImplCopyWith(_$HoleSubmissionImpl value,
          $Res Function(_$HoleSubmissionImpl) then) =
      __$$HoleSubmissionImplCopyWithImpl<$Res>;
  @override
  @useResult
  $Res call({int holeNumber, int grossStrokes});
}

/// @nodoc
class __$$HoleSubmissionImplCopyWithImpl<$Res>
    extends _$HoleSubmissionCopyWithImpl<$Res, _$HoleSubmissionImpl>
    implements _$$HoleSubmissionImplCopyWith<$Res> {
  __$$HoleSubmissionImplCopyWithImpl(
      _$HoleSubmissionImpl _value, $Res Function(_$HoleSubmissionImpl) _then)
      : super(_value, _then);

  /// Create a copy of HoleSubmission
  /// with the given fields replaced by the non-null parameter values.
  @pragma('vm:prefer-inline')
  @override
  $Res call({
    Object? holeNumber = null,
    Object? grossStrokes = null,
  }) {
    return _then(_$HoleSubmissionImpl(
      holeNumber: null == holeNumber
          ? _value.holeNumber
          : holeNumber // ignore: cast_nullable_to_non_nullable
              as int,
      grossStrokes: null == grossStrokes
          ? _value.grossStrokes
          : grossStrokes // ignore: cast_nullable_to_non_nullable
              as int,
    ));
  }
}

/// @nodoc
@JsonSerializable()
class _$HoleSubmissionImpl implements _HoleSubmission {
  const _$HoleSubmissionImpl(
      {required this.holeNumber, required this.grossStrokes});

  factory _$HoleSubmissionImpl.fromJson(Map<String, dynamic> json) =>
      _$$HoleSubmissionImplFromJson(json);

  @override
  final int holeNumber;
  @override
  final int grossStrokes;

  @override
  String toString() {
    return 'HoleSubmission(holeNumber: $holeNumber, grossStrokes: $grossStrokes)';
  }

  @override
  bool operator ==(Object other) {
    return identical(this, other) ||
        (other.runtimeType == runtimeType &&
            other is _$HoleSubmissionImpl &&
            (identical(other.holeNumber, holeNumber) ||
                other.holeNumber == holeNumber) &&
            (identical(other.grossStrokes, grossStrokes) ||
                other.grossStrokes == grossStrokes));
  }

  @JsonKey(includeFromJson: false, includeToJson: false)
  @override
  int get hashCode => Object.hash(runtimeType, holeNumber, grossStrokes);

  /// Create a copy of HoleSubmission
  /// with the given fields replaced by the non-null parameter values.
  @JsonKey(includeFromJson: false, includeToJson: false)
  @override
  @pragma('vm:prefer-inline')
  _$$HoleSubmissionImplCopyWith<_$HoleSubmissionImpl> get copyWith =>
      __$$HoleSubmissionImplCopyWithImpl<_$HoleSubmissionImpl>(
          this, _$identity);

  @override
  Map<String, dynamic> toJson() {
    return _$$HoleSubmissionImplToJson(
      this,
    );
  }
}

abstract class _HoleSubmission implements HoleSubmission {
  const factory _HoleSubmission(
      {required final int holeNumber,
      required final int grossStrokes}) = _$HoleSubmissionImpl;

  factory _HoleSubmission.fromJson(Map<String, dynamic> json) =
      _$HoleSubmissionImpl.fromJson;

  @override
  int get holeNumber;
  @override
  int get grossStrokes;

  /// Create a copy of HoleSubmission
  /// with the given fields replaced by the non-null parameter values.
  @override
  @JsonKey(includeFromJson: false, includeToJson: false)
  _$$HoleSubmissionImplCopyWith<_$HoleSubmissionImpl> get copyWith =>
      throw _privateConstructorUsedError;
}
