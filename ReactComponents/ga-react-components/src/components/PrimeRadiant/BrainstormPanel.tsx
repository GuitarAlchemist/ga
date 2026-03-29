// src/components/PrimeRadiant/BrainstormPanel.tsx
// AI-powered "what's next" advisor — surfaces prioritized recommendations

import React, { useState, useCallback } from 'react';

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
}

interface BrainstormState {
  recommendations: Recommendation[];
  loading: boolean;
  query: string;
}

// ---------------------------------------------------------------------------
// Priority styling
// ---------------------------------------------------------------------------
const PRIORITY_META: Record<Priority, { label: string; color: string }> = {
  urgent:    { label: 'Urgent',    color: '#FF4444' },
  high:      { label: 'High Value', color: '#FFD700' },
  quick:     { label: 'Quick Win', color: '#33CC66' },
  strategic: { label: 'Strategic', color: '#4FC3F7' },
};

// ---------------------------------------------------------------------------
// Mock recommendations (derived from real backlog)
// ---------------------------------------------------------------------------
const MOCK_RECOMMENDATIONS: Recommendation[] = [
  { id: 'r1', title: 'Fix CI/CD pipeline visibility', rationale: 'CI panel shows stale data — real-time status checks improve deploy confidence.', priority: 'urgent', source: 'CI/CD health' },
  { id: 'r2', title: 'Godot 4.6 bridge protocol', rationale: 'Foundation for 3D governance viz. Start with typed WebSocket events.', priority: 'high', source: 'Backlog #37' },
  { id: 'r3', title: 'AI-powered backlog beliefs', rationale: 'Helps prioritize the 30+ backlog items with feasibility and confidence scores.', priority: 'high', source: 'Backlog #34' },
  { id: 'r4', title: 'Rich hover popovers on rail icons', rationale: 'Small UX lift — quick-glance stats without opening panels.', priority: 'quick', source: 'Backlog #32' },
  { id: 'r5', title: 'Admin-only access controls', rationale: 'Required before public deployment. Simple isAdmin gate.', priority: 'quick', source: 'Backlog #30' },
  { id: 'r6', title: 'OPTIC-K v2 embedding expansion', rationale: 'Long-term quality improvement for semantic search. Needs full re-index.', priority: 'strategic', source: 'Backlog' },
  { id: 'r7', title: 'Spectral RAG multi-hop retrieval', rationale: 'Better answer quality for complex queries. Builds on existing vector infra.', priority: 'strategic', source: 'Backlog' },
];

// ---------------------------------------------------------------------------
// Hook
// ---------------------------------------------------------------------------
function useBrainstormAdvice() {
  const [state, setState] = useState<BrainstormState>({
    recommendations: [],
    loading: false,
    query: '',
  });

  const advise = useCallback(async (query?: string) => {
    setState(prev => ({ ...prev, loading: true, query: query ?? '' }));

    try {
      const res = await fetch('/api/brainstorm/advise', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ query: query ?? 'What should I work on next?' }),
      });
      if (res.ok) {
        const data = await res.json() as { recommendations: Recommendation[] };
        setState({ recommendations: data.recommendations, loading: false, query: query ?? '' });
        return;
      }
    } catch { /* fallback below */ }

    // Mock fallback — filter by query if provided
    await new Promise(r => setTimeout(r, 600));
    const q = (query ?? '').toLowerCase();
    const filtered = q
      ? MOCK_RECOMMENDATIONS.filter(r => r.title.toLowerCase().includes(q) || r.rationale.toLowerCase().includes(q))
      : MOCK_RECOMMENDATIONS;
    setState({ recommendations: filtered.length > 0 ? filtered : MOCK_RECOMMENDATIONS, loading: false, query: query ?? '' });
  }, []);

  return { ...state, advise };
}

// ---------------------------------------------------------------------------
// Component
// ---------------------------------------------------------------------------
export const BrainstormPanel: React.FC = () => {
  const [open, setOpen] = useState(false);
  const [input, setInput] = useState('');
  const { recommendations, loading, advise } = useBrainstormAdvice();

  const grouped = recommendations.reduce<Record<Priority, Recommendation[]>>((acc, r) => {
    (acc[r.priority] ??= []).push(r);
    return acc;
  }, { urgent: [], high: [], quick: [], strategic: [] });

  return (
    <>
      {/* Trigger button */}
      <button
        className="brainstorm-trigger"
        onClick={() => {
          setOpen(v => !v);
          if (!open && recommendations.length === 0) advise();
        }}
        title="Brainstorm — what should I work on next?"
        aria-label="Open brainstorm advisor"
      >
        <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
          <path d="M9.663 17h4.673M12 3v1m6.364 1.636l-.707.707M21 12h-1M4 12H3m3.343-5.657l-.707-.707m2.828 9.9a5 5 0 1 1 7.072 0l-.548.547A3.374 3.374 0 0 0 14 18.469V19a2 2 0 1 1-4 0v-.531c0-.895-.356-1.754-.988-2.386l-.548-.547z" />
        </svg>
      </button>

      {/* Panel */}
      {open && (
        <div className="brainstorm-panel">
          <div className="brainstorm-panel__header">
            <span className="brainstorm-panel__title">Brainstorm</span>
            <button className="brainstorm-panel__close" onClick={() => setOpen(false)}>&times;</button>
          </div>

          {/* Quick action */}
          <button
            className="brainstorm-panel__auto"
            onClick={() => advise()}
            disabled={loading}
          >
            {loading ? 'Thinking...' : "What should I work on next?"}
          </button>

          {/* Focused query */}
          <div className="brainstorm-panel__input-row">
            <input
              className="brainstorm-panel__input"
              placeholder="Or ask about a specific area..."
              value={input}
              onChange={e => setInput(e.target.value)}
              onKeyDown={e => { if (e.key === 'Enter' && input.trim()) advise(input.trim()); }}
            />
            <button
              className="brainstorm-panel__go"
              onClick={() => { if (input.trim()) advise(input.trim()); }}
              disabled={loading || !input.trim()}
            >Go</button>
          </div>

          {/* Results */}
          <div className="brainstorm-panel__results">
            {loading && <div className="brainstorm-panel__loading">Gathering context and analyzing priorities...</div>}
            {!loading && recommendations.length === 0 && (
              <div className="brainstorm-panel__empty">Click above to get AI-powered recommendations.</div>
            )}
            {!loading && (['urgent', 'high', 'quick', 'strategic'] as Priority[]).map(priority => {
              const items = grouped[priority];
              if (items.length === 0) return null;
              const meta = PRIORITY_META[priority];
              return (
                <div key={priority} className="brainstorm-panel__group">
                  <div className="brainstorm-panel__group-label" style={{ color: meta.color }}>
                    {meta.label}
                  </div>
                  {items.map(r => (
                    <div key={r.id} className="brainstorm-panel__card" style={{ borderLeftColor: meta.color }}>
                      <div className="brainstorm-panel__card-title">{r.title}</div>
                      <div className="brainstorm-panel__card-rationale">{r.rationale}</div>
                      {r.source && <div className="brainstorm-panel__card-source">{r.source}</div>}
                      <button
                        className="brainstorm-panel__card-start"
                        onClick={() => console.log('[Brainstorm] Start:', r.title)}
                      >Start</button>
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
