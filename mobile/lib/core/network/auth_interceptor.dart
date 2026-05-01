import 'package:dio/dio.dart';

import '../auth/token_service.dart';

typedef OnLogout = void Function();

class AuthInterceptor extends Interceptor {
  AuthInterceptor({
    required this.tokenService,
    required this.onLogout,
  });

  final TokenService tokenService;
  final OnLogout onLogout;

  // Used to avoid multiple concurrent refresh attempts.
  bool _isRefreshing = false;
  final List<_RetryRequest> _pendingRequests = [];

  @override
  Future<void> onRequest(
    RequestOptions options,
    RequestInterceptorHandler handler,
  ) async {
    final token = await tokenService.getAccessToken();
    if (token != null) {
      options.headers['Authorization'] = 'Bearer $token';
    }
    handler.next(options);
  }

  @override
  Future<void> onError(
    DioException err,
    ErrorInterceptorHandler handler,
  ) async {
    if (err.response?.statusCode != 401) {
      handler.next(err);
      return;
    }

    // Queue this request if a refresh is already in progress.
    if (_isRefreshing) {
      final retryCompleter = _RetryRequest(err, handler);
      _pendingRequests.add(retryCompleter);
      return;
    }

    _isRefreshing = true;
    try {
      final newToken = await tokenService.refresh();
      // Retry the original request with the fresh token.
      final response = await _retry(err.requestOptions, newToken);
      handler.resolve(response);

      // Drain queued requests.
      for (final pending in _pendingRequests) {
        try {
          final r = await _retry(
            pending.err.requestOptions,
            newToken,
          );
          pending.handler.resolve(r);
        } catch (e) {
          pending.handler.next(pending.err);
        }
      }
    } catch (_) {
      // Refresh failed — log out and reject all pending requests.
      await tokenService.logout();
      onLogout();
      handler.next(err);
      for (final pending in _pendingRequests) {
        pending.handler.next(pending.err);
      }
    } finally {
      _pendingRequests.clear();
      _isRefreshing = false;
    }
  }

  Future<Response<dynamic>> _retry(
    RequestOptions options,
    String token,
  ) async {
    final dio = Dio();
    return dio.request<dynamic>(
      options.path,
      data: options.data,
      queryParameters: options.queryParameters,
      options: Options(
        method: options.method,
        headers: {
          ...options.headers,
          'Authorization': 'Bearer $token',
        },
        responseType: options.responseType,
        contentType: options.contentType,
      ),
    );
  }
}

class _RetryRequest {
  const _RetryRequest(this.err, this.handler);
  final DioException err;
  final ErrorInterceptorHandler handler;
}
