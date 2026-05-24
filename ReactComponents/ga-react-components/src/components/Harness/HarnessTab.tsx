// HarnessTab — the orchestrator that replaces the old HarnessCard inline
// in DevelopmentSection.tsx. Sections, in order:
//
//   1. Header banner: donut ring + plan link + last-updated stamp
//   2. Timeline strip (clickable pills)
//   3. Action bar: "Invoke skill" buttons for the four shipped skills
//   4. Item grid: responsive cards (xs=12 sm=6 md=4), one per item
//   5. Baseline metrics tiles (kept from previous design)
//   6. Principles refresher (kept from previous design)
//   7. Related PRs (kept from previous design)

import React, { useEffect, useMemo, useState } from 'react';
import {
  Alert,
  Box,
  Button,
  Chip,
  CircularProgress,
  Grid,
  Link as MuiLink,
  Paper,
  Stack,
  Typography,
} from '@mui/material';
import ConstructionIcon from '@mui/icons-material/Construction';
import OpenInNewIcon from '@mui/icons-material/OpenInNew';
import UnfoldMoreIcon from '@mui/icons-material/UnfoldMore';
import UnfoldLessIcon from '@mui/icons-material/UnfoldLess';
import type { HarnessPayload } from './types';
import { HarnessDonut } from './HarnessDonut';
import { HarnessTimeline } from './HarnessTimeline';
import { HarnessItemCard } from './HarnessItemCard';
import { SkillActionButton } from './SkillActionButton';

// Skills exposed at the top action bar. These mirror what's been shipped
// in items #5, #6, #11, #13. Adding a new skill here is a one-line edit
// (and the queue endpoint accepts any kebab-case name).
const TOP_SKILLS: { skill: string; label: string; tooltip: string }[] = [
  { skill: 'grade-last-pr', label: '/grade-last-pr', tooltip: 'Re-run octo:review against the most recent merged PR.' },
  { skill: 'council', label: '/council', tooltip: 'Spawn the 5-persona advisory council for the current one-way-door PR.' },
  { skill: 'backlog-groom', label: '/backlog-groom', tooltip: 'Rank the top-3 work items from BACKLOG.md for the next session.' },
  { skill: 'test-plan', label: '/test-plan', tooltip: 'Generate a structured test plan for the current PR.' },
];

