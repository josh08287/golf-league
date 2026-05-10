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
      final data = (response.data as Map<String, dynamic>)['data'] as Map<String, dynamic>;
      state = MyStatusState(
        status: data['status'] as String,
        playerId: data['playerId'] as int?,
        role: (data['role'] as String?) ?? 'player',
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
