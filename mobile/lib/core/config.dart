class AppConfig {
  static const String apiBaseUrl = 'https://api.capitalgolfleague.com/api/v1';

  // Entra External ID tenant ID (GUID from the Overview blade of your
  // External ID tenant — looks like xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx).
  static const String externalTenantId = '8299a09c-bf4e-4d14-aa8c-13afa3c58965';

  // Client ID of the mobile app registration inside the External ID tenant.
  static const String clientId = 'cfdbb15b-fe49-4775-9dba-f51392b56cd7';

  // Redirect URI registered on the mobile app registration.
  static const String redirectUri = 'com.golfleague.app://auth';

  // Entra External ID authority — no policy segment, no b2clogin domain.
  static String get authority =>
      'https://login.microsoftonline.com/$externalTenantId/v2.0';

  // Scopes: openid + offline_access for token/refresh, plus the API scope.
  // The API scope URI is: api://<API-CLIENT-ID>/<scope-name>
  static const List<String> scopes = [
    'openid',
    'offline_access',
    'api://39dca729-4792-4830-8b72-5441fbe31c2b/access',
  ];
}
