// AI Annotations card — surfaces the @ai: in-source markers shipped from
// ix (docs/contracts/2026-05-24-ai-annotation.contract.md).
//
// Data flow:
//   ix-ai-annotations writes JSONL  ->  ix-ai-annotations-reconcile.json
//   /dev-data/ai-annotations (vite middleware) reads both and serves a
//   merged payload  ->  this card.
//
// The card is intentionally read-only. Filtering is client-side over the
// payload; we refetch every 60s.

import React, { useEffect, useMemo, useState } from 'react';
import {
  Alert,
  Box,
  Chip,
  CircularProgress,
  Link as MuiLink,
  Paper,
  Stack,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
  Tooltip,
  Typography,
} from '@mui/material';
import RuleIcon from '@mui/icons-material/Rule';
import type {
  Annotation,
  AiAnnotationsPayload,
  Certainty,
  TruthValue,
  AnnotationKind,
} from './types';

const TRUTH_VALUES: TruthValue[] = ['T', 'P', 'U', 'D', 'F', 'C'];

// Demerzel-aligned colors. Tightly bounded palette so the eye groups
// "verified vs. unknown vs. trouble" without effort.
const TRUTH_COLORS: Record<TruthValue, { color: 'success' | 'info' | 'default' | 'warning' | 'error'; label: string }> = {
  T: { color: 'success', label: 'True (verified)' },
  P: { color: 'info', label: 'Probable (leans true)' },
  U: { color: 'default', label: 'Unknown (insufficient evidence)' },
  D: { color: 'warning', label: 'Doubtful (leans false)' },
  F: { color: 'error', label: 'False (refuted)' },
  C: { color: 'error', label: 'Contradictory (conflicting)' },
};

const CERTAINTY_VALUES: Certainty[] = [
  'test',
  'formal-proof',
  'manually-reviewed',
  'assumed',
  'uncertain',
  'inferred',
  'dismissed',
];

const KIND_VALUES: AnnotationKind[] = [
  'invariant',
  'assumption',
  'hypothesis',
  'contract',
  'smell',
  'decision',
  'hint',
];

function confidencePill(c: number): string {
  if (c >= 0.9) return 'autonomous';
  if (c >= 0.7) return 'with-note';
  if (c >= 0.5) return 'confirm';
  if (c >= 0.3) return 'escalate';
  return 'do-not-act';
}

function truncate(s: string, n: number): string {
  return s.length > n ? `${s.slice(0, n - 1)}…` : s;
}

