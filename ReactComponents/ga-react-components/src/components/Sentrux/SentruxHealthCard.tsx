// SentruxHealthCard — top tile of the Sentrux tab.
//
// Renders:
//   * Status pill (green if reachable, red if not)
//   * Quality signal (0-10000, sentrux's geometric-mean composite)
//   * Bottleneck dimension + per-cause scores
//   * "Trigger rescan" button (POSTs /actions/sentrux/rescan)
//
// The endpoint /dev-data/sentrux/health spawns sentrux.exe as a one-shot MCP
// stdio child, runs initialize → scan → health, and returns the inner payload.
// First scan after boot is slow (~5-10s on the ga repo); subsequent /health
// calls don't re-scan unless rescan is invoked.

import React, { useCallback, useEffect, useState } from 'react';
import {
  Alert,
  Box,
  Button,
  Chip,
  CircularProgress,
  Divider,
  LinearProgress,
  Paper,
  Stack,
  Typography,
} from '@mui/material';
import SensorsIcon from '@mui/icons-material/Sensors';
import RefreshIcon from '@mui/icons-material/Refresh';
import CheckCircleIcon from '@mui/icons-material/CheckCircle';
import ErrorOutlineIcon from '@mui/icons-material/ErrorOutline';
import type { SentruxEnvelope, SentruxHealth } from './types';

const REFRESH_MS = 30_000;

