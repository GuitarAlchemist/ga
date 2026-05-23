import React, { useEffect, useState } from 'react';
import {
  Box,
  Card,
  CardActionArea,
  CardContent,
  Chip,
  CircularProgress,
  Grid,
  Link as MuiLink,
  Paper,
  Stack,
  Tooltip,
  Typography,
} from '@mui/material';
import { useNavigate } from 'react-router-dom';
import ReactMarkdown from 'react-markdown';
import remarkGfm from 'remark-gfm';
import HubIcon from '@mui/icons-material/Hub';
import AccountTreeIcon from '@mui/icons-material/AccountTree';
import ForumIcon from '@mui/icons-material/Forum';
import CategoryIcon from '@mui/icons-material/Category';
import CircleIcon from '@mui/icons-material/Circle';
import OpenInNewIcon from '@mui/icons-material/OpenInNew';

interface DevLink {
  title: string;
  description: string;
  path: string;
  icon: React.ReactNode;
}

const devLinks: DevLink[] = [
  {
    title: 'Ecosystem Roadmap',
    description: 'Three-mode roadmap: icicle, Poincaré disk, Poincaré ball (WebGPU).',
    path: '/test/ecosystem-roadmap',
    icon: <AccountTreeIcon />,
  },
  {
    title: 'Prime Radiant',
    description: 'Demerzel governance graph — 3D force-directed visualization with health overlay.',
    path: '/test/prime-radiant',
    icon: <HubIcon />,
  },
  {
    title: 'GA Chat (AG-UI)',
    description: 'AG-UI protocol chat panel; SSE streaming with diatonic chord side-effects.',
    path: '/test/ga-chat',
    icon: <ForumIcon />,
  },
  {
    title: 'Grothendieck DSL',
    description: 'Category-theory operations on musical objects with live parsing and AST view.',
    path: '/test/grothendieck-dsl',
    icon: <CategoryIcon />,
  },
];

interface QualityEntry {
  source: string;
  data: Record<string, unknown>;
}

interface QualityPayload {
  generated_at: string;
  domains: Record<string, QualityEntry>;
}

interface HealthProbe {
  label: string;
  url: string;
  note?: string;
}

const healthProbes: HealthProbe[] = [
  { label: 'SPA (Vite)', url: '/', note: 'this page loads → SPA up' },
  { label: 'GaApi /health', url: '/health' },
  { label: 'Chatbot /api/chatbot/status', url: '/api/chatbot/status' },
];

type ProbeState = 'pending' | 'ok' | 'fail';

interface ProbeResult {
  state: ProbeState;
  code?: number;
  detail?: string;
}

const DashboardLinks: React.FC = () => {
  const navigate = useNavigate();
  return (
    <Grid container spacing={2}>
      {devLinks.map((link) => (
        <Grid item xs={12} sm={6} md={3} key={link.path}>
          <Card sx={{ height: '100%' }}>
            <CardActionArea onClick={() => navigate(link.path)} sx={{ height: '100%' }}>
              <CardContent>
                <Stack direction="row" spacing={1.5} alignItems="center" sx={{ mb: 1 }}>
                  <Box sx={{ color: 'primary.main' }}>{link.icon}</Box>
                  <Typography variant="subtitle1" sx={{ fontWeight: 600 }}>{link.title}</Typography>
                </Stack>
                <Typography variant="body2" color="text.secondary">{link.description}</Typography>
                <Typography variant="caption" sx={{ display: 'block', mt: 1, color: 'primary.main' }}>{link.path}</Typography>
              </CardContent>
            </CardActionArea>
          </Card>
        </Grid>
      ))}
    </Grid>
  );
};

