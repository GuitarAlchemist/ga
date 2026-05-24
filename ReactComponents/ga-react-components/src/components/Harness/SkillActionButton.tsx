// SkillActionButton — POSTs to /actions/harness/skill/<name> to queue
// a skill invocation. The action middleware appends a line to
// state/harness/skill-invocations.jsonl (gitignored). The button itself
// doesn't execute the skill — agents read the queue on their own beat.
//
// AUTH: In production this surface is fronted by Cloudflare Access; the
// path /actions/* is gated to the operator's email. The button polls
// /cdn-cgi/access/get-identity via useCfIdentity() and disables itself
// with a sign-in CTA when no identity is returned (401 or 404 when CF
// Access isn't configured yet). See docs/runbooks/cf-access-dashboard.md.
//
// In local dev the Vite middleware stubs the identity endpoint so the
// button stays enabled — operators don't need a CF cookie to dogfood
// locally.
//
// UX: shows a snackbar/alert on success or failure. Disabled while in
// flight to prevent double-click duplicate queue entries.

import React, { useState } from 'react';
import { Alert, Button, ButtonProps, Snackbar, Tooltip } from '@mui/material';
import PlayArrowIcon from '@mui/icons-material/PlayArrow';
import LockOutlinedIcon from '@mui/icons-material/LockOutlined';
import { useCfIdentity } from '../../hooks/useCfIdentity';

interface Props {
  skill: string;
  /** Optional label override; defaults to `/${skill}`. */
  label?: string;
  /** Optional context string (item title etc.) sent in POST body for traceability. */
  context?: string;
  /** Optional harness item number for cross-reference in the queue file. */
  itemNumber?: number;
  /** Style overrides. */
  variant?: ButtonProps['variant'];
  size?: ButtonProps['size'];
  /** A short tooltip explaining what queueing means. */
  tooltip?: string;
}

export const SkillActionButton: React.FC<Props> = ({
  skill,
  label,
  context,
  itemNumber,
  variant = 'outlined',
  size = 'small',
  tooltip,
}) => {
  const [busy, setBusy] = useState(false);
  const [snack, setSnack] = useState<{ severity: 'success' | 'error' | 'info'; message: string } | null>(null);
  const { authed, loading: authLoading, signInUrl, identity } = useCfIdentity();

  const handleClick = async (e: React.MouseEvent) => {
    e.stopPropagation();
    // Not signed in: redirect to CF Access login. CF will bounce back
    // here after the operator completes the PIN / OAuth flow.
    if (!authed) {
      window.location.href = signInUrl;
      return;
    }
    setBusy(true);
    try {
      const resp = await fetch(`/actions/harness/skill/${encodeURIComponent(skill)}`, {
        method: 'POST',
        credentials: 'include',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          source: 'harness-tab',
          context: context ?? null,
          item_number: itemNumber ?? null,
          // Echoing operator identity helps the queue file attribute
          // who fired what; the server doesn't trust this value, it's
          // a UX breadcrumb. Real auth is CF Access on /actions/*.
          actor_email: identity?.email ?? null,
        }),
      });
      const body = await resp.json().catch(() => ({}));
      if (resp.status === 401 || resp.status === 403) {
        // CF Access kicked us out — session likely expired. Send to login.
        setSnack({
          severity: 'error',
          message: 'Sign-in required. Redirecting to Cloudflare Access…',
        });
        window.setTimeout(() => { window.location.href = signInUrl; }, 1200);
        return;
      }
      if (!resp.ok) {
        setSnack({
          severity: 'error',
          message: `Queue failed (HTTP ${resp.status}): ${body?.error ?? 'unknown'}`,
        });
      } else {
        setSnack({
          severity: 'success',
          message: body?.message ?? `Queued /${skill} — agents will pick this up.`,
        });
      }
    } catch (err) {
      setSnack({
        severity: 'error',
        message: `Network error queueing /${skill}: ${String((err as Error).message ?? err)}`,
      });
    } finally {
      setBusy(false);
    }
  };

  // Visual: lock icon + muted styling when not authed; play arrow otherwise.
  // We keep the button enabled even when !authed so clicking it routes to
  // sign-in (better discoverability than a dead disabled control).
  const startIcon = authed
    ? <PlayArrowIcon sx={{ fontSize: 16 }} />
    : <LockOutlinedIcon sx={{ fontSize: 14 }} />;

  const effectiveTooltip = authLoading
    ? 'Checking sign-in…'
    : !authed
      ? 'Sign in via Cloudflare Access to run actions'
      : tooltip;

  const button = (
    <Button
      size={size}
      variant={variant}
      startIcon={startIcon}
      onClick={handleClick}
      disabled={busy || authLoading}
      aria-label={!authed ? `${label ?? skill} — sign in required` : (label ?? skill)}
      data-authed={authed ? 'true' : 'false'}
      sx={{
        textTransform: 'none',
        fontSize: '0.75rem',
        py: 0.25,
        opacity: !authed ? 0.7 : 1,
      }}
    >
      {label ?? `/${skill}`}
    </Button>
  );

  return (
    <>
      {effectiveTooltip ? <Tooltip title={effectiveTooltip} arrow>{button}</Tooltip> : button}
      <Snackbar
        open={snack != null}
        autoHideDuration={5000}
        onClose={() => setSnack(null)}
        anchorOrigin={{ vertical: 'bottom', horizontal: 'center' }}
      >
        {snack ? (
          <Alert
            severity={snack.severity}
            variant="filled"
            onClose={() => setSnack(null)}
            sx={{ width: '100%', maxWidth: 520 }}
          >
            {snack.message}
          </Alert>
        ) : undefined}
      </Snackbar>
    </>
  );
};

export default SkillActionButton;
