import React, { createContext, useCallback, useContext, useEffect, useMemo, useState } from 'react';
import { fetchMe, login as apiLogin, logout as apiLogout } from '../api/endpoints';
import { registerTokenListener, setTokens } from '../api/client';
import type { Me } from '../api/types';
import { clearTokens, loadTokens, saveTokens } from './storage';

interface AuthState {
  me: Me | null;
  loading: boolean;
  signIn: (email: string, password: string) => Promise<void>;
  signOut: () => Promise<void>;
  /** Latest refresh token, exposed so logout can revoke it server-side. */
  getRefreshToken: () => string | null;
}

const AuthContext = createContext<AuthState | undefined>(undefined);

export function AuthProvider({ children }: { children: React.ReactNode }) {
  const [me, setMe] = useState<Me | null>(null);
  const [loading, setLoading] = useState(true);
  const [refreshToken, setRefreshToken] = useState<string | null>(null);

  // Keep SecureStore in sync whenever the client rotates tokens (silent 401 refresh).
  useEffect(() => {
    registerTokenListener((tokens) => {
      if (tokens) {
        setRefreshToken(tokens.refreshToken);
        void saveTokens(tokens);
      } else {
        setRefreshToken(null);
        void clearTokens();
        setMe(null);
      }
    });
  }, []);

  // Restore a session on cold start: load tokens, prime the client, fetch the profile.
  useEffect(() => {
    (async () => {
      try {
        const tokens = await loadTokens();
        if (tokens) {
          setTokens(tokens);
          setRefreshToken(tokens.refreshToken);
          const profile = await fetchMe();
          setMe(profile);
        }
      } catch {
        await clearTokens();
        setTokens(null);
        setMe(null);
      } finally {
        setLoading(false);
      }
    })();
  }, []);

  const signIn = useCallback(async (email: string, password: string) => {
    const res = await apiLogin(email, password);
    const tokens = { accessToken: res.accessToken, refreshToken: res.refreshToken };
    setTokens(tokens);
    setRefreshToken(res.refreshToken);
    await saveTokens(tokens);
    setMe(res.user);
  }, []);

  const signOut = useCallback(async () => {
    const rt = refreshToken;
    setMe(null);
    setTokens(null);
    setRefreshToken(null);
    await clearTokens();
    if (rt) {
      try {
        await apiLogout(rt);
      } catch {
        // Best-effort revoke; tokens are already cleared locally.
      }
    }
  }, [refreshToken]);

  const value = useMemo<AuthState>(
    () => ({ me, loading, signIn, signOut, getRefreshToken: () => refreshToken }),
    [me, loading, signIn, signOut, refreshToken],
  );

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

export function useAuth(): AuthState {
  const ctx = useContext(AuthContext);
  if (!ctx) throw new Error('useAuth must be used within AuthProvider');
  return ctx;
}
