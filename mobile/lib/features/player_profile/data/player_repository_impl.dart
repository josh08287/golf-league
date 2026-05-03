import 'package:connectivity_plus/connectivity_plus.dart';
import 'package:dio/dio.dart';

import '../domain/models.dart';
import '../domain/player_repository.dart';

class PlayerRepositoryImpl implements PlayerRepository {
  PlayerRepositoryImpl({required this.dio});

  final Dio dio;

  @override
  Future<PlayerProfile> getPlayer(int playerId) async {
    final isOnline = await _checkConnectivity();
    if (!isOnline) {
      throw Exception('Device is offline. Player profile not cached.');
    }

    final response =
        await dio.get<Map<String, dynamic>>('/players/$playerId');
    final data = _extractSingle(response.data);
    return PlayerProfile.fromJson(data);
  }

  @override
  Future<List<HandicapHistoryEntry>> getHandicapHistory(
    int playerId,
  ) async {
    final isOnline = await _checkConnectivity();
    if (!isOnline) return [];

    final response = await dio
        .get<Map<String, dynamic>>('/players/$playerId/handicap-history');
    final rawList = _extractList(response.data);
    return rawList.map((e) => HandicapHistoryEntry.fromJson(e)).toList();
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
