import 'package:dio/dio.dart';
import 'package:flutter_secure_storage/flutter_secure_storage.dart';
import 'package:flutter_web_auth_2/flutter_web_auth_2.dart';

const _accessTokenKey = 'access_token';
const _refreshTokenKey = 'refresh_token';

// Google OAuth 2.0 Android client ID (registered in Google Cloud Console).
// Used by the backend to validate the ID token audience.
const googleClientId =
    '99124513187-hlvp2u9rh6381osoc2v5noluvmpmo1pt.apps.googleusercontent.com';

// The backend relay endpoint receives Google's https:// redirect, then
// immediately bounces to the custom scheme so flutter_web_auth_2 can capture it.
// The https:// URL must be registered in the Google web OAuth client's
// Authorized redirect URIs.
const _externalRedirectUri =
    'https://golf-league-fn-g5vkqe.azurewebsites.net/api/v1/auth/external/google/mobile-callback';
const _externalCallbackScheme = 'com.golfleague.app';

class AuthService {
  AuthService({required Dio dio, FlutterSecureStorage? storage})
    : _dio = dio,
      _storage = storage ?? const FlutterSecureStorage();

  final Dio _dio;
  final FlutterSecureStorage _storage;

  Future<String?> getAccessToken() => _storage.read(key: _accessTokenKey);

  Future<bool> isSignedIn() async {
    final token = await _storage.read(key: _accessTokenKey);
    return token != null;
  }

  Future<AuthResult> loginWithPassword(String email, String password) async {
    final response = await _dio.post<dynamic>(
      '/auth/login',
      data: {'email': email, 'password': password},
    );
    return _handleAuthResponse(response.data);
  }

  Future<AuthResult> register({
    required String email,
    required String password,
    String? inviteToken,
    String? firstName,
    String? lastName,
  }) async {
    final response = await _dio.post<dynamic>(
      '/auth/register',
      data: {
        'email': email,
        'password': password,
        'inviteToken': ?inviteToken,
        'firstName': ?firstName,
        'lastName': ?lastName,
      },
    );
    return _handleAuthResponse(response.data);
  }

  /// Kicks off the Google or Facebook OAuth flow in an in-app browser tab
  /// and waits for the provider redirect back to com.golfleague.app://auth.
  Future<AuthResult> loginWithSocial(String provider) async {
    final start = await _dio.post<dynamic>(
      '/auth/external/$provider/start',
      data: {'redirectUri': _externalRedirectUri},
    );
    final startData =
        (start.data as Map<String, dynamic>)['data'] as Map<String, dynamic>;
    final authorizeUrl = startData['authorizeUrl'] as String;
    final state = startData['state'] as String;

    final callback = await FlutterWebAuth2.authenticate(
      url: authorizeUrl,
      callbackUrlScheme: _externalCallbackScheme,
    );

    final uri = Uri.parse(callback);
    final providerError = uri.queryParameters['error'];
    if (providerError != null && providerError.isNotEmpty) {
      throw AuthException(
        providerError == 'access_denied'
            ? 'Sign-in was cancelled.'
            : 'Sign-in failed: $providerError',
      );
    }
    final code = uri.queryParameters['code'];
    final returnedState = uri.queryParameters['state'];
    if (code == null || returnedState != state) {
      throw const AuthException('Social sign-in returned an invalid response.');
    }

    final complete = await _dio.post<dynamic>(
      '/auth/external/$provider/callback',
      data: {'state': state, 'code': code, 'redirectUri': _externalRedirectUri},
    );
    return _handleAuthResponse(complete.data);
  }

  /// Exchange an MFA-challenge token + 6-digit code for full tokens. Mobile
  /// admins still complete TOTP on a desktop today, but this lets us close
  /// the loop without adding a passkey/authenticator integration.
  Future<AuthResult> verifyTotp({
    required String mfaToken,
    required String code,
  }) async {
    final response = await _dio.post<dynamic>(
      '/auth/mfa/totp/verify',
      data: {'mfaToken': mfaToken, 'code': code},
    );
    return _handleAuthResponse(response.data);
  }

  Future<void> requestPasswordReset(String email) async {
    await _dio.post<dynamic>(
      '/auth/password-reset/request',
      data: {'email': email},
    );
  }

