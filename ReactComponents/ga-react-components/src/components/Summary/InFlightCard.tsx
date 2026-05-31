// InFlightCard — Summary-tab tile that answers
// "what's being worked on right now and when will it ship?"
//
// Reads /dev-data/in-flight (defined in vite.config.ts devDataPlugin),
// auto-refreshes every 20s.
//
// Renders, in order:
//   1. Header — counts of open PRs, criticals (algedonic last 24h), agents
//   2. Per-PR rows with:
//      - mergeable status icon
//      - PR # + title (truncated, full title in tooltip)
//      - author badge (human / agent + agent name)
//      - CI progress bar (passed / total) color-coded
//      - age vs ETA bar with text "Nm · ETA Nm"
//   3. Collapsible "Recent merges" section (last 5)
//   4. Active loops + goals (only if either has entries)
//
// All data flows from a single endpoint so the tile renders even when
// gh fails (warnings array on the payload).

import React, { useCallback, useEffect, useMemo, useState } from 'react';
import {
  Alert,
  Box,
  Chip,
  Collapse,
  IconButton,
  LinearProgress,
  Link,
  Paper,
  Stack,
  Tooltip,
  Typography,
} from '@mui/material';
import RocketLaunchIcon from '@mui/icons-material/RocketLaunch';
import RefreshIcon from '@mui/icons-material/Refresh';
import ExpandMoreIcon from '@mui/icons-material/ExpandMore';
import ExpandLessIcon from '@mui/icons-material/ExpandLess';
import OpenInNewIcon from '@mui/icons-material/OpenInNew';
import PersonIcon from '@mui/icons-material/Person';
import SmartToyIcon from '@mui/icons-material/SmartToy';

interface PrCheckDetail { name: string; status: string; conclusion: string | null; workflow: string }
export interface InFlightPr {
  number: number;
  title: string;
  url: string;
  head_branch: string;
  author: string;
  author_is_agent: boolean;
  author_agent_name: string | null;
  opened_at: string;
  updated_at: string;
  age_minutes: number;
  draft: boolean;
  labels: string[];
  checks: { total: number; passed: number; failed: number; pending: number; details: PrCheckDetail[] };
  mergeable: string;
  eta_minutes: number | null;
  eta_basis: string;
  pr_type: string;
}
export interface InFlightRecentMerge { number: number; title: string; url: string; merged_at: string; ago_minutes: number; author: string }
export interface InFlightLoopOrGoal { id: string; label: string; status: string; updated_at: string }
export interface InFlightAlgedonicSummary {
  info: number;
  warn: number;
  fail: number;
  critical: number;
  top_unacked: { id: string; severity: string; summary: string; emitted_at: string; repo: string }[];
}
export interface InFlightPayload {
  fetched_at: string;
  open_prs: InFlightPr[];
  recent_merges: InFlightRecentMerge[];
  active_loops: InFlightLoopOrGoal[];
  active_goals: InFlightLoopOrGoal[];
  algedonic_recent: InFlightAlgedonicSummary;
  eta_baseline: { source: string; window_days: number | null; computed_at: string | null };
  warnings: string[];
}

function truncate(s: string, max: number): string {
  if (!s) return '';
  return s.length <= max ? s : s.slice(0, max - 1) + '…';
}

function fmtMinutes(m: number): string {
  if (m < 1) return '<1m';
  if (m < 60) return `${m}m`;
  const h = Math.floor(m / 60);
  const rem = m % 60;
  if (h < 24) return rem > 0 ? `${h}h${rem}m` : `${h}h`;
  const d = Math.floor(h / 24);
  return `${d}d${h % 24}h`;
}

