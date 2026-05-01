import 'package:drift/drift.dart' show Value;
import 'package:riverpod_annotation/riverpod_annotation.dart';

import '../../../core/auth/token_service.dart';
import '../../../core/database/app_database.dart';
import '../../../core/network/dio_client.dart';
import '../../../core/utils/stableford_calculator.dart';
import '../../leaderboard/presentation/providers.dart';
import '../data/score_entry_repository_impl.dart';
import '../data/sync_service.dart';
import '../domain/models.dart';
import '../domain/score_entry_repository.dart';

part 'providers.g.dart';

@riverpod
ScoreEntryRepository scoreEntryRepository(ScoreEntryRepositoryRef ref) {
  return ScoreEntryRepositoryImpl(dio: ref.watch(dioClientProvider));
}

@riverpod
SyncService syncService(SyncServiceRef ref) {
  final service = SyncService(
    db: ref.watch(appDatabaseProvider),
    repository: ref.watch(scoreEntryRepositoryProvider),
  );
  ref.onDispose(service.dispose);
  return service;
}

/// Loads hole setup data for score entry (par, SI, strokes received).
@riverpod
Future<List<ScoreEntryHole>> scoreEntryHoles(
  ScoreEntryHolesRef ref,
  int roundId,
) async {
  final repo = ref.watch(scoreEntryRepositoryProvider);
  return repo.getHolesForRound(roundId);
}

// ---------------------------------------------------------------------------
// Per-hole entry state
// ---------------------------------------------------------------------------

class HoleEntryState {
  const HoleEntryState({
    required this.holeNumber,
    required this.par,
    required this.strokeIndex,
    required this.strokesReceived,
    required this.grossStrokes,
    required this.stablefordPoints,
  });

  final int holeNumber;
  final int par;
  final int strokeIndex;
  final int strokesReceived;
  final int grossStrokes;
  final int stablefordPoints;

  HoleEntryState copyWith({int? grossStrokes, int? stablefordPoints}) {
    return HoleEntryState(
      holeNumber: holeNumber,
      par: par,
      strokeIndex: strokeIndex,
      strokesReceived: strokesReceived,
      grossStrokes: grossStrokes ?? this.grossStrokes,
      stablefordPoints: stablefordPoints ?? this.stablefordPoints,
    );
  }
}

// ---------------------------------------------------------------------------
// Score entry notifier — keyed by roundId only (player resolved from JWT)
// ---------------------------------------------------------------------------

@riverpod
class ScoreEntryNotifier extends _$ScoreEntryNotifier {
  @override
  List<HoleEntryState> build(int roundId) => [];

  void initHoles(List<ScoreEntryHole> holes) {
    state = holes.map((h) => _recalculate(HoleEntryState(
          holeNumber: h.holeNumber,
          par: h.par,
          strokeIndex: h.strokeIndex,
          strokesReceived: h.strokesReceived,
          grossStrokes: h.grossStrokes ?? (h.par + 2 + h.strokesReceived),
          stablefordPoints: 0,
        ))).toList();
  }

  void setGross(int holeNumber, int gross) {
    state = [
      for (final h in state)
        if (h.holeNumber == holeNumber) _recalculate(h.copyWith(grossStrokes: gross)) else h,
    ];
  }

  /// Writes a single hole score to the local pending-sync queue.
  Future<void> saveHole({
    required int roundId,
    required int holeNumber,
    required int grossStrokes,
  }) async {
    final db = ref.read(appDatabaseProvider);
    final playerId =
        await ref.read(tokenServiceProvider).getPlayerId() ?? 0;
    await db.insertPendingScore(
      PendingSyncScoresCompanion(
        roundId: Value(roundId),
        playerId: Value(playerId),
        holeNumber: Value(holeNumber),
        grossStrokes: Value(grossStrokes),
        createdAt: Value(DateTime.now()),
      ),
    );
  }

  /// Flushes all pending scores to the API.
  Future<void> submitAll(int roundId) async {
    await ref.read(syncServiceProvider).syncNow();
  }

  HoleEntryState _recalculate(HoleEntryState h) {
    final net = StablefordCalculator.netStrokes(h.grossStrokes, h.strokesReceived);
    return h.copyWith(stablefordPoints: StablefordCalculator.points(net, h.par));
  }
}
