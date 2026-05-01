import 'dart:io';

import 'package:drift/drift.dart';
import 'package:drift/native.dart';
import 'package:path/path.dart' as p;
import 'package:path_provider/path_provider.dart';

part 'app_database.g.dart';

// ---------------------------------------------------------------------------
// Table definitions
// ---------------------------------------------------------------------------

class CachedFlights extends Table {
  IntColumn get id => integer()();
  TextColumn get name => text()();
  TextColumn get description => text().nullable()();
  DateTimeColumn get cachedAt => dateTime()();

  @override
  Set<Column> get primaryKey => {id};
}

class CachedLeaderboardEntries extends Table {
  IntColumn get id => integer().autoIncrement()();
  IntColumn get flightId => integer()();
  IntColumn get playerId => integer()();
  TextColumn get playerName => text()();
  IntColumn get totalStablefordPoints => integer()();
  IntColumn get roundsPlayed => integer()();
  IntColumn get currentRank => integer()();
  IntColumn get previousRank => integer().nullable()();
  RealColumn get currentHandicap => real()();
  DateTimeColumn get cachedAt => dateTime()();
}

class CachedRounds extends Table {
  IntColumn get id => integer()();
  TextColumn get courseName => text()();
  DateTimeColumn get scheduledDate => dateTime()();
  DateTimeColumn get playedDate => dateTime().nullable()();
  TextColumn get status => text()();
  IntColumn get roundNumber => integer()();
  TextColumn get weatherConditions => text().nullable()();
  DateTimeColumn get cachedAt => dateTime()();

  @override
  Set<Column> get primaryKey => {id};
}

class CachedHoleScores extends Table {
  IntColumn get id => integer().autoIncrement()();
  IntColumn get roundId => integer()();
  IntColumn get playerId => integer()();
  IntColumn get holeNumber => integer()();
  IntColumn get par => integer()();
  IntColumn get strokeIndex => integer()();
  IntColumn get grossStrokes => integer().nullable()();
  IntColumn get handicapStrokes => integer()();
  IntColumn get netStrokes => integer().nullable()();
  IntColumn get stablefordPoints => integer().nullable()();
  BoolColumn get isMaxScore => boolean()();
}

class PendingSyncScores extends Table {
  IntColumn get id => integer().autoIncrement()();
  IntColumn get roundId => integer()();
  IntColumn get playerId => integer()();
  IntColumn get holeNumber => integer()();
  IntColumn get grossStrokes => integer()();
  BoolColumn get pendingSync =>
      boolean().withDefault(const Constant(true))();
  DateTimeColumn get createdAt => dateTime()();
}

// ---------------------------------------------------------------------------
// Database class
// ---------------------------------------------------------------------------

@DriftDatabase(tables: [
  CachedFlights,
  CachedLeaderboardEntries,
  CachedRounds,
  CachedHoleScores,
  PendingSyncScores,
])
class AppDatabase extends _$AppDatabase {
  AppDatabase() : super(_openConnection());

  @override
  int get schemaVersion => 1;

  @override
  MigrationStrategy get migration => MigrationStrategy(
        onCreate: (m) => m.createAll(),
        onUpgrade: (m, from, to) async {},
      );

  // ---------------------------------------------------------------------------
  // Flights
  // ---------------------------------------------------------------------------

  Future<void> upsertFlights(List<CachedFlightsCompanion> flights) async {
    await batch((b) {
      b.insertAllOnConflictUpdate(cachedFlights, flights);
    });
  }

  Future<List<CachedFlight>> getAllFlights() => select(cachedFlights).get();

  // ---------------------------------------------------------------------------
  // Leaderboard entries
  // ---------------------------------------------------------------------------

  Future<void> replaceLeaderboardForFlight(
    int flightId,
    List<CachedLeaderboardEntriesCompanion> entries,
  ) async {
    await (delete(cachedLeaderboardEntries)
          ..where((t) => t.flightId.equals(flightId)))
        .go();
    await batch((b) {
      b.insertAll(cachedLeaderboardEntries, entries);
    });
  }

  Future<List<CachedLeaderboardEntry>> getLeaderboardForFlight(
    int flightId,
  ) =>
      (select(cachedLeaderboardEntries)
            ..where((t) => t.flightId.equals(flightId))
            ..orderBy([(t) => OrderingTerm.asc(t.currentRank)]))
          .get();

  // ---------------------------------------------------------------------------
  // Rounds
  // ---------------------------------------------------------------------------

  Future<void> upsertRounds(List<CachedRoundsCompanion> rounds) async {
    await batch((b) {
      b.insertAllOnConflictUpdate(cachedRounds, rounds);
    });
  }

  Future<List<CachedRound>> getAllRounds() =>
      (select(cachedRounds)
            ..orderBy([
              (t) => OrderingTerm.desc(t.scheduledDate),
            ]))
          .get();

  Future<CachedRound?> getRoundById(int id) =>
      (select(cachedRounds)..where((t) => t.id.equals(id)))
          .getSingleOrNull();

  // ---------------------------------------------------------------------------
  // Hole scores
  // ---------------------------------------------------------------------------

  Future<void> replaceHoleScores(
    int roundId,
    int playerId,
    List<CachedHoleScoresCompanion> scores,
  ) async {
    await (delete(cachedHoleScores)
          ..where(
            (t) =>
                t.roundId.equals(roundId) & t.playerId.equals(playerId),
          ))
        .go();
    await batch((b) {
      b.insertAll(cachedHoleScores, scores);
    });
  }

  Future<List<CachedHoleScore>> getHoleScores(
    int roundId,
    int playerId,
  ) =>
      (select(cachedHoleScores)
            ..where(
              (t) =>
                  t.roundId.equals(roundId) & t.playerId.equals(playerId),
            )
            ..orderBy([(t) => OrderingTerm.asc(t.holeNumber)]))
          .get();

  // ---------------------------------------------------------------------------
  // Pending sync scores
  // ---------------------------------------------------------------------------

  Future<void> insertPendingScore(PendingSyncScoresCompanion entry) =>
      into(pendingSyncScores).insert(entry);

  Future<List<PendingSyncScore>> getPendingScores() =>
      (select(pendingSyncScores)
            ..where((t) => t.pendingSync.equals(true)))
          .get();

  Future<List<PendingSyncScore>> getPendingScoresForRound(int roundId) =>
      (select(pendingSyncScores)
            ..where(
              (t) =>
                  t.roundId.equals(roundId) & t.pendingSync.equals(true),
            ))
          .get();

  Future<void> markScoresSynced(List<int> ids) async {
    await (update(pendingSyncScores)..where((t) => t.id.isIn(ids))).write(
      const PendingSyncScoresCompanion(pendingSync: Value(false)),
    );
  }
}

LazyDatabase _openConnection() {
  return LazyDatabase(() async {
    final dir = await getApplicationDocumentsDirectory();
    final file = File(p.join(dir.path, 'golf_league.db'));
    return NativeDatabase.createInBackground(file);
  });
}
