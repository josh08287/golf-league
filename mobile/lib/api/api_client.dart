import 'package:dio/dio.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

const _baseUrl = 'https://golf-league-fn-g5vkqe.azurewebsites.net/api/v1';

final apiClientProvider = Provider<Dio>((ref) {
  return Dio(
    BaseOptions(
      baseUrl: _baseUrl,
      connectTimeout: const Duration(seconds: 15),
      receiveTimeout: const Duration(seconds: 15),
      headers: {
        'Accept': 'application/json',
        'Content-Type': 'application/json',
      },
    ),
  );
});
