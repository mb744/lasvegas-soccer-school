import React, { createContext, useCallback, useContext, useEffect, useMemo, useRef, useState } from 'react';
import { AppState } from 'react-native';
import { fetchMe, login as apiLogin, logout as apiLogout } from '../api/endpoints';
import { registerTokenListener, setTokens } from '../api/client';
import type { Me, TokenResponse } from '../api/types';
import { clearTokens, loadTokens, saveTokens } from './storage';
import { checkIn } from '../push/register';

/** How often a foregrounded app re-checks in (launches and sign-ins always do). */
const CHECK_IN_INTERVAL_MS = 60 * 60 * 1000;

interface AuthState {
  me: Me | null;
  loading: boolean;
  signIn: (email: string, password: string) => Promise<void>;
  /** Adopts a token pair the caller already obtained (e.g. from a Google/Facebook exchange). */
  signInWithTokens: (res: TokenResponse) => Promise<void>;
  signOut: () => Promise<void>;
  /** Re-fetches the profile, e.g. after joining or leaving a family. */
  refreshMe: () => Promise<void>;
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

  // Check in whenever a login becomes active (sign-in or restored session), and again when the app
  // returns to the foreground after a while. Keeps "Last seen" current and registers push.
  const lastCheckIn = useRef(0);
  const userId = me?.userId;
  useEffect(() => {
    if (!userId) return;
    lastCheckIn.current = Date.now();
    void checkIn({ askPermission: true });
    const sub = AppState.addEventListener('change', (state) => {
      if (state === 'active' && Date.now() - lastCheckIn.current > CHECK_IN_INTERVAL_MS) {
        lastCheckIn.current = Date.now();
        void checkIn();
      }
    });
    return () => sub.remove();
  }, [userId]);

  const adoptTokens = useCallback(async (res: TokenResponse) => {
    const tokens = { accessToken: res.accessToken, refreshToken: res.refreshToken };
    setTokens(tokens);
    setRefreshToken(res.refreshToken);
    await saveTokens(tokens);
    setMe(res.user);
  }, []);

  const signIn = useCallback(async (email: string, password: string) => {
    const res = await apiLogin(email, password);
    await adoptTokens(res);
  }, [adoptTokens]);

  const signInWithTokens = useCallback(async (res: TokenResponse) => {
    await adoptTokens(res);
  }, [adoptTokens]);

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

  const refreshMe = useCallback(async () => {
    try {
      setMe(await fetchMe());
    } catch {
      // Keep the current profile; the next launch refreshes it.
    }
  }, []);

  const value = useMemo<AuthState>(
    () => ({ me, loading, signIn, signInWithTokens, signOut, refreshMe, getRefreshToken: () => refreshToken }),
    [me, loading, signIn, signInWithTokens, signOut, refreshMe, refreshToken],
  );

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

export function useAuth(): AuthState {
  const ctx = useContext(AuthContext);
  if (!ctx) throw new Error('useAuth must be used within AuthProvider');
  return ctx;
}
