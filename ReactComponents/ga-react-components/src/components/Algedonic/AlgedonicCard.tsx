// AlgedonicCard — Heartbeat tile for the VSM algedonic-channel inbox.
//
// Reads /dev-data/algedonic (projected from state/algedonic/inbox.jsonl per the
// contract at docs/contracts/2026-05-24-algedonic-channel.contract.md).
//
// Renders:
//   - Severity-tier badges (info / warn / fail / critical) with counts
//   - Top-3 unacked signals with summary + evidence link + Ack button
//   - Auto-refresh every 30s
//
// The red critical banner is rendered at the OverviewSection level (above the
// existing Heartbeat) when projection.has_critical is true. This component is
// the always-visible tile.

import React, { useCallback, useEffect, useState } from 'react';
import {
  Alert,
  Box,
  Button,
  Chip,
  IconButton,
  Link,
  Paper,
  Stack,
  Tooltip,
  Typography,
} from '@mui/material';
import CheckCircleOutlineIcon from '@mui/icons-material/CheckCircleOutline';
import OpenInNewIcon from '@mui/icons-material/OpenInNew';
import NotificationsActiveIcon from '@mui/icons-material/NotificationsActive';
import RefreshIcon from '@mui/icons-material/Refresh';
import LockOutlinedIcon from '@mui/icons-material/LockOutlined';
import { useCfIdentity } from '../../hooks/useCfIdentity';

export type Severity = 'info' | 'warn' | 'fail' | 'critical';

export interface AlgedonicSignal {
  id: string;
  schema: string;
  emitted_at: string;
  repo: string;
  source: string;
  severity: Severity;
  summary: string;
  details: string;
  evidence_url: string | null;
  affected_artifacts: string[];
  ttl_hours: number;
  escalation: { on_unack_after_hours: number | null; route_to: string };
  ack: { acked: boolean; acked_by: string | null; acked_at: string | null; resolution: string | null };
  supersedes: string[];
}

export interface AlgedonicProjection {
  generated_at: string;
  total: number;
  by_severity: { info: number; warn: number; fail: number; critical: number };
  unacked: AlgedonicSignal[];
  top3: AlgedonicSignal[];
  has_critical: boolean;
}

// MUI palette key per severity. Mirrors the dashboard's Heartbeat color
// language so the visual cue matches the regression banner.
const SEVERITY_COLOR: Record<Severity, 'error' | 'warning' | 'info' | 'success'> = {
  critical: 'error',
  fail: 'error',
  warn: 'warning',
  info: 'info',
};

// Repo emoji — matches the GA dashboard's "fleet" iconography. Keeps the tile
// scannable so the operator can spot at-a-glance which sibling repo is shouting.
const REPO_GLYPH: Record<string, string> = {
  ga: 'GA',
  ix: 'IX',
  demerzel: 'DZ',
  tars: 'TS',
  sentrux: 'SX',
  hari: 'HR',
};

function relativeTime(iso: string): string {
  const minutes = Math.floor((Date.now() - new Date(iso).getTime()) / 60000);
  if (minutes < 1) return 'just now';
  if (minutes < 60) return `${minutes}m ago`;
  const hours = Math.floor(minutes / 60);
  if (hours < 24) return `${hours}h ago`;
  return `${Math.floor(hours / 24)}d ago`;
}

interface AlgedonicCardProps {
  refreshIntervalMs?: number;
}

