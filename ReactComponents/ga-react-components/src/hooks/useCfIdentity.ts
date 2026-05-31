// useCfIdentity — React hook that surfaces Cloudflare Access identity.
//
// Cloudflare Access (when configured on demos.guitaralchemist.com) serves
// the signed-in user's identity at /cdn-cgi/access/get-identity. The
// endpoint returns 200 + { email, name, ... } when the operator has a
// valid session cookie, and 401 otherwise.
//
// In local dev (no CF in front), the Vite middleware in vite.config.ts
// stubs the same path with { email: 'dev@localhost', name: 'Local Dev' }
// so the auth-aware UI keeps working when running `npm run dev`.
//
// Usage:
//   const { authed, identity, loading, signInUrl } = useCfIdentity();
//   if (!authed) return <a href={signInUrl}>Sign in</a>;
//
// The hook polls every 5 minutes to catch session expiry without forcing
// the operator to refresh. A 401 response is NOT an error — it's the
// normal "not signed in" state.
//
// See docs/runbooks/cf-access-dashboard.md for the operator setup.

import { useCallback, useEffect, useMemo, useState } from 'react';

export interface CfIdentity {
  email: string;
  name?: string;
  /** Cloudflare Access user id, or 'local-dev-stub' in dev. */
  id?: string;
  /** 'local-dev-stub' marks the dev middleware stub; useful for diagnostics. */
  type?: string;
}

export interface UseCfIdentityReturn {
  identity: CfIdentity | null;
  authed: boolean;
  loading: boolean;
  /** URL to send the operator to for sign-in (CF Access login + redirect back). */
  signInUrl: string;
  /** Re-fetch identity now. Useful after manual sign-in / sign-out. */
  refresh: () => void;
}

const IDENTITY_PATH = '/cdn-cgi/access/get-identity';
const REFRESH_INTERVAL_MS = 5 * 60 * 1000; // 5 min

/**
 * Build the Cloudflare Access login URL for the current host, redirecting
 * back to the page the operator is currently on. CF will replace the
 * cookie and bounce back through `redirect_url`.
 *
 * In dev (localhost), CF isn't in front — clicking the link will 404. The
 * AuthChip surface handles that case by hiding/disabling the sign-in link
 * when the stub identity is already returned.
 */
function buildSignInUrl(): string {
  if (typeof window === 'undefined') return '#';
  const { hostname, href } = window.location;
  // CF Access login path is /cdn-cgi/access/login/<hostname>?redirect_url=<encoded>
  return `/cdn-cgi/access/login/${hostname}?redirect_url=${encodeURIComponent(href)}`;
}

export function useCfIdentity(): UseCfIdentityReturn {
  const [identity, setIdentity] = useState<CfIdentity | null>(null);
  const [loading, setLoading] = useState<boolean>(true);
  const [tick, setTick] = useState<number>(0);

  const load = useCallback(async () => {
    try {
      const resp = await fetch(IDENTITY_PATH, {
        credentials: 'include',
        cache: 'no-store',
        headers: { Accept: 'application/json' },
      });
      if (!resp.ok) {
        // 401 / 403 / 404 all mean "not signed in" for our purposes.
        // 404 happens when CF Access isn't configured on the zone yet —
        // the runbook covers enabling it. Treat as "unauthenticated".
        setIdentity(null);
        return;
      }
      const body = (await resp.json()) as Partial<CfIdentity> | null;
      if (body && typeof body.email === 'string') {
        setIdentity({
          email: body.email,
          name: body.name,
          id: body.id,
          type: body.type,
        });
      } else {
        setIdentity(null);
      }
    } catch {
      // Network failure — surface as unauthenticated. The button will be
      // disabled; operator can retry by clicking refresh or reloading.
      setIdentity(null);
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    let cancelled = false;
    void (async () => {
      await load();
      if (cancelled) return;
    })();
    const handle = window.setInterval(() => setTick((t) => t + 1), REFRESH_INTERVAL_MS);
    return () => {
      cancelled = true;
      window.clearInterval(handle);
    };
  }, [load]);

  // Re-run load() when tick changes (from interval) or `refresh` callback fires.
  useEffect(() => {
    if (tick === 0) return; // initial load already ran in the mount effect
    void load();
  }, [tick, load]);

  const signInUrl = useMemo(() => buildSignInUrl(), []);

  return {
    identity,
    authed: identity != null,
    loading,
    signInUrl,
    refresh: () => setTick((t) => t + 1),
  };
}

export default useCfIdentity;
