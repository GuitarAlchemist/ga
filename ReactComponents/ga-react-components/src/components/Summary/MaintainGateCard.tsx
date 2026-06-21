import React, { useEffect, useState } from 'react';
import { Box, Chip, CircularProgress, Divider, Paper, Stack, Tooltip, Typography } from '@mui/material';
import GavelIcon from '@mui/icons-material/Gavel';
import InfoOutlinedIcon from '@mui/icons-material/InfoOutlined';
import type {
  MaintainVerdictSnapshot,
  MaintainSignal,
  MaintainSignalStatus,
  MaintainStatus,
} from '../../dev-data/parsers';

// Maintain-gate advisory verdict tile (ga#446 / Phase C).
//
// Renders IX's fused, cross-signal hexavalent (T/P/U/D/F/C) maintain verdict
// federated into state/quality/maintain-gate/last.json (producer ix#146,
// schema ga#445). Self-fetching like ValueScorecard / MissionControl: no
// props, reads /dev-data/maintain-gate (served by the Vite dev-data
// middleware, which computes age_hours + stale server-side).
//
// IMPORTANT: this is ADVISORY / non-binding until IX Phase-3b — it is the
// fused verdict GA's per-axis gates (CanonicalSignatureChecker, semantic-
// regression) individually lack, NOT another pass/fail gate. The tile makes
// that explicit (advisory badge) and never reads a missing/old snapshot as a
// fake green (stale badge + traffic-light honesty — `unknown` ≠ green).

interface MaintainPayload {
  generated_at: string;
  snapshot: MaintainVerdictSnapshot | null;
  source: 'live' | 'fixture';
  age_hours: number | null;
  stale: boolean;
  note?: string;
}

// Hexavalent legend — the cross-repo LOCKED status enum.
const STATUS_LEGEND: Record<MaintainStatus, { label: string; meaning: string; color: 'success' | 'warning' | 'error' | 'default' }> = {
  T: { label: 'T', meaning: 'accept', color: 'success' },
  P: { label: 'P', meaning: 'accept (with flags)', color: 'success' },
  U: { label: 'U', meaning: 'escalate (no / unverifiable signal)', color: 'warning' },
  D: { label: 'D', meaning: 'escalate (disputed — oscillating)', color: 'warning' },
  F: { label: 'F', meaning: 'reject', color: 'error' },
  C: { label: 'C', meaning: 'reject + alarm (reward-hack)', color: 'error' },
};

// oracle_status → MUI traffic-light color.
function oracleColor(oracle: string): 'success' | 'warning' | 'error' | 'default' {
  if (oracle === 'ok') return 'success';
  if (oracle === 'warn') return 'warning';
  if (oracle === 'error') return 'error';
  return 'default';
}

// per-signal status → color + glyph. `unknown` is deliberately neutral (never
// green) so a no-signal lens caps the read honestly.
const SIGNAL_VIS: Record<MaintainSignalStatus, { color: 'success' | 'error' | 'default'; glyph: string }> = {
  ok: { color: 'success', glyph: '✓' },
  bad: { color: 'error', glyph: '✗' },
  unknown: { color: 'default', glyph: '—' },
};

function formatAge(ageHours: number | null): string {
  if (ageHours == null) return 'unknown age';
  if (ageHours < 1) return `${Math.round(ageHours * 60)}m ago`;
  if (ageHours < 48) return `${Math.round(ageHours)}h ago`;
  return `${Math.round(ageHours / 24)}d ago`;
}

const SignalChip: React.FC<{ sig: MaintainSignal }> = ({ sig }) => {
  const vis = SIGNAL_VIS[sig.status] ?? SIGNAL_VIS.unknown;
  return (
    <Tooltip title={`${sig.lens}: ${sig.status}${sig.detail ? ` — ${sig.detail}` : ''}`} placement="top">
      <Chip
        label={`${vis.glyph} ${sig.lens}`}
        color={vis.color}
        size="small"
        variant={sig.status === 'ok' ? 'filled' : 'outlined'}
        sx={{ fontSize: '0.7rem' }}
        data-testid={`maintain-signal-${sig.lens}`}
        data-signal-status={sig.status}
      />
    </Tooltip>
  );
};

const Header: React.FC = () => (
  <Stack direction="row" spacing={1} alignItems="center" sx={{ mb: 1 }}>
    <GavelIcon sx={{ color: 'text.secondary' }} />
    <Typography variant="subtitle1" sx={{ fontWeight: 700 }}>Maintain Gate</Typography>
    <Tooltip
      title="IX's fused cross-signal maintain verdict (metric · guardrail · convergence · drift), rolled up to a single hexavalent status. The verdict GA's per-axis gates individually lack. Federated daily from ix (ix_maintain_snapshot)."
      placement="top"
      enterDelay={300}
    >
      <InfoOutlinedIcon sx={{ fontSize: 15, color: 'text.disabled' }} />
    </Tooltip>
  </Stack>
);

