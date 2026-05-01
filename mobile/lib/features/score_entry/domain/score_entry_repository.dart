import 'models.dart';

abstract class ScoreEntryRepository {
  Future<List<ScoreEntryHole>> getHolesForRound(int roundId, int playerId);
  Future<void> submitScores(int roundId, ScoreSubmission submission);
}
