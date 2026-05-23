import React, { useEffect, useState } from 'react';
import {
  Alert,
  Box,
  Chip,
  CircularProgress,
  Grid,
  LinearProgress,
  Paper,
  Stack,
  Tooltip,
  Typography,
} from '@mui/material';
import TrendingUpIcon from '@mui/icons-material/TrendingUp';
import CheckCircleIcon from '@mui/icons-material/CheckCircle';
import HourglassEmptyIcon from '@mui/icons-material/HourglassEmpty';
import InventoryIcon from '@mui/icons-material/Inventory';
import InfoOutlinedIcon from '@mui/icons-material/InfoOutlined';

interface EpicSubSection { title: string; category: 'shipped' | 'active' | 'backlog'; item_count: number }
interface BacklogEpic {
  title: string;
  total_items: number;
  shipped: number;
  active: number;
  backlog: number;
  progress_pct: number;
  sub_sections: EpicSubSection[];
}
interface BacklogPayload {
  total_epics: number;
  total_items: number;
  total_shipped: number;
  overall_progress_pct: number;
  epics: BacklogEpic[];
}

interface QualityEntry { source: string; data: Record<string, unknown> }
interface QualityPayload { domains: Record<string, QualityEntry>; regressions?: string[] }

interface ActivityByDay { date: string; count: number }

interface Manifest {
  generated_at: string;
  backlog: BacklogPayload | null;
  quality: QualityPayload;
  activity_by_day?: ActivityByDay[] | { error: string };
}

const StatTile: React.FC<{ icon: React.ReactNode; label: string; value: string | number; sub?: string; color?: string }> = ({
  icon, label, value, sub, color,
}) => (
  <Paper sx={{ p: 2, height: '100%' }}>
    <Stack direction="row" spacing={1} alignItems="center" sx={{ mb: 0.5 }}>
      <Box sx={{ color: color ?? 'primary.main', display: 'flex' }}>{icon}</Box>
      <Typography variant="caption" color="text.secondary" sx={{ textTransform: 'uppercase', letterSpacing: 0.5 }}>
        {label}
      </Typography>
    </Stack>
    <Typography variant="h4" sx={{ fontWeight: 700, lineHeight: 1.1 }}>{value}</Typography>
    {sub && <Typography variant="caption" color="text.secondary">{sub}</Typography>}
  </Paper>
);

const EpicRow: React.FC<{ epic: BacklogEpic }> = ({ epic }) => {
  const empty = epic.total_items === 0;
  return (
    <Box sx={{ py: 1.25, borderBottom: '1px solid', borderColor: 'divider' }}>
      <Stack direction="row" justifyContent="space-between" alignItems="baseline" spacing={2}>
        <Typography variant="subtitle2" sx={{ fontWeight: 600, flex: 1 }}>{epic.title}</Typography>
        <Typography variant="body2" sx={{ fontFamily: 'monospace', minWidth: 60, textAlign: 'right' }}>
          {empty ? '—' : `${epic.progress_pct}%`}
        </Typography>
      </Stack>
      <LinearProgress
        variant="determinate"
        value={empty ? 0 : epic.progress_pct}
        sx={{
          height: 6,
          borderRadius: 1,
          my: 0.75,
          bgcolor: 'action.hover',
          '& .MuiLinearProgress-bar': {
            bgcolor: epic.progress_pct >= 70 ? 'success.main' : epic.progress_pct >= 30 ? 'warning.main' : 'info.main',
          },
        }}
      />
      <Stack direction="row" spacing={0.5} flexWrap="wrap" useFlexGap>
        {epic.shipped > 0 && (
          <Chip label={`✓ ${epic.shipped} shipped`} size="small" color="success" sx={{ fontSize: '0.7rem', height: 20 }} />
        )}
        {epic.active > 0 && (
          <Chip label={`◐ ${epic.active} active`} size="small" color="warning" sx={{ fontSize: '0.7rem', height: 20 }} />
        )}
        {epic.backlog > 0 && (
          <Chip label={`○ ${epic.backlog} backlog`} size="small" variant="outlined" sx={{ fontSize: '0.7rem', height: 20 }} />
        )}
        {empty && (
          <Chip label="empty (no bullets yet)" size="small" variant="outlined" sx={{ fontSize: '0.7rem', height: 20, color: 'text.disabled' }} />
        )}
      </Stack>
    </Box>
  );
};

