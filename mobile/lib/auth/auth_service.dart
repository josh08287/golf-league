import 'dart:convert';

import 'package:flutter_appauth/flutter_appauth.dart';
import 'package:flutter_secure_storage/flutter_secure_storage.dart';

const _tenantId = '8299a09c-bf4e-4d14-aa8c-13afa3c58965';
const _clientId = '39dca729-4792-4830-8b72-5441fbe31c2b';
const _redirectUri = 'com.golfleague.app://auth';
const _discoveryUrl =
    'https://login.microsoftonline.com/$_tenantId/v2.0/.well-known/openid-configuration';
const _scopes = [
  'openid',
  'offline_access',
  'api://$_clientId/access',
];

const _accessTokenKey = 'access_token';
const _refreshTokenKey = 'refresh_token';
const _idTokenKey = 'id_token';

class AuthService {
  AuthService({
    FlutterAppAuth? appAuth,
    FlutterSecureStorage? storage,
  })  : _appAuth = appAuth ?? const FlutterAppAuth(),
        _storage = storage ?? const FlutterSecureStorage();

  final FlutterAppAuth _appAuth;
  final FlutterSecureStorage _storage;

  Future<AuthResult?> signIn() async {
    final result = await _appAuth.authorizeAndExchangeCode(
      AuthorizationTokenRequest(
        _clientId,
        _redirectUri,
        discoveryUrl: _discoveryUrl,
        scopes: _scopes,
        promptValues: ['select_account'],
      ),
    );
    await _persist(result.accessToken, result.refreshToken, result.idToken);
    return AuthResult._fromAuthorizeResponse(result);
  }

  Future<AuthResult?> refresh() async {
    final refreshToken = await _storage.read(key: _refreshTokenKey);
    if (refreshToken == null) return null;

    final result = await _appAuth.token(
      TokenRequest(
        _clientId,
        _redirectUri,
        discoveryUrl: _discoveryUrl,
        refreshToken: refreshToken,
        scopes: _scopes,
      ),
    );
    await _persist(result.accessToken, result.refreshToken, result.idToken);
    return AuthResult._fromTokenResponse(result);
  }

  Future<String?> getAccessToken() => _storage.read(key: _accessTokenKey);

  Future<bool> isSignedIn() async {
    final token = await _storage.read(key: _accessTokenKey);
    return token != null;
  }

  Future<void> signOut() async {
    await _storage.deleteAll();
  }

  Future<void> _persist(String? access, String? refresh, String? id) async {
    if (access != null) await _storage.write(key: _accessTokenKey, value: access);
    if (refresh != null) await _storage.write(key: _refreshTokenKey, value: refresh);
    if (id != null) await _storage.write(key: _idTokenKey, value: id);
  }
}

class AuthResult {
  const AuthResult({
    required this.accessToken,
    required this.idToken,
    this.givenName,
    this.familyName,
    this.email,
    this.phone,
    this.sub,
  });

  final String accessToken;
  final String idToken;
  final String? givenName;
  final String? familyName;
  final String? email;
  final String? phone;
  final String? sub;

  factory AuthResult._fromAuthorizeResponse(AuthorizationTokenResponse r) {
    final claims = _decodeJwt(r.idToken ?? '');
    return AuthResult(
      accessToken: r.accessToken ?? '',
      idToken: r.idToken ?? '',
      givenName: claims['given_name'] as String?,
      familyName: claims['family_name'] as String?,
      email: (claims['email'] ?? claims['preferred_username']) as String?,
      phone: claims['phone_number'] as String?,
      sub: (claims['oid'] ?? claims['sub']) as String?,
    );
  }

  factory AuthResult._fromTokenResponse(TokenResponse r) {
    final claims = _decodeJwt(r.idToken ?? '');
    return AuthResult(
      accessToken: r.accessToken ?? '',
      idToken: r.idToken ?? '',
      givenName: claims['given_name'] as String?,
      familyName: claims['family_name'] as String?,
      email: (claims['email'] ?? claims['preferred_username']) as String?,
      phone: claims['phone_number'] as String?,
      sub: (claims['oid'] ?? claims['sub']) as String?,
    );
  }
}

Map<String, dynamic> _decodeJwt(String token) {
  try {
    final parts = token.split('.');
    if (parts.length < 2) return {};
    final payload = parts[1].replaceAll('-', '+').replaceAll('_', '/');
    final padded = payload.padRight((payload.length + 3) & ~3, '=');
    final bytes = base64.decode(padded);
    final decoded = utf8.decode(bytes);
    return jsonDecode(decoded) as Map<String, dynamic>;
  } catch (_) {
    return {};
  }
}
