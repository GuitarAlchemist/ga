// src/components/PrimeRadiant/ActivityPanel.tsx
// Top-left accordion panel — Activities, Commits, and more
// Data sourced from central GitHubPollingManager (single polling loop)

import React, { useEffect, useState } from 'react';
import { timeAgo } from './utils';
import { gitHubPollingManager } from './GitHubPollingManager';

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

interface TeamInfo {
  id: string;
  name: string;
  status: 'running' | 'completed' | 'pending';
  task: string;
  duration: string;
  members: number;
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
// GitHub configuration (repos list — fetching delegated to polling manager)
// ---------------------------------------------------------------------------
const GITHUB_OWNER = 'GuitarAlchemist';
const GITHUB_REPOS = ['ga', 'Demerzel', 'hari', 'tars', 'ix', 'demerzel-bot', 'guitar-singularity'];

// ---------------------------------------------------------------------------
// Fallback data (used when API is unavailable / all repos return empty)
// ---------------------------------------------------------------------------
const FALLBACK_COMMITS: CommitInfo[] = [
  { hash: 'abc1234', message: 'feat: algedonic research + belief monitor pipeline', repo: 'Demerzel', time: 'just now', timestamp: Date.now() },
  { hash: 'def5678', message: 'feat: demo improvement — routing, fallbacks, categories', repo: 'ga', time: '1h ago', timestamp: Date.now() - 3600000 },
  { hash: 'ghi9012', message: 'fix: update System.Text.Json to patched versions', repo: 'tars', time: '2h ago', timestamp: Date.now() - 7200000 },
  { hash: 'jkl3456', message: 'feat: hexavalent logic — 6-valued system (T/P/U/D/F/C)', repo: 'Demerzel', time: '1d ago', timestamp: Date.now() - 86400000 },
  { hash: 'mno7890', message: 'feat: Governance module — memristive Markov', repo: 'ix', time: '2d ago', timestamp: Date.now() - 172800000 },
];

const FALLBACK_ISSUES: IssueInfo[] = [
  { number: 180, title: 'Project Jarvis Phase 2 — Voice Integration', repo: 'ga', url: 'https://github.com/GuitarAlchemist/ga/issues/180' },
  { number: 53, title: 'AI probes — autonomous codebase exploration', repo: 'Demerzel', url: 'https://github.com/GuitarAlchemist/Demerzel/issues/53' },
  { number: 12, title: 'Memristive Markov state persistence', repo: 'ix', url: 'https://github.com/GuitarAlchemist/ix/issues/12' },
  { number: 8, title: 'Seldon Plan — long-horizon prediction engine', repo: 'hari', url: 'https://github.com/GuitarAlchemist/hari/issues/8' },
  { number: 42, title: 'F# reasoning agent — belief propagation', repo: 'tars', url: 'https://github.com/GuitarAlchemist/tars/issues/42' },
];

const FALLBACK_ACTIVITIES: Activity[] = [
  { id: 'fb-1', name: '[Demerzel] Governance audit cycle 004', status: 'active', progress: 60, eta: '2d', category: 'governance' },
  { id: 'fb-2', name: 'PR #42 [ga] Prime Radiant command center', status: 'active', progress: 75, category: 'build' },
  { id: 'fb-3', name: '[hari] AGI research — Lie algebra architecture', status: 'pending', progress: 20, category: 'research' },
  { id: 'fb-4', name: '[tars] Belief propagation engine', status: 'active', progress: 45, category: 'build' },
  { id: 'fb-5', name: '[ix] Memristive Markov — 34 tests passing', status: 'completed', progress: 100, eta: 'merged 1d ago', category: 'build' },
  { id: 'fb-6', name: '[Demerzel] Red team cycle 003 — defense scoring', status: 'completed', progress: 100, eta: 'merged 2d ago', category: 'test' },
];

// ─── Category inference from repo name ───
function categoryForRepo(repo: string): Activity['category'] {
  switch (repo) {
    case 'Demerzel':
    case 'demerzel-bot':
      return 'governance';
    case 'hari':
      return 'research';
    default:
      return 'build';
  }
}

// ---------------------------------------------------------------------------
// Transform raw API data into typed domain objects
// ---------------------------------------------------------------------------
function transformCommits(dataByRepo: Map<string, unknown[]>): CommitInfo[] {
  const allCommits: CommitInfo[] = [];
  for (const [repo, commits] of dataByRepo) {
    for (const commit of commits as Array<Record<string, unknown>>) {
      const commitObj = commit.commit as Record<string, unknown> | undefined;
      const committer = commitObj?.committer as Record<string, unknown> | undefined;
      const dateStr = (committer?.date as string) ?? new Date().toISOString();
      const message = commitObj?.message as string | undefined;
      allCommits.push({
        hash: (commit.sha as string) ?? '?',
        message: message?.split('\n')[0] ?? '',
        repo,
        time: timeAgo(dateStr),
        timestamp: new Date(dateStr).getTime(),
      });
    }
  }
  allCommits.sort((a, b) => b.timestamp - a.timestamp);
  return allCommits.length > 0 ? allCommits.slice(0, 15) : FALLBACK_COMMITS;
}

function transformIssues(dataByRepo: Map<string, unknown[]>): IssueInfo[] {
  const allIssues: IssueInfo[] = [];
  for (const [repo, issues] of dataByRepo) {
    for (const issue of issues as Array<Record<string, unknown>>) {
      if (issue.pull_request) continue; // skip PRs
      allIssues.push({
        number: issue.number as number,
        title: issue.title as string,
        repo,
        url: issue.html_url as string,
      });
    }
  }
  return allIssues.length > 0 ? allIssues.slice(0, 10) : FALLBACK_ISSUES;
}

function transformMilestones(dataByRepo: Map<string, unknown[]>): Activity[] {
  const activities: Activity[] = [];
  for (const [repo, milestones] of dataByRepo) {
    for (const ms of milestones as Array<Record<string, unknown>>) {
      const openIssues = ms.open_issues as number;
      const closedIssues = ms.closed_issues as number;
      const total = openIssues + closedIssues;
      const progress = total > 0 ? Math.round((closedIssues / total) * 100) : 0;
      const now = Date.now();
      const dueOn = ms.due_on as string | null;
      const dueSoon = dueOn && new Date(dueOn).getTime() - now < 7 * 24 * 3600 * 1000;
      const overdue = dueOn && new Date(dueOn).getTime() < now;
      const status: Activity['status'] = overdue ? 'blocked' : dueSoon ? 'active' : 'pending';
      activities.push({
        id: `ms-${repo}-${ms.id}`,
        name: `[${repo}] ${ms.title as string}`,
        status,
        progress,
        eta: dueOn ? new Date(dueOn).toLocaleDateString() : undefined,
        category: categoryForRepo(repo),
      });
    }
  }
  return activities;
}

function transformOpenPRs(dataByRepo: Map<string, unknown[]>): Activity[] {
  const activities: Activity[] = [];
  for (const [repo, prs] of dataByRepo) {
    for (const pr of prs as Array<Record<string, unknown>>) {
      const draft = pr.draft as boolean;
      const reviewers = pr.requested_reviewers as unknown[] | undefined;
      const progress = draft ? 25 : (reviewers && reviewers.length > 0 ? 50 : 75);
      activities.push({
        id: `pr-${repo}-${pr.number}`,
        name: `PR #${pr.number} [${repo}] ${pr.title as string}`,
        status: 'active',
        progress,
        eta: timeAgo(pr.created_at as string),
        category: categoryForRepo(repo),
      });
    }
  }
  return activities;
}

function transformClosedPRs(dataByRepo: Map<string, unknown[]>): Activity[] {
  const activities: Activity[] = [];
  const cutoff = Date.now() - 7 * 24 * 3600 * 1000;
  for (const [repo, prs] of dataByRepo) {
    for (const pr of prs as Array<Record<string, unknown>>) {
      const mergedAt = pr.merged_at as string | null;
      const closedAt = pr.closed_at as string | null;
      const closedTime = mergedAt ?? closedAt;
      if (!closedTime || new Date(closedTime).getTime() <= cutoff) continue;
      activities.push({
        id: `pr-closed-${repo}-${pr.number}`,
        name: `PR #${pr.number} [${repo}] ${pr.title as string}`,
        status: 'completed',
        progress: 100,
        eta: mergedAt ? `merged ${timeAgo(mergedAt)}` : `closed ${timeAgo(closedAt!)}`,
        category: categoryForRepo(repo),
      });
    }
  }
  return activities;
}

// ---------------------------------------------------------------------------
// Constants
// ---------------------------------------------------------------------------
const FALLBACK_TEAMS: TeamInfo[] = [
  { id: '1', name: 'layer1-fanout', status: 'completed', task: 'Multi-Model Fan-Out endpoint', duration: '4m 8s', members: 1 },
  { id: '2', name: 'layer2-tribunal', status: 'completed', task: 'Theory Tribunal panel', duration: '5m 26s', members: 1 },
  { id: '3', name: 'voice-wire', status: 'running', task: 'Wire Demerzel voice to ChatWidget', duration: '2m 15s', members: 1 },
];

const TEAM_STATUS_COLOR: Record<TeamInfo['status'], string> = {
  running: '#58A6FF',
  completed: '#33CC66',
  pending: '#6b7280',
};

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
  const [rateLimited, setRateLimited] = useState(false);