export const SentruxHealthCard: React.FC = () => {
  const [data, setData] = useState<SentruxEnvelope<SentruxHealth> | null>(null);
  const [loading, setLoading] = useState(false);
  const [rescanBusy, setRescanBusy] = useState(false);
  const [rescanMessage, setRescanMessage] = useState<string | null>(null);

  const fetchHealth = useCallback(async () => {
    setLoading(true);
    try {
      const r = await fetch('/dev-data/sentrux/health');
      const body = (await r.json()) as SentruxEnvelope<SentruxHealth>;
      setData(body);
    } catch (e) {
      setData({
        ok: false,
        generated_at: new Date().toISOString(),
        error: String((e as Error)?.message ?? e),
      });
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    void fetchHealth();
    const id = window.setInterval(() => { void fetchHealth(); }, REFRESH_MS);
    return () => window.clearInterval(id);
  }, [fetchHealth]);

  const triggerRescan = useCallback(async () => {
    setRescanBusy(true);
    setRescanMessage(null);
    try {
      const r = await fetch('/actions/sentrux/rescan', { method: 'POST' });
      const body = (await r.json()) as { queued?: boolean; scan_id?: string; error?: string };
      if (r.ok && body.queued) {
        setRescanMessage(`Rescan queued (id ${body.scan_id ?? 'unknown'})`);
        // Pull a fresh health probe shortly after the rescan completes.
        window.setTimeout(() => { void fetchHealth(); }, 4_000);
      } else {
        setRescanMessage(body.error ?? 'rescan failed');
      }
    } catch (e) {
      setRescanMessage(`rescan failed: ${(e as Error)?.message ?? e}`);
    } finally {
      setRescanBusy(false);
    }
  }, [fetchHealth]);

  const up = !!data?.ok;
  const h = data?.data;

  return (
    <Paper sx={{ p: 2 }}>
      <Stack direction={{ xs: 'column', md: 'row' }} spacing={2} alignItems={{ xs: 'flex-start', md: 'center' }}>
        <Stack direction="row" spacing={1} alignItems="center" sx={{ minWidth: 0, flex: 1 }}>
          <SensorsIcon sx={{ color: up ? 'success.main' : 'error.main' }} />
          <Box sx={{ minWidth: 0 }}>
            <Stack direction="row" spacing={1} alignItems="center">
              <Typography variant="h6">Sentrux</Typography>
              <Chip
                size="small"
                icon={up ? <CheckCircleIcon /> : <ErrorOutlineIcon />}
                color={up ? 'success' : 'error'}
                label={up ? 'Online' : 'Unreachable'}
                sx={{ fontSize: '0.7rem', height: 20 }}
              />
              {h?.version && (
                <Chip label={`v${h.version}`} size="small" variant="outlined" sx={{ fontSize: '0.65rem', height: 18 }} />
              )}
            </Stack>
            <Typography variant="caption" color="text.secondary">
              Realtime structural-quality sensor. Live treemap, regression gate, file-watcher feedback.
            </Typography>
          </Box>
        </Stack>

        <Stack direction="row" spacing={1} alignItems="center">
          {loading && <CircularProgress size={16} />}
          <Button
            size="small"
            variant="outlined"
            startIcon={<RefreshIcon />}
            disabled={rescanBusy}
            onClick={() => { void triggerRescan(); }}
          >
            {rescanBusy ? 'Rescanning…' : 'Trigger rescan'}
          </Button>
        </Stack>
      </Stack>

      {rescanMessage && (
        <Alert severity="info" sx={{ mt: 1.5 }}>{rescanMessage}</Alert>
      )}

      {!up && data?.error && (
        <Alert severity="warning" sx={{ mt: 1.5 }}>
          <Typography variant="body2"><strong>Sentrux unreachable:</strong> {data.error}</Typography>
          {data.hint && <Typography variant="caption" color="text.secondary">{data.hint}</Typography>}
        </Alert>
      )}

      {up && h && (
        <>
          <Divider sx={{ my: 1.5 }} />
          <Stack direction={{ xs: 'column', sm: 'row' }} spacing={3}>
            <Box sx={{ minWidth: 180 }}>
              <Typography variant="caption" color="text.secondary">Quality signal</Typography>
              <Typography variant="h4">
                {typeof h.quality_signal === 'number' ? h.quality_signal.toLocaleString() : '–'}
                <Typography variant="caption" component="span" color="text.secondary" sx={{ ml: 1 }}>
                  / 10000
                </Typography>
              </Typography>
              {typeof h.quality_signal === 'number' && (
                <LinearProgress
                  variant="determinate"
                  value={Math.min(100, (h.quality_signal / 10000) * 100)}
                  sx={{ mt: 0.5, height: 6, borderRadius: 3 }}
                />
              )}
            </Box>
            <Box>
              <Typography variant="caption" color="text.secondary">Bottleneck</Typography>
              <Typography variant="body1">{h.bottleneck ?? '—'}</Typography>
              <Typography variant="caption" color="text.secondary">
                {typeof h.cross_module_edges === 'number' && typeof h.total_import_edges === 'number'
                  ? `${h.cross_module_edges} / ${h.total_import_edges} cross-module edges`
                  : ''}
              </Typography>
            </Box>
            <Box sx={{ flex: 1 }}>
              <Typography variant="caption" color="text.secondary">Files / lines</Typography>
              <Typography variant="body1">
                {h.files != null ? h.files.toLocaleString() : '—'}{' files, '}
                {h.lines != null ? h.lines.toLocaleString() : '—'}{' lines'}
              </Typography>
              {h.last_scan_at && (
                <Typography variant="caption" color="text.secondary">
                  Last scan {new Date(h.last_scan_at).toLocaleString()}
                </Typography>
              )}
            </Box>
          </Stack>
          {h.root_causes && (
            <Box sx={{ mt: 1.5 }}>
              <Typography variant="caption" color="text.secondary">Root causes</Typography>
              <Stack direction="row" spacing={1} flexWrap="wrap" sx={{ mt: 0.5 }}>
                {Object.entries(h.root_causes).map(([k, v]) => (
                  <Chip
                    key={k}
                    size="small"
                    label={`${k}: ${typeof v?.score === 'number' ? v.score.toLocaleString() : '?'}`}
                    color={h.bottleneck === k ? 'warning' : 'default'}
                    variant={h.bottleneck === k ? 'filled' : 'outlined'}
                    sx={{ fontSize: '0.7rem' }}
                  />
                ))}
              </Stack>
            </Box>
          )}
        </>
      )}
    </Paper>
  );
};
