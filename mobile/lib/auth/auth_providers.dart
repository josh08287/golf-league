import 'package:dio/dio.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import 'auth_service.dart';

final authServiceProvider = Provider<AuthService>((ref) => AuthService());

// Holds the current AuthResult (null = not signed in)
final authResultProvider = StateProvider<AuthResult?>((ref) => null);

// The status returned by GET /api/v1/auth/me
// Values: "none" | "pending" | "approved" | "rejected"
class MyStatusState {
  const MyStatusState({
    this.status = 'none',
    this.playerId,
    this.rejectionReason,
    this.isLoading = false,
    this.error,
  });

  final String status;
  final int? playerId;
  final String? rejectionReason;
  final bool isLoading;
  final String? error;

  MyStatusState copyWith({
    String? status,
    int? playerId,
    String? rejectionReason,
    bool? isLoading,
    String? error,
  }) =>
      MyStatusState(
        status: status ?? this.status,
        playerId: playerId ?? this.playerId,
        rejectionReason: rejectionReason ?? this.rejectionReason,
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
        rejectionReason: data['rejectionReason'] as String?,
      );
    } on DioException catch (e) {
      state = state.copyWith(isLoading: false, error: e.message);
    }
  }

  Future<void> submitRegistration({
    required String firstName,
    required String lastName,
    required String email,
    String? phone,
  }) async {
    final token = await _auth.getAccessToken();
    if (token == null) return;

    state = state.copyWith(isLoading: true, error: null);
    try {
      await _dio.post(
        '/auth/register',
        data: {
          'firstName': firstName,
          'lastName': lastName,
          'email': email,
          if (phone != null && phone.isNotEmpty) 'phone': phone,
        },
        options: Options(headers: {'Authorization': 'Bearer $token'}),
      );
      await fetch();
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

// Separate Dio just for auth endpoints — no auth interceptor needed yet
final _authDioProvider = Provider<Dio>((ref) {
  return Dio(BaseOptions(
    baseUrl: 'https://golf-league-fn-g5vkqe.azurewebsites.net/api/v1',
    connectTimeout: const Duration(seconds: 15),
    receiveTimeout: const Duration(seconds: 15),
    headers: {'Content-Type': 'application/json'},
  ));
});
