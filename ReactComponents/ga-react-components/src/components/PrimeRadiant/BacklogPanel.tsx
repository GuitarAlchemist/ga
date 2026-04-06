// src/components/PrimeRadiant/BacklogPanel.tsx
// Shows the project backlog with AI-powered belief assessments per item

import React, { useEffect, useState, useCallback } from 'react';

// ---------------------------------------------------------------------------
// Types
// ---------------------------------------------------------------------------
interface BacklogSection {
  section: string;
  subsection?: string;
  items: string[];
}

interface BacklogData {
  sections: BacklogSection[];
  lastModified?: string;
}

type Feasibility = 'easy' | 'medium' | 'hard' | 'research';
type TShirtSize = 'S' | 'M' | 'L' | 'XL';
type BeliefValue = 'T' | 'F' | 'U' | 'C';

interface BacklogAssessment {
  feasibility: Feasibility;
  effort: TShirtSize;
  dependencies: string[];
  infraReady: number; // 0-100
  approach: string;
  belief: BeliefValue;
  confidence: number; // 0-1
}

// ---------------------------------------------------------------------------
// Mock assessment generator
// ---------------------------------------------------------------------------
const FEASIBILITY_COLORS: Record<Feasibility, string> = {
  easy: '#33CC66',
  medium: '#FFB300',
  hard: '#FF4444',
  research: '#CE93D8',
};

const BELIEF_LABELS: Record<BeliefValue, string> = {
  T: 'True — should prioritize',
  F: 'False — deprioritize',
  U: 'Unknown — needs investigation',
  C: 'Contradictory — mixed signals',
};

function generateAssessment(title: string): BacklogAssessment {
  const lower = title.toLowerCase();
  const hasAI = /\b(ai|ml|godot|embedding|spectral|rag)\b/.test(lower);
  const hasFix = /\b(fix|panel|button|icon|badge)\b/.test(lower);
  const hasIntegration = /\b(integrat|pipeline|bridge|protocol)\b/.test(lower);

  let feasibility: Feasibility = 'medium';
  let effort: TShirtSize = 'M';
  let infraReady = 45;
  let belief: BeliefValue = 'U';
  let confidence = 0.6;

  if (hasFix) {
    feasibility = 'easy'; effort = 'S'; infraReady = 75; belief = 'T'; confidence = 0.85;
  } else if (hasAI) {
    feasibility = 'hard'; effort = 'XL'; infraReady = 30; belief = 'U'; confidence = 0.4;
  } else if (hasIntegration) {
    feasibility = 'medium'; effort = 'L'; infraReady = 50; belief = 'T'; confidence = 0.7;
  }

  const deps = [];
  if (hasAI) deps.push('Ollama running locally', 'OPTIC-K schema stable');
  if (hasIntegration) deps.push('Backend API endpoint', 'SignalR hub');
  if (deps.length === 0) deps.push('No blocking dependencies');

  const approaches = [
    'Start with the existing panel infrastructure and extend incrementally.',
    'Create a new component following the PanelRegistry pattern, wire into IconRail.',
    'Prototype in isolation first, then integrate into ForceRadiant once stable.',
  ];
  const approach = approaches[title.length % approaches.length];

  return { feasibility, effort, dependencies: deps, infraReady, approach, belief, confidence };
}

