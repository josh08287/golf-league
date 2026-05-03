import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../models/models.dart';
import 'api_client.dart';

List<T> _extractList<T>(
  dynamic responseData,
  T Function(Map<String, dynamic>) fromJson,
) {
  if (responseData == null) return [];
  if (responseData is Map) {
    final data = responseData['data'];
    if (data is List) return data.map((e) => fromJson(e as Map<String, dynamic>)).toList();
  }
  if (responseData is List) {
    return responseData.map((e) => fromJson(e as Map<String, dynamic>)).toList();
  }
  return [];
}

final flightsProvider = FutureProvider<List<Flight>>((ref) async {
  final dio = ref.watch(apiClientProvider);
  final response = await dio.get<dynamic>('/flights');
  return _extractList(response.data, Flight.fromJson);
});

final roundsProvider = FutureProvider<List<Round>>((ref) async {
  final dio = ref.watch(apiClientProvider);
  final response = await dio.get<dynamic>('/rounds', queryParameters: {
    'page': 1,
    'pageSize': 20,
  });
  final rounds = _extractList(response.data, Round.fromJson);
  rounds.sort((a, b) => a.scheduledDate.compareTo(b.scheduledDate));
  return rounds;
});

final roundDetailProvider = FutureProvider.family<Round, int>((ref, roundId) async {
  final dio = ref.watch(apiClientProvider);
  final response = await dio.get<dynamic>('/rounds/$roundId');
  final data = response.data;
  if (data is Map && data.containsKey('data')) {
    return Round.fromJson(data['data'] as Map<String, dynamic>);
  }
  return Round.fromJson(data as Map<String, dynamic>);
});

final scorecardsProvider = FutureProvider.family<List<Scorecard>, int>((ref, roundId) async {
  final dio = ref.watch(apiClientProvider);
  final response = await dio.get<dynamic>('/rounds/$roundId/scorecards');
  return _extractList(response.data, Scorecard.fromJson);
});
