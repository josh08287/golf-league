import 'package:riverpod_annotation/riverpod_annotation.dart';

import '../../../core/database/app_database.dart';
import '../../../core/network/dio_client.dart';
import '../../leaderboard/presentation/providers.dart';
import '../data/rounds_repository_impl.dart';
import '../domain/models.dart';
import '../domain/rounds_repository.dart';

part 'providers.g.dart';

@riverpod
RoundsRepository roundsRepository(RoundsRepositoryRef ref) {
  return RoundsRepositoryImpl(
    dio: ref.watch(dioClientProvider),
    db: ref.watch(appDatabaseProvider),
  );
}

@riverpod
Future<List<Round>> rounds(RoundsRef ref) async {
  final repo = ref.watch(roundsRepositoryProvider);
  return repo.getRounds();
}

@riverpod
Future<RoundDetail> roundDetail(RoundDetailRef ref, int roundId) async {
  final repo = ref.watch(roundsRepositoryProvider);
  return repo.getRoundDetail(roundId);
}

/// Uses a named record as the parameter so both the record fields are clear at
/// the call site: `ref.watch(playerScorecardProvider((roundId: 1, playerId: 2)))`
@riverpod
Future<PlayerScorecard> playerScorecard(
  PlayerScorecardRef ref,
  ({int roundId, int playerId}) args,
) async {
  final repo = ref.watch(roundsRepositoryProvider);
  return repo.getPlayerScorecard(args.roundId, args.playerId);
}
