// SentruxNextStepsCard — actionable refactor prescriptions surfaced at the
// top of /test#dev/sentrux. Reads /dev-data/sentrux/next-steps which serves
// state/quality/sentrux-next-steps/latest.md (produced by the
// /sentrux-next-steps skill — see .claude/skills/sentrux-next-steps/SKILL.md).
//
// Why this card is FIRST on the Sentrux tab:
//   The other four cards (Health / Rules / TestGaps / DSM) surface raw
//   measurements. This card surfaces what to DO about them. The most
//   action-oriented surface earns the top slot.
//
// Header shows last-generated timestamp + a Regenerate button (queues the
// /sentrux-next-steps skill via the existing SkillActionButton component;
// an agent picks the request up out of state/harness/skill-invocations.jsonl).
//
// Empty state shows when state/quality/sentrux-next-steps/latest.md is
// absent — first-run onboarding: "Click 'Regenerate' to generate the
// first recommendations."
//
// Refresh cadence: every 60s. The artifact regenerates rarely (daily at
// most), so cheap polling is fine.

import React, { useCallback, useEffect, useState } from 'react';
import {
  Alert,
  Box,
  Chip,
  CircularProgress,
  Paper,
  Stack,
  Typography,
} from '@mui/material';
import LightbulbIcon from '@mui/icons-material/Lightbulb';
import ReactMarkdown from 'react-markdown';
import remarkGfm from 'remark-gfm';
import SkillActionButton from '../Harness/SkillActionButton';

const REFRESH_MS = 60_000;

// ── Types ────────────────────────────────────────────────────────────────
// Mirror the shape returned by /dev-data/sentrux/next-steps. Keep these
// loose because the frontmatter is operator-authored and may include
// additional fields in future revisions; the card renders only what it
// recognizes.

export interface SentruxNextStepsInputs {
  quality_signal?: number;
  cycles?: number;
  coverage_pct?: number;
  bottleneck?: string;
  value_annotations?: number;
  smell_annotations?: number;
  [k: string]: number | string | undefined;
}

export interface SentruxNextStepsPayload {
  empty: boolean;
  schema?: string | null;
  generated_at?: string | null;
  generator?: string | null;
  inputs?: SentruxNextStepsInputs;
  markdown?: string;
  source_path?: string;
  source_mtime?: string;
  hint?: string;
  error?: string;
  fetched_at?: string;
}

// ── Helpers ──────────────────────────────────────────────────────────────

function timeAgo(iso: string | null | undefined): string {
  if (!iso) return '—';
  const t = Date.parse(iso);
  if (Number.isNaN(t)) return iso;
  const delta = Date.now() - t;
  const min = Math.round(delta / 60_000);
  if (min < 1) return 'just now';
  if (min < 60) return `${min}m ago`;
  const h = Math.round(min / 60);
  if (h < 24) return `${h}h ago`;
  const d = Math.round(h / 24);
  return `${d}d ago`;
}

function formatInput(key: string, value: number | string | undefined): string | null {
  if (value === undefined || value === null || value === '') return null;
  switch (key) {
    case 'quality_signal':
      return typeof value === 'number' ? `quality ${value.toLocaleString()}/10000` : `quality ${value}`;
    case 'cycles':
      return typeof value === 'number' ? `${value.toLocaleString()} cycles` : `${value} cycles`;
    case 'coverage_pct':
      return typeof value === 'number' ? `${value.toFixed(1)}% coverage` : `${value}% coverage`;
    case 'bottleneck':
      return `bottleneck: ${value}`;
    case 'value_annotations':
      return typeof value === 'number' && value > 0 ? `${value} value-tagged` : null;
    case 'smell_annotations':
      return typeof value === 'number' && value > 0 ? `${value} smell-tagged` : null;
    default:
      return `${key}: ${value}`;
  }
}

// ── Component ────────────────────────────────────────────────────────────

