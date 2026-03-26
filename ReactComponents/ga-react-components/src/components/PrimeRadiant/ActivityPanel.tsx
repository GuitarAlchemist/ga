// src/components/PrimeRadiant/ActivityPanel.tsx
// Top-left accordion panel — Activities, Commits, and more

import React, { useEffect, useState } from 'react';

// ---------------------------------------------------------------------------
// Types
// ---------------------------------------------------------------------------
export interface Activity {
  id: string;
  name: string;
  status: 'active' | 'pending' | 'completed' | 'blocked';
  progress: number;
  eta?: string;
  category: 'governance' | 'research' | 'build' | 'test' | 'deploy';
}

interface CommitInfo {
  hash: string;
  message: string;
  repo: string;
  time: string;
  timestamp: number; // epoch ms for sorting
}

interface IssueInfo {
  number: number;
  title: string;
  repo: string;
  url: string;
}

// ---------------------------------------------------------------------------
// GitHub API configuration
// ---------------------------------------------------------------------------
const GITHUB_OWNER = 'GuitarAlchemist';
const GITHUB_REPOS = ['ga', 'Demerzel', 'hari', 'tars', 'ix', 'demerzel-bot', 'guitar-singularity'];
const GITHUB_API = 'https://api.github.com';

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

// ─── Fetch real commits from GitHub API ───
async function fetchCommits(): Promise<CommitInfo[]> {
  try {
    const allCommits: CommitInfo[] = [];
    for (const repo of GITHUB_REPOS) {
      const res = await fetch(`${GITHUB_API}/repos/${GITHUB_OWNER}/${repo}/commits?per_page=8`, {
        headers: { 'Accept': 'application/vnd.github.v3+json' },
      });
      if (!res.ok) continue;
      const data = await res.json();
      for (const commit of data) {
        const dateStr = commit.commit?.committer?.date ?? new Date().toISOString();
        allCommits.push({
          hash: commit.sha?.substring(0, 7) ?? '?',
          message: commit.commit?.message?.split('\n')[0] ?? '',
          repo,
          time: timeAgo(dateStr),
          timestamp: new Date(dateStr).getTime(),
        });
      }
    }
    return allCommits.sort((a, b) => b.timestamp - a.timestamp).slice(0, 15);
  } catch {
    return []; // silent fail — panel shows empty
  }
}

// ─── Fetch real issues from GitHub API ───
async function fetchIssues(): Promise<IssueInfo[]> {
  try {
    const allIssues: IssueInfo[] = [];
    for (const repo of GITHUB_REPOS) {
      const res = await fetch(`${GITHUB_API}/repos/${GITHUB_OWNER}/${repo}/issues?state=open&per_page=10&sort=updated`, {
        headers: { 'Accept': 'application/vnd.github.v3+json' },
      });
      if (!res.ok) continue;
      const data = await res.json();
      for (const issue of data) {
        if (issue.pull_request) continue; // skip PRs
        allIssues.push({
          number: issue.number,
          title: issue.title,
          repo,
          url: issue.html_url,
        });
      }
    }
    return allIssues.slice(0, 10);
  } catch {
    return [];
  }
}

// ─── Derive activities from GitHub milestones or plan files ───
async function fetchActivities(): Promise<Activity[]> {
  try {
    // Try fetching from backend activity endpoint
    const res = await fetch('/api/activities');
    if (res.ok) {
      return await res.json();
    }
  } catch { /* fall through */ }

  // Fallback: derive from open issues labels
  try {
    const res = await fetch(`${GITHUB_API}/repos/${GITHUB_OWNER}/ga/issues?state=open&per_page=10&labels=active`, {
      headers: { 'Accept': 'application/vnd.github.v3+json' },
    });
    if (res.ok) {
      const data = await res.json();
      return data.map((issue: { number: number; title: string; labels: { name: string }[] }, i: number) => ({
        id: String(issue.number),
        name: issue.title,
        status: 'active' as const,
        progress: 50,
        category: (issue.labels?.find((l: { name: string }) => ['governance', 'research', 'build', 'test', 'deploy'].includes(l.name))?.name ?? 'build') as Activity['category'],
      }));
    }
  } catch { /* fall through */ }

  return []; // empty — no mock fallback
}

// ---------------------------------------------------------------------------
// Constants
// ---------------------------------------------------------------------------
const STATUS_COLOR: Record<Activity['status'], string> = {
  active: '#33CC66',
  pending: '#888888',
  completed: '#33CC66',
  blocked: '#FF4444',
};

const CATEGORY_ICON: Record<Activity['category'], string> = {
  governance: 'G',
  research: 'R',
  build: 'B',
  test: 'T',
  deploy: 'D',
};

const REPO_COLOR: Record<string, string> = {
  ga: '#FFB300',
  Demerzel: '#FFD700',
  hari: '#c4b5fd',
  tars: '#4FC3F7',
  ix: '#73d13d',
  'demerzel-bot': '#ff85c0',
  'guitar-singularity': '#ff7a45',
};