  Future<void> confirmPasswordReset({
    required String email,
    required String token,
    required String newPassword,
  }) async {
    await _dio.post<dynamic>(
      '/auth/password-reset/confirm',
      data: {'email': email, 'token': token, 'newPassword': newPassword},
    );
  }

  /// Start TOTP enrollment. During first-login enrollment the user only has
  /// an MFA-challenge token; pass it via [bearerOverride]. Returns the shared
  /// secret and otpauth:// URI to show in the enrollment UI.
  Future<({String secret, String otpAuthUri})> startTotpEnrollment({
    String? bearerOverride,
  }) async {
    final token = bearerOverride ?? await getAccessToken() ?? '';
    final response = await _dio.post<dynamic>(
      '/auth/mfa/totp/enroll',
      options: Options(headers: {'Authorization': 'Bearer $token'}),
    );
    final raw = response.data;
    final data = raw is Map && raw.containsKey('data')
        ? raw['data'] as Map<String, dynamic>
        : raw as Map<String, dynamic>;
    return (
      secret: data['secret'] as String? ?? '',
      otpAuthUri: data['otpAuthUri'] as String? ?? '',
    );
  }

  Future<void> verifyTotpEnrollment(String code, {String? bearerOverride}) async {
    final token = bearerOverride ?? await getAccessToken() ?? '';
    await _dio.post<dynamic>(
      '/auth/mfa/totp/verify-enrollment',
      data: {'code': code},
      options: Options(headers: {'Authorization': 'Bearer $token'}),
    );
  }

  Future<AuthResult?> refresh() async {
    final refreshToken = await _storage.read(key: _refreshTokenKey);
    if (refreshToken == null) return null;
    try {
      final response = await _dio.post<dynamic>(
        '/auth/refresh',
        data: {'refreshToken': refreshToken},
      );
      return _handleAuthResponse(response.data);
    } on DioException {
      await signOut();
      return null;
    }
  }

  Future<void> signOut() async {
    final refreshToken = await _storage.read(key: _refreshTokenKey);
    if (refreshToken != null) {
      try {
        await _dio.post<dynamic>(
          '/auth/logout',
          data: {'refreshToken': refreshToken},
        );
      } catch (_) {
        // Best-effort — clear local storage regardless.
      }
    }
    await _storage.deleteAll();
  }

  Future<AuthResult> _handleAuthResponse(dynamic responseData) async {
    final data =
        (responseData as Map<String, dynamic>)['data'] as Map<String, dynamic>;
    final accessToken = data['accessToken'] as String;
    final refreshToken = (data['refreshToken'] as String?) ?? '';
    final mfaRequired = data['mfaRequired'] as bool? ?? false;
    final mfaEnrollmentRequired =
        data['mfaEnrollmentRequired'] as bool? ?? false;

    // Backend returns a 'roles' list; derive the highest-privilege role.
    final rawRoles = data['roles'];
    final List<String> roles = rawRoles is List
        ? rawRoles.map((e) => e.toString()).toList()
        : [(data['role'] as String?) ?? 'player'];
    const rolePriority = ['admin', 'scorer', 'player'];
    final role = rolePriority.firstWhere(
      (r) => roles.contains(r),
      orElse: () => roles.isNotEmpty ? roles.first : 'player',
    );

    if (mfaRequired || mfaEnrollmentRequired) {
      // Don't store the challenge token — caller must complete MFA first.
      return AuthResult(
        accessToken: accessToken,
        refreshToken: '',
        role: role,
        mfaRequired: mfaRequired,
        mfaEnrollmentRequired: mfaEnrollmentRequired,
      );
    }

    await _storage.write(key: _accessTokenKey, value: accessToken);
    if (refreshToken.isNotEmpty) {
      await _storage.write(key: _refreshTokenKey, value: refreshToken);
    }
    return AuthResult(
      accessToken: accessToken,
      refreshToken: refreshToken,
      role: role,
      mfaRequired: false,
    );
  }
}

class AuthResult {
  const AuthResult({
    required this.accessToken,
    required this.refreshToken,
    required this.role,
    required this.mfaRequired,
    this.mfaEnrollmentRequired = false,
  });

  final String accessToken;
  final String refreshToken;
  final String role;
  final bool mfaRequired;
  final bool mfaEnrollmentRequired;
}

class AuthException implements Exception {
  const AuthException(this.message);
  final String message;
  @override
  String toString() => message;
}
