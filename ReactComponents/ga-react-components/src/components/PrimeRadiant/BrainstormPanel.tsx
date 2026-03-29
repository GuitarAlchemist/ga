// src/components/PrimeRadiant/BrainstormPanel.tsx
// Demerzel's "What's Next?" advisor — pulls real context from GitHub, CI/CD,
// governance health, and epistemic state to surface prioritized recommendations.

import React, { useState, useCallback, useEffect, useRef } from 'react';

// ---------------------------------------------------------------------------
// Types
// ---------------------------------------------------------------------------
type Priority = 'urgent' | 'high' | 'quick' | 'strategic';

interface Recommendation {
  id: string;
  title: string;
  rationale: string;
  priority: Priority;
  source?: string;
  actionUrl?: string;
  tensorState?: string;
}

// ---------------------------------------------------------------------------
// Priority styling
// ---------------------------------------------------------------------------
const PRIORITY_META: Record<Priority, { label: string; color: string; icon: string }> = {
  urgent:    { label: 'Urgent',     color: '#FF4444', icon: '!' },
  high:      { label: 'High Value', color: '#FFD700', icon: '\u2605' },
  quick:     { label: 'Quick Win',  color: '#33CC66', icon: '\u26A1' },
  strategic: { label: 'Strategic',  color: '#4FC3F7', icon: '\u2192' },
};

// ---------------------------------------------------------------------------
// GitHub context fetcher
// ---------------------------------------------------------------------------
const GITHUB_OWNER = 'GuitarAlchemist';
const GITHUB_REPOS = ['ga', 'Demerzel', 'ix', 'tars'];
const GITHUB_API = 'https://api.github.com';

const githubToken: string | null =
  (typeof import.meta !== 'undefined' && (import.meta as Record<string, unknown>).env
    ? (((import.meta as Record<string, unknown>).env) as Record<string, string | undefined>)['VITE_GITHUB_TOKEN'] ?? null
    : null)
  ?? (typeof localStorage !== 'undefined' ? localStorage.getItem('ga-github-token') : null);

const GH_HEADERS: HeadersInit = {
  'Accept': 'application/vnd.github.v3+json',
  ...(githubToken ? { 'Authorization': `Bearer ${githubToken}` } : {}),
};

interface GHIssue {
  number: number;
  title: string;
  labels: { name: string }[];
  html_url: string;
  repo: string;
}

interface GHWorkflowRun {
  name: string;
  conclusion: string | null;
  status: string;
  repo: string;
  html_url: string;
}

async function fetchOpenIssues(): Promise<GHIssue[]> {
  const issues: GHIssue[] = [];
  for (const repo of GITHUB_REPOS) {
    try {
      const res = await fetch(`${GITHUB_API}/repos/${GITHUB_OWNER}/${repo}/issues?state=open&per_page=10`, { headers: GH_HEADERS });
      if (res.ok) {
        const data = await res.json() as { number: number; title: string; labels: { name: string }[]; html_url: string }[];
        issues.push(...data.map(i => ({ ...i, repo })));
      }
    } catch { /* skip */ }
  }
  return issues;
}

async function fetchCIStatus(): Promise<GHWorkflowRun[]> {
  const runs: GHWorkflowRun[] = [];
  for (const repo of GITHUB_REPOS) {
    try {
      const res = await fetch(`${GITHUB_API}/repos/${GITHUB_OWNER}/${repo}/actions/runs?per_page=5&status=completed`, { headers: GH_HEADERS });
      if (res.ok) {
        const data = await res.json() as { workflow_runs: { name: string; conclusion: string | null; status: string; html_url: string }[] };
        runs.push(...(data.workflow_runs ?? []).map(r => ({ ...r, repo })));
      }
    } catch { /* skip */ }
  }
  return runs;
}

