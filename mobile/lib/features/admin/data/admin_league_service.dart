import 'package:dio/dio.dart';

import '../../../core/api/golf_api_helpers.dart';

/// Thin HTTP wrapper for league admin APIs (parity with web admin).
class AdminLeagueService {
  AdminLeagueService(this._dio);

  final Dio _dio;

  // --- Players ---

  Future<List<Map<String, dynamic>>> listPlayers({int pageSize = 500}) async {
    final r = await _dio.get<Map<String, dynamic>>(
      '/players',
      queryParameters: {'page': 1, 'pageSize': pageSize},
    );
    return extractDataList(r.data);
  }

  Future<Map<String, dynamic>> getPlayer(int id) async {
    final r = await _dio.get<Map<String, dynamic>>('/players/$id');
    final m = r.data;
    if (m == null) throw Exception('Invalid player response');
    return Map<String, dynamic>.from(m);
  }

  Future<void> createPlayer({
    required String name,
    required String email,
    required double initialHandicap,
    String? flightId,
  }) async {
    await _dio.post<Map<String, dynamic>>(
      '/players',
      data: {
        'name': name,
        'email': email,
        'initialHandicap': initialHandicap,
        'flightId': ?flightId,
      },
    );
  }

  Future<void> patchPlayer(
    int id, {
    String? name,
    String? email,
    String? flightId,
  }) async {
    await _dio.patch<Map<String, dynamic>>(
      '/players/$id',
      data: {
        'name': ?name,
        'email': ?email,
        'flightId': ?flightId,
      },
    );
  }

  Future<void> deactivatePlayer(int id) async {
    await _dio.post<Map<String, dynamic>>('/players/$id/deactivate');
  }

  Future<void> deletePlayer(int id) async {
    await _dio.delete<Map<String, dynamic>>('/players/$id');
  }

  Future<void> setHandicap(
    int id, {
    required double newIndex,
    String? notes,
  }) async {
    await _dio.post<Map<String, dynamic>>(
      '/players/$id/handicap',
      data: {'newIndex': newIndex, 'notes': ?notes},
    );
  }

  Future<List<Map<String, dynamic>>> handicapHistory(int id) async {
    final r =
        await _dio.get<Map<String, dynamic>>('/players/$id/handicap-history');
    return extractDataList(r.data);
  }

  // --- Flights ---

  Future<List<Map<String, dynamic>>> listFlights() async {
    final r = await _dio.get<Map<String, dynamic>>('/flights');
    return extractDataList(r.data);
  }

  Future<void> createFlight({
    required String name,
    double? minHandicap,
    double? maxHandicap,
    int displayOrder = 0,
    int? seasonId,
  }) async {
    await _dio.post<Map<String, dynamic>>(
      '/flights',
      data: {
        'name': name,
        'displayOrder': displayOrder,
        'minHandicap': ?minHandicap,
        'maxHandicap': ?maxHandicap,
        'seasonId': ?seasonId,
      },
    );
  }

  Future<void> deleteFlight(int id) async {
    await _dio.delete<Map<String, dynamic>>('/flights/$id');
  }

  // --- Rounds ---

  Future<List<Map<String, dynamic>>> listRounds({int pageSize = 100}) async {
    final r = await _dio.get<Map<String, dynamic>>(
      '/rounds',
      queryParameters: {'page': 1, 'pageSize': pageSize},
    );
    return extractDataList(r.data);
  }

  Future<void> createRound({
    int? seasonId,
    required int flightId,
    required int courseId,
    required String scheduledDate,
    required List<int> playerIds,
    String? notes,
  }) async {
    await _dio.post<Map<String, dynamic>>(
      '/rounds',
      data: {
        'seasonId': ?seasonId,
        'flightId': flightId,
        'courseId': courseId,
        'scheduledDate': scheduledDate,
        'playerIds': playerIds,
        'notes': ?notes,
      },
    );
  }

  Future<void> finalizeRound(int id) async {
    await _dio.post<Map<String, dynamic>>('/rounds/$id/finalize');
  }

  Future<void> deleteRound(int id) async {
    await _dio.delete<Map<String, dynamic>>('/rounds/$id');
  }

  // --- Courses ---

  Future<List<Map<String, dynamic>>> listCourses() async {
    final r = await _dio.get<Map<String, dynamic>>('/courses');
    return extractDataList(r.data);
  }

  Future<Map<String, dynamic>> getCourse(int id) async {
    final r = await _dio.get<Map<String, dynamic>>('/courses/$id');
    final m = r.data;
    if (m == null) throw Exception('Invalid course response');
    return Map<String, dynamic>.from(m);
  }

  Future<void> createCourse({
    required String name,
    required double rating,
    required int slope,
  }) async {
    await _dio.post<Map<String, dynamic>>(
      '/courses',
      data: {'name': name, 'rating': rating, 'slope': slope},
    );
  }

  Future<void> updateCourseHoles(
    int courseId,
    List<Map<String, dynamic>> holes,
  ) async {
    await _dio.put<Map<String, dynamic>>(
      '/courses/$courseId/holes',
      data: {
        'holes': holes
            .map(
              (h) => <String, dynamic>{
                'holeNumber': h['holeNumber'],
                'par': h['par'],
                'strokeIndex': h['strokeIndex'],
              },
            )
            .toList(),
      },
    );
  }

  Future<void> deleteCourse(int id) async {
    await _dio.delete<Map<String, dynamic>>('/courses/$id');
  }

  // --- Seasons ---

  /// API returns a bare JSON array (not `{ data: [...] }`).
  Future<List<Map<String, dynamic>>> listSeasons() async {
    final r = await _dio.get<dynamic>('/seasons');
    final raw = r.data;
    if (raw is List) {
      return raw
          .map((e) => Map<String, dynamic>.from(e as Map))
          .toList();
    }
    return [];
  }

  Future<void> createSeason({
    required String name,
    required int year,
    required String startDate,
    required String endDate,
    int? bestNRounds,
  }) async {
    await _dio.post<Map<String, dynamic>>(
      '/seasons',
      data: {
        'name': name,
        'year': year,
        'startDate': startDate,
        'endDate': endDate,
        'bestNRounds': ?bestNRounds,
      },
    );
  }

  Future<void> activateSeason(int id) async {
    await _dio.post<Map<String, dynamic>>('/seasons/$id/activate');
  }

  Future<void> deleteSeason(int id) async {
    await _dio.delete<Map<String, dynamic>>('/seasons/$id');
  }

  // --- Audit ---

  /// API returns the page DTO at the root (not wrapped in `data`).
  Future<Map<String, dynamic>> auditLog({int page = 1, int pageSize = 25}) async {
    final r = await _dio.get<Map<String, dynamic>>(
      '/admin/audit-log',
      queryParameters: {'page': page, 'pageSize': pageSize},
    );
    final m = r.data;
    if (m == null) throw Exception('Invalid audit response');
    return Map<String, dynamic>.from(m);
  }
}