  // Subscribe to central polling manager for all data types
  useEffect(() => {
    // Mutable accumulators for activities (composed from 3 data types)
    let latestMilestones: Activity[] = [];
    let latestOpenPRs: Activity[] = [];
    let latestClosedPRs: Activity[] = [];

    function mergeActivities(): void {
      const merged = [...latestOpenPRs, ...latestMilestones, ...latestClosedPRs];
      setActivities(merged.length > 0 ? merged : FALLBACK_ACTIVITIES);
      setRateLimited(gitHubPollingManager.isRateLimited());
    }

    const unsubs = [
      gitHubPollingManager.subscribe('milestones', GITHUB_REPOS, (data) => {
        latestMilestones = transformMilestones(data);
        mergeActivities();
      }),
      gitHubPollingManager.subscribe('pulls-open', GITHUB_REPOS, (data) => {
        latestOpenPRs = transformOpenPRs(data);
        mergeActivities();
      }),
      gitHubPollingManager.subscribe('pulls-closed', GITHUB_REPOS, (data) => {
        latestClosedPRs = transformClosedPRs(data);
        mergeActivities();
      }),
      gitHubPollingManager.subscribe('commits', GITHUB_REPOS, (data) => {
        setCommits(transformCommits(data));
        setRateLimited(gitHubPollingManager.isRateLimited());
      }),
      gitHubPollingManager.subscribe('issues', GITHUB_REPOS, (data) => {
        setIssues(transformIssues(data));
        setRateLimited(gitHubPollingManager.isRateLimited());
      }),
    ];

    return () => unsubs.forEach((unsub) => unsub());
  }, []);

