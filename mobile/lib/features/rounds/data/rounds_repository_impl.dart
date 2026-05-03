import 'package:connectivity_plus/connectivity_plus.dart';
import 'package:dio/dio.dart';
import 'package:drift/drift.dart' show Value;

import '../../../core/database/app_database.dart';
import '../domain/models.dart';
import '../domain/rounds_repository.dart';

class RoundsRepositoryImpl implements RoundsRepository {
  RoundsRepositoryImpl({
    required this.dio,
    required this.db,
  });

  final Dio dio;
  final AppDatabase db;

  @override
  Future<List<Round>> getRounds({int page = 1, int pageSize = 20}) async {
    final isOnline = await _checkConnectivity();

    if (isOnline) {
      try {
        final response = await dio.get<Map<String, dynamic>>(
          '/rounds',
          queryParameters: {'page': page, 'pageSize': pageSize},
        );
        final rawList = _extractList(response.data);
        final rounds = rawList.map((e) => Round.fromJson(e)).toList();

        await db.upsertRounds(
          rounds
              .map(
                (r) => CachedRoundsCompanion(
                  id: Value(r.id),
                  courseName: Value(r.courseName),
                  scheduledDate: Value(r.scheduledDate),
                  playedDate: Value(r.playedDate),
                  status: Value(r.status),
                  roundNumber: Value(r.roundNumber),
                  weatherConditions: Value(r.weatherConditions),
                  cachedAt: Value(DateTime.now()),
                ),
              )
              .toList(),
        );

        return rounds;
      } catch (_) {
        // Fall through to cache.
      }
    }

    final cached = await db.getAllRounds();
    return cached
        .map(
          (c) => Round(
            id: c.id,
            courseName: c.courseName,
            scheduledDate: c.scheduledDate,
            playedDate: c.playedDate,
            status: c.status,
            roundNumber: c.roundNumber,
            weatherConditions: c.weatherConditions,
          ),
        )
        .toList();
  }

  @override
  Future<RoundDetail> getRoundDetail(int roundId) async {
    final isOnline = await _checkConnectivity();

    if (isOnline) {
      try {
        final response =
            await dio.get<Map<String, dynamic>>('/rounds/$roundId');
        final data = _extractSingle(response.data);
        return RoundDetail.fromJson(data);
      } catch (_) {
        // Fall through to cache.
      }
    }

    final cached = await db.getRoundById(roundId);
    if (cached != null) {
      return RoundDetail(
        id: cached.id,
        courseName: cached.courseName,
        scheduledDate: cached.scheduledDate,
        playedDate: cached.playedDate,
        status: cached.status,
        roundNumber: cached.roundNumber,
        weatherConditions: cached.weatherConditions,
      );
    }

    throw Exception('Round $roundId not found in cache and device is offline.');
  }

  @override
  Future<PlayerScorecard> getPlayerScorecard(
    int roundId,
    int playerId,
  ) async {
    final isOnline = await _checkConnectivity();

    if (isOnline) {
      try {
        final response = await dio.get<Map<String, dynamic>>(
          '/rounds/$roundId/scores/$playerId/holes',
        );
        final data = _extractSingle(response.data);
        final scorecard = PlayerScorecard.fromJson(data);

        // Cache hole scores.
        await db.replaceHoleScores(
          roundId,
          playerId,
          scorecard.holes
              .map(
                (h) => CachedHoleScoresCompanion(
                  roundId: Value(roundId),
                  playerId: Value(playerId),
                  holeNumber: Value(h.holeNumber),
                  par: Value(h.par),
                  strokeIndex: Value(h.strokeIndex),
                  grossStrokes: Value(h.grossStrokes),
                  handicapStrokes: Value(h.handicapStrokes),
                  netStrokes: Value(h.netStrokes),
                  stablefordPoints: Value(h.stablefordPoints),
                  isMaxScore: Value(h.isMaxScore),
                ),
              )
              .toList(),
        );

        return scorecard;
      } catch (_) {
        // Fall through to cache.
      }
    }

    final cachedHoles = await db.getHoleScores(roundId, playerId);
    if (cachedHoles.isNotEmpty) {
      final holes = cachedHoles
          .map(
            (c) => HoleScore(
              holeNumber: c.holeNumber,
              par: c.par,
              strokeIndex: c.strokeIndex,
              grossStrokes: c.grossStrokes,
              handicapStrokes: c.handicapStrokes,
              netStrokes: c.netStrokes,
              stablefordPoints: c.stablefordPoints,
              isMaxScore: c.isMaxScore,
            ),
          )
          .toList();

      final totalGross = holes.fold<int>(
        0,
        (s, h) => s + (h.grossStrokes ?? 0),
      );
      final totalStableford = holes.fold<int>(
        0,
        (s, h) => s + (h.stablefordPoints ?? 0),
      );

      return PlayerScorecard(
        roundId: roundId,
        playerId: playerId,
        playerName: '',
        courseHandicap: 0,
        holes: holes,
        totalGross: totalGross,
        totalStableford: totalStableford,
      );
    }

    throw Exception(
      'Scorecard for round $roundId / player $playerId not in cache and device is offline.',
    );
  }

  // ---------------------------------------------------------------------------
  // Helpers
  // ---------------------------------------------------------------------------

  Future<bool> _checkConnectivity() async {
    final results = await Connectivity().checkConnectivity();
    return results.any((r) => r != ConnectivityResult.none);
  }

  List<Map<String, dynamic>> _extractList(
    Map<String, dynamic>? responseData,
  ) {
    if (responseData == null) return [];
    final data = responseData['data'];
    if (data is List) return data.cast<Map<String, dynamic>>();
    return [];
  }

  Map<String, dynamic> _extractSingle(
    Map<String, dynamic>? responseData,
  ) {
    if (responseData == null) return {};
    final data = responseData['data'];
    if (data is Map<String, dynamic>) return data;
    return {};
  }
}
