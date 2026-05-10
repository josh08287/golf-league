import axios, {
  AxiosError,
  AxiosHeaders,
  InternalAxiosRequestConfig,
} from 'axios';
import { getAccessToken, refresh, clearAuth } from './auth';

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

    // Refresh failed — sign the user out and redirect (unless we're already
    // on the login page, to avoid a navigation loop).
    clearAuth();
    if (window.location.pathname !== '/login') {
      window.location.href = '/login';
    }
    return Promise.reject(error);
  },
);
