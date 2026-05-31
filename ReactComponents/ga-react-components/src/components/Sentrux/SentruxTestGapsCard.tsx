// SentruxTestGapsCard — surfaces test coverage / risky untested files.
//
// Sentrux free tier returns AGGREGATE coverage stats (source_files,
// test_files, untested, coverage_score). Pro tier additionally returns
// a per-file `files[]` array of the riskiest untested sources
// (churn × complexity). We render both shapes:
//   * Aggregate: coverage ratio + counts in a tile grid
//   * Per-file: top-10 table sorted by risk_score
//
// Refreshes every 5 minutes (coverage moves slowly).

import React, { useEffect, useState } from 'react';
import {
  Alert,
  Box,
  CircularProgress,
  LinearProgress,
  Paper,
  Stack,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
  Typography,
} from '@mui/material';
import BugReportIcon from '@mui/icons-material/BugReport';
import type { SentruxEnvelope, SentruxTestGap, SentruxTestGapsPayload } from './types';

const REFRESH_MS = 5 * 60 * 1000;

export const SentruxTestGapsCard: React.FC = () => {
  const [data, setData] = useState<SentruxEnvelope<SentruxTestGapsPayload> | null>(null);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    let cancelled = false;
    const load = async () => {
      try {
        const r = await fetch('/dev-data/sentrux/test-gaps');
        const body = (await r.json()) as SentruxEnvelope<SentruxTestGapsPayload>;
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
    const id = window.setInterval(() => { void load(); }, REFRESH_MS);
    return () => { cancelled = true; window.clearInterval(id); };
  }, []);

  const payload = data?.data;
  const files: SentruxTestGap[] = payload?.files ?? [];
  const topTen = files.slice(0, 10);
  const total = payload?.total_untested ?? payload?.untested ?? files.length;
  const coverageRatio = payload?.coverage_score;

  return (
    <Paper sx={{ p: 2 }}>
      <Stack direction="row" spacing={1} alignItems="center" sx={{ mb: 1 }}>
        <BugReportIcon fontSize="small" sx={{ color: 'primary.main' }} />
        <Typography variant="h6">Test gaps</Typography>
        <Typography variant="caption" color="text.secondary">
          Coverage + riskiest untested files
        </Typography>
      </Stack>

      {loading && <CircularProgress size={18} />}

      {!loading && !data?.ok && (
        <Alert severity="warning">
          <Typography variant="body2">{data?.error ?? 'sentrux test_gaps unreachable'}</Typography>
          {data?.hint && <Typography variant="caption" color="text.secondary">{data.hint}</Typography>}
        </Alert>
      )}

      {!loading && data?.ok && payload && (
        <>
          {/* Aggregate stats tile — always shown if any of the count fields is present */}
          {(payload.source_files != null || payload.test_files != null || payload.untested != null) && (
            <Stack direction={{ xs: 'column', sm: 'row' }} spacing={3} sx={{ mb: 1.5 }}>
              {typeof coverageRatio === 'number' && (
                <Box sx={{ minWidth: 160 }}>
                  <Typography variant="caption" color="text.secondary">Coverage</Typography>
                  <Typography variant="h5">
                    {(coverageRatio * 100).toFixed(1)}%
                  </Typography>
                  <LinearProgress
                    variant="determinate"
                    value={Math.min(100, Math.max(0, coverageRatio * 100))}
                    sx={{ mt: 0.5, height: 6, borderRadius: 3 }}
                    color={coverageRatio < 0.3 ? 'error' : coverageRatio < 0.6 ? 'warning' : 'success'}
                  />
                </Box>
              )}
              <Box>
                <Typography variant="caption" color="text.secondary">Source files</Typography>
                <Typography variant="body1">{payload.source_files?.toLocaleString() ?? '–'}</Typography>
              </Box>
              <Box>
                <Typography variant="caption" color="text.secondary">Test files</Typography>
                <Typography variant="body1">{payload.test_files?.toLocaleString() ?? '–'}</Typography>
              </Box>
              <Box>
                <Typography variant="caption" color="text.secondary">Tested / untested</Typography>
                <Typography variant="body1">
                  {payload.tested?.toLocaleString() ?? '–'} / {payload.untested?.toLocaleString() ?? '–'}
                </Typography>
              </Box>
            </Stack>
          )}

          {/* Per-file table — only when sentrux Pro returns files[] */}
          {topTen.length > 0 && (
            <>
              <Typography variant="subtitle2" sx={{ mb: 0.5 }}>Top 10 riskiest untested files</Typography>
              <TableContainer>
                <Table size="small">
                  <TableHead>
                    <TableRow>
                      <TableCell>File</TableCell>
                      <TableCell align="right">Risk</TableCell>
                      <TableCell align="right">Complexity</TableCell>
                      <TableCell align="right">Imports</TableCell>
                    </TableRow>
                  </TableHead>
                  <TableBody>
                    {topTen.map((f, idx) => (
                      <TableRow key={idx}>
                        <TableCell sx={{ fontFamily: 'monospace', fontSize: '0.78rem', wordBreak: 'break-all' }}>
                          {f.file}
                        </TableCell>
                        <TableCell align="right">{f.risk_score != null ? f.risk_score.toLocaleString() : '–'}</TableCell>
                        <TableCell align="right">{f.complexity ?? '–'}</TableCell>
                        <TableCell align="right">{f.imports ?? '–'}</TableCell>
                      </TableRow>
                    ))}
                  </TableBody>
                </Table>
              </TableContainer>
              <Box sx={{ mt: 1 }}>
                <Typography variant="caption" color="text.secondary">
                  {total.toLocaleString()} total file{total === 1 ? '' : 's'} flagged by sentrux test_gaps
                </Typography>
              </Box>
            </>
          )}

          {topTen.length === 0 && payload.source_files == null && payload.text && (
            <Typography variant="body2" color="text.secondary" sx={{ whiteSpace: 'pre-wrap', fontFamily: 'monospace', fontSize: '0.78rem' }}>
              {payload.text}
            </Typography>
          )}

          {topTen.length === 0 && payload.source_files != null && (
            <Typography variant="caption" color="text.secondary" sx={{ display: 'block', mt: 0.5 }}>
              Per-file ranking requires sentrux Pro. Free tier reports aggregate coverage only.
            </Typography>
          )}
        </>
      )}
    </Paper>
  );
};