export const AlgedonicCard: React.FC<AlgedonicCardProps> = ({ refreshIntervalMs = 30_000 }) => {
  const [projection, setProjection] = useState<AlgedonicProjection | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [ackingId, setAckingId] = useState<string | null>(null);

  // Read endpoint (/dev-data/algedonic) is public; ack endpoint
  // (/actions/algedonic/ack/<id>) is gated by Cloudflare Access. Hook
  // tells us whether the operator can fire acks. If not, we render the
  // Ack button with a lock + sign-in CTA instead of disabling it dead.
  const { authed, loading: authLoading, signInUrl } = useCfIdentity();

  const load = useCallback(async () => {
    try {
      const r = await fetch('/dev-data/algedonic', { cache: 'no-store' });
      if (!r.ok) throw new Error(`HTTP ${r.status}`);
      const data = (await r.json()) as AlgedonicProjection;
      setProjection(data);
      setError(null);
    } catch (e) {
      setError(String((e as Error).message ?? e));
    }
  }, []);

  useEffect(() => {
    load();
    const handle = window.setInterval(load, refreshIntervalMs);
    return () => window.clearInterval(handle);
  }, [load, refreshIntervalMs]);

  const ack = useCallback(
    async (id: string) => {
      if (!authed) {
        window.location.href = signInUrl;
        return;
      }
      setAckingId(id);
      try {
        const r = await fetch(`/actions/algedonic/ack/${encodeURIComponent(id)}`, {
          method: 'POST',
          credentials: 'include',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({ acked_by: 'dashboard', resolution: 'acked from Heartbeat tile' }),
        });
        if (r.status === 401 || r.status === 403) {
          // CF Access session lapsed — bounce to login.
          setError('Sign-in required. Redirecting to Cloudflare Access…');
          window.setTimeout(() => { window.location.href = signInUrl; }, 1200);
          return;
        }
        if (!r.ok) throw new Error(`ack HTTP ${r.status}`);
        await load();
      } catch (e) {
        setError(`ack failed: ${String((e as Error).message ?? e)}`);
      } finally {
        setAckingId(null);
      }
    },
    [authed, signInUrl, load],
  );

  if (error && !projection) {
    return (
      <Paper sx={{ p: 2 }}>
        <Alert severity="warning">Algedonic channel unreachable: {error}</Alert>
      </Paper>
    );
  }

  if (!projection) {
    return (
      <Paper sx={{ p: 2 }}>
        <Typography variant="caption" color="text.secondary">Loading algedonic channel…</Typography>
      </Paper>
    );
  }

  const totalUnacked =
    projection.by_severity.info + projection.by_severity.warn + projection.by_severity.fail + projection.by_severity.critical;

  return (
    <Paper sx={{ p: 2 }} data-testid="algedonic-card">
      <Stack direction="row" justifyContent="space-between" alignItems="baseline" sx={{ mb: 1 }}>
        <Stack direction="row" spacing={1} alignItems="center">
          <NotificationsActiveIcon
            fontSize="small"
            color={projection.has_critical ? 'error' : totalUnacked > 0 ? 'warning' : 'disabled'}
          />
          <Typography variant="h6">Algedonic channel</Typography>
          <Tooltip title="VSM alarm path — cross-repo critical signals bypass normal channels. See docs/contracts/2026-05-24-algedonic-channel.contract.md.">
            <Typography variant="caption" color="text.secondary">
              {totalUnacked} unacked · {projection.total} total
            </Typography>
          </Tooltip>
        </Stack>
        <Tooltip title="Refresh now">
          <IconButton size="small" onClick={load} aria-label="refresh algedonic channel">
            <RefreshIcon fontSize="small" />
          </IconButton>
        </Tooltip>
      </Stack>

      {/* Severity-tier badges */}
      <Stack direction="row" spacing={0.75} sx={{ mb: 1.5 }} flexWrap="wrap" useFlexGap>
        {(['critical', 'fail', 'warn', 'info'] as const).map((sev) => {
          const count = projection.by_severity[sev];
          const color = SEVERITY_COLOR[sev];
          return (
            <Chip
              key={sev}
              label={`${sev} ${count}`}
              size="small"
              color={count > 0 ? color : 'default'}
              variant={count > 0 ? 'filled' : 'outlined'}
              sx={{ textTransform: 'uppercase', fontSize: '0.7rem', fontWeight: 600 }}
            />
          );
        })}
      </Stack>

      {/* Top-3 unacked */}
      {projection.top3.length === 0 ? (
        <Typography variant="body2" color="text.secondary">
          No unacked signals. Inbox is quiet.
        </Typography>
      ) : (
        <Stack spacing={1}>
          {projection.top3.map((sig) => (
            <Box
              key={sig.id}
              sx={{
                p: 1,
                border: '1px solid',
                borderColor:
                  sig.severity === 'critical' || sig.severity === 'fail'
                    ? 'error.main'
                    : sig.severity === 'warn'
                      ? 'warning.main'
                      : 'divider',
                borderRadius: 1,
                bgcolor:
                  sig.severity === 'critical' ? 'error.lighter' : 'background.paper',
              }}
            >
              <Stack direction="row" alignItems="flex-start" spacing={1}>
                <Chip
                  label={REPO_GLYPH[sig.repo] ?? sig.repo.slice(0, 2).toUpperCase()}
                  size="small"
                  sx={{ fontFamily: 'monospace', fontSize: '0.7rem', minWidth: 36 }}
                />
                <Chip
                  label={sig.severity}
                  size="small"
                  color={SEVERITY_COLOR[sig.severity]}
                  sx={{ fontSize: '0.7rem', textTransform: 'uppercase', fontWeight: 600 }}
                />
                <Box sx={{ flex: 1, minWidth: 0 }}>
                  <Typography variant="body2" sx={{ fontWeight: 500, lineHeight: 1.3 }}>
                    {sig.summary}
                  </Typography>
                  <Typography variant="caption" color="text.secondary">
                    {sig.source} · {relativeTime(sig.emitted_at)}
                  </Typography>
                </Box>
                <Stack direction="row" spacing={0.5}>
                  {sig.evidence_url && (
                    <Tooltip title="Open evidence">
                      <IconButton
                        size="small"
                        component={Link}
                        href={sig.evidence_url}
                        target="_blank"
                        rel="noopener noreferrer"
                        aria-label="open evidence"
                      >
                        <OpenInNewIcon fontSize="small" />
                      </IconButton>
                    </Tooltip>
                  )}
                  <Tooltip
                    title={
                      authLoading
                        ? 'Checking sign-in…'
                        : !authed
                          ? 'Sign in via Cloudflare Access to acknowledge signals'
                          : 'Acknowledge — moves out of unacked list'
                    }
                  >
                    <span>
                      <Button
                        size="small"
                        variant="outlined"
                        startIcon={
                          authed
                            ? <CheckCircleOutlineIcon fontSize="small" />
                            : <LockOutlinedIcon fontSize="small" />
                        }
                        disabled={ackingId === sig.id || authLoading}
                        onClick={() => ack(sig.id)}
                        aria-label={!authed ? 'Sign in required to ack' : `Ack signal ${sig.id}`}
                        data-authed={authed ? 'true' : 'false'}
                        sx={{ minWidth: 0, px: 1, opacity: !authed ? 0.7 : 1 }}
                      >
                        Ack
                      </Button>
                    </span>
                  </Tooltip>
                </Stack>
              </Stack>
            </Box>
          ))}
        </Stack>
      )}

      {projection.unacked.length > projection.top3.length && (
        <Typography variant="caption" color="text.secondary" sx={{ display: 'block', mt: 1 }}>
          + {projection.unacked.length - projection.top3.length} more unacked
        </Typography>
      )}

      {error && (
        <Alert severity="warning" sx={{ mt: 1 }}>{error}</Alert>
      )}
    </Paper>
  );
};

