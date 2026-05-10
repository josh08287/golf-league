import axios, {
  AxiosError,
  AxiosHeaders,
  InternalAxiosRequestConfig,
} from 'axios';
import { msalInstance, tokenRequest } from './msalConfig';

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
// Attach a Bearer token if an account is signed in.
apiClient.interceptors.request.use(
  async (config: InternalAxiosRequestConfig) => {
    const accounts = msalInstance.getAllAccounts();
    if (accounts.length === 0) return config;

    try {
      const result = await msalInstance.acquireTokenSilent({
        ...tokenRequest,
        account: accounts[0],
      });

      const headers = config.headers ?? new AxiosHeaders();
      (headers as AxiosHeaders).set('Authorization', `Bearer ${result.accessToken}`);
      config.headers = headers as InternalAxiosRequestConfig['headers'];
    } catch {
      // Token refresh failed — let the request through unauthenticated;
      // the 401 response interceptor will handle the redirect.
    }

    return config;
  },
  (error: unknown) => Promise.reject(error),
);

// ── Response interceptor ──────────────────────────────────────────────────────
// On 401: attempt a final silent refresh; if that also fails, redirect to login.
apiClient.interceptors.response.use(
  (response) => response,
  async (error: AxiosError) => {
    if (error.response?.status !== 401) return Promise.reject(error);

    const accounts = msalInstance.getAllAccounts();
    if (accounts.length > 0) {
      try {
        await msalInstance.acquireTokenSilent({
          ...tokenRequest,
          account: accounts[0],
          forceRefresh: true,
        });
        // Retry the original request once.
        return apiClient.request(error.config!);
      } catch {
        // Silent refresh failed — fall through to interactive login.
      }
    }

    // Don't redirect if already on login page to prevent loops
    if (window.location.pathname === '/login') {
      return Promise.reject(error);
    }

    await msalInstance.loginRedirect(tokenRequest);
    return Promise.reject(error);
  },
);