const CommitActivityChart: React.FC<{ data: ActivityByDay[] }> = ({ data }) => {
  if (data.length === 0) return null;
  const maxCount = Math.max(1, ...data.map((d) => d.count));
  const totalCommits = data.reduce((s, d) => s + d.count, 0);
  const activeDays = data.filter((d) => d.count > 0).length;

  const barWidth = 100 / data.length;

  return (
    <Paper sx={{ p: 2, height: '100%' }}>
      <Stack direction="row" justifyContent="space-between" alignItems="baseline" sx={{ mb: 1 }}>
        <Stack direction="row" spacing={1} alignItems="center">
          <TrendingUpIcon fontSize="small" />
          <Typography variant="h6">Commit Activity</Typography>
        </Stack>
        <Typography variant="caption" color="text.secondary">last {data.length} days</Typography>
      </Stack>
      <Stack direction="row" spacing={3} sx={{ mb: 1.5 }}>
        <Box>
          <Typography variant="caption" color="text.secondary">Total commits</Typography>
          <Typography variant="h6" sx={{ fontWeight: 600, lineHeight: 1 }}>{totalCommits}</Typography>
        </Box>
        <Box>
          <Typography variant="caption" color="text.secondary">Active days</Typography>
          <Typography variant="h6" sx={{ fontWeight: 600, lineHeight: 1 }}>{activeDays}/{data.length}</Typography>
        </Box>
        <Box>
          <Typography variant="caption" color="text.secondary">Peak day</Typography>
          <Typography variant="h6" sx={{ fontWeight: 600, lineHeight: 1 }}>{maxCount}</Typography>
        </Box>
      </Stack>
      <Box sx={{ position: 'relative', height: 80, width: '100%' }}>
        <svg width="100%" height="100%" viewBox="0 0 100 100" preserveAspectRatio="none">
          {data.map((d, i) => {
            const h = (d.count / maxCount) * 100;
            return (
              <Tooltip key={d.date} title={`${d.date}: ${d.count} commit${d.count === 1 ? '' : 's'}`}>
                <rect
                  x={i * barWidth + 0.2}
                  y={100 - h}
                  width={Math.max(0, barWidth - 0.4)}
                  height={h}
                  fill={d.count === 0 ? '#e0e0e0' : '#1976d2'}
                />
              </Tooltip>
            );
          })}
        </svg>
      </Box>
      <Stack direction="row" justifyContent="space-between" sx={{ mt: 0.5 }}>
        <Typography variant="caption" color="text.secondary">{data[0]?.date}</Typography>
        <Typography variant="caption" color="text.secondary">{data[data.length - 1]?.date}</Typography>
      </Stack>
    </Paper>
  );
};

const QualityStatusRow: React.FC<{ quality: QualityPayload }> = ({ quality }) => {
  const domains = Object.entries(quality.domains);
  if (domains.length === 0) return null;
  const okCount = domains.filter(([, e]) => (e.data.oracle_status as string) === 'ok').length;
  const totalCount = domains.length;
  return (
    <Paper sx={{ p: 2, height: '100%' }}>
      <Stack direction="row" justifyContent="space-between" alignItems="baseline" sx={{ mb: 1 }}>
        <Typography variant="h6">QA Status</Typography>
        <Typography variant="caption" color="text.secondary">
          {okCount}/{totalCount} domains green
        </Typography>
      </Stack>
      {quality.regressions && quality.regressions.length > 0 && (
        <Alert severity="warning" sx={{ mb: 1, py: 0, fontSize: '0.8rem' }}>
          {quality.regressions.length} regression{quality.regressions.length === 1 ? '' : 's'} detected
        </Alert>
      )}
      <Stack direction="row" spacing={0.75} flexWrap="wrap" useFlexGap>
        {domains.map(([name, entry]) => {
          const status = entry.data.oracle_status as string | undefined;
          const color = status === 'ok' ? 'success' : status === 'warn' ? 'warning' : status ? 'error' : 'default';
          return (
            <Tooltip key={name} title={`${name}: ${status ?? 'unknown'} (${entry.source})`}>
              <Chip
                label={name}
                color={color as 'success' | 'warning' | 'error' | 'default'}
                size="small"
                variant={status === 'ok' ? 'filled' : 'outlined'}
                sx={{ fontSize: '0.7rem' }}
              />
            </Tooltip>
          );
        })}
      </Stack>
    </Paper>
  );
};