// ---------------------------------------------------------------------------
// Hook: lazy assessment with cache
// ---------------------------------------------------------------------------
function useBacklogAssessment(itemTitle: string | null): {
  assessment: BacklogAssessment | null;
  loading: boolean;
  refresh: () => void;
} {
  const [assessment, setAssessment] = useState<BacklogAssessment | null>(null);
  const [loading, setLoading] = useState(false);

  const cacheKey = itemTitle ? `backlog-belief-${btoa(unescape(encodeURIComponent(itemTitle))).slice(0, 24)}` : null;

  const fetchAssessment = useCallback(async (useCache: boolean) => {
    if (!itemTitle || !cacheKey) return;

    // Check cache first
    if (useCache) {
      const cached = localStorage.getItem(cacheKey);
      if (cached) {
        try { setAssessment(JSON.parse(cached)); return; } catch { /* ignore */ }
      }
    }

    setLoading(true);
    try {
      const res = await fetch(`/api/governance/backlog-analysis?item=${encodeURIComponent(itemTitle)}`);
      if (res.ok) {
        const data = await res.json() as BacklogAssessment;
        localStorage.setItem(cacheKey, JSON.stringify(data));
        setAssessment(data);
        setLoading(false);
        return;
      }
    } catch { /* fallback below */ }

    // Mock fallback
    await new Promise(r => setTimeout(r, 400));
    const mock = generateAssessment(itemTitle);
    localStorage.setItem(cacheKey, JSON.stringify(mock));
    setAssessment(mock);
    setLoading(false);
  }, [itemTitle, cacheKey]);

  useEffect(() => {
    if (itemTitle) fetchAssessment(true);
    else setAssessment(null);
  }, [itemTitle, fetchAssessment]);

  const refresh = useCallback(() => {
    if (cacheKey) localStorage.removeItem(cacheKey);
    fetchAssessment(false);
  }, [cacheKey, fetchAssessment]);

  return { assessment, loading, refresh };
}