// CriticalBanner — rendered at the top of the Heartbeat block when any
// critical-severity signal is unacked. Impossible to miss.
export const AlgedonicCriticalBanner: React.FC<{ projection: AlgedonicProjection | null }> = ({ projection }) => {
  if (!projection || !projection.has_critical) return null;
  const criticals = projection.unacked.filter((s) => s.severity === 'critical');
  if (criticals.length === 0) return null;
  const lead = criticals[0];

  return (
    <Paper
      sx={{
        p: 1.5,
        bgcolor: 'error.main',
        color: 'common.white',
        display: 'flex',
        alignItems: 'center',
        gap: 1.5,
        flexWrap: 'wrap',
      }}
    >
      <NotificationsActiveIcon sx={{ color: 'common.white' }} />
      <Typography variant="body2" sx={{ fontWeight: 700, color: 'inherit' }}>
        CRITICAL · {criticals.length} unacked
      </Typography>
      <Typography variant="body2" sx={{ color: 'inherit', flex: 1, opacity: 0.95 }}>
        [{lead.repo}] {lead.summary}
      </Typography>
      {lead.evidence_url && (
        <Link
          href={lead.evidence_url}
          target="_blank"
          rel="noopener noreferrer"
          sx={{ color: 'common.white', fontWeight: 600, textDecoration: 'underline' }}
        >
          open evidence
        </Link>
      )}
    </Paper>
  );
};

export default AlgedonicCard;
