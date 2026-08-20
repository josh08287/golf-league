import axios, {
  AxiosError,
  AxiosHeaders,
  InternalAxiosRequestConfig,
} from 'axios';
import { getAccessToken, isTokenExpired, refresh, clearAuth } from './auth';
import { useAuthStore } from '@/store/authStore';
import { useActiveLeagueStore } from '@/store/activeLeagueStore';

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

// ── Shared refresh lock ──────────────────────────────────────────────────────
// Refresh tokens are single-use (rotated server-side on every call), so if
// two requests each call refresh() around the same moment, the second one
// gets rejected — since the first already burned the stored refresh token —
// and that failure wipes the session, even though the first refresh
// succeeded. Both the proactive (request interceptor) and reactive (401
// response interceptor) refresh paths must share one in-flight promise so
// concurrent callers all await the same refresh instead of racing.
let pendingRefresh: Promise<string | null> | null = null;
function refreshOnce(): Promise<string | null> {
  if (!pendingRefresh) {
    pendingRefresh = refresh().finally(() => {
      pendingRefresh = null;
    });
  }
  return pendingRefresh;
}

// ── Request interceptor ────────────────────────────────────────────────────────
// Attach the access token. If the stored token is within 60s of expiry,
// proactively refresh it first so we never send an already-expired token.
apiClient.interceptors.request.use(
  async (config: InternalAxiosRequestConfig) => {
    const headers = config.headers ?? new AxiosHeaders();
    let token = getAccessToken();
    if (token && isTokenExpired()) {
      token = await refreshOnce();
    }
    if (token) {
      (headers as AxiosHeaders).set('Authorization', `Bearer ${token}`);
    }
    config.headers = headers as InternalAxiosRequestConfig['headers'];
    return config;
  },
  (error: unknown) => Promise.reject(error),
);

// ── Response interceptor ──────────────────────────────────────────────────────
// On 401: try a one-time refresh, then either retry the original request or
// boot the user back to /login. We mark the retried request so we don't loop
// if the retry itself returns 401.

// Track whether we've already kicked off a logout navigation so multiple
// concurrent 401s from failing requests don't each trigger a separate redirect.
let redirectingToLogin = false;

apiClient.interceptors.response.use(
  (response) => response,
  async (error: AxiosError) => {
    const status = error.response?.status;
    const config = error.config as InternalAxiosRequestConfig & { _retry?: boolean };

    if (status !== 401 || !config || config._retry) {
      return Promise.reject(error);
    }

    config._retry = true;

    const newToken = await refreshOnce();

    if (newToken) {
      const headers = config.headers ?? new AxiosHeaders();
      (headers as AxiosHeaders).set('Authorization', `Bearer ${newToken}`);
      config.headers = headers as InternalAxiosRequestConfig['headers'];
      return apiClient.request(config);
    }

    // Refresh failed — sign the user out and redirect to /login unless already there.
    // Guard against multiple simultaneous 401s all trying to redirect.
    if (redirectingToLogin) return Promise.reject(error);
    redirectingToLogin = true;

    clearAuth();
    useAuthStore.getState().clearUser();
    useActiveLeagueStore.getState().setActiveLeague(null);
    const path = window.location.pathname;
    const publicPaths = ['/login', '/register', '/accept-invite', '/auth/'];
    const onPublicPage = publicPaths.some((p) => path === p || path.startsWith(p));
    if (!onPublicPage) {
      // Always use the React Router navigator if available. If it's not set
      // yet (race during initial mount), wait one tick for effects to flush
      // before falling back to a hard reload.
      if (_navigate) {
        redirectingToLogin = false;
        _navigate('/login');
      } else {
        // Delay slightly so NavigatorInjector's useEffect has time to run.
        setTimeout(() => {
          redirectingToLogin = false;
          if (_navigate) {
            _navigate('/login');
          } else {
            window.location.href = '/login';
          }
        }, 50);
      }
    } else {
      redirectingToLogin = false;
    }
    return Promise.reject(error);
  },
);
