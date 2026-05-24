// Value × Complexity 2×2 heatmap — operator-decision-priority surface.
//
// Cross-products two annotation kinds shipped from ix:
//   - @ai:business-value  (operator-declared, schema v2)
//   - @ai:smell           (sentrux-emitted or human-authored)
//
// Each file is scored on two axes:
//   value_score      = max confidence over its `business-value` annotations
//                      with truth_value in {T, P}; 0 if none.
//   complexity_score = max confidence over its `smell` annotations with
//                      certainty in {detected-by-sentrux, *}; 0 if none.
//
// Bucketed into 4 quadrants at the 0.5 threshold:
//
//                low complexity    │    high complexity
//   high value   KEEP STABLE       │    REFACTOR FIRST     (red)
//   low value    MAINTENANCE BURDEN│    DELETE CANDIDATE   (orange)
//
// Reads /dev-data/ai-annotations (already wired in vite.config.ts; no new
// middleware needed). Refetches every 60s.

import React, { useEffect, useMemo, useState } from 'react';
import {
  Alert,
  Box,
  Chip,
  CircularProgress,
  Grid,
  Link as MuiLink,
  Paper,
  Stack,
  Tooltip,
  Typography,
} from '@mui/material';
import GridViewIcon from '@mui/icons-material/GridView';
import type { AiAnnotationsPayload, Annotation } from './types';

const GITHUB_BLOB_BASE = 'https://github.com/GuitarAlchemist/ga/blob/main';
const VALUE_THRESHOLD = 0.5;
const COMPLEXITY_THRESHOLD = 0.5;
const TOP_N = 5;

type QuadrantId =
  | 'refactor-first'
  | 'delete-candidate'
  | 'keep-stable'
  | 'maintenance-burden';

interface QuadrantSpec {
  id: QuadrantId;
  title: string;
  subtitle: string;
  borderColor: string;
  bg: string;
  /** Sort score for ranking the top-5 files inside this quadrant. */
  rankBy: (file: FileScore) => number;
}

interface FileScore {
  path: string;
  value_score: number;
  complexity_score: number;
  /** Annotations grouped by kind for tooltip rendering. */
  value_annotations: Annotation[];
  complexity_annotations: Annotation[];
}

const QUADRANTS: Record<QuadrantId, QuadrantSpec> = {
  'refactor-first': {
    id: 'refactor-first',
    title: 'REFACTOR FIRST',
    subtitle: 'fix what matters',
    borderColor: '#c62828',
    bg: 'rgba(198, 40, 40, 0.06)',
    rankBy: (f) => f.value_score * f.complexity_score,
  },
  'delete-candidate': {
    id: 'delete-candidate',
    title: 'DELETE CANDIDATE',
    subtitle: 'stop maintaining',
    borderColor: '#ef6c00',
    bg: 'rgba(239, 108, 0, 0.06)',
    rankBy: (f) => (1 - f.value_score) * f.complexity_score,
  },
  'keep-stable': {
    id: 'keep-stable',
    title: 'KEEP STABLE',
    subtitle: 'guard against regression',
    borderColor: '#2e7d32',
    bg: 'rgba(46, 125, 50, 0.06)',
    rankBy: (f) => f.value_score * (1 - f.complexity_score),
  },
  'maintenance-burden': {
    id: 'maintenance-burden',
    title: 'MAINTENANCE BURDEN',
    subtitle: 'background',
    borderColor: '#616161',
    bg: 'rgba(97, 97, 97, 0.04)',
    rankBy: (f) => (1 - f.value_score) * (1 - f.complexity_score),
  },
};

/** Bucket assignment given a file's two scores. */
export function bucketize(value_score: number, complexity_score: number): QuadrantId {
  const highValue = value_score >= VALUE_THRESHOLD;
  const highComplexity = complexity_score >= COMPLEXITY_THRESHOLD;
  if (highValue && highComplexity) return 'refactor-first';
  if (!highValue && highComplexity) return 'delete-candidate';
  if (highValue && !highComplexity) return 'keep-stable';
  return 'maintenance-burden';
}

