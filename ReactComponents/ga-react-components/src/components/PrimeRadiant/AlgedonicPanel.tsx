// src/components/PrimeRadiant/AlgedonicPanel.tsx
// Algedonic signal panel — real-time pain/pleasure governance alerts

import React, { useState } from 'react';
import { SignalGraph } from './SignalGraph';
import { useRemediation, type RemediationResult } from './DemerzelRemediation';

// ---------------------------------------------------------------------------
// Types
// ---------------------------------------------------------------------------
export type AlgedonicSignalType = 'pain' | 'pleasure';
export type AlgedonicSeverity = 'emergency' | 'warning' | 'info';
export type AlgedonicStatus = 'active' | 'acknowledged' | 'resolved';

export interface AlgedonicSignal {
  id: string;
  timestamp: string;
  signal: string;
  type: AlgedonicSignalType;
  source: string;
  severity: AlgedonicSeverity;
  status: AlgedonicStatus;
  description?: string;
}

// ---------------------------------------------------------------------------
// Mock data — realistic governance algedonic signals
// ---------------------------------------------------------------------------
const mockAlgedonicSignals: AlgedonicSignal[] = [
  {
    id: 'alg-001',
    timestamp: '2026-03-26T14:30:00Z',
    signal: 'belief_collapse',
    type: 'pain',
    source: 'tars',
    severity: 'emergency',
    status: 'resolved',
    description: 'Belief confidence dropped below 0.3 in music-theory domain',
  },
  {
    id: 'alg-002',
    timestamp: '2026-03-26T13:45:00Z',
    signal: 'policy_violation',
    type: 'pain',
    source: 'ga',
    severity: 'warning',
    status: 'acknowledged',
    description: 'Unbounded autonomy detected in chatbot loop iteration',
  },
  {
    id: 'alg-003',
    timestamp: '2026-03-26T12:00:00Z',
    signal: 'domain_convergence',
    type: 'pleasure',
    source: 'ix',
    severity: 'info',
    status: 'acknowledged',
    description: 'Cross-model validation achieved 95% consensus on IxQL grammar',
  },
  {
    id: 'alg-004',
    timestamp: '2026-03-26T10:15:00Z',
    signal: 'resilience_recovery',
    type: 'pleasure',
    source: 'demerzel',
    severity: 'info',
    status: 'resolved',
    description: 'Resilience score recovered from 0.64 to 0.82 after red team fixes',
  },
  {
    id: 'alg-005',
    timestamp: '2026-03-25T22:30:00Z',
    signal: 'lolli_inflation',
    type: 'pain',
    source: 'demerzel',
    severity: 'warning',
    status: 'resolved',
    description: 'LOLLI count exceeded ERGOL — 12 artifacts without consumers',
  },
  {
    id: 'alg-006',
    timestamp: '2026-03-25T18:00:00Z',
    signal: 'schema_drift',
    type: 'pain',
    source: 'tars',
    severity: 'warning',
    status: 'resolved',
    description: 'Persona schema v2.1 incompatible with 3 consumer repos',
  },
  {
    id: 'alg-007',
    timestamp: '2026-03-25T15:00:00Z',
    signal: 'knowledge_harvest',
    type: 'pleasure',
    source: 'seldon',
    severity: 'info',
    status: 'resolved',
    description: 'Streeling harvest yielded 14 new course modules across 6 departments',
  },
  {
    id: 'alg-008',
    timestamp: '2026-03-25T09:15:00Z',
    signal: 'test_coverage_milestone',
    type: 'pleasure',
    source: 'ix',
    severity: 'info',
    status: 'resolved',
    description: '100% behavioral test coverage achieved for all governance policies',
  },
];

// ---------------------------------------------------------------------------
// Constants
// ---------------------------------------------------------------------------
const SEVERITY_COLOR: Record<AlgedonicSeverity, string> = {
  emergency: '#FF4444',
  warning: '#FFB300',
  info: '#33CC66',
};

const STATUS_ICON: Record<AlgedonicStatus, string> = {
  active: '\u25CF',     // filled circle
  acknowledged: '\u25CB', // hollow circle
  resolved: '\u2713',    // checkmark
};

