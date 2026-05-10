import axios from 'axios';

const ACCESS_KEY = 'golf-league-access';
const REFRESH_KEY = 'golf-league-refresh';
const ACCESS_EXPIRES_KEY = 'golf-league-access-expires';

export type Provider = 'google' | 'facebook';

export interface AuthResponse {
  accessToken: string;
  refreshToken: string;
  accessTokenExpiresAt: string;
  role: 'admin' | 'scorer' | 'player' | 'mfa-pending';
  userId: string;
  mfaRequired: boolean;
  mfaEnrollmentRequired: boolean;
}

export interface CurrentUser {
  userId: string;
  email: string;
  role: 'admin' | 'scorer' | 'player';
  playerId: number | null;
  hasPasskey: boolean;
  hasTotp: boolean;
}

const BASE_URL =
  (import.meta.env.VITE_API_BASE_URL as string | undefined) ?? '/api/v1';

// Bare axios instance for auth calls — does NOT go through the interceptor
// chain in api.ts (which would try to attach the very token we're trying to
// fetch and recurse on 401).
const authClient = axios.create({
  baseURL: BASE_URL,
  headers: { 'Content-Type': 'application/json' },
});

// ── Token storage ────────────────────────────────────────────────────────────
// Access token: kept in module memory + mirrored to localStorage so a hard
// reload doesn't kick the user out. Mirror is best-effort — XSS exposure is
// unavoidable for a pure SPA without a backend session cookie, which is a
// bigger lift we deferred.
// Refresh token: localStorage. Always rotated on use.

let accessTokenInMemory: string | null = null;

export function getAccessToken(): string | null {
  if (accessTokenInMemory) return accessTokenInMemory;
  const stored = localStorage.getItem(ACCESS_KEY);
  if (!stored) return null;
  accessTokenInMemory = stored;
  return stored;
}

function setAccessToken(token: string | null, expiresAt?: string) {
  accessTokenInMemory = token;
  if (token) {
    localStorage.setItem(ACCESS_KEY, token);
    if (expiresAt) localStorage.setItem(ACCESS_EXPIRES_KEY, expiresAt);
  } else {
    localStorage.removeItem(ACCESS_KEY);
    localStorage.removeItem(ACCESS_EXPIRES_KEY);
  }
}

function setRefreshToken(token: string | null) {
  if (token) localStorage.setItem(REFRESH_KEY, token);
  else localStorage.removeItem(REFRESH_KEY);
}

function getRefreshToken(): string | null {
  return localStorage.getItem(REFRESH_KEY);
}

export function isAuthenticated(): boolean {
  return getAccessToken() !== null;
}

export function storeAuthResponse(resp: AuthResponse) {
  setAccessToken(resp.accessToken, resp.accessTokenExpiresAt);
  // Only store refresh token when we got a real one — the MFA-challenge path
  // returns an empty string here.
  setRefreshToken(resp.refreshToken || null);
}

export function clearAuth() {
  setAccessToken(null);
  setRefreshToken(null);
}

// ── Auth API calls ───────────────────────────────────────────────────────────

function unwrap<T>(envelope: { data?: T } | T): T {
  if (envelope && typeof envelope === 'object' && 'data' in (envelope as object)) {
    return (envelope as { data: T }).data;
  }
  return envelope as T;
}

export async function login(email: string, password: string): Promise<AuthResponse> {
  const res = await authClient.post('/auth/login', { email, password });
  const data = unwrap<AuthResponse>(res.data);
  storeAuthResponse(data);
  return data;
}

export async function register(input: {
  email: string;
  password: string;
  firstName?: string;
  lastName?: string;
}): Promise<AuthResponse> {
  const res = await authClient.post('/auth/register', input);
  const data = unwrap<AuthResponse>(res.data);
  storeAuthResponse(data);
  return data;
}

export async function refresh(): Promise<string | null> {
  const refreshToken = getRefreshToken();
  if (!refreshToken) return null;
  try {
    const res = await authClient.post('/auth/refresh', { refreshToken });
    const data = unwrap<AuthResponse>(res.data);
    storeAuthResponse(data);
    return data.accessToken;
  } catch {
    clearAuth();
    return null;
  }
}

export async function logout(): Promise<void> {
  const refreshToken = getRefreshToken();
  if (refreshToken) {
    try {
      await authClient.post('/auth/logout', { refreshToken });
    } catch {
      // Best-effort — clear locally even if the server call fails.
    }
  }
  clearAuth();
}

export async function getCurrentUser(): Promise<CurrentUser> {
  const res = await authClient.get('/auth/current', {
    headers: { Authorization: `Bearer ${getAccessToken() ?? ''}` },
  });
  return unwrap<CurrentUser>(res.data);
}

// ── Social login (Google / Facebook) ─────────────────────────────────────────

interface StartResponse { authorizeUrl: string; state: string }

export async function startExternalLogin(provider: Provider): Promise<void> {
  const redirectUri = `${window.location.origin}/auth/callback`;
  const res = await authClient.post(`/auth/external/${provider}/start`, { redirectUri });
  const { authorizeUrl } = unwrap<StartResponse>(res.data);
  sessionStorage.setItem('golf-league-oauth-provider', provider);
  sessionStorage.setItem('golf-league-oauth-redirect-uri', redirectUri);
  window.location.assign(authorizeUrl);
}

export async function completeExternalLogin(state: string, code: string): Promise<AuthResponse> {
  const provider = sessionStorage.getItem('golf-league-oauth-provider') as Provider | null;
  const redirectUri = sessionStorage.getItem('golf-league-oauth-redirect-uri');
  sessionStorage.removeItem('golf-league-oauth-provider');
  sessionStorage.removeItem('golf-league-oauth-redirect-uri');

  if (!provider || !redirectUri) {
    throw new Error('Missing OAuth flow state. Start sign-in again.');
  }

  const res = await authClient.post(`/auth/external/${provider}/callback`, {
    state, code, redirectUri,
  });
  const data = unwrap<AuthResponse>(res.data);
  storeAuthResponse(data);
  return data;
}

// ── MFA (TOTP) ───────────────────────────────────────────────────────────────

export async function verifyTotpForLogin(mfaToken: string, code: string): Promise<AuthResponse> {
  const res = await authClient.post('/auth/mfa/totp/verify', { mfaToken, code });
  const data = unwrap<AuthResponse>(res.data);
  storeAuthResponse(data);
  return data;
}

export async function startTotpEnrollment(): Promise<{ secret: string; otpAuthUri: string }> {
  const res = await authClient.post('/auth/mfa/totp/enroll', null, {
    headers: { Authorization: `Bearer ${getAccessToken() ?? ''}` },
  });
  return unwrap(res.data);
}

export async function verifyTotpEnrollment(code: string): Promise<void> {
  await authClient.post('/auth/mfa/totp/verify-enrollment', { code }, {
    headers: { Authorization: `Bearer ${getAccessToken() ?? ''}` },
  });
}

// ── Password reset ───────────────────────────────────────────────────────────

export async function requestPasswordReset(email: string): Promise<void> {
  await authClient.post('/auth/password-reset/request', { email });
}

export async function confirmPasswordReset(input: {
  email: string;
  token: string;
  newPassword: string;
}): Promise<void> {
  await authClient.post('/auth/password-reset/confirm', input);
}
