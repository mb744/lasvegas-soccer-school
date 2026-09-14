import axios, { AxiosError, AxiosInstance, InternalAxiosRequestConfig } from 'axios';
import { API_URL } from '../config';
import type { TokenResponse } from './types';

/**
 * Single axios instance for the whole app. It attaches the current access token to every request
 * and, on a 401, transparently refreshes using the refresh token and replays the request once.
 * AuthContext owns the tokens and registers the handlers below — keeping token state in one place
 * while the client stays a dumb transport.
 */

let accessToken: string | null = null;
let refreshToken: string | null = null;

// Called by AuthContext when a refresh produces new tokens, and when the session ends.
let onTokens: ((tokens: { accessToken: string; refreshToken: string } | null) => void) | null = null;

export function setTokens(tokens: { accessToken: string; refreshToken: string } | null) {
  accessToken = tokens?.accessToken ?? null;
  refreshToken = tokens?.refreshToken ?? null;
}

export function registerTokenListener(cb: (t: { accessToken: string; refreshToken: string } | null) => void) {
  onTokens = cb;
}

/** Current access token — used by the SignalR hub's accessTokenFactory. */
export function getAccessToken(): string | null {
  return accessToken;
}

export const api: AxiosInstance = axios.create({
  baseURL: API_URL,
  timeout: 20000,
});

api.interceptors.request.use((config: InternalAxiosRequestConfig) => {
  if (accessToken) {
    config.headers.Authorization = `Bearer ${accessToken}`;
  }
  return config;
});

// A bare client (no interceptors) for the refresh call itself, so a 401 there can't recurse.
const bare = axios.create({ baseURL: API_URL, timeout: 20000 });

// Coalesce concurrent refreshes: if several requests 401 at once, only one refresh runs.
let refreshing: Promise<string | null> | null = null;

async function refreshAccessToken(): Promise<string | null> {
  if (!refreshToken) return null;
  if (!refreshing) {
    refreshing = (async () => {
      try {
        const { data } = await bare.post<TokenResponse>('/mobile/auth/refresh', { refreshToken });
        const next = { accessToken: data.accessToken, refreshToken: data.refreshToken };
        setTokens(next);
        onTokens?.(next);
        return data.accessToken;
      } catch {
        setTokens(null);
        onTokens?.(null);
        return null;
      } finally {
        refreshing = null;
      }
    })();
  }
  return refreshing;
}

api.interceptors.response.use(
  (res) => res,
  async (error: AxiosError) => {
    const original = error.config as (InternalAxiosRequestConfig & { _retried?: boolean }) | undefined;
    if (error.response?.status === 401 && original && !original._retried && refreshToken) {
      original._retried = true;
      const fresh = await refreshAccessToken();
      if (fresh) {
        original.headers.Authorization = `Bearer ${fresh}`;
        return api(original);
      }
    }
    return Promise.reject(error);
  },
);
