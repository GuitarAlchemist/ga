// src/components/PrimeRadiant/IssuesPanel.tsx
// Cross-repo GitHub issues and open PRs triage panel for Prime Radiant.
// Data sourced from the central GitHubPollingManager (single polling loop).

import React, { useEffect, useMemo, useState } from 'react';
import { gitHubPollingManager, type GitHubDataType } from './GitHubPollingManager';
import { timeAgo } from './utils';

// ---------------------------------------------------------------------------
// Types
// ---------------------------------------------------------------------------
export type ItemKind = 'issue' | 'pr';
export type Staleness = 'fresh' | 'aging' | 'stale';

export interface GitHubItem {
  id: string;
  kind: ItemKind;
  number: number;
  title: string;
  repo: string;
  url: string;
  author: string;
  createdAt: string;
  updatedAt: string;
  labels: string[];
  draft: boolean;
  staleness: Staleness;
  ageDays: number;
}

interface RepoGroup {
  repo: string;
  items: GitHubItem[];
  issueCount: number;
  prCount: number;
  staleCount: number;
}

// ---------------------------------------------------------------------------
// Configuration
// ---------------------------------------------------------------------------
export const GITHUB_REPOS = ['ga', 'Demerzel', 'ix', 'tars'];
export const STALE_DAYS = 14;
export const AGING_DAYS = 7;

const REPO_COLOR: Record<string, string> = {
  Demerzel: '#FFD700',
  ga: '#FFB300',
  tars: '#4FC3F7',
  ix: '#73d13d',
};

const STALE_COLORS: Record<Staleness, string> = {
  fresh: '#33CC66',
  aging: '#FFB300',
  stale: '#FF4444',
};

const KIND_ICONS: Record<ItemKind, string> = {
  issue: '●',
  pr: '⛓',
};

// ---------------------------------------------------------------------------
// Pure transform functions (exported for testing)
// ---------------------------------------------------------------------------
export function classifyStaleness(ageDays: number): Staleness {
  if (ageDays >= STALE_DAYS) return 'stale';
  if (ageDays >= AGING_DAYS) return 'aging';
  return 'fresh';
}

export function transformGitHubItems(
  dataByRepo: Map<string, unknown[]>,
  kind: ItemKind,
): GitHubItem[] {
  const all: GitHubItem[] = [];
  const now = Date.now();
  for (const [repo, rawItems] of dataByRepo) {
    for (const raw of rawItems as Array<Record<string, unknown>>) {
      const number = (raw.number as number) ?? 0;
      const title = (raw.title as string) ?? 'Untitled';
      const url = (raw.html_url as string) ?? `https://github.com/GuitarAlchemist/${repo}/issues/${number}`;
      const user = raw.user as Record<string, unknown> | undefined;
      const author = (user?.login as string) ?? 'unknown';
      const createdAt = (raw.created_at as string) ?? new Date().toISOString();
      const updatedAt = (raw.updated_at as string) ?? createdAt;
      const rawLabels = (raw.labels as Array<Record<string, unknown>>) ?? [];
      const labels = rawLabels.map(l => (l.name as string) ?? '').filter(Boolean);
      const draft = !!raw.draft;
      const ageMs = now - new Date(createdAt).getTime();
      const ageDays = Math.max(0, Math.floor(ageMs / (1000 * 60 * 60 * 24)));
      all.push({
        id: `${repo}-${kind}-${number}`,
        kind,
        number,
        title,
        repo,
        url,
        author,
        createdAt,
        updatedAt,
        labels,
        draft,
        staleness: classifyStaleness(ageDays),
        ageDays,
      });
    }
  }
  return all;
}

export function groupByRepo(items: GitHubItem[]): RepoGroup[] {
  const map = new Map<string, GitHubItem[]>();
  for (const item of items) {
    const list = map.get(item.repo) ?? [];
    list.push(item);
    map.set(item.repo, list);
  }
  return GITHUB_REPOS.map((repo) => {
    const list = map.get(repo) ?? [];
    const sorted = [...list].sort((a, b) => b.ageDays - a.ageDays);
    return {
      repo,
      items: sorted,
      issueCount: sorted.filter(i => i.kind === 'issue').length,
      prCount: sorted.filter(i => i.kind === 'pr').length,
      staleCount: sorted.filter(i => i.staleness === 'stale').length,
    };
  }).filter(g => g.items.length > 0);
}

