import React, { useEffect, useState } from 'react';
import { Box, Chip, CircularProgress, Divider, Paper, Stack, Tooltip, Typography } from '@mui/material';
import StarRateIcon from '@mui/icons-material/StarRate';
import InfoOutlinedIcon from '@mui/icons-material/InfoOutlined';
import { StarRating } from './StarRating';
import type { ValueRecord } from '../../dev-data/parsers';

// Business-value scorecard — the "expose" surface for the federated RICE→stars
// catalog (ix-value, sibling ix repo). Self-fetching like MissionControl /
// InFlightCard: no props, reads /dev-data/value (served by the Vite dev-data
// middleware over IX_ROOT/state/value/catalog.jsonl).
//
// Layout: repo leaderboard (rollup rows) on top, demo leaderboard below — both
// sorted by score by the server. Stars are the headline; the continuous score
// and rationale ride in the tooltip.

interface ValuePayload {
  generated_at: string;
  records: ValueRecord[];
  demos: ValueRecord[];
  repos: ValueRecord[];
}

const ScoreRow: React.FC<{ rec: ValueRecord; showRepo?: boolean }> = ({ rec, showRepo }) => (
  <Stack
    direction="row"
    spacing={1}
    alignItems="center"
    data-testid="value-row"
    sx={{ py: 0.5, '&:hover': { bgcolor: 'action.hover' }, borderRadius: 0.5 }}
  >
    <StarRating value={rec.stars} score01={rec.score01} />
    <Typography variant="body2" sx={{ fontWeight: 600, minWidth: 0, flexShrink: 1 }} noWrap>
      {rec.title}
    </Typography>
    {showRepo && (
      <Chip label={rec.repo} size="small" variant="outlined" sx={{ height: 18, fontSize: '0.65rem' }} />
    )}
    <Box sx={{ flex: 1 }} />
    <Typography variant="caption" color="text.secondary" sx={{ fontFamily: 'monospace' }}>
      {rec.score01.toFixed(2)}
    </Typography>
    {rec.rationale && (
      <Tooltip title={rec.rationale} placement="top-end" enterDelay={300}>
        <InfoOutlinedIcon sx={{ fontSize: 14, color: 'text.disabled' }} />
      </Tooltip>
    )}
  </Stack>
);

export const ValueScorecard: React.FC = () => {
  const [payload, setPayload] = useState<ValuePayload | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    let cancelled = false;
    fetch('/dev-data/value', { cache: 'no-store' })
      .then(async (r) => {
        if (!r.ok) throw new Error(`/dev-data/value → ${r.status}`);
        return r.json() as Promise<ValuePayload>;
      })
      .then((d) => { if (!cancelled) setPayload(d); })
      .catch((e) => { if (!cancelled) setError(String(e)); });
    return () => { cancelled = true; };
  }, []);

  // Absent catalog (404) or fetch error — render a quiet hint, never blank the
  // dashboard. This is the expected state until ix-value runs in this checkout.
  if (error) {
    return (
      <Paper data-testid="value-scorecard" sx={{ p: 2 }}>
        <Stack direction="row" spacing={1} alignItems="center" sx={{ mb: 0.5 }}>
          <StarRateIcon sx={{ color: 'warning.main' }} />
          <Typography variant="subtitle1" sx={{ fontWeight: 700 }}>Business Value</Typography>
        </Stack>
        <Typography variant="caption" color="text.secondary">
          Value catalog unavailable — run <code>cargo run -p ix-value -- catalog</code> in the sibling ix repo.
        </Typography>
      </Paper>
    );
  }

  if (!payload) {
    return (
      <Paper data-testid="value-scorecard" sx={{ p: 2, display: 'flex', justifyContent: 'center' }}>
        <CircularProgress size={20} />
      </Paper>
    );
  }

  return (
    <Paper data-testid="value-scorecard" sx={{ p: 2 }}>
      <Stack direction="row" spacing={1} alignItems="center" sx={{ mb: 1 }}>
        <StarRateIcon sx={{ color: 'warning.main' }} />
        <Typography variant="subtitle1" sx={{ fontWeight: 700 }}>Business Value</Typography>
        <Tooltip
          title="RICE → stars (geometric mean of Reach·Impact·Confidence, so the weakest axis caps the score). Federated across repos by ix-value."
          placement="top"
          enterDelay={300}
        >
          <InfoOutlinedIcon sx={{ fontSize: 15, color: 'text.disabled' }} />
        </Tooltip>
        <Box sx={{ flex: 1 }} />
        <Chip label={`${payload.demos.length} demos · ${payload.repos.length} repos`} size="small" sx={{ height: 20 }} />
      </Stack>

      {payload.repos.length > 0 && (
        <>
          <Typography variant="caption" color="text.secondary" sx={{ textTransform: 'uppercase', letterSpacing: 0.5 }}>
            Repos
          </Typography>
          <Box data-testid="value-repos" sx={{ mb: 1.5 }}>
            {payload.repos.map((r) => (
              <ScoreRow key={`repo:${r.id}`} rec={r} />
            ))}
          </Box>
          <Divider sx={{ mb: 1 }} />
        </>
      )}

      <Typography variant="caption" color="text.secondary" sx={{ textTransform: 'uppercase', letterSpacing: 0.5 }}>
        Demos
      </Typography>
      <Box data-testid="value-demos">
        {payload.demos.length === 0 ? (
          <Typography variant="caption" color="text.secondary">No demo items in the catalog yet.</Typography>
        ) : (
          payload.demos.map((d) => <ScoreRow key={`demo:${d.repo}:${d.id}`} rec={d} showRepo />)
        )}
      </Box>
    </Paper>
  );
};
