// GENERATED CODE - DO NOT MODIFY BY HAND

part of 'app_database.dart';

// ignore_for_file: type=lint
class $CachedFlightsTable extends CachedFlights
    with TableInfo<$CachedFlightsTable, CachedFlight> {
  @override
  final GeneratedDatabase attachedDatabase;
  final String? _alias;
  $CachedFlightsTable(this.attachedDatabase, [this._alias]);
  static const VerificationMeta _idMeta = const VerificationMeta('id');
  @override
  late final GeneratedColumn<int> id = GeneratedColumn<int>(
      'id', aliasedName, false,
      type: DriftSqlType.int, requiredDuringInsert: false);
  static const VerificationMeta _nameMeta = const VerificationMeta('name');
  @override
  late final GeneratedColumn<String> name = GeneratedColumn<String>(
      'name', aliasedName, false,
      type: DriftSqlType.string, requiredDuringInsert: true);
  static const VerificationMeta _descriptionMeta =
      const VerificationMeta('description');
  @override
  late final GeneratedColumn<String> description = GeneratedColumn<String>(
      'description', aliasedName, true,
      type: DriftSqlType.string, requiredDuringInsert: false);
  static const VerificationMeta _cachedAtMeta =
      const VerificationMeta('cachedAt');
  @override
  late final GeneratedColumn<DateTime> cachedAt = GeneratedColumn<DateTime>(
      'cached_at', aliasedName, false,
      type: DriftSqlType.dateTime, requiredDuringInsert: true);
  @override
  List<GeneratedColumn> get $columns => [id, name, description, cachedAt];
  @override
  String get aliasedName => _alias ?? actualTableName;
  @override
  String get actualTableName => $name;
  static const String $name = 'cached_flights';
  @override
  VerificationContext validateIntegrity(Insertable<CachedFlight> instance,
      {bool isInserting = false}) {
    final context = VerificationContext();
    final data = instance.toColumns(true);
    if (data.containsKey('id')) {
      context.handle(_idMeta, id.isAcceptableOrUnknown(data['id']!, _idMeta));
    }
    if (data.containsKey('name')) {
      context.handle(
          _nameMeta, name.isAcceptableOrUnknown(data['name']!, _nameMeta));
    } else if (isInserting) {
      context.missing(_nameMeta);
    }
    if (data.containsKey('description')) {
      context.handle(
          _descriptionMeta,
          description.isAcceptableOrUnknown(
              data['description']!, _descriptionMeta));
    }
    if (data.containsKey('cached_at')) {
      context.handle(_cachedAtMeta,
          cachedAt.isAcceptableOrUnknown(data['cached_at']!, _cachedAtMeta));
    } else if (isInserting) {
      context.missing(_cachedAtMeta);
    }
    return context;
  }

  @override
  Set<GeneratedColumn> get $primaryKey => {id};
  @override
  CachedFlight map(Map<String, dynamic> data, {String? tablePrefix}) {
    final effectivePrefix = tablePrefix != null ? '$tablePrefix.' : '';
    return CachedFlight(
      id: attachedDatabase.typeMapping
          .read(DriftSqlType.int, data['${effectivePrefix}id'])!,
      name: attachedDatabase.typeMapping
          .read(DriftSqlType.string, data['${effectivePrefix}name'])!,
      description: attachedDatabase.typeMapping
          .read(DriftSqlType.string, data['${effectivePrefix}description']),
      cachedAt: attachedDatabase.typeMapping
          .read(DriftSqlType.dateTime, data['${effectivePrefix}cached_at'])!,
    );
  }

  @override
  $CachedFlightsTable createAlias(String alias) {
    return $CachedFlightsTable(attachedDatabase, alias);
  }
}

class CachedFlight extends DataClass implements Insertable<CachedFlight> {
  final int id;
  final String name;
  final String? description;
  final DateTime cachedAt;
  const CachedFlight(
      {required this.id,
      required this.name,
      this.description,
      required this.cachedAt});
  @override
  Map<String, Expression> toColumns(bool nullToAbsent) {
    final map = <String, Expression>{};
    map['id'] = Variable<int>(id);
    map['name'] = Variable<String>(name);
    if (!nullToAbsent || description != null) {
      map['description'] = Variable<String>(description);
    }
    map['cached_at'] = Variable<DateTime>(cachedAt);
    return map;
  }

  CachedFlightsCompanion toCompanion(bool nullToAbsent) {
    return CachedFlightsCompanion(
      id: Value(id),
      name: Value(name),
      description: description == null && nullToAbsent
          ? const Value.absent()
          : Value(description),
      cachedAt: Value(cachedAt),
    );
  }

  factory CachedFlight.fromJson(Map<String, dynamic> json,
      {ValueSerializer? serializer}) {
    serializer ??= driftRuntimeOptions.defaultSerializer;
    return CachedFlight(
      id: serializer.fromJson<int>(json['id']),
      name: serializer.fromJson<String>(json['name']),
      description: serializer.fromJson<String?>(json['description']),
      cachedAt: serializer.fromJson<DateTime>(json['cachedAt']),
    );
  }
  @override
  Map<String, dynamic> toJson({ValueSerializer? serializer}) {
    serializer ??= driftRuntimeOptions.defaultSerializer;
    return <String, dynamic>{
      'id': serializer.toJson<int>(id),
      'name': serializer.toJson<String>(name),
      'description': serializer.toJson<String?>(description),
      'cachedAt': serializer.toJson<DateTime>(cachedAt),
    };
  }

  CachedFlight copyWith(
          {int? id,
          String? name,
          Value<String?> description = const Value.absent(),
          DateTime? cachedAt}) =>
      CachedFlight(
        id: id ?? this.id,
        name: name ?? this.name,
        description: description.present ? description.value : this.description,
        cachedAt: cachedAt ?? this.cachedAt,
      );
  CachedFlight copyWithCompanion(CachedFlightsCompanion data) {
    return CachedFlight(
      id: data.id.present ? data.id.value : this.id,
      name: data.name.present ? data.name.value : this.name,
      description:
          data.description.present ? data.description.value : this.description,
      cachedAt: data.cachedAt.present ? data.cachedAt.value : this.cachedAt,
    );
  }

  @override
  String toString() {
    return (StringBuffer('CachedFlight(')
          ..write('id: $id, ')
          ..write('name: $name, ')
          ..write('description: $description, ')
          ..write('cachedAt: $cachedAt')
          ..write(')'))
        .toString();
  }

  @override
  int get hashCode => Object.hash(id, name, description, cachedAt);
  @override
  bool operator ==(Object other) =>
      identical(this, other) ||
      (other is CachedFlight &&
          other.id == this.id &&
          other.name == this.name &&
          other.description == this.description &&
          other.cachedAt == this.cachedAt);
}

class CachedFlightsCompanion extends UpdateCompanion<CachedFlight> {
  final Value<int> id;
  final Value<String> name;
  final Value<String?> description;
  final Value<DateTime> cachedAt;
  const CachedFlightsCompanion({
    this.id = const Value.absent(),
    this.name = const Value.absent(),
    this.description = const Value.absent(),
    this.cachedAt = const Value.absent(),
  });
  CachedFlightsCompanion.insert({
    this.id = const Value.absent(),
    required String name,
    this.description = const Value.absent(),
    required DateTime cachedAt,
  })  : name = Value(name),
        cachedAt = Value(cachedAt);
  static Insertable<CachedFlight> custom({
    Expression<int>? id,
    Expression<String>? name,
    Expression<String>? description,
    Expression<DateTime>? cachedAt,
  }) {
    return RawValuesInsertable({
      if (id != null) 'id': id,
      if (name != null) 'name': name,
      if (description != null) 'description': description,
      if (cachedAt != null) 'cached_at': cachedAt,
    });
  }

  CachedFlightsCompanion copyWith(
      {Value<int>? id,
      Value<String>? name,
      Value<String?>? description,
      Value<DateTime>? cachedAt}) {
    return CachedFlightsCompanion(
      id: id ?? this.id,
      name: name ?? this.name,
      description: description ?? this.description,
      cachedAt: cachedAt ?? this.cachedAt,
    );
  }

  @override
  Map<String, Expression> toColumns(bool nullToAbsent) {
    final map = <String, Expression>{};
    if (id.present) {
      map['id'] = Variable<int>(id.value);
    }
    if (name.present) {
      map['name'] = Variable<String>(name.value);
    }
    if (description.present) {
      map['description'] = Variable<String>(description.value);
    }
    if (cachedAt.present) {
      map['cached_at'] = Variable<DateTime>(cachedAt.value);
    }
    return map;
  }

  @override
  String toString() {
    return (StringBuffer('CachedFlightsCompanion(')
          ..write('id: $id, ')
          ..write('name: $name, ')
          ..write('description: $description, ')
          ..write('cachedAt: $cachedAt')
          ..write(')'))
        .toString();
  }
}

class $CachedLeaderboardEntriesTable extends CachedLeaderboardEntries
    with TableInfo<$CachedLeaderboardEntriesTable, CachedLeaderboardEntry> {
  @override
  final GeneratedDatabase attachedDatabase;
  final String? _alias;
  $CachedLeaderboardEntriesTable(this.attachedDatabase, [this._alias]);
  static const VerificationMeta _idMeta = const VerificationMeta('id');
  @override
  late final GeneratedColumn<int> id = GeneratedColumn<int>(
      'id', aliasedName, false,
      hasAutoIncrement: true,
      type: DriftSqlType.int,
      requiredDuringInsert: false,
      defaultConstraints:
          GeneratedColumn.constraintIsAlways('PRIMARY KEY AUTOINCREMENT'));
  static const VerificationMeta _flightIdMeta =
      const VerificationMeta('flightId');
  @override
  late final GeneratedColumn<int> flightId = GeneratedColumn<int>(
      'flight_id', aliasedName, false,
      type: DriftSqlType.int, requiredDuringInsert: true);
  static const VerificationMeta _playerIdMeta =
      const VerificationMeta('playerId');
  @override
  late final GeneratedColumn<int> playerId = GeneratedColumn<int>(
      'player_id', aliasedName, false,
      type: DriftSqlType.int, requiredDuringInsert: true);
  static const VerificationMeta _playerNameMeta =
      const VerificationMeta('playerName');
  @override
  late final GeneratedColumn<String> playerName = GeneratedColumn<String>(
      'player_name', aliasedName, false,
      type: DriftSqlType.string, requiredDuringInsert: true);
  static const VerificationMeta _totalStablefordPointsMeta =
      const VerificationMeta('totalStablefordPoints');
  @override
  late final GeneratedColumn<int> totalStablefordPoints = GeneratedColumn<int>(
      'total_stableford_points', aliasedName, false,
      type: DriftSqlType.int, requiredDuringInsert: true);
  static const VerificationMeta _roundsPlayedMeta =
      const VerificationMeta('roundsPlayed');
  @override
  late final GeneratedColumn<int> roundsPlayed = GeneratedColumn<int>(
      'rounds_played', aliasedName, false,
      type: DriftSqlType.int, requiredDuringInsert: true);
  static const VerificationMeta _currentRankMeta =
      const VerificationMeta('currentRank');
  @override
  late final GeneratedColumn<int> currentRank = GeneratedColumn<int>(
      'current_rank', aliasedName, false,
      type: DriftSqlType.int, requiredDuringInsert: true);
  static const VerificationMeta _previousRankMeta =
      const VerificationMeta('previousRank');
  @override
  late final GeneratedColumn<int> previousRank = GeneratedColumn<int>(
      'previous_rank', aliasedName, true,
      type: DriftSqlType.int, requiredDuringInsert: false);
  static const VerificationMeta _currentHandicapMeta =
      const VerificationMeta('currentHandicap');
  @override
  late final GeneratedColumn<double> currentHandicap = GeneratedColumn<double>(
      'current_handicap', aliasedName, false,
      type: DriftSqlType.double, requiredDuringInsert: true);
  static const VerificationMeta _cachedAtMeta =
      const VerificationMeta('cachedAt');
  @override
  late final GeneratedColumn<DateTime> cachedAt = GeneratedColumn<DateTime>(
      'cached_at', aliasedName, false,
      type: DriftSqlType.dateTime, requiredDuringInsert: true);
  @override
  List<GeneratedColumn> get $columns => [
        id,
        flightId,
        playerId,
        playerName,
        totalStablefordPoints,
        roundsPlayed,
        currentRank,
        previousRank,
        currentHandicap,
        cachedAt
      ];
  @override
  String get aliasedName => _alias ?? actualTableName;
  @override
  String get actualTableName => $name;
  static const String $name = 'cached_leaderboard_entries';
  @override
  VerificationContext validateIntegrity(
      Insertable<CachedLeaderboardEntry> instance,
      {bool isInserting = false}) {
    final context = VerificationContext();
    final data = instance.toColumns(true);
    if (data.containsKey('id')) {
      context.handle(_idMeta, id.isAcceptableOrUnknown(data['id']!, _idMeta));
    }
    if (data.containsKey('flight_id')) {
      context.handle(_flightIdMeta,
          flightId.isAcceptableOrUnknown(data['flight_id']!, _flightIdMeta));
    } else if (isInserting) {
      context.missing(_flightIdMeta);
    }
    if (data.containsKey('player_id')) {
      context.handle(_playerIdMeta,
          playerId.isAcceptableOrUnknown(data['player_id']!, _playerIdMeta));
    } else if (isInserting) {
      context.missing(_playerIdMeta);
    }
    if (data.containsKey('player_name')) {
      context.handle(
          _playerNameMeta,
          playerName.isAcceptableOrUnknown(
              data['player_name']!, _playerNameMeta));
    } else if (isInserting) {
      context.missing(_playerNameMeta);
    }
    if (data.containsKey('total_stableford_points')) {
      context.handle(
          _totalStablefordPointsMeta,
          totalStablefordPoints.isAcceptableOrUnknown(
              data['total_stableford_points']!, _totalStablefordPointsMeta));
    } else if (isInserting) {
      context.missing(_totalStablefordPointsMeta);
    }
    if (data.containsKey('rounds_played')) {
      context.handle(
          _roundsPlayedMeta,
          roundsPlayed.isAcceptableOrUnknown(
              data['rounds_played']!, _roundsPlayedMeta));
    } else if (isInserting) {
      context.missing(_roundsPlayedMeta);
    }
    if (data.containsKey('current_rank')) {
      context.handle(
          _currentRankMeta,
          currentRank.isAcceptableOrUnknown(
              data['current_rank']!, _currentRankMeta));
    } else if (isInserting) {
      context.missing(_currentRankMeta);
    }
    if (data.containsKey('previous_rank')) {
      context.handle(
          _previousRankMeta,
          previousRank.isAcceptableOrUnknown(
              data['previous_rank']!, _previousRankMeta));
    }
    if (data.containsKey('current_handicap')) {
      context.handle(
          _currentHandicapMeta,
          currentHandicap.isAcceptableOrUnknown(
              data['current_handicap']!, _currentHandicapMeta));
    } else if (isInserting) {
      context.missing(_currentHandicapMeta);
    }
    if (data.containsKey('cached_at')) {
      context.handle(_cachedAtMeta,
          cachedAt.isAcceptableOrUnknown(data['cached_at']!, _cachedAtMeta));
    } else if (isInserting) {
      context.missing(_cachedAtMeta);
    }
    return context;
  }

  @override
  Set<GeneratedColumn> get $primaryKey => {id};
  @override
  CachedLeaderboardEntry map(Map<String, dynamic> data, {String? tablePrefix}) {
    final effectivePrefix = tablePrefix != null ? '$tablePrefix.' : '';
    return CachedLeaderboardEntry(
      id: attachedDatabase.typeMapping
          .read(DriftSqlType.int, data['${effectivePrefix}id'])!,
      flightId: attachedDatabase.typeMapping
          .read(DriftSqlType.int, data['${effectivePrefix}flight_id'])!,
      playerId: attachedDatabase.typeMapping
          .read(DriftSqlType.int, data['${effectivePrefix}player_id'])!,
      playerName: attachedDatabase.typeMapping
          .read(DriftSqlType.string, data['${effectivePrefix}player_name'])!,
      totalStablefordPoints: attachedDatabase.typeMapping.read(
          DriftSqlType.int, data['${effectivePrefix}total_stableford_points'])!,
      roundsPlayed: attachedDatabase.typeMapping
          .read(DriftSqlType.int, data['${effectivePrefix}rounds_played'])!,
      currentRank: attachedDatabase.typeMapping
          .read(DriftSqlType.int, data['${effectivePrefix}current_rank'])!,
      previousRank: attachedDatabase.typeMapping
          .read(DriftSqlType.int, data['${effectivePrefix}previous_rank']),
      currentHandicap: attachedDatabase.typeMapping.read(
          DriftSqlType.double, data['${effectivePrefix}current_handicap'])!,
      cachedAt: attachedDatabase.typeMapping
          .read(DriftSqlType.dateTime, data['${effectivePrefix}cached_at'])!,
    );
  }

  @override
  $CachedLeaderboardEntriesTable createAlias(String alias) {
    return $CachedLeaderboardEntriesTable(attachedDatabase, alias);
  }
}

