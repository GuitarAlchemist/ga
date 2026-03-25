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
}

interface IssueInfo {
  number: number;
  title: string;
  repo: string;
  url: string;
}

// ---------------------------------------------------------------------------
// Mock data — TODO: connect to real sources (GitHub API, git log)
// ---------------------------------------------------------------------------
function getMockActivities(): Activity[] {
  return [
    { id: '1', name: 'Hexavalent logic schemas', status: 'completed', progress: 100, eta: 'done', category: 'governance' },
    { id: '2', name: 'Prime Radiant v4', status: 'active', progress: 78, eta: '~30m', category: 'build' },
    { id: '3', name: 'ix GPU lattice ops', status: 'pending', progress: 0, eta: '~2h', category: 'build' },
    { id: '4', name: 'Red team cycle 004', status: 'pending', progress: 0, eta: '~1h', category: 'test' },
    { id: '5', name: 'Seldon research — 6-valued logic', status: 'active', progress: 45, eta: '~45m', category: 'research' },
    { id: '6', name: 'DNS propagation', status: 'blocked', progress: 0, eta: 'waiting', category: 'deploy' },
  ];
}

function getMockIssues(): IssueInfo[] {
  return [
    { number: 23, title: 'Multi-model design loop — Claude + GPT + Codex', repo: 'ga', url: 'https://github.com/GuitarAlchemist/ga/issues/23' },
    { number: 22, title: 'Procedural solar system — all planets', repo: 'ga', url: 'https://github.com/GuitarAlchemist/ga/issues/22' },
    { number: 21, title: 'Bottom drawer — icicle navigator + file viewer', repo: 'ga', url: 'https://github.com/GuitarAlchemist/ga/issues/21' },
    { number: 20, title: 'Breadcrumb file navigator with icons', repo: 'ga', url: 'https://github.com/GuitarAlchemist/ga/issues/20' },
    { number: 19, title: 'Blender GLTF head for Demerzel', repo: 'ga', url: 'https://github.com/GuitarAlchemist/ga/issues/19' },
    { number: 18, title: 'Clickable activities + accordion sections', repo: 'ga', url: 'https://github.com/GuitarAlchemist/ga/issues/18' },
  ];
}

function getMockCommits(): CommitInfo[] {
  return [
    { hash: '642e8cd', message: 'LLM panel expanded, denser padding', repo: 'ga', time: '2m ago' },
    { hash: '754e5cb', message: 'Gentle spin on active activity icons', repo: 'ga', time: '5m ago' },
    { hash: '10df84e', message: 'Activity Panel — progress bars + ETAs', repo: 'ga', time: '12m ago' },
    { hash: '863b94b', message: 'LLM Status panel — providers, tokens', repo: 'ga', time: '15m ago' },
    { hash: 'c4b81fe', message: 'Responsive — phone, tablet, touch', repo: 'ga', time: '20m ago' },
    { hash: '250b8b7', message: 'cloudflare-tunnel skill', repo: 'Demerzel', time: '45m ago' },
    { hash: 'd8e6705', message: 'Hexavalent logic (T/P/U/D/F/C)', repo: 'Demerzel', time: '1h ago' },
    { hash: '6d8ffe7', message: 'Prime Radiant v3 — health colors', repo: 'ga', time: '2h ago' },
  ];
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
  ix: '#4FC3F7',
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
    setActivities(getMockActivities());
    setCommits(getMockCommits());
    setIssues(getMockIssues());
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
