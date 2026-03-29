// src/components/PrimeRadiant/BrainstormPanel.tsx
// Demerzel's "What's Next?" advisor — side panel version (registered in IconRail).
// Pulls real context from GitHub, CI/CD, governance, and epistemic state.

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
function buildRecommendations(issues: GHIssue[], ciRuns: GHWorkflowRun[]): Recommendation[] {
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

  // High value: feature issues
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

  // Quick wins: short-titled issues
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

  // Strategic: epistemic items
  const amnesiaSchedule = JSON.parse(localStorage.getItem('epistemic-amnesia-schedule') ?? '[]') as { beliefId: string; executed: boolean }[];
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

  recs.push({
    id: 'epistemic-review',
    title: 'Run epistemic tensor review',
    rationale: 'Article E-9: periodic federated peer review prevents epistemic isolation.',
    priority: 'strategic',
    source: 'Epistemic Constitution',
    tensorState: 'E-9',
  });

  // Fallback if nothing found
  if (recs.length <= 2) {
    recs.push(
      { id: 'fb-1', title: 'Godot 4.6 bridge protocol', rationale: 'Phase 1 plan ready. Start with typed WebSocket events.', priority: 'high', source: 'Plan doc' },
      { id: 'fb-2', title: 'Blue-green build end-to-end test', rationale: 'Scripts exist but untested. Run full cycle.', priority: 'quick', source: 'Infrastructure' },
      { id: 'fb-3', title: 'demerzel:meta-brainstorm skill', rationale: 'Recursive meta-learning tool. Brainstorm session proved the concept.', priority: 'strategic', source: 'Epistemic Constitution' },
    );
  }

  return recs;
}

// ---------------------------------------------------------------------------
// Pipeline stages
// ---------------------------------------------------------------------------
type PipelineStage = 'idle' | 'brainstorm' | 'plan' | 'implement' | 'review' | 'compound' | 'done';

const PIPELINE_STAGES: { stage: PipelineStage; label: string; icon: string; color: string }[] = [
  { stage: 'brainstorm', label: 'Brainstorm', icon: '\uD83D\uDCA1', color: '#4FC3F7' },
  { stage: 'plan',       label: 'Plan',       icon: '\uD83D\uDCCB', color: '#FFD700' },
  { stage: 'implement',  label: 'Build',      icon: '\u2692',       color: '#33CC66' },
  { stage: 'review',     label: 'Review',     icon: '\uD83D\uDD0D', color: '#CE93D8' },
  { stage: 'compound',   label: 'Compound',   icon: '\uD83C\uDF31', color: '#FF8A65' },
];

interface PipelineState {
  itemId: string;
  stage: PipelineStage;
  log: string[];
  autopilot: boolean;
}

async function runPipelineStage(
  stage: PipelineStage,
  title: string,
  source: string | undefined,
): Promise<string> {
  // Call the real backend API — runs Claude Code CLI or Ollama
  const res = await fetch('/api/pipeline/run', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ stage, title, source }),
  });

  if (res.status === 401) {
    throw new Error('Admin access required');
  }

  if (!res.ok) {
    const err = await res.text().catch(() => 'Unknown error');
    throw new Error(`Pipeline failed: ${err}`);
  }

  const data = await res.json() as { stage: string; success: boolean; output: string; durationMs: number };
  if (!data.success) {
    throw new Error(data.output);
  }

  return `${data.output} (${Math.round(data.durationMs / 1000)}s)`;
}

// ---------------------------------------------------------------------------
// Admin check — only owner can run pipeline actions
// ---------------------------------------------------------------------------
function checkIsAdmin(): boolean {
  if (typeof window === 'undefined') return false;
  const { hostname } = window.location;
  if (hostname === 'localhost' || hostname === '127.0.0.1') return true;
  return localStorage.getItem('pr-admin-token') === 'ga-owner-2026';
}

