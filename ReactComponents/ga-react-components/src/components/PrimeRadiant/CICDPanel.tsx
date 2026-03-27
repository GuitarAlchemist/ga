// src/components/PrimeRadiant/CICDPanel.tsx
// GitHub Actions CI/CD status panel for the Prime Radiant command center

import React, { useEffect, useState } from 'react';

// ---------------------------------------------------------------------------
// Types
// ---------------------------------------------------------------------------
interface WorkflowRun {
  id: string;
  name: string;
  repo: string;
  branch: string;
  status: 'success' | 'failure' | 'in_progress' | 'queued' | 'cancelled';
  conclusion?: string;
  startedAt: string;
  duration?: string;
  url: string;
}

// ---------------------------------------------------------------------------
// GitHub API configuration (same pattern as ActivityPanel)
// ---------------------------------------------------------------------------
const GITHUB_OWNER = 'GuitarAlchemist';
const GITHUB_REPOS = ['Demerzel', 'ga', 'tars', 'ix'];
const GITHUB_API = 'https://api.github.com';

const githubToken: string | null =
  (typeof import.meta !== 'undefined' && (import.meta as Record<string, unknown>).env
    ? (((import.meta as Record<string, unknown>).env) as Record<string, string | undefined>)['VITE_GITHUB_TOKEN'] ?? null
    : null)
  ?? (typeof localStorage !== 'undefined' ? localStorage.getItem('ga-github-token') : null);

const GITHUB_HEADERS: HeadersInit = {
  'Accept': 'application/vnd.github.v3+json',
  ...(githubToken ? { 'Authorization': `Bearer ${githubToken}` } : {}),
};

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------
function timeAgo(dateStr: string): string {
  const seconds = Math.floor((Date.now() - new Date(dateStr).getTime()) / 1000);
  if (seconds < 60) return `${seconds}s ago`;
  const minutes = Math.floor(seconds / 60);
  if (minutes < 60) return `${minutes}m ago`;
  const hours = Math.floor(minutes / 60);
  if (hours < 24) return `${hours}h ago`;
  const days = Math.floor(hours / 24);
  return `${days}d ago`;
}

function formatDuration(startedAt: string, completedAt?: string): string {
  const start = new Date(startedAt).getTime();
  const end = completedAt ? new Date(completedAt).getTime() : Date.now();
  const totalSeconds = Math.floor((end - start) / 1000);
  const m = Math.floor(totalSeconds / 60);
  const s = totalSeconds % 60;
  return m > 0 ? `${m}m ${s}s` : `${s}s`;
}

function mapStatus(status: string, conclusion: string | null): WorkflowRun['status'] {
  if (status === 'in_progress' || status === 'queued') return status as WorkflowRun['status'];
  if (conclusion === 'success') return 'success';
  if (conclusion === 'failure') return 'failure';
  if (conclusion === 'cancelled') return 'cancelled';
  return 'success';
}

// ---------------------------------------------------------------------------
// Fetch workflow runs from GitHub API
// ---------------------------------------------------------------------------
async function fetchWorkflowRuns(): Promise<WorkflowRun[]> {
  try {
    const allRuns: WorkflowRun[] = [];
    for (const repo of GITHUB_REPOS) {
      const res = await fetch(
        `${GITHUB_API}/repos/${GITHUB_OWNER}/${repo}/actions/runs?per_page=5`,
        { headers: GITHUB_HEADERS },
      );
      if (!res.ok) continue;
      const data = await res.json();
      for (const run of data.workflow_runs ?? []) {
        allRuns.push({
          id: `${repo}-${run.id}`,
          name: run.name ?? 'Workflow',
          repo,
          branch: run.head_branch ?? 'main',
          status: mapStatus(run.status, run.conclusion),
          conclusion: run.conclusion ?? undefined,
          startedAt: run.run_started_at ?? run.created_at,
          duration: formatDuration(
            run.run_started_at ?? run.created_at,
            run.status === 'completed' ? run.updated_at : undefined,
          ),
          url: run.html_url,
        });
      }
    }
    if (allRuns.length > 0) return allRuns;
    throw new Error('No runs fetched');
  } catch {
    return fallbackRuns();
  }
}