function fallbackItems(): GitHubItem[] {
  return [
    { id: 'fb-ga-issue-1', kind: 'issue', number: 180, title: 'Project Jarvis Phase 2 — Voice Integration', repo: 'ga', url: 'https://github.com/GuitarAlchemist/ga/issues/180', author: 'demo', createdAt: new Date(Date.now() - 86400000 * 12).toISOString(), updatedAt: new Date(Date.now() - 86400000 * 2).toISOString(), labels: ['enhancement'], draft: false, staleness: 'aging', ageDays: 12 },
    { id: 'fb-Demerzel-issue-1', kind: 'issue', number: 53, title: 'AI probes — autonomous codebase exploration', repo: 'Demerzel', url: 'https://github.com/GuitarAlchemist/Demerzel/issues/53', author: 'demo', createdAt: new Date(Date.now() - 86400000 * 18).toISOString(), updatedAt: new Date(Date.now() - 86400000 * 5).toISOString(), labels: ['research'], draft: false, staleness: 'stale', ageDays: 18 },
    { id: 'fb-ix-pr-1', kind: 'pr', number: 42, title: 'Memristive Markov state persistence', repo: 'ix', url: 'https://github.com/GuitarAlchemist/ix/pull/42', author: 'demo', createdAt: new Date(Date.now() - 86400000 * 4).toISOString(), updatedAt: new Date(Date.now() - 86400000).toISOString(), labels: ['draft'], draft: true, staleness: 'fresh', ageDays: 4 },
    { id: 'fb-tars-pr-1', kind: 'pr', number: 7, title: 'F# reasoning agent — belief propagation', repo: 'tars', url: 'https://github.com/GuitarAlchemist/tars/pull/7', author: 'demo', createdAt: new Date(Date.now() - 86400000 * 9).toISOString(), updatedAt: new Date(Date.now() - 86400000 * 2).toISOString(), labels: ['review'], draft: false, staleness: 'aging', ageDays: 9 },
  ];
}

// ---------------------------------------------------------------------------
// Panel status hook (reusable for icon-rail dot)
// ---------------------------------------------------------------------------
export interface IssuesHealth {
  total: number;
  staleCount: number;
  prCount: number;
  issueCount: number;
}

export function useIssuesHealth(): IssuesHealth {
  const [health, setHealth] = useState<IssuesHealth>({ total: 0, staleCount: 0, prCount: 0, issueCount: 0 });

  useEffect(() => {
    const unsubIssues = gitHubPollingManager.subscribe('issues' as GitHubDataType, GITHUB_REPOS, (data) => {
      const issues = transformGitHubItems(data, 'issue');
      const openPrs = transformGitHubItems(new Map(GITHUB_REPOS.map(r => [r, gitHubPollingManager.getLastData('pulls-open', r) ?? []])), 'pr');
      const all = [...issues, ...openPrs];
      setHealth({
        total: all.length,
        staleCount: all.filter(i => i.staleness === 'stale').length,
        prCount: all.filter(i => i.kind === 'pr').length,
        issueCount: all.filter(i => i.kind === 'issue').length,
      });
    });
    const unsubPrs = gitHubPollingManager.subscribe('pulls-open' as GitHubDataType, GITHUB_REPOS, (data) => {
      const prs = transformGitHubItems(data, 'pr');
      const issues = transformGitHubItems(new Map(GITHUB_REPOS.map(r => [r, gitHubPollingManager.getLastData('issues', r) ?? []])), 'issue');
      const all = [...issues, ...prs];
      setHealth({
        total: all.length,
        staleCount: all.filter(i => i.staleness === 'stale').length,
        prCount: all.filter(i => i.kind === 'pr').length,
        issueCount: all.filter(i => i.kind === 'issue').length,
      });
    });
    return () => { unsubIssues(); unsubPrs(); };
  }, []);

  return health;
}

