import 'package:dio/dio.dart';

/// Paged API envelope: `{ "data": [ ... ] }` or list metadata.
List<Map<String, dynamic>> extractDataList(Map<String, dynamic>? response) {
  if (response == null) return [];
  final data = response['data'];
  if (data is List) {
    return data.map((e) => Map<String, dynamic>.from(e as Map)).toList();
  }
  return [];
}

Map<String, dynamic>? extractDataMap(Map<String, dynamic>? response) {
  if (response == null) return null;
  final data = response['data'];
  if (data is Map) {
    return Map<String, dynamic>.from(data);
  }
  return null;
}

String? extractErrorMessage(DioException e) {
  final d = e.response?.data;
  if (d is Map && d['error'] != null) return d['error'].toString();
  return e.message;
}

/// Returns the active season id, or null if none marked active.
Future<int?> fetchActiveSeasonId(Dio dio) async {
  final r = await dio.get<Map<String, dynamic>>('/seasons');
  final list = extractDataList(r.data);
  for (final m in list) {
    final active = m['isActive'];
    if (active == true) {
      return (m['id'] as num).toInt();
    }
  }
  return null;
}