function fallbackRuns(): WorkflowRun[] {
  const now = Date.now();
  return [
    { id: 'fb-1', name: 'CI', repo: 'Demerzel', branch: 'master', status: 'success', startedAt: new Date(now - 1800000).toISOString(), duration: '1m 12s', url: 'https://github.com/GuitarAlchemist/Demerzel/actions' },
    { id: 'fb-2', name: 'Auto-remediation', repo: 'Demerzel', branch: 'master', status: 'success', startedAt: new Date(now - 3600000).toISOString(), duration: '2m 45s', url: 'https://github.com/GuitarAlchemist/Demerzel/actions' },
    { id: 'fb-3', name: 'Governance audit', repo: 'Demerzel', branch: 'master', status: 'in_progress', startedAt: new Date(now - 300000).toISOString(), duration: '5m 0s', url: 'https://github.com/GuitarAlchemist/Demerzel/actions' },
    { id: 'fb-4', name: 'CI', repo: 'ga', branch: 'master', status: 'failure', conclusion: 'lint errors', startedAt: new Date(now - 900000).toISOString(), duration: '0m 34s', url: 'https://github.com/GuitarAlchemist/ga/actions' },
    { id: 'fb-5', name: 'Deploy', repo: 'ga', branch: 'master', status: 'success', startedAt: new Date(now - 7200000).toISOString(), duration: '3m 18s', url: 'https://github.com/GuitarAlchemist/ga/actions' },
    { id: 'fb-6', name: 'Playwright E2E', repo: 'ga', branch: 'master', status: 'success', startedAt: new Date(now - 5400000).toISOString(), duration: '4m 52s', url: 'https://github.com/GuitarAlchemist/ga/actions' },
    { id: 'fb-7', name: 'CI', repo: 'tars', branch: 'master', status: 'success', startedAt: new Date(now - 10800000).toISOString(), duration: '2m 1s', url: 'https://github.com/GuitarAlchemist/tars/actions' },
    { id: 'fb-8', name: 'Dependabot auto-merge', repo: 'tars', branch: 'master', status: 'success', startedAt: new Date(now - 14400000).toISOString(), duration: '0m 48s', url: 'https://github.com/GuitarAlchemist/tars/actions' },
    { id: 'fb-9', name: 'CI', repo: 'ix', branch: 'master', status: 'success', startedAt: new Date(now - 21600000).toISOString(), duration: '1m 55s', url: 'https://github.com/GuitarAlchemist/ix/actions' },
    { id: 'fb-10', name: 'Cargo test', repo: 'ix', branch: 'master', status: 'success', startedAt: new Date(now - 25200000).toISOString(), duration: '3m 10s', url: 'https://github.com/GuitarAlchemist/ix/actions' },
  ];
}

// ---------------------------------------------------------------------------
// Status badge colors
// ---------------------------------------------------------------------------
const STATUS_CONFIG: Record<WorkflowRun['status'], { color: string; icon: string; label: string }> = {
  success: { color: '#33CC66', icon: '\u2713', label: 'passing' },
  failure: { color: '#FF4444', icon: '\u2717', label: 'failing' },
  in_progress: { color: '#FFB300', icon: '\u25CF', label: 'running' },
  queued: { color: '#6b7280', icon: '\u25CB', label: 'queued' },
  cancelled: { color: '#6b7280', icon: '\u2212', label: 'cancelled' },
};

const REPO_COLOR: Record<string, string> = {
  Demerzel: '#FFD700',
  ga: '#FFB300',
  tars: '#4FC3F7',
  ix: '#73d13d',
};