// MUI palette key per mergeStateStatus from the GraphQL enum.
// MERGEABLE / CLEAN → green ; UNSTABLE / BEHIND → yellow ; DIRTY / BLOCKED → red.
function mergeStateColor(state: string): 'success' | 'warning' | 'error' | 'default' {
  const s = (state ?? '').toUpperCase();
  if (s === 'MERGEABLE' || s === 'CLEAN' || s === 'HAS_HOOKS') return 'success';
  if (s === 'UNSTABLE' || s === 'BEHIND' || s === 'UNKNOWN') return 'warning';
  if (s === 'DIRTY' || s === 'BLOCKED') return 'error';
  return 'default';
}
function mergeStateGlyph(state: string): string {
  const c = mergeStateColor(state);
  if (c === 'success') return '●';
  if (c === 'warning') return '◐';
  if (c === 'error') return '●';
  return '○';
}

interface PrRowProps { pr: InFlightPr }
const PrRow: React.FC<PrRowProps> = ({ pr }) => {
  // Progress bar — green slice for passed, red for failed, yellow for pending
  const total = Math.max(1, pr.checks.total);
  const pctPassed = (pr.checks.passed / total) * 100;
  const pctFailed = (pr.checks.failed / total) * 100;
  const pctPending = (pr.checks.pending / total) * 100;

  // ETA vs age — bar fills as work approaches ETA (0% at start, 100% at ETA reached).
  // If age > ETA (overdue), cap at 100% and tint orange.
  const etaTotal = (pr.eta_minutes ?? 0) + pr.age_minutes;
  const etaPct = etaTotal > 0 ? Math.min(100, (pr.age_minutes / etaTotal) * 100) : 0;
  const overdue = (pr.eta_minutes ?? 0) === 0 && pr.age_minutes > 0;

  const mergeColor = mergeStateColor(pr.mergeable);

  const checkTooltip = useMemo(() => {
    const parts: string[] = [];
    if (pr.checks.passed > 0) parts.push(`${pr.checks.passed} passed`);
    if (pr.checks.failed > 0) parts.push(`${pr.checks.failed} failed`);
    if (pr.checks.pending > 0) parts.push(`${pr.checks.pending} pending`);
    if (parts.length === 0) parts.push('no checks');
    // Append first 3 failing/pending names for context
    const interesting = pr.checks.details
      .filter((d) => d.conclusion === 'FAILURE' || d.status === 'IN_PROGRESS' || d.status === 'QUEUED' || d.status === 'PENDING')
      .slice(0, 3)
      .map((d) => `${d.name} (${d.conclusion ?? d.status})`);
    if (interesting.length > 0) parts.push('\n' + interesting.join('\n'));
    return parts.join(' · ');
  }, [pr.checks]);

  return (
    <Box
      sx={{
        py: 1,
        px: 1,
        borderBottom: '1px solid',
        borderColor: 'divider',
        '&:hover': { bgcolor: 'action.hover' },
        cursor: 'pointer',
      }}
      onClick={() => window.open(pr.url, '_blank', 'noopener,noreferrer')}
      role="link"
      aria-label={`Open PR #${pr.number} in new tab`}
    >
      <Stack direction="row" alignItems="center" spacing={1}>
        <Tooltip title={`mergeable: ${pr.mergeable.toLowerCase()}`}>
          <Box
            sx={{
              width: 14,
              textAlign: 'center',
              color:
                mergeColor === 'success'
                  ? 'success.main'
                  : mergeColor === 'warning'
                    ? 'warning.main'
                    : mergeColor === 'error'
                      ? 'error.main'
                      : 'text.disabled',
              fontSize: '0.9rem',
              lineHeight: 1,
            }}
          >
            {mergeStateGlyph(pr.mergeable)}
          </Box>
        </Tooltip>
        <Typography variant="body2" sx={{ fontFamily: 'monospace', fontWeight: 600, minWidth: 48 }}>
          #{pr.number}
        </Typography>
        <Tooltip title={pr.title}>
          <Typography variant="body2" sx={{ flex: 1, minWidth: 0, overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>
            {truncate(pr.title, 60)}
          </Typography>
        </Tooltip>
        <Tooltip title={pr.author_is_agent ? `agent · ${pr.author_agent_name ?? pr.author}` : `human · ${pr.author}`}>
          <Chip
            icon={pr.author_is_agent ? <SmartToyIcon sx={{ fontSize: 14 }} /> : <PersonIcon sx={{ fontSize: 14 }} />}
            label={pr.author_is_agent ? (pr.author_agent_name ?? 'agent') : pr.author}
            size="small"
            variant="outlined"
            sx={{ fontSize: '0.7rem', height: 22, '& .MuiChip-icon': { ml: 0.5 } }}
            color={pr.author_is_agent ? 'primary' : 'default'}
          />
        </Tooltip>
        {pr.draft && (
          <Chip label="draft" size="small" variant="outlined" sx={{ fontSize: '0.65rem', height: 18 }} />
        )}
        <Tooltip title="Open on GitHub">
          <IconButton
            size="small"
            onClick={(e) => { e.stopPropagation(); window.open(pr.url, '_blank', 'noopener,noreferrer'); }}
            aria-label={`open PR #${pr.number}`}
          >
            <OpenInNewIcon sx={{ fontSize: 14 }} />
          </IconButton>
        </Tooltip>
      </Stack>

      <Stack direction="row" spacing={1.5} alignItems="center" sx={{ mt: 0.5 }}>
        {/* CI checks progress — 3-color stacked bar */}
        <Tooltip title={checkTooltip}>
          <Box sx={{ flex: 1, minWidth: 0 }}>
            <Box sx={{ display: 'flex', height: 8, borderRadius: 1, overflow: 'hidden', bgcolor: 'action.hover' }}>
              {pctPassed > 0 && (
                <Box sx={{ width: `${pctPassed}%`, bgcolor: 'success.main' }} />
              )}
              {pctFailed > 0 && (
                <Box sx={{ width: `${pctFailed}%`, bgcolor: 'error.main' }} />
              )}
              {pctPending > 0 && (
                <Box sx={{ width: `${pctPending}%`, bgcolor: 'warning.main' }} />
              )}
            </Box>
            <Typography variant="caption" color="text.secondary" sx={{ fontSize: '0.65rem' }}>
              {pr.checks.passed}/{pr.checks.total} checks
              {pr.checks.failed > 0 && ` · ${pr.checks.failed} failed`}
              {pr.checks.pending > 0 && ` · ${pr.checks.pending} pending`}
            </Typography>
          </Box>
        </Tooltip>

        {/* Age + ETA */}
        <Box sx={{ minWidth: 120 }}>
          <Tooltip title={pr.eta_basis}>
            <Box>
              <LinearProgress
                variant="determinate"
                value={etaPct}
                sx={{
                  height: 4,
                  borderRadius: 1,
                  bgcolor: 'action.hover',
                  '& .MuiLinearProgress-bar': {
                    bgcolor: overdue ? 'warning.main' : 'info.main',
                  },
                }}
              />
              <Typography variant="caption" color="text.secondary" sx={{ fontSize: '0.65rem' }}>
                {fmtMinutes(pr.age_minutes)}
                {pr.eta_minutes != null && pr.eta_minutes > 0 && ` · ETA ${fmtMinutes(pr.eta_minutes)}`}
                {overdue && ' · overdue'}
              </Typography>
            </Box>
          </Tooltip>
        </Box>
      </Stack>
    </Box>
  );
};

interface InFlightCardProps {
  refreshIntervalMs?: number;
}

export const InFlightCard: React.FC<InFlightCardProps> = ({ refreshIntervalMs = 20_000 }) => {
  const [payload, setPayload] = useState<InFlightPayload | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [recentExpanded, setRecentExpanded] = useState(false);

  const load = useCallback(async () => {
    try {
      const r = await fetch('/dev-data/in-flight', { cache: 'no-store' });
      if (!r.ok) throw new Error(`HTTP ${r.status}`);
      const data = (await r.json()) as InFlightPayload;
      setPayload(data);
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

  if (error && !payload) {
    return (
      <Paper sx={{ p: 2 }}>
        <Alert severity="warning">In Flight tile unreachable: {error}</Alert>
      </Paper>
    );
  }
  if (!payload) {
    return (
      <Paper sx={{ p: 2 }}>
        <Typography variant="caption" color="text.secondary">Loading in-flight work…</Typography>
      </Paper>
    );
  }

  const prsToShow = payload.open_prs.filter((p) => !p.draft);
  const draftCount = payload.open_prs.length - prsToShow.length;
  const agentCount = new Set(payload.open_prs.filter((p) => p.author_is_agent).map((p) => p.author_agent_name ?? p.author)).size;
  const totalAlgedonic = payload.algedonic_recent.critical + payload.algedonic_recent.fail + payload.algedonic_recent.warn + payload.algedonic_recent.info;
  const criticalCount = payload.algedonic_recent.critical + payload.algedonic_recent.fail;

  const recentToShow = payload.recent_merges.slice(0, 5);
  const hasLoopsOrGoals = payload.active_loops.length > 0 || payload.active_goals.length > 0;

  return (
    <Paper sx={{ p: 2 }}>
      <Stack direction="row" justifyContent="space-between" alignItems="baseline" sx={{ mb: 1 }}>
        <Stack direction="row" spacing={1} alignItems="center">
          <RocketLaunchIcon fontSize="small" color={criticalCount > 0 ? 'error' : prsToShow.length > 0 ? 'primary' : 'disabled'} />
          <Typography variant="h6">In Flight</Typography>
          <Typography variant="caption" color="text.secondary">
            {prsToShow.length} PR{prsToShow.length === 1 ? '' : 's'}
            {draftCount > 0 && ` + ${draftCount} draft`}
            {' · '}
            {criticalCount} critical
            {' · '}
            {agentCount} agent{agentCount === 1 ? '' : 's'} working
          </Typography>
        </Stack>
        <Tooltip title={`Last fetched ${new Date(payload.fetched_at).toLocaleTimeString()} · refreshes every ${refreshIntervalMs / 1000}s`}>
          <IconButton size="small" onClick={load} aria-label="refresh in-flight tile">
            <RefreshIcon fontSize="small" />
          </IconButton>
        </Tooltip>
      </Stack>

      {/* Warning banner if backend had issues collecting */}
      {payload.warnings.length > 0 && (
        <Alert severity="info" sx={{ mb: 1, py: 0, fontSize: '0.75rem' }}>
          {payload.warnings.join(' · ')}
        </Alert>
      )}

      {/* Open PRs */}
      {prsToShow.length === 0 ? (
        <Typography variant="body2" color="text.secondary" sx={{ py: 1 }}>
          {payload.open_prs.length === 0 ? 'No open PRs.' : `All ${payload.open_prs.length} open PR(s) are drafts.`}
        </Typography>
      ) : (
        <Box>
          {prsToShow.map((pr) => <PrRow key={pr.number} pr={pr} />)}
        </Box>
      )}

      {/* Critical algedonic call-out */}
      {payload.algedonic_recent.top_unacked.length > 0 && (
        <Box sx={{ mt: 1.5, p: 1, bgcolor: criticalCount > 0 ? 'error.lighter' : 'warning.lighter', borderRadius: 1 }}>
          <Typography variant="caption" sx={{ fontWeight: 600, display: 'block', mb: 0.5 }}>
            Algedonic (last 24h): {totalAlgedonic} signal{totalAlgedonic === 1 ? '' : 's'}
          </Typography>
          {payload.algedonic_recent.top_unacked.map((s) => (
            <Typography key={s.id} variant="caption" color="text.secondary" sx={{ display: 'block', lineHeight: 1.4 }}>
              <Box component="span" sx={{ fontWeight: 600, textTransform: 'uppercase', mr: 0.5 }}>[{s.severity}]</Box>
              [{s.repo}] {truncate(s.summary, 80)}
            </Typography>
          ))}
        </Box>
      )}

      {/* Recent merges — collapsible */}
      <Box sx={{ mt: 1.5 }}>
        <Stack
          direction="row"
          spacing={0.5}
          alignItems="center"
          sx={{ cursor: 'pointer' }}
          onClick={() => setRecentExpanded((v) => !v)}
          role="button"
          aria-expanded={recentExpanded}
        >
          {recentExpanded ? <ExpandLessIcon fontSize="small" /> : <ExpandMoreIcon fontSize="small" />}
          <Typography variant="caption" color="text.secondary">
            Recent merges ({payload.recent_merges.length})
            {recentToShow[0] && ` · last ${fmtMinutes(recentToShow[0].ago_minutes)} ago`}
          </Typography>
        </Stack>
        <Collapse in={recentExpanded}>
          <Stack spacing={0.25} sx={{ mt: 0.5, pl: 2 }}>
            {recentToShow.map((m) => (
              <Stack key={m.number} direction="row" spacing={1} alignItems="baseline">
                <Link
                  href={m.url}
                  target="_blank"
                  rel="noopener noreferrer"
                  sx={{ fontFamily: 'monospace', fontSize: '0.75rem', minWidth: 48 }}
                >
                  #{m.number}
                </Link>
                <Typography variant="caption" sx={{ flex: 1, minWidth: 0, overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>
                  {truncate(m.title, 70)}
                </Typography>
                <Typography variant="caption" color="text.secondary">
                  {fmtMinutes(m.ago_minutes)} ago
                </Typography>
              </Stack>
            ))}
            {payload.recent_merges.length === 0 && (
              <Typography variant="caption" color="text.secondary">No merges yet.</Typography>
            )}
          </Stack>
        </Collapse>
      </Box>

      {/* Loops + goals — only renders when populated */}
      {hasLoopsOrGoals && (
        <Box sx={{ mt: 1.5, pt: 1, borderTop: '1px solid', borderColor: 'divider' }}>
          <Typography variant="caption" sx={{ fontWeight: 600, display: 'block', mb: 0.5 }}>
            Active runtime sessions
          </Typography>
          {payload.active_loops.length > 0 && (
            <Stack direction="row" spacing={0.5} flexWrap="wrap" useFlexGap sx={{ mb: 0.5 }}>
              <Typography variant="caption" color="text.secondary">Loops:</Typography>
              {payload.active_loops.slice(0, 3).map((l) => (
                <Chip key={l.id} label={truncate(l.label, 40)} size="small" variant="outlined" sx={{ fontSize: '0.65rem', height: 18 }} />
              ))}
              {payload.active_loops.length > 3 && (
                <Typography variant="caption" color="text.secondary">+{payload.active_loops.length - 3}</Typography>
              )}
            </Stack>
          )}
          {payload.active_goals.length > 0 && (
            <Stack direction="row" spacing={0.5} flexWrap="wrap" useFlexGap>
              <Typography variant="caption" color="text.secondary">Goals:</Typography>
              {payload.active_goals.slice(0, 3).map((g) => (
                <Chip key={g.id} label={truncate(g.label, 40)} size="small" variant="outlined" color="secondary" sx={{ fontSize: '0.65rem', height: 18 }} />
              ))}
              {payload.active_goals.length > 3 && (
                <Typography variant="caption" color="text.secondary">+{payload.active_goals.length - 3}</Typography>
              )}
            </Stack>
          )}
        </Box>
      )}

      <Typography variant="caption" color="text.secondary" sx={{ display: 'block', mt: 1, fontSize: '0.65rem' }}>
        ETA: {payload.eta_baseline.source === 'file'
          ? `from ${payload.eta_baseline.window_days}d baseline computed ${payload.eta_baseline.computed_at ? new Date(payload.eta_baseline.computed_at).toLocaleDateString() : 'recently'}`
          : `fallback 30-min default (baseline ${payload.eta_baseline.source})`}
      </Typography>
    </Paper>
  );
};

export default InFlightCard;