export const AiAnnotationsCard: React.FC = () => {
  const [data, setData] = useState<AiAnnotationsPayload | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [truthFilter, setTruthFilter] = useState<Set<TruthValue>>(new Set(TRUTH_VALUES));
  const [certaintyFilter, setCertaintyFilter] = useState<Set<Certainty>>(new Set(CERTAINTY_VALUES));
  const [kindFilter, setKindFilter] = useState<Set<AnnotationKind>>(new Set(KIND_VALUES));

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

  const visible = useMemo(() => {
    if (!data) return [];
    return data.annotations.filter(
      (a) =>
        truthFilter.has(a.truth_value) &&
        certaintyFilter.has(a.certainty) &&
        kindFilter.has(a.kind),
    );
  }, [data, truthFilter, certaintyFilter, kindFilter]);

  const verifiedPct = useMemo(() => {
    if (!data || data.total === 0) return 0;
    return Math.round(((data.verified_by_test ?? 0) / data.total) * 100);
  }, [data]);

  const trouble = useMemo(() => {
    if (!data) return 0;
    return (data.by_truth_value?.['C'] ?? 0) + (data.by_truth_value?.['F'] ?? 0);
  }, [data]);

  const toggleSet = <T extends string>(set: Set<T>, value: T, setter: (s: Set<T>) => void) => {
    const next = new Set(set);
    if (next.has(value)) next.delete(value);
    else next.add(value);
    setter(next);
  };

  return (
    <Paper sx={{ p: 2 }}>
      <Stack spacing={2}>
        <Stack direction="row" spacing={1} alignItems="center">
          <RuleIcon fontSize="small" />
          <Typography variant="h6">AI Annotations</Typography>
          {data && (
            <Typography variant="caption" color="text.secondary">
              {data.total} markers • verified {verifiedPct}% • trouble {trouble} •{' '}
              generated {new Date(data.generated_at).toLocaleString()}
            </Typography>
          )}
        </Stack>

        {error && (
          <Alert severity="error">
            Failed to load /dev-data/ai-annotations: {error}
          </Alert>
        )}

        {!data && !error && (
          <Stack direction="row" spacing={1} alignItems="center">
            <CircularProgress size={16} />
            <Typography variant="caption" color="text.secondary">
              Loading /dev-data/ai-annotations…
            </Typography>
          </Stack>
        )}

        {data?.empty && (
          <Alert severity="info">
            No annotations extracted yet. Drop <code>// @ai:invariant ... [T:test conf:0.95]</code>{' '}
            markers in source files, then run <code>cargo run -p ix-ai-annotations</code> from the
            ix repo.{' '}
            <MuiLink
              href="https://github.com/GuitarAlchemist/ix/blob/main/docs/contracts/2026-05-24-ai-annotation.contract.md"
              target="_blank"
              rel="noreferrer"
            >
              See the contract
            </MuiLink>
            .
          </Alert>
        )}

        {data && !data.empty && (
          <>
            {/* Top stats */}
            <Stack direction="row" spacing={2} flexWrap="wrap">
              <Tooltip title="Annotations with truth_value=T AND a matched test file">
                <Chip
                  label={`% verified-by-test ${verifiedPct}%`}
                  color="success"
                  variant="outlined"
                />
              </Tooltip>
              <Tooltip title="P (probable) + U (unknown). Need investigation.">
                <Chip
                  label={`P+U: ${(data.by_truth_value?.['P'] ?? 0) +
                    (data.by_truth_value?.['U'] ?? 0)}`}
                  color="info"
                  variant="outlined"
                />
              </Tooltip>
              <Tooltip title="C (contradictory) + F (refuted). Block merges unless waived.">
                <Chip label={`C+F: ${trouble}`} color="error" variant="outlined" />
              </Tooltip>
              <Tooltip title="File touched after annotation was last updated.">
                <Chip label={`stale: ${data.stale ?? 0}`} color="warning" variant="outlined" />
              </Tooltip>
            </Stack>

            {/* Filters */}
            <Stack spacing={1}>
              <Box>
                <Typography variant="caption" color="text.secondary">
                  truth value
                </Typography>
                <Stack direction="row" spacing={0.5} flexWrap="wrap" sx={{ mt: 0.5 }}>
                  {TRUTH_VALUES.map((t) => (
                    <Tooltip key={t} title={TRUTH_COLORS[t].label}>
                      <Chip
                        label={`${t} (${data.by_truth_value?.[t] ?? 0})`}
                        size="small"
                        color={TRUTH_COLORS[t].color}
                        variant={truthFilter.has(t) ? 'filled' : 'outlined'}
                        onClick={() => toggleSet(truthFilter, t, setTruthFilter)}
                      />
                    </Tooltip>
                  ))}
                </Stack>
              </Box>
              <Box>
                <Typography variant="caption" color="text.secondary">
                  certainty
                </Typography>
                <Stack direction="row" spacing={0.5} flexWrap="wrap" sx={{ mt: 0.5 }}>
                  {CERTAINTY_VALUES.map((c) => (
                    <Chip
                      key={c}
                      label={`${c} (${data.by_certainty?.[c] ?? 0})`}
                      size="small"
                      variant={certaintyFilter.has(c) ? 'filled' : 'outlined'}
                      onClick={() => toggleSet(certaintyFilter, c, setCertaintyFilter)}
                    />
                  ))}
                </Stack>
              </Box>
              <Box>
                <Typography variant="caption" color="text.secondary">
                  kind
                </Typography>
                <Stack direction="row" spacing={0.5} flexWrap="wrap" sx={{ mt: 0.5 }}>
                  {KIND_VALUES.map((k) => (
                    <Chip
                      key={k}
                      label={`${k} (${data.by_kind?.[k] ?? 0})`}
                      size="small"
                      variant={kindFilter.has(k) ? 'filled' : 'outlined'}
                      onClick={() => toggleSet(kindFilter, k, setKindFilter)}
                    />
                  ))}
                </Stack>
              </Box>
            </Stack>

            <TableContainer component={Paper} variant="outlined" sx={{ maxHeight: 480 }}>
              <Table size="small" stickyHeader>
                <TableHead>
                  <TableRow>
                    <TableCell>File:Line</TableCell>
                    <TableCell>Kind</TableCell>
                    <TableCell>Claim</TableCell>
                    <TableCell>Truth</TableCell>
                    <TableCell>Certainty</TableCell>
                    <TableCell align="right">Conf</TableCell>
                    <TableCell>Source</TableCell>
                  </TableRow>
                </TableHead>
                <TableBody>
                  {visible.map((a: Annotation) => (
                    <TableRow key={a.id} hover>
                      <TableCell sx={{ fontFamily: 'monospace', fontSize: '0.75rem' }}>
                        {a.location.path}:{a.location.line_start}
                        {a.stale && (
                          <Chip
                            label="stale"
                            color="warning"
                            size="small"
                            sx={{ ml: 0.5, height: 16, fontSize: '0.6rem' }}
                          />
                        )}
                      </TableCell>
                      <TableCell>
                        <Chip label={a.kind} size="small" variant="outlined" />
                      </TableCell>
                      <TableCell title={a.claim}>{truncate(a.claim, 80)}</TableCell>
                      <TableCell>
                        <Tooltip title={TRUTH_COLORS[a.truth_value].label}>
                          <Chip
                            label={a.truth_value}
                            size="small"
                            color={TRUTH_COLORS[a.truth_value].color}
                          />
                        </Tooltip>
                      </TableCell>
                      <TableCell>
                        <Typography variant="caption">{a.certainty}</Typography>
                      </TableCell>
                      <TableCell align="right">
                        <Tooltip title={confidencePill(a.confidence)}>
                          <Typography variant="caption" sx={{ fontFamily: 'monospace' }}>
                            {a.confidence.toFixed(2)}
                          </Typography>
                        </Tooltip>
                      </TableCell>
                      <TableCell>
                        <Typography variant="caption">{a.source.author}</Typography>
                        {a.source.evidence && (
                          <Typography
                            variant="caption"
                            color="text.secondary"
                            sx={{ display: 'block', fontFamily: 'monospace' }}
                          >
                            {truncate(a.source.evidence, 40)}
                          </Typography>
                        )}
                      </TableCell>
                    </TableRow>
                  ))}
                  {visible.length === 0 && (
                    <TableRow>
                      <TableCell colSpan={7} align="center">
                        <Typography variant="caption" color="text.secondary">
                          No annotations match the current filters.
                        </Typography>
                      </TableCell>
                    </TableRow>
                  )}
                </TableBody>
              </Table>
            </TableContainer>
          </>
        )}
      </Stack>
    </Paper>
  );
};

export default AiAnnotationsCard;