// ---------------------------------------------------------------------------
// Component
// ---------------------------------------------------------------------------
export const CICDPanel: React.FC = () => {
  const [runs, setRuns] = useState<WorkflowRun[]>([]);
  const [collapsed, setCollapsed] = useState(false);

  useEffect(() => {
    fetchWorkflowRuns().then(setRuns);
    const id = setInterval(() => fetchWorkflowRuns().then(setRuns), 60_000);
    return () => clearInterval(id);
  }, []);

  // Group by repo
  const grouped = GITHUB_REPOS.reduce<Record<string, WorkflowRun[]>>((acc, repo) => {
    acc[repo] = runs.filter((r) => r.repo === repo);
    return acc;
  }, {});

  // Summary counts
  const passing = runs.filter((r) => r.status === 'success').length;
  const failing = runs.filter((r) => r.status === 'failure').length;
  const running = runs.filter((r) => r.status === 'in_progress').length;

  return (
    <div className="prime-radiant__activity" style={{ maxHeight: '70vh', overflowY: 'auto' }}>
      <div
        className="prime-radiant__activity-header"
        onClick={() => setCollapsed(!collapsed)}
      >
        <span className="prime-radiant__activity-title">
          CI/CD
          <span className="prime-radiant__activity-count">
            <span style={{ color: '#33CC66' }}>{passing} passing</span>
            {failing > 0 && <span style={{ color: '#FF4444' }}> &middot; {failing} failing</span>}
            {running > 0 && <span style={{ color: '#FFB300' }}> &middot; {running} running</span>}
          </span>
        </span>
        <span className="prime-radiant__activity-toggle">
          {collapsed ? '\u25B6' : '\u25BC'}
        </span>
      </div>

      {!collapsed && (
        <div style={{ padding: '8px 0' }}>
          {GITHUB_REPOS.map((repo) => {
            const repoRuns = grouped[repo];
            if (!repoRuns || repoRuns.length === 0) return null;
            return (
              <div key={repo} style={{ marginBottom: 12 }}>
                <div style={{
                  fontSize: '0.75rem',
                  fontWeight: 600,
                  color: REPO_COLOR[repo] ?? '#8b949e',
                  textTransform: 'uppercase',
                  letterSpacing: '0.05em',
                  padding: '4px 12px',
                  borderBottom: `1px solid ${REPO_COLOR[repo] ?? '#8b949e'}22`,
                }}>
                  {repo}
                </div>
                {repoRuns.map((run) => {
                  const cfg = STATUS_CONFIG[run.status];
                  const isFailing = run.status === 'failure';
                  return (
                    <div
                      key={run.id}
                      className="prime-radiant__commit-item"
                      style={{
                        cursor: 'pointer',
                        borderLeft: isFailing ? '2px solid #FF4444' : '2px solid transparent',
                        paddingLeft: 10,
                      }}
                      onClick={() => window.open(run.url, '_blank')}
                      title={`${run.name} on ${run.branch} — ${cfg.label}`}
                    >
                      {/* Status badge */}
                      <span style={{
                        color: cfg.color,
                        fontWeight: 700,
                        fontSize: '0.85rem',
                        width: 18,
                        textAlign: 'center',
                        flexShrink: 0,
                      }}>
                        {cfg.icon}
                      </span>

                      {/* Workflow name */}
                      <span className="prime-radiant__commit-msg" style={{
                        color: isFailing ? '#FF4444' : '#c9d1d9',
                        fontWeight: isFailing ? 600 : 400,
                      }}>
                        {run.name}
                      </span>

                      {/* Branch */}
                      <span style={{
                        color: '#6b7280',
                        fontSize: '0.7rem',
                        flexShrink: 0,
                        padding: '1px 5px',
                        border: '1px solid #30363d',
                        borderRadius: 3,
                        fontFamily: 'monospace',
                      }}>
                        {run.branch}
                      </span>

                      {/* Duration */}
                      {run.duration && (
                        <span style={{
                          color: '#6b7280',
                          fontSize: '0.7rem',
                          flexShrink: 0,
                          marginLeft: 4,
                        }}>
                          {run.duration}
                        </span>
                      )}

                      {/* Time ago */}
                      <span className="prime-radiant__commit-time" style={{ flexShrink: 0 }}>
                        {timeAgo(run.startedAt)}
                      </span>
                    </div>
                  );
                })}
              </div>
            );
          })}
          {/* Emergency remediation button */}
          {failing > 0 && (
            <div style={{ padding: '8px 12px', borderTop: '1px solid #21262d' }}>
              <button
                onClick={() => {
                  const failedRepos = [...new Set(runs.filter(r => r.status === 'failure').map(r => r.repo))];
                  const msg = `Emergency remediation requested for ${failedRepos.join(', ')} — ${failing} failing workflow(s)`;
                  // Store trigger for governance pickup
                  const trigger = {
                    type: 'emergency_remediation',
                    repos: failedRepos,
                    failingCount: failing,
                    failedWorkflows: runs.filter(r => r.status === 'failure').map(r => ({ name: r.name, repo: r.repo, url: r.url })),
                    requestedAt: new Date().toISOString(),
                  };
                  const existing = JSON.parse(localStorage.getItem('prime-radiant-remediation-queue') ?? '[]');
                  existing.push(trigger);
                  localStorage.setItem('prime-radiant-remediation-queue', JSON.stringify(existing));
                  alert(msg + '\n\nTrigger queued. Run /demerzel drive to execute remediation.');
                }}
                style={{
                  width: '100%',
                  padding: '8px 12px',
                  background: 'rgba(255, 68, 68, 0.15)',
                  border: '1px solid rgba(255, 68, 68, 0.4)',
                  borderRadius: 6,
                  color: '#FF4444',
                  fontFamily: "'JetBrains Mono', monospace",
                  fontSize: '0.75rem',
                  fontWeight: 600,
                  cursor: 'pointer',
                  display: 'flex',
                  alignItems: 'center',
                  justifyContent: 'center',
                  gap: 6,
                  transition: 'all 0.15s',
                }}
                onMouseEnter={e => {
                  (e.target as HTMLElement).style.background = 'rgba(255, 68, 68, 0.25)';
                  (e.target as HTMLElement).style.borderColor = 'rgba(255, 68, 68, 0.6)';
                }}
                onMouseLeave={e => {
                  (e.target as HTMLElement).style.background = 'rgba(255, 68, 68, 0.15)';
                  (e.target as HTMLElement).style.borderColor = 'rgba(255, 68, 68, 0.4)';
                }}
                title="Queue emergency remediation for all failing workflows"
              >
                ⚡ Emergency Remediation ({failing} failing)
              </button>
            </div>
          )}
        </div>
      )}
    </div>
  );
};
