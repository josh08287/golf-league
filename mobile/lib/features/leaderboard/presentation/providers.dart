import 'package:connectivity_plus/connectivity_plus.dart';
import 'package:dio/dio.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:riverpod_annotation/riverpod_annotation.dart';

import '../../../core/database/app_database.dart';
import '../../../core/network/dio_client.dart';
import '../data/leaderboard_repository_impl.dart';
import '../domain/leaderboard_repository.dart';
import '../domain/models.dart';

part 'providers.g.dart';

@riverpod
LeaderboardRepository leaderboardRepository(LeaderboardRepositoryRef ref) {
  return LeaderboardRepositoryImpl(
    dio: ref.watch(dioClientProvider),
    db: ref.watch(appDatabaseProvider),
  );
}

@riverpod
Future<List<Flight>> flights(FlightsRef ref) async {
  final repo = ref.watch(leaderboardRepositoryProvider);
  return repo.getFlights();
}

@riverpod
Future<List<LeaderboardEntry>> standings(
  StandingsRef ref,
  int flightId,
) async {
  final repo = ref.watch(leaderboardRepositoryProvider);
  return repo.getStandings(flightId);
}

@riverpod
Stream<List<ConnectivityResult>> connectivity(ConnectivityRef ref) {
  return Connectivity().onConnectivityChanged;
}

@riverpod
AppDatabase appDatabase(AppDatabaseRef ref) {
  final db = AppDatabase();
  ref.onDispose(db.close);
  return db;
}