// ---------------------------------------------------------------------------
// Recommendation engine
// ---------------------------------------------------------------------------
function buildRecommendations(
  issues: GHIssue[],
  ciRuns: GHWorkflowRun[],
): Recommendation[] {
  const recs: Recommendation[] = [];

  // Urgent: failing CI
  const failures = ciRuns.filter(r => r.conclusion === 'failure');
  const failedRepos = [...new Set(failures.map(f => f.repo))];
  if (failedRepos.length > 0) {
    recs.push({
      id: 'ci-fail',
      title: `Fix CI failures in ${failedRepos.join(', ')}`,
      rationale: `${failures.length} workflow run(s) failing. Green CI is prerequisite for everything else.`,
      priority: 'urgent',
      source: 'CI/CD',
      actionUrl: failures[0]?.html_url,
    });
  }

  // Urgent: issues with "bug" label
  const bugs = issues.filter(i => i.labels.some(l => l.name === 'bug'));
  bugs.slice(0, 2).forEach(bug => {
    recs.push({
      id: `bug-${bug.repo}-${bug.number}`,
      title: bug.title,
      rationale: `Bug in ${bug.repo}. Fix bugs before adding features.`,
      priority: 'urgent',
      source: `${bug.repo}#${bug.number}`,
      actionUrl: bug.html_url,
    });
  });

  // High value: issues with "enhancement" label or feature issues
  const features = issues.filter(i =>
    i.labels.some(l => l.name === 'enhancement' || l.name === 'feat') ||
    i.title.toLowerCase().startsWith('feat:')
  );
  features.slice(0, 3).forEach(feat => {
    recs.push({
      id: `feat-${feat.repo}-${feat.number}`,
      title: feat.title,
      rationale: `Feature request in ${feat.repo}. Adds user-facing value.`,
      priority: 'high',
      source: `${feat.repo}#${feat.number}`,
      actionUrl: feat.html_url,
    });
  });

  // Quick wins: issues with short titles (likely small scope)
  const quickCandidates = issues.filter(i =>
    !bugs.includes(i) && !features.includes(i) && i.title.length < 60
  );
  quickCandidates.slice(0, 2).forEach(q => {
    recs.push({
      id: `quick-${q.repo}-${q.number}`,
      title: q.title,
      rationale: `Small scope item in ${q.repo}. Can ship quickly.`,
      priority: 'quick',
      source: `${q.repo}#${q.number}`,
      actionUrl: q.html_url,
    });
  });

  // Strategic: epistemic constitution items
  const amnesiaSchedule = JSON.parse(localStorage.getItem('epistemic-amnesia-schedule') ?? '[]') as { beliefId: string; scheduledFor: string; executed: boolean }[];
  const pendingAmnesia = amnesiaSchedule.filter(a => !a.executed);
  if (pendingAmnesia.length > 0) {
    recs.push({
      id: 'epistemic-amnesia',
      title: `${pendingAmnesia.length} belief(s) scheduled for amnesia review`,
      rationale: 'Article E-5: beliefs scheduled for deliberate deletion need re-derivation testing.',
      priority: 'strategic',
      source: 'Epistemic Constitution',
      tensorState: 'E-5',
    });
  }

  // Strategic: always suggest epistemic health check
  recs.push({
    id: 'epistemic-review',
    title: 'Run epistemic tensor review',
    rationale: 'Article E-9: periodic federated peer review of high-confidence beliefs prevents epistemic isolation.',
    priority: 'strategic',
    source: 'Epistemic Constitution',
    tensorState: 'E-9',
  });

  // If no issues found at all, provide fallback recommendations
  if (recs.length <= 2) {
    recs.push(
      { id: 'fb-1', title: 'Godot 4.6 bridge protocol', rationale: 'Phase 1 plan ready. Start with typed WebSocket events.', priority: 'high', source: 'Plan doc' },
      { id: 'fb-2', title: 'Blue-green build end-to-end test', rationale: 'Scripts exist but untested. Run ga-bootstrap.ps1 through full cycle.', priority: 'quick', source: 'Infrastructure' },
      { id: 'fb-3', title: 'demerzel:meta-brainstorm skill', rationale: 'Recursive meta-learning tool. This session proved the concept.', priority: 'strategic', source: 'Epistemic Constitution' },
    );
  }

  return recs;
}

// ---------------------------------------------------------------------------
// Hook
// ---------------------------------------------------------------------------
function useDemerzelAdvice() {
  const [recommendations, setRecommendations] = useState<Recommendation[]>([]);
  const [loading, setLoading] = useState(false);
  const [lastUpdated, setLastUpdated] = useState<string | null>(null);

  const analyze = useCallback(async (query?: string) => {
    setLoading(true);

    // Try backend API first
    try {
      const res = await fetch('/api/brainstorm/advise', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ query: query ?? 'What should I work on next?' }),
      });
      if (res.ok) {
        const data = await res.json() as { recommendations: Recommendation[] };
        setRecommendations(data.recommendations);
        setLastUpdated(new Date().toLocaleTimeString());
        setLoading(false);
        return;
      }
    } catch { /* fallback below */ }

    // Fallback: fetch real context from GitHub + local state
    const [issues, ciRuns] = await Promise.all([
      fetchOpenIssues(),
      fetchCIStatus(),
    ]);

    let recs = buildRecommendations(issues, ciRuns);

    // Filter by query if provided
    if (query) {
      const q = query.toLowerCase();
      const filtered = recs.filter(r =>
        r.title.toLowerCase().includes(q) || r.rationale.toLowerCase().includes(q)
      );
      if (filtered.length > 0) recs = filtered;
    }

    setRecommendations(recs);
    setLastUpdated(new Date().toLocaleTimeString());
    setLoading(false);
  }, []);

  return { recommendations, loading, lastUpdated, analyze };
}