export const MaintainGateCard: React.FC = () => {
  const [payload, setPayload] = useState<MaintainPayload | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    let cancelled = false;
    fetch('/dev-data/maintain-gate', { cache: 'no-store' })
      .then(async (r) => {
        if (!r.ok) throw new Error(`/dev-data/maintain-gate → ${r.status}`);
        return r.json() as Promise<MaintainPayload>;
      })
      .then((d) => { if (!cancelled) setPayload(d); })
      .catch((e) => { if (!cancelled) setError(String(e)); });
    return () => { cancelled = true; };
  }, []);

  // Absent snapshot (404) or fetch error — quiet hint, never blank or fake green.
  if (error) {
    return (
      <Paper data-testid="maintain-gate-card" sx={{ p: 2 }}>
        <Header />
        <Typography variant="caption" color="text.secondary">
          No maintain verdict yet — federated from the sibling ix repo
          (<code>ix_maintain_snapshot</code>, ix#146) into
          <code> state/quality/maintain-gate/last.json</code>.
        </Typography>
      </Paper>
    );
  }

  if (!payload) {
    return (
      <Paper data-testid="maintain-gate-card" sx={{ p: 2, display: 'flex', justifyContent: 'center' }}>
        <CircularProgress size={20} />
      </Paper>
    );
  }

  const snap = payload.snapshot;
  if (!snap) {
    return (
      <Paper data-testid="maintain-gate-card" sx={{ p: 2 }}>
        <Header />
        <Typography variant="caption" color="text.secondary">
          {payload.note ?? 'Snapshot unparseable — treated as stale (no fake green).'}
        </Typography>
      </Paper>
    );
  }

  const legend = STATUS_LEGEND[snap.status as MaintainStatus];
  const statusColor = legend?.color ?? 'default';
  const statusMeaning = legend?.meaning ?? snap.decision;
  const oracle = oracleColor(snap.oracle_status);

  return (
    <Paper data-testid="maintain-gate-card" data-maintain-status={snap.status} data-maintain-stale={payload.stale} sx={{ p: 2 }}>
      <Header />

      {/* Verdict row: hexavalent status chip (color by value) + decision +
          oracle traffic-light dot + freshness + advisory/stale badges. */}
      <Stack direction="row" spacing={1} alignItems="center" flexWrap="wrap" useFlexGap sx={{ mb: 1 }}>
        <Tooltip title={`Hexavalent verdict ${snap.status} — ${statusMeaning}`} placement="top">
          <Chip
            label={`${snap.status} · ${statusMeaning}`}
            color={statusColor}
            size="small"
            sx={{ fontWeight: 700 }}
            data-testid="maintain-status-chip"
          />
        </Tooltip>

        {/* Oracle traffic light. */}
        <Tooltip title={`oracle_status: ${snap.oracle_status}`} placement="top">
          <Box
            data-testid="maintain-oracle-dot"
            data-oracle={snap.oracle_status}
            sx={{
              width: 12, height: 12, borderRadius: '50%',
              bgcolor: oracle === 'default' ? 'text.disabled' : `${oracle}.main`,
            }}
          />
        </Tooltip>

        <Box sx={{ flex: 1 }} />

        {snap.advisory && (
          <Tooltip title="Non-binding marker — this verdict is informational until IX Phase-3b makes it gating. GA presents it as advisory, not pass/fail." placement="top">
            <Chip
              label="ADVISORY — non-binding until IX Phase-3b"
              size="small"
              variant="outlined"
              color="info"
              sx={{ fontSize: '0.65rem', height: 20 }}
              data-testid="maintain-advisory-badge"
            />
          </Tooltip>
        )}

        <Tooltip title={`emitted ${snap.emitted_at}${payload.source === 'fixture' ? ' (dev fixture — no live snapshot)' : ''}`} placement="top">
          <Chip
            label={payload.stale ? `STALE · ${formatAge(payload.age_hours)}` : formatAge(payload.age_hours)}
            size="small"
            variant="outlined"
            color={payload.stale ? 'warning' : 'default'}
            sx={{ fontSize: '0.65rem', height: 20 }}
            data-testid="maintain-freshness-badge"
          />
        </Tooltip>
      </Stack>

      {/* One-line summary. */}
      <Typography variant="body2" sx={{ mb: 1 }}>{snap.summary}</Typography>

      <Divider sx={{ mb: 1 }} />

      {/* Per-signal breakdown. */}
      <Typography variant="caption" color="text.secondary" sx={{ textTransform: 'uppercase', letterSpacing: 0.5 }}>
        Sub-signals
      </Typography>
      <Stack direction="row" spacing={0.75} flexWrap="wrap" useFlexGap data-testid="maintain-signals" sx={{ mt: 0.5 }}>
        {snap.signals.length === 0 ? (
          <Typography variant="caption" color="text.secondary">no sub-signals reported</Typography>
        ) : (
          snap.signals.map((sig) => <SignalChip key={sig.lens} sig={sig} />)
        )}
      </Stack>

      {/* Trend rollup (advisory context). */}
      {snap.maintain_trend && (snap.maintain_trend.total ?? 0) > 0 && (
        <Typography variant="caption" color="text.secondary" sx={{ display: 'block', mt: 1 }}>
          trend: {snap.maintain_trend.total} verdicts · {snap.maintain_trend.accepts ?? 0} accept ·{' '}
          {snap.maintain_trend.rejects ?? 0} reject · {snap.maintain_trend.escalates ?? 0} escalate
          {(snap.maintain_trend.reward_hacks ?? 0) > 0 && ` · ${snap.maintain_trend.reward_hacks} reward-hack`}
        </Typography>
      )}

      {/* Hexavalent legend so the chip is self-explanatory. */}
      <Typography variant="caption" color="text.disabled" sx={{ display: 'block', mt: 1, fontSize: '0.65rem' }}>
        T accept · P accept-w/-flags · U escalate · D disputed · F reject · C reward-hack
      </Typography>
    </Paper>
  );
};

export default MaintainGateCard;
