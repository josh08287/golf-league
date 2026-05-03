import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/network/dio_client.dart';
import '../data/player_repository_impl.dart';
import '../domain/models.dart';
import '../domain/player_repository.dart';

final playerRepositoryProvider = Provider<PlayerRepository>((ref) {
  return PlayerRepositoryImpl(dio: ref.watch(dioClientProvider));
});

/// Profile for a single player row id.
final playerProfileProvider =
    FutureProvider.family<PlayerProfile, int>((ref, playerId) async {
  final repo = ref.watch(playerRepositoryProvider);
  return repo.getPlayer(playerId);
});

final handicapHistoryProvider =
    FutureProvider.family<List<HandicapHistoryEntry>, int>(
        (ref, playerId) async {
  final repo = ref.watch(playerRepositoryProvider);
  return repo.getHandicapHistory(playerId);
});