const MetadataConventionCard: React.FC = () => (
  <Paper sx={{ p: 2, bgcolor: 'action.hover' }}>
    <Stack direction="row" spacing={1} alignItems="flex-start">
      <InfoOutlinedIcon sx={{ color: 'info.main', mt: 0.3 }} />
      <Box sx={{ flex: 1 }}>
        <Typography variant="subtitle2" sx={{ fontWeight: 600, mb: 0.5 }}>
          ETA, business value, and priority are not tracked yet
        </Typography>
        <Typography variant="caption" color="text.secondary" sx={{ display: 'block', mb: 1 }}>
          The dashboard shows what's derivable from real data (shipped vs total per epic from BACKLOG.md H3 structure, commit activity, QA snapshots). To surface ETA / value / priority, adopt an HTML-comment convention on epic headings:
        </Typography>
        <Box
          component="pre"
          sx={{
            m: 0,
            p: 1,
            bgcolor: 'background.paper',
            border: '1px solid',
            borderColor: 'divider',
            borderRadius: 0.5,
            fontSize: '0.75rem',
            fontFamily: 'monospace',
            overflow: 'auto',
          }}
        >
{`## Prime Radiant / Living Cosmos Ideas <!-- value:high, eta:2026-Q3, priority:1 -->`}
        </Box>
        <Typography variant="caption" color="text.secondary" sx={{ display: 'block', mt: 1 }}>
          Once you start using this on any H2, the parser can pick it up and render badges. No change needed for existing epics — they'd just show as "unset".
        </Typography>
      </Box>
    </Stack>
  </Paper>
);