class CachedLeaderboardEntry extends DataClass
    implements Insertable<CachedLeaderboardEntry> {
  final int id;
  final int flightId;
  final int playerId;
  final String playerName;
  final int totalStablefordPoints;
  final int roundsPlayed;
  final int currentRank;
  final int? previousRank;
  final double currentHandicap;
  final DateTime cachedAt;
  const CachedLeaderboardEntry(
      {required this.id,
      required this.flightId,
      required this.playerId,
      required this.playerName,
      required this.totalStablefordPoints,
      required this.roundsPlayed,
      required this.currentRank,
      this.previousRank,
      required this.currentHandicap,
      required this.cachedAt});
  @override
  Map<String, Expression> toColumns(bool nullToAbsent) {
    final map = <String, Expression>{};
    map['id'] = Variable<int>(id);
    map['flight_id'] = Variable<int>(flightId);
    map['player_id'] = Variable<int>(playerId);
    map['player_name'] = Variable<String>(playerName);
    map['total_stableford_points'] = Variable<int>(totalStablefordPoints);
    map['rounds_played'] = Variable<int>(roundsPlayed);
    map['current_rank'] = Variable<int>(currentRank);
    if (!nullToAbsent || previousRank != null) {
      map['previous_rank'] = Variable<int>(previousRank);
    }
    map['current_handicap'] = Variable<double>(currentHandicap);
    map['cached_at'] = Variable<DateTime>(cachedAt);
    return map;
  }

  CachedLeaderboardEntriesCompanion toCompanion(bool nullToAbsent) {
    return CachedLeaderboardEntriesCompanion(
      id: Value(id),
      flightId: Value(flightId),
      playerId: Value(playerId),
      playerName: Value(playerName),
      totalStablefordPoints: Value(totalStablefordPoints),
      roundsPlayed: Value(roundsPlayed),
      currentRank: Value(currentRank),
      previousRank: previousRank == null && nullToAbsent
          ? const Value.absent()
          : Value(previousRank),
      currentHandicap: Value(currentHandicap),
      cachedAt: Value(cachedAt),
    );
  }

  factory CachedLeaderboardEntry.fromJson(Map<String, dynamic> json,
      {ValueSerializer? serializer}) {
    serializer ??= driftRuntimeOptions.defaultSerializer;
    return CachedLeaderboardEntry(
      id: serializer.fromJson<int>(json['id']),
      flightId: serializer.fromJson<int>(json['flightId']),
      playerId: serializer.fromJson<int>(json['playerId']),
      playerName: serializer.fromJson<String>(json['playerName']),
      totalStablefordPoints:
          serializer.fromJson<int>(json['totalStablefordPoints']),
      roundsPlayed: serializer.fromJson<int>(json['roundsPlayed']),
      currentRank: serializer.fromJson<int>(json['currentRank']),
      previousRank: serializer.fromJson<int?>(json['previousRank']),
      currentHandicap: serializer.fromJson<double>(json['currentHandicap']),
      cachedAt: serializer.fromJson<DateTime>(json['cachedAt']),
    );
  }
  @override
  Map<String, dynamic> toJson({ValueSerializer? serializer}) {
    serializer ??= driftRuntimeOptions.defaultSerializer;
    return <String, dynamic>{
      'id': serializer.toJson<int>(id),
      'flightId': serializer.toJson<int>(flightId),
      'playerId': serializer.toJson<int>(playerId),
      'playerName': serializer.toJson<String>(playerName),
      'totalStablefordPoints': serializer.toJson<int>(totalStablefordPoints),
      'roundsPlayed': serializer.toJson<int>(roundsPlayed),
      'currentRank': serializer.toJson<int>(currentRank),
      'previousRank': serializer.toJson<int?>(previousRank),
      'currentHandicap': serializer.toJson<double>(currentHandicap),
      'cachedAt': serializer.toJson<DateTime>(cachedAt),
    };
  }

  CachedLeaderboardEntry copyWith(
          {int? id,
          int? flightId,
          int? playerId,
          String? playerName,
          int? totalStablefordPoints,
          int? roundsPlayed,
          int? currentRank,
          Value<int?> previousRank = const Value.absent(),
          double? currentHandicap,
          DateTime? cachedAt}) =>
      CachedLeaderboardEntry(
        id: id ?? this.id,
        flightId: flightId ?? this.flightId,
        playerId: playerId ?? this.playerId,
        playerName: playerName ?? this.playerName,
        totalStablefordPoints:
            totalStablefordPoints ?? this.totalStablefordPoints,
        roundsPlayed: roundsPlayed ?? this.roundsPlayed,
        currentRank: currentRank ?? this.currentRank,
        previousRank:
            previousRank.present ? previousRank.value : this.previousRank,
        currentHandicap: currentHandicap ?? this.currentHandicap,
        cachedAt: cachedAt ?? this.cachedAt,
      );
  CachedLeaderboardEntry copyWithCompanion(
      CachedLeaderboardEntriesCompanion data) {
    return CachedLeaderboardEntry(
      id: data.id.present ? data.id.value : this.id,
      flightId: data.flightId.present ? data.flightId.value : this.flightId,
      playerId: data.playerId.present ? data.playerId.value : this.playerId,
      playerName:
          data.playerName.present ? data.playerName.value : this.playerName,
      totalStablefordPoints: data.totalStablefordPoints.present
          ? data.totalStablefordPoints.value
          : this.totalStablefordPoints,
      roundsPlayed: data.roundsPlayed.present
          ? data.roundsPlayed.value
          : this.roundsPlayed,
      currentRank:
          data.currentRank.present ? data.currentRank.value : this.currentRank,
      previousRank: data.previousRank.present
          ? data.previousRank.value
          : this.previousRank,
      currentHandicap: data.currentHandicap.present
          ? data.currentHandicap.value
          : this.currentHandicap,
      cachedAt: data.cachedAt.present ? data.cachedAt.value : this.cachedAt,
    );
  }

  @override
  String toString() {
    return (StringBuffer('CachedLeaderboardEntry(')
          ..write('id: $id, ')
          ..write('flightId: $flightId, ')
          ..write('playerId: $playerId, ')
          ..write('playerName: $playerName, ')
          ..write('totalStablefordPoints: $totalStablefordPoints, ')
          ..write('roundsPlayed: $roundsPlayed, ')
          ..write('currentRank: $currentRank, ')
          ..write('previousRank: $previousRank, ')
          ..write('currentHandicap: $currentHandicap, ')
          ..write('cachedAt: $cachedAt')
          ..write(')'))
        .toString();
  }

  @override
  int get hashCode => Object.hash(
      id,
      flightId,
      playerId,
      playerName,
      totalStablefordPoints,
      roundsPlayed,
      currentRank,
      previousRank,
      currentHandicap,
      cachedAt);
  @override
  bool operator ==(Object other) =>
      identical(this, other) ||
      (other is CachedLeaderboardEntry &&
          other.id == this.id &&
          other.flightId == this.flightId &&
          other.playerId == this.playerId &&
          other.playerName == this.playerName &&
          other.totalStablefordPoints == this.totalStablefordPoints &&
          other.roundsPlayed == this.roundsPlayed &&
          other.currentRank == this.currentRank &&
          other.previousRank == this.previousRank &&
          other.currentHandicap == this.currentHandicap &&
          other.cachedAt == this.cachedAt);
}

class CachedLeaderboardEntriesCompanion
    extends UpdateCompanion<CachedLeaderboardEntry> {
  final Value<int> id;
  final Value<int> flightId;
  final Value<int> playerId;
  final Value<String> playerName;
  final Value<int> totalStablefordPoints;
  final Value<int> roundsPlayed;
  final Value<int> currentRank;
  final Value<int?> previousRank;
  final Value<double> currentHandicap;
  final Value<DateTime> cachedAt;
  const CachedLeaderboardEntriesCompanion({
    this.id = const Value.absent(),
    this.flightId = const Value.absent(),
    this.playerId = const Value.absent(),
    this.playerName = const Value.absent(),
    this.totalStablefordPoints = const Value.absent(),
    this.roundsPlayed = const Value.absent(),
    this.currentRank = const Value.absent(),
    this.previousRank = const Value.absent(),
    this.currentHandicap = const Value.absent(),
    this.cachedAt = const Value.absent(),
  });
  CachedLeaderboardEntriesCompanion.insert({
    this.id = const Value.absent(),
    required int flightId,
    required int playerId,
    required String playerName,
    required int totalStablefordPoints,
    required int roundsPlayed,
    required int currentRank,
    this.previousRank = const Value.absent(),
    required double currentHandicap,
    required DateTime cachedAt,
  })  : flightId = Value(flightId),
        playerId = Value(playerId),
        playerName = Value(playerName),
        totalStablefordPoints = Value(totalStablefordPoints),
        roundsPlayed = Value(roundsPlayed),
        currentRank = Value(currentRank),
        currentHandicap = Value(currentHandicap),
        cachedAt = Value(cachedAt);
  static Insertable<CachedLeaderboardEntry> custom({
    Expression<int>? id,
    Expression<int>? flightId,
    Expression<int>? playerId,
    Expression<String>? playerName,
    Expression<int>? totalStablefordPoints,
    Expression<int>? roundsPlayed,
    Expression<int>? currentRank,
    Expression<int>? previousRank,
    Expression<double>? currentHandicap,
    Expression<DateTime>? cachedAt,
  }) {
    return RawValuesInsertable({
      if (id != null) 'id': id,
      if (flightId != null) 'flight_id': flightId,
      if (playerId != null) 'player_id': playerId,
      if (playerName != null) 'player_name': playerName,
      if (totalStablefordPoints != null)
        'total_stableford_points': totalStablefordPoints,
      if (roundsPlayed != null) 'rounds_played': roundsPlayed,
      if (currentRank != null) 'current_rank': currentRank,
      if (previousRank != null) 'previous_rank': previousRank,
      if (currentHandicap != null) 'current_handicap': currentHandicap,
      if (cachedAt != null) 'cached_at': cachedAt,
    });
  }

  CachedLeaderboardEntriesCompanion copyWith(
      {Value<int>? id,
      Value<int>? flightId,
      Value<int>? playerId,
      Value<String>? playerName,
      Value<int>? totalStablefordPoints,
      Value<int>? roundsPlayed,
      Value<int>? currentRank,
      Value<int?>? previousRank,
      Value<double>? currentHandicap,
      Value<DateTime>? cachedAt}) {
    return CachedLeaderboardEntriesCompanion(
      id: id ?? this.id,
      flightId: flightId ?? this.flightId,
      playerId: playerId ?? this.playerId,
      playerName: playerName ?? this.playerName,
      totalStablefordPoints:
          totalStablefordPoints ?? this.totalStablefordPoints,
      roundsPlayed: roundsPlayed ?? this.roundsPlayed,
      currentRank: currentRank ?? this.currentRank,
      previousRank: previousRank ?? this.previousRank,
      currentHandicap: currentHandicap ?? this.currentHandicap,
      cachedAt: cachedAt ?? this.cachedAt,
    );
  }

  @override
  Map<String, Expression> toColumns(bool nullToAbsent) {
    final map = <String, Expression>{};
    if (id.present) {
      map['id'] = Variable<int>(id.value);
    }
    if (flightId.present) {
      map['flight_id'] = Variable<int>(flightId.value);
    }
    if (playerId.present) {
      map['player_id'] = Variable<int>(playerId.value);
    }
    if (playerName.present) {
      map['player_name'] = Variable<String>(playerName.value);
    }
    if (totalStablefordPoints.present) {
      map['total_stableford_points'] =
          Variable<int>(totalStablefordPoints.value);
    }
    if (roundsPlayed.present) {
      map['rounds_played'] = Variable<int>(roundsPlayed.value);
    }
    if (currentRank.present) {
      map['current_rank'] = Variable<int>(currentRank.value);
    }
    if (previousRank.present) {
      map['previous_rank'] = Variable<int>(previousRank.value);
    }
    if (currentHandicap.present) {
      map['current_handicap'] = Variable<double>(currentHandicap.value);
    }
    if (cachedAt.present) {
      map['cached_at'] = Variable<DateTime>(cachedAt.value);
    }
    return map;
  }

  @override
  String toString() {
    return (StringBuffer('CachedLeaderboardEntriesCompanion(')
          ..write('id: $id, ')
          ..write('flightId: $flightId, ')
          ..write('playerId: $playerId, ')
          ..write('playerName: $playerName, ')
          ..write('totalStablefordPoints: $totalStablefordPoints, ')
          ..write('roundsPlayed: $roundsPlayed, ')
          ..write('currentRank: $currentRank, ')
          ..write('previousRank: $previousRank, ')
          ..write('currentHandicap: $currentHandicap, ')
          ..write('cachedAt: $cachedAt')
          ..write(')'))
        .toString();
  }
}