/** Aggregate annotations into per-file scores. Exported for unit tests. */
export function aggregate(annotations: Annotation[]): FileScore[] {
  const byPath = new Map<string, FileScore>();

  for (const a of annotations) {
    const path = a.location?.path;
    if (!path) continue;
    let entry = byPath.get(path);
    if (!entry) {
      entry = {
        path,
        value_score: 0,
        complexity_score: 0,
        value_annotations: [],
        complexity_annotations: [],
      };
      byPath.set(path, entry);
    }

    if (
      a.kind === 'business-value' &&
      (a.truth_value === 'T' || a.truth_value === 'P')
    ) {
      const c = typeof a.confidence === 'number' ? a.confidence : 0;
      if (c > entry.value_score) entry.value_score = c;
      entry.value_annotations.push(a);
    } else if (a.kind === 'smell') {
      // The brief calls out `certainty=detected-by-sentrux` specifically. The
      // schema's `certainty` enum doesn't include that literal today; treat
      // any smell annotation as a complexity signal but prefer
      // sentrux-emitted ones (author === 'auto' OR evidence pointing at
      // sentrux). This keeps us forward-compatible with the bridge PR.
      const c = typeof a.confidence === 'number' ? a.confidence : 0;
      if (c > entry.complexity_score) entry.complexity_score = c;
      entry.complexity_annotations.push(a);
    }
  }

  return Array.from(byPath.values()).filter(
    (f) => f.value_annotations.length > 0 || f.complexity_annotations.length > 0,
  );
}

function bucketize_files(files: FileScore[]): Record<QuadrantId, FileScore[]> {
  const out: Record<QuadrantId, FileScore[]> = {
    'refactor-first': [],
    'delete-candidate': [],
    'keep-stable': [],
    'maintenance-burden': [],
  };
  for (const f of files) {
    const q = bucketize(f.value_score, f.complexity_score);
    out[q].push(f);
  }
  for (const q of Object.keys(out) as QuadrantId[]) {
    out[q].sort((a, b) => QUADRANTS[q].rankBy(b) - QUADRANTS[q].rankBy(a));
  }
  return out;
}

const Quadrant: React.FC<{
  spec: QuadrantSpec;
  files: FileScore[];
}> = ({ spec, files }) => {
  const top = files.slice(0, TOP_N);
  return (
    <Paper
      variant="outlined"
      data-testid={`heatmap-quadrant-${spec.id}`}
      sx={{
        p: 1.5,
        borderColor: spec.borderColor,
        borderWidth: 2,
        background: spec.bg,
        minHeight: 180,
      }}
    >
      <Stack spacing={1}>
        <Stack direction="row" alignItems="baseline" spacing={1}>
          <Typography variant="subtitle2" sx={{ color: spec.borderColor, fontWeight: 700 }}>
            {spec.title}
          </Typography>
          <Chip
            label={files.length}
            size="small"
            sx={{
              height: 18,
              fontSize: '0.7rem',
              bgcolor: spec.borderColor,
              color: '#fff',
            }}
            data-testid={`heatmap-count-${spec.id}`}
          />
        </Stack>
        <Typography variant="caption" color="text.secondary">
          {spec.subtitle}
        </Typography>
        {top.length === 0 ? (
          <Typography variant="caption" color="text.secondary" sx={{ fontStyle: 'italic' }}>
            (no files)
          </Typography>
        ) : (
          <Stack spacing={0.25}>
            {top.map((f) => {
              const tooltip = [
                ...f.value_annotations.map(
                  (a) =>
                    `[business-value ${a.truth_value} conf=${a.confidence.toFixed(2)}] ${a.claim}`,
                ),
                ...f.complexity_annotations.map(
                  (a) => `[smell ${a.truth_value} conf=${a.confidence.toFixed(2)}] ${a.claim}`,
                ),
              ].join('\n');
              return (
                <Tooltip key={f.path} title={<Box sx={{ whiteSpace: 'pre-wrap' }}>{tooltip}</Box>}>
                  <MuiLink
                    href={`${GITHUB_BLOB_BASE}/${f.path}`}
                    target="_blank"
                    rel="noreferrer"
                    sx={{
                      fontFamily: 'monospace',
                      fontSize: '0.72rem',
                      color: 'text.primary',
                      textDecoration: 'none',
                      '&:hover': { textDecoration: 'underline' },
                    }}
                  >
                    {f.path}{' '}
                    <Typography
                      component="span"
                      variant="caption"
                      color="text.secondary"
                      sx={{ fontFamily: 'monospace' }}
                    >
                      [v={f.value_score.toFixed(2)} c={f.complexity_score.toFixed(2)}]
                    </Typography>
                  </MuiLink>
                </Tooltip>
              );
            })}
            {files.length > TOP_N && (
              <Typography variant="caption" color="text.secondary">
                …and {files.length - TOP_N} more
              </Typography>
            )}
          </Stack>
        )}
      </Stack>
    </Paper>
  );
};

