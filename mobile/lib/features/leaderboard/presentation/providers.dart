import 'package:connectivity_plus/connectivity_plus.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/database/app_database.dart';
import '../../../core/network/dio_client.dart';
import '../data/leaderboard_repository_impl.dart';
import '../domain/leaderboard_repository.dart';
import '../domain/models.dart';

final appDatabaseProvider = Provider<AppDatabase>((ref) {
  final db = AppDatabase();
  ref.onDispose(db.close);
  return db;
});

final leaderboardRepositoryProvider = Provider<LeaderboardRepository>((ref) {
  return LeaderboardRepositoryImpl(
    dio: ref.watch(dioClientProvider),
    db: ref.watch(appDatabaseProvider),
  );
});

final flightsProvider = FutureProvider<List<Flight>>((ref) async {
  final repo = ref.watch(leaderboardRepositoryProvider);
  return repo.getFlights();
});

final standingsProvider =
    FutureProvider.family<List<LeaderboardEntry>, int>((ref, flightId) async {
  final repo = ref.watch(leaderboardRepositoryProvider);
  return repo.getStandings(flightId);
});

final connectivityProvider = StreamProvider<List<ConnectivityResult>>((ref) {
  return Connectivity().onConnectivityChanged;
});