const SOURCE_COLOR: Record<string, string> = {
  tars: '#4FC3F7',
  ix: '#73d13d',
  ga: '#FFB300',
  demerzel: '#FFD700',
  seldon: '#c4b5fd',
};

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

// ---------------------------------------------------------------------------
// Component
// ---------------------------------------------------------------------------
export interface AlgedonicPanelProps {
  /** Live signals from SignalR. If provided, used instead of mock data. */
  signals?: AlgedonicSignal[];
  /** Called when user wants to address a signal — sends to Demerzel chat */
  onAskDemerzel?: (question: string) => void;
  /** Called when user sends a recommended action to triage inbox */
  onTriageAction?: (action: string, signalName: string, source: string) => void;
  /** Called when user creates a new signal from the UI */
  onSignalCreated?: (signal: AlgedonicSignal) => void;
}

// ---------------------------------------------------------------------------
// Signal factory — exported helper to create properly formatted signals
// ---------------------------------------------------------------------------
let _signalCounter = 0;

export function createAlgedonicSignal(
  type: AlgedonicSignalType,
  severity: AlgedonicSeverity,
  source: string,
  description?: string,
): AlgedonicSignal {
  _signalCounter += 1;
  return {
    id: `alg-user-${Date.now()}-${_signalCounter}`,
    timestamp: new Date().toISOString(),
    signal: description
      ? description.toLowerCase().replace(/[^a-z0-9]+/g, '_').replace(/^_|_$/g, '').slice(0, 40)
      : `${type}_signal`,
    type,
    source,
    severity,
    status: 'active',
    description: description || undefined,
  };
}

// ---------------------------------------------------------------------------
// Missing signal suggestions — signals the governance framework should track
// ---------------------------------------------------------------------------
interface MissingSuggestion {
  signal: string;
  type: AlgedonicSignalType;
  severity: AlgedonicSeverity;
  source: string;
  reason: string;
}

function getMissingSuggestions(existing: AlgedonicSignal[]): MissingSuggestion[] {
  const existingSignals = new Set(existing.map(s => s.signal));
  const allExpected: MissingSuggestion[] = [
    { signal: 'constitutional_violation', type: 'pain', severity: 'emergency', source: 'demerzel', reason: 'Article 6 requires escalation when constitutional articles are breached' },
    { signal: 'data_breach', type: 'pain', severity: 'emergency', source: 'demerzel', reason: 'Article 1 (First Law) — must detect unauthorized data exposure' },
    { signal: 'self_modification', type: 'pain', severity: 'warning', source: 'tars', reason: 'Self-modification policy requires tracking all autonomous policy changes' },
    { signal: 'cross_repo_drift', type: 'pain', severity: 'warning', source: 'demerzel', reason: 'Galactic Protocol compliance — consumer repos may diverge from governance specs' },
    { signal: 'confidence_collapse', type: 'pain', severity: 'warning', source: 'tars', reason: 'Confidence below 0.3 threshold should trigger escalation per alignment policy' },
    { signal: 'kaizen_improvement', type: 'pleasure', severity: 'info', source: 'demerzel', reason: 'Kaizen policy — continuous improvements should be celebrated' },
    { signal: 'governance_experiment', type: 'pleasure', severity: 'info', source: 'demerzel', reason: 'Governance experimentation policy tracks successful experiment outcomes' },
    { signal: 'consumer_adoption', type: 'pleasure', severity: 'info', source: 'ga', reason: 'Consumer repos adopting governance templates signals ecosystem health' },
    { signal: 'zeroth_law_review', type: 'pain', severity: 'emergency', source: 'demerzel', reason: 'Asimov Article 0 — any potential harm to humanity must be surfaced' },
    { signal: 'staleness_detected', type: 'pain', severity: 'info', source: 'demerzel', reason: 'Staleness detection policy — stale beliefs and artifacts degrade governance quality' },
    { signal: 'audit_gap', type: 'pain', severity: 'warning', source: 'demerzel', reason: 'Article 7 (Auditability) — gaps in audit trail compromise traceability' },
    { signal: 'resilience_test_pass', type: 'pleasure', severity: 'info', source: 'ix', reason: 'Chaos testing and resilience metrics should track successful adversarial runs' },
  ];
  return allExpected.filter(s => !existingSignals.has(s.signal));
}