class $CachedRoundsTable extends CachedRounds
    with TableInfo<$CachedRoundsTable, CachedRound> {
  @override
  final GeneratedDatabase attachedDatabase;
  final String? _alias;
  $CachedRoundsTable(this.attachedDatabase, [this._alias]);
  static const VerificationMeta _idMeta = const VerificationMeta('id');
  @override
  late final GeneratedColumn<int> id = GeneratedColumn<int>(
      'id', aliasedName, false,
      type: DriftSqlType.int, requiredDuringInsert: false);
  static const VerificationMeta _courseNameMeta =
      const VerificationMeta('courseName');
  @override
  late final GeneratedColumn<String> courseName = GeneratedColumn<String>(
      'course_name', aliasedName, false,
      type: DriftSqlType.string, requiredDuringInsert: true);
  static const VerificationMeta _scheduledDateMeta =
      const VerificationMeta('scheduledDate');
  @override
  late final GeneratedColumn<DateTime> scheduledDate =
      GeneratedColumn<DateTime>('scheduled_date', aliasedName, false,
          type: DriftSqlType.dateTime, requiredDuringInsert: true);
  static const VerificationMeta _playedDateMeta =
      const VerificationMeta('playedDate');
  @override
  late final GeneratedColumn<DateTime> playedDate = GeneratedColumn<DateTime>(
      'played_date', aliasedName, true,
      type: DriftSqlType.dateTime, requiredDuringInsert: false);
  static const VerificationMeta _statusMeta = const VerificationMeta('status');
  @override
  late final GeneratedColumn<String> status = GeneratedColumn<String>(
      'status', aliasedName, false,
      type: DriftSqlType.string, requiredDuringInsert: true);
  static const VerificationMeta _roundNumberMeta =
      const VerificationMeta('roundNumber');
  @override
  late final GeneratedColumn<int> roundNumber = GeneratedColumn<int>(
      'round_number', aliasedName, false,
      type: DriftSqlType.int, requiredDuringInsert: true);
  static const VerificationMeta _weatherConditionsMeta =
      const VerificationMeta('weatherConditions');
  @override
  late final GeneratedColumn<String> weatherConditions =
      GeneratedColumn<String>('weather_conditions', aliasedName, true,
          type: DriftSqlType.string, requiredDuringInsert: false);
  static const VerificationMeta _cachedAtMeta =
      const VerificationMeta('cachedAt');
  @override
  late final GeneratedColumn<DateTime> cachedAt = GeneratedColumn<DateTime>(
      'cached_at', aliasedName, false,
      type: DriftSqlType.dateTime, requiredDuringInsert: true);
  @override
  List<GeneratedColumn> get $columns => [
        id,
        courseName,
        scheduledDate,
        playedDate,
        status,
        roundNumber,
        weatherConditions,
        cachedAt
      ];
  @override
  String get aliasedName => _alias ?? actualTableName;
  @override
  String get actualTableName => $name;
  static const String $name = 'cached_rounds';
  @override
  VerificationContext validateIntegrity(Insertable<CachedRound> instance,
      {bool isInserting = false}) {
    final context = VerificationContext();
    final data = instance.toColumns(true);
    if (data.containsKey('id')) {
      context.handle(_idMeta, id.isAcceptableOrUnknown(data['id']!, _idMeta));
    }
    if (data.containsKey('course_name')) {
      context.handle(
          _courseNameMeta,
          courseName.isAcceptableOrUnknown(
              data['course_name']!, _courseNameMeta));
    } else if (isInserting) {
      context.missing(_courseNameMeta);
    }
    if (data.containsKey('scheduled_date')) {
      context.handle(
          _scheduledDateMeta,
          scheduledDate.isAcceptableOrUnknown(
              data['scheduled_date']!, _scheduledDateMeta));
    } else if (isInserting) {
      context.missing(_scheduledDateMeta);
    }
    if (data.containsKey('played_date')) {
      context.handle(
          _playedDateMeta,
          playedDate.isAcceptableOrUnknown(
              data['played_date']!, _playedDateMeta));
    }
    if (data.containsKey('status')) {
      context.handle(_statusMeta,
          status.isAcceptableOrUnknown(data['status']!, _statusMeta));
    } else if (isInserting) {
      context.missing(_statusMeta);
    }
    if (data.containsKey('round_number')) {
      context.handle(
          _roundNumberMeta,
          roundNumber.isAcceptableOrUnknown(
              data['round_number']!, _roundNumberMeta));
    } else if (isInserting) {
      context.missing(_roundNumberMeta);
    }
    if (data.containsKey('weather_conditions')) {
      context.handle(
          _weatherConditionsMeta,
          weatherConditions.isAcceptableOrUnknown(
              data['weather_conditions']!, _weatherConditionsMeta));
    }
    if (data.containsKey('cached_at')) {
      context.handle(_cachedAtMeta,
          cachedAt.isAcceptableOrUnknown(data['cached_at']!, _cachedAtMeta));
    } else if (isInserting) {
      context.missing(_cachedAtMeta);
    }
    return context;
  }

  @override
  Set<GeneratedColumn> get $primaryKey => {id};
  @override
  CachedRound map(Map<String, dynamic> data, {String? tablePrefix}) {
    final effectivePrefix = tablePrefix != null ? '$tablePrefix.' : '';
    return CachedRound(
      id: attachedDatabase.typeMapping
          .read(DriftSqlType.int, data['${effectivePrefix}id'])!,
      courseName: attachedDatabase.typeMapping
          .read(DriftSqlType.string, data['${effectivePrefix}course_name'])!,
      scheduledDate: attachedDatabase.typeMapping.read(
          DriftSqlType.dateTime, data['${effectivePrefix}scheduled_date'])!,
      playedDate: attachedDatabase.typeMapping
          .read(DriftSqlType.dateTime, data['${effectivePrefix}played_date']),
      status: attachedDatabase.typeMapping
          .read(DriftSqlType.string, data['${effectivePrefix}status'])!,
      roundNumber: attachedDatabase.typeMapping
          .read(DriftSqlType.int, data['${effectivePrefix}round_number'])!,
      weatherConditions: attachedDatabase.typeMapping.read(
          DriftSqlType.string, data['${effectivePrefix}weather_conditions']),
      cachedAt: attachedDatabase.typeMapping
          .read(DriftSqlType.dateTime, data['${effectivePrefix}cached_at'])!,
    );
  }

  @override
  $CachedRoundsTable createAlias(String alias) {
    return $CachedRoundsTable(attachedDatabase, alias);
  }
}

class CachedRound extends DataClass implements Insertable<CachedRound> {
  final int id;
  final String courseName;
  final DateTime scheduledDate;
  final DateTime? playedDate;
  final String status;
  final int roundNumber;
  final String? weatherConditions;
  final DateTime cachedAt;
  const CachedRound(
      {required this.id,
      required this.courseName,
      required this.scheduledDate,
      this.playedDate,
      required this.status,
      required this.roundNumber,
      this.weatherConditions,
      required this.cachedAt});
  @override
  Map<String, Expression> toColumns(bool nullToAbsent) {
    final map = <String, Expression>{};
    map['id'] = Variable<int>(id);
    map['course_name'] = Variable<String>(courseName);
    map['scheduled_date'] = Variable<DateTime>(scheduledDate);
    if (!nullToAbsent || playedDate != null) {
      map['played_date'] = Variable<DateTime>(playedDate);
    }
    map['status'] = Variable<String>(status);
    map['round_number'] = Variable<int>(roundNumber);
    if (!nullToAbsent || weatherConditions != null) {
      map['weather_conditions'] = Variable<String>(weatherConditions);
    }
    map['cached_at'] = Variable<DateTime>(cachedAt);
    return map;
  }

  CachedRoundsCompanion toCompanion(bool nullToAbsent) {
    return CachedRoundsCompanion(
      id: Value(id),
      courseName: Value(courseName),
      scheduledDate: Value(scheduledDate),
      playedDate: playedDate == null && nullToAbsent
          ? const Value.absent()
          : Value(playedDate),
      status: Value(status),
      roundNumber: Value(roundNumber),
      weatherConditions: weatherConditions == null && nullToAbsent
          ? const Value.absent()
          : Value(weatherConditions),
      cachedAt: Value(cachedAt),
    );
  }

  factory CachedRound.fromJson(Map<String, dynamic> json,
      {ValueSerializer? serializer}) {
    serializer ??= driftRuntimeOptions.defaultSerializer;
    return CachedRound(
      id: serializer.fromJson<int>(json['id']),
      courseName: serializer.fromJson<String>(json['courseName']),
      scheduledDate: serializer.fromJson<DateTime>(json['scheduledDate']),
      playedDate: serializer.fromJson<DateTime?>(json['playedDate']),
      status: serializer.fromJson<String>(json['status']),
      roundNumber: serializer.fromJson<int>(json['roundNumber']),
      weatherConditions:
          serializer.fromJson<String?>(json['weatherConditions']),
      cachedAt: serializer.fromJson<DateTime>(json['cachedAt']),
    );
  }
  @override
  Map<String, dynamic> toJson({ValueSerializer? serializer}) {
    serializer ??= driftRuntimeOptions.defaultSerializer;
    return <String, dynamic>{
      'id': serializer.toJson<int>(id),
      'courseName': serializer.toJson<String>(courseName),
      'scheduledDate': serializer.toJson<DateTime>(scheduledDate),
      'playedDate': serializer.toJson<DateTime?>(playedDate),
      'status': serializer.toJson<String>(status),
      'roundNumber': serializer.toJson<int>(roundNumber),
      'weatherConditions': serializer.toJson<String?>(weatherConditions),
      'cachedAt': serializer.toJson<DateTime>(cachedAt),
    };
  }

  CachedRound copyWith(
          {int? id,
          String? courseName,
          DateTime? scheduledDate,
          Value<DateTime?> playedDate = const Value.absent(),
          String? status,
          int? roundNumber,
          Value<String?> weatherConditions = const Value.absent(),
          DateTime? cachedAt}) =>
      CachedRound(
        id: id ?? this.id,
        courseName: courseName ?? this.courseName,
        scheduledDate: scheduledDate ?? this.scheduledDate,
        playedDate: playedDate.present ? playedDate.value : this.playedDate,
        status: status ?? this.status,
        roundNumber: roundNumber ?? this.roundNumber,
        weatherConditions: weatherConditions.present
            ? weatherConditions.value
            : this.weatherConditions,
        cachedAt: cachedAt ?? this.cachedAt,
      );
  CachedRound copyWithCompanion(CachedRoundsCompanion data) {
    return CachedRound(
      id: data.id.present ? data.id.value : this.id,
      courseName:
          data.courseName.present ? data.courseName.value : this.courseName,
      scheduledDate: data.scheduledDate.present
          ? data.scheduledDate.value
          : this.scheduledDate,
      playedDate:
          data.playedDate.present ? data.playedDate.value : this.playedDate,
      status: data.status.present ? data.status.value : this.status,
      roundNumber:
          data.roundNumber.present ? data.roundNumber.value : this.roundNumber,
      weatherConditions: data.weatherConditions.present
          ? data.weatherConditions.value
          : this.weatherConditions,
      cachedAt: data.cachedAt.present ? data.cachedAt.value : this.cachedAt,
    );
  }

  @override
  String toString() {
    return (StringBuffer('CachedRound(')
          ..write('id: $id, ')
          ..write('courseName: $courseName, ')
          ..write('scheduledDate: $scheduledDate, ')
          ..write('playedDate: $playedDate, ')
          ..write('status: $status, ')
          ..write('roundNumber: $roundNumber, ')
          ..write('weatherConditions: $weatherConditions, ')
          ..write('cachedAt: $cachedAt')
          ..write(')'))
        .toString();
  }

  @override
  int get hashCode => Object.hash(id, courseName, scheduledDate, playedDate,
      status, roundNumber, weatherConditions, cachedAt);
  @override
  bool operator ==(Object other) =>
      identical(this, other) ||
      (other is CachedRound &&
          other.id == this.id &&
          other.courseName == this.courseName &&
          other.scheduledDate == this.scheduledDate &&
          other.playedDate == this.playedDate &&
          other.status == this.status &&
          other.roundNumber == this.roundNumber &&
          other.weatherConditions == this.weatherConditions &&
          other.cachedAt == this.cachedAt);
}

class CachedRoundsCompanion extends UpdateCompanion<CachedRound> {
  final Value<int> id;
  final Value<String> courseName;
  final Value<DateTime> scheduledDate;
  final Value<DateTime?> playedDate;
  final Value<String> status;
  final Value<int> roundNumber;
  final Value<String?> weatherConditions;
  final Value<DateTime> cachedAt;
  const CachedRoundsCompanion({
    this.id = const Value.absent(),
    this.courseName = const Value.absent(),
    this.scheduledDate = const Value.absent(),
    this.playedDate = const Value.absent(),
    this.status = const Value.absent(),
    this.roundNumber = const Value.absent(),
    this.weatherConditions = const Value.absent(),
    this.cachedAt = const Value.absent(),
  });
  CachedRoundsCompanion.insert({
    this.id = const Value.absent(),
    required String courseName,
    required DateTime scheduledDate,
    this.playedDate = const Value.absent(),
    required String status,
    required int roundNumber,
    this.weatherConditions = const Value.absent(),
    required DateTime cachedAt,
  })  : courseName = Value(courseName),
        scheduledDate = Value(scheduledDate),
        status = Value(status),
        roundNumber = Value(roundNumber),
        cachedAt = Value(cachedAt);
  static Insertable<CachedRound> custom({
    Expression<int>? id,
    Expression<String>? courseName,
    Expression<DateTime>? scheduledDate,
    Expression<DateTime>? playedDate,
    Expression<String>? status,
    Expression<int>? roundNumber,
    Expression<String>? weatherConditions,
    Expression<DateTime>? cachedAt,
  }) {
    return RawValuesInsertable({
      if (id != null) 'id': id,
      if (courseName != null) 'course_name': courseName,
      if (scheduledDate != null) 'scheduled_date': scheduledDate,
      if (playedDate != null) 'played_date': playedDate,
      if (status != null) 'status': status,
      if (roundNumber != null) 'round_number': roundNumber,
      if (weatherConditions != null) 'weather_conditions': weatherConditions,
      if (cachedAt != null) 'cached_at': cachedAt,
    });
  }

  CachedRoundsCompanion copyWith(
      {Value<int>? id,
      Value<String>? courseName,
      Value<DateTime>? scheduledDate,
      Value<DateTime?>? playedDate,
      Value<String>? status,
      Value<int>? roundNumber,
      Value<String?>? weatherConditions,
      Value<DateTime>? cachedAt}) {
    return CachedRoundsCompanion(
      id: id ?? this.id,
      courseName: courseName ?? this.courseName,
      scheduledDate: scheduledDate ?? this.scheduledDate,
      playedDate: playedDate ?? this.playedDate,
      status: status ?? this.status,
      roundNumber: roundNumber ?? this.roundNumber,
      weatherConditions: weatherConditions ?? this.weatherConditions,
      cachedAt: cachedAt ?? this.cachedAt,
    );
  }

  @override
  Map<String, Expression> toColumns(bool nullToAbsent) {
    final map = <String, Expression>{};
    if (id.present) {
      map['id'] = Variable<int>(id.value);
    }
    if (courseName.present) {
      map['course_name'] = Variable<String>(courseName.value);
    }
    if (scheduledDate.present) {
      map['scheduled_date'] = Variable<DateTime>(scheduledDate.value);
    }
    if (playedDate.present) {
      map['played_date'] = Variable<DateTime>(playedDate.value);
    }
    if (status.present) {
      map['status'] = Variable<String>(status.value);
    }
    if (roundNumber.present) {
      map['round_number'] = Variable<int>(roundNumber.value);
    }
    if (weatherConditions.present) {
      map['weather_conditions'] = Variable<String>(weatherConditions.value);
    }
    if (cachedAt.present) {
      map['cached_at'] = Variable<DateTime>(cachedAt.value);
    }
    return map;
  }

  @override
  String toString() {
    return (StringBuffer('CachedRoundsCompanion(')
          ..write('id: $id, ')
          ..write('courseName: $courseName, ')
          ..write('scheduledDate: $scheduledDate, ')
          ..write('playedDate: $playedDate, ')
          ..write('status: $status, ')
          ..write('roundNumber: $roundNumber, ')
          ..write('weatherConditions: $weatherConditions, ')
          ..write('cachedAt: $cachedAt')
          ..write(')'))
        .toString();
  }
}

