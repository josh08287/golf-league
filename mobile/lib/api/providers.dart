import 'package:dio/dio.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../auth/auth_providers.dart';
import '../models/models.dart';
import 'api_client.dart';

List<T> _extractList<T>(
  dynamic responseData,
  T Function(Map<String, dynamic>) fromJson,
) {
  if (responseData == null) return [];
  if (responseData is Map) {
    final data = responseData['data'];
    if (data is List) {
      return data.map((e) => fromJson(e as Map<String, dynamic>)).toList();
    }
  }
  if (responseData is List) {
    return responseData
        .map((e) => fromJson(e as Map<String, dynamic>))
        .toList();
  }
  return [];
}

T? _extractData<T>(
  dynamic responseData,
  T Function(Map<String, dynamic>) fromJson,
) {
  if (responseData == null) return null;
  if (responseData is Map && responseData.containsKey('data')) {
    final data = responseData['data'];
    if (data is Map<String, dynamic>) {
      return fromJson(data);
    }
  }
  if (responseData is Map<String, dynamic>) {
    return fromJson(responseData);
  }
  return null;
}

// ── Flights ──────────────────────────────────────────────────────────────────

final flightsProvider = FutureProvider<List<Flight>>((ref) async {
  final dio = ref.watch(apiClientProvider);
  final auth = ref.watch(authServiceProvider);
  final token = await auth.getAccessToken();
  final response = await dio.get<dynamic>(
    '/flights',
    options: Options(
      headers: token != null ? {'Authorization': 'Bearer $token'} : null,
    ),
  );
  return _extractList(response.data, Flight.fromJson);
});

final flightStandingsProvider =
    FutureProvider.family<List<Standing>, _FlightStandingsParams>((
      ref,
      params,
    ) async {
      final dio = ref.watch(apiClientProvider);
      final auth = ref.watch(authServiceProvider);
      final token = await auth.getAccessToken();
      final response = await dio.get<dynamic>(
        '/flights/${params.flightId}/standings',
        queryParameters: {
          'halfId': params.halfId,
          'useGrossPoints': params.useGrossPoints.toString(),
        },
        options: Options(
          headers: token != null ? {'Authorization': 'Bearer $token'} : null,
        ),
      );
      final data = response.data;
      if (data is List) {
        return data
            .map((e) => Standing.fromJson(e as Map<String, dynamic>))
            .toList();
      }
      return _extractList(data, Standing.fromJson);
    });

class FlightStandingsParams {
  const FlightStandingsParams({
    required this.flightId,
    required this.halfId,
    this.useGrossPoints = false,
  });
  final String flightId;
  final String halfId;
  final bool useGrossPoints;
}

// For backward compatibility
typedef _FlightStandingsParams = FlightStandingsParams;

// ── Rounds ────────────────────────────────────────────────────────────────────

final roundsProvider = FutureProvider<List<Round>>((ref) async {
  final dio = ref.watch(apiClientProvider);
  final auth = ref.watch(authServiceProvider);
  final token = await auth.getAccessToken();
  final response = await dio.get<dynamic>(
    '/rounds',
    queryParameters: {'page': 1, 'pageSize': 20},
    options: Options(
      headers: token != null ? {'Authorization': 'Bearer $token'} : null,
    ),
  );
  final rounds = _extractList(response.data, Round.fromJson);
  rounds.sort((a, b) => b.scheduledDate.compareTo(a.scheduledDate));
  return rounds;
});

final roundDetailProvider = FutureProvider.family<Round, int>((
  ref,
  roundId,
) async {
  final dio = ref.watch(apiClientProvider);
  final auth = ref.watch(authServiceProvider);
  final token = await auth.getAccessToken();
  final response = await dio.get<dynamic>(
    '/rounds/$roundId',
    options: Options(
      headers: token != null ? {'Authorization': 'Bearer $token'} : null,
    ),
  );
  final data = response.data;
  if (data is Map && data.containsKey('data')) {
    return Round.fromJson(data['data'] as Map<String, dynamic>);
  }
  return Round.fromJson(data as Map<String, dynamic>);
});

final scorecardsProvider = FutureProvider.family<List<Scorecard>, int>((
  ref,
  roundId,
) async {
  final dio = ref.watch(apiClientProvider);
  final auth = ref.watch(authServiceProvider);
  final token = await auth.getAccessToken();
  final response = await dio.get<dynamic>(
    '/rounds/$roundId/scorecards',
    options: Options(
      headers: token != null ? {'Authorization': 'Bearer $token'} : null,
    ),
  );
  return _extractList(response.data, Scorecard.fromJson);
});

// ── Players ───────────────────────────────────────────────────────────────────

final playersProvider = FutureProvider<List<Player>>((ref) async {
  final dio = ref.watch(apiClientProvider);
  final auth = ref.watch(authServiceProvider);
  final token = await auth.getAccessToken();
  final response = await dio.get<dynamic>(
    '/players',
    queryParameters: {'page': 1, 'pageSize': 1000},
    options: Options(
      headers: token != null ? {'Authorization': 'Bearer $token'} : null,
    ),
  );
  return _extractList(response.data, Player.fromJson);
});

final playerDetailProvider = FutureProvider.family<Player, int>((
  ref,
  playerId,
) async {
  final dio = ref.watch(apiClientProvider);
  final auth = ref.watch(authServiceProvider);
  final token = await auth.getAccessToken();
  final response = await dio.get<dynamic>(
    '/players/$playerId',
    options: Options(
      headers: token != null ? {'Authorization': 'Bearer $token'} : null,
    ),
  );
  final data = response.data;
  if (data is Map && data.containsKey('data')) {
    return Player.fromJson(data['data'] as Map<String, dynamic>);
  }
  return Player.fromJson(data as Map<String, dynamic>);
});

