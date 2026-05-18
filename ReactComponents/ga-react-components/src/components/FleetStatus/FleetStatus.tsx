import React, { useEffect, useState } from 'react';
import {
  Box,
  Chip,
  CircularProgress,
  Container,
  Link,
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

// ---------------------------------------------------------------------------
// Types — mirror the JSON shape emitted by Scripts/fleet-status-generate.py
// ---------------------------------------------------------------------------

interface PullRequest {
  number: number;
  title: string;
  createdAt: string;
  ageDays: number | null;
  mergeable: string;
  isDraft: boolean;
  headRef: string;
  failingChecks: number;
  url: string;
  author: string;
}

interface InstallAuditSummary {
  available: boolean;
  score: number | null;
  maxScore: number | null;
  verdict: string | null;
  readiness: string | null;
  checkCount: number;
  failingChecks: string[];
}

interface FleetRepo {
  name: string;
  fullName: string;
  prs: PullRequest[];
  prCount: number;
  installAudit: InstallAuditSummary;
}

interface Initiative {
  id: string;
  name: string;
  phase?: string;
  description?: string;
  lastUpdated?: string;
}

interface Blocker {
  surface: string;
  hard?: string;
  soft?: string;
}

interface FleetMeta {
  generatedAt: string;
  commitSha: string;
  repoCount: number;
  totalPRs: number;
}

interface FleetStatusData {
  schemaVersion: string;
  meta: FleetMeta;
  repos: FleetRepo[];
  initiatives: Initiative[];
  blockers: Blocker[];
}

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------

const verdictColor = (verdict: string | null): 'success' | 'warning' | 'error' | 'default' => {
  switch (verdict) {
    case 'pass':
    case 'MERGEABLE':
      return 'success';
    case 'warn':
      return 'warning';
    case 'fail':
    case 'CONFLICTING':
      return 'error';
    default:
      return 'default';
  }
};

const formatAge = (days: number | null): string => {
  if (days === null || days === undefined) return '?';
  if (days === 0) return 'today';
  if (days === 1) return '1d';
  return `${days}d`;
};

// ---------------------------------------------------------------------------
// Component
// ---------------------------------------------------------------------------

const FleetStatus: React.FC = () => {
  const [data, setData] = useState<FleetStatusData | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    // Public assets are served from Vite's BASE_URL (root in dev,
    // /ga/ on GitHub Pages); bust cache on first paint.
    const base = (import.meta.env.BASE_URL || '/').replace(/\/$/, '');
    fetch(`${base}/fleet-status.json`, { cache: 'no-cache' })
      .then((r) => {
        if (!r.ok) throw new Error(`HTTP ${r.status}`);
        return r.json();
      })
      .then((d: FleetStatusData) => setData(d))
      .catch((e: Error) => setError(e.message));
  }, []);

  if (error) {
    return (
      <Container maxWidth="lg" sx={{ py: 4 }}>
        <Typography color="error">
          Could not load fleet-status.json: {error}
        </Typography>
        <Typography variant="caption" sx={{ color: '#666' }}>
          The page reads <code>/fleet-status.json</code>, baked into
          {' '}<code>public/</code> by the CI cron in{' '}
          <code>.github/workflows/fleet-status.yml</code>.
        </Typography>
      </Container>
    );
  }

  if (!data) {
    return (
      <Container maxWidth="lg" sx={{ py: 4 }}>
        <Stack direction="row" spacing={2} alignItems="center">
          <CircularProgress size={20} />
          <Typography>Loading fleet status…</Typography>
        </Stack>
      </Container>
    );
  }

  return (
    <Container maxWidth="lg" sx={{ py: 4 }}>
      {/* Header */}
      <Box sx={{ mb: 3 }}>
        <Typography variant="h4" sx={{ fontWeight: 'bold', mb: 0.5 }}>
          Fleet Status
        </Typography>
        <Typography variant="body2" sx={{ color: '#666' }}>
          What&apos;s happening across the 5 sibling repos right now.
        </Typography>
        <Typography variant="caption" sx={{ color: '#999', display: 'block', mt: 0.5 }}>
          {data.meta.totalPRs} open PR{data.meta.totalPRs === 1 ? '' : 's'} ·{' '}
          {data.meta.repoCount} repos · generated{' '}
          {new Date(data.meta.generatedAt).toLocaleString()}
        </Typography>
      </Box>

      {/* Section 1: Active PRs */}
      <Typography variant="h6" sx={{ mt: 4, mb: 1.5, fontWeight: 'bold' }}>
        Active PRs
      </Typography>
      <Stack spacing={2.5}>
        {data.repos.map((repo) => (
          <Paper key={repo.name} variant="outlined" sx={{ p: 2 }}>
            <Stack direction="row" spacing={1} alignItems="baseline" sx={{ mb: 1 }}>
              <Typography variant="subtitle1" sx={{ fontWeight: 'bold' }}>
                {repo.name}
              </Typography>
              <Typography variant="caption" sx={{ color: '#666' }}>
                {repo.prCount} open
              </Typography>
            </Stack>
            {repo.prs.length === 0 ? (
              <Typography variant="body2" sx={{ color: '#888', fontStyle: 'italic' }}>
                No open PRs.
              </Typography>
            ) : (
              <TableContainer>
                <Table size="small">
                  <TableHead>
                    <TableRow>
                      <TableCell>#</TableCell>
                      <TableCell>Title</TableCell>
                      <TableCell align="right">Age</TableCell>
                      <TableCell>Mergeable</TableCell>
                      <TableCell align="right">Failing</TableCell>
                      <TableCell>Author</TableCell>
                    </TableRow>
                  </TableHead>
                  <TableBody>
                    {repo.prs.map((pr) => (
                      <TableRow key={pr.number}>
                        <TableCell sx={{ fontFamily: 'monospace' }}>
                          <Link href={pr.url} target="_blank" rel="noreferrer">
                            #{pr.number}
                          </Link>
                        </TableCell>
                        <TableCell sx={{ maxWidth: 480 }}>
                          {pr.title}
                          {pr.isDraft && (
                            <Chip
                              label="draft"
                              size="small"
                              sx={{ ml: 1, height: 18, fontSize: 10 }}
                            />
                          )}
                        </TableCell>
                        <TableCell align="right">{formatAge(pr.ageDays)}</TableCell>
                        <TableCell>
                          <Chip
                            label={pr.mergeable}
                            size="small"
                            color={verdictColor(pr.mergeable)}
                            sx={{ height: 20, fontSize: 11 }}
                          />
                        </TableCell>
                        <TableCell
                          align="right"
                          sx={{
                            color: pr.failingChecks > 0 ? '#c62828' : 'inherit',
                            fontWeight: pr.failingChecks > 0 ? 600 : 'normal',
                          }}
                        >
                          {pr.failingChecks}
                        </TableCell>
                        <TableCell sx={{ color: '#666', fontSize: 12 }}>
                          {pr.author}
                        </TableCell>
                      </TableRow>
                    ))}
                  </TableBody>
                </Table>
              </TableContainer>
            )}
          </Paper>
        ))}
      </Stack>

      {/* Section 2: Install-audit fleet */}
      <Typography variant="h6" sx={{ mt: 5, mb: 1.5, fontWeight: 'bold' }}>
        Install-audit fleet score
      </Typography>
      <TableContainer component={Paper} variant="outlined">
        <Table size="small">
          <TableHead>
            <TableRow>
              <TableCell>Repo</TableCell>
              <TableCell>Verdict</TableCell>
              <TableCell align="right">Score</TableCell>
              <TableCell>Readiness</TableCell>
              <TableCell>Failing checks</TableCell>
            </TableRow>
          </TableHead>
          <TableBody>
            {data.repos.map((repo) => {
              const a = repo.installAudit;
              return (
                <TableRow key={repo.name}>
                  <TableCell sx={{ fontWeight: 'bold' }}>{repo.name}</TableCell>
                  <TableCell>
                    {a.available && a.verdict ? (
                      <Chip
                        label={a.verdict}
                        size="small"
                        color={verdictColor(a.verdict)}
                        sx={{ height: 20, fontSize: 11 }}
                      />
                    ) : (
                      <Typography variant="caption" sx={{ color: '#999' }}>
                        —
                      </Typography>
                    )}
                  </TableCell>
                  <TableCell align="right" sx={{ fontFamily: 'monospace' }}>
                    {a.available && a.score !== null
                      ? `${a.score}/${a.maxScore}`
                      : '—'}
                  </TableCell>
                  <TableCell sx={{ color: '#666' }}>
                    {a.readiness || '—'}
                  </TableCell>
                  <TableCell sx={{ fontSize: 12, color: '#666' }}>
                    {a.failingChecks.length > 0
                      ? a.failingChecks.join(', ')
                      : '—'}
                  </TableCell>
                </TableRow>
              );
            })}
          </TableBody>
        </Table>
      </TableContainer>

      {/* Section 3: Active initiatives */}
      <Typography variant="h6" sx={{ mt: 5, mb: 1.5, fontWeight: 'bold' }}>
        Active initiatives
      </Typography>
      <Stack spacing={1.5}>
        {data.initiatives.length === 0 ? (
          <Typography variant="body2" sx={{ color: '#888', fontStyle: 'italic' }}>
            No active initiatives. Seed{' '}
            <code>state/fleet-status-initiatives.json</code>.
          </Typography>
        ) : (
          data.initiatives.map((init) => (
            <Paper key={init.id} variant="outlined" sx={{ p: 1.5 }}>
              <Stack direction="row" spacing={1} alignItems="baseline" flexWrap="wrap">
                <Typography variant="subtitle2" sx={{ fontWeight: 'bold' }}>
                  {init.name}
                </Typography>
                {init.phase && (
                  <Chip
                    label={init.phase}
                    size="small"
                    variant="outlined"
                    sx={{ height: 20, fontSize: 11 }}
                  />
                )}
                {init.lastUpdated && (
                  <Typography variant="caption" sx={{ color: '#999' }}>
                    updated {init.lastUpdated}
                  </Typography>
                )}
              </Stack>
              {init.description && (
                <Typography variant="body2" sx={{ mt: 0.5, color: '#444' }}>
                  {init.description}
                </Typography>
              )}
            </Paper>
          ))
        )}
      </Stack>

      {/* Section 4: Blockers per surface */}
      <Typography variant="h6" sx={{ mt: 5, mb: 1.5, fontWeight: 'bold' }}>
        Blockers per surface
      </Typography>
      <TableContainer component={Paper} variant="outlined">
        <Table size="small">
          <TableHead>
            <TableRow>
              <TableCell sx={{ width: '20%' }}>Surface</TableCell>
              <TableCell sx={{ width: '40%' }}>Hard blocker</TableCell>
              <TableCell sx={{ width: '40%' }}>Soft blocker</TableCell>
            </TableRow>
          </TableHead>
          <TableBody>
            {data.blockers.map((b) => (
              <TableRow key={b.surface}>
                <TableCell sx={{ fontWeight: 'bold' }}>{b.surface}</TableCell>
                <TableCell
                  sx={{
                    color: b.hard && b.hard !== 'None' ? '#c62828' : '#666',
                  }}
                >
                  {b.hard || '—'}
                </TableCell>
                <TableCell sx={{ color: '#666' }}>{b.soft || '—'}</TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      </TableContainer>

      {/* Footer */}
      <Box sx={{ mt: 5, pt: 2, borderTop: '1px solid #eee', color: '#999' }}>
        <Typography variant="caption" display="block">
          Last updated: {new Date(data.meta.generatedAt).toLocaleString()} (UTC:{' '}
          {data.meta.generatedAt})
        </Typography>
        {data.meta.commitSha && (
          <Typography variant="caption" display="block" sx={{ fontFamily: 'monospace' }}>
            Commit: {data.meta.commitSha.slice(0, 12)}
          </Typography>
        )}
        <Typography variant="caption" display="block">
          Markdown mirror:{' '}
          <Link
            href="https://github.com/GuitarAlchemist/ga/blob/main/state/fleet-status.md"
            target="_blank"
            rel="noreferrer"
          >
            state/fleet-status.md
          </Link>
        </Typography>
      </Box>
    </Container>
  );
};

export default FleetStatus;
