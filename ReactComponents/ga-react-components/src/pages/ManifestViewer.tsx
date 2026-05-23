import React, { useCallback, useEffect, useState } from 'react';
import {
  Alert,
  Box,
  Button,
  Chip,
  CircularProgress,
  Container,
  Divider,
  IconButton,
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
import { useNavigate } from 'react-router-dom';
import ArrowBackIcon from '@mui/icons-material/ArrowBack';
import RefreshIcon from '@mui/icons-material/Refresh';
import ContentCopyIcon from '@mui/icons-material/ContentCopy';
import OpenInNewIcon from '@mui/icons-material/OpenInNew';
import WarningAmberIcon from '@mui/icons-material/WarningAmber';

interface ServiceTopology {
  name: string;
  port: number;
  public_path: string;
  expected: string;
}

interface BacklogSection { title: string; item_count: number }
interface BacklogEpic {
  title: string;
  total_items: number;
  shipped: number;
  active: number;
  backlog: number;
  progress_pct: number;
}
interface BacklogSummary {
  total_sections: number;
  top_sections: BacklogSection[];
  total_epics?: number;
  total_items?: number;
  total_shipped?: number;
  overall_progress_pct?: number;
  epics?: BacklogEpic[];
}

interface QualityEntry { source: string; data: Record<string, unknown> }
interface QualityPayload { domains: Record<string, QualityEntry>; regressions?: string[] }

interface ActivityCommit { sha: string; short_sha: string; author: string; date: string; subject: string }

interface ArchDoc { file: string; title: string; modified_at: string; size: number }

interface Manifest {
  schema_version: string;
  generated_at: string;
  repo: string;
  public_url: string;
  endpoints: Record<string, string>;
  services: ServiceTopology[];
  backlog: BacklogSummary | null;
  quality: QualityPayload;
  activity: ActivityCommit[] | { error: string };
  architecture: ArchDoc[];
}

const formatBytes = (n: number): string => (n < 1024 ? `${n} B` : `${Math.round(n / 1024)} KB`);
const relative = (iso: string): string => {
  const diff = Date.now() - new Date(iso).getTime();
  const minutes = Math.floor(diff / 60000);
  if (minutes < 1) return 'just now';
  if (minutes < 60) return `${minutes}m ago`;
  const hours = Math.floor(minutes / 60);
  if (hours < 24) return `${hours}h ago`;
  return `${Math.floor(hours / 24)}d ago`;
};

const SectionHeader: React.FC<{ title: string; count?: number; right?: React.ReactNode }> = ({ title, count, right }) => (
  <Stack direction="row" justifyContent="space-between" alignItems="center" sx={{ mb: 1 }}>
    <Stack direction="row" spacing={1} alignItems="center">
      <Typography variant="h6">{title}</Typography>
      {count !== undefined && <Chip label={count} size="small" />}
    </Stack>
    {right}
  </Stack>
);

const CopyButton: React.FC<{ text: string; label: string }> = ({ text, label }) => {
  const [copied, setCopied] = useState(false);
  return (
    <Tooltip title={copied ? 'Copied!' : label}>
      <Button
        size="small"
        variant="outlined"
        startIcon={<ContentCopyIcon fontSize="small" />}
        onClick={() => {
          navigator.clipboard.writeText(text).then(() => {
            setCopied(true);
            setTimeout(() => setCopied(false), 2000);
          });
        }}
      >
        {label}
      </Button>
    </Tooltip>
  );
};

export const ManifestViewer: React.FC = () => {
  const navigate = useNavigate();
  const [manifest, setManifest] = useState<Manifest | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);

  const fetchManifest = useCallback(() => {
    setLoading(true);
    setError(null);
    fetch('/dev-data/manifest', { cache: 'no-store' })
      .then((r) => {
        if (!r.ok) throw new Error(`HTTP ${r.status}`);
        return r.json() as Promise<Manifest>;
      })
      .then((m) => { setManifest(m); setLoading(false); })
      .catch((e) => { setError(String(e.message ?? e)); setLoading(false); });
  }, []);

  useEffect(() => { fetchManifest(); }, [fetchManifest]);

  const curlCommand = manifest
    ? `curl -sS ${manifest.public_url}/dev-data/manifest | jq .`
    : 'curl -sS https://demos.guitaralchemist.com/dev-data/manifest | jq .';

  const renderQualityDomain = (name: string, entry: QualityEntry) => {
    const d = entry.data;
    const metricName = d.metric_name as string | undefined;
    const metricValue = d.metric_value as number | undefined;
    const oracleStatus = d.oracle_status as string | undefined;
    const emittedAt = d.emitted_at as string | undefined;
    const summary = d.summary as string | undefined;
    const problems = d.problems as unknown[] | undefined;
    const statusColor = oracleStatus === 'ok' ? 'success'
      : oracleStatus === 'warn' ? 'warning'
      : oracleStatus ? 'error' : 'default';

    return (
      <TableRow key={name}>
        <TableCell sx={{ fontWeight: 600, whiteSpace: 'nowrap' }}>{name}</TableCell>
        <TableCell>
          {oracleStatus && (
            <Chip
              label={oracleStatus}
              color={statusColor as 'success' | 'warning' | 'error' | 'default'}
              size="small"
              sx={{ fontSize: '0.7rem', height: 20 }}
            />
          )}
        </TableCell>
        <TableCell sx={{ fontFamily: 'monospace', fontSize: '0.8rem' }}>
          {metricName ?? '—'}
        </TableCell>
        <TableCell sx={{ fontFamily: 'monospace', fontWeight: 600 }}>
          {metricValue !== undefined ? metricValue : '—'}
        </TableCell>
        <TableCell sx={{ fontSize: '0.8rem' }}>
          {emittedAt ? new Date(emittedAt).toLocaleDateString() : '—'}
        </TableCell>
        <TableCell sx={{ fontFamily: 'monospace', fontSize: '0.75rem', color: 'text.secondary' }}>
          {entry.source}
        </TableCell>
        <TableCell sx={{ fontSize: '0.8rem', maxWidth: 280 }}>
          {summary ?? '—'}
          {problems && problems.length > 0 && (
            <Typography variant="caption" color="warning.main" sx={{ display: 'block' }}>
              {problems.length} problem{problems.length === 1 ? '' : 's'} reported
            </Typography>
          )}
        </TableCell>
      </TableRow>
    );
  };

  const activityCommits = manifest && Array.isArray(manifest.activity) ? manifest.activity : [];
  const activityError = manifest && !Array.isArray(manifest.activity) ? (manifest.activity as { error: string }).error : null;

  return (
    <Container maxWidth="xl" sx={{ py: 4 }}>
      <Stack direction="row" justifyContent="space-between" alignItems="center" sx={{ mb: 3 }}>
        <Stack direction="row" spacing={1.5} alignItems="center">
          <IconButton onClick={() => navigate('/test')} size="small"><ArrowBackIcon /></IconButton>
          <Box>
            <Typography variant="h4" sx={{ fontWeight: 700, m: 0 }}>Dev Manifest</Typography>
            <Typography variant="body2" color="text.secondary">
              Live project context for humans and AI tools — schema_version{' '}
              <Box component="code" sx={{ px: 0.5, bgcolor: 'action.hover', borderRadius: 0.5, fontSize: '0.85rem' }}>
                {manifest?.schema_version ?? '…'}
              </Box>
              {manifest && <> · generated {relative(manifest.generated_at)}</>}
            </Typography>
          </Box>
        </Stack>
        <Stack direction="row" spacing={1}>
          <CopyButton text={curlCommand} label="Copy curl" />
          {manifest && <CopyButton text={JSON.stringify(manifest, null, 2)} label="Copy JSON" />}
          <Tooltip title="Refresh"><IconButton onClick={fetchManifest} disabled={loading}><RefreshIcon /></IconButton></Tooltip>
        </Stack>
      </Stack>

      {error && <Alert severity="error" sx={{ mb: 2 }}>Failed to load manifest: {error}</Alert>}
      {loading && !manifest && <Box sx={{ display: 'flex', justifyContent: 'center', py: 8 }}><CircularProgress /></Box>}

      {manifest && (
        <Stack spacing={3}>
          {/* AI-tool curl bootstrap hint */}
          <Alert severity="info">
            <Typography variant="body2" sx={{ mb: 0.5 }}>
              <strong>For AI tools:</strong> fetch this manifest to bootstrap project context. No auth required, dev-only middleware.
            </Typography>
            <Box
              component="code"
              sx={{
                display: 'block',
                p: 1,
                bgcolor: 'background.paper',
                border: '1px solid',
                borderColor: 'divider',
                borderRadius: 0.5,
                fontFamily: 'monospace',
                fontSize: '0.8rem',
                overflow: 'auto',
              }}
            >
              {curlCommand}
            </Box>
          </Alert>

          {/* Endpoints */}
          <Paper sx={{ p: 2 }}>
            <SectionHeader title="Endpoints" count={Object.keys(manifest.endpoints).length} />
            <TableContainer>
              <Table size="small">
                <TableHead>
                  <TableRow>
                    <TableCell sx={{ fontWeight: 700 }}>Key</TableCell>
                    <TableCell sx={{ fontWeight: 700 }}>Path</TableCell>
                    <TableCell sx={{ fontWeight: 700, width: 100 }}>Open</TableCell>
                  </TableRow>
                </TableHead>
                <TableBody>
                  {Object.entries(manifest.endpoints).map(([key, p]) => (
                    <TableRow key={key} hover>
                      <TableCell sx={{ fontFamily: 'monospace' }}>{key}</TableCell>
                      <TableCell sx={{ fontFamily: 'monospace', fontSize: '0.85rem' }}>{p}</TableCell>
                      <TableCell>
                        <MuiLink href={p} target="_blank" rel="noopener noreferrer" sx={{ fontSize: '0.8rem' }}>
                          raw <OpenInNewIcon fontSize="inherit" />
                        </MuiLink>
                      </TableCell>
                    </TableRow>
                  ))}
                </TableBody>
              </Table>
            </TableContainer>
          </Paper>

          {/* Services */}
          <Paper sx={{ p: 2 }}>
            <SectionHeader title="Services" count={manifest.services.length} />
            <TableContainer>
              <Table size="small">
                <TableHead>
                  <TableRow>
                    <TableCell sx={{ fontWeight: 700 }}>Service</TableCell>
                    <TableCell sx={{ fontWeight: 700, width: 80 }}>Port</TableCell>
                    <TableCell sx={{ fontWeight: 700 }}>Public path</TableCell>
                    <TableCell sx={{ fontWeight: 700 }}>Expected</TableCell>
                  </TableRow>
                </TableHead>
                <TableBody>
                  {manifest.services.map((s) => (
                    <TableRow key={s.name} hover>
                      <TableCell sx={{ fontWeight: 500 }}>{s.name}</TableCell>
                      <TableCell sx={{ fontFamily: 'monospace' }}>{s.port > 0 ? s.port : '—'}</TableCell>
                      <TableCell sx={{ fontFamily: 'monospace', fontSize: '0.85rem' }}>{s.public_path}</TableCell>
                      <TableCell sx={{ fontSize: '0.85rem' }}>{s.expected}</TableCell>
                    </TableRow>
                  ))}
                </TableBody>
              </Table>
            </TableContainer>
          </Paper>

          {/* Backlog summary */}
          <Paper sx={{ p: 2 }}>
            <SectionHeader
              title="Backlog"
              count={manifest.backlog?.total_sections}
              right={
                <MuiLink
                  href={`https://github.com/${manifest.repo}/blob/main/BACKLOG.md`}
                  target="_blank"
                  rel="noopener noreferrer"
                  sx={{ fontSize: '0.85rem', display: 'flex', alignItems: 'center', gap: 0.5 }}
                >
                  BACKLOG.md <OpenInNewIcon fontSize="inherit" />
                </MuiLink>
              }
            />
            {!manifest.backlog && <Typography color="text.secondary">BACKLOG.md not found.</Typography>}
            {manifest.backlog?.epics && manifest.backlog.epics.length > 0 && (
              <>
                <Typography variant="caption" color="text.secondary" sx={{ display: 'block', mb: 1 }}>
                  Overall: <strong>{manifest.backlog.overall_progress_pct ?? 0}%</strong> shipped
                  ({manifest.backlog.total_shipped ?? 0} of {manifest.backlog.total_items ?? 0} items across {manifest.backlog.total_epics ?? 0} epics)
                </Typography>
                <TableContainer>
                  <Table size="small">
                    <TableHead>
                      <TableRow>
                        <TableCell sx={{ fontWeight: 700 }}>Epic</TableCell>
                        <TableCell sx={{ fontWeight: 700, width: 70, textAlign: 'right' }}>Total</TableCell>
                        <TableCell sx={{ fontWeight: 700, width: 80, textAlign: 'right' }}>Shipped</TableCell>
                        <TableCell sx={{ fontWeight: 700, width: 80, textAlign: 'right' }}>Active</TableCell>
                        <TableCell sx={{ fontWeight: 700, width: 80, textAlign: 'right' }}>Backlog</TableCell>
                        <TableCell sx={{ fontWeight: 700, width: 80, textAlign: 'right' }}>Progress</TableCell>
                      </TableRow>
                    </TableHead>
                    <TableBody>
                      {manifest.backlog.epics.map((e) => (
                        <TableRow key={e.title} hover>
                          <TableCell>{e.title}</TableCell>
                          <TableCell sx={{ fontFamily: 'monospace', textAlign: 'right' }}>{e.total_items}</TableCell>
                          <TableCell sx={{ fontFamily: 'monospace', textAlign: 'right', color: 'success.main' }}>{e.shipped}</TableCell>
                          <TableCell sx={{ fontFamily: 'monospace', textAlign: 'right', color: 'warning.main' }}>{e.active}</TableCell>
                          <TableCell sx={{ fontFamily: 'monospace', textAlign: 'right', color: 'text.secondary' }}>{e.backlog}</TableCell>
                          <TableCell sx={{ fontFamily: 'monospace', textAlign: 'right', fontWeight: 600 }}>
                            {e.total_items > 0 ? `${e.progress_pct}%` : '—'}
                          </TableCell>
                        </TableRow>
                      ))}
                    </TableBody>
                  </Table>
                </TableContainer>
              </>
            )}
            {manifest.backlog && (!manifest.backlog.epics || manifest.backlog.epics.length === 0) && (
              <TableContainer>
                <Table size="small">
                  <TableHead>
                    <TableRow>
                      <TableCell sx={{ fontWeight: 700 }}>Section</TableCell>
                      <TableCell sx={{ fontWeight: 700, width: 100 }}>Items</TableCell>
                    </TableRow>
                  </TableHead>
                  <TableBody>
                    {manifest.backlog.top_sections.map((s, i) => (
                      <TableRow key={`${i}-${s.title}`} hover>
                        <TableCell>{s.title}</TableCell>
                        <TableCell sx={{ fontFamily: 'monospace' }}>{s.item_count}</TableCell>
                      </TableRow>
                    ))}
                  </TableBody>
                </Table>
              </TableContainer>
            )}
          </Paper>

          {/* Quality */}
          <Paper sx={{ p: 2 }}>
            <SectionHeader
              title="Quality"
              count={Object.keys(manifest.quality.domains).length}
              right={
                <MuiLink
                  href={`https://github.com/${manifest.repo}/blob/main/docs/quality/README.md`}
                  target="_blank"
                  rel="noopener noreferrer"
                  sx={{ fontSize: '0.85rem', display: 'flex', alignItems: 'center', gap: 0.5 }}
                >
                  Full trend report <OpenInNewIcon fontSize="inherit" />
                </MuiLink>
              }
            />
            {manifest.quality.regressions && manifest.quality.regressions.length > 0 && (
              <Alert severity="warning" icon={<WarningAmberIcon />} sx={{ mb: 2 }}>
                <strong>{manifest.quality.regressions.length} regression{manifest.quality.regressions.length === 1 ? '' : 's'}:</strong>
                <Box component="ul" sx={{ pl: 2, m: 0, mt: 0.5 }}>
                  {manifest.quality.regressions.map((r, i) => (
                    <li key={i}><Typography variant="body2" component="span" sx={{ fontFamily: 'monospace' }}>{r}</Typography></li>
                  ))}
                </Box>
              </Alert>
            )}
            <TableContainer>
              <Table size="small">
                <TableHead>
                  <TableRow>
                    <TableCell sx={{ fontWeight: 700 }}>Domain</TableCell>
                    <TableCell sx={{ fontWeight: 700, width: 90 }}>Status</TableCell>
                    <TableCell sx={{ fontWeight: 700 }}>Metric</TableCell>
                    <TableCell sx={{ fontWeight: 700, width: 80 }}>Value</TableCell>
                    <TableCell sx={{ fontWeight: 700, width: 110 }}>Emitted</TableCell>
                    <TableCell sx={{ fontWeight: 700 }}>Source</TableCell>
                    <TableCell sx={{ fontWeight: 700 }}>Summary</TableCell>
                  </TableRow>
                </TableHead>
                <TableBody>
                  {Object.entries(manifest.quality.domains).map(([name, entry]) => renderQualityDomain(name, entry))}
                </TableBody>
              </Table>
            </TableContainer>
          </Paper>

          {/* Activity */}
          <Paper sx={{ p: 2 }}>
            <SectionHeader title="Recent Activity" count={activityCommits.length} />
            {activityError && <Alert severity="error">git log failed: {activityError}</Alert>}
            {activityCommits.length > 0 && (
              <TableContainer>
                <Table size="small">
                  <TableHead>
                    <TableRow>
                      <TableCell sx={{ fontWeight: 700, width: 110 }}>SHA</TableCell>
                      <TableCell sx={{ fontWeight: 700 }}>Subject</TableCell>
                      <TableCell sx={{ fontWeight: 700, width: 180 }}>Author</TableCell>
                      <TableCell sx={{ fontWeight: 700, width: 110 }}>When</TableCell>
                    </TableRow>
                  </TableHead>
                  <TableBody>
                    {activityCommits.map((c) => (
                      <TableRow key={c.sha} hover>
                        <TableCell>
                          <MuiLink
                            href={`https://github.com/${manifest.repo}/commit/${c.sha}`}
                            target="_blank"
                            rel="noopener noreferrer"
                            sx={{ fontFamily: 'monospace', fontSize: '0.8rem' }}
                          >
                            {c.short_sha}
                          </MuiLink>
                        </TableCell>
                        <TableCell sx={{ fontSize: '0.875rem' }}>{c.subject}</TableCell>
                        <TableCell sx={{ fontSize: '0.8rem', color: 'text.secondary' }}>{c.author}</TableCell>
                        <TableCell sx={{ fontSize: '0.8rem', color: 'text.secondary' }} title={new Date(c.date).toLocaleString()}>
                          {relative(c.date)}
                        </TableCell>
                      </TableRow>
                    ))}
                  </TableBody>
                </Table>
              </TableContainer>
            )}
          </Paper>

          {/* Architecture */}
          <Paper sx={{ p: 2 }}>
            <SectionHeader
              title="Architecture"
              count={manifest.architecture.length}
              right={
                <MuiLink
                  href={`https://github.com/${manifest.repo}/tree/main/docs/architecture`}
                  target="_blank"
                  rel="noopener noreferrer"
                  sx={{ fontSize: '0.85rem', display: 'flex', alignItems: 'center', gap: 0.5 }}
                >
                  docs/architecture <OpenInNewIcon fontSize="inherit" />
                </MuiLink>
              }
            />
            {manifest.architecture.length === 0 && (
              <Typography color="text.secondary">No docs in docs/architecture/.</Typography>
            )}
            {manifest.architecture.length > 0 && (
              <TableContainer>
                <Table size="small">
                  <TableHead>
                    <TableRow>
                      <TableCell sx={{ fontWeight: 700 }}>Title</TableCell>
                      <TableCell sx={{ fontWeight: 700 }}>File</TableCell>
                      <TableCell sx={{ fontWeight: 700, width: 130 }}>Modified</TableCell>
                      <TableCell sx={{ fontWeight: 700, width: 80 }}>Size</TableCell>
                    </TableRow>
                  </TableHead>
                  <TableBody>
                    {manifest.architecture.map((doc) => (
                      <TableRow key={doc.file} hover>
                        <TableCell>
                          <MuiLink
                            href={`https://github.com/${manifest.repo}/blob/main/docs/architecture/${doc.file}`}
                            target="_blank"
                            rel="noopener noreferrer"
                            sx={{ fontWeight: 500 }}
                          >
                            {doc.title}
                          </MuiLink>
                        </TableCell>
                        <TableCell sx={{ fontFamily: 'monospace', fontSize: '0.8rem', color: 'text.secondary' }}>
                          {doc.file}
                        </TableCell>
                        <TableCell sx={{ fontSize: '0.8rem' }}>
                          {new Date(doc.modified_at).toLocaleDateString()}
                        </TableCell>
                        <TableCell sx={{ fontFamily: 'monospace', fontSize: '0.8rem' }}>
                          {formatBytes(doc.size)}
                        </TableCell>
                      </TableRow>
                    ))}
                  </TableBody>
                </Table>
              </TableContainer>
            )}
          </Paper>

          <Divider />

          {/* Raw JSON */}
          <Paper sx={{ p: 2 }}>
            <SectionHeader
              title="Raw manifest JSON"
              right={<CopyButton text={JSON.stringify(manifest, null, 2)} label="Copy JSON" />}
            />
            <Box
              component="pre"
              sx={{
                m: 0,
                p: 2,
                bgcolor: 'action.hover',
                borderRadius: 1,
                fontSize: '0.75rem',
                fontFamily: 'monospace',
                overflow: 'auto',
                maxHeight: 500,
              }}
            >
              {JSON.stringify(manifest, null, 2)}
            </Box>
          </Paper>
        </Stack>
      )}
    </Container>
  );
};

export default ManifestViewer;
