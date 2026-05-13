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

  Future<void> setTeeTimePreference(int playerId, List<String> preferredSlots) async {
    await _dio.put<dynamic>(
      '/players/$playerId/tee-time-preference',
      data: {'preferredSlots': preferredSlots},
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
