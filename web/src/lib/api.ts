import axios, {
  AxiosError,
  AxiosHeaders,
  InternalAxiosRequestConfig,
} from 'axios';
import { getAccessToken, refresh, clearAuth } from './auth';

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
// Attach the access token if we have one in local storage.
apiClient.interceptors.request.use(
  (config: InternalAxiosRequestConfig) => {
    const token = getAccessToken();
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
    // on a public auth page. Public auth pages (login, register, password
    // reset, social callback, MFA, invite acceptance) must never trigger an
    // auth redirect from a background 401, or the user gets bounced mid-flow.
    clearAuth();
    const path = window.location.pathname;
    const publicAuthPaths = ['/login', '/register', '/accept-invite', '/auth/'];
    const onPublicAuthPage = publicAuthPaths.some((p) =>
      p.endsWith('/') ? path.startsWith(p) : path === p,
    );
    if (!onPublicAuthPage) {
      if (_navigate) {
        _navigate('/login');
      } else {
        window.location.href = '/login';
      }
    }
    return Promise.reject(error);
  },
);
