import 'package:riverpod_annotation/riverpod_annotation.dart';

import '../../../core/auth/token_service.dart';
import '../../../core/network/dio_client.dart';
import '../data/player_repository_impl.dart';
import '../domain/models.dart';
import '../domain/player_repository.dart';

part 'providers.g.dart';

@riverpod
PlayerRepository playerRepository(PlayerRepositoryRef ref) {
  return PlayerRepositoryImpl(dio: ref.watch(dioClientProvider));
}

/// Fetches the profile for [playerId]. If null, falls back to the
/// authenticated player's own ID from the JWT.
@riverpod
Future<PlayerProfile> playerProfile(
  PlayerProfileRef ref,
  int? playerId,
) {
  final repo = ref.watch(playerRepositoryProvider);
  final id = playerId ?? ref.watch(tokenServiceProvider).getPlayerId();
  return repo.getPlayer(id ?? 0);
}

@riverpod
Future<List<HandicapHistoryEntry>> handicapHistory(
  HandicapHistoryRef ref,
  int playerId,
) async {
  final repo = ref.watch(playerRepositoryProvider);
  return repo.getHandicapHistory(playerId);
}
