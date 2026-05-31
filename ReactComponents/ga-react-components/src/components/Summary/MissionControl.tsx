// MissionControl — 4-quadrant "what's happening RIGHT NOW" summary
// rendered above the existing tiles on /test#dev/summary.
//
// Goal: a human dropping in cold answers "what's the project doing?" in
// 5 seconds without scrolling. Each quadrant is a scannable list (max 5
// items) backed by an existing /dev-data/* endpoint plus a new
// /dev-data/recent-events aggregator for the JUST HAPPENED quadrant.
//
// Layout: responsive Grid (2x2 on >= md, 1x4 stack on xs/sm).
// Auto-refresh: 30s (matches AlgedonicCard cadence).
//
// Color language mirrors the rest of the dashboard:
//   NOW            → info.main   (blue)
//   WAITING ON YOU → warning.main (amber)
//   JUST HAPPENED  → success.main (green)
//   AT RISK        → error.main  (red)

import React, { useCallback, useEffect, useMemo, useState } from 'react';
import {
  Box,
  Chip,
  Grid,
  Link,
  Paper,
  Stack,
  Tooltip,
  Typography,
} from '@mui/material';
import BoltIcon from '@mui/icons-material/Bolt';
import NotificationsActiveIcon from '@mui/icons-material/NotificationsActive';
import HistoryIcon from '@mui/icons-material/History';
import WarningAmberIcon from '@mui/icons-material/WarningAmber';
import type { InFlightPayload, InFlightPr } from './InFlightCard';
import type { AlgedonicProjection, AlgedonicSignal } from '../Algedonic/AlgedonicCard';

// ── Types for the auxiliary endpoints ────────────────────────────────────────
interface LoopsGoalsActiveView {
  id: string;
  kind: 'loop' | 'goal';
  session_id: string;
  prompt_or_condition: string;
  last_activity_at: string;
  status: string;
  last_activity_min_ago: number;
}
interface LoopsGoalsProjection {
  fetched_at: string;
  active_loops: LoopsGoalsActiveView[];
  active_goals: LoopsGoalsActiveView[];
  completed_recent: unknown[];
  total_records: number;
}

interface RecentEvent {
  kind: 'merge' | 'algedonic' | 'commit' | 'ack';
  at: string;                  // ISO timestamp
  ago_minutes: number;
  summary: string;
  url?: string;
  severity?: string;           // for algedonic
  author?: string;             // for commit/merge
  pr_number?: number;          // for merge
}
interface RecentEventsPayload {
  fetched_at: string;
  window_hours: number;
  events: RecentEvent[];
}

interface QualityDomain { source: string; data: Record<string, unknown> }
interface ManifestQuality { domains: Record<string, QualityDomain>; regressions?: string[] }
interface ManifestPayload { quality?: ManifestQuality }

// ── Helpers ──────────────────────────────────────────────────────────────────
function fmtMinutes(m: number): string {
  if (m < 0) return '?';
  if (m < 1) return 'just now';
  if (m < 60) return `${m}m ago`;
  const h = Math.floor(m / 60);
  if (h < 24) return `${h}h ago`;
  const d = Math.floor(h / 24);
  return `${d}d ago`;
}
function truncate(s: string, max: number): string {
  if (!s) return '';
  return s.length <= max ? s : s.slice(0, max - 1) + '…';
}

// Stale-agent heuristic: an active loop/goal whose last_activity is > 5min ago.
// This matches operator intuition — "if a /loop hasn't ticked in 5min, something
// is wrong (Claude exited, host crashed, hook deadlocked)".
const STALE_HEARTBEAT_MIN = 5;

// Stuck-PR heuristic: open > 24h, OR mergeable state is DIRTY/BLOCKED.
const STUCK_PR_AGE_MIN = 24 * 60;

// ── Quadrant tile ────────────────────────────────────────────────────────────
type QuadrantColor = 'info' | 'warning' | 'success' | 'error';

interface QuadrantProps {
  title: string;
  icon: React.ReactNode;
  color: QuadrantColor;
  count: number;
  emptyMessage: string;
  children?: React.ReactNode;
  testId: string;
}

