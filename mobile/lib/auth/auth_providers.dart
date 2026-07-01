import 'package:dio/dio.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../api/api_client.dart';
import 'auth_service.dart';

final authServiceProvider = Provider<AuthService>((ref) {
  final dio = ref.watch(apiClientProvider);
  return AuthService(dio: dio);
});

// Holds the current AuthResult (null = not signed in)
final authResultProvider = StateProvider<AuthResult?>((ref) => null);

class MyStatusState {
  const MyStatusState({
    this.status = 'none',
    this.playerId,
    this.role = 'player',
    this.isLoading = false,
    this.error,
  });

  final String status;
  final int? playerId;
  final String role;
  final bool isLoading;
  final String? error;

  MyStatusState copyWith({
    String? status,
    int? playerId,
    String? role,
    bool? isLoading,
    String? error,
  }) =>
      MyStatusState(
        status: status ?? this.status,
        playerId: playerId ?? this.playerId,
        role: role ?? this.role,
        isLoading: isLoading ?? this.isLoading,
        error: error,
      );
}

class MyStatusNotifier extends StateNotifier<MyStatusState> {
  MyStatusNotifier(this._dio, this._auth) : super(const MyStatusState());

  final Dio _dio;
  final AuthService _auth;

  Future<void> fetch() async {
    final token = await _auth.getAccessToken();
    if (token == null) {
      state = const MyStatusState(status: 'none');
      return;
    }

    state = state.copyWith(isLoading: true, error: null);
    try {
      final response = await _dio.get<dynamic>(
        '/auth/me',
        options: Options(headers: {'Authorization': 'Bearer $token'}),
      );
      // /auth/me returns the status object directly (no {data: ...}
      // envelope), but tolerate both shapes.
      final body = response.data;
      final Map<String, dynamic> data;
      if (body is Map<String, dynamic> && body['data'] is Map<String, dynamic>) {
        data = body['data'] as Map<String, dynamic>;
      } else if (body is Map<String, dynamic>) {
        data = body;
      } else {
        state = state.copyWith(isLoading: false, error: 'Unexpected response');
        return;
      }

      // The API returns a 'roles' list; derive the highest-privilege role.
      final rawRoles = data['roles'];
      final roles = rawRoles is List
          ? rawRoles.map((e) => e.toString()).toList()
          : [(data['role'] as String?) ?? 'player'];
      const rolePriority = ['admin', 'scorer', 'player'];
      final role = rolePriority.firstWhere(
        roles.contains,
        orElse: () => roles.isNotEmpty ? roles.first : 'player',
      );

      state = MyStatusState(
        status: (data['status'] as String?) ?? 'none',
        playerId: (data['playerId'] as num?)?.toInt(),
        role: role,
      );
    } on DioException catch (e) {
      state = state.copyWith(isLoading: false, error: e.message);
    }
  }
}

final myStatusProvider =
    StateNotifierProvider<MyStatusNotifier, MyStatusState>((ref) {
  final dio = ref.watch(apiClientProvider);
  final auth = ref.watch(authServiceProvider);
  return MyStatusNotifier(dio, auth);
});