class $CachedHoleScoresTable extends CachedHoleScores
    with TableInfo<$CachedHoleScoresTable, CachedHoleScore> {
  @override
  final GeneratedDatabase attachedDatabase;
  final String? _alias;
  $CachedHoleScoresTable(this.attachedDatabase, [this._alias]);
  static const VerificationMeta _idMeta = const VerificationMeta('id');
  @override
  late final GeneratedColumn<int> id = GeneratedColumn<int>(
      'id', aliasedName, false,
      hasAutoIncrement: true,
      type: DriftSqlType.int,
      requiredDuringInsert: false,
      defaultConstraints:
          GeneratedColumn.constraintIsAlways('PRIMARY KEY AUTOINCREMENT'));
  static const VerificationMeta _roundIdMeta =
      const VerificationMeta('roundId');
  @override
  late final GeneratedColumn<int> roundId = GeneratedColumn<int>(
      'round_id', aliasedName, false,
      type: DriftSqlType.int, requiredDuringInsert: true);
  static const VerificationMeta _playerIdMeta =
      const VerificationMeta('playerId');
  @override
  late final GeneratedColumn<int> playerId = GeneratedColumn<int>(
      'player_id', aliasedName, false,
      type: DriftSqlType.int, requiredDuringInsert: true);
  static const VerificationMeta _holeNumberMeta =
      const VerificationMeta('holeNumber');
  @override
  late final GeneratedColumn<int> holeNumber = GeneratedColumn<int>(
      'hole_number', aliasedName, false,
      type: DriftSqlType.int, requiredDuringInsert: true);
  static const VerificationMeta _parMeta = const VerificationMeta('par');
  @override
  late final GeneratedColumn<int> par = GeneratedColumn<int>(
      'par', aliasedName, false,
      type: DriftSqlType.int, requiredDuringInsert: true);
  static const VerificationMeta _strokeIndexMeta =
      const VerificationMeta('strokeIndex');
  @override
  late final GeneratedColumn<int> strokeIndex = GeneratedColumn<int>(
      'stroke_index', aliasedName, false,
      type: DriftSqlType.int, requiredDuringInsert: true);
  static const VerificationMeta _grossStrokesMeta =
      const VerificationMeta('grossStrokes');
  @override
  late final GeneratedColumn<int> grossStrokes = GeneratedColumn<int>(
      'gross_strokes', aliasedName, true,
      type: DriftSqlType.int, requiredDuringInsert: false);
  static const VerificationMeta _handicapStrokesMeta =
      const VerificationMeta('handicapStrokes');
  @override
  late final GeneratedColumn<int> handicapStrokes = GeneratedColumn<int>(
      'handicap_strokes', aliasedName, false,
      type: DriftSqlType.int, requiredDuringInsert: true);
  static const VerificationMeta _netStrokesMeta =
      const VerificationMeta('netStrokes');
  @override
  late final GeneratedColumn<int> netStrokes = GeneratedColumn<int>(
      'net_strokes', aliasedName, true,
      type: DriftSqlType.int, requiredDuringInsert: false);
  static const VerificationMeta _stablefordPointsMeta =
      const VerificationMeta('stablefordPoints');
  @override
  late final GeneratedColumn<int> stablefordPoints = GeneratedColumn<int>(
      'stableford_points', aliasedName, true,
      type: DriftSqlType.int, requiredDuringInsert: false);
  static const VerificationMeta _isMaxScoreMeta =
      const VerificationMeta('isMaxScore');
  @override
  late final GeneratedColumn<bool> isMaxScore = GeneratedColumn<bool>(
      'is_max_score', aliasedName, false,
      type: DriftSqlType.bool,
      requiredDuringInsert: true,
      defaultConstraints: GeneratedColumn.constraintIsAlways(
          'CHECK ("is_max_score" IN (0, 1))'));
  @override
  List<GeneratedColumn> get $columns => [
        id,
        roundId,
        playerId,
        holeNumber,
        par,
        strokeIndex,
        grossStrokes,
        handicapStrokes,
        netStrokes,
        stablefordPoints,
        isMaxScore
      ];
  @override
  String get aliasedName => _alias ?? actualTableName;
  @override
  String get actualTableName => $name;
  static const String $name = 'cached_hole_scores';
  @override
  VerificationContext validateIntegrity(Insertable<CachedHoleScore> instance,
      {bool isInserting = false}) {
    final context = VerificationContext();
    final data = instance.toColumns(true);
    if (data.containsKey('id')) {
      context.handle(_idMeta, id.isAcceptableOrUnknown(data['id']!, _idMeta));
    }
    if (data.containsKey('round_id')) {
      context.handle(_roundIdMeta,
          roundId.isAcceptableOrUnknown(data['round_id']!, _roundIdMeta));
    } else if (isInserting) {
      context.missing(_roundIdMeta);
    }
    if (data.containsKey('player_id')) {
      context.handle(_playerIdMeta,
          playerId.isAcceptableOrUnknown(data['player_id']!, _playerIdMeta));
    } else if (isInserting) {
      context.missing(_playerIdMeta);
    }
    if (data.containsKey('hole_number')) {
      context.handle(
          _holeNumberMeta,
          holeNumber.isAcceptableOrUnknown(
              data['hole_number']!, _holeNumberMeta));
    } else if (isInserting) {
      context.missing(_holeNumberMeta);
    }
    if (data.containsKey('par')) {
      context.handle(
          _parMeta, par.isAcceptableOrUnknown(data['par']!, _parMeta));
    } else if (isInserting) {
      context.missing(_parMeta);
    }
    if (data.containsKey('stroke_index')) {
      context.handle(
          _strokeIndexMeta,
          strokeIndex.isAcceptableOrUnknown(
              data['stroke_index']!, _strokeIndexMeta));
    } else if (isInserting) {
      context.missing(_strokeIndexMeta);
    }
    if (data.containsKey('gross_strokes')) {
      context.handle(
          _grossStrokesMeta,
          grossStrokes.isAcceptableOrUnknown(
              data['gross_strokes']!, _grossStrokesMeta));
    }
    if (data.containsKey('handicap_strokes')) {
      context.handle(
          _handicapStrokesMeta,
          handicapStrokes.isAcceptableOrUnknown(
              data['handicap_strokes']!, _handicapStrokesMeta));
    } else if (isInserting) {
      context.missing(_handicapStrokesMeta);
    }
    if (data.containsKey('net_strokes')) {
      context.handle(
          _netStrokesMeta,
          netStrokes.isAcceptableOrUnknown(
              data['net_strokes']!, _netStrokesMeta));
    }
    if (data.containsKey('stableford_points')) {
      context.handle(
          _stablefordPointsMeta,
          stablefordPoints.isAcceptableOrUnknown(
              data['stableford_points']!, _stablefordPointsMeta));
    }
    if (data.containsKey('is_max_score')) {
      context.handle(
          _isMaxScoreMeta,
          isMaxScore.isAcceptableOrUnknown(
              data['is_max_score']!, _isMaxScoreMeta));
    } else if (isInserting) {
      context.missing(_isMaxScoreMeta);
    }
    return context;
  }

  @override
  Set<GeneratedColumn> get $primaryKey => {id};
  @override
  CachedHoleScore map(Map<String, dynamic> data, {String? tablePrefix}) {
    final effectivePrefix = tablePrefix != null ? '$tablePrefix.' : '';
    return CachedHoleScore(
      id: attachedDatabase.typeMapping
          .read(DriftSqlType.int, data['${effectivePrefix}id'])!,
      roundId: attachedDatabase.typeMapping
          .read(DriftSqlType.int, data['${effectivePrefix}round_id'])!,
      playerId: attachedDatabase.typeMapping
          .read(DriftSqlType.int, data['${effectivePrefix}player_id'])!,
      holeNumber: attachedDatabase.typeMapping
          .read(DriftSqlType.int, data['${effectivePrefix}hole_number'])!,
      par: attachedDatabase.typeMapping
          .read(DriftSqlType.int, data['${effectivePrefix}par'])!,
      strokeIndex: attachedDatabase.typeMapping
          .read(DriftSqlType.int, data['${effectivePrefix}stroke_index'])!,
      grossStrokes: attachedDatabase.typeMapping
          .read(DriftSqlType.int, data['${effectivePrefix}gross_strokes']),
      handicapStrokes: attachedDatabase.typeMapping
          .read(DriftSqlType.int, data['${effectivePrefix}handicap_strokes'])!,
      netStrokes: attachedDatabase.typeMapping
          .read(DriftSqlType.int, data['${effectivePrefix}net_strokes']),
      stablefordPoints: attachedDatabase.typeMapping
          .read(DriftSqlType.int, data['${effectivePrefix}stableford_points']),
      isMaxScore: attachedDatabase.typeMapping
          .read(DriftSqlType.bool, data['${effectivePrefix}is_max_score'])!,
    );
  }

  @override
  $CachedHoleScoresTable createAlias(String alias) {
    return $CachedHoleScoresTable(attachedDatabase, alias);
  }
}

class CachedHoleScore extends DataClass implements Insertable<CachedHoleScore> {
  final int id;
  final int roundId;
  final int playerId;
  final int holeNumber;
  final int par;
  final int strokeIndex;
  final int? grossStrokes;
  final int handicapStrokes;
  final int? netStrokes;
  final int? stablefordPoints;
  final bool isMaxScore;
  const CachedHoleScore(
      {required this.id,
      required this.roundId,
      required this.playerId,
      required this.holeNumber,
      required this.par,
      required this.strokeIndex,
      this.grossStrokes,
      required this.handicapStrokes,
      this.netStrokes,
      this.stablefordPoints,
      required this.isMaxScore});
  @override
  Map<String, Expression> toColumns(bool nullToAbsent) {
    final map = <String, Expression>{};
    map['id'] = Variable<int>(id);
    map['round_id'] = Variable<int>(roundId);
    map['player_id'] = Variable<int>(playerId);
    map['hole_number'] = Variable<int>(holeNumber);
    map['par'] = Variable<int>(par);
    map['stroke_index'] = Variable<int>(strokeIndex);
    if (!nullToAbsent || grossStrokes != null) {
      map['gross_strokes'] = Variable<int>(grossStrokes);
    }
    map['handicap_strokes'] = Variable<int>(handicapStrokes);
    if (!nullToAbsent || netStrokes != null) {
      map['net_strokes'] = Variable<int>(netStrokes);
    }
    if (!nullToAbsent || stablefordPoints != null) {
      map['stableford_points'] = Variable<int>(stablefordPoints);
    }
    map['is_max_score'] = Variable<bool>(isMaxScore);
    return map;
  }

  CachedHoleScoresCompanion toCompanion(bool nullToAbsent) {
    return CachedHoleScoresCompanion(
      id: Value(id),
      roundId: Value(roundId),
      playerId: Value(playerId),
      holeNumber: Value(holeNumber),
      par: Value(par),
      strokeIndex: Value(strokeIndex),
      grossStrokes: grossStrokes == null && nullToAbsent
          ? const Value.absent()
          : Value(grossStrokes),
      handicapStrokes: Value(handicapStrokes),
      netStrokes: netStrokes == null && nullToAbsent
          ? const Value.absent()
          : Value(netStrokes),
      stablefordPoints: stablefordPoints == null && nullToAbsent
          ? const Value.absent()
          : Value(stablefordPoints),
      isMaxScore: Value(isMaxScore),
    );
  }

  factory CachedHoleScore.fromJson(Map<String, dynamic> json,
      {ValueSerializer? serializer}) {
    serializer ??= driftRuntimeOptions.defaultSerializer;
    return CachedHoleScore(
      id: serializer.fromJson<int>(json['id']),
      roundId: serializer.fromJson<int>(json['roundId']),
      playerId: serializer.fromJson<int>(json['playerId']),
      holeNumber: serializer.fromJson<int>(json['holeNumber']),
      par: serializer.fromJson<int>(json['par']),
      strokeIndex: serializer.fromJson<int>(json['strokeIndex']),
      grossStrokes: serializer.fromJson<int?>(json['grossStrokes']),
      handicapStrokes: serializer.fromJson<int>(json['handicapStrokes']),
      netStrokes: serializer.fromJson<int?>(json['netStrokes']),
      stablefordPoints: serializer.fromJson<int?>(json['stablefordPoints']),
      isMaxScore: serializer.fromJson<bool>(json['isMaxScore']),
    );
  }
  @override
  Map<String, dynamic> toJson({ValueSerializer? serializer}) {
    serializer ??= driftRuntimeOptions.defaultSerializer;
    return <String, dynamic>{
      'id': serializer.toJson<int>(id),
      'roundId': serializer.toJson<int>(roundId),
      'playerId': serializer.toJson<int>(playerId),
      'holeNumber': serializer.toJson<int>(holeNumber),
      'par': serializer.toJson<int>(par),
      'strokeIndex': serializer.toJson<int>(strokeIndex),
      'grossStrokes': serializer.toJson<int?>(grossStrokes),
      'handicapStrokes': serializer.toJson<int>(handicapStrokes),
      'netStrokes': serializer.toJson<int?>(netStrokes),
      'stablefordPoints': serializer.toJson<int?>(stablefordPoints),
      'isMaxScore': serializer.toJson<bool>(isMaxScore),
    };
  }

  CachedHoleScore copyWith(
          {int? id,
          int? roundId,
          int? playerId,
          int? holeNumber,
          int? par,
          int? strokeIndex,
          Value<int?> grossStrokes = const Value.absent(),
          int? handicapStrokes,
          Value<int?> netStrokes = const Value.absent(),
          Value<int?> stablefordPoints = const Value.absent(),
          bool? isMaxScore}) =>
      CachedHoleScore(
        id: id ?? this.id,
        roundId: roundId ?? this.roundId,
        playerId: playerId ?? this.playerId,
        holeNumber: holeNumber ?? this.holeNumber,
        par: par ?? this.par,
        strokeIndex: strokeIndex ?? this.strokeIndex,
        grossStrokes:
            grossStrokes.present ? grossStrokes.value : this.grossStrokes,
        handicapStrokes: handicapStrokes ?? this.handicapStrokes,
        netStrokes: netStrokes.present ? netStrokes.value : this.netStrokes,
        stablefordPoints: stablefordPoints.present
            ? stablefordPoints.value
            : this.stablefordPoints,
        isMaxScore: isMaxScore ?? this.isMaxScore,
      );
  CachedHoleScore copyWithCompanion(CachedHoleScoresCompanion data) {
    return CachedHoleScore(
      id: data.id.present ? data.id.value : this.id,
      roundId: data.roundId.present ? data.roundId.value : this.roundId,
      playerId: data.playerId.present ? data.playerId.value : this.playerId,
      holeNumber:
          data.holeNumber.present ? data.holeNumber.value : this.holeNumber,
      par: data.par.present ? data.par.value : this.par,
      strokeIndex:
          data.strokeIndex.present ? data.strokeIndex.value : this.strokeIndex,
      grossStrokes: data.grossStrokes.present
          ? data.grossStrokes.value
          : this.grossStrokes,
      handicapStrokes: data.handicapStrokes.present
          ? data.handicapStrokes.value
          : this.handicapStrokes,
      netStrokes:
          data.netStrokes.present ? data.netStrokes.value : this.netStrokes,
      stablefordPoints: data.stablefordPoints.present
          ? data.stablefordPoints.value
          : this.stablefordPoints,
      isMaxScore:
          data.isMaxScore.present ? data.isMaxScore.value : this.isMaxScore,
    );
  }

  @override
  String toString() {
    return (StringBuffer('CachedHoleScore(')
          ..write('id: $id, ')
          ..write('roundId: $roundId, ')
          ..write('playerId: $playerId, ')
          ..write('holeNumber: $holeNumber, ')
          ..write('par: $par, ')
          ..write('strokeIndex: $strokeIndex, ')
          ..write('grossStrokes: $grossStrokes, ')
          ..write('handicapStrokes: $handicapStrokes, ')
          ..write('netStrokes: $netStrokes, ')
          ..write('stablefordPoints: $stablefordPoints, ')
          ..write('isMaxScore: $isMaxScore')
          ..write(')'))
        .toString();
  }

  @override
  int get hashCode => Object.hash(
      id,
      roundId,
      playerId,
      holeNumber,
      par,
      strokeIndex,
      grossStrokes,
      handicapStrokes,
      netStrokes,
      stablefordPoints,
      isMaxScore);
  @override
  bool operator ==(Object other) =>
      identical(this, other) ||
      (other is CachedHoleScore &&
          other.id == this.id &&
          other.roundId == this.roundId &&
          other.playerId == this.playerId &&
          other.holeNumber == this.holeNumber &&
          other.par == this.par &&
          other.strokeIndex == this.strokeIndex &&
          other.grossStrokes == this.grossStrokes &&
          other.handicapStrokes == this.handicapStrokes &&
          other.netStrokes == this.netStrokes &&
          other.stablefordPoints == this.stablefordPoints &&
          other.isMaxScore == this.isMaxScore);
}