export const SentruxNextStepsCard: React.FC = () => {
  const [data, setData] = useState<SentruxNextStepsPayload | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);

  const load = useCallback(async () => {
    try {
      const r = await fetch('/dev-data/sentrux/next-steps', { cache: 'no-store' });
      if (!r.ok) throw new Error(`HTTP ${r.status}`);
      const payload = (await r.json()) as SentruxNextStepsPayload;
      setData(payload);
      setError(null);
    } catch (e) {
      setError(String((e as Error)?.message ?? e));
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    void load();
    const id = window.setInterval(() => { void load(); }, REFRESH_MS);
    return () => window.clearInterval(id);
  }, [load]);

  const inputs = data?.inputs ?? {};
  const chips = Object.entries(inputs)
    .map(([k, v]) => formatInput(k, v))
    .filter((s): s is string => s != null);

  return (
    <Paper sx={{ p: 2 }} data-testid="sentrux-next-steps-card">
      <Stack direction={{ xs: 'column', md: 'row' }} spacing={2} alignItems={{ xs: 'flex-start', md: 'center' }} sx={{ mb: 1.5 }}>
        <Stack direction="row" spacing={1} alignItems="center" sx={{ minWidth: 0, flex: 1 }}>
          <LightbulbIcon sx={{ color: 'warning.main' }} />
          <Box sx={{ minWidth: 0 }}>
            <Typography variant="h6">Next Steps</Typography>
            <Typography variant="caption" color="text.secondary">
              Actionable refactor prescriptions from{' '}
              <code>/sentrux-next-steps</code>. Generated{' '}
              {data?.generated_at ? <strong>{timeAgo(data.generated_at)}</strong> : <em>never</em>}
              {data?.generator && <> by <code>{data.generator}</code></>}.
            </Typography>
          </Box>
        </Stack>
        <Stack direction="row" spacing={1} alignItems="center">
          {loading && <CircularProgress size={16} />}
          <SkillActionButton
            skill="sentrux-next-steps"
            label="Regenerate"
            tooltip="Queues /sentrux-next-steps. An agent picks it up, re-reads sentrux + annotations, and rewrites state/quality/sentrux-next-steps/latest.md."
          />
        </Stack>
      </Stack>

      {chips.length > 0 && (
        <Stack direction="row" spacing={1} flexWrap="wrap" useFlexGap sx={{ mb: 1.5 }}>
          {chips.map((c) => (
            <Chip key={c} label={c} size="small" variant="outlined" sx={{ fontSize: '0.7rem' }} />
          ))}
        </Stack>
      )}

      {error && (
        <Alert severity="warning" sx={{ mb: 1, py: 0, fontSize: '0.8rem' }}>
          Failed to load /dev-data/sentrux/next-steps: {error}
        </Alert>
      )}

      {data?.empty && (
        <Alert severity="info" icon={<LightbulbIcon fontSize="small" />} sx={{ fontSize: '0.85rem' }}>
          <Typography variant="body2" sx={{ mb: 0.5 }}>
            <strong>No recommendations yet.</strong>{' '}
            {data.hint ?? "Click 'Regenerate' to generate the first recommendations."}
          </Typography>
          <Typography variant="caption" color="text.secondary">
            See <code>.claude/skills/sentrux-next-steps/SKILL.md</code> for the heuristic.
            Outputs land in <code>state/quality/sentrux-next-steps/</code>.
          </Typography>
        </Alert>
      )}

      {!data?.empty && data?.markdown && (
        <Box
          sx={{
            maxHeight: 600,
            overflow: 'auto',
            fontSize: '0.85rem',
            '& h1': { fontSize: '1.15rem', mt: 0, mb: 1 },
            '& h2': { fontSize: '1.0rem', mt: 2, mb: 0.75, borderBottom: '1px solid', borderColor: 'divider', pb: 0.25 },
            '& h3': { fontSize: '0.9rem', mt: 1.25 },
            '& blockquote': {
              borderLeft: '3px solid',
              borderColor: 'warning.main',
              bgcolor: 'action.hover',
              pl: 1.5,
              py: 0.5,
              my: 1,
              '& p': { my: 0.25, fontSize: '0.85rem' },
            },
            '& ul, & ol': { pl: 3, my: 0.5 },
            '& li': { my: 0.25 },
            '& code': { bgcolor: 'action.hover', px: 0.5, borderRadius: 0.5, fontSize: '0.85em' },
            '& pre': { bgcolor: 'action.hover', p: 1, borderRadius: 1, overflow: 'auto', fontSize: '0.78rem' },
            '& strong': { fontWeight: 600 },
            '& table': { borderCollapse: 'collapse', width: '100%', my: 1, fontSize: '0.78rem' },
            '& th, & td': { border: '1px solid', borderColor: 'divider', px: 0.75, py: 0.4, textAlign: 'left' },
          }}
        >
          <ReactMarkdown remarkPlugins={[remarkGfm]}>{data.markdown}</ReactMarkdown>
        </Box>
      )}

      {data?.source_path && (
        <Typography variant="caption" color="text.secondary" sx={{ display: 'block', mt: 1.5 }}>
          source: <code>{data.source_path}</code>
          {data.source_mtime && <> · last updated {timeAgo(data.source_mtime)}</>}
        </Typography>
      )}
    </Paper>
  );
};

export default SentruxNextStepsCard;
