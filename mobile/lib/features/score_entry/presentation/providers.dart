import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/network/dio_client.dart';
import '../../leaderboard/presentation/providers.dart';
import '../data/score_entry_repository_impl.dart';
import '../data/sync_service.dart';
import '../domain/models.dart';
import '../domain/score_entry_repository.dart';

final scoreEntryRepositoryProvider = Provider<ScoreEntryRepository>((ref) {
  return ScoreEntryRepositoryImpl(dio: ref.watch(dioClientProvider));
});

final syncServiceProvider = Provider<SyncService>((ref) {
  final service = SyncService(
    db: ref.watch(appDatabaseProvider),
    repository: ref.watch(scoreEntryRepositoryProvider),
  );
  ref.onDispose(service.dispose);
  return service;
});

final scoreEntryHolesProvider =
    FutureProvider.family<List<ScoreEntryHole>, int>((ref, roundId) async {
  final repo = ref.watch(scoreEntryRepositoryProvider);
  final playerId = ref.read(tokenServiceProvider).getPlayerId() ?? 0;
  return repo.getHolesForRound(roundId, playerId);
});