// ---------------------------------------------------------------------------
// Accordion Section
// ---------------------------------------------------------------------------
const AccordionSection: React.FC<{
  title: string;
  badge?: string;
  defaultOpen?: boolean;
  children: React.ReactNode;
}> = ({ title, badge, defaultOpen = false, children }) => {
  const [open, setOpen] = useState(defaultOpen);

  return (
    <div className="prime-radiant__accordion-section">
      <div
        className="prime-radiant__accordion-header"
        onClick={() => setOpen(!open)}
      >
        <span className="prime-radiant__accordion-title">
          {title}
          {badge && <span className="prime-radiant__accordion-badge">{badge}</span>}
        </span>
        <span className="prime-radiant__accordion-arrow">{open ? '▼' : '▶'}</span>
      </div>
      {open && <div className="prime-radiant__accordion-body">{children}</div>}
    </div>
  );
};

// ---------------------------------------------------------------------------
// Component
// ---------------------------------------------------------------------------
export const ActivityPanel: React.FC = () => {
  const [activities, setActivities] = useState<Activity[]>([]);
  const [commits, setCommits] = useState<CommitInfo[]>([]);
  const [issues, setIssues] = useState<IssueInfo[]>([]);
  const [panelCollapsed, setPanelCollapsed] = useState(false);

  useEffect(() => {
    // Fetch real data from GitHub API
    fetchActivities().then(setActivities);
    fetchCommits().then(setCommits);
    fetchIssues().then(setIssues);

    // Refresh every 60 seconds
    const interval = setInterval(() => {
      fetchCommits().then(setCommits);
      fetchIssues().then(setIssues);
    }, 60000);
    return () => clearInterval(interval);
  }, []);

  const active = activities.filter((a) => a.status === 'active');
  const pending = activities.filter((a) => a.status === 'pending' || a.status === 'blocked');

  return (
    <div className="prime-radiant__activity">
      <div
        className="prime-radiant__activity-header"
        onClick={() => setPanelCollapsed(!panelCollapsed)}
      >
        <span className="prime-radiant__activity-title">
          Command
          <span className="prime-radiant__activity-count">
            {active.length} active · {commits.length} commits
          </span>
        </span>
        <span className="prime-radiant__activity-toggle">
          {panelCollapsed ? '▶' : '▼'}
        </span>
      </div>

      {!panelCollapsed && (
        <>
          {/* Activities accordion */}
          <AccordionSection
            title="Activities"
            badge={`${active.length}`}
            defaultOpen={true}
          >
            {activities.map((a) => (
              <div key={a.id} className="prime-radiant__activity-item">
                <span
                  className={`prime-radiant__activity-cat${a.status === 'active' ? ' prime-radiant__activity-cat--active' : ''}`}
                  style={{ color: STATUS_COLOR[a.status] }}
                >
                  {CATEGORY_ICON[a.category]}
                </span>
                <div className="prime-radiant__activity-info">
                  <div className="prime-radiant__activity-name" style={{
                    color: a.status === 'completed' ? '#6b7280' : '#c9d1d9',
                    textDecoration: a.status === 'completed' ? 'line-through' : 'none',
                  }}>
                    {a.name}
                  </div>
                  {a.status !== 'completed' && (
                    <div className="prime-radiant__activity-bar">
                      <div
                        className="prime-radiant__activity-fill"
                        style={{
                          width: `${a.progress}%`,
                          backgroundColor: STATUS_COLOR[a.status],
                        }}
                      />
                    </div>
                  )}
                </div>
                <span className="prime-radiant__activity-eta" style={{
                  color: a.status === 'blocked' ? '#FF4444' : '#6b7280',
                }}>
                  {a.eta}
                </span>
              </div>
            ))}
          </AccordionSection>

          {/* Commits accordion */}
          <AccordionSection
            title="Commits"
            badge={`${commits.length}`}
            defaultOpen={false}
          >
            {commits.map((c) => (
              <div key={c.hash} className="prime-radiant__commit-item">
                <span className="prime-radiant__commit-hash" style={{
                  color: REPO_COLOR[c.repo] ?? '#8b949e',
                }}>
                  {c.hash.slice(0, 7)}
                </span>
                <span className="prime-radiant__commit-msg">{c.message}</span>
                <span className="prime-radiant__commit-time">{c.time}</span>
              </div>
            ))}
          </AccordionSection>

          {/* Issues accordion */}
          <AccordionSection
            title="Issues"
            badge={`${issues.length}`}
            defaultOpen={false}
          >
            {issues.map((issue) => (
              <div
                key={issue.number}
                className="prime-radiant__commit-item"
                style={{ cursor: 'pointer' }}
                onClick={() => window.open(issue.url, '_blank')}
              >
                <span className="prime-radiant__commit-hash" style={{
                  color: '#33CC66',
                }}>
                  #{issue.number}
                </span>
                <span className="prime-radiant__commit-msg">{issue.title}</span>
              </div>
            ))}
          </AccordionSection>
        </>
      )}
    </div>
  );
};