final playerHandicapHistoryProvider =
    FutureProvider.family<List<HandicapHistoryEntry>, int>((
      ref,
      playerId,
    ) async {
      final dio = ref.watch(apiClientProvider);
      final auth = ref.watch(authServiceProvider);
      final token = await auth.getAccessToken();
      final response = await dio.get<dynamic>(
        '/players/$playerId/handicap-history',
        options: Options(
          headers: token != null ? {'Authorization': 'Bearer $token'} : null,
        ),
      );
      final data = response.data;
      if (data is List) {
        return data
            .map(
              (e) => HandicapHistoryEntry.fromJson(e as Map<String, dynamic>),
            )
            .toList();
      }
      return _extractList(data, HandicapHistoryEntry.fromJson);
    });

final playerRoundsProvider =
    FutureProvider.family<List<PlayerRoundSummary>, int>((ref, playerId) async {
      final dio = ref.watch(apiClientProvider);
      final auth = ref.watch(authServiceProvider);
      final token = await auth.getAccessToken();
      final response = await dio.get<dynamic>(
        '/players/$playerId/rounds',
        options: Options(
          headers: token != null ? {'Authorization': 'Bearer $token'} : null,
        ),
      );
      final data = response.data;
      if (data is List) {
        return data
            .map((e) => PlayerRoundSummary.fromJson(e as Map<String, dynamic>))
            .toList();
      }
      return _extractList(data, PlayerRoundSummary.fromJson);
    });

// ── Tee Times ──────────────────────────────────────────────────────────────────

final nextRoundTeeTimesProvider = FutureProvider<RoundTeeTimeSchedule?>((
  ref,
) async {
  final dio = ref.watch(apiClientProvider);
  final auth = ref.watch(authServiceProvider);
  final token = await auth.getAccessToken();
  try {
    final response = await dio.get<dynamic>(
      '/tee-times/next',
      options: Options(
        headers: token != null ? {'Authorization': 'Bearer $token'} : null,
      ),
    );
    return _extractData(response.data, RoundTeeTimeSchedule.fromJson);
  } on DioException catch (e) {
    if (e.response?.statusCode == 404) return null;
    rethrow;
  }
});

final roundTeeTimesProvider = FutureProvider.family<RoundTeeTimeSchedule?, int>(
  (ref, roundId) async {
    final dio = ref.watch(apiClientProvider);
    final auth = ref.watch(authServiceProvider);
    final token = await auth.getAccessToken();
    try {
      final response = await dio.get<dynamic>(
        '/rounds/$roundId/tee-times',
        options: Options(
          headers: token != null ? {'Authorization': 'Bearer $token'} : null,
        ),
      );
      return _extractData(response.data, RoundTeeTimeSchedule.fromJson);
    } on DioException catch (e) {
      if (e.response?.statusCode == 404) return null;
      rethrow;
    }
  },
);

final myTodaysTeeTimeProvider = FutureProvider<MyTodaysTeeTime?>((ref) async {
  final dio = ref.watch(apiClientProvider);
  final auth = ref.watch(authServiceProvider);
  final token = await auth.getAccessToken();
  if (token == null) return null;
  try {
    final response = await dio.get<dynamic>(
      '/me/todays-tee-time',
      options: Options(headers: {'Authorization': 'Bearer $token'}),
    );
    return _extractData(response.data, MyTodaysTeeTime.fromJson);
  } on DioException catch (e) {
    if (e.response?.statusCode == 404) return null;
    rethrow;
  }
});

final teeTimeGroupScorecardProvider =
    FutureProvider.family<TeeTimeGroupScorecard?, int>((ref, teeTimeId) async {
      final dio = ref.watch(apiClientProvider);
      final auth = ref.watch(authServiceProvider);
      final token = await auth.getAccessToken();
      if (token == null) return null;
      final response = await dio.get<dynamic>(
        '/tee-times/$teeTimeId/group-scorecard',
        options: Options(headers: {'Authorization': 'Bearer $token'}),
      );
      return _extractData(response.data, TeeTimeGroupScorecard.fromJson);
    });

// ── Courses & Statistics ──────────────────────────────────────────────────────

final coursesProvider = FutureProvider<List<Course>>((ref) async {
  final dio = ref.watch(apiClientProvider);
  final auth = ref.watch(authServiceProvider);
  final token = await auth.getAccessToken();
  final response = await dio.get<dynamic>(
    '/courses',
    options: Options(
      headers: token != null ? {'Authorization': 'Bearer $token'} : null,
    ),
  );
  return _extractList(response.data, Course.fromJson);
});

final courseStatisticsProvider = FutureProvider.family<CourseStatistics?, int>((
  ref,
  courseId,
) async {
  final dio = ref.watch(apiClientProvider);
  final auth = ref.watch(authServiceProvider);
  final token = await auth.getAccessToken();
  final response = await dio.get<dynamic>(
    '/courses/$courseId/statistics',
    options: Options(
      headers: token != null ? {'Authorization': 'Bearer $token'} : null,
    ),
  );
  final data = response.data;
  if (data is Map<String, dynamic>) {
    return CourseStatistics.fromJson(data);
  }
  return _extractData(data, CourseStatistics.fromJson);
});

final mostImprovedProvider = FutureProvider<MostImprovedResult?>((ref) async {
  final dio = ref.watch(apiClientProvider);
  final auth = ref.watch(authServiceProvider);
  final token = await auth.getAccessToken();
  final response = await dio.get<dynamic>(
    '/statistics/most-improved',
    options: Options(
      headers: token != null ? {'Authorization': 'Bearer $token'} : null,
    ),
  );
  return _extractData(response.data, MostImprovedResult.fromJson);
});
