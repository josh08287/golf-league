import 'package:dio/dio.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import 'auth_service.dart';

final authServiceProvider = Provider<AuthService>((ref) => AuthService());

// Holds the current AuthResult (null = not signed in)
final authResultProvider = StateProvider<AuthResult?>((ref) => null);

// Values: "approved" | "none"
class MyStatusState {
  const MyStatusState({
    this.status = 'none',
    this.playerId,
    this.isLoading = false,
    this.error,
  });

  final String status;
  final int? playerId;
  final bool isLoading;
  final String? error;

  MyStatusState copyWith({
    String? status,
    int? playerId,
    bool? isLoading,
    String? error,
  }) =>
      MyStatusState(
        status: status ?? this.status,
        playerId: playerId ?? this.playerId,
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
      final response = await _dio.get(
        '/auth/me',
        options: Options(headers: {'Authorization': 'Bearer $token'}),
      );
      final data = (response.data as Map<String, dynamic>)['data'] as Map<String, dynamic>;
      state = MyStatusState(
        status: data['status'] as String,
        playerId: data['playerId'] as int?,
      );
    } on DioException catch (e) {
      state = state.copyWith(isLoading: false, error: e.message);
    }
  }
}

final myStatusProvider =
    StateNotifierProvider<MyStatusNotifier, MyStatusState>((ref) {
  final dio = ref.watch(_authDioProvider);
  final auth = ref.watch(authServiceProvider);
  return MyStatusNotifier(dio, auth);
});

final _authDioProvider = Provider<Dio>((ref) {
  return Dio(BaseOptions(
    baseUrl: 'https://golf-league-fn-g5vkqe.azurewebsites.net/api/v1',
    connectTimeout: const Duration(seconds: 15),
    receiveTimeout: const Duration(seconds: 15),
    headers: {'Content-Type': 'application/json'},
  ));
});