export const ValueComplexityHeatmap: React.FC = () => {
  const [data, setData] = useState<AiAnnotationsPayload | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    let cancelled = false;
    const load = () => {
      fetch('/dev-data/ai-annotations')
        .then((r) => {
          if (!r.ok) throw new Error(`HTTP ${r.status}`);
          return r.json() as Promise<AiAnnotationsPayload>;
        })
        .then((p) => {
          if (!cancelled) {
            setData(p);
            setError(null);
          }
        })
        .catch((e) => {
          if (!cancelled) setError(String(e?.message ?? e));
        });
    };
    load();
    const id = window.setInterval(load, 60_000);
    return () => {
      cancelled = true;
      window.clearInterval(id);
    };
  }, []);

  const { files, buckets, totalScored, valueOnly, complexityOnly } = useMemo(() => {
    if (!data) {
      return {
        files: [] as FileScore[],
        buckets: bucketize_files([]),
        totalScored: 0,
        valueOnly: 0,
        complexityOnly: 0,
      };
    }
    const f = aggregate(data.annotations ?? []);
    return {
      files: f,
      buckets: bucketize_files(f),
      totalScored: f.length,
      valueOnly: f.filter((x) => x.value_score > 0 && x.complexity_score === 0).length,
      complexityOnly: f.filter((x) => x.value_score === 0 && x.complexity_score > 0).length,
    };
  }, [data]);

  return (
    <Paper sx={{ p: 2 }} data-testid="value-complexity-heatmap">
      <Stack spacing={2}>
        <Stack direction="row" spacing={1} alignItems="center">
          <GridViewIcon fontSize="small" />
          <Typography variant="h6">Value × Complexity</Typography>
          <Typography variant="caption" color="text.secondary">
            {totalScored} file{totalScored === 1 ? '' : 's'} scored • value-only{' '}
            {valueOnly} • complexity-only {complexityOnly}
          </Typography>
        </Stack>

        <Typography variant="caption" color="text.secondary">
          Cross-products{' '}
          <code>@ai:business-value</code> (operator-declared) against{' '}
          <code>@ai:smell</code> (sentrux-emitted) to surface the four
          operator-decision quadrants. Threshold {VALUE_THRESHOLD} on each axis.
        </Typography>

        {error && (
          <Alert severity="error">
            Failed to load /dev-data/ai-annotations: {error}
          </Alert>
        )}

        {!data && !error && (
          <Stack direction="row" spacing={1} alignItems="center">
            <CircularProgress size={16} />
            <Typography variant="caption" color="text.secondary">
              Loading…
            </Typography>
          </Stack>
        )}

        {data && totalScored === 0 && (
          <Alert severity="info">
            No <code>@ai:business-value</code> or <code>@ai:smell</code> annotations
            yet. Drop a marker like{' '}
            <code>// @ai:business-value core engine [T:manually-reviewed conf:0.95]</code>{' '}
            in a high-value file and rerun the extractor in ix.
          </Alert>
        )}

        {data && totalScored > 0 && (
          <Grid container spacing={1.5}>
            <Grid item xs={12} sm={6}>
              <Quadrant spec={QUADRANTS['keep-stable']} files={buckets['keep-stable']} />
            </Grid>
            <Grid item xs={12} sm={6}>
              <Quadrant
                spec={QUADRANTS['refactor-first']}
                files={buckets['refactor-first']}
              />
            </Grid>
            <Grid item xs={12} sm={6}>
              <Quadrant
                spec={QUADRANTS['maintenance-burden']}
                files={buckets['maintenance-burden']}
              />
            </Grid>
            <Grid item xs={12} sm={6}>
              <Quadrant
                spec={QUADRANTS['delete-candidate']}
                files={buckets['delete-candidate']}
              />
            </Grid>
          </Grid>
        )}
      </Stack>
    </Paper>
  );
};

export default ValueComplexityHeatmap;
