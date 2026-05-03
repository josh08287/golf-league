import 'dart:async';

import 'package:connectivity_plus/connectivity_plus.dart';
import 'package:flutter/foundation.dart';

import '../../../core/database/app_database.dart';
import '../domain/models.dart';
import '../domain/score_entry_repository.dart';

/// Watches network connectivity and automatically posts queued hole scores
/// to the backend when the device comes online.
///
/// Uses exponential backoff: 2s, 4s, 8s, 16s, 32s then gives up until
/// the next connectivity event fires.
class SyncService {
  SyncService({
    required this.db,
    required this.repository,
  }) {
    _subscription = Connectivity()
        .onConnectivityChanged
        .listen(_onConnectivityChanged);
  }

  final AppDatabase db;
  final ScoreEntryRepository repository;

  late final StreamSubscription<List<ConnectivityResult>> _subscription;

  static const List<Duration> _backoffSchedule = [
    Duration(seconds: 2),
    Duration(seconds: 4),
    Duration(seconds: 8),
    Duration(seconds: 16),
    Duration(seconds: 32),
  ];

  void _onConnectivityChanged(List<ConnectivityResult> results) {
    final isOnline = results.any((r) => r != ConnectivityResult.none);
    if (isOnline) {
      _syncPending();
    }
  }

  Future<void> _syncPending() async {
    final pending = await db.getPendingScores();
    if (pending.isEmpty) return;

    // Group by (roundId, playerId) to submit one batch per player/round.
    final grouped = <String, List<PendingSyncScore>>{};
    for (final score in pending) {
      final key = '${score.roundId}_${score.playerId}';
      grouped.putIfAbsent(key, () => []).add(score);
    }

    for (final entry in grouped.entries) {
      final scores = entry.value;
      final roundId = scores.first.roundId;
      final playerId = scores.first.playerId;
      await _submitWithBackoff(roundId, playerId, scores);
    }
  }

  Future<void> _submitWithBackoff(
    int roundId,
    int playerId,
    List<PendingSyncScore> scores,
  ) async {
    final submission = ScoreSubmission(
      playerId: playerId,
      holes: scores
          .map(
            (s) => HoleSubmission(
              holeNumber: s.holeNumber,
              grossStrokes: s.grossStrokes,
            ),
          )
          .toList(),
    );

    for (var attempt = 0;
        attempt < _backoffSchedule.length;
        attempt++) {
      try {
        await repository.submitScores(roundId, submission);
        await db.markScoresSynced(scores.map((s) => s.id).toList());
        return;
      } catch (e) {
        if (attempt == _backoffSchedule.length - 1) {
          debugPrint(
            'SyncService: exhausted retries for round $roundId / '
            'player $playerId: $e',
          );
          return;
        }
        await Future<void>.delayed(_backoffSchedule[attempt]);

        // Abort retry if device went offline.
        final results = await Connectivity().checkConnectivity();
        if (results.every((r) => r == ConnectivityResult.none)) {
          debugPrint('SyncService: went offline during retry; aborting.');
          return;
        }
      }
    }
  }

  /// Trigger a manual sync attempt (e.g., from the confirmation screen).
  Future<void> syncNow() => _syncPending();

  void dispose() {
    _subscription.cancel();
  }
}