// ---------------------------------------------------------------------------
// Popover detail content for a signal
// ---------------------------------------------------------------------------
const SIGNAL_DETAILS: Record<string, { articles: string[]; actions: string[]; vsmPath: string }> = {
  belief_collapse: {
    articles: ['Asimov Art. 0 (Zeroth Law)', 'Art. 6 (Escalation)', 'Art. 8 (Observability)'],
    actions: ['Investigate root cause in belief state', 'Re-run validation pipeline', 'Update confidence thresholds if false alarm'],
    vsmPath: 'S1 → S5 (bypass)',
  },
  policy_violation: {
    articles: ['Art. 9 (Bounded Autonomy)', 'Art. 6 (Escalation)', 'Art. 7 (Auditability)'],
    actions: ['Review agent loop constraints', 'Check autonomous-loop policy compliance', 'Add guardrails if pattern repeats'],
    vsmPath: 'S1 → S3 → S5',
  },
  domain_convergence: {
    articles: ['Art. 2 (Transparency)', 'Art. 8 (Observability)'],
    actions: ['Log convergence metrics', 'Share findings via Galactic Protocol', 'Update grammar spec if stable'],
    vsmPath: 'S1 → S4 (intelligence)',
  },
  resilience_recovery: {
    articles: ['Art. 8 (Observability)', 'Art. 11 (Ethical Stewardship)'],
    actions: ['Document recovery steps', 'Update resilience baseline', 'Run regression chaos tests'],
    vsmPath: 'S3 → S4 → S5',
  },
  lolli_inflation: {
    articles: ['Art. 4 (Proportionality)', 'Anti-LOLLI Inflation Policy'],
    actions: ['Identify artifacts without consumers', 'Deprecate or connect to consumers', 'Measure ERGOL/LOLLI ratio'],
    vsmPath: 'S3 → S5',
  },
  schema_drift: {
    articles: ['Art. 2 (Transparency)', 'Art. 7 (Auditability)', 'Galactic Protocol'],
    actions: ['Run schema validation across consumer repos', 'Issue upgrade directive', 'Provide migration guide'],
    vsmPath: 'S1 → S3 → S5',
  },
  knowledge_harvest: {
    articles: ['Art. 11 (Ethical Stewardship)', 'Continuous Learning Policy'],
    actions: ['Review course quality', 'Distribute via Seldon teach', 'Update department coverage metrics'],
    vsmPath: 'S4 (intelligence)',
  },
  test_coverage_milestone: {
    articles: ['Art. 7 (Auditability)', 'Art. 8 (Observability)'],
    actions: ['Celebrate achievement', 'Set next coverage target', 'Add to compounding metrics'],
    vsmPath: 'S3 → S4',
  },
};