class CachedHoleScoresCompanion extends UpdateCompanion<CachedHoleScore> {
  final Value<int> id;
  final Value<int> roundId;
  final Value<int> playerId;
  final Value<int> holeNumber;
  final Value<int> par;
  final Value<int> strokeIndex;
  final Value<int?> grossStrokes;
  final Value<int> handicapStrokes;
  final Value<int?> netStrokes;
  final Value<int?> stablefordPoints;
  final Value<bool> isMaxScore;
  const CachedHoleScoresCompanion({
    this.id = const Value.absent(),
    this.roundId = const Value.absent(),
    this.playerId = const Value.absent(),
    this.holeNumber = const Value.absent(),
    this.par = const Value.absent(),
    this.strokeIndex = const Value.absent(),
    this.grossStrokes = const Value.absent(),
    this.handicapStrokes = const Value.absent(),
    this.netStrokes = const Value.absent(),
    this.stablefordPoints = const Value.absent(),
    this.isMaxScore = const Value.absent(),
  });
  CachedHoleScoresCompanion.insert({
    this.id = const Value.absent(),
    required int roundId,
    required int playerId,
    required int holeNumber,
    required int par,
    required int strokeIndex,
    this.grossStrokes = const Value.absent(),
    required int handicapStrokes,
    this.netStrokes = const Value.absent(),
    this.stablefordPoints = const Value.absent(),
    required bool isMaxScore,
  })  : roundId = Value(roundId),
        playerId = Value(playerId),
        holeNumber = Value(holeNumber),
        par = Value(par),
        strokeIndex = Value(strokeIndex),
        handicapStrokes = Value(handicapStrokes),
        isMaxScore = Value(isMaxScore);
  static Insertable<CachedHoleScore> custom({
    Expression<int>? id,
    Expression<int>? roundId,
    Expression<int>? playerId,
    Expression<int>? holeNumber,
    Expression<int>? par,
    Expression<int>? strokeIndex,
    Expression<int>? grossStrokes,
    Expression<int>? handicapStrokes,
    Expression<int>? netStrokes,
    Expression<int>? stablefordPoints,
    Expression<bool>? isMaxScore,
  }) {
    return RawValuesInsertable({
      if (id != null) 'id': id,
      if (roundId != null) 'round_id': roundId,
      if (playerId != null) 'player_id': playerId,
      if (holeNumber != null) 'hole_number': holeNumber,
      if (par != null) 'par': par,
      if (strokeIndex != null) 'stroke_index': strokeIndex,
      if (grossStrokes != null) 'gross_strokes': grossStrokes,
      if (handicapStrokes != null) 'handicap_strokes': handicapStrokes,
      if (netStrokes != null) 'net_strokes': netStrokes,
      if (stablefordPoints != null) 'stableford_points': stablefordPoints,
      if (isMaxScore != null) 'is_max_score': isMaxScore,
    });
  }

  CachedHoleScoresCompanion copyWith(
      {Value<int>? id,
      Value<int>? roundId,
      Value<int>? playerId,
      Value<int>? holeNumber,
      Value<int>? par,
      Value<int>? strokeIndex,
      Value<int?>? grossStrokes,
      Value<int>? handicapStrokes,
      Value<int?>? netStrokes,
      Value<int?>? stablefordPoints,
      Value<bool>? isMaxScore}) {
    return CachedHoleScoresCompanion(
      id: id ?? this.id,
      roundId: roundId ?? this.roundId,
      playerId: playerId ?? this.playerId,
      holeNumber: holeNumber ?? this.holeNumber,
      par: par ?? this.par,
      strokeIndex: strokeIndex ?? this.strokeIndex,
      grossStrokes: grossStrokes ?? this.grossStrokes,
      handicapStrokes: handicapStrokes ?? this.handicapStrokes,
      netStrokes: netStrokes ?? this.netStrokes,
      stablefordPoints: stablefordPoints ?? this.stablefordPoints,
      isMaxScore: isMaxScore ?? this.isMaxScore,
    );
  }

  @override
  Map<String, Expression> toColumns(bool nullToAbsent) {
    final map = <String, Expression>{};
    if (id.present) {
      map['id'] = Variable<int>(id.value);
    }
    if (roundId.present) {
      map['round_id'] = Variable<int>(roundId.value);
    }
    if (playerId.present) {
      map['player_id'] = Variable<int>(playerId.value);
    }
    if (holeNumber.present) {
      map['hole_number'] = Variable<int>(holeNumber.value);
    }
    if (par.present) {
      map['par'] = Variable<int>(par.value);
    }
    if (strokeIndex.present) {
      map['stroke_index'] = Variable<int>(strokeIndex.value);
    }
    if (grossStrokes.present) {
      map['gross_strokes'] = Variable<int>(grossStrokes.value);
    }
    if (handicapStrokes.present) {
      map['handicap_strokes'] = Variable<int>(handicapStrokes.value);
    }
    if (netStrokes.present) {
      map['net_strokes'] = Variable<int>(netStrokes.value);
    }
    if (stablefordPoints.present) {
      map['stableford_points'] = Variable<int>(stablefordPoints.value);
    }
    if (isMaxScore.present) {
      map['is_max_score'] = Variable<bool>(isMaxScore.value);
    }
    return map;
  }

  @override
  String toString() {
    return (StringBuffer('CachedHoleScoresCompanion(')
          ..write('id: $id, ')
          ..write('roundId: $roundId, ')
          ..write('playerId: $playerId, ')
          ..write('holeNumber: $holeNumber, ')
          ..write('par: $par, ')
          ..write('strokeIndex: $strokeIndex, ')
          ..write('grossStrokes: $grossStrokes, ')
          ..write('handicapStrokes: $handicapStrokes, ')
          ..write('netStrokes: $netStrokes, ')
          ..write('stablefordPoints: $stablefordPoints, ')
          ..write('isMaxScore: $isMaxScore')
          ..write(')'))
        .toString();
  }
}

class $PendingSyncScoresTable extends PendingSyncScores
    with TableInfo<$PendingSyncScoresTable, PendingSyncScore> {
  @override
  final GeneratedDatabase attachedDatabase;
  final String? _alias;
  $PendingSyncScoresTable(this.attachedDatabase, [this._alias]);
  static const VerificationMeta _idMeta = const VerificationMeta('id');
  @override
  late final GeneratedColumn<int> id = GeneratedColumn<int>(
      'id', aliasedName, false,
      hasAutoIncrement: true,
      type: DriftSqlType.int,
      requiredDuringInsert: false,
      defaultConstraints:
          GeneratedColumn.constraintIsAlways('PRIMARY KEY AUTOINCREMENT'));
  static const VerificationMeta _roundIdMeta =
      const VerificationMeta('roundId');
  @override
  late final GeneratedColumn<int> roundId = GeneratedColumn<int>(
      'round_id', aliasedName, false,
      type: DriftSqlType.int, requiredDuringInsert: true);
  static const VerificationMeta _playerIdMeta =
      const VerificationMeta('playerId');
  @override
  late final GeneratedColumn<int> playerId = GeneratedColumn<int>(
      'player_id', aliasedName, false,
      type: DriftSqlType.int, requiredDuringInsert: true);
  static const VerificationMeta _holeNumberMeta =
      const VerificationMeta('holeNumber');
  @override
  late final GeneratedColumn<int> holeNumber = GeneratedColumn<int>(
      'hole_number', aliasedName, false,
      type: DriftSqlType.int, requiredDuringInsert: true);
  static const VerificationMeta _grossStrokesMeta =
      const VerificationMeta('grossStrokes');
  @override
  late final GeneratedColumn<int> grossStrokes = GeneratedColumn<int>(
      'gross_strokes', aliasedName, false,
      type: DriftSqlType.int, requiredDuringInsert: true);
  static const VerificationMeta _pendingSyncMeta =
      const VerificationMeta('pendingSync');
  @override
  late final GeneratedColumn<bool> pendingSync = GeneratedColumn<bool>(
      'pending_sync', aliasedName, false,
      type: DriftSqlType.bool,
      requiredDuringInsert: false,
      defaultConstraints: GeneratedColumn.constraintIsAlways(
          'CHECK ("pending_sync" IN (0, 1))'),
      defaultValue: const Constant(true));
  static const VerificationMeta _createdAtMeta =
      const VerificationMeta('createdAt');
  @override
  late final GeneratedColumn<DateTime> createdAt = GeneratedColumn<DateTime>(
      'created_at', aliasedName, false,
      type: DriftSqlType.dateTime, requiredDuringInsert: true);
  @override
  List<GeneratedColumn> get $columns =>
      [id, roundId, playerId, holeNumber, grossStrokes, pendingSync, createdAt];
  @override
  String get aliasedName => _alias ?? actualTableName;
  @override
  String get actualTableName => $name;
  static const String $name = 'pending_sync_scores';
  @override
  VerificationContext validateIntegrity(Insertable<PendingSyncScore> instance,
      {bool isInserting = false}) {
    final context = VerificationContext();
    final data = instance.toColumns(true);
    if (data.containsKey('id')) {
      context.handle(_idMeta, id.isAcceptableOrUnknown(data['id']!, _idMeta));
    }
    if (data.containsKey('round_id')) {
      context.handle(_roundIdMeta,
          roundId.isAcceptableOrUnknown(data['round_id']!, _roundIdMeta));
    } else if (isInserting) {
      context.missing(_roundIdMeta);
    }
    if (data.containsKey('player_id')) {
      context.handle(_playerIdMeta,
          playerId.isAcceptableOrUnknown(data['player_id']!, _playerIdMeta));
    } else if (isInserting) {
      context.missing(_playerIdMeta);
    }
    if (data.containsKey('hole_number')) {
      context.handle(
          _holeNumberMeta,
          holeNumber.isAcceptableOrUnknown(
              data['hole_number']!, _holeNumberMeta));
    } else if (isInserting) {
      context.missing(_holeNumberMeta);
    }
    if (data.containsKey('gross_strokes')) {
      context.handle(
          _grossStrokesMeta,
          grossStrokes.isAcceptableOrUnknown(
              data['gross_strokes']!, _grossStrokesMeta));
    } else if (isInserting) {
      context.missing(_grossStrokesMeta);
    }
    if (data.containsKey('pending_sync')) {
      context.handle(
          _pendingSyncMeta,
          pendingSync.isAcceptableOrUnknown(
              data['pending_sync']!, _pendingSyncMeta));
    }
    if (data.containsKey('created_at')) {
      context.handle(_createdAtMeta,
          createdAt.isAcceptableOrUnknown(data['created_at']!, _createdAtMeta));
    } else if (isInserting) {
      context.missing(_createdAtMeta);
    }
    return context;
  }

  @override
  Set<GeneratedColumn> get $primaryKey => {id};
  @override
  PendingSyncScore map(Map<String, dynamic> data, {String? tablePrefix}) {
    final effectivePrefix = tablePrefix != null ? '$tablePrefix.' : '';
    return PendingSyncScore(
      id: attachedDatabase.typeMapping
          .read(DriftSqlType.int, data['${effectivePrefix}id'])!,
      roundId: attachedDatabase.typeMapping
          .read(DriftSqlType.int, data['${effectivePrefix}round_id'])!,
      playerId: attachedDatabase.typeMapping
          .read(DriftSqlType.int, data['${effectivePrefix}player_id'])!,
      holeNumber: attachedDatabase.typeMapping
          .read(DriftSqlType.int, data['${effectivePrefix}hole_number'])!,
      grossStrokes: attachedDatabase.typeMapping
          .read(DriftSqlType.int, data['${effectivePrefix}gross_strokes'])!,
      pendingSync: attachedDatabase.typeMapping
          .read(DriftSqlType.bool, data['${effectivePrefix}pending_sync'])!,
      createdAt: attachedDatabase.typeMapping
          .read(DriftSqlType.dateTime, data['${effectivePrefix}created_at'])!,
    );
  }

  @override
  $PendingSyncScoresTable createAlias(String alias) {
    return $PendingSyncScoresTable(attachedDatabase, alias);
  }
}

class PendingSyncScore extends DataClass
    implements Insertable<PendingSyncScore> {
  final int id;
  final int roundId;
  final int playerId;
  final int holeNumber;
  final int grossStrokes;
  final bool pendingSync;
  final DateTime createdAt;
  const PendingSyncScore(
      {required this.id,
      required this.roundId,
      required this.playerId,
      required this.holeNumber,
      required this.grossStrokes,
      required this.pendingSync,
      required this.createdAt});
  @override
  Map<String, Expression> toColumns(bool nullToAbsent) {
    final map = <String, Expression>{};
    map['id'] = Variable<int>(id);
    map['round_id'] = Variable<int>(roundId);
    map['player_id'] = Variable<int>(playerId);
    map['hole_number'] = Variable<int>(holeNumber);
    map['gross_strokes'] = Variable<int>(grossStrokes);
    map['pending_sync'] = Variable<bool>(pendingSync);
    map['created_at'] = Variable<DateTime>(createdAt);
    return map;
  }

  PendingSyncScoresCompanion toCompanion(bool nullToAbsent) {
    return PendingSyncScoresCompanion(
      id: Value(id),
      roundId: Value(roundId),
      playerId: Value(playerId),
      holeNumber: Value(holeNumber),
      grossStrokes: Value(grossStrokes),
      pendingSync: Value(pendingSync),
      createdAt: Value(createdAt),
    );
  }

  factory PendingSyncScore.fromJson(Map<String, dynamic> json,
      {ValueSerializer? serializer}) {
    serializer ??= driftRuntimeOptions.defaultSerializer;
    return PendingSyncScore(
      id: serializer.fromJson<int>(json['id']),
      roundId: serializer.fromJson<int>(json['roundId']),
      playerId: serializer.fromJson<int>(json['playerId']),
      holeNumber: serializer.fromJson<int>(json['holeNumber']),
      grossStrokes: serializer.fromJson<int>(json['grossStrokes']),
      pendingSync: serializer.fromJson<bool>(json['pendingSync']),
      createdAt: serializer.fromJson<DateTime>(json['createdAt']),
    );
  }
  @override
  Map<String, dynamic> toJson({ValueSerializer? serializer}) {
    serializer ??= driftRuntimeOptions.defaultSerializer;
    return <String, dynamic>{
      'id': serializer.toJson<int>(id),
      'roundId': serializer.toJson<int>(roundId),
      'playerId': serializer.toJson<int>(playerId),
      'holeNumber': serializer.toJson<int>(holeNumber),
      'grossStrokes': serializer.toJson<int>(grossStrokes),
      'pendingSync': serializer.toJson<bool>(pendingSync),
      'createdAt': serializer.toJson<DateTime>(createdAt),
    };
  }

  PendingSyncScore copyWith(
          {int? id,
          int? roundId,
          int? playerId,
          int? holeNumber,
          int? grossStrokes,
          bool? pendingSync,
          DateTime? createdAt}) =>
      PendingSyncScore(
        id: id ?? this.id,
        roundId: roundId ?? this.roundId,
        playerId: playerId ?? this.playerId,
        holeNumber: holeNumber ?? this.holeNumber,
        grossStrokes: grossStrokes ?? this.grossStrokes,
        pendingSync: pendingSync ?? this.pendingSync,
        createdAt: createdAt ?? this.createdAt,
      );
  PendingSyncScore copyWithCompanion(PendingSyncScoresCompanion data) {
    return PendingSyncScore(
      id: data.id.present ? data.id.value : this.id,
      roundId: data.roundId.present ? data.roundId.value : this.roundId,
      playerId: data.playerId.present ? data.playerId.value : this.playerId,
      holeNumber:
          data.holeNumber.present ? data.holeNumber.value : this.holeNumber,
      grossStrokes: data.grossStrokes.present
          ? data.grossStrokes.value
          : this.grossStrokes,
      pendingSync:
          data.pendingSync.present ? data.pendingSync.value : this.pendingSync,
      createdAt: data.createdAt.present ? data.createdAt.value : this.createdAt,
    );
  }

  @override
  String toString() {
    return (StringBuffer('PendingSyncScore(')
          ..write('id: $id, ')
          ..write('roundId: $roundId, ')
          ..write('playerId: $playerId, ')
          ..write('holeNumber: $holeNumber, ')
          ..write('grossStrokes: $grossStrokes, ')
          ..write('pendingSync: $pendingSync, ')
          ..write('createdAt: $createdAt')
          ..write(')'))
        .toString();
  }

  @override
  int get hashCode => Object.hash(
      id, roundId, playerId, holeNumber, grossStrokes, pendingSync, createdAt);
  @override
  bool operator ==(Object other) =>
      identical(this, other) ||
      (other is PendingSyncScore &&
          other.id == this.id &&
          other.roundId == this.roundId &&
          other.playerId == this.playerId &&
          other.holeNumber == this.holeNumber &&
          other.grossStrokes == this.grossStrokes &&
          other.pendingSync == this.pendingSync &&
          other.createdAt == this.createdAt);
}

