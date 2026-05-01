import 'package:dio/dio.dart';

import '../domain/models.dart';
import '../domain/score_entry_repository.dart';

class ScoreEntryRepositoryImpl implements ScoreEntryRepository {
  ScoreEntryRepositoryImpl({required this.dio});

  final Dio dio;

  @override
  Future<List<ScoreEntryHole>> getHolesForRound(
    int roundId,
    int playerId,
  ) async {
    final response = await dio.get<Map<String, dynamic>>(
      '/rounds/$roundId/scores/$playerId/holes',
    );
    final data = response.data;
    if (data == null) return [];
    final raw = data['data'];
    if (raw is List) {
      return raw
          .cast<Map<String, dynamic>>()
          .map((e) => ScoreEntryHole.fromJson(e))
          .toList();
    }
    return [];
  }

  @override
  Future<void> submitScores(
    int roundId,
    ScoreSubmission submission,
  ) async {
    await dio.post<void>(
      '/rounds/$roundId/scores',
      data: submission.toJson(),
    );
  }
}