export const OverviewSection: React.FC = () => {
  const [manifest, setManifest] = useState<Manifest | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    let cancelled = false;
    fetch('/dev-data/manifest', { cache: 'no-store' })
      .then((r) => { if (!r.ok) throw new Error(`HTTP ${r.status}`); return r.json() as Promise<Manifest>; })
      .then((m) => { if (!cancelled) setManifest(m); })
      .catch((e) => { if (!cancelled) setError(String(e.message ?? e)); });
    return () => { cancelled = true; };
  }, []);

  if (error) return <Alert severity="error">Failed to load overview: {error}</Alert>;
  if (!manifest) return <Box sx={{ display: 'flex', justifyContent: 'center', py: 4 }}><CircularProgress /></Box>;

  const b = manifest.backlog;
  const byDay = manifest.activity_by_day && Array.isArray(manifest.activity_by_day) ? manifest.activity_by_day : [];
  const totalCommits30d = byDay.reduce((s, d) => s + d.count, 0);
  // QA domain count: how many are explicitly oracle_status="ok"
  const qaDomains = manifest.quality?.domains ? Object.entries(manifest.quality.domains) : [];
  const qaOk = qaDomains.filter(([, e]) => (e.data as Record<string, unknown>)?.oracle_status === 'ok').length;
  const qaTotal = qaDomains.length;
  const regressionCount = manifest.quality?.regressions?.length ?? 0;
  // Most recent commit + relative time
  const latestCommit = Array.isArray(manifest.activity) ? manifest.activity[0] : null;
  const heartbeatRel = (() => {
    if (!latestCommit?.date) return '';
    const minutes = Math.floor((Date.now() - new Date(latestCommit.date).getTime()) / 60000);
    if (minutes < 1) return 'just now';
    if (minutes < 60) return `${minutes}m ago`;
    const hours = Math.floor(minutes / 60);
    if (hours < 24) return `${hours}h ago`;
    return `${Math.floor(hours / 24)}d ago`;
  })();
  const healthy = regressionCount === 0 && qaOk === qaTotal && qaTotal > 0;

  return (
    <Stack spacing={2}>
      {/* Heartbeat — one-line project status banner */}
      <Paper
        sx={{
          p: 1.5,
          display: 'flex',
          alignItems: 'center',
          gap: 1.5,
          flexWrap: 'wrap',
          bgcolor: healthy ? 'success.main' : regressionCount > 0 ? 'warning.main' : 'info.main',
          color: 'common.white',
        }}
      >
        <Box sx={{ fontSize: '1.1rem', lineHeight: 1 }}>
          {healthy ? '⚡' : regressionCount > 0 ? '⚠' : 'ⓘ'}
        </Box>
        <Typography variant="body2" sx={{ fontWeight: 600, color: 'inherit' }}>
          {healthy ? 'Live · all systems nominal' : regressionCount > 0 ? `Live · ${regressionCount} regression${regressionCount === 1 ? '' : 's'}` : 'Live'}
        </Typography>
        <Typography variant="body2" sx={{ color: 'inherit', opacity: 0.92 }}>
          · {b?.total_epics ?? 0} epics, <strong>{b?.overall_progress_pct ?? 0}%</strong> shipped ({b?.total_shipped ?? 0}/{b?.total_items ?? 0})
        </Typography>
        <Typography variant="body2" sx={{ color: 'inherit', opacity: 0.92 }}>
          · <strong>{totalCommits30d}</strong> commits/30d
        </Typography>
        {qaTotal > 0 && (
          <Typography variant="body2" sx={{ color: 'inherit', opacity: 0.92 }}>
            · QA <strong>{qaOk}/{qaTotal}</strong>
          </Typography>
        )}
        {latestCommit && (
          <Typography variant="body2" sx={{ color: 'inherit', opacity: 0.92, flex: 1, textAlign: 'right' }}>
            last commit {heartbeatRel} · <Box component="span" sx={{ fontFamily: 'monospace' }}>{latestCommit.short_sha}</Box>
          </Typography>
        )}
      </Paper>

      {/* Top stat tiles */}
      <Grid container spacing={2}>
        <Grid item xs={6} md={3}>
          <StatTile
            icon={<InventoryIcon />}
            label="Epics"
            value={b?.total_epics ?? 0}
            sub={`${b?.total_items ?? 0} items across all epics`}
          />
        </Grid>
        <Grid item xs={6} md={3}>
          <StatTile
            icon={<CheckCircleIcon />}
            label="Shipped"
            value={b?.total_shipped ?? 0}
            sub={`${b?.overall_progress_pct ?? 0}% of all tracked items`}
            color="success.main"
          />
        </Grid>
        <Grid item xs={6} md={3}>
          <StatTile
            icon={<HourglassEmptyIcon />}
            label="In flight + backlog"
            value={(b?.total_items ?? 0) - (b?.total_shipped ?? 0)}
            sub="active + untouched"
            color="warning.main"
          />
        </Grid>
        <Grid item xs={6} md={3}>
          <StatTile
            icon={<TrendingUpIcon />}
            label="Commits (30d)"
            value={byDay.reduce((s, d) => s + d.count, 0)}
            sub={`${byDay.filter((d) => d.count > 0).length} active days`}
          />
        </Grid>
      </Grid>

      {/* Epic progress + activity chart side by side */}
      <Grid container spacing={2}>
        <Grid item xs={12} md={7}>
          <Paper sx={{ p: 2, height: '100%' }}>
            <Stack direction="row" justifyContent="space-between" alignItems="baseline" sx={{ mb: 1 }}>
              <Typography variant="h6">Epic Progress</Typography>
              <Typography variant="caption" color="text.secondary">shipped / total derived from BACKLOG.md H3 sections</Typography>
            </Stack>
            {b && b.epics.length === 0 && <Typography color="text.secondary">No epics found.</Typography>}
            {b && b.epics.map((e) => <EpicRow key={e.title} epic={e} />)}
          </Paper>
        </Grid>
        <Grid item xs={12} md={5}>
          <Stack spacing={2} sx={{ height: '100%' }}>
            <CommitActivityChart data={byDay} />
            <QualityStatusRow quality={manifest.quality} />
          </Stack>
        </Grid>
      </Grid>

      <MetadataConventionCard />
    </Stack>
  );
};

export default OverviewSection;