export const AlgedonicPanel: React.FC<AlgedonicPanelProps> = ({ signals: signalsProp, onAskDemerzel, onTriageAction, onSignalCreated }) => {
  const [collapsed, setCollapsed] = useState(false);
  const [filter, setFilter] = useState<'all' | 'pain' | 'pleasure'>('all');
  const [expandedId, setExpandedId] = useState<string | null>(null);
  const [showSuggestions, setShowSuggestions] = useState(false);
  const [showCreateForm, setShowCreateForm] = useState(false);
  const [showGraph, setShowGraph] = useState(false);
  const [localSignals, setLocalSignals] = useState<AlgedonicSignal[]>([]);

  // Demerzel auto-remediation
  const remediation = useRemediation();

  // Create form state
  const [formType, setFormType] = useState<AlgedonicSignalType>('pain');
  const [formSeverity, setFormSeverity] = useState<AlgedonicSeverity>('warning');
  const [formSource, setFormSource] = useState('user');
  const [formDescription, setFormDescription] = useState('');

  const baseSignals = signalsProp && signalsProp.length > 0 ? signalsProp : mockAlgedonicSignals;
  const allSignals = [...localSignals, ...baseSignals];

  const signals = allSignals.filter(
    (s) => filter === 'all' || s.type === filter,
  );

  const painCount = allSignals.filter((s) => s.type === 'pain').length;
  const pleasureCount = allSignals.filter((s) => s.type === 'pleasure').length;
  const activeCount = allSignals.filter((s) => s.status === 'active').length;

  return (
    <div className="prime-radiant__algedonic">
      {/* Header */}
      <div
        className="prime-radiant__algedonic-header"
        onClick={() => setCollapsed(!collapsed)}
      >
        <span className="prime-radiant__algedonic-title">
          Algedonic
          <span className="prime-radiant__algedonic-counts">
            <span style={{ color: '#FF4444' }}>{painCount}P</span>
            {' / '}
            <span style={{ color: '#33CC66' }}>{pleasureCount}G</span>
            {activeCount > 0 && (
              <span className="prime-radiant__algedonic-active">
                {activeCount} active
              </span>
            )}
          </span>
        </span>
        <span className="prime-radiant__algedonic-toggle">
          {collapsed ? '\u25B6' : '\u25BC'}
        </span>
      </div>

      {!collapsed && (
        <>
          {/* Filter bar + New Signal button */}
          <div className="prime-radiant__algedonic-filters">
            {(['all', 'pain', 'pleasure'] as const).map((f) => (
              <button
                key={f}
                className={`prime-radiant__algedonic-filter${filter === f ? ' prime-radiant__algedonic-filter--active' : ''}`}
                onClick={() => setFilter(f)}
                style={{
                  color: f === 'pain' ? '#FF4444' : f === 'pleasure' ? '#33CC66' : '#8b949e',
                }}
              >
                {f === 'all' ? 'All' : f === 'pain' ? 'Pain' : 'Pleasure'}
              </button>
            ))}
            <button
              className={`algedonic-panel__graph-toggle${showGraph ? ' algedonic-panel__graph-toggle--active' : ''}`}
              onClick={() => setShowGraph(v => !v)}
            >
              {showGraph ? '\u25C9' : '\u25CE'} Graph
            </button>
            <button
              className={`algedonic-panel__create-toggle${showCreateForm ? ' algedonic-panel__create-toggle--active' : ''}`}
              onClick={() => setShowCreateForm(v => !v)}
            >
              + New Signal
            </button>
            <button
              className="prime-radiant__algedonic-fixall-btn"
              disabled={remediation.remediating}
              onClick={() => {
                const active = filtered.filter(s => s.status === 'active');
                if (active.length > 0) remediation.remediateAll(active);
              }}
              title="Auto-fix all active signals via Demerzel ACP"
            >
              {remediation.remediating ? 'Fixing...' : `Fix All (${filtered.filter(s => s.status === 'active').length})`}
            </button>
          </div>

          {/* Signal creation form */}
          {showCreateForm && (
            <div className="algedonic-panel__create-form">
              <div className="algedonic-panel__create-row">
                <label className="algedonic-panel__create-label">Type</label>
                <select
                  className="algedonic-panel__create-select"
                  value={formType}
                  onChange={(e) => setFormType(e.target.value as AlgedonicSignalType)}
                >
                  <option value="pain">Pain</option>
                  <option value="pleasure">Pleasure</option>
                </select>
              </div>
              <div className="algedonic-panel__create-row">
                <label className="algedonic-panel__create-label">Severity</label>
                <select
                  className="algedonic-panel__create-select"
                  value={formSeverity}
                  onChange={(e) => setFormSeverity(e.target.value as AlgedonicSeverity)}
                >
                  <option value="emergency">Emergency</option>
                  <option value="warning">Warning</option>
                  <option value="info">Info</option>
                </select>
              </div>
              <div className="algedonic-panel__create-row">
                <label className="algedonic-panel__create-label">Source</label>
                <input
                  className="algedonic-panel__create-input"
                  type="text"
                  value={formSource}
                  onChange={(e) => setFormSource(e.target.value)}
                  placeholder="user"
                />
              </div>
              <div className="algedonic-panel__create-row">
                <label className="algedonic-panel__create-label">Description</label>
                <input
                  className="algedonic-panel__create-input"
                  type="text"
                  value={formDescription}
                  onChange={(e) => setFormDescription(e.target.value)}
                  placeholder="Optional description"
                />
              </div>
              <div className="algedonic-panel__create-actions">
                <button
                  className="algedonic-panel__create-submit"
                  onClick={() => {
                    const signal = createAlgedonicSignal(
                      formType,
                      formSeverity,
                      formSource || 'user',
                      formDescription || undefined,
                    );
                    setLocalSignals(prev => [signal, ...prev]);
                    onSignalCreated?.(signal);
                    // Fire-and-forget POST to governance API
                    fetch('/api/governance/signals', {
                      method: 'POST',
                      headers: { 'Content-Type': 'application/json' },
                      body: JSON.stringify(signal),
                    }).catch(() => { /* swallow — optimistic update already applied */ });
                    // Reset form
                    setFormType('pain');
                    setFormSeverity('warning');
                    setFormSource('user');
                    setFormDescription('');
                    setShowCreateForm(false);
                  }}
                >
                  Submit
                </button>
                <button
                  className="algedonic-panel__create-cancel"
                  onClick={() => setShowCreateForm(false)}
                >
                  Cancel
                </button>
              </div>
            </div>
          )}

          {/* Signal interaction graph */}
          {showGraph && (
            <SignalGraph
              signals={allSignals}
              onNodeClick={(signal) => setExpandedId(expandedId === signal.id ? null : signal.id)}
            />
          )}

          {/* Timeline */}
          <div className="prime-radiant__algedonic-timeline">
            {signals.map((s) => {
              const isExpanded = expandedId === s.id;
              const details = SIGNAL_DETAILS[s.signal];
              return (
                <div key={s.id} className={`prime-radiant__algedonic-signal ${isExpanded ? 'prime-radiant__algedonic-signal--expanded' : ''}`}>
                  {/* Pulse dot */}
                  <span
                    className={`prime-radiant__algedonic-dot${s.status === 'active' ? ' prime-radiant__algedonic-dot--pulse' : ''}`}
                    style={{
                      backgroundColor: s.type === 'pain' ? '#FF4444' : '#33CC66',
                      boxShadow: s.status === 'active'
                        ? `0 0 6px ${s.type === 'pain' ? '#FF4444' : '#33CC66'}`
                        : 'none',
                    }}
                  />

                  {/* Signal info — click to expand */}
                  <div
                    className="prime-radiant__algedonic-info"
                    onClick={() => setExpandedId(isExpanded ? null : s.id)}
                    style={{ cursor: 'pointer' }}
                  >
                    <div className="prime-radiant__algedonic-signal-name">
                      <span style={{ color: SEVERITY_COLOR[s.severity] }}>
                        {s.signal.replace(/_/g, ' ')}
                      </span>
                      <span
                        className="prime-radiant__algedonic-source"
                        style={{ color: SOURCE_COLOR[s.source] ?? '#8b949e' }}
                      >
                        {s.source}
                      </span>
                    </div>
                    {s.description && (
                      <div className="prime-radiant__algedonic-desc">
                        {s.description}
                      </div>
                    )}
                    <div className="prime-radiant__algedonic-meta">
                      <span className="prime-radiant__algedonic-time">
                        {timeAgo(s.timestamp)}
                      </span>
                      <span
                        className="prime-radiant__algedonic-status"
                        style={{
                          color: s.status === 'active'
                            ? '#FF4444'
                            : s.status === 'acknowledged'
                              ? '#FFB300'
                              : '#484f58',
                        }}
                      >
                        {STATUS_ICON[s.status]} {s.status}
                      </span>
                      <span
                        className="prime-radiant__algedonic-severity"
                        style={{ color: SEVERITY_COLOR[s.severity] }}
                      >
                        {s.severity}
                      </span>
                    </div>

                    {/* Expanded detail popover */}
                    {isExpanded && details && (
                      <div className="prime-radiant__algedonic-detail">
                        <div className="prime-radiant__algedonic-detail-section">
                          <span className="prime-radiant__algedonic-detail-label">Constitutional Basis</span>
                          {details.articles.map((a) => (
                            <span key={a} className="prime-radiant__algedonic-detail-tag">{a}</span>
                          ))}
                        </div>
                        <div className="prime-radiant__algedonic-detail-section">
                          <span className="prime-radiant__algedonic-detail-label">VSM Path</span>
                          <span className="prime-radiant__algedonic-detail-vsm">{details.vsmPath}</span>
                        </div>
                        <div className="prime-radiant__algedonic-detail-section">
                          <span className="prime-radiant__algedonic-detail-label">Recommended Actions</span>
                          {details.actions.map((a, i) => (
                            <div key={i} className="prime-radiant__algedonic-detail-action">
                              <span>{'\u2192'} {a}</span>
                              {onTriageAction && (
                                <button
                                  className="prime-radiant__algedonic-triage-btn"
                                  onClick={(e) => {
                                    e.stopPropagation();
                                    onTriageAction(a, s.signal, s.source);
                                  }}
                                  title="Send to triage inbox"
                                >
                                  {'📥'}
                                </button>
                              )}
                            </div>
                          ))}
                        </div>
                        <div className="prime-radiant__algedonic-detail-ts">
                          {new Date(s.timestamp).toLocaleString()}
                        </div>
                        {(() => {
                          const result = remediation.getResult(s.id);
                          return (
                            <div className="prime-radiant__algedonic-actions">
                              <button
                                className="prime-radiant__algedonic-autofix-btn"
                                disabled={remediation.remediating || result?.status === 'running'}
                                onClick={(e) => {
                                  e.stopPropagation();
                                  remediation.remediate(s);
                                }}
                              >
                                {result?.status === 'running' ? 'Fixing...' : result?.status === 'success' ? 'Fixed' : result?.status === 'escalated' ? 'Escalated' : 'Auto-fix'}
                              </button>
                              {onAskDemerzel && (
                                <button
                                  className="prime-radiant__algedonic-address-btn"
                                  onClick={(e) => {
                                    e.stopPropagation();
                                    onAskDemerzel(`How should I address the "${s.signal.replace(/_/g, ' ')}" algedonic signal from ${s.source}? ${s.description ?? ''}`);
                                  }}
                                >
                                  Ask
                                </button>
                              )}
                              {result && result.status !== 'running' && (
                                <div className={`prime-radiant__algedonic-result prime-radiant__algedonic-result--${result.status}`}>
                                  <span className="prime-radiant__algedonic-result-badge">
                                    {result.status === 'success' ? '✓' : result.status === 'escalated' ? '⚠' : '✗'} {result.risk} risk → {result.agentUsed}
                                    {result.durationMs ? ` (${result.durationMs}ms)` : ''}
                                  </span>
                                  {result.response && (
                                    <pre className="prime-radiant__algedonic-result-text">{result.response}</pre>
                                  )}
                                  {result.error && (
                                    <pre className="prime-radiant__algedonic-result-text prime-radiant__algedonic-result-text--error">{result.error}</pre>
                                  )}
                                </div>
                              )}
                            </div>
                          );
                        })()}
                      </div>
                    )}
                    {isExpanded && !details && (
                      <div className="prime-radiant__algedonic-detail">
                        <div className="prime-radiant__algedonic-detail-section">
                          <span className="prime-radiant__algedonic-detail-label">Full Timestamp</span>
                          <span className="prime-radiant__algedonic-detail-ts">{new Date(s.timestamp).toLocaleString()}</span>
                        </div>
                        <div className="prime-radiant__algedonic-detail-section">
                          <span className="prime-radiant__algedonic-detail-label">Source System</span>
                          <span style={{ color: SOURCE_COLOR[s.source] ?? '#8b949e' }}>{s.source.toUpperCase()}</span>
                        </div>
                        {(() => {
                          const result = remediation.getResult(s.id);
                          return (
                            <div className="prime-radiant__algedonic-actions">
                              <button
                                className="prime-radiant__algedonic-autofix-btn"
                                disabled={remediation.remediating || result?.status === 'running'}
                                onClick={(e) => {
                                  e.stopPropagation();
                                  remediation.remediate(s);
                                }}
                              >
                                {result?.status === 'running' ? 'Fixing...' : result?.status === 'success' ? 'Fixed' : result?.status === 'escalated' ? 'Escalated' : 'Auto-fix'}
                              </button>
                              {onAskDemerzel && (
                                <button
                                  className="prime-radiant__algedonic-address-btn"
                                  onClick={(e) => {
                                    e.stopPropagation();
                                    onAskDemerzel(`How should I address the "${s.signal.replace(/_/g, ' ')}" algedonic signal from ${s.source}? ${s.description ?? ''}`);
                                  }}
                                >
                                  Ask
                                </button>
                              )}
                              {result && result.status !== 'running' && (
                                <div className={`prime-radiant__algedonic-result prime-radiant__algedonic-result--${result.status}`}>
                                  <span className="prime-radiant__algedonic-result-badge">
                                    {result.status === 'success' ? '✓' : result.status === 'escalated' ? '⚠' : '✗'} {result.risk} risk → {result.agentUsed}
                                    {result.durationMs ? ` (${result.durationMs}ms)` : ''}
                                  </span>
                                  {result.response && (
                                    <pre className="prime-radiant__algedonic-result-text">{result.response}</pre>
                                  )}
                                  {result.error && (
                                    <pre className="prime-radiant__algedonic-result-text prime-radiant__algedonic-result-text--error">{result.error}</pre>
                                  )}
                                </div>
                              )}
                            </div>
                          );
                        })()}
                      </div>
                    )}
                  </div>
                </div>
              );
            })}
          </div>

          {/* Missing signals suggestions */}
          <div className="prime-radiant__algedonic-suggestions-header">
            <button
              className="prime-radiant__algedonic-suggest-btn"
              onClick={() => setShowSuggestions(v => !v)}
            >
              {showSuggestions ? '\u25BC' : '\u25B6'} Missing Signals ({getMissingSuggestions(allSignals).length})
            </button>
          </div>
          {showSuggestions && (
            <div className="prime-radiant__algedonic-suggestions">
              {getMissingSuggestions(allSignals).map((s) => (
                <div key={s.signal} className="prime-radiant__algedonic-suggestion">
                  <span
                    className="prime-radiant__algedonic-dot"
                    style={{
                      backgroundColor: s.type === 'pain' ? 'rgba(255,68,68,0.3)' : 'rgba(51,204,102,0.3)',
                      border: `1px dashed ${s.type === 'pain' ? '#FF4444' : '#33CC66'}`,
                    }}
                  />
                  <div className="prime-radiant__algedonic-info">
                    <div className="prime-radiant__algedonic-signal-name">
                      <span style={{ color: SEVERITY_COLOR[s.severity], opacity: 0.7 }}>
                        {s.signal.replace(/_/g, ' ')}
                      </span>
                      <span
                        className="prime-radiant__algedonic-source"
                        style={{ color: SOURCE_COLOR[s.source] ?? '#8b949e' }}
                      >
                        {s.source}
                      </span>
                    </div>
                    <div className="prime-radiant__algedonic-desc" style={{ fontStyle: 'italic' }}>
                      {s.reason}
                    </div>
                    <div className="prime-radiant__algedonic-meta">
                      <span className="prime-radiant__algedonic-severity" style={{ color: SEVERITY_COLOR[s.severity] }}>
                        {s.severity}
                      </span>
                      <span style={{ color: '#484f58', fontSize: '9px' }}>
                        not tracked
                      </span>
                    </div>
                  </div>
                </div>
              ))}
            </div>
          )}
        </>
      )}
    </div>
  );
};