export const HarnessTab: React.FC = () => {
  const [data, setData] = useState<HarnessPayload | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [expandAll, setExpandAll] = useState(false);
  const [expandTick, setExpandTick] = useState(0); // bump to force remount on toggle

  useEffect(() => {
    let cancelled = false;
    fetch('/dev-data/harness')
      .then((r) => {
        if (!r.ok) throw new Error(`HTTP ${r.status}`);
        return r.json() as Promise<HarnessPayload>;
      })
      .then((p) => { if (!cancelled) setData(p); })
      .catch((e) => { if (!cancelled) setError(String(e?.message ?? e)); });
    return () => { cancelled = true; };
  }, []);

  const sortedItems = useMemo(() => {
    if (!data) return [];
    return [...data.items].sort((a, b) => a.number - b.number);
  }, [data]);

  const handleToggleExpand = () => {
    setExpandAll((v) => !v);
    setExpandTick((t) => t + 1);
  };

  return (
    <Stack spacing={2}>
      {/* ── 1. Header banner: donut + plan link + last-updated ────── */}
      <Paper sx={{ p: 2 }}>
        <Stack direction={{ xs: 'column', md: 'row' }} spacing={2} alignItems={{ xs: 'flex-start', md: 'center' }}>
          {data ? (
            <HarnessDonut items={data.items} />
          ) : (
            <Box sx={{ width: 140, height: 140, display: 'flex', alignItems: 'center', justifyContent: 'center' }}>
              <CircularProgress size={24} />
            </Box>
          )}
          <Box sx={{ flex: 1, minWidth: 0 }}>
            <Stack direction="row" spacing={1} alignItems="center" sx={{ mb: 0.5 }}>
              <ConstructionIcon fontSize="small" sx={{ color: 'primary.main' }} />
              <Typography variant="h6">Harness engineering adoption</Typography>
              {data?.schema_version && (
                <Chip label={`v${data.schema_version}`} size="small" variant="outlined" sx={{ fontSize: '0.65rem', height: 18 }} />
              )}
            </Stack>
            <Typography variant="body2" color="text.secondary" sx={{ mb: 1 }}>
              Industry-canonical agent-harness techniques (Anthropic, OpenAI, LangChain, Fowler,
              claude-codex-forge) graded against GA&apos;s multi-agent stack. Pick items by hand; each is
              independent. Full plan:{' '}
              <MuiLink
                href="https://github.com/GuitarAlchemist/ga/blob/main/docs/plans/2026-05-23-arch-harness-engineering-adoption-plan.md"
                target="_blank"
                rel="noopener noreferrer"
                sx={{ display: 'inline-flex', alignItems: 'center', gap: 0.3 }}
              >
                2026-05-23-arch-harness-engineering-adoption-plan.md <OpenInNewIcon fontSize="inherit" />
              </MuiLink>
              .
            </Typography>
            {data?.last_updated && (
              <Typography variant="caption" color="text.secondary" sx={{ display: 'block' }}>
                items.json last updated {data.last_updated}
              </Typography>
            )}
            {error && <Alert severity="error" sx={{ mt: 1 }}>Failed to load /dev-data/harness: {error}</Alert>}
          </Box>
        </Stack>
      </Paper>

      {/* ── 2. Timeline ──────────────────────────────────────────── */}
      {data && (
        <Paper sx={{ p: 2 }}>
          <Typography variant="subtitle2" sx={{ fontWeight: 600, mb: 1 }}>
            Shipment timeline
          </Typography>
          <Typography variant="caption" color="text.secondary" sx={{ display: 'block', mb: 1 }}>
            Each pill is a harness item, positioned at its merge date. Click to jump to its card.
          </Typography>
          <HarnessTimeline items={data.items} />
        </Paper>
      )}

      {/* ── 3. Action bar: top-level skill invocations ──────────── */}
      <Paper sx={{ p: 2 }}>
        <Stack direction={{ xs: 'column', sm: 'row' }} spacing={1} alignItems={{ xs: 'flex-start', sm: 'center' }}>
          <Box sx={{ flex: 1 }}>
            <Typography variant="subtitle2" sx={{ fontWeight: 600 }}>Skill actions</Typography>
            <Typography variant="caption" color="text.secondary">
              Click to queue an invocation into <Box component="code" sx={{ px: 0.5, bgcolor: 'action.hover', borderRadius: 0.5, fontSize: '0.7rem' }}>state/harness/skill-invocations.jsonl</Box>.
              The button writes the queue line; an agent session runs the skill.
            </Typography>
          </Box>
          <Stack direction="row" spacing={1} flexWrap="wrap" useFlexGap>
            {TOP_SKILLS.map(({ skill, label, tooltip }) => (
              <SkillActionButton key={skill} skill={skill} label={label} tooltip={tooltip} variant="outlined" />
            ))}
          </Stack>
        </Stack>
      </Paper>

      {/* ── 4. Item grid ─────────────────────────────────────────── */}
      {data && (
        <Box>
          <Stack direction="row" spacing={1} alignItems="center" sx={{ mb: 1.5 }}>
            <Typography variant="subtitle2" sx={{ fontWeight: 600, flex: 1 }}>
              Items ({sortedItems.length})
            </Typography>
            <Button
              size="small"
              variant="text"
              startIcon={expandAll ? <UnfoldLessIcon /> : <UnfoldMoreIcon />}
              onClick={handleToggleExpand}
              sx={{ textTransform: 'none', fontSize: '0.75rem' }}
            >
              {expandAll ? 'collapse all' : 'expand all'}
            </Button>
          </Stack>
          <Grid container spacing={1.5}>
            {sortedItems.map((item) => (
              <Grid item xs={12} sm={6} md={4} key={`${item.number}-${expandTick}`}>
                <HarnessItemCard item={item} defaultExpanded={expandAll} />
              </Grid>
            ))}
          </Grid>
        </Box>
      )}

      {/* ── 5. Baselines (kept from previous design) ─────────────── */}
      {data?.baselines && (
        <Paper sx={{ p: 2 }}>
          <Typography variant="subtitle2" sx={{ fontWeight: 600, mb: 1 }}>Baseline metrics</Typography>
          <Typography variant="caption" color="text.secondary" sx={{ display: 'block', mb: 1.5 }}>
            Concrete numbers from the plan. Statically authored in <Box component="code" sx={{ px: 0.5, bgcolor: 'action.hover', borderRadius: 0.5, fontSize: '0.8rem' }}>state/harness/items.json</Box>; metric-collection automation is incremental.
          </Typography>
          <Grid container spacing={1.5}>
            {Object.entries(data.baselines).map(([key, b]) => {
              const numericValue = typeof b.value === 'number' ? b.value : null;
              const numericTarget = typeof b.target === 'number' ? b.target : null;
              const onTrack =
                numericValue != null && numericTarget != null
                  ? key === 'frontend_typecheck_errors' || key === 'algedonic_signals_unacked'
                    ? numericValue <= numericTarget
                    : numericValue >= numericTarget
                  : null;
              return (
                <Grid item xs={12} sm={6} md={4} key={key}>
                  <Box sx={{ p: 1.5, bgcolor: 'action.hover', borderRadius: 1, height: '100%', borderLeft: 3, borderColor: onTrack ? 'success.main' : onTrack === false ? 'warning.main' : 'divider' }}>
                    <Typography variant="caption" color="text.secondary" sx={{ display: 'block', fontFamily: 'monospace' }}>
                      {key}
                    </Typography>
                    <Stack direction="row" spacing={1} alignItems="baseline" sx={{ mt: 0.5 }}>
                      <Typography variant="h6" sx={{ fontWeight: 600 }}>{String(b.value)}</Typography>
                      {b.target !== undefined && (
                        <Typography variant="caption" color="text.secondary">
                          / target: {String(b.target)}
                        </Typography>
                      )}
                    </Stack>
                    {b.note && (
                      <Typography variant="caption" color="text.secondary" sx={{ display: 'block', mt: 0.5 }}>
                        {b.note}
                      </Typography>
                    )}
                    {b.urls && b.urls.length > 0 && (
                      <Typography variant="caption" sx={{ display: 'block', mt: 0.5, fontFamily: 'monospace', fontSize: '0.7rem' }}>
                        {b.urls.join(' · ')}
                      </Typography>
                    )}
                    {b.as_of && (
                      <Typography variant="caption" color="text.disabled" sx={{ display: 'block', mt: 0.25, fontSize: '0.65rem' }}>
                        as of {b.as_of}
                      </Typography>
                    )}
                  </Box>
                </Grid>
              );
            })}
          </Grid>
        </Paper>
      )}

      {/* ── 6. Principles refresher ──────────────────────────────── */}
      {data?.principles && data.principles.length > 0 && (
        <Paper sx={{ p: 2 }}>
          <Typography variant="subtitle2" sx={{ fontWeight: 600, mb: 1 }}>Principles refresher</Typography>
          <Typography variant="caption" color="text.secondary" sx={{ display: 'block', mb: 1.5 }}>
            The 4 rule families from the plan. Educates without forcing a full read of the source document.
          </Typography>
          <Grid container spacing={1.5}>
            {data.principles.map((p) => (
              <Grid item xs={12} sm={6} key={p.name}>
                <Box sx={{ p: 1.5, bgcolor: 'action.hover', borderRadius: 1, height: '100%', borderLeft: 3, borderColor: 'primary.main' }}>
                  <Typography variant="subtitle2" sx={{ fontWeight: 600, mb: 0.5 }}>{p.name}</Typography>
                  <Box component="ul" sx={{ pl: 2.5, m: 0, '& li': { mb: 0.5 } }}>
                    {p.techniques.map((t, i) => (
                      <li key={i}>
                        <Typography variant="caption" color="text.secondary">{t}</Typography>
                      </li>
                    ))}
                  </Box>
                </Box>
              </Grid>
            ))}
          </Grid>
        </Paper>
      )}

      {/* ── 7. Related PRs ───────────────────────────────────────── */}
      {data?.related_prs && data.related_prs.length > 0 && (
        <Paper sx={{ p: 2 }}>
          <Typography variant="subtitle2" sx={{ fontWeight: 600, mb: 1 }}>Related PRs</Typography>
          <Stack spacing={1}>
            {data.related_prs.map((pr) => (
              <Stack key={pr.number} direction="row" spacing={1} alignItems="center" sx={{ py: 0.5 }}>
                <MuiLink
                  href={pr.evidence_url}
                  target="_blank"
                  rel="noopener noreferrer"
                  sx={{ fontFamily: 'monospace', fontSize: '0.8rem', flexShrink: 0 }}
                >
                  #{pr.number}
                </MuiLink>
                <Typography variant="body2" sx={{ flex: 1 }}>{pr.title}</Typography>
                <Chip
                  label={pr.state}
                  size="small"
                  color={pr.state === 'merged' ? 'success' : pr.state.includes('blocked') ? 'warning' : 'default'}
                  sx={{ fontSize: '0.7rem', height: 20 }}
                />
              </Stack>
            ))}
          </Stack>
        </Paper>
      )}

      {!data && !error && (
        <Stack direction="row" spacing={1} alignItems="center">
          <CircularProgress size={20} />
          <Typography variant="caption" color="text.secondary">Loading /dev-data/harness…</Typography>
        </Stack>
      )}
    </Stack>
  );
};

export default HarnessTab;
