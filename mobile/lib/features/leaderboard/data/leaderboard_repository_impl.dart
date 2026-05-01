import 'package:connectivity_plus/connectivity_plus.dart';
import 'package:dio/dio.dart';
import 'package:drift/drift.dart' show Value;

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
        final response =
            await dio.get<Map<String, dynamic>>('/seasons/current/flights');
        final rawList = _extractList(response.data);
        final flights =
            rawList.map((e) => Flight.fromJson(e)).toList();

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
        final response = await dio
            .get<Map<String, dynamic>>('/flights/$flightId/standings');
        final rawList = _extractList(response.data);
        final entries =
            rawList.map((e) => LeaderboardEntry.fromJson(e)).toList();

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
    final result = await Connectivity().checkConnectivity();
    return result != ConnectivityResult.none;
  }

  List<Map<String, dynamic>> _extractList(
    Map<String, dynamic>? responseData,
  ) {
    if (responseData == null) return [];
    final data = responseData['data'];
    if (data is List) {
      return data.cast<Map<String, dynamic>>();
    }
    return [];
  }
}
