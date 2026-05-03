import 'package:dio/dio.dart';
import 'package:flutter/foundation.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../auth/token_service.dart';
import '../config.dart';
import 'auth_interceptor.dart';

final tokenServiceProvider = Provider<TokenService>((ref) => TokenService());

final dioClientProvider = Provider<Dio>((ref) {
  final tokenService = ref.watch(tokenServiceProvider);

  final dio = Dio(
    BaseOptions(
      baseUrl: AppConfig.apiBaseUrl,
      connectTimeout: const Duration(seconds: 30),
      receiveTimeout: const Duration(seconds: 30),
      headers: {
        'Accept': 'application/vnd.golfleague.v1+json',
        'Content-Type': 'application/json',
      },
    ),
  );

  dio.interceptors.add(
    AuthInterceptor(
      tokenService: tokenService,
      onLogout: () {
        ref.invalidate(tokenServiceProvider);
      },
    ),
  );

  if (kDebugMode) {
    dio.interceptors.add(
      LogInterceptor(
        requestBody: true,
        responseBody: true,
        logPrint: (obj) => debugPrint(obj.toString()),
      ),
    );
  }

  return dio;
});
