import 'models.dart';

abstract class RoundsRepository {
  Future<List<Round>> getRounds({int page = 1, int pageSize = 20});
  Future<RoundDetail> getRoundDetail(int roundId);
  Future<PlayerScorecard> getPlayerScorecard(int roundId, int playerId);
}
