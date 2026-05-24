// SentruxDsmCard — Design Structure Matrix summary.
//
// Sentrux free tier returns AGGREGATE DSM stats: size, density, edge_count,
// propagation_cost, level_breaks, clusters[]. Pro tier additionally returns
// per-cycle and per-file fan-in/fan-out detail. We render both shapes.
//
// Loads once on mount (DSM is heavy: scans the whole import graph). The
// full NxN matrix is too large for a card — link out to the sentrux desktop
// app for the interactive treemap view.

import React, { useEffect, useState } from 'react';
import {
  Alert,
  Box,
  Chip,
  CircularProgress,
  Divider,
  Paper,
  Stack,
  Typography,
} from '@mui/material';
import AccountTreeIcon from '@mui/icons-material/AccountTree';
import type { SentruxEnvelope, SentruxDsmPayload } from './types';

export const SentruxDsmCard: React.FC = () => {
  const [data, setData] = useState<SentruxEnvelope<SentruxDsmPayload> | null>(null);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    let cancelled = false;
    const load = async () => {
      try {
        const r = await fetch('/dev-data/sentrux/dsm');
        const body = (await r.json()) as SentruxEnvelope<SentruxDsmPayload>;
        if (!cancelled) setData(body);
      } catch (e) {
        if (!cancelled) {
          setData({
            ok: false,
            generated_at: new Date().toISOString(),
            error: String((e as Error)?.message ?? e),
          });
        }
      } finally {
        if (!cancelled) setLoading(false);
      }
    };
    void load();
    return () => { cancelled = true; };
  }, []);

  const payload = data?.data;
  const cycles = (payload?.cycles ?? []).slice(0, 5);
  const hotspots = (payload?.hotspots ?? []).slice(0, 5);
  const topClusters = (payload?.clusters ?? []).slice(0, 5);
  const matrixSize = payload?.matrix_size ?? payload?.size;

  return (
    <Paper sx={{ p: 2 }}>
      <Stack direction="row" spacing={1} alignItems="center" sx={{ mb: 1 }}>
        <AccountTreeIcon fontSize="small" sx={{ color: 'primary.main' }} />
        <Typography variant="h6">Dependency Structure Matrix</Typography>
        {matrixSize != null && (
          <Typography variant="caption" color="text.secondary">
            {matrixSize.toLocaleString()}×{matrixSize.toLocaleString()} import graph
          </Typography>
        )}
      </Stack>

      {loading && <CircularProgress size={18} />}

      {!loading && !data?.ok && (
        <Alert severity="warning">
          <Typography variant="body2">{data?.error ?? 'sentrux dsm unreachable'}</Typography>
          {data?.hint && <Typography variant="caption" color="text.secondary">{data.hint}</Typography>}
        </Alert>
      )}

      {!loading && data?.ok && payload && (
        <>
          {/* Aggregate stats tile — always shown if free-tier shape present */}
          {(payload.edge_count != null || payload.density != null || payload.propagation_cost != null) && (
            <>
              <Stack direction={{ xs: 'column', sm: 'row' }} spacing={3} sx={{ mb: 1.5 }} flexWrap="wrap" useFlexGap>
                <Box>
                  <Typography variant="caption" color="text.secondary">Edges</Typography>
                  <Typography variant="body1">{payload.edge_count?.toLocaleString() ?? '–'}</Typography>
                </Box>
                <Box>
                  <Typography variant="caption" color="text.secondary">Density</Typography>
                  <Typography variant="body1">{payload.density != null ? payload.density.toLocaleString() : '–'}</Typography>
                </Box>
                <Box>
                  <Typography variant="caption" color="text.secondary">Propagation cost</Typography>
                  <Typography variant="body1">{payload.propagation_cost?.toLocaleString() ?? '–'}</Typography>
                </Box>
                <Box>
                  <Typography variant="caption" color="text.secondary">Level breaks</Typography>
                  <Typography variant="body1">{payload.level_breaks?.toLocaleString() ?? '–'}</Typography>
                </Box>
                <Box>
                  <Typography variant="caption" color="text.secondary">Above / below diag</Typography>
                  <Typography variant="body1">
                    {payload.above_diagonal?.toLocaleString() ?? '–'} / {payload.below_diagonal?.toLocaleString() ?? '–'}
                  </Typography>
                </Box>
              </Stack>
              {payload.interpretation && (
                <Alert
                  severity={payload.above_diagonal && payload.above_diagonal > 0 ? 'warning' : 'success'}
                  sx={{ mb: 1.5 }}
                >
                  <Typography variant="body2">{payload.interpretation}</Typography>
                </Alert>
              )}
            </>
          )}

          {/* Pro-tier: cycles and fan-in/out hotspots */}
          {cycles.length > 0 && (
            <Box sx={{ mb: 1.5 }}>
              <Typography variant="subtitle2" sx={{ mb: 0.5 }}>Top cycles</Typography>
              <Stack spacing={0.5}>
                {cycles.map((c, idx) => (
                  <Box key={idx} sx={{ p: 0.75, bgcolor: 'action.hover', borderRadius: 1 }}>
                    <Typography variant="caption" sx={{ fontFamily: 'monospace', fontSize: '0.72rem', wordBreak: 'break-all' }}>
                      {c.text ?? (c.files ?? []).join(' → ')}
                      {c.size != null && <span> (size {c.size})</span>}
                    </Typography>
                  </Box>
                ))}
              </Stack>
            </Box>
          )}

          {hotspots.length > 0 && (
            <Box sx={{ mb: 1.5 }}>
              <Typography variant="subtitle2" sx={{ mb: 0.5 }}>Fan-in / fan-out hotspots</Typography>
              <Stack spacing={0.5}>
                {hotspots.map((h, idx) => (
                  <Stack
                    key={idx}
                    direction="row"
                    spacing={1}
                    alignItems="center"
                    sx={{ p: 0.75, bgcolor: 'action.hover', borderRadius: 1 }}
                  >
                    <Typography variant="caption" sx={{ fontFamily: 'monospace', fontSize: '0.72rem', flex: 1, wordBreak: 'break-all' }}>
                      {h.file}
                    </Typography>
                    <Typography variant="caption" color="text.secondary">in {h.fan_in ?? 0}</Typography>
                    <Typography variant="caption" color="text.secondary">out {h.fan_out ?? 0}</Typography>
                  </Stack>
                ))}
              </Stack>
            </Box>
          )}

          {/* Free-tier: top clusters */}
          {topClusters.length > 0 && cycles.length === 0 && (
            <Box>
              <Typography variant="subtitle2" sx={{ mb: 0.5 }}>Top clusters</Typography>
              <Stack direction="row" spacing={1} flexWrap="wrap" useFlexGap>
                {topClusters.map((c, idx) => (
                  <Chip
                    key={idx}
                    size="small"
                    variant="outlined"
                    label={`${c.files_count ?? '?'} files · ${c.internal_edges ?? 0} edges · L${c.level ?? '?'}`}
                    sx={{ fontSize: '0.7rem' }}
                  />
                ))}
              </Stack>
            </Box>
          )}

          {/* Final fallback for raw text-only output */}
          {!payload.edge_count && cycles.length === 0 && hotspots.length === 0 && topClusters.length === 0 && payload.text && (
            <Typography variant="caption" color="text.secondary" sx={{ whiteSpace: 'pre-wrap', fontFamily: 'monospace', display: 'block' }}>
              {payload.text}
            </Typography>
          )}

          <Divider sx={{ my: 1.5 }} />
          <Typography variant="caption" color="text.secondary">
            Full matrix viz lives in the sentrux desktop app — run <code>sentrux scan</code> locally for the interactive treemap.
          </Typography>
        </>
      )}
    </Paper>
  );
};
