// SentruxRulesCard — surfaces current rule violations from
// .sentrux/rules.toml. Each row: severity / file / rule / message.
//
// Refreshes every 60s. Falls back to a friendly empty state when sentrux
// returns text-only output or no violations are present.

import React, { useEffect, useState } from 'react';
import {
  Alert,
  Box,
  Chip,
  CircularProgress,
  Paper,
  Stack,
  Typography,
} from '@mui/material';
import RuleIcon from '@mui/icons-material/Rule';
import type { SentruxEnvelope, SentruxRulesPayload } from './types';

const REFRESH_MS = 60_000;

function severityColor(s?: string): 'error' | 'warning' | 'info' | 'default' {
  const norm = (s ?? '').toLowerCase();
  if (norm.includes('error') || norm.includes('fail') || norm.includes('high')) return 'error';
  if (norm.includes('warn') || norm.includes('medium')) return 'warning';
  if (norm.includes('info') || norm.includes('low')) return 'info';
  return 'default';
}

export const SentruxRulesCard: React.FC = () => {
  const [data, setData] = useState<SentruxEnvelope<SentruxRulesPayload> | null>(null);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    let cancelled = false;
    const load = async () => {
      try {
        const r = await fetch('/dev-data/sentrux/rules');
        const body = (await r.json()) as SentruxEnvelope<SentruxRulesPayload>;
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

  const violations = data?.data?.violations ?? [];
  const ruleCount = data?.data?.rule_count;

  return (
    <Paper sx={{ p: 2 }}>
      <Stack direction="row" spacing={1} alignItems="center" sx={{ mb: 1 }}>
        <RuleIcon fontSize="small" sx={{ color: 'primary.main' }} />
        <Typography variant="h6">Rule violations</Typography>
        {ruleCount != null && (
          <Chip label={`${ruleCount} rule${ruleCount === 1 ? '' : 's'}`} size="small" variant="outlined" />
        )}
        {data?.data?.passed === true && (
          <Chip label="passing" size="small" color="success" />
        )}
        {data?.data?.passed === false && (
          <Chip label="failing" size="small" color="error" />
        )}
      </Stack>

      {loading && <CircularProgress size={18} />}

      {!loading && !data?.ok && (
        <Alert severity="warning">
          <Typography variant="body2">{data?.error ?? 'sentrux rules unreachable'}</Typography>
          {data?.hint && <Typography variant="caption" color="text.secondary">{data.hint}</Typography>}
        </Alert>
      )}

      {!loading && data?.ok && violations.length === 0 && (
        <>
          {data?.data?.text ? (
            <Typography variant="body2" color="text.secondary" sx={{ whiteSpace: 'pre-wrap', fontFamily: 'monospace', fontSize: '0.78rem' }}>
              {data.data.text}
            </Typography>
          ) : (
            <Typography variant="body2" color="text.secondary">
              No rule violations. Define rules in <code>.sentrux/rules.toml</code> to enforce architectural constraints.
            </Typography>
          )}
        </>
      )}

      {!loading && data?.ok && violations.length > 0 && (
        <Stack spacing={0.5}>
          {violations.map((v, idx) => (
            <Box
              key={idx}
              sx={{
                display: 'grid',
                gridTemplateColumns: { xs: '1fr', sm: '90px 1fr' },
                gap: 1,
                p: 1,
                borderRadius: 1,
                bgcolor: 'action.hover',
                fontSize: '0.85rem',
              }}
            >
              <Chip
                size="small"
                color={severityColor(v.severity)}
                label={v.severity ?? 'info'}
                sx={{ fontSize: '0.7rem', height: 20, alignSelf: 'flex-start' }}
              />
              <Box sx={{ minWidth: 0 }}>
                <Typography variant="body2" sx={{ fontWeight: 500 }}>
                  {v.rule ?? 'rule'}
                </Typography>
                {v.file && (
                  <Typography variant="caption" color="text.secondary" sx={{ fontFamily: 'monospace', wordBreak: 'break-all' }}>
                    {v.file}
                  </Typography>
                )}
                {v.message && (
                  <Typography variant="body2" color="text.secondary" sx={{ mt: 0.25 }}>
                    {v.message}
                  </Typography>
                )}
              </Box>
            </Box>
          ))}
        </Stack>
      )}
    </Paper>
  );
};