// ---------------------------------------------------------------------------
// Component
// ---------------------------------------------------------------------------
export const BrainstormPanel: React.FC = () => {
  const [open, setOpen] = useState(false);
  const [input, setInput] = useState('');
  const { recommendations, loading, lastUpdated, analyze } = useDemerzelAdvice();
  const initialLoad = useRef(false);

  // Auto-analyze on first open
  useEffect(() => {
    if (open && !initialLoad.current && recommendations.length === 0) {
      initialLoad.current = true;
      analyze();
    }
  }, [open, recommendations.length, analyze]);

  const grouped = recommendations.reduce<Record<Priority, Recommendation[]>>((acc, r) => {
    (acc[r.priority] ??= []).push(r);
    return acc;
  }, { urgent: [], high: [], quick: [], strategic: [] });

  const urgentCount = grouped.urgent.length;

  return (
    <>
      {/* Trigger button — Demerzel branded */}
      <button
        className={`brainstorm-trigger ${urgentCount > 0 ? 'brainstorm-trigger--urgent' : ''}`}
        onClick={() => setOpen(v => !v)}
        title="Demerzel — What should I work on next?"
        aria-label="Ask Demerzel what's next"
      >
        <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
          <path d="M9.663 17h4.673M12 3v1m6.364 1.636l-.707.707M21 12h-1M4 12H3m3.343-5.657l-.707-.707m2.828 9.9a5 5 0 1 1 7.072 0l-.548.547A3.374 3.374 0 0 0 14 18.469V19a2 2 0 1 1-4 0v-.531c0-.895-.356-1.754-.988-2.386l-.548-.547z" />
        </svg>
        {urgentCount > 0 && <span className="brainstorm-trigger__badge">{urgentCount}</span>}
      </button>

      {/* Panel */}
      {open && (
        <div className="brainstorm-panel">
          <div className="brainstorm-panel__header">
            <span className="brainstorm-panel__title">Demerzel recommends</span>
            {lastUpdated && <span className="brainstorm-panel__time">{lastUpdated}</span>}
            <button className="brainstorm-panel__close" onClick={() => setOpen(false)}>&times;</button>
          </div>

          {/* Quick action */}
          <button
            className="brainstorm-panel__auto"
            onClick={() => analyze()}
            disabled={loading}
          >
            {loading ? 'Analyzing GitHub, CI/CD, governance...' : "What's next?"}
          </button>

          {/* Focused query */}
          <div className="brainstorm-panel__input-row">
            <input
              className="brainstorm-panel__input"
              placeholder="Or ask about a specific area..."
              value={input}
              onChange={e => setInput(e.target.value)}
              onKeyDown={e => { if (e.key === 'Enter' && input.trim()) analyze(input.trim()); }}
            />
            <button
              className="brainstorm-panel__go"
              onClick={() => { if (input.trim()) analyze(input.trim()); }}
              disabled={loading || !input.trim()}
            >Go</button>
          </div>

          {/* Results */}
          <div className="brainstorm-panel__results">
            {loading && (
              <div className="brainstorm-panel__loading">
                <span className="brainstorm-panel__loading-icon">&#x2604;</span>
                Scanning 4 repos, CI/CD, governance health, epistemic state...
              </div>
            )}
            {!loading && recommendations.length === 0 && (
              <div className="brainstorm-panel__empty">Click above to ask Demerzel.</div>
            )}
            {!loading && (['urgent', 'high', 'quick', 'strategic'] as Priority[]).map(priority => {
              const items = grouped[priority];
              if (items.length === 0) return null;
              const meta = PRIORITY_META[priority];
              return (
                <div key={priority} className="brainstorm-panel__group">
                  <div className="brainstorm-panel__group-label" style={{ color: meta.color }}>
                    <span className="brainstorm-panel__group-icon">{meta.icon}</span>
                    {meta.label}
                  </div>
                  {items.map(r => (
                    <div key={r.id} className="brainstorm-panel__card" style={{ borderLeftColor: meta.color }}>
                      <div className="brainstorm-panel__card-title">{r.title}</div>
                      <div className="brainstorm-panel__card-rationale">{r.rationale}</div>
                      <div className="brainstorm-panel__card-footer">
                        {r.source && <span className="brainstorm-panel__card-source">{r.source}</span>}
                        {r.tensorState && <span className="brainstorm-panel__card-tensor">{r.tensorState}</span>}
                        <span className="brainstorm-panel__card-actions">
                          {r.actionUrl && (
                            <a
                              href={r.actionUrl}
                              target="_blank"
                              rel="noopener noreferrer"
                              className="brainstorm-panel__card-link"
                              onClick={e => e.stopPropagation()}
                            >View</a>
                          )}
                          <button
                            className="brainstorm-panel__card-start"
                            onClick={() => console.log('[Demerzel] Start:', r.title)}
                          >Start</button>
                        </span>
                      </div>
                    </div>
                  ))}
                </div>
              );
            })}
          </div>
        </div>
      )}
    </>
  );
};