// ---------------------------------------------------------------------------
// Component
// ---------------------------------------------------------------------------
export const IssuesPanel: React.FC = () => {
  const [items, setItems] = useState<GitHubItem[]>([]);
  const [collapsed, setCollapsed] = useState(false);
  const [filter, setFilter] = useState<'all' | 'issues' | 'prs'>('all');

  useEffect(() => {
    let issuesData: GitHubItem[] = [];
    let prsData: GitHubItem[] = [];
    let hasData = false;

    const update = () => {
      if (issuesData.length === 0 && prsData.length === 0 && !hasData) return;
      hasData = true;
      const merged = [...issuesData, ...prsData];
      setItems(merged.length > 0 ? merged : fallbackItems());
    };

    const unsubIssues = gitHubPollingManager.subscribe('issues' as GitHubDataType, GITHUB_REPOS, (data) => {
      issuesData = transformGitHubItems(data, 'issue');
      update();
    });
    const unsubPrs = gitHubPollingManager.subscribe('pulls-open' as GitHubDataType, GITHUB_REPOS, (data) => {
      prsData = transformGitHubItems(data, 'pr');
      update();
    });

    return () => { unsubIssues(); unsubPrs(); };
  }, []);

  const filtered = useMemo(() => {
    if (filter === 'issues') return items.filter(i => i.kind === 'issue');
    if (filter === 'prs') return items.filter(i => i.kind === 'pr');
    return items;
  }, [items, filter]);

  const groups = useMemo(() => groupByRepo(filtered), [filtered]);
  const totalIssues = items.filter(i => i.kind === 'issue').length;
  const totalPrs = items.filter(i => i.kind === 'pr').length;
  const totalStale = items.filter(i => i.staleness === 'stale').length;

  return (
    <div className="prime-radiant__activity" style={{ maxHeight: '70vh', overflowY: 'auto' }}>
      <div className="prime-radiant__activity-header" onClick={() => setCollapsed(!collapsed)}>
        <span className="prime-radiant__activity-title">
          Issues & PRs
          <span className="prime-radiant__activity-count">
            <span>{totalIssues + totalPrs} open</span>
            {totalPrs > 0 && <span style={{ color: '#58A6FF' }}> · {totalPrs} PR</span>}
            {totalStale > 0 && <span style={{ color: '#FF4444' }}> · {totalStale} stale</span>}
          </span>
        </span>
        <span className="prime-radiant__activity-toggle">{collapsed ? '\u25B6' : '\u25BC'}</span>
      </div>

      {!collapsed && (
        <div style={{ padding: '8px 0' }}>
          <div style={{ display: 'flex', gap: 8, padding: '0 12px 8px', borderBottom: '1px solid #30363d' }}>
            {(['all', 'issues', 'prs'] as const).map((f) => (
              <button
                key={f}
                onClick={() => setFilter(f)}
                style={{
                  padding: '2px 8px',
                  fontSize: '0.7rem',
                  background: filter === f ? '#30363d' : 'transparent',
                  color: filter === f ? '#e6edf3' : '#8b949e',
                  border: '1px solid #30363d',
                  borderRadius: 4,
                  cursor: 'pointer',
                  textTransform: 'capitalize',
                }}
              >
                {f}
              </button>
            ))}
          </div>

          {groups.length === 0 && (
            <div style={{ padding: '1rem', color: '#8b949e', fontSize: '0.75rem', textAlign: 'center' }}>
              No open issues or PRs match the current filter.
            </div>
          )}

          {groups.map((group) => (
            <div key={group.repo} style={{ marginBottom: 12 }}>
              <div style={{
                display: 'flex', alignItems: 'center', gap: 8, fontSize: '0.75rem', fontWeight: 600,
                color: REPO_COLOR[group.repo] ?? '#8b949e', textTransform: 'uppercase', letterSpacing: '0.05em',
                padding: '4px 12px', borderBottom: `1px solid ${REPO_COLOR[group.repo] ?? '#8b949e'}22`,
              }}>
                <span>{group.repo}</span>
                <span style={{ marginLeft: 'auto', fontSize: '0.65rem', fontWeight: 400, textTransform: 'none', color: '#6b7280' }}>
                  {group.issueCount} issue{group.issueCount !== 1 ? 's' : ''}
                  {group.prCount > 0 && <span style={{ color: '#58A6FF' }}> · {group.prCount} PR</span>}
                  {group.staleCount > 0 && <span style={{ color: '#FF4444' }}> · {group.staleCount} stale</span>}
                </span>
              </div>
              {group.items.map((item) => (
                <a
                  key={item.id}
                  href={item.url}
                  target="_blank"
                  rel="noopener noreferrer"
                  style={{
                    display: 'block', padding: '6px 12px', textDecoration: 'none', color: '#e6edf3',
                    borderBottom: '1px solid #21262d', transition: 'background 0.15s',
                  }}
                  onMouseEnter={(e) => { (e.currentTarget as HTMLAnchorElement).style.background = '#161b22'; }}
                  onMouseLeave={(e) => { (e.currentTarget as HTMLAnchorElement).style.background = 'transparent'; }}
                >
                  <div style={{ display: 'flex', alignItems: 'flex-start', gap: 8 }}>
                    <span style={{ color: item.kind === 'pr' ? '#58A6FF' : '#33CC66', fontSize: '0.65rem', lineHeight: 1.5, flexShrink: 0 }}>
                      {KIND_ICONS[item.kind]}
                    </span>
                    <div style={{ flex: 1, minWidth: 0 }}>
                      <div style={{ fontSize: '0.75rem', fontWeight: 500, lineHeight: 1.4, overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>
                        #{item.number} {item.title}
                        {item.draft && <span style={{ marginLeft: 6, fontSize: '0.6rem', color: '#6b7280', border: '1px solid #30363d', padding: '0 4px', borderRadius: 4 }}>draft</span>}
                      </div>
                      <div style={{ display: 'flex', alignItems: 'center', gap: 8, marginTop: 2, fontSize: '0.65rem', color: '#8b949e' }}>
                        <span style={{ color: STALE_COLORS[item.staleness] }}>● {item.ageDays}d</span>
                        <span>{item.author}</span>
                        <span>{timeAgo(item.updatedAt)}</span>
                        {item.labels.length > 0 && (
                          <span style={{ display: 'flex', gap: 4, overflow: 'hidden' }}>
                            {item.labels.slice(0, 3).map((label) => (
                              <span key={label} style={{ padding: '0 4px', borderRadius: 4, background: '#30363d', color: '#c9d1d9', fontSize: '0.6rem' }}>
                                {label}
                              </span>
                            ))}
                          </span>
                        )}
                      </div>
                    </div>
                  </div>
                </a>
              ))}
            </div>
          ))}
        </div>
      )}
    </div>
  );
};
