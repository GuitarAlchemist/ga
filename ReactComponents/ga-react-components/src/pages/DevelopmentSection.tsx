import React, { useEffect, useState } from 'react';
import {
  Alert,
  Box,
  Button,
  Card,
  CardActionArea,
  CardContent,
  Chip,
  CircularProgress,
  Grid,
  IconButton,
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
import ArticleIcon from '@mui/icons-material/Article';
import HistoryIcon from '@mui/icons-material/History';
import AssignmentLateIcon from '@mui/icons-material/AssignmentLate';
import WarningAmberIcon from '@mui/icons-material/WarningAmber';
import ContentCopyIcon from '@mui/icons-material/ContentCopy';
import CodeIcon from '@mui/icons-material/Code';

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
  regressions?: string[];
}

interface ArchDoc {
  file: string;
  title: string;
  modified_at: string;
  size: number;
}

interface ArchitecturePayload {
  generated_at: string;
  docs: ArchDoc[];
}

interface ActivityCommit {
  sha: string;
  short_sha: string;
  author: string;
  date: string;
  subject: string;
}

interface ActivityPayload {
  generated_at: string;
  commits: ActivityCommit[] | { error: string };
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
      {data?.regressions && data.regressions.length > 0 && (
        <Alert
          severity="warning"
          icon={<WarningAmberIcon fontSize="small" />}
          sx={{ mb: 1, py: 0, fontSize: '0.8rem' }}
        >
          <strong>{data.regressions.length} regression{data.regressions.length === 1 ? '' : 's'}:</strong>{' '}
          {data.regressions.join(' · ')}
        </Alert>
      )}
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

const ArchitectureCard: React.FC = () => {
  const [data, setData] = useState<ArchitecturePayload | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    let cancelled = false;
    fetch('/dev-data/architecture')
      .then((r) => { if (!r.ok) throw new Error(`HTTP ${r.status}`); return r.json() as Promise<ArchitecturePayload>; })
      .then((p) => { if (!cancelled) setData(p); })
      .catch((e) => { if (!cancelled) setError(String(e.message ?? e)); });
    return () => { cancelled = true; };
  }, []);

  return (
    <Paper sx={{ p: 2, height: '100%' }}>
      <Stack direction="row" justifyContent="space-between" alignItems="center" sx={{ mb: 1 }}>
        <Stack direction="row" spacing={1} alignItems="center">
          <ArticleIcon fontSize="small" />
          <Typography variant="h6">Architecture</Typography>
        </Stack>
        <MuiLink
          href="https://github.com/GuitarAlchemist/ga/tree/main/docs/architecture"
          target="_blank"
          rel="noopener noreferrer"
          sx={{ fontSize: '0.8rem', display: 'flex', alignItems: 'center', gap: 0.5 }}
        >
          docs/architecture <OpenInNewIcon fontSize="inherit" />
        </MuiLink>
      </Stack>
      <Box sx={{ maxHeight: 280, overflow: 'auto' }}>
        {error && <Typography color="error">Failed to load: {error}</Typography>}
        {!data && !error && <CircularProgress size={20} />}
        {data && data.docs.length === 0 && (
          <Typography color="text.secondary">No docs in docs/architecture/.</Typography>
        )}
        {data && data.docs.map((doc) => (
          <Box key={doc.file} sx={{ py: 0.75, borderBottom: '1px solid', borderColor: 'divider' }}>
            <MuiLink
              href={`https://github.com/GuitarAlchemist/ga/blob/main/docs/architecture/${doc.file}`}
              target="_blank"
              rel="noopener noreferrer"
              sx={{ fontSize: '0.875rem', fontWeight: 500, display: 'block' }}
            >
              {doc.title}
            </MuiLink>
            <Typography variant="caption" color="text.secondary">
              {doc.file} · {new Date(doc.modified_at).toLocaleDateString()} · {Math.round(doc.size / 1024)} KB
            </Typography>
          </Box>
        ))}
      </Box>
    </Paper>
  );
};