// ---------------------------------------------------------------------------
// Component — side panel (no floating trigger)
// ---------------------------------------------------------------------------
export const BrainstormPanel: React.FC = () => {
  const isAdmin = checkIsAdmin();
  const [recommendations, setRecommendations] = useState<Recommendation[]>([]);
  const [loading, setLoading] = useState(false);
  const [lastUpdated, setLastUpdated] = useState<string | null>(null);
  const [input, setInput] = useState('');
  const initialLoad = useRef(false);
  const [pipeline, setPipeline] = useState<PipelineState | null>(null);
  const pipelineAbortRef = useRef(false);

  const analyze = useCallback(async (query?: string) => {
    setLoading(true);
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
    } catch { /* fallback */ }

    const [issues, ciRuns] = await Promise.all([fetchOpenIssues(), fetchCIStatus()]);
    let recs = buildRecommendations(issues, ciRuns);
    if (query) {
      const q = query.toLowerCase();
      const filtered = recs.filter(r => r.title.toLowerCase().includes(q) || r.rationale.toLowerCase().includes(q));
      if (filtered.length > 0) recs = filtered;
    }
    setRecommendations(recs);
    setLastUpdated(new Date().toLocaleTimeString());
    setLoading(false);
  }, []);

  // Run a single pipeline stage for an item
  const runStage = useCallback(async (itemId: string, stage: PipelineStage) => {
    const rec = recommendations.find(r => r.id === itemId);
    if (!rec) return;
    setPipeline(prev => ({
      itemId,
      stage,
      log: [...(prev?.itemId === itemId ? prev.log : [])],
      autopilot: prev?.autopilot ?? false,
    }));
    try {
      const result = await runPipelineStage(stage, rec.title, rec.source);
      setPipeline(prev => prev ? { ...prev, stage: 'idle', log: [...prev.log, `[${stage}] ${result}`] } : null);
      return result;
    } catch (err) {
      const msg = err instanceof Error ? err.message : 'Unknown error';
      setPipeline(prev => prev ? { ...prev, stage: 'idle', log: [...prev.log, `[${stage}] ERROR: ${msg}`] } : null);
      return undefined;
    }
  }, [recommendations]);

  // Run full pipeline (autopilot)
  const runFullPipeline = useCallback(async (itemId: string) => {
    pipelineAbortRef.current = false;
    setPipeline({ itemId, stage: 'brainstorm', log: [], autopilot: true });
    for (const { stage } of PIPELINE_STAGES) {
      if (pipelineAbortRef.current) break;
      const rec = recommendations.find(r => r.id === itemId);
      if (!rec) break;
      setPipeline(prev => prev ? { ...prev, stage } : null);
      const result = await runPipelineStage(stage, rec.title, rec.source);
      setPipeline(prev => prev ? { ...prev, log: [...prev.log, `[${stage}] ${result}`] } : null);
    }
    if (!pipelineAbortRef.current) {
      setPipeline(prev => prev ? { ...prev, stage: 'done' } : null);
    }
  }, [recommendations]);

  // Auto-pick: run autopilot on the highest-priority item
  const autoPick = useCallback(() => {
    const first = recommendations[0];
    if (first) runFullPipeline(first.id);
  }, [recommendations, runFullPipeline]);

  const stopPipeline = useCallback(() => {
    pipelineAbortRef.current = true;
    setPipeline(prev => prev ? { ...prev, stage: 'idle', autopilot: false } : null);
  }, []);

  // Auto-analyze on mount
  useEffect(() => {
    if (!initialLoad.current) {
      initialLoad.current = true;
      analyze();
    }
  }, [analyze]);

  const grouped = recommendations.reduce<Record<Priority, Recommendation[]>>((acc, r) => {
    (acc[r.priority] ??= []).push(r);
    return acc;
  }, { urgent: [], high: [], quick: [], strategic: [] });

  return (
    <div className="brainstorm-side">
      <div className="brainstorm-side__header">
        <span className="brainstorm-side__title">Demerzel recommends</span>
        {lastUpdated && <span className="brainstorm-side__time">{lastUpdated}</span>}
        <button className="brainstorm-side__refresh" onClick={() => analyze()} disabled={loading} title="Refresh">
          <svg width="12" height="12" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round">
            <path d="M3 12a9 9 0 1 0 9-9 9.75 9.75 0 0 0-6.74 2.74L3 8" />
            <path d="M3 3v5h5" />
          </svg>
        </button>
      </div>

      {/* Quick actions */}
      <div className="brainstorm-side__actions">
        <button className="brainstorm-side__auto" onClick={() => analyze()} disabled={loading}>
          {loading ? 'Scanning...' : "What's next?"}
        </button>
        {isAdmin && (
          <button
            className="brainstorm-side__autopilot"
            onClick={pipeline?.autopilot ? stopPipeline : autoPick}
            disabled={loading || recommendations.length === 0}
            title={pipeline?.autopilot ? 'Stop autopilot' : 'Auto-pick top item and run full pipeline'}
          >
            {pipeline?.autopilot ? '\u23F9 Stop' : '\u25B6 Autopilot'}
          </button>
        )}
      </div>

      {!isAdmin && (
        <div className="brainstorm-side__readonly">
          Read-only mode — pipeline actions require admin access
        </div>
      )}

      {/* Focused query */}
      <div className="brainstorm-side__input-row">
        <input
          className="brainstorm-side__input"
          placeholder="Ask about a specific area..."
          value={input}
          onChange={e => setInput(e.target.value)}
          onKeyDown={e => { if (e.key === 'Enter' && input.trim()) analyze(input.trim()); }}
        />
      </div>

      {/* Results */}
      <div className="brainstorm-side__results">
        {loading && <div className="brainstorm-side__loading">Analyzing 4 repos...</div>}
        {!loading && recommendations.length === 0 && (
          <div className="brainstorm-side__empty">No recommendations yet.</div>
        )}
        {!loading && (['urgent', 'high', 'quick', 'strategic'] as Priority[]).map(priority => {
          const items = grouped[priority];
          if (items.length === 0) return null;
          const meta = PRIORITY_META[priority];
          return (
            <div key={priority} className="brainstorm-side__group">
              <div className="brainstorm-side__group-label" style={{ color: meta.color }}>
                <span>{meta.icon}</span> {meta.label}
              </div>
              {items.map(r => {
                const isPipelineTarget = pipeline?.itemId === r.id;
                return (
                  <div key={r.id} className={`brainstorm-side__card ${isPipelineTarget ? 'brainstorm-side__card--active' : ''}`} style={{ borderLeftColor: meta.color }}>
                    <div className="brainstorm-side__card-title">{r.title}</div>
                    <div className="brainstorm-side__card-rationale">{r.rationale}</div>
                    <div className="brainstorm-side__card-footer">
                      {r.source && <span className="brainstorm-side__card-source">{r.source}</span>}
                      {r.tensorState && <span className="brainstorm-side__card-tensor">{r.tensorState}</span>}
                      {r.actionUrl && (
                        <a href={r.actionUrl} target="_blank" rel="noopener noreferrer" className="brainstorm-side__card-link">View</a>
                      )}
                    </div>

                    {/* Pipeline action buttons — admin only */}
                    {isAdmin && <div className="brainstorm-side__pipeline">
                      {PIPELINE_STAGES.map(({ stage, label, icon, color }) => {
                        const isActive = isPipelineTarget && pipeline?.stage === stage;
                        const isDone = isPipelineTarget && pipeline?.log.some(l => l.startsWith(`[${stage}]`));
                        return (
                          <button
                            key={stage}
                            className={`brainstorm-side__stage ${isActive ? 'brainstorm-side__stage--active' : ''} ${isDone ? 'brainstorm-side__stage--done' : ''}`}
                            style={{ '--stage-color': color } as React.CSSProperties}
                            onClick={() => runStage(r.id, stage)}
                            disabled={isActive}
                            title={`${label} this item`}
                          >
                            <span>{isDone ? '\u2713' : icon}</span>
                            <span className="brainstorm-side__stage-label">{label}</span>
                          </button>
                        );
                      })}
                      <button
                        className="brainstorm-side__stage brainstorm-side__stage--auto"
                        onClick={() => runFullPipeline(r.id)}
                        disabled={isPipelineTarget && pipeline?.autopilot}
                        title="Run full pipeline: Brainstorm → Plan → Build → Review → Compound"
                      >
                        <span>{'\u25B6'}</span>
                        <span className="brainstorm-side__stage-label">All</span>
                      </button>
                    </div>}

                    {/* Pipeline log — visible to all (read-only context) */}
                    {isPipelineTarget && pipeline.log.length > 0 && (
                      <div className="brainstorm-side__log">
                        {pipeline.log.map((line, i) => (
                          <div key={i} className="brainstorm-side__log-line">{line}</div>
                        ))}
                        {pipeline.stage === 'done' && (
                          <div className="brainstorm-side__log-done">Pipeline complete</div>
                        )}
                      </div>
                    )}
                  </div>
                );
              })}
            </div>
          );
        })}
      </div>
    </div>
  );
};
