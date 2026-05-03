import 'package:connectivity_plus/connectivity_plus.dart';
import 'package:dio/dio.dart';

import '../../../core/api/golf_api_helpers.dart';
import '../domain/dashboard_repository.dart';
import '../domain/models.dart';
import '../../leaderboard/domain/models.dart';

class DashboardRepositoryImpl implements DashboardRepository {
  DashboardRepositoryImpl({required this.dio});

  final Dio dio;

  @override
  Future<DashboardData> getDashboardData() async {
    final isOnline = await _checkConnectivity();
    if (!isOnline) {
      return const DashboardData();
    }

    // Fetch flights.
    late List<Flight> flights;
    try {
      final flightsResponse = await dio.get<Map<String, dynamic>>('/flights');
      final rawFlights = extractDataList(flightsResponse.data);
      flights = rawFlights.map(Flight.fromJson).toList();
    } catch (_) {
      flights = [];
    }

    // Fetch top-3 standings per flight in parallel.
    final flightSummaries = await Future.wait(
      flights.map((f) => _buildFlightSummary(f)),
    );

    // Fetch latest round.
    LatestRoundSummary? latestRound;
    try {
      final roundsResponse = await dio.get<Map<String, dynamic>>(
        '/rounds',
        queryParameters: {'page': 1, 'pageSize': 1},
      );
      final rawRounds = extractDataList(roundsResponse.data);
      if (rawRounds.isNotEmpty) {
        final r = rawRounds.first;
        final playedDateRaw =
            r['scheduledDate'] as String? ?? r['playedDate'] as String?;
        if (playedDateRaw != null) {
          latestRound = LatestRoundSummary(
            roundId: (r['id'] as num).toInt(),
            courseName: r['courseName'] as String? ?? '',
            playedDate: DateTime.parse(playedDateRaw),
            status: r['status'] as String? ?? '',
          );
        }
      }
    } catch (_) {
      // Not critical.
    }

    return DashboardData(
      flightSummaries: flightSummaries,
      latestRound: latestRound,
    );
  }

  Future<FlightSummary> _buildFlightSummary(Flight flight) async {
    try {
      final seasonId = await fetchActiveSeasonId(dio);
      if (seasonId == null) {
        return FlightSummary(
          flightId: flight.id,
          flightName: flight.name,
        );
      }
      final response = await dio.get<Map<String, dynamic>>(
        '/flights/${flight.id}/standings',
        queryParameters: {'seasonId': seasonId},
      );
      final rawList = extractDataList(response.data);

      final topThree = rawList
          .take(3)
          .toList()
          .asMap()
          .entries
          .map(
            (entry) => FlightTopEntry(
              rank: entry.key + 1,
              playerId:
                  (entry.value['playerId'] as num?)?.toInt() ?? 0,
              playerName: entry.value['playerFullName'] as String? ??
                  entry.value['playerName'] as String? ??
                  '',
              totalPoints:
                  (entry.value['totalPoints'] as num?)?.toInt() ??
                      (entry.value['totalStablefordPoints'] as num?)
                          ?.toInt() ??
                      0,
              handicap:
                  (entry.value['currentHandicapIndex'] as num?)
                          ?.toDouble() ??
                      (entry.value['currentHandicap'] as num?)
                          ?.toDouble() ??
                      0.0,
            ),
          )
          .toList();

      return FlightSummary(
        flightId: flight.id,
        flightName: flight.name,
        topThree: topThree,
      );
    } catch (_) {
      return FlightSummary(
        flightId: flight.id,
        flightName: flight.name,
      );
    }
  }

  // ---------------------------------------------------------------------------
  // Helpers
  // ---------------------------------------------------------------------------

  Future<bool> _checkConnectivity() async {
    final results = await Connectivity().checkConnectivity();
    return results.any((r) => r != ConnectivityResult.none);
  }

}