const ActivityCard: React.FC = () => {
  const [data, setData] = useState<ActivityPayload | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    let cancelled = false;
    fetch('/dev-data/activity?limit=10')
      .then((r) => { if (!r.ok) throw new Error(`HTTP ${r.status}`); return r.json() as Promise<ActivityPayload>; })
      .then((p) => { if (!cancelled) setData(p); })
      .catch((e) => { if (!cancelled) setError(String(e.message ?? e)); });
    return () => { cancelled = true; };
  }, []);

  const relative = (iso: string): string => {
    const diff = Date.now() - new Date(iso).getTime();
    const minutes = Math.floor(diff / 60000);
    if (minutes < 1) return 'just now';
    if (minutes < 60) return `${minutes}m ago`;
    const hours = Math.floor(minutes / 60);
    if (hours < 24) return `${hours}h ago`;
    const days = Math.floor(hours / 24);
    return `${days}d ago`;
  };

  const commits = data && Array.isArray(data.commits) ? data.commits : [];
  const gitError = data && !Array.isArray(data.commits) ? (data.commits as { error: string }).error : null;

  return (
    <Paper sx={{ p: 2, height: '100%' }}>
      <Stack direction="row" justifyContent="space-between" alignItems="center" sx={{ mb: 1 }}>
        <Stack direction="row" spacing={1} alignItems="center">
          <HistoryIcon fontSize="small" />
          <Typography variant="h6">Recent Activity</Typography>
        </Stack>
        <Typography variant="caption" color="text.secondary">last 10 commits</Typography>
      </Stack>
      <Box sx={{ maxHeight: 280, overflow: 'auto' }}>
        {error && <Typography color="error">Failed to load: {error}</Typography>}
        {gitError && <Typography color="error">git log failed: {gitError}</Typography>}
        {!data && !error && <CircularProgress size={20} />}
        {commits.map((c) => (
          <Box key={c.sha} sx={{ py: 0.5, borderBottom: '1px solid', borderColor: 'divider' }}>
            <Stack direction="row" spacing={1} alignItems="baseline">
              <MuiLink
                href={`https://github.com/GuitarAlchemist/ga/commit/${c.sha}`}
                target="_blank"
                rel="noopener noreferrer"
                sx={{ fontFamily: 'monospace', fontSize: '0.75rem', flexShrink: 0 }}
              >
                {c.short_sha}
              </MuiLink>
              <Typography variant="body2" sx={{ flex: 1, overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }} title={c.subject}>
                {c.subject}
              </Typography>
            </Stack>
            <Typography variant="caption" color="text.secondary">
              {c.author} · {relative(c.date)}
            </Typography>
          </Box>
        ))}
      </Box>
    </Paper>
  );
};

interface OperationalTodo {
  title: string;
  why: string;
  command?: string;
  doc?: string;
}

const operationalTodos: OperationalTodo[] = [
  {
    title: 'Install GuitarAlchemist Windows service',
    why: 'Vite + GaApi + GaChatbot.Api currently die on reboot/logoff (24h outage on 2026-05-22). Service install supervises them.',
    command: 'pwsh C:\\Users\\spare\\source\\repos\\ga\\Scripts\\install-ga-service.ps1',
    doc: 'docs/runbooks/chatbot-deploy.md',
  },
  {
    title: 'Set MISTRAL_API_KEY for chatbot cascade',
    why: 'Without it the chatbot falls back to local Ollama (llama3.2:3b). Mistral cascade is the recommended production path per runbook.',
    command: '$env:MISTRAL_API_KEY = "<your-key>"; $env:AI__CascadeProvider = "mistral"',
  },
  {
    title: 'Migrate /dev-data middleware to real backend for production builds',
    why: 'devDataPlugin only runs under `vite` dev server. `vite build` strips it. If demos.guitaralchemist.com ever serves a static build instead of dev mode, the Development tab breaks.',
  },
];