const COLOR_MAP: Record<QuadrantColor, { main: string; bg: string }> = {
  info:    { main: 'info.main',    bg: 'info.lighter' },
  warning: { main: 'warning.main', bg: 'warning.lighter' },
  success: { main: 'success.main', bg: 'success.lighter' },
  error:   { main: 'error.main',   bg: 'error.lighter' },
};

const Quadrant: React.FC<QuadrantProps> = ({ title, icon, color, count, emptyMessage, children, testId }) => {
  const { main } = COLOR_MAP[color];
  return (
    <Paper
      data-testid={testId}
      sx={{
        p: 1.5,
        height: '100%',
        minHeight: 180,
        borderLeft: '4px solid',
        borderColor: main,
        display: 'flex',
        flexDirection: 'column',
      }}
    >
      <Stack direction="row" spacing={1} alignItems="center" sx={{ mb: 1 }}>
        <Box sx={{ color: main, display: 'flex' }}>{icon}</Box>
        <Typography
          variant="caption"
          sx={{ fontWeight: 700, textTransform: 'uppercase', letterSpacing: 0.8, color: 'text.secondary' }}
        >
          {title}
        </Typography>
        <Chip
          label={count}
          size="small"
          data-testid={`${testId}-count`}
          sx={{
            height: 18,
            fontSize: '0.7rem',
            fontWeight: 700,
            bgcolor: main,
            color: 'common.white',
            '& .MuiChip-label': { px: 0.75 },
          }}
        />
      </Stack>
      <Box sx={{ flex: 1, minHeight: 0 }}>
        {count === 0 ? (
          <Typography variant="caption" color="text.disabled" sx={{ fontStyle: 'italic' }}>
            {emptyMessage}
          </Typography>
        ) : (
          children
        )}
      </Box>
    </Paper>
  );
};

// ── Row primitives ───────────────────────────────────────────────────────────
interface RowProps {
  primary: string;
  secondary?: string;
  href?: string;
  onClick?: () => void;
  prefix?: React.ReactNode;
  tooltip?: string;
  testId?: string;
}
const Row: React.FC<RowProps> = ({ primary, secondary, href, onClick, prefix, tooltip, testId }) => {
  const content = (
    <Stack
      direction="row"
      spacing={0.75}
      alignItems="center"
      sx={{
        py: 0.4,
        px: 0.5,
        borderRadius: 0.5,
        cursor: href || onClick ? 'pointer' : 'default',
        '&:hover': href || onClick ? { bgcolor: 'action.hover' } : {},
      }}
      data-testid={testId}
      onClick={onClick}
    >
      {prefix}
      <Typography
        variant="caption"
        sx={{
          flex: 1,
          minWidth: 0,
          overflow: 'hidden',
          textOverflow: 'ellipsis',
          whiteSpace: 'nowrap',
          fontSize: '0.75rem',
        }}
      >
        {primary}
      </Typography>
      {secondary && (
        <Typography variant="caption" color="text.secondary" sx={{ fontSize: '0.65rem', flexShrink: 0 }}>
          {secondary}
        </Typography>
      )}
    </Stack>
  );
  const wrapped = tooltip ? <Tooltip title={tooltip} placement="top-start" enterDelay={400}>{content}</Tooltip> : content;
  if (href) {
    return (
      <Link
        href={href}
        target="_blank"
        rel="noopener noreferrer"
        underline="none"
        color="inherit"
        sx={{ display: 'block' }}
      >
        {wrapped}
      </Link>
    );
  }
  return wrapped;
};

// ── Scroll-to-element helper ────────────────────────────────────────────────
function scrollToTile(selector: string): void {
  const el = document.querySelector(selector);
  if (el) {
    el.scrollIntoView({ behavior: 'smooth', block: 'start' });
  }
}

// ── MissionControl ──────────────────────────────────────────────────────────
interface MissionControlProps {
  refreshIntervalMs?: number;
}

