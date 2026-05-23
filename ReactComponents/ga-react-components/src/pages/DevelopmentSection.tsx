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
  Tab,
  Tabs,
  Tooltip,
  Typography,
} from '@mui/material';
import { useNavigate } from 'react-router-dom';
import ReactMarkdown from 'react-markdown';
import remarkGfm from 'remark-gfm';
import OverviewSection from './OverviewSection';
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
import GroupsIcon from '@mui/icons-material/Groups';
import CheckCircleIcon from '@mui/icons-material/CheckCircle';
import CancelIcon from '@mui/icons-material/Cancel';
import FolderIcon from '@mui/icons-material/Folder';
import InsertDriveFileIcon from '@mui/icons-material/InsertDriveFile';
import DashboardIcon from '@mui/icons-material/Dashboard';
import InventoryIcon from '@mui/icons-material/Inventory';
import VerifiedIcon from '@mui/icons-material/Verified';
import LayersIcon from '@mui/icons-material/Layers';

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
  {
    title: 'Wire per-agent token-quota visibility',
    why: 'The AI Contributors card surfaces handoffs + commit counts, but "Tokens left" is —. Real values require per-provider auth: Anthropic console API for Claude, OpenAI usage API for Codex, Google AI Studio for Gemini. Each is a separate integration with its own credentials.',
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

interface AgentFileEntry {
  path: string;
  exists: boolean;
  size: number | null;
  modified_at: string | null;
  is_directory: boolean;
  description: string;
}

interface McpServersInfo {
  count: number;
  names: string[];
}

interface AgentsPayload {
  generated_at: string;
  agent_files: AgentFileEntry[];
  mcp_servers: McpServersInfo;
}

const AgentCollaborationCard: React.FC = () => {
  const [data, setData] = useState<AgentsPayload | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [copied, setCopied] = useState<string | null>(null);

  useEffect(() => {
    let cancelled = false;
    fetch('/dev-data/agents')
      .then((r) => { if (!r.ok) throw new Error(`HTTP ${r.status}`); return r.json() as Promise<AgentsPayload>; })
      .then((p) => { if (!cancelled) setData(p); })
      .catch((e) => { if (!cancelled) setError(String(e.message ?? e)); });
    return () => { cancelled = true; };
  }, []);

  const copy = (text: string) => {
    navigator.clipboard.writeText(text).then(() => {
      setCopied(text);
      setTimeout(() => setCopied(null), 2000);
    });
  };

  const bootstrapCommands: { label: string; command: string }[] = [
    {
      label: 'Bootstrap any AI agent',
      command: 'curl -sS https://demos.guitaralchemist.com/dev-data/manifest | jq .',
    },
    {
      label: 'Codex CLI on this repo',
      command: 'cd C:\\Users\\spare\\source\\repos\\ga && codex',
    },
    {
      label: 'Antigravity v2 in this workspace',
      command: 'antigravity C:\\Users\\spare\\source\\repos\\ga',
    },
  ];

  const presentFiles = data?.agent_files.filter((f) => f.exists) ?? [];
  const missingFiles = data?.agent_files.filter((f) => !f.exists) ?? [];

  return (
    <Paper sx={{ p: 2 }}>
      <Stack direction="row" spacing={1} alignItems="center" sx={{ mb: 1 }}>
        <GroupsIcon fontSize="small" sx={{ color: 'primary.main' }} />
        <Typography variant="h6">Agent Collaboration</Typography>
        <Chip label={`${presentFiles.length}/${data?.agent_files.length ?? 0} config files`} size="small" />
        {data && <Chip label={`${data.mcp_servers.count} MCP servers`} size="small" color="primary" />}
      </Stack>
      <Typography variant="caption" color="text.secondary" sx={{ display: 'block', mb: 2 }}>
        Files that govern how AI agents (Claude, Antigravity v2, codex, Gemini CLI) behave in this repo.
        Source via{' '}
        <Box component="code" sx={{ px: 0.5, bgcolor: 'action.hover', borderRadius: 0.5, fontSize: '0.8rem' }}>
          /dev-data/agents
        </Box>.
      </Typography>

      {error && <Alert severity="error">Failed to load: {error}</Alert>}
      {!data && !error && <CircularProgress size={20} />}

      {data && (
        <Grid container spacing={2}>
          <Grid item xs={12} md={7}>
            <Typography variant="subtitle2" sx={{ fontWeight: 600, mb: 1 }}>Config files</Typography>
            <Box sx={{ maxHeight: 300, overflow: 'auto' }}>
              {data.agent_files.map((f) => (
                <Stack
                  key={f.path}
                  direction="row"
                  spacing={1}
                  alignItems="center"
                  sx={{ py: 0.5, borderBottom: '1px solid', borderColor: 'divider', opacity: f.exists ? 1 : 0.55 }}
                >
                  {f.exists ? (
                    <CheckCircleIcon sx={{ fontSize: 16, color: 'success.main' }} />
                  ) : (
                    <CancelIcon sx={{ fontSize: 16, color: 'text.disabled' }} />
                  )}
                  {f.exists && (f.is_directory ? <FolderIcon sx={{ fontSize: 16, color: 'text.secondary' }} /> : <InsertDriveFileIcon sx={{ fontSize: 16, color: 'text.secondary' }} />)}
                  <Box sx={{ flex: 1, minWidth: 0 }}>
                    <Tooltip title={f.description}>
                      <Typography variant="body2" sx={{ fontFamily: 'monospace', fontSize: '0.8rem', fontWeight: 500 }}>
                        {f.path}
                      </Typography>
                    </Tooltip>
                    <Typography variant="caption" color="text.secondary" sx={{ display: 'block', overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>
                      {f.description}
                    </Typography>
                  </Box>
                  {f.exists && f.modified_at && (
                    <Typography variant="caption" color="text.secondary" sx={{ flexShrink: 0 }}>
                      {new Date(f.modified_at).toLocaleDateString()}
                    </Typography>
                  )}
                </Stack>
              ))}
            </Box>
            {missingFiles.length > 0 && (
              <Typography variant="caption" color="text.secondary" sx={{ display: 'block', mt: 1, fontStyle: 'italic' }}>
                {missingFiles.length} not yet present: {missingFiles.map((f) => f.path).join(', ')}
              </Typography>
            )}
          </Grid>
          <Grid item xs={12} md={5}>
            <Typography variant="subtitle2" sx={{ fontWeight: 600, mb: 1 }}>
              MCP federation
              {data.mcp_servers.count > 0 && (
                <Typography component="span" variant="caption" color="text.secondary" sx={{ ml: 1 }}>
                  via .mcp.json (read by Claude + Antigravity)
                </Typography>
              )}
            </Typography>
            <Stack direction="row" spacing={0.5} flexWrap="wrap" useFlexGap sx={{ mb: 2 }}>
              {data.mcp_servers.names.map((name) => (
                <Chip key={name} label={name} size="small" variant="outlined" sx={{ fontSize: '0.7rem' }} />
              ))}
            </Stack>

            <Typography variant="subtitle2" sx={{ fontWeight: 600, mb: 1 }}>Quick start</Typography>
            <Stack spacing={1}>
              {bootstrapCommands.map((c) => (
                <Box key={c.label}>
                  <Typography variant="caption" color="text.secondary" sx={{ display: 'block', fontWeight: 500 }}>
                    {c.label}
                  </Typography>
                  <Stack direction="row" spacing={0.5} alignItems="center">
                    <Box
                      component="code"
                      sx={{
                        flex: 1,
                        p: 0.5,
                        bgcolor: 'action.hover',
                        border: '1px solid',
                        borderColor: 'divider',
                        borderRadius: 0.5,
                        fontSize: '0.7rem',
                        fontFamily: 'monospace',
                        overflow: 'auto',
                        whiteSpace: 'nowrap',
                      }}
                    >
                      {c.command}
                    </Box>
                    <Tooltip title={copied === c.command ? 'Copied!' : 'Copy'}>
                      <IconButton size="small" onClick={() => copy(c.command)}>
                        <ContentCopyIcon fontSize="inherit" />
                      </IconButton>
                    </Tooltip>
                  </Stack>
                </Box>
              ))}
            </Stack>
          </Grid>
        </Grid>
      )}
    </Paper>
  );
};

interface AgentActivityEntry {
  agent: string;
  display_name: string;
  last_seen_at: string | null;
  handoff_count: number;
  coauthored_commits_30d: number;
  recent_handoffs: { at: string; branch: string | null; head: string | null; path: string }[];
}
interface AgentActivityPayload {
  generated_at: string;
  agents: AgentActivityEntry[];
  recent_handoffs: { from: string; at: string; branch: string | null; head: string | null; path: string }[];
}

const agentColor = (id: string): 'primary' | 'success' | 'warning' | 'info' | 'secondary' | 'default' => {
  switch (id) {
    case 'claude': return 'primary';
    case 'codex': return 'success';
    case 'antigravity': return 'warning';
    case 'gemini': return 'info';
    case 'junie': return 'secondary';
    default: return 'default';
  }
};

const AgentContributorsCard: React.FC = () => {
  const [data, setData] = useState<AgentActivityPayload | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    let cancelled = false;
    fetch('/dev-data/agent-activity')
      .then((r) => { if (!r.ok) throw new Error(`HTTP ${r.status}`); return r.json() as Promise<AgentActivityPayload>; })
      .then((p) => { if (!cancelled) setData(p); })
      .catch((e) => { if (!cancelled) setError(String(e.message ?? e)); });
    return () => { cancelled = true; };
  }, []);

  const formatRelative = (iso: string | null): string => {
    if (!iso) return '—';
    const minutes = Math.floor((Date.now() - new Date(iso).getTime()) / 60000);
    if (minutes < 1) return 'just now';
    if (minutes < 60) return `${minutes}m ago`;
    const hours = Math.floor(minutes / 60);
    if (hours < 24) return `${hours}h ago`;
    return `${Math.floor(hours / 24)}d ago`;
  };

  return (
    <Paper sx={{ p: 2 }}>
      <Stack direction="row" spacing={1} alignItems="center" sx={{ mb: 1 }}>
        <GroupsIcon fontSize="small" sx={{ color: 'primary.main' }} />
        <Typography variant="h6">AI Contributors</Typography>
        {data && <Chip label={`${data.agents.length} agents`} size="small" />}
      </Stack>
      <Typography variant="caption" color="text.secondary" sx={{ display: 'block', mb: 2 }}>
        Activity signal across all AI agents working on this repo. Handoffs from{' '}
        <Box component="code" sx={{ px: 0.5, bgcolor: 'action.hover', borderRadius: 0.5, fontSize: '0.8rem' }}>state/handoffs/</Box>{' '}
        + commit counts from{' '}
        <Box component="code" sx={{ px: 0.5, bgcolor: 'action.hover', borderRadius: 0.5, fontSize: '0.8rem' }}>git log --since=30 days ago</Box>.
      </Typography>

      {error && <Alert severity="error">Failed to load: {error}</Alert>}
      {!data && !error && <CircularProgress size={20} />}

      {data && (
        <Grid container spacing={2}>
          <Grid item xs={12} md={7}>
            <Typography variant="subtitle2" sx={{ fontWeight: 600, mb: 1 }}>Per-agent activity</Typography>
            <Box sx={{ overflow: 'auto' }}>
              <Box component="table" sx={{ width: '100%', borderCollapse: 'collapse', fontSize: '0.85rem' }}>
                <Box component="thead" sx={{ '& th': { textAlign: 'left', py: 0.5, color: 'text.secondary', fontWeight: 600, fontSize: '0.75rem', borderBottom: '1px solid', borderColor: 'divider' } }}>
                  <tr>
                    <th>Agent</th>
                    <th style={{ textAlign: 'right' }}>Commits (30d)</th>
                    <th style={{ textAlign: 'right' }}>Handoffs</th>
                    <th style={{ textAlign: 'right' }}>Last seen</th>
                    <th style={{ textAlign: 'right' }}>Tokens left</th>
                  </tr>
                </Box>
                <Box component="tbody" sx={{ '& td': { py: 0.75, borderBottom: '1px solid', borderColor: 'divider' } }}>
                  {data.agents.map((a) => (
                    <tr key={a.agent}>
                      <td>
                        <Chip label={a.display_name} color={agentColor(a.agent)} size="small" sx={{ fontSize: '0.7rem' }} />
                      </td>
                      <td style={{ textAlign: 'right', fontFamily: 'monospace' }}>{a.coauthored_commits_30d || '—'}</td>
                      <td style={{ textAlign: 'right', fontFamily: 'monospace' }}>{a.handoff_count || '—'}</td>
                      <td style={{ textAlign: 'right', color: '#888' }}>{formatRelative(a.last_seen_at)}</td>
                      <td style={{ textAlign: 'right', color: '#bbb' }}>—</td>
                    </tr>
                  ))}
                </Box>
              </Box>
            </Box>
            <Typography variant="caption" color="text.secondary" sx={{ display: 'block', mt: 1.5, fontStyle: 'italic' }}>
              "Tokens left" is not tracked yet — requires per-provider auth (Anthropic / OpenAI / Google).
              See Operational TODO below.
            </Typography>
          </Grid>
          <Grid item xs={12} md={5}>
            <Typography variant="subtitle2" sx={{ fontWeight: 600, mb: 1 }}>Recent handoffs</Typography>
            <Box sx={{ maxHeight: 280, overflow: 'auto' }}>
              {data.recent_handoffs.length === 0 && (
                <Typography variant="caption" color="text.secondary">No handoffs recorded.</Typography>
              )}
              {data.recent_handoffs.map((h) => (
                <Box key={h.path} sx={{ py: 0.75, borderBottom: '1px solid', borderColor: 'divider' }}>
                  <Stack direction="row" spacing={1} alignItems="center">
                    <Chip label={h.from} color={agentColor(h.from)} size="small" sx={{ fontSize: '0.65rem', height: 18 }} />
                    <Typography variant="caption" color="text.secondary">{formatRelative(h.at)}</Typography>
                  </Stack>
                  {h.branch && (
                    <Typography variant="caption" sx={{ display: 'block', fontFamily: 'monospace', fontSize: '0.7rem', color: 'text.secondary' }}>
                      {h.branch}{h.head ? ` @ ${h.head.slice(0, 8)}` : ''}
                    </Typography>
                  )}
                  <MuiLink
                    href={`https://github.com/GuitarAlchemist/ga/blob/main/${h.path}`}
                    target="_blank"
                    rel="noopener noreferrer"
                    sx={{ fontSize: '0.7rem' }}
                  >
                    {h.path.replace('state/handoffs/', '')}
                  </MuiLink>
                </Box>
              ))}
            </Box>
          </Grid>
        </Grid>
      )}
    </Paper>
  );
};

const ManifestBanner: React.FC = () => {
  const navigate = useNavigate();
  return (
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
          onClick={() => navigate('/test/manifest')}
          endIcon={<OpenInNewIcon />}
        >
          Open manifest
        </Button>
      </Stack>
    </Alert>
  );
};

// Bottom-up 5-layer model lifted from CLAUDE.md (Architecture section).
// Static content — if CLAUDE.md changes, this list should be updated to match.
const LayerMapCard: React.FC = () => {
  const layers = [
    { n: 1, name: 'Core', projects: 'GA.Core, GA.Domain.Core', desc: 'Pure primitives: Note, Interval, Fretboard' },
    { n: 2, name: 'Domain', projects: 'GA.Business.Core, GA.Business.Config, GA.BSP.Core', desc: 'Logic, YAML config, BSP' },
    { n: 3, name: 'Analysis', projects: 'GA.Business.Core.Harmony, GA.Business.Core.Fretboard', desc: 'Chord/scale, voice leading, spectral' },
    { n: 4, name: 'AI/ML', projects: 'GA.Business.ML', desc: 'Embeddings, vector search, RAG, OPTIC-K schema' },
    { n: 5, name: 'Orchestration', projects: 'GA.Business.Core.Orchestration, GA.Business.Assets, GA.Business.Intelligence', desc: 'Top layer; AI code lives at layer 4, orchestration at 5' },
  ];
  return (
    <Paper sx={{ p: 2 }}>
      <Stack direction="row" spacing={1} alignItems="center" sx={{ mb: 1 }}>
        <LayersIcon fontSize="small" sx={{ color: 'primary.main' }} />
        <Typography variant="h6">Five-layer model</Typography>
      </Stack>
      <Typography variant="caption" color="text.secondary" sx={{ display: 'block', mb: 1.5 }}>
        Strict bottom-up dependency. AI code in layer 4. Orchestration in layer 5. Never in lower layers. Full map:{' '}
        <MuiLink href="https://github.com/GuitarAlchemist/ga/blob/main/docs/architecture/layers.md" target="_blank" rel="noopener noreferrer">docs/architecture/layers.md</MuiLink>.
      </Typography>
      <Stack spacing={1}>
        {layers.map((l) => (
          <Box key={l.n} sx={{ p: 1.25, bgcolor: 'action.hover', borderRadius: 1, borderLeft: 3, borderColor: 'primary.main' }}>
            <Stack direction="row" spacing={1} alignItems="baseline">
              <Chip label={l.n} size="small" color="primary" sx={{ fontWeight: 700, height: 22 }} />
              <Typography variant="subtitle2" sx={{ fontWeight: 600 }}>{l.name}</Typography>
              <Typography variant="caption" sx={{ fontFamily: 'monospace', color: 'text.secondary' }}>{l.projects}</Typography>
            </Stack>
            <Typography variant="caption" color="text.secondary" sx={{ display: 'block', mt: 0.25 }}>{l.desc}</Typography>
          </Box>
        ))}
      </Stack>
    </Paper>
  );
};

type DevSubTab = 'summary' | 'architecture' | 'product' | 'project' | 'qa';
const DEV_SUB_TABS: DevSubTab[] = ['summary', 'architecture', 'product', 'project', 'qa'];

const readSubTabFromHash = (): DevSubTab => {
  if (typeof window === 'undefined') return 'summary';
  const m = window.location.hash.match(/^#dev\/(\w+)$/);
  if (m && (DEV_SUB_TABS as string[]).includes(m[1])) return m[1] as DevSubTab;
  return 'summary';
};

export const DevelopmentSection: React.FC = () => {
  const [subTab, setSubTab] = React.useState<DevSubTab>(readSubTabFromHash);

  // Keep URL hash in sync with the selected tab so /test#dev/qa is deep-linkable.
  React.useEffect(() => {
    const target = `#dev/${subTab}`;
    if (window.location.hash !== target) {
      window.history.replaceState(null, '', target);
    }
  }, [subTab]);

  // Listen for back/forward navigation that changes the hash externally.
  React.useEffect(() => {
    const onHashChange = () => setSubTab(readSubTabFromHash());
    window.addEventListener('hashchange', onHashChange);
    return () => window.removeEventListener('hashchange', onHashChange);
  }, []);

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

      <Box sx={{ borderBottom: 1, borderColor: 'divider' }}>
        <Tabs
          value={subTab}
          onChange={(_, v: DevSubTab) => setSubTab(v)}
          variant="scrollable"
          scrollButtons="auto"
          aria-label="Development sub-sections"
        >
          <Tab value="summary"      label="Summary"      icon={<DashboardIcon fontSize="small" />}   iconPosition="start" sx={{ minHeight: 44 }} />
          <Tab value="architecture" label="Architecture" icon={<AccountTreeIcon fontSize="small" />} iconPosition="start" sx={{ minHeight: 44 }} />
          <Tab value="product"      label="Product"      icon={<InventoryIcon fontSize="small" />}   iconPosition="start" sx={{ minHeight: 44 }} />
          <Tab value="project"      label="Project"      icon={<GroupsIcon fontSize="small" />}      iconPosition="start" sx={{ minHeight: 44 }} />
          <Tab value="qa"           label="QA"           icon={<VerifiedIcon fontSize="small" />}    iconPosition="start" sx={{ minHeight: 44 }} />
        </Tabs>
      </Box>

      {subTab === 'summary' && (
        <Stack spacing={2}>
          <OverviewSection />
          <ProcessHealthCard />
        </Stack>
      )}

      {subTab === 'architecture' && (
        <Grid container spacing={2}>
          <Grid item xs={12} md={6}>
            <LayerMapCard />
          </Grid>
          <Grid item xs={12} md={6}>
            <ArchitectureCard />
          </Grid>
        </Grid>
      )}

      {subTab === 'product' && (
        <Stack spacing={2}>
          <BacklogCard />
        </Stack>
      )}

      {subTab === 'project' && (
        <Grid container spacing={2}>
          <Grid item xs={12}>
            <ActivityCard />
          </Grid>
          <Grid item xs={12}>
            <AgentContributorsCard />
          </Grid>
          <Grid item xs={12}>
            <AgentCollaborationCard />
          </Grid>
          <Grid item xs={12}>
            <OperationalTodoCard />
          </Grid>
        </Grid>
      )}

      {subTab === 'qa' && (
        <Stack spacing={2}>
          <QualityCard />
          <ProcessHealthCard />
        </Stack>
      )}
    </Stack>
  );
};

export default DevelopmentSection;