const OperationalTodoCard: React.FC = () => {
  const [copied, setCopied] = useState<string | null>(null);
  const copy = (text: string) => {
    navigator.clipboard.writeText(text).then(() => {
      setCopied(text);
      setTimeout(() => setCopied(null), 2000);
    });
  };
  return (
    <Paper sx={{ p: 2 }}>
      <Stack direction="row" spacing={1} alignItems="center" sx={{ mb: 1 }}>
        <AssignmentLateIcon fontSize="small" sx={{ color: 'warning.main' }} />
        <Typography variant="h6">Operational TODO</Typography>
        <Chip label={`${operationalTodos.length} item${operationalTodos.length === 1 ? '' : 's'}`} size="small" />
      </Stack>
      <Typography variant="caption" color="text.secondary" sx={{ display: 'block', mb: 1.5 }}>
        Action items that need a human/admin. Not auto-run.
      </Typography>
      <Stack spacing={1.5}>
        {operationalTodos.map((t) => (
          <Box key={t.title} sx={{ p: 1.5, bgcolor: 'action.hover', borderRadius: 1 }}>
            <Typography variant="subtitle2" sx={{ fontWeight: 600 }}>{t.title}</Typography>
            <Typography variant="caption" color="text.secondary" sx={{ display: 'block', mb: t.command ? 1 : 0 }}>
              {t.why}
            </Typography>
            {t.command && (
              <Stack direction="row" spacing={1} alignItems="center" sx={{ mt: 0.5 }}>
                <Box
                  component="code"
                  sx={{
                    flex: 1,
                    p: 0.75,
                    bgcolor: 'background.paper',
                    border: '1px solid',
                    borderColor: 'divider',
                    borderRadius: 0.5,
                    fontSize: '0.75rem',
                    fontFamily: 'monospace',
                    overflow: 'auto',
                    whiteSpace: 'nowrap',
                  }}
                >
                  {t.command}
                </Box>
                <Tooltip title={copied === t.command ? 'Copied!' : 'Copy command'}>
                  <IconButton size="small" onClick={() => copy(t.command!)}>
                    <ContentCopyIcon fontSize="small" />
                  </IconButton>
                </Tooltip>
              </Stack>
            )}
            {t.doc && (
              <MuiLink
                href={`https://github.com/GuitarAlchemist/ga/blob/main/${t.doc}`}
                target="_blank"
                rel="noopener noreferrer"
                sx={{ fontSize: '0.75rem', display: 'inline-flex', alignItems: 'center', gap: 0.5, mt: 0.5 }}
              >
                {t.doc} <OpenInNewIcon fontSize="inherit" />
              </MuiLink>
            )}
          </Box>
        ))}
      </Stack>
    </Paper>
  );
};

const ManifestBanner: React.FC = () => (
  <Alert
    severity="info"
    icon={<CodeIcon fontSize="small" />}
    sx={{ '& .MuiAlert-message': { width: '100%' } }}
  >
    <Stack direction={{ xs: 'column', sm: 'row' }} spacing={1} alignItems={{ sm: 'center' }} justifyContent="space-between">
      <Typography variant="body2">
        AI tools (Claude, Junie, Codex): fetch <Box component="code" sx={{ px: 0.5, bgcolor: 'background.paper', borderRadius: 0.5 }}>/dev-data/manifest</Box> for full project context.
      </Typography>
      <Button
        size="small"
        variant="outlined"
        href="/dev-data/manifest"
        target="_blank"
        rel="noopener noreferrer"
        endIcon={<OpenInNewIcon />}
      >
        Open manifest
      </Button>
    </Stack>
  </Alert>
);

export const DevelopmentSection: React.FC = () => {
  return (
    <Stack spacing={3}>
      <Box>
        <Typography variant="h5" gutterBottom>Development</Typography>
        <Typography variant="body2" color="text.secondary" sx={{ mb: 2 }}>
          Roadmap, architecture, and QA at a glance. Live data from BACKLOG.md, state/quality, git log, and local health probes.
        </Typography>
        <DashboardLinks />
      </Box>

      <ManifestBanner />

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
        <Grid item xs={12} md={6}>
          <ActivityCard />
        </Grid>
        <Grid item xs={12} md={6}>
          <ArchitectureCard />
        </Grid>
        <Grid item xs={12}>
          <OperationalTodoCard />
        </Grid>
      </Grid>
    </Stack>
  );
};

export default DevelopmentSection;
