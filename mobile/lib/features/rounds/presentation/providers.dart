import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/network/dio_client.dart';
import '../../leaderboard/presentation/providers.dart';
import '../data/rounds_repository_impl.dart';
import '../domain/models.dart';
import '../domain/rounds_repository.dart';

final roundsRepositoryProvider = Provider<RoundsRepository>((ref) {
  return RoundsRepositoryImpl(
    dio: ref.watch(dioClientProvider),
    db: ref.watch(appDatabaseProvider),
  );
});

final roundsListProvider = FutureProvider<List<Round>>((ref) async {
  final repo = ref.watch(roundsRepositoryProvider);
  return repo.getRounds();
});

final roundDetailProvider =
    FutureProvider.family<RoundDetail, int>((ref, roundId) async {
  final repo = ref.watch(roundsRepositoryProvider);
  return repo.getRoundDetail(roundId);
});

/// `ref.watch(playerScorecardProvider((roundId: 1, playerId: 2)))`
final playerScorecardProvider = FutureProvider.family<
    PlayerScorecard,
    ({int roundId, int playerId})>((ref, args) async {
  final repo = ref.watch(roundsRepositoryProvider);
  return repo.getPlayerScorecard(args.roundId, args.playerId);
});
