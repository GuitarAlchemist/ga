// src/components/PrimeRadiant/AuthContext.tsx
// OAuth2/JWT authentication context for Prime Radiant.
//
// The backend (ga-server) owns the OAuth dance; this client holds the
// issued access_token in sessionStorage, exposes current user + roles
// to React, and refreshes the token when it expires via the httpOnly
// refresh cookie (POST /api/auth/refresh).
//
// On first mount we check the URL fragment (#access_token=…) left by
// the backend's OAuth callback, stash it, and clean the URL.

import { createContext, useCallback, useContext, useEffect, useMemo, useState } from 'react';

const TOKEN_KEY = 'ga_jwt';
const TOKEN_EXP_KEY = 'ga_jwt_exp';

export interface AuthUser {
  id: string;
  email: string;
  name: string;
  avatarUrl?: string | null;
  provider: 'google' | 'github' | string;
  roles: string[];
}

export interface AuthState {
  user: AuthUser | null;
  token: string | null;
  isAuthenticated: boolean;
  isAdmin: boolean;
  login: (provider: 'google' | 'github') => void;
  logout: () => Promise<void>;
  authFetch: (input: RequestInfo | URL, init?: RequestInit) => Promise<Response>;
}

const AuthContext = createContext<AuthState | null>(null);

/** Read a persisted token — returns null if expired. */
function readStoredToken(): { token: string; expiresAt: number } | null {
  try {
    const token = sessionStorage.getItem(TOKEN_KEY);
    const expRaw = sessionStorage.getItem(TOKEN_EXP_KEY);
    if (!token || !expRaw) return null;
    const expiresAt = parseInt(expRaw, 10);
    if (!Number.isFinite(expiresAt) || Date.now() >= expiresAt) return null;
    return { token, expiresAt };
  } catch {
    return null;
  }
}

function writeStoredToken(token: string, expiresInSec: number): void {
  try {
    sessionStorage.setItem(TOKEN_KEY, token);
    sessionStorage.setItem(TOKEN_EXP_KEY, String(Date.now() + expiresInSec * 1000));
  } catch { /* ignore quota errors */ }
}

function clearStoredToken(): void {
  try {
    sessionStorage.removeItem(TOKEN_KEY);
    sessionStorage.removeItem(TOKEN_EXP_KEY);
  } catch { /* ignore */ }
}

/** Extract #access_token=…&expires_in=… from the URL, return null if absent. */
function extractTokenFromFragment(): { token: string; expiresIn: number } | null {
  if (typeof window === 'undefined' || !window.location.hash) return null;
  const hash = window.location.hash.replace(/^#/, '');
  const params = new URLSearchParams(hash);
  const token = params.get('access_token');
  const expiresIn = parseInt(params.get('expires_in') ?? '0', 10);
  if (!token || !Number.isFinite(expiresIn) || expiresIn <= 0) return null;
  // Clean the URL — remove the fragment
  const clean = window.location.pathname + window.location.search;
  window.history.replaceState(null, '', clean);
  return { token, expiresIn };
}

export function AuthProvider({ children }: { children: React.ReactNode }): JSX.Element {
  const [token, setToken] = useState<string | null>(null);
  const [user, setUser] = useState<AuthUser | null>(null);

  // On mount: consume URL fragment, restore session, fetch /me
  useEffect(() => {
    const fragToken = extractTokenFromFragment();
    if (fragToken) {
      writeStoredToken(fragToken.token, fragToken.expiresIn);
      setToken(fragToken.token);
      return;
    }
    const stored = readStoredToken();
    if (stored) setToken(stored.token);
  }, []);

  // Fetch /api/auth/me whenever token changes
  useEffect(() => {
    if (!token) { setUser(null); return; }
    let cancelled = false;
    (async () => {
      try {
        const res = await fetch('/api/auth/me', {
          headers: { Authorization: `Bearer ${token}` },
          credentials: 'include',
        });
        if (!res.ok) { if (!cancelled) { setUser(null); setToken(null); clearStoredToken(); } return; }
        const json = await res.json();
        if (!cancelled) setUser(json as AuthUser);
      } catch {
        if (!cancelled) setUser(null);
      }
    })();
    return () => { cancelled = true; };
  }, [token]);

  const login = useCallback((provider: 'google' | 'github') => {
    const returnUrl = window.location.origin + window.location.pathname + window.location.search;
    const challengeUrl = `/api/auth/challenge/${provider}?returnUrl=${encodeURIComponent(returnUrl)}`;
    window.location.href = challengeUrl;
  }, []);

  const logout = useCallback(async () => {
    try {
      await fetch('/api/auth/logout', { method: 'POST', credentials: 'include' });
    } catch { /* best effort */ }
    clearStoredToken();
    setToken(null);
    setUser(null);
  }, []);

  // Silently refresh and retry once on 401 — uses the httpOnly refresh cookie.
  const refreshToken = useCallback(async (): Promise<string | null> => {
    try {
      const res = await fetch('/api/auth/refresh', { method: 'POST', credentials: 'include' });
      if (!res.ok) return null;
      const json = await res.json() as { access_token: string; expires_in: number };
      writeStoredToken(json.access_token, json.expires_in);
      setToken(json.access_token);
      return json.access_token;
    } catch {
      return null;
    }
  }, []);

  const authFetch = useCallback(async (input: RequestInfo | URL, init?: RequestInit): Promise<Response> => {
    const current = token ?? sessionStorage.getItem(TOKEN_KEY);
    const headers = new Headers(init?.headers);
    if (current) headers.set('Authorization', `Bearer ${current}`);
    const res = await fetch(input, { ...init, headers, credentials: init?.credentials ?? 'include' });
    if (res.status !== 401) return res;
    // Try refresh + one retry
    const fresh = await refreshToken();
    if (!fresh) return res;
    const retryHeaders = new Headers(init?.headers);
    retryHeaders.set('Authorization', `Bearer ${fresh}`);
    return fetch(input, { ...init, headers: retryHeaders, credentials: init?.credentials ?? 'include' });
  }, [token, refreshToken]);

  const value = useMemo<AuthState>(() => ({
    user,
    token,
    isAuthenticated: !!token && !!user,
    isAdmin: !!user?.roles?.includes('admin'),
    login,
    logout,
    authFetch,
  }), [user, token, login, logout, authFetch]);

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

export function useAuth(): AuthState {
  const ctx = useContext(AuthContext);
  if (!ctx) throw new Error('useAuth must be used within <AuthProvider>');
  return ctx;
}
