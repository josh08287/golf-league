import 'models.dart';

abstract class PlayerRepository {
  Future<PlayerProfile> getPlayer(int playerId);
  Future<List<HandicapHistoryEntry>> getHandicapHistory(int playerId);
}
