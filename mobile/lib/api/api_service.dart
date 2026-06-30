import 'package:dio/dio.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../auth/auth_providers.dart';
import '../models/models.dart';
import 'api_client.dart';

final apiServiceProvider = Provider<ApiService>((ref) {
  final dio = ref.watch(apiClientProvider);
  final auth = ref.watch(authServiceProvider);
  return ApiService(dio: dio, auth: auth);
});

class ApiService {
  const ApiService({required Dio dio, required dynamic auth})
      : _dio = dio,
        _auth = auth;

  final Dio _dio;
  final dynamic _auth;

  Future<String?> _getToken() => _auth.getAccessToken();

  Future<Options> _authOptions() async {
    final token = await _getToken();
    return Options(headers: token != null ? {'Authorization': 'Bearer $token'} : null);
  }

  // ── Tee Time Mutations ────────────────────────────────────────────────────────

  Future<RoundTeeTimeSchedule?> joinTeeTime(int roundId, int teeTimeId) async {
    final response = await _dio.post<dynamic>(
      '/rounds/$roundId/tee-times/$teeTimeId/join',
      options: await _authOptions(),
    );
    final data = response.data;
    if (data is Map && data.containsKey('data')) {
      return RoundTeeTimeSchedule.fromJson(data['data'] as Map<String, dynamic>);
    }
    if (data is Map<String, dynamic>) {
      return RoundTeeTimeSchedule.fromJson(data);
    }
    return null;
  }

  Future<RoundTeeTimeSchedule?> leaveTeeTime(int roundId) async {
    final response = await _dio.post<dynamic>(
      '/rounds/$roundId/tee-times/leave',
      options: await _authOptions(),
    );
    final data = response.data;
    if (data is Map && data.containsKey('data')) {
      return RoundTeeTimeSchedule.fromJson(data['data'] as Map<String, dynamic>);
    }
    if (data is Map<String, dynamic>) {
      return RoundTeeTimeSchedule.fromJson(data);
    }
    return null;
  }

  Future<Player> updatePlayer(
    int playerId, {
    required String firstName,
    required String lastName,
    String? email,
  }) async {
    final response = await _dio.put<dynamic>(
      '/players/$playerId',
      data: {'firstName': firstName, 'lastName': lastName, 'email': email},
      options: await _authOptions(),
    );
    final data = response.data;
    final json = (data is Map && data.containsKey('data'))
        ? data['data'] as Map<String, dynamic>
        : data as Map<String, dynamic>;
    return Player.fromJson(json);
  }

  Future<void> setTeeTimePreference(int playerId, List<String> preferredSlots) async {
    await _dio.put<dynamic>(
      '/players/$playerId/tee-time-preference',
      data: {'preferredSlots': preferredSlots},
      options: await _authOptions(),
    );
  }

  Future<RoundTeeTimeSchedule?> skipMyWeek(int roundId, bool skipped) async {
    final response = await _dio.post<dynamic>(
      '/rounds/$roundId/tee-times/me/skip',
      data: {'skipped': skipped},
      options: await _authOptions(),
    );
    final data = response.data;
    if (data is Map && data.containsKey('data')) {
      return RoundTeeTimeSchedule.fromJson(data['data'] as Map<String, dynamic>);
    }
    if (data is Map<String, dynamic>) {
      return RoundTeeTimeSchedule.fromJson(data);
    }
    return null;
  }

  // ── Admin: tee time management ────────────────────────────────────────────

  Future<void> adminMoveParticipantToTeeTime(
    int roundId,
    int teeTimeId,
    int participantId,
  ) async {
    await _dio.post<dynamic>(
      '/admin/rounds/$roundId/tee-times/$teeTimeId/participants/$participantId',
      options: await _authOptions(),
    );
  }

  Future<void> adminRemoveParticipantFromTeeTime(
    int roundId,
    int participantId,
  ) async {
    await _dio.delete<dynamic>(
      '/admin/rounds/$roundId/tee-times/participants/$participantId',
      options: await _authOptions(),
    );
  }

  // ── Admin: rounds ───────────────────────────────────────────────────────────

  Future<void> createRound({
    required int halfId,
    required int courseId,
    required String scheduledDate,
    String nineHoleSide = 'Front',
    String? notes,
  }) async {
    await _dio.post<dynamic>(
      '/rounds',
      data: {
        'halfId': halfId,
        'courseId': courseId,
        'scheduledDate': scheduledDate,
        'nineHoleSide': nineHoleSide,
        if (notes != null && notes.isNotEmpty) 'notes': notes,
      },
      options: await _authOptions(),
    );
  }

  Future<void> finalizeRound(int roundId) async {
    await _dio.post<dynamic>(
      '/rounds/$roundId/finalize',
      options: await _authOptions(),
    );
  }

  Future<void> reopenRound(int roundId) async {
    await _dio.post<dynamic>(
      '/rounds/$roundId/reopen',
      options: await _authOptions(),
    );
  }

  Future<void> cancelRound(int roundId) async {
    await _dio.post<dynamic>(
      '/rounds/$roundId/cancel',
      options: await _authOptions(),
    );
  }

  Future<void> deleteRound(int roundId) async {
    await _dio.delete<dynamic>(
      '/rounds/$roundId',
      options: await _authOptions(),
    );
  }

  // ── Admin: score entry ────────────────────────────────────────────────────

  Future<void> submitHoleScores(
    int roundId,
    int playerId,
    List<Map<String, int>> scores,
  ) async {
    await _dio.put<dynamic>(
      '/rounds/$roundId/scores/$playerId/holes',
      data: {'scores': scores},
      options: await _authOptions(),
    );
  }

  Future<void> setParticipantSkipped(
    int roundId,
    int playerId,
    bool skipped,
  ) async {
    await _dio.post<dynamic>(
      '/rounds/$roundId/participants/$playerId/skip',
      data: {'skipped': skipped},
      options: await _authOptions(),
    );
  }

  // ── Score Entry ─────────────────────────────────────────────────────────────

  Future<Map<String, dynamic>?> submitTeeTimeScores(
    int teeTimeId,
    List<Map<String, dynamic>> playerScores,
  ) async {
    final response = await _dio.post<dynamic>(
      '/tee-times/$teeTimeId/submit-scores',
      data: {'playerScores': playerScores},
      options: await _authOptions(),
    );
    final data = response.data;
    if (data is Map && data.containsKey('data')) {
      return data['data'] as Map<String, dynamic>;
    }
    if (data is Map<String, dynamic>) {
      return data;
    }
    return null;
  }
}
