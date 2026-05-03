import 'package:connectivity_plus/connectivity_plus.dart';
import 'package:dio/dio.dart';
import 'package:drift/drift.dart' show Value;

import '../../../core/api/golf_api_helpers.dart';
import '../../../core/database/app_database.dart';
import '../domain/leaderboard_repository.dart';
import '../domain/models.dart';

class LeaderboardRepositoryImpl implements LeaderboardRepository {
  LeaderboardRepositoryImpl({
    required this.dio,
    required this.db,
  });

  final Dio dio;
  final AppDatabase db;

  @override
  Future<List<Flight>> getFlights() async {
    final isOnline = await _checkConnectivity();

    if (isOnline) {
      try {
        final response = await dio.get<Map<String, dynamic>>('/flights');
        final rawList = extractDataList(response.data);
        final flights = rawList.map(Flight.fromJson).toList();

        // Cache the result.
        await db.upsertFlights(
          flights
              .map(
                (f) => CachedFlightsCompanion(
                  id: Value(f.id),
                  name: Value(f.name),
                  description: Value(f.description),
                  cachedAt: Value(DateTime.now()),
                ),
              )
              .toList(),
        );

        return flights;
      } catch (_) {
        // Fall through to cache.
      }
    }

    // Return stale cache.
    final cached = await db.getAllFlights();
    return cached
        .map(
          (c) => Flight(
            id: c.id,
            name: c.name,
            description: c.description,
          ),
        )
        .toList();
  }

  @override
  Future<List<LeaderboardEntry>> getStandings(int flightId) async {
    final isOnline = await _checkConnectivity();

    if (isOnline) {
      try {
        final seasonId = await fetchActiveSeasonId(dio);
        if (seasonId == null) {
          return _leaderboardFromCache(db, flightId);
        }
        final response = await dio.get<Map<String, dynamic>>(
          '/flights/$flightId/standings',
          queryParameters: {'seasonId': seasonId},
        );
        final rawList = extractDataList(response.data);
        final entries = rawList.map(_standingToEntry).toList();

        await db.replaceLeaderboardForFlight(
          flightId,
          entries
              .map(
                (e) => CachedLeaderboardEntriesCompanion(
                  flightId: Value(flightId),
                  playerId: Value(e.playerId),
                  playerName: Value(e.playerName),
                  totalStablefordPoints: Value(e.totalStablefordPoints),
                  roundsPlayed: Value(e.roundsPlayed),
                  currentRank: Value(e.currentRank),
                  previousRank: Value(e.previousRank),
                  currentHandicap: Value(e.currentHandicap),
                  cachedAt: Value(DateTime.now()),
                ),
              )
              .toList(),
        );

        return entries;
      } catch (_) {
        // Fall through to cache.
      }
    }

    return _leaderboardFromCache(db, flightId);
  }

  Future<List<LeaderboardEntry>> _leaderboardFromCache(
    AppDatabase db,
    int flightId,
  ) async {
    final cached = await db.getLeaderboardForFlight(flightId);
    return cached
        .map(
          (c) => LeaderboardEntry(
            playerId: c.playerId,
            playerName: c.playerName,
            totalStablefordPoints: c.totalStablefordPoints,
            roundsPlayed: c.roundsPlayed,
            currentRank: c.currentRank,
            previousRank: c.previousRank,
            currentHandicap: c.currentHandicap,
          ),
        )
        .toList();
  }

  // ---------------------------------------------------------------------------
  // Helpers
  // ---------------------------------------------------------------------------

  Future<bool> _checkConnectivity() async {
    final results = await Connectivity().checkConnectivity();
    return results.any((r) => r != ConnectivityResult.none);
  }
}

LeaderboardEntry _standingToEntry(Map<String, dynamic> e) {
  return LeaderboardEntry(
    playerId: (e['playerId'] as num).toInt(),
    playerName: e['playerFullName'] as String? ??
        e['playerName'] as String? ??
        '',
    totalStablefordPoints: (e['totalPoints'] as num?)?.toInt() ??
        (e['totalStablefordPoints'] as num?)?.toInt() ??
        0,
    roundsPlayed: (e['roundsPlayed'] as num?)?.toInt() ?? 0,
    currentRank: (e['position'] as num?)?.toInt() ??
        (e['currentRank'] as num?)?.toInt() ??
        0,
    previousRank: null,
    currentHandicap:
        (e['currentHandicapIndex'] as num?)?.toDouble() ??
            (e['currentHandicap'] as num?)?.toDouble() ??
            0,
    averagePoints: (e['averagePoints'] as num?)?.toDouble(),
    lastRoundPoints: null,
  );
}