class PendingSyncScoresCompanion extends UpdateCompanion<PendingSyncScore> {
  final Value<int> id;
  final Value<int> roundId;
  final Value<int> playerId;
  final Value<int> holeNumber;
  final Value<int> grossStrokes;
  final Value<bool> pendingSync;
  final Value<DateTime> createdAt;
  const PendingSyncScoresCompanion({
    this.id = const Value.absent(),
    this.roundId = const Value.absent(),
    this.playerId = const Value.absent(),
    this.holeNumber = const Value.absent(),
    this.grossStrokes = const Value.absent(),
    this.pendingSync = const Value.absent(),
    this.createdAt = const Value.absent(),
  });
  PendingSyncScoresCompanion.insert({
    this.id = const Value.absent(),
    required int roundId,
    required int playerId,
    required int holeNumber,
    required int grossStrokes,
    this.pendingSync = const Value.absent(),
    required DateTime createdAt,
  })  : roundId = Value(roundId),
        playerId = Value(playerId),
        holeNumber = Value(holeNumber),
        grossStrokes = Value(grossStrokes),
        createdAt = Value(createdAt);
  static Insertable<PendingSyncScore> custom({
    Expression<int>? id,
    Expression<int>? roundId,
    Expression<int>? playerId,
    Expression<int>? holeNumber,
    Expression<int>? grossStrokes,
    Expression<bool>? pendingSync,
    Expression<DateTime>? createdAt,
  }) {
    return RawValuesInsertable({
      if (id != null) 'id': id,
      if (roundId != null) 'round_id': roundId,
      if (playerId != null) 'player_id': playerId,
      if (holeNumber != null) 'hole_number': holeNumber,
      if (grossStrokes != null) 'gross_strokes': grossStrokes,
      if (pendingSync != null) 'pending_sync': pendingSync,
      if (createdAt != null) 'created_at': createdAt,
    });
  }

  PendingSyncScoresCompanion copyWith(
      {Value<int>? id,
      Value<int>? roundId,
      Value<int>? playerId,
      Value<int>? holeNumber,
      Value<int>? grossStrokes,
      Value<bool>? pendingSync,
      Value<DateTime>? createdAt}) {
    return PendingSyncScoresCompanion(
      id: id ?? this.id,
      roundId: roundId ?? this.roundId,
      playerId: playerId ?? this.playerId,
      holeNumber: holeNumber ?? this.holeNumber,
      grossStrokes: grossStrokes ?? this.grossStrokes,
      pendingSync: pendingSync ?? this.pendingSync,
      createdAt: createdAt ?? this.createdAt,
    );
  }

  @override
  Map<String, Expression> toColumns(bool nullToAbsent) {
    final map = <String, Expression>{};
    if (id.present) {
      map['id'] = Variable<int>(id.value);
    }
    if (roundId.present) {
      map['round_id'] = Variable<int>(roundId.value);
    }
    if (playerId.present) {
      map['player_id'] = Variable<int>(playerId.value);
    }
    if (holeNumber.present) {
      map['hole_number'] = Variable<int>(holeNumber.value);
    }
    if (grossStrokes.present) {
      map['gross_strokes'] = Variable<int>(grossStrokes.value);
    }
    if (pendingSync.present) {
      map['pending_sync'] = Variable<bool>(pendingSync.value);
    }
    if (createdAt.present) {
      map['created_at'] = Variable<DateTime>(createdAt.value);
    }
    return map;
  }

  @override
  String toString() {
    return (StringBuffer('PendingSyncScoresCompanion(')
          ..write('id: $id, ')
          ..write('roundId: $roundId, ')
          ..write('playerId: $playerId, ')
          ..write('holeNumber: $holeNumber, ')
          ..write('grossStrokes: $grossStrokes, ')
          ..write('pendingSync: $pendingSync, ')
          ..write('createdAt: $createdAt')
          ..write(')'))
        .toString();
  }
}

abstract class _$AppDatabase extends GeneratedDatabase {
  _$AppDatabase(QueryExecutor e) : super(e);
  $AppDatabaseManager get managers => $AppDatabaseManager(this);
  late final $CachedFlightsTable cachedFlights = $CachedFlightsTable(this);
  late final $CachedLeaderboardEntriesTable cachedLeaderboardEntries =
      $CachedLeaderboardEntriesTable(this);
  late final $CachedRoundsTable cachedRounds = $CachedRoundsTable(this);
  late final $CachedHoleScoresTable cachedHoleScores =
      $CachedHoleScoresTable(this);
  late final $PendingSyncScoresTable pendingSyncScores =
      $PendingSyncScoresTable(this);
  @override
  Iterable<TableInfo<Table, Object?>> get allTables =>
      allSchemaEntities.whereType<TableInfo<Table, Object?>>();
  @override
  List<DatabaseSchemaEntity> get allSchemaEntities => [
        cachedFlights,
        cachedLeaderboardEntries,
        cachedRounds,
        cachedHoleScores,
        pendingSyncScores
      ];
}

typedef $$CachedFlightsTableCreateCompanionBuilder = CachedFlightsCompanion
    Function({
  Value<int> id,
  required String name,
  Value<String?> description,
  required DateTime cachedAt,
});
typedef $$CachedFlightsTableUpdateCompanionBuilder = CachedFlightsCompanion
    Function({
  Value<int> id,
  Value<String> name,
  Value<String?> description,
  Value<DateTime> cachedAt,
});

class $$CachedFlightsTableFilterComposer
    extends Composer<_$AppDatabase, $CachedFlightsTable> {
  $$CachedFlightsTableFilterComposer({
    required super.$db,
    required super.$table,
    super.joinBuilder,
    super.$addJoinBuilderToRootComposer,
    super.$removeJoinBuilderFromRootComposer,
  });
  ColumnFilters<int> get id => $composableBuilder(
      column: $table.id, builder: (column) => ColumnFilters(column));

  ColumnFilters<String> get name => $composableBuilder(
      column: $table.name, builder: (column) => ColumnFilters(column));

  ColumnFilters<String> get description => $composableBuilder(
      column: $table.description, builder: (column) => ColumnFilters(column));

  ColumnFilters<DateTime> get cachedAt => $composableBuilder(
      column: $table.cachedAt, builder: (column) => ColumnFilters(column));
}

class $$CachedFlightsTableOrderingComposer
    extends Composer<_$AppDatabase, $CachedFlightsTable> {
  $$CachedFlightsTableOrderingComposer({
    required super.$db,
    required super.$table,
    super.joinBuilder,
    super.$addJoinBuilderToRootComposer,
    super.$removeJoinBuilderFromRootComposer,
  });
  ColumnOrderings<int> get id => $composableBuilder(
      column: $table.id, builder: (column) => ColumnOrderings(column));

  ColumnOrderings<String> get name => $composableBuilder(
      column: $table.name, builder: (column) => ColumnOrderings(column));

  ColumnOrderings<String> get description => $composableBuilder(
      column: $table.description, builder: (column) => ColumnOrderings(column));

  ColumnOrderings<DateTime> get cachedAt => $composableBuilder(
      column: $table.cachedAt, builder: (column) => ColumnOrderings(column));
}

class $$CachedFlightsTableAnnotationComposer
    extends Composer<_$AppDatabase, $CachedFlightsTable> {
  $$CachedFlightsTableAnnotationComposer({
    required super.$db,
    required super.$table,
    super.joinBuilder,
    super.$addJoinBuilderToRootComposer,
    super.$removeJoinBuilderFromRootComposer,
  });
  GeneratedColumn<int> get id =>
      $composableBuilder(column: $table.id, builder: (column) => column);

  GeneratedColumn<String> get name =>
      $composableBuilder(column: $table.name, builder: (column) => column);

  GeneratedColumn<String> get description => $composableBuilder(
      column: $table.description, builder: (column) => column);

  GeneratedColumn<DateTime> get cachedAt =>
      $composableBuilder(column: $table.cachedAt, builder: (column) => column);
}

class $$CachedFlightsTableTableManager extends RootTableManager<
    _$AppDatabase,
    $CachedFlightsTable,
    CachedFlight,
    $$CachedFlightsTableFilterComposer,
    $$CachedFlightsTableOrderingComposer,
    $$CachedFlightsTableAnnotationComposer,
    $$CachedFlightsTableCreateCompanionBuilder,
    $$CachedFlightsTableUpdateCompanionBuilder,
    (
      CachedFlight,
      BaseReferences<_$AppDatabase, $CachedFlightsTable, CachedFlight>
    ),
    CachedFlight,
    PrefetchHooks Function()> {
  $$CachedFlightsTableTableManager(_$AppDatabase db, $CachedFlightsTable table)
      : super(TableManagerState(
          db: db,
          table: table,
          createFilteringComposer: () =>
              $$CachedFlightsTableFilterComposer($db: db, $table: table),
          createOrderingComposer: () =>
              $$CachedFlightsTableOrderingComposer($db: db, $table: table),
          createComputedFieldComposer: () =>
              $$CachedFlightsTableAnnotationComposer($db: db, $table: table),
          updateCompanionCallback: ({
            Value<int> id = const Value.absent(),
            Value<String> name = const Value.absent(),
            Value<String?> description = const Value.absent(),
            Value<DateTime> cachedAt = const Value.absent(),
          }) =>
              CachedFlightsCompanion(
            id: id,
            name: name,
            description: description,
            cachedAt: cachedAt,
          ),
          createCompanionCallback: ({
            Value<int> id = const Value.absent(),
            required String name,
            Value<String?> description = const Value.absent(),
            required DateTime cachedAt,
          }) =>
              CachedFlightsCompanion.insert(
            id: id,
            name: name,
            description: description,
            cachedAt: cachedAt,
          ),
          withReferenceMapper: (p0) => p0
              .map((e) => (e.readTable(table), BaseReferences(db, table, e)))
              .toList(),
          prefetchHooksCallback: null,
        ));
}

typedef $$CachedFlightsTableProcessedTableManager = ProcessedTableManager<
    _$AppDatabase,
    $CachedFlightsTable,
    CachedFlight,
    $$CachedFlightsTableFilterComposer,
    $$CachedFlightsTableOrderingComposer,
    $$CachedFlightsTableAnnotationComposer,
    $$CachedFlightsTableCreateCompanionBuilder,
    $$CachedFlightsTableUpdateCompanionBuilder,
    (
      CachedFlight,
      BaseReferences<_$AppDatabase, $CachedFlightsTable, CachedFlight>
    ),
    CachedFlight,
    PrefetchHooks Function()>;
typedef $$CachedLeaderboardEntriesTableCreateCompanionBuilder
    = CachedLeaderboardEntriesCompanion Function({
  Value<int> id,
  required int flightId,
  required int playerId,
  required String playerName,
  required int totalStablefordPoints,
  required int roundsPlayed,
  required int currentRank,
  Value<int?> previousRank,
  required double currentHandicap,
  required DateTime cachedAt,
});
typedef $$CachedLeaderboardEntriesTableUpdateCompanionBuilder
    = CachedLeaderboardEntriesCompanion Function({
  Value<int> id,
  Value<int> flightId,
  Value<int> playerId,
  Value<String> playerName,
  Value<int> totalStablefordPoints,
  Value<int> roundsPlayed,
  Value<int> currentRank,
  Value<int?> previousRank,
  Value<double> currentHandicap,
  Value<DateTime> cachedAt,
});

class $$CachedLeaderboardEntriesTableFilterComposer
    extends Composer<_$AppDatabase, $CachedLeaderboardEntriesTable> {
  $$CachedLeaderboardEntriesTableFilterComposer({
    required super.$db,
    required super.$table,
    super.joinBuilder,
    super.$addJoinBuilderToRootComposer,
    super.$removeJoinBuilderFromRootComposer,
  });
  ColumnFilters<int> get id => $composableBuilder(
      column: $table.id, builder: (column) => ColumnFilters(column));

  ColumnFilters<int> get flightId => $composableBuilder(
      column: $table.flightId, builder: (column) => ColumnFilters(column));

  ColumnFilters<int> get playerId => $composableBuilder(
      column: $table.playerId, builder: (column) => ColumnFilters(column));

  ColumnFilters<String> get playerName => $composableBuilder(
      column: $table.playerName, builder: (column) => ColumnFilters(column));

  ColumnFilters<int> get totalStablefordPoints => $composableBuilder(
      column: $table.totalStablefordPoints,
      builder: (column) => ColumnFilters(column));

  ColumnFilters<int> get roundsPlayed => $composableBuilder(
      column: $table.roundsPlayed, builder: (column) => ColumnFilters(column));

  ColumnFilters<int> get currentRank => $composableBuilder(
      column: $table.currentRank, builder: (column) => ColumnFilters(column));

  ColumnFilters<int> get previousRank => $composableBuilder(
      column: $table.previousRank, builder: (column) => ColumnFilters(column));

  ColumnFilters<double> get currentHandicap => $composableBuilder(
      column: $table.currentHandicap,
      builder: (column) => ColumnFilters(column));

  ColumnFilters<DateTime> get cachedAt => $composableBuilder(
      column: $table.cachedAt, builder: (column) => ColumnFilters(column));
}

class $$CachedLeaderboardEntriesTableOrderingComposer
    extends Composer<_$AppDatabase, $CachedLeaderboardEntriesTable> {
  $$CachedLeaderboardEntriesTableOrderingComposer({
    required super.$db,
    required super.$table,
    super.joinBuilder,
    super.$addJoinBuilderToRootComposer,
    super.$removeJoinBuilderFromRootComposer,
  });
  ColumnOrderings<int> get id => $composableBuilder(
      column: $table.id, builder: (column) => ColumnOrderings(column));

  ColumnOrderings<int> get flightId => $composableBuilder(
      column: $table.flightId, builder: (column) => ColumnOrderings(column));

  ColumnOrderings<int> get playerId => $composableBuilder(
      column: $table.playerId, builder: (column) => ColumnOrderings(column));

  ColumnOrderings<String> get playerName => $composableBuilder(
      column: $table.playerName, builder: (column) => ColumnOrderings(column));

  ColumnOrderings<int> get totalStablefordPoints => $composableBuilder(
      column: $table.totalStablefordPoints,
      builder: (column) => ColumnOrderings(column));

  ColumnOrderings<int> get roundsPlayed => $composableBuilder(
      column: $table.roundsPlayed,
      builder: (column) => ColumnOrderings(column));

  ColumnOrderings<int> get currentRank => $composableBuilder(
      column: $table.currentRank, builder: (column) => ColumnOrderings(column));

  ColumnOrderings<int> get previousRank => $composableBuilder(
      column: $table.previousRank,
      builder: (column) => ColumnOrderings(column));

  ColumnOrderings<double> get currentHandicap => $composableBuilder(
      column: $table.currentHandicap,
      builder: (column) => ColumnOrderings(column));

  ColumnOrderings<DateTime> get cachedAt => $composableBuilder(
      column: $table.cachedAt, builder: (column) => ColumnOrderings(column));
}

class $$CachedLeaderboardEntriesTableAnnotationComposer
    extends Composer<_$AppDatabase, $CachedLeaderboardEntriesTable> {
  $$CachedLeaderboardEntriesTableAnnotationComposer({
    required super.$db,
    required super.$table,
    super.joinBuilder,
    super.$addJoinBuilderToRootComposer,
    super.$removeJoinBuilderFromRootComposer,
  });
  GeneratedColumn<int> get id =>
      $composableBuilder(column: $table.id, builder: (column) => column);

  GeneratedColumn<int> get flightId =>
      $composableBuilder(column: $table.flightId, builder: (column) => column);

  GeneratedColumn<int> get playerId =>
      $composableBuilder(column: $table.playerId, builder: (column) => column);

  GeneratedColumn<String> get playerName => $composableBuilder(
      column: $table.playerName, builder: (column) => column);

  GeneratedColumn<int> get totalStablefordPoints => $composableBuilder(
      column: $table.totalStablefordPoints, builder: (column) => column);

  GeneratedColumn<int> get roundsPlayed => $composableBuilder(
      column: $table.roundsPlayed, builder: (column) => column);

  GeneratedColumn<int> get currentRank => $composableBuilder(
      column: $table.currentRank, builder: (column) => column);

  GeneratedColumn<int> get previousRank => $composableBuilder(
      column: $table.previousRank, builder: (column) => column);

  GeneratedColumn<double> get currentHandicap => $composableBuilder(
      column: $table.currentHandicap, builder: (column) => column);

  GeneratedColumn<DateTime> get cachedAt =>
      $composableBuilder(column: $table.cachedAt, builder: (column) => column);
}