export const MissionControl: React.FC<MissionControlProps> = ({ refreshIntervalMs = 30_000 }) => {
  const [inFlight, setInFlight] = useState<InFlightPayload | null>(null);
  const [algedonic, setAlgedonic] = useState<AlgedonicProjection | null>(null);
  const [loopsGoals, setLoopsGoals] = useState<LoopsGoalsProjection | null>(null);
  const [recentEvents, setRecentEvents] = useState<RecentEventsPayload | null>(null);
  const [quality, setQuality] = useState<ManifestQuality | null>(null);

  const load = useCallback(async () => {
    const fetchJson = async <T,>(url: string): Promise<T | null> => {
      try {
        const r = await fetch(url, { cache: 'no-store' });
        if (!r.ok) return null;
        return (await r.json()) as T;
      } catch {
        return null;
      }
    };
    const [inF, alg, lg, ev, manifest] = await Promise.all([
      fetchJson<InFlightPayload>('/dev-data/in-flight'),
      fetchJson<AlgedonicProjection>('/dev-data/algedonic'),
      fetchJson<LoopsGoalsProjection>('/dev-data/runtime-loops-goals'),
      fetchJson<RecentEventsPayload>('/dev-data/recent-events'),
      fetchJson<ManifestPayload>('/dev-data/manifest'),
    ]);
    if (inF) setInFlight(inF);
    if (alg) setAlgedonic(alg);
    if (lg) setLoopsGoals(lg);
    if (ev) setRecentEvents(ev);
    if (manifest?.quality) setQuality(manifest.quality);
  }, []);

  useEffect(() => {
    load();
    const h = window.setInterval(load, refreshIntervalMs);
    return () => window.clearInterval(h);
  }, [load, refreshIntervalMs]);

  // ── NOW: active agents, /loop, /goal, top in-flight PR ────────────────────
  const nowItems = useMemo(() => {
    const items: { key: string; primary: string; secondary: string; tooltip?: string; onClick?: () => void; href?: string }[] = [];
    // Top in-flight PR (first non-draft)
    const topPr: InFlightPr | undefined = inFlight?.open_prs.find((p) => !p.draft);
    if (topPr) {
      const etaPart = topPr.eta_minutes != null && topPr.eta_minutes > 0
        ? ` · ETA ${fmtAgo(topPr.eta_minutes)}`
        : '';
      items.push({
        key: `pr-${topPr.number}`,
        primary: `#${topPr.number} ${truncate(topPr.title, 55)}`,
        secondary: `${Math.floor(topPr.age_minutes / 60)}h${etaPart}`,
        tooltip: `${topPr.title}\n\n${topPr.checks.passed}/${topPr.checks.total} checks · ${topPr.author_is_agent ? 'agent' : 'human'} ${topPr.author_agent_name ?? topPr.author}`,
        href: topPr.url,
      });
    }
    // Active loops (up to 2)
    for (const l of (loopsGoals?.active_loops ?? []).slice(0, 2)) {
      items.push({
        key: `loop-${l.id}`,
        primary: `/loop · ${truncate(l.prompt_or_condition, 60)}`,
        secondary: fmtMinutes(l.last_activity_min_ago),
        tooltip: `${l.prompt_or_condition}\n\nsession ${l.session_id}`,
      });
    }
    // Active goals (up to 2)
    for (const g of (loopsGoals?.active_goals ?? []).slice(0, 2)) {
      items.push({
        key: `goal-${g.id}`,
        primary: `/goal · ${truncate(g.prompt_or_condition, 60)}`,
        secondary: fmtMinutes(g.last_activity_min_ago),
        tooltip: `${g.prompt_or_condition}\n\nsession ${g.session_id}`,
      });
    }
    return items.slice(0, 5);
  }, [inFlight, loopsGoals]);

  // ── WAITING ON YOU: PRs ready to merge, unacked algedonic, P0/P1 ──────────
  const waitingItems = useMemo(() => {
    const items: { key: string; primary: string; secondary: string; tooltip?: string; href?: string; onClick?: () => void }[] = [];
    // PRs ready to merge: passing checks, no draft, no failed CI, not DIRTY/BLOCKED
    const readyPrs = (inFlight?.open_prs ?? []).filter((p) => {
      if (p.draft) return false;
      if (p.checks.failed > 0) return false;
      if (p.checks.pending > 0) return false;
      const state = (p.mergeable ?? '').toUpperCase();
      if (state === 'DIRTY' || state === 'BLOCKED') return false;
      return true;
    });
    for (const pr of readyPrs.slice(0, 3)) {
      items.push({
        key: `ready-${pr.number}`,
        primary: `#${pr.number} ${truncate(pr.title, 55)}`,
        secondary: 'ready',
        tooltip: `${pr.title}\n\nAll ${pr.checks.passed}/${pr.checks.total} checks green. Click to scroll to InFlightCard.`,
        href: pr.url,
      });
    }
    // Unacked algedonic (top 3)
    const top = (algedonic?.top3 ?? []);
    for (const sig of top.slice(0, 3)) {
      items.push({
        key: `alg-${sig.id}`,
        primary: `[${sig.severity}] ${truncate(sig.summary, 60)}`,
        secondary: relMin(sig.emitted_at),
        tooltip: `${sig.repo} · ${sig.summary}\n\nClick to scroll to AlgedonicCard for Ack.`,
        onClick: () => scrollToTile('[data-testid="algedonic-card"]'),
      });
    }
    return items.slice(0, 5);
  }, [inFlight, algedonic]);

  // ── JUST HAPPENED: last 6h merges + signals + commits ─────────────────────
  const happenedItems = useMemo(() => {
    const items: { key: string; primary: string; secondary: string; tooltip?: string; href?: string }[] = [];
    for (const e of (recentEvents?.events ?? []).slice(0, 5)) {
      const prefix =
        e.kind === 'merge' ? 'merged' :
        e.kind === 'algedonic' ? `signal[${e.severity ?? '?'}]` :
        e.kind === 'ack' ? 'acked' :
        'commit';
      items.push({
        key: `${e.kind}-${e.at}`,
        primary: `${prefix} · ${truncate(e.summary, 55)}`,
        secondary: fmtMinutes(e.ago_minutes),
        tooltip: e.summary,
        href: e.url,
      });
    }
    return items;
  }, [recentEvents]);

  // ── AT RISK: stale agents, stuck PRs, silent rot, regressions ─────────────
  const atRiskItems = useMemo(() => {
    const items: { key: string; primary: string; secondary: string; tooltip?: string }[] = [];
    // Stale agents — loops/goals whose last heartbeat is older than threshold
    const stale = [
      ...(loopsGoals?.active_loops ?? []),
      ...(loopsGoals?.active_goals ?? []),
    ].filter((x) => x.last_activity_min_ago > STALE_HEARTBEAT_MIN);
    for (const s of stale.slice(0, 2)) {
      items.push({
        key: `stale-${s.id}`,
        primary: `stale ${s.kind} · ${truncate(s.prompt_or_condition, 50)}`,
        secondary: `silent ${fmtMinutes(s.last_activity_min_ago)}`,
        tooltip: `${s.kind} in session ${s.session_id} hasn't ticked in ${fmtMinutes(s.last_activity_min_ago)}`,
      });
    }
    // Stuck PRs: open > 24h or conflicts
    const stuck = (inFlight?.open_prs ?? []).filter((p) => {
      if (p.draft) return false;
      const state = (p.mergeable ?? '').toUpperCase();
      if (state === 'DIRTY' || state === 'BLOCKED') return true;
      if (p.age_minutes > STUCK_PR_AGE_MIN) return true;
      return false;
    });
    for (const pr of stuck.slice(0, 2)) {
      const reason = (pr.mergeable ?? '').toUpperCase() === 'DIRTY' ? 'conflicts'
        : (pr.mergeable ?? '').toUpperCase() === 'BLOCKED' ? 'blocked'
        : `${Math.floor(pr.age_minutes / 60)}h old`;
      items.push({
        key: `stuck-${pr.number}`,
        primary: `#${pr.number} ${truncate(pr.title, 50)}`,
        secondary: reason,
        tooltip: `${pr.title}\n\nState: ${pr.mergeable}`,
      });
    }
    // Quality regressions
    for (const r of (quality?.regressions ?? []).slice(0, 2)) {
      items.push({
        key: `reg-${r}`,
        primary: `regression · ${truncate(r, 55)}`,
        secondary: 'QA',
        tooltip: r,
      });
    }
    // Silent rot: quality domain last.json mtime stale (> 7d). Surfaced as the
    // 'unknown' / non-ok oracle_status entries already covered by regressions,
    // so skip here to avoid duplication.
    return items.slice(0, 5);
  }, [inFlight, loopsGoals, quality]);

  // Loading shell: render the 4 quadrants with zero counts so the layout
  // is stable while data arrives (avoids reflow-flash on first paint).
  const isLoading = !inFlight && !algedonic && !loopsGoals && !recentEvents;

  return (
    <Box data-testid="mission-control">
      <Grid container spacing={2}>
        <Grid item xs={12} md={6}>
          <Quadrant
            testId="quadrant-now"
            title="NOW"
            icon={<BoltIcon fontSize="small" />}
            color="info"
            count={nowItems.length}
            emptyMessage={isLoading ? 'Loading…' : 'No active sessions or PRs.'}
          >
            <Stack spacing={0}>
              {nowItems.map((i) => (
                <Row
                  key={i.key}
                  primary={i.primary}
                  secondary={i.secondary}
                  tooltip={i.tooltip}
                  href={i.href}
                  onClick={i.onClick}
                />
              ))}
            </Stack>
          </Quadrant>
        </Grid>
        <Grid item xs={12} md={6}>
          <Quadrant
            testId="quadrant-waiting"
            title="WAITING ON YOU"
            icon={<NotificationsActiveIcon fontSize="small" />}
            color="warning"
            count={waitingItems.length}
            emptyMessage={isLoading ? 'Loading…' : 'Nothing waiting — inbox clear.'}
          >
            <Stack spacing={0}>
              {waitingItems.map((i) => (
                <Row
                  key={i.key}
                  primary={i.primary}
                  secondary={i.secondary}
                  tooltip={i.tooltip}
                  href={i.href}
                  onClick={i.onClick}
                  testId={i.key.startsWith('ready-') ? 'waiting-ready-pr' : undefined}
                />
              ))}
            </Stack>
          </Quadrant>
        </Grid>
        <Grid item xs={12} md={6}>
          <Quadrant
            testId="quadrant-happened"
            title="JUST HAPPENED"
            icon={<HistoryIcon fontSize="small" />}
            color="success"
            count={happenedItems.length}
            emptyMessage={isLoading ? 'Loading…' : `Quiet — no events in last ${recentEvents?.window_hours ?? 6}h.`}
          >
            <Stack spacing={0}>
              {happenedItems.map((i) => (
                <Row
                  key={i.key}
                  primary={i.primary}
                  secondary={i.secondary}
                  tooltip={i.tooltip}
                  href={i.href}
                />
              ))}
            </Stack>
          </Quadrant>
        </Grid>
        <Grid item xs={12} md={6}>
          <Quadrant
            testId="quadrant-at-risk"
            title="AT RISK"
            icon={<WarningAmberIcon fontSize="small" />}
            color="error"
            count={atRiskItems.length}
            emptyMessage={isLoading ? 'Loading…' : 'No risks detected.'}
          >
            <Stack spacing={0}>
              {atRiskItems.map((i) => (
                <Row
                  key={i.key}
                  primary={i.primary}
                  secondary={i.secondary}
                  tooltip={i.tooltip}
                />
              ))}
            </Stack>
          </Quadrant>
        </Grid>
      </Grid>
    </Box>
  );
};

// ── small local helpers ─────────────────────────────────────────────────────
function fmtAgo(m: number): string {
  if (m < 60) return `${m}m`;
  return `${Math.floor(m / 60)}h`;
}
function relMin(iso: string): string {
  if (!iso) return '';
  const ms = Date.now() - new Date(iso).getTime();
  if (Number.isNaN(ms)) return '';
  const minutes = Math.max(0, Math.floor(ms / 60000));
  return fmtMinutes(minutes);
}

// Re-export for the test crossover-skip idiom — lets the spec do
// `await import('.../MissionControl')` to feature-detect the new layout.
export const MISSION_CONTROL_TESTID = 'mission-control';

// Unused type guard — keeps the AlgedonicSignal symbol from being tree-shaken
// in case downstream consumers rely on the re-export through this module.
// eslint-disable-next-line @typescript-eslint/no-unused-vars
const _typeGuard: AlgedonicSignal | null = null;

export default MissionControl;
