import axios, {
  AxiosError,
  AxiosHeaders,
  InternalAxiosRequestConfig,
} from 'axios';
import { getAccessToken, isTokenExpired, refresh, clearAuth } from './auth';

// Module-level navigator — set once by NavigatorInjector in App.tsx so the
// interceptor can do a soft React Router redirect instead of a hard reload.
let _navigate: ((path: string) => void) | null = null;
export function setNavigator(fn: (path: string) => void) {
  _navigate = fn;
}

const BASE_URL =
  (import.meta.env.VITE_API_BASE_URL as string | undefined) ?? '/api/v1';

export const apiClient = axios.create({
  baseURL: BASE_URL,
  headers: {
    'Content-Type': 'application/json',
  },
});

// Alias for legacy imports: import { api } from '@/lib/api'
export const api = apiClient;

// ── Request interceptor ────────────────────────────────────────────────────────
// Attach the access token. If the stored token is within 60s of expiry,
// proactively refresh it first so we never send an already-expired token.
apiClient.interceptors.request.use(
  async (config: InternalAxiosRequestConfig) => {
    let token = getAccessToken();
    if (token && isTokenExpired()) {
      token = await refresh();
    }
    if (!token) return config;
    const headers = config.headers ?? new AxiosHeaders();
    (headers as AxiosHeaders).set('Authorization', `Bearer ${token}`);
    config.headers = headers as InternalAxiosRequestConfig['headers'];
    return config;
  },
  (error: unknown) => Promise.reject(error),
);

// ── Response interceptor ──────────────────────────────────────────────────────
// On 401: try a one-time refresh, then either retry the original request or
// boot the user back to /login. We mark the retried request so we don't loop
// if the retry itself returns 401.
let pendingRefresh: Promise<string | null> | null = null;

apiClient.interceptors.response.use(
  (response) => response,
  async (error: AxiosError) => {
    const status = error.response?.status;
    const config = error.config as InternalAxiosRequestConfig & { _retry?: boolean };

    if (status !== 401 || !config || config._retry) {
      return Promise.reject(error);
    }

    config._retry = true;

    if (!pendingRefresh) {
      pendingRefresh = refresh().finally(() => {
        pendingRefresh = null;
      });
    }
    const newToken = await pendingRefresh;

    if (newToken) {
      const headers = config.headers ?? new AxiosHeaders();
      (headers as AxiosHeaders).set('Authorization', `Bearer ${newToken}`);
      config.headers = headers as InternalAxiosRequestConfig['headers'];
      return apiClient.request(config);
    }

    // Refresh failed — sign the user out and redirect, unless we're already
    // on a public auth page. With path-based league routing, paths are
    // /:leagueSlug/login etc. — match the segment after the slug.
    clearAuth();
    const path = window.location.pathname;
    const publicAuthSegments = ['login', 'register', 'accept-invite', 'auth'];
    const pathSegments = path.split('/').filter(Boolean);
    // pathSegments[0] = leagueSlug, pathSegments[1] = page
    const onPublicAuthPage = pathSegments.length >= 2
      && publicAuthSegments.some((s) => pathSegments[1] === s || pathSegments[1]?.startsWith(s));
    if (!onPublicAuthPage) {
      // Navigate to /:leagueSlug/login, preserving the current league slug
      const slug = pathSegments[0] ?? '';
      const loginPath = slug ? `/${slug}/login` : '/login';
      if (_navigate) {
        _navigate(loginPath);
      } else {
        window.location.href = loginPath;
      }
    }
    return Promise.reject(error);
  },
);