const BacklogCard: React.FC = () => {
  const [content, setContent] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    let cancelled = false;
    fetch('/dev-data/backlog')
      .then((r) => {
        if (!r.ok) throw new Error(`HTTP ${r.status}`);
        return r.text();
      })
      .then((text) => { if (!cancelled) setContent(text); })
      .catch((e) => { if (!cancelled) setError(String(e.message ?? e)); });
    return () => { cancelled = true; };
  }, []);

  return (
    <Paper sx={{ p: 2, height: '100%', display: 'flex', flexDirection: 'column' }}>
      <Stack direction="row" justifyContent="space-between" alignItems="center" sx={{ mb: 1 }}>
        <Typography variant="h6">Roadmap — BACKLOG.md</Typography>
        <MuiLink
          href="https://github.com/GuitarAlchemist/ga/blob/main/BACKLOG.md"
          target="_blank"
          rel="noopener noreferrer"
          sx={{ fontSize: '0.8rem', display: 'flex', alignItems: 'center', gap: 0.5 }}
        >
          GitHub <OpenInNewIcon fontSize="inherit" />
        </MuiLink>
      </Stack>
      <Box
        sx={{
          flex: 1,
          maxHeight: 480,
          overflow: 'auto',
          fontSize: '0.85rem',
          '& h1, & h2, & h3': { mt: 1.5, mb: 0.5 },
          '& h1': { fontSize: '1.2rem' },
          '& h2': { fontSize: '1.05rem' },
          '& h3': { fontSize: '0.95rem' },
          '& ul, & ol': { pl: 3, my: 0.5 },
          '& code': { bgcolor: 'action.hover', px: 0.5, borderRadius: 0.5 },
          '& pre': { bgcolor: 'action.hover', p: 1, borderRadius: 1, overflow: 'auto', fontSize: '0.8rem' },
        }}
      >
        {error && <Typography color="error">Failed to load BACKLOG.md: {error}</Typography>}
        {!content && !error && <CircularProgress size={20} />}
        {content && <ReactMarkdown remarkPlugins={[remarkGfm]}>{content}</ReactMarkdown>}
      </Box>
    </Paper>
  );
};

const QualityCard: React.FC = () => {
  const [data, setData] = useState<QualityPayload | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    let cancelled = false;
    fetch('/dev-data/quality')
      .then((r) => {
        if (!r.ok) throw new Error(`HTTP ${r.status}`);
        return r.json() as Promise<QualityPayload>;
      })
      .then((payload) => { if (!cancelled) setData(payload); })
      .catch((e) => { if (!cancelled) setError(String(e.message ?? e)); });
    return () => { cancelled = true; };
  }, []);

  const renderDomain = (name: string, entry: QualityEntry) => {
    const d = entry.data;
    const metricName = d.metric_name as string | undefined;
    const metricValue = d.metric_value as number | undefined;
    const oracleStatus = d.oracle_status as string | undefined;
    const emittedAt = d.emitted_at as string | undefined;
    const summary = d.summary as string | undefined;
    const problems = d.problems as unknown[] | undefined;

    const statusColor = oracleStatus === 'ok' ? 'success' : oracleStatus === 'warn' ? 'warning' : oracleStatus ? 'error' : 'default';

    return (
      <Box key={name} sx={{ py: 1, borderBottom: '1px solid', borderColor: 'divider' }}>
        <Stack direction="row" justifyContent="space-between" alignItems="center" spacing={1}>
          <Typography variant="subtitle2" sx={{ fontWeight: 600 }}>{name}</Typography>
          {oracleStatus && (
            <Chip
              label={oracleStatus}
              color={statusColor as 'success' | 'warning' | 'error' | 'default'}
              size="small"
              sx={{ fontSize: '0.7rem', height: 20 }}
            />
          )}
        </Stack>
        {metricName !== undefined && (
          <Typography variant="caption" color="text.secondary" sx={{ display: 'block' }}>
            {metricName}: <strong>{metricValue ?? '—'}</strong>
            {emittedAt && <> · {new Date(emittedAt).toLocaleDateString()}</>}
            <> · {entry.source}</>
          </Typography>
        )}
        {summary && (
          <Typography variant="caption" sx={{ display: 'block', mt: 0.5 }}>{summary}</Typography>
        )}
        {problems && problems.length > 0 && (
          <Typography variant="caption" color="warning.main" sx={{ display: 'block', mt: 0.5 }}>
            {problems.length} problem{problems.length === 1 ? '' : 's'} reported
          </Typography>
        )}
      </Box>
    );
  };

  return (
    <Paper sx={{ p: 2, height: '100%' }}>
      <Stack direction="row" justifyContent="space-between" alignItems="center" sx={{ mb: 1 }}>
        <Typography variant="h6">QA — Quality Snapshots</Typography>
        <MuiLink
          href="https://github.com/GuitarAlchemist/ga/blob/main/docs/quality/README.md"
          target="_blank"
          rel="noopener noreferrer"
          sx={{ fontSize: '0.8rem', display: 'flex', alignItems: 'center', gap: 0.5 }}
        >
          Full report <OpenInNewIcon fontSize="inherit" />
        </MuiLink>
      </Stack>
      <Box sx={{ maxHeight: 480, overflow: 'auto' }}>
        {error && <Typography color="error">Failed to load: {error}</Typography>}
        {!data && !error && <CircularProgress size={20} />}
        {data && Object.keys(data.domains).length === 0 && (
          <Typography color="text.secondary">No snapshots found in state/quality.</Typography>
        )}
        {data && Object.entries(data.domains).map(([name, entry]) => renderDomain(name, entry))}
      </Box>
    </Paper>
  );
};