class $$CachedLeaderboardEntriesTableTableManager extends RootTableManager<
    _$AppDatabase,
    $CachedLeaderboardEntriesTable,
    CachedLeaderboardEntry,
    $$CachedLeaderboardEntriesTableFilterComposer,
    $$CachedLeaderboardEntriesTableOrderingComposer,
    $$CachedLeaderboardEntriesTableAnnotationComposer,
    $$CachedLeaderboardEntriesTableCreateCompanionBuilder,
    $$CachedLeaderboardEntriesTableUpdateCompanionBuilder,
    (
      CachedLeaderboardEntry,
      BaseReferences<_$AppDatabase, $CachedLeaderboardEntriesTable,
          CachedLeaderboardEntry>
    ),
    CachedLeaderboardEntry,
    PrefetchHooks Function()> {
  $$CachedLeaderboardEntriesTableTableManager(
      _$AppDatabase db, $CachedLeaderboardEntriesTable table)
      : super(TableManagerState(
          db: db,
          table: table,
          createFilteringComposer: () =>
              $$CachedLeaderboardEntriesTableFilterComposer(
                  $db: db, $table: table),
          createOrderingComposer: () =>
              $$CachedLeaderboardEntriesTableOrderingComposer(
                  $db: db, $table: table),
          createComputedFieldComposer: () =>
              $$CachedLeaderboardEntriesTableAnnotationComposer(
                  $db: db, $table: table),
          updateCompanionCallback: ({
            Value<int> id = const Value.absent(),
            Value<int> flightId = const Value.absent(),
            Value<int> playerId = const Value.absent(),
            Value<String> playerName = const Value.absent(),
            Value<int> totalStablefordPoints = const Value.absent(),
            Value<int> roundsPlayed = const Value.absent(),
            Value<int> currentRank = const Value.absent(),
            Value<int?> previousRank = const Value.absent(),
            Value<double> currentHandicap = const Value.absent(),
            Value<DateTime> cachedAt = const Value.absent(),
          }) =>
              CachedLeaderboardEntriesCompanion(
            id: id,
            flightId: flightId,
            playerId: playerId,
            playerName: playerName,
            totalStablefordPoints: totalStablefordPoints,
            roundsPlayed: roundsPlayed,
            currentRank: currentRank,
            previousRank: previousRank,
            currentHandicap: currentHandicap,
            cachedAt: cachedAt,
          ),
          createCompanionCallback: ({
            Value<int> id = const Value.absent(),
            required int flightId,
            required int playerId,
            required String playerName,
            required int totalStablefordPoints,
            required int roundsPlayed,
            required int currentRank,
            Value<int?> previousRank = const Value.absent(),
            required double currentHandicap,
            required DateTime cachedAt,
          }) =>
              CachedLeaderboardEntriesCompanion.insert(
            id: id,
            flightId: flightId,
            playerId: playerId,
            playerName: playerName,
            totalStablefordPoints: totalStablefordPoints,
            roundsPlayed: roundsPlayed,
            currentRank: currentRank,
            previousRank: previousRank,
            currentHandicap: currentHandicap,
            cachedAt: cachedAt,
          ),
          withReferenceMapper: (p0) => p0
              .map((e) => (e.readTable(table), BaseReferences(db, table, e)))
              .toList(),
          prefetchHooksCallback: null,
        ));
}

typedef $$CachedLeaderboardEntriesTableProcessedTableManager
    = ProcessedTableManager<
        _$AppDatabase,
        $CachedLeaderboardEntriesTable,
        CachedLeaderboardEntry,
        $$CachedLeaderboardEntriesTableFilterComposer,
        $$CachedLeaderboardEntriesTableOrderingComposer,
        $$CachedLeaderboardEntriesTableAnnotationComposer,
        $$CachedLeaderboardEntriesTableCreateCompanionBuilder,
        $$CachedLeaderboardEntriesTableUpdateCompanionBuilder,
        (
          CachedLeaderboardEntry,
          BaseReferences<_$AppDatabase, $CachedLeaderboardEntriesTable,
              CachedLeaderboardEntry>
        ),
        CachedLeaderboardEntry,
        PrefetchHooks Function()>;
typedef $$CachedRoundsTableCreateCompanionBuilder = CachedRoundsCompanion
    Function({
  Value<int> id,
  required String courseName,
  required DateTime scheduledDate,
  Value<DateTime?> playedDate,
  required String status,
  required int roundNumber,
  Value<String?> weatherConditions,
  required DateTime cachedAt,
});
typedef $$CachedRoundsTableUpdateCompanionBuilder = CachedRoundsCompanion
    Function({
  Value<int> id,
  Value<String> courseName,
  Value<DateTime> scheduledDate,
  Value<DateTime?> playedDate,
  Value<String> status,
  Value<int> roundNumber,
  Value<String?> weatherConditions,
  Value<DateTime> cachedAt,
});

class $$CachedRoundsTableFilterComposer
    extends Composer<_$AppDatabase, $CachedRoundsTable> {
  $$CachedRoundsTableFilterComposer({
    required super.$db,
    required super.$table,
    super.joinBuilder,
    super.$addJoinBuilderToRootComposer,
    super.$removeJoinBuilderFromRootComposer,
  });
  ColumnFilters<int> get id => $composableBuilder(
      column: $table.id, builder: (column) => ColumnFilters(column));

  ColumnFilters<String> get courseName => $composableBuilder(
      column: $table.courseName, builder: (column) => ColumnFilters(column));

  ColumnFilters<DateTime> get scheduledDate => $composableBuilder(
      column: $table.scheduledDate, builder: (column) => ColumnFilters(column));

  ColumnFilters<DateTime> get playedDate => $composableBuilder(
      column: $table.playedDate, builder: (column) => ColumnFilters(column));

  ColumnFilters<String> get status => $composableBuilder(
      column: $table.status, builder: (column) => ColumnFilters(column));

  ColumnFilters<int> get roundNumber => $composableBuilder(
      column: $table.roundNumber, builder: (column) => ColumnFilters(column));

  ColumnFilters<String> get weatherConditions => $composableBuilder(
      column: $table.weatherConditions,
      builder: (column) => ColumnFilters(column));

  ColumnFilters<DateTime> get cachedAt => $composableBuilder(
      column: $table.cachedAt, builder: (column) => ColumnFilters(column));
}

class $$CachedRoundsTableOrderingComposer
    extends Composer<_$AppDatabase, $CachedRoundsTable> {
  $$CachedRoundsTableOrderingComposer({
    required super.$db,
    required super.$table,
    super.joinBuilder,
    super.$addJoinBuilderToRootComposer,
    super.$removeJoinBuilderFromRootComposer,
  });
  ColumnOrderings<int> get id => $composableBuilder(
      column: $table.id, builder: (column) => ColumnOrderings(column));

  ColumnOrderings<String> get courseName => $composableBuilder(
      column: $table.courseName, builder: (column) => ColumnOrderings(column));

  ColumnOrderings<DateTime> get scheduledDate => $composableBuilder(
      column: $table.scheduledDate,
      builder: (column) => ColumnOrderings(column));

  ColumnOrderings<DateTime> get playedDate => $composableBuilder(
      column: $table.playedDate, builder: (column) => ColumnOrderings(column));

  ColumnOrderings<String> get status => $composableBuilder(
      column: $table.status, builder: (column) => ColumnOrderings(column));

  ColumnOrderings<int> get roundNumber => $composableBuilder(
      column: $table.roundNumber, builder: (column) => ColumnOrderings(column));

  ColumnOrderings<String> get weatherConditions => $composableBuilder(
      column: $table.weatherConditions,
      builder: (column) => ColumnOrderings(column));

  ColumnOrderings<DateTime> get cachedAt => $composableBuilder(
      column: $table.cachedAt, builder: (column) => ColumnOrderings(column));
}

class $$CachedRoundsTableAnnotationComposer
    extends Composer<_$AppDatabase, $CachedRoundsTable> {
  $$CachedRoundsTableAnnotationComposer({
    required super.$db,
    required super.$table,
    super.joinBuilder,
    super.$addJoinBuilderToRootComposer,
    super.$removeJoinBuilderFromRootComposer,
  });
  GeneratedColumn<int> get id =>
      $composableBuilder(column: $table.id, builder: (column) => column);

  GeneratedColumn<String> get courseName => $composableBuilder(
      column: $table.courseName, builder: (column) => column);

  GeneratedColumn<DateTime> get scheduledDate => $composableBuilder(
      column: $table.scheduledDate, builder: (column) => column);

  GeneratedColumn<DateTime> get playedDate => $composableBuilder(
      column: $table.playedDate, builder: (column) => column);

  GeneratedColumn<String> get status =>
      $composableBuilder(column: $table.status, builder: (column) => column);

  GeneratedColumn<int> get roundNumber => $composableBuilder(
      column: $table.roundNumber, builder: (column) => column);

  GeneratedColumn<String> get weatherConditions => $composableBuilder(
      column: $table.weatherConditions, builder: (column) => column);

  GeneratedColumn<DateTime> get cachedAt =>
      $composableBuilder(column: $table.cachedAt, builder: (column) => column);
}

class $$CachedRoundsTableTableManager extends RootTableManager<
    _$AppDatabase,
    $CachedRoundsTable,
    CachedRound,
    $$CachedRoundsTableFilterComposer,
    $$CachedRoundsTableOrderingComposer,
    $$CachedRoundsTableAnnotationComposer,
    $$CachedRoundsTableCreateCompanionBuilder,
    $$CachedRoundsTableUpdateCompanionBuilder,
    (
      CachedRound,
      BaseReferences<_$AppDatabase, $CachedRoundsTable, CachedRound>
    ),
    CachedRound,
    PrefetchHooks Function()> {
  $$CachedRoundsTableTableManager(_$AppDatabase db, $CachedRoundsTable table)
      : super(TableManagerState(
          db: db,
          table: table,
          createFilteringComposer: () =>
              $$CachedRoundsTableFilterComposer($db: db, $table: table),
          createOrderingComposer: () =>
              $$CachedRoundsTableOrderingComposer($db: db, $table: table),
          createComputedFieldComposer: () =>
              $$CachedRoundsTableAnnotationComposer($db: db, $table: table),
          updateCompanionCallback: ({
            Value<int> id = const Value.absent(),
            Value<String> courseName = const Value.absent(),
            Value<DateTime> scheduledDate = const Value.absent(),
            Value<DateTime?> playedDate = const Value.absent(),
            Value<String> status = const Value.absent(),
            Value<int> roundNumber = const Value.absent(),
            Value<String?> weatherConditions = const Value.absent(),
            Value<DateTime> cachedAt = const Value.absent(),
          }) =>
              CachedRoundsCompanion(
            id: id,
            courseName: courseName,
            scheduledDate: scheduledDate,
            playedDate: playedDate,
            status: status,
            roundNumber: roundNumber,
            weatherConditions: weatherConditions,
            cachedAt: cachedAt,
          ),
          createCompanionCallback: ({
            Value<int> id = const Value.absent(),
            required String courseName,
            required DateTime scheduledDate,
            Value<DateTime?> playedDate = const Value.absent(),
            required String status,
            required int roundNumber,
            Value<String?> weatherConditions = const Value.absent(),
            required DateTime cachedAt,
          }) =>
              CachedRoundsCompanion.insert(
            id: id,
            courseName: courseName,
            scheduledDate: scheduledDate,
            playedDate: playedDate,
            status: status,
            roundNumber: roundNumber,
            weatherConditions: weatherConditions,
            cachedAt: cachedAt,
          ),
          withReferenceMapper: (p0) => p0
              .map((e) => (e.readTable(table), BaseReferences(db, table, e)))
              .toList(),
          prefetchHooksCallback: null,
        ));
}

typedef $$CachedRoundsTableProcessedTableManager = ProcessedTableManager<
    _$AppDatabase,
    $CachedRoundsTable,
    CachedRound,
    $$CachedRoundsTableFilterComposer,
    $$CachedRoundsTableOrderingComposer,
    $$CachedRoundsTableAnnotationComposer,
    $$CachedRoundsTableCreateCompanionBuilder,
    $$CachedRoundsTableUpdateCompanionBuilder,
    (
      CachedRound,
      BaseReferences<_$AppDatabase, $CachedRoundsTable, CachedRound>
    ),
    CachedRound,
    PrefetchHooks Function()>;
typedef $$CachedHoleScoresTableCreateCompanionBuilder
    = CachedHoleScoresCompanion Function({
  Value<int> id,
  required int roundId,
  required int playerId,
  required int holeNumber,
  required int par,
  required int strokeIndex,
  Value<int?> grossStrokes,
  required int handicapStrokes,
  Value<int?> netStrokes,
  Value<int?> stablefordPoints,
  required bool isMaxScore,
});
typedef $$CachedHoleScoresTableUpdateCompanionBuilder
    = CachedHoleScoresCompanion Function({
  Value<int> id,
  Value<int> roundId,
  Value<int> playerId,
  Value<int> holeNumber,
  Value<int> par,
  Value<int> strokeIndex,
  Value<int?> grossStrokes,
  Value<int> handicapStrokes,
  Value<int?> netStrokes,
  Value<int?> stablefordPoints,
  Value<bool> isMaxScore,
});

class $$CachedHoleScoresTableFilterComposer
    extends Composer<_$AppDatabase, $CachedHoleScoresTable> {
  $$CachedHoleScoresTableFilterComposer({
    required super.$db,
    required super.$table,
    super.joinBuilder,
    super.$addJoinBuilderToRootComposer,
    super.$removeJoinBuilderFromRootComposer,
  });
  ColumnFilters<int> get id => $composableBuilder(
      column: $table.id, builder: (column) => ColumnFilters(column));

  ColumnFilters<int> get roundId => $composableBuilder(
      column: $table.roundId, builder: (column) => ColumnFilters(column));

  ColumnFilters<int> get playerId => $composableBuilder(
      column: $table.playerId, builder: (column) => ColumnFilters(column));

  ColumnFilters<int> get holeNumber => $composableBuilder(
      column: $table.holeNumber, builder: (column) => ColumnFilters(column));

  ColumnFilters<int> get par => $composableBuilder(
      column: $table.par, builder: (column) => ColumnFilters(column));

  ColumnFilters<int> get strokeIndex => $composableBuilder(
      column: $table.strokeIndex, builder: (column) => ColumnFilters(column));

  ColumnFilters<int> get grossStrokes => $composableBuilder(
      column: $table.grossStrokes, builder: (column) => ColumnFilters(column));

  ColumnFilters<int> get handicapStrokes => $composableBuilder(
      column: $table.handicapStrokes,
      builder: (column) => ColumnFilters(column));

  ColumnFilters<int> get netStrokes => $composableBuilder(
      column: $table.netStrokes, builder: (column) => ColumnFilters(column));

  ColumnFilters<int> get stablefordPoints => $composableBuilder(
      column: $table.stablefordPoints,
      builder: (column) => ColumnFilters(column));

  ColumnFilters<bool> get isMaxScore => $composableBuilder(
      column: $table.isMaxScore, builder: (column) => ColumnFilters(column));
}