// ---------------------------------------------------------------------------
// Component
// ---------------------------------------------------------------------------
export const BacklogPanel: React.FC<{ collapsed?: boolean }> = ({ collapsed: initialCollapsed = false }) => {
  const [data, setData] = useState<BacklogData | null>(null);
  const [collapsed, setCollapsed] = useState(initialCollapsed);
  const [expandedSections, setExpandedSections] = useState<Set<string>>(new Set());
  const [selectedItem, setSelectedItem] = useState<string | null>(null);
  const { assessment, loading, refresh } = useBacklogAssessment(selectedItem);

  useEffect(() => {
    const fetchBacklog = async () => {
      try {
        const res = await fetch('/api/governance/backlog');
        if (res.ok) {
          setData(await res.json());
        }
      } catch {
        // Fallback: representative backlog when API is unavailable
        setData({
          sections: [
            { section: 'Governance', items: [
              'Cycle 005: Full governance audit with compliance scoring',
              'Streeling: Expand multilingual course coverage to 12 languages',
              'Proto-conscience: Observability dashboard for ethical decisions',
            ]},
            { section: 'Research', items: [
              'Hari: Lie algebra DNA architecture — prototype phase',
              'Seldon Plan: Long-horizon prediction engine validation',
              'Hexavalent logic: Integration with belief currency system',
            ]},
            { section: 'Build', items: [
              'Prime Radiant: Real-time WebSocket updates',
              'ix: Memristive Markov persistence layer',
              'tars: F# reasoning agent — belief propagation',
              'ga: Guitar Singularity — interactive fretboard',
            ]},
            { section: 'AI/ML', items: [
              'OPTIC-K v2: Expand embedding schema to 256 dims',
              'Spectral RAG: Multi-hop retrieval with reranking',
              'Ollama fleet: Load balancing across GPU instances',
            ]},
          ],
        });
      }
    };
    fetchBacklog();
  }, []);

  const toggleSection = (key: string) => {
    setExpandedSections(prev => {
      const next = new Set(prev);
      if (next.has(key)) next.delete(key);
      else next.add(key);
      return next;
    });
  };

  const totalItems = data ? data.sections.reduce((sum, s) => sum + s.items.length, 0) : 0;

  return (
    <div className="prime-radiant__backlog">
      <div
        className="prime-radiant__backlog-header"
        onClick={() => setCollapsed(!collapsed)}
      >
        <span className="prime-radiant__backlog-title">
          Backlog
          <span className="prime-radiant__backlog-count">{totalItems}</span>
        </span>
        <span className="prime-radiant__backlog-toggle">{collapsed ? '▶' : '▼'}</span>
      </div>

      {!collapsed && data && (
        <div className="prime-radiant__backlog-body">
          {data.sections.map((section, i) => {
            const key = `${section.section}-${section.subsection ?? i}`;
            const isExpanded = expandedSections.has(key);
            const label = section.subsection
              ? `${section.subsection}`
              : section.section;

            return (
              <div key={key} className="prime-radiant__backlog-section">
                <div
                  className="prime-radiant__backlog-section-header"
                  onClick={() => toggleSection(key)}
                >
                  <span>{isExpanded ? '▼' : '▶'}</span>
                  <span className="prime-radiant__backlog-section-title">{label}</span>
                  <span className="prime-radiant__backlog-section-count">{section.items.length}</span>
                </div>

                {isExpanded && (
                  <div className="prime-radiant__backlog-items">
                    {section.items.map((item, j) => {
                      const colonIdx = item.indexOf(':');
                      const title = colonIdx > 0 ? item.slice(0, colonIdx) : item;
                      const desc = colonIdx > 0 ? item.slice(colonIdx + 1).trim() : '';
                      const isSelected = selectedItem === item;

                      return (
                        <div key={j} className="prime-radiant__backlog-item-wrap">
                          <div
                            className={`prime-radiant__backlog-item ${isSelected ? 'prime-radiant__backlog-item--selected' : ''}`}
                            title={item}
                            onClick={() => setSelectedItem(isSelected ? null : item)}
                          >
                            <span className="prime-radiant__backlog-item-title">{title}</span>
                            {desc && <span className="prime-radiant__backlog-item-desc">{desc}</span>}
                            <span className="prime-radiant__backlog-item-expand">{isSelected ? '▼' : '▶'}</span>
                          </div>

                          {/* AI Assessment card */}
                          {isSelected && (
                            <div className="backlog-assess">
                              {loading ? (
                                <div className="backlog-assess__loading">Analyzing...</div>
                              ) : assessment ? (
                                <>
                                  <div className="backlog-assess__row">
                                    <span
                                      className="backlog-assess__badge"
                                      style={{ background: `${FEASIBILITY_COLORS[assessment.feasibility]}22`, color: FEASIBILITY_COLORS[assessment.feasibility], borderColor: `${FEASIBILITY_COLORS[assessment.feasibility]}44` }}
                                    >
                                      {assessment.feasibility}
                                    </span>
                                    <span className="backlog-assess__effort">{assessment.effort}</span>
                                    <span className="backlog-assess__belief" title={BELIEF_LABELS[assessment.belief]}>
                                      {assessment.belief} ({Math.round(assessment.confidence * 100)}%)
                                    </span>
                                  </div>

                                  <div className="backlog-assess__infra">
                                    <span className="backlog-assess__infra-label">Infrastructure ready</span>
                                    <div className="backlog-assess__infra-bar">
                                      <div
                                        className="backlog-assess__infra-fill"
                                        style={{ width: `${assessment.infraReady}%`, background: assessment.infraReady > 60 ? '#33CC66' : assessment.infraReady > 30 ? '#FFB300' : '#FF4444' }}
                                      />
                                    </div>
                                    <span className="backlog-assess__infra-pct">{assessment.infraReady}%</span>
                                  </div>

                                  <div className="backlog-assess__approach">{assessment.approach}</div>

                                  <div className="backlog-assess__deps">
                                    <span className="backlog-assess__deps-label">Dependencies</span>
                                    {assessment.dependencies.map((d, di) => (
                                      <span key={di} className="backlog-assess__dep">{d}</span>
                                    ))}
                                  </div>

                                  <div className="backlog-assess__actions">
                                    <button className="backlog-assess__refresh" onClick={(e) => { e.stopPropagation(); refresh(); }}>
                                      Re-analyze
                                    </button>
                                    <button className="backlog-assess__start" onClick={(e) => { e.stopPropagation(); console.log('[BacklogPanel] Start /feature for:', item); }}>
                                      Start /feature
                                    </button>
                                  </div>
                                </>
                              ) : null}
                            </div>
                          )}
                        </div>
                      );
                    })}
                  </div>
                )}
              </div>
            );
          })}
        </div>
      )}
    </div>
  );
};