  const active = activities.filter((a) => a.status === 'active');
  const _pending = activities.filter((a) => a.status === 'pending' || a.status === 'blocked');

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
          {rateLimited && (
            <span style={{ color: '#FF4444', fontSize: '0.75em', marginLeft: 8 }}>
              Rate limited — refreshing every 5m
            </span>
          )}
        </span>
        <span className="prime-radiant__activity-toggle">
          {panelCollapsed ? '▶' : '▼'}
        </span>
      </div>

      {!panelCollapsed && (
        <>
          {/* Active Teams accordion */}
          <AccordionSection
            title="Active Teams"
            badge={`${FALLBACK_TEAMS.length}`}
            defaultOpen={true}
          >
            {FALLBACK_TEAMS.map((team) => (
              <div
                key={team.id}
                className="activity-panel__team-card"
                style={{ opacity: team.status === 'completed' ? 0.6 : 1 }}
              >
                <span
                  className="activity-panel__team-dot"
                  style={{ backgroundColor: TEAM_STATUS_COLOR[team.status] }}
                />
                <div className="activity-panel__team-info">
                  <div className="activity-panel__team-name">{team.name}</div>
                  <div className="activity-panel__team-task">{team.task}</div>
                </div>
                <span className="activity-panel__team-duration">{team.duration}</span>
              </div>
            ))}
          </AccordionSection>

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
              <div
                key={c.hash}
                className="prime-radiant__commit-item"
                title={`${c.repo}/${c.hash} — ${c.message}`}
                style={{ cursor: 'pointer' }}
                onClick={() => window.open(`https://github.com/${GITHUB_OWNER}/${c.repo}/commit/${c.hash}`, '_blank')}
              >
                <span className="prime-radiant__repo-badge" style={{
                  color: REPO_COLOR[c.repo] ?? '#8b949e',
                  borderColor: `${REPO_COLOR[c.repo] ?? '#8b949e'}44`,
                }}>
                  {c.repo}
                </span>
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
                key={`${issue.repo}-${issue.number}`}
                className="prime-radiant__commit-item"
                style={{ cursor: 'pointer' }}
                onClick={() => window.open(issue.url, '_blank')}
                title={`${issue.repo}#${issue.number} — ${issue.title}`}
              >
                <span className="prime-radiant__repo-badge" style={{
                  color: REPO_COLOR[issue.repo] ?? '#8b949e',
                  borderColor: `${REPO_COLOR[issue.repo] ?? '#8b949e'}44`,
                }}>
                  {issue.repo}
                </span>
                <span className="prime-radiant__commit-hash" style={{
                  color: REPO_COLOR[issue.repo] ?? '#33CC66',
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