class $$CachedHoleScoresTableOrderingComposer
    extends Composer<_$AppDatabase, $CachedHoleScoresTable> {
  $$CachedHoleScoresTableOrderingComposer({
    required super.$db,
    required super.$table,
    super.joinBuilder,
    super.$addJoinBuilderToRootComposer,
    super.$removeJoinBuilderFromRootComposer,
  });
  ColumnOrderings<int> get id => $composableBuilder(
      column: $table.id, builder: (column) => ColumnOrderings(column));

  ColumnOrderings<int> get roundId => $composableBuilder(
      column: $table.roundId, builder: (column) => ColumnOrderings(column));

  ColumnOrderings<int> get playerId => $composableBuilder(
      column: $table.playerId, builder: (column) => ColumnOrderings(column));

  ColumnOrderings<int> get holeNumber => $composableBuilder(
      column: $table.holeNumber, builder: (column) => ColumnOrderings(column));

  ColumnOrderings<int> get par => $composableBuilder(
      column: $table.par, builder: (column) => ColumnOrderings(column));

  ColumnOrderings<int> get strokeIndex => $composableBuilder(
      column: $table.strokeIndex, builder: (column) => ColumnOrderings(column));

  ColumnOrderings<int> get grossStrokes => $composableBuilder(
      column: $table.grossStrokes,
      builder: (column) => ColumnOrderings(column));

  ColumnOrderings<int> get handicapStrokes => $composableBuilder(
      column: $table.handicapStrokes,
      builder: (column) => ColumnOrderings(column));

  ColumnOrderings<int> get netStrokes => $composableBuilder(
      column: $table.netStrokes, builder: (column) => ColumnOrderings(column));

  ColumnOrderings<int> get stablefordPoints => $composableBuilder(
      column: $table.stablefordPoints,
      builder: (column) => ColumnOrderings(column));

  ColumnOrderings<bool> get isMaxScore => $composableBuilder(
      column: $table.isMaxScore, builder: (column) => ColumnOrderings(column));
}

class $$CachedHoleScoresTableAnnotationComposer
    extends Composer<_$AppDatabase, $CachedHoleScoresTable> {
  $$CachedHoleScoresTableAnnotationComposer({
    required super.$db,
    required super.$table,
    super.joinBuilder,
    super.$addJoinBuilderToRootComposer,
    super.$removeJoinBuilderFromRootComposer,
  });
  GeneratedColumn<int> get id =>
      $composableBuilder(column: $table.id, builder: (column) => column);

  GeneratedColumn<int> get roundId =>
      $composableBuilder(column: $table.roundId, builder: (column) => column);

  GeneratedColumn<int> get playerId =>
      $composableBuilder(column: $table.playerId, builder: (column) => column);

  GeneratedColumn<int> get holeNumber => $composableBuilder(
      column: $table.holeNumber, builder: (column) => column);

  GeneratedColumn<int> get par =>
      $composableBuilder(column: $table.par, builder: (column) => column);

  GeneratedColumn<int> get strokeIndex => $composableBuilder(
      column: $table.strokeIndex, builder: (column) => column);

  GeneratedColumn<int> get grossStrokes => $composableBuilder(
      column: $table.grossStrokes, builder: (column) => column);

  GeneratedColumn<int> get handicapStrokes => $composableBuilder(
      column: $table.handicapStrokes, builder: (column) => column);

  GeneratedColumn<int> get netStrokes => $composableBuilder(
      column: $table.netStrokes, builder: (column) => column);

  GeneratedColumn<int> get stablefordPoints => $composableBuilder(
      column: $table.stablefordPoints, builder: (column) => column);

  GeneratedColumn<bool> get isMaxScore => $composableBuilder(
      column: $table.isMaxScore, builder: (column) => column);
}

class $$CachedHoleScoresTableTableManager extends RootTableManager<
    _$AppDatabase,
    $CachedHoleScoresTable,
    CachedHoleScore,
    $$CachedHoleScoresTableFilterComposer,
    $$CachedHoleScoresTableOrderingComposer,
    $$CachedHoleScoresTableAnnotationComposer,
    $$CachedHoleScoresTableCreateCompanionBuilder,
    $$CachedHoleScoresTableUpdateCompanionBuilder,
    (
      CachedHoleScore,
      BaseReferences<_$AppDatabase, $CachedHoleScoresTable, CachedHoleScore>
    ),
    CachedHoleScore,
    PrefetchHooks Function()> {
  $$CachedHoleScoresTableTableManager(
      _$AppDatabase db, $CachedHoleScoresTable table)
      : super(TableManagerState(
          db: db,
          table: table,
          createFilteringComposer: () =>
              $$CachedHoleScoresTableFilterComposer($db: db, $table: table),
          createOrderingComposer: () =>
              $$CachedHoleScoresTableOrderingComposer($db: db, $table: table),
          createComputedFieldComposer: () =>
              $$CachedHoleScoresTableAnnotationComposer($db: db, $table: table),
          updateCompanionCallback: ({
            Value<int> id = const Value.absent(),
            Value<int> roundId = const Value.absent(),
            Value<int> playerId = const Value.absent(),
            Value<int> holeNumber = const Value.absent(),
            Value<int> par = const Value.absent(),
            Value<int> strokeIndex = const Value.absent(),
            Value<int?> grossStrokes = const Value.absent(),
            Value<int> handicapStrokes = const Value.absent(),
            Value<int?> netStrokes = const Value.absent(),
            Value<int?> stablefordPoints = const Value.absent(),
            Value<bool> isMaxScore = const Value.absent(),
          }) =>
              CachedHoleScoresCompanion(
            id: id,
            roundId: roundId,
            playerId: playerId,
            holeNumber: holeNumber,
            par: par,
            strokeIndex: strokeIndex,
            grossStrokes: grossStrokes,
            handicapStrokes: handicapStrokes,
            netStrokes: netStrokes,
            stablefordPoints: stablefordPoints,
            isMaxScore: isMaxScore,
          ),
          createCompanionCallback: ({
            Value<int> id = const Value.absent(),
            required int roundId,
            required int playerId,
            required int holeNumber,
            required int par,
            required int strokeIndex,
            Value<int?> grossStrokes = const Value.absent(),
            required int handicapStrokes,
            Value<int?> netStrokes = const Value.absent(),
            Value<int?> stablefordPoints = const Value.absent(),
            required bool isMaxScore,
          }) =>
              CachedHoleScoresCompanion.insert(
            id: id,
            roundId: roundId,
            playerId: playerId,
            holeNumber: holeNumber,
            par: par,
            strokeIndex: strokeIndex,
            grossStrokes: grossStrokes,
            handicapStrokes: handicapStrokes,
            netStrokes: netStrokes,
            stablefordPoints: stablefordPoints,
            isMaxScore: isMaxScore,
          ),
          withReferenceMapper: (p0) => p0
              .map((e) => (e.readTable(table), BaseReferences(db, table, e)))
              .toList(),
          prefetchHooksCallback: null,
        ));
}

typedef $$CachedHoleScoresTableProcessedTableManager = ProcessedTableManager<
    _$AppDatabase,
    $CachedHoleScoresTable,
    CachedHoleScore,
    $$CachedHoleScoresTableFilterComposer,
    $$CachedHoleScoresTableOrderingComposer,
    $$CachedHoleScoresTableAnnotationComposer,
    $$CachedHoleScoresTableCreateCompanionBuilder,
    $$CachedHoleScoresTableUpdateCompanionBuilder,
    (
      CachedHoleScore,
      BaseReferences<_$AppDatabase, $CachedHoleScoresTable, CachedHoleScore>
    ),
    CachedHoleScore,
    PrefetchHooks Function()>;
typedef $$PendingSyncScoresTableCreateCompanionBuilder
    = PendingSyncScoresCompanion Function({
  Value<int> id,
  required int roundId,
  required int playerId,
  required int holeNumber,
  required int grossStrokes,
  Value<bool> pendingSync,
  required DateTime createdAt,
});
typedef $$PendingSyncScoresTableUpdateCompanionBuilder
    = PendingSyncScoresCompanion Function({
  Value<int> id,
  Value<int> roundId,
  Value<int> playerId,
  Value<int> holeNumber,
  Value<int> grossStrokes,
  Value<bool> pendingSync,
  Value<DateTime> createdAt,
});

class $$PendingSyncScoresTableFilterComposer
    extends Composer<_$AppDatabase, $PendingSyncScoresTable> {
  $$PendingSyncScoresTableFilterComposer({
    required super.$db,
    required super.$table,
    super.joinBuilder,
    super.$addJoinBuilderToRootComposer,
    super.$removeJoinBuilderFromRootComposer,
  });
  ColumnFilters<int> get id => $composableBuilder(
      column: $table.id, builder: (column) => ColumnFilters(column));

  ColumnFilters<int> get roundId => $composableBuilder(
      column: $table.roundId, builder: (column) => ColumnFilters(column));

  ColumnFilters<int> get playerId => $composableBuilder(
      column: $table.playerId, builder: (column) => ColumnFilters(column));

  ColumnFilters<int> get holeNumber => $composableBuilder(
      column: $table.holeNumber, builder: (column) => ColumnFilters(column));

  ColumnFilters<int> get grossStrokes => $composableBuilder(
      column: $table.grossStrokes, builder: (column) => ColumnFilters(column));

  ColumnFilters<bool> get pendingSync => $composableBuilder(
      column: $table.pendingSync, builder: (column) => ColumnFilters(column));

  ColumnFilters<DateTime> get createdAt => $composableBuilder(
      column: $table.createdAt, builder: (column) => ColumnFilters(column));
}

class $$PendingSyncScoresTableOrderingComposer
    extends Composer<_$AppDatabase, $PendingSyncScoresTable> {
  $$PendingSyncScoresTableOrderingComposer({
    required super.$db,
    required super.$table,
    super.joinBuilder,
    super.$addJoinBuilderToRootComposer,
    super.$removeJoinBuilderFromRootComposer,
  });
  ColumnOrderings<int> get id => $composableBuilder(
      column: $table.id, builder: (column) => ColumnOrderings(column));

  ColumnOrderings<int> get roundId => $composableBuilder(
      column: $table.roundId, builder: (column) => ColumnOrderings(column));

  ColumnOrderings<int> get playerId => $composableBuilder(
      column: $table.playerId, builder: (column) => ColumnOrderings(column));

  ColumnOrderings<int> get holeNumber => $composableBuilder(
      column: $table.holeNumber, builder: (column) => ColumnOrderings(column));

  ColumnOrderings<int> get grossStrokes => $composableBuilder(
      column: $table.grossStrokes,
      builder: (column) => ColumnOrderings(column));

  ColumnOrderings<bool> get pendingSync => $composableBuilder(
      column: $table.pendingSync, builder: (column) => ColumnOrderings(column));

  ColumnOrderings<DateTime> get createdAt => $composableBuilder(
      column: $table.createdAt, builder: (column) => ColumnOrderings(column));
}

class $$PendingSyncScoresTableAnnotationComposer
    extends Composer<_$AppDatabase, $PendingSyncScoresTable> {
  $$PendingSyncScoresTableAnnotationComposer({
    required super.$db,
    required super.$table,
    super.joinBuilder,
    super.$addJoinBuilderToRootComposer,
    super.$removeJoinBuilderFromRootComposer,
  });
  GeneratedColumn<int> get id =>
      $composableBuilder(column: $table.id, builder: (column) => column);

  GeneratedColumn<int> get roundId =>
      $composableBuilder(column: $table.roundId, builder: (column) => column);

  GeneratedColumn<int> get playerId =>
      $composableBuilder(column: $table.playerId, builder: (column) => column);

  GeneratedColumn<int> get holeNumber => $composableBuilder(
      column: $table.holeNumber, builder: (column) => column);

  GeneratedColumn<int> get grossStrokes => $composableBuilder(
      column: $table.grossStrokes, builder: (column) => column);

  GeneratedColumn<bool> get pendingSync => $composableBuilder(
      column: $table.pendingSync, builder: (column) => column);

  GeneratedColumn<DateTime> get createdAt =>
      $composableBuilder(column: $table.createdAt, builder: (column) => column);
}

class $$PendingSyncScoresTableTableManager extends RootTableManager<
    _$AppDatabase,
    $PendingSyncScoresTable,
    PendingSyncScore,
    $$PendingSyncScoresTableFilterComposer,
    $$PendingSyncScoresTableOrderingComposer,
    $$PendingSyncScoresTableAnnotationComposer,
    $$PendingSyncScoresTableCreateCompanionBuilder,
    $$PendingSyncScoresTableUpdateCompanionBuilder,
    (
      PendingSyncScore,
      BaseReferences<_$AppDatabase, $PendingSyncScoresTable, PendingSyncScore>
    ),
    PendingSyncScore,
    PrefetchHooks Function()> {
  $$PendingSyncScoresTableTableManager(
      _$AppDatabase db, $PendingSyncScoresTable table)
      : super(TableManagerState(
          db: db,
          table: table,
          createFilteringComposer: () =>
              $$PendingSyncScoresTableFilterComposer($db: db, $table: table),
          createOrderingComposer: () =>
              $$PendingSyncScoresTableOrderingComposer($db: db, $table: table),
          createComputedFieldComposer: () =>
              $$PendingSyncScoresTableAnnotationComposer(
                  $db: db, $table: table),
          updateCompanionCallback: ({
            Value<int> id = const Value.absent(),
            Value<int> roundId = const Value.absent(),
            Value<int> playerId = const Value.absent(),
            Value<int> holeNumber = const Value.absent(),
            Value<int> grossStrokes = const Value.absent(),
            Value<bool> pendingSync = const Value.absent(),
            Value<DateTime> createdAt = const Value.absent(),
          }) =>
              PendingSyncScoresCompanion(
            id: id,
            roundId: roundId,
            playerId: playerId,
            holeNumber: holeNumber,
            grossStrokes: grossStrokes,
            pendingSync: pendingSync,
            createdAt: createdAt,
          ),
          createCompanionCallback: ({
            Value<int> id = const Value.absent(),
            required int roundId,
            required int playerId,
            required int holeNumber,
            required int grossStrokes,
            Value<bool> pendingSync = const Value.absent(),
            required DateTime createdAt,
          }) =>
              PendingSyncScoresCompanion.insert(
            id: id,
            roundId: roundId,
            playerId: playerId,
            holeNumber: holeNumber,
            grossStrokes: grossStrokes,
            pendingSync: pendingSync,
            createdAt: createdAt,
          ),
          withReferenceMapper: (p0) => p0
              .map((e) => (e.readTable(table), BaseReferences(db, table, e)))
              .toList(),
          prefetchHooksCallback: null,
        ));
}

typedef $$PendingSyncScoresTableProcessedTableManager = ProcessedTableManager<
    _$AppDatabase,
    $PendingSyncScoresTable,
    PendingSyncScore,
    $$PendingSyncScoresTableFilterComposer,
    $$PendingSyncScoresTableOrderingComposer,
    $$PendingSyncScoresTableAnnotationComposer,
    $$PendingSyncScoresTableCreateCompanionBuilder,
    $$PendingSyncScoresTableUpdateCompanionBuilder,
    (
      PendingSyncScore,
      BaseReferences<_$AppDatabase, $PendingSyncScoresTable, PendingSyncScore>
    ),
    PendingSyncScore,
    PrefetchHooks Function()>;

class $AppDatabaseManager {
  final _$AppDatabase _db;
  $AppDatabaseManager(this._db);
  $$CachedFlightsTableTableManager get cachedFlights =>
      $$CachedFlightsTableTableManager(_db, _db.cachedFlights);
  $$CachedLeaderboardEntriesTableTableManager get cachedLeaderboardEntries =>
      $$CachedLeaderboardEntriesTableTableManager(
          _db, _db.cachedLeaderboardEntries);
  $$CachedRoundsTableTableManager get cachedRounds =>
      $$CachedRoundsTableTableManager(_db, _db.cachedRounds);
  $$CachedHoleScoresTableTableManager get cachedHoleScores =>
      $$CachedHoleScoresTableTableManager(_db, _db.cachedHoleScores);
  $$PendingSyncScoresTableTableManager get pendingSyncScores =>
      $$PendingSyncScoresTableTableManager(_db, _db.pendingSyncScores);
}