const ProcessHealthCard: React.FC = () => {
  const [probes, setProbes] = useState<Record<string, ProbeResult>>(
    () => Object.fromEntries(healthProbes.map((p) => [p.label, { state: 'pending' as ProbeState }])),
  );

  const runProbes = React.useCallback(() => {
    setProbes((prev) => Object.fromEntries(Object.keys(prev).map((k) => [k, { state: 'pending' as ProbeState }])));
    healthProbes.forEach((probe) => {
      const controller = new AbortController();
      const timeout = setTimeout(() => controller.abort(), 4000);
      fetch(probe.url, { signal: controller.signal, cache: 'no-store' })
        .then(async (r) => {
          clearTimeout(timeout);
          const ok = r.ok;
          let detail: string | undefined;
          try {
            const text = await r.text();
            if (text.length > 0 && text.length < 200) detail = text.trim();
          } catch { /* ignore */ }
          setProbes((prev) => ({
            ...prev,
            [probe.label]: { state: ok ? 'ok' : 'fail', code: r.status, detail },
          }));
        })
        .catch((e) => {
          clearTimeout(timeout);
          setProbes((prev) => ({
            ...prev,
            [probe.label]: { state: 'fail', detail: String(e.message ?? e) },
          }));
        });
    });
  }, []);

  useEffect(() => {
    runProbes();
    const interval = setInterval(runProbes, 30000);
    return () => clearInterval(interval);
  }, [runProbes]);

  const dotColor = (s: ProbeState) =>
    s === 'ok' ? 'success.main' : s === 'fail' ? 'error.main' : 'warning.main';

  return (
    <Paper sx={{ p: 2, height: '100%' }}>
      <Stack direction="row" justifyContent="space-between" alignItems="center" sx={{ mb: 1 }}>
        <Typography variant="h6">Process Health</Typography>
        <Typography variant="caption" color="text.secondary">refreshes every 30s</Typography>
      </Stack>
      <Stack spacing={1}>
        {healthProbes.map((probe) => {
          const result = probes[probe.label];
          return (
            <Tooltip
              key={probe.label}
              title={`${probe.url}${result?.code ? ` → ${result.code}` : ''}${result?.detail ? ` · ${result.detail}` : ''}`}
              arrow
            >
              <Stack direction="row" spacing={1.5} alignItems="center">
                <CircleIcon sx={{ fontSize: 14, color: dotColor(result?.state ?? 'pending') }} />
                <Box sx={{ flex: 1 }}>
                  <Typography variant="body2" sx={{ fontWeight: 500 }}>{probe.label}</Typography>
                  {probe.note && (
                    <Typography variant="caption" color="text.secondary">{probe.note}</Typography>
                  )}
                </Box>
                <Typography variant="caption" color="text.secondary">
                  {result?.code ?? (result?.state === 'pending' ? '…' : '—')}
                </Typography>
              </Stack>
            </Tooltip>
          );
        })}
      </Stack>
    </Paper>
  );
};

export const DevelopmentSection: React.FC = () => {
  return (
    <Stack spacing={3}>
      <Box>
        <Typography variant="h5" gutterBottom>Development</Typography>
        <Typography variant="body2" color="text.secondary" sx={{ mb: 2 }}>
          Roadmap, architecture, and QA at a glance. Live data from BACKLOG.md, state/quality, and local health probes.
        </Typography>
        <DashboardLinks />
      </Box>

      <Grid container spacing={2}>
        <Grid item xs={12} md={6}>
          <BacklogCard />
        </Grid>
        <Grid item xs={12} md={6}>
          <Stack spacing={2}>
            <QualityCard />
            <ProcessHealthCard />
          </Stack>
        </Grid>
      </Grid>
    </Stack>
  );
};

export default DevelopmentSection;
