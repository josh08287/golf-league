import 'package:dart_jsonwebtoken/dart_jsonwebtoken.dart';
import 'package:flutter_appauth/flutter_appauth.dart';
import 'package:flutter_secure_storage/flutter_secure_storage.dart';
import 'package:hive_flutter/hive_flutter.dart';

import '../config.dart';

const _refreshTokenKey = 'refresh_token';
const _accessTokenKey = 'access_token';
const _claimsKey = 'claims';

class TokenService {
  TokenService({
    FlutterAppAuth? appAuth,
    FlutterSecureStorage? secureStorage,
  })  : _appAuth = appAuth ?? const FlutterAppAuth(),
        _secureStorage = secureStorage ??
            const FlutterSecureStorage(
              aOptions: AndroidOptions(encryptedSharedPreferences: true),
            );

  final FlutterAppAuth _appAuth;
  final FlutterSecureStorage _secureStorage;

  Box get _authBox => Hive.box('auth');

  // ---------------------------------------------------------------------------
  // Public API
  // ---------------------------------------------------------------------------

  Future<void> login() async {
    final result = await _appAuth.authorizeAndExchangeCode(
      AuthorizationTokenRequest(
        AppConfig.clientId,
        AppConfig.redirectUri,
        serviceConfiguration: AuthorizationServiceConfiguration(
          authorizationEndpoint:
              '${AppConfig.authority}/oauth2/v2.0/authorize',
          tokenEndpoint: '${AppConfig.authority}/oauth2/v2.0/token',
        ),
        scopes: AppConfig.scopes,
        promptValues: ['login'],
      ),
    );

    if (result == null) {
      throw Exception('Login was cancelled or failed.');
    }

    await _persistTokens(
      accessToken: result.accessToken,
      refreshToken: result.refreshToken,
    );
  }

  Future<void> logout() async {
    await _secureStorage.delete(key: _refreshTokenKey);
    await _authBox.delete(_accessTokenKey);
    await _authBox.delete(_claimsKey);
  }

  Future<String?> getAccessToken() async {
    return _authBox.get(_accessTokenKey) as String?;
  }

  Future<String> refresh() async {
    final refreshToken = await _secureStorage.read(key: _refreshTokenKey);
    if (refreshToken == null) {
      throw Exception('No refresh token available. Please log in again.');
    }

    final result = await _appAuth.token(
      TokenRequest(
        AppConfig.clientId,
        AppConfig.redirectUri,
        serviceConfiguration: AuthorizationServiceConfiguration(
          authorizationEndpoint:
              '${AppConfig.authority}/oauth2/v2.0/authorize',
          tokenEndpoint: '${AppConfig.authority}/oauth2/v2.0/token',
        ),
        refreshToken: refreshToken,
        scopes: AppConfig.scopes,
      ),
    );

    if (result == null || result.accessToken == null) {
      throw Exception('Token refresh failed. Please log in again.');
    }

    await _persistTokens(
      accessToken: result.accessToken,
      refreshToken: result.refreshToken ?? refreshToken,
    );

    return result.accessToken!;
  }

  Future<bool> isLoggedIn() async {
    final token = await getAccessToken();
    if (token == null) return false;
    try {
      final jwt = JWT.decode(token);
      final exp = jwt.payload['exp'] as int?;
      if (exp == null) return false;
      final expiry =
          DateTime.fromMillisecondsSinceEpoch(exp * 1000, isUtc: true);
      // Token is valid if it expires more than 60 seconds from now.
      return expiry.isAfter(
        DateTime.now().toUtc().add(const Duration(seconds: 60)),
      );
    } catch (_) {
      return false;
    }
  }

  List<String> getRoles() {
    final claims = _authBox.get(_claimsKey);
    if (claims == null) return [];
    final role = (claims as Map<dynamic, dynamic>)['extension_Role'];
    if (role == null) return [];
    if (role is List) return List<String>.from(role);
    return [role.toString()];
  }

  int? getPlayerId() {
    final claims = _authBox.get(_claimsKey);
    if (claims == null) return null;
    final id = (claims as Map<dynamic, dynamic>)['extension_PlayerId'];
    if (id == null) return null;
    return int.tryParse(id.toString());
  }

  bool hasRole(String role) => getRoles().contains(role);

  bool get isScorer =>
      hasRole('scorer') || hasRole('admin');

  // ---------------------------------------------------------------------------
  // Private helpers
  // ---------------------------------------------------------------------------

  Future<void> _persistTokens({
    String? accessToken,
    String? refreshToken,
  }) async {
    if (refreshToken != null) {
      await _secureStorage.write(
        key: _refreshTokenKey,
        value: refreshToken,
      );
    }

    if (accessToken != null) {
      await _authBox.put(_accessTokenKey, accessToken);
      try {
        final jwt = JWT.decode(accessToken);
        await _authBox.put(_claimsKey, Map<String, dynamic>.from(
          (jwt.payload as Map).map(
            (k, v) => MapEntry(k.toString(), v),
          ),
        ));
      } catch (_) {
        // Non-critical; claims used for role checks only.
      }
    }
  }
}
