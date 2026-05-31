// src/components/PrimeRadiant/DetailPanel.tsx
// React side panel showing selected governance node details

import React, { useState, useCallback } from 'react';
import type { GovernanceNode, GovernanceNodeType, FileTreeNode, NodeAugmentation, HexavalentTruth } from './types';
import { HEALTH_COLORS, NODE_COLORS, HEXAVALENT_COLORS } from './types';
import { getHealthStatus } from './DataLoader';
import type { GraphIndex } from './DataLoader';

// ---------------------------------------------------------------------------
// Dashboard channel sections — AI annotations / test coverage / algedonic
// Collapsed <details> by default. Each gracefully handles missing
// augmentation: shows "no data" inline when the channel is undefined.
// ---------------------------------------------------------------------------

const TRUTH_ORDER: HexavalentTruth[] = ['T', 'P', 'U', 'D', 'F', 'C'];

const summaryStyle: React.CSSProperties = {
  cursor: 'pointer',
  fontSize: 11,
  color: '#8b949e',
  textTransform: 'uppercase',
  letterSpacing: 0.4,
  padding: '6px 0',
  userSelect: 'none',
};

const sectionStyle: React.CSSProperties = {
  borderTop: '1px solid #21262d',
  paddingTop: 4,
  marginTop: 8,
};

const AnnotationsSection: React.FC<{ augmentation?: NodeAugmentation }> = ({ augmentation }) => {
  const ann = augmentation?.annotations;
  const total = ann?.total ?? 0;
  return (
    <details style={sectionStyle}>
      <summary style={summaryStyle}>AI Annotations ({total})</summary>
      <div style={{ padding: '6px 0 10px' }}>
        {!ann ? (
          <div style={{ fontSize: 11, color: '#484f58' }}>No annotations for this node.</div>
        ) : (
          <>
            <div style={{ display: 'flex', gap: 6, flexWrap: 'wrap', marginBottom: 8 }}>
              {TRUTH_ORDER.map(tv => {
                const count = ann.by_truth_value[tv] ?? 0;
                const color = HEXAVALENT_COLORS[tv];
                const active = count > 0;
                return (
                  <span
                    key={tv}
                    title={`${tv}: ${count}`}
                    style={{
                      display: 'inline-flex',
                      alignItems: 'center',
                      gap: 4,
                      fontSize: 10,
                      fontFamily: 'monospace',
                      background: active ? `${color}22` : 'transparent',
                      color: active ? color : '#484f58',
                      border: `1px solid ${active ? `${color}66` : '#21262d'}`,
                      borderRadius: 4,
                      padding: '2px 6px',
                      opacity: active ? 1 : 0.5,
                    }}
                  >
                    <span style={{ fontWeight: 'bold' }}>{tv}</span>
                    <span>{count}</span>
                  </span>
                );
              })}
            </div>
            <ul style={{ listStyle: 'none', padding: 0, margin: 0 }}>
              {ann.recent.map((a, i) => (
                <li
                  key={`${a.line_start}-${i}`}
                  style={{
                    fontSize: 11,
                    padding: '4px 6px',
                    marginBottom: 3,
                    background: '#0d1117',
                    border: `1px solid ${HEXAVALENT_COLORS[a.truth_value]}33`,
                    borderLeft: `3px solid ${HEXAVALENT_COLORS[a.truth_value]}`,
                    borderRadius: 3,
                  }}
                >
                  <div style={{ display: 'flex', gap: 6, fontSize: 9, color: '#8b949e', marginBottom: 2 }}>
                    <span style={{ color: HEXAVALENT_COLORS[a.truth_value], fontWeight: 'bold' }}>{a.truth_value}</span>
                    <span>{a.kind}</span>
                    <span>conf:{a.certainty}</span>
                    <span style={{ marginLeft: 'auto' }}>L{a.line_start}{a.line_start !== a.line_end ? `-${a.line_end}` : ''}</span>
                  </div>
                  <div style={{ color: '#e6edf3' }}>{a.claim}</div>
                </li>
              ))}
            </ul>
          </>
        )}
      </div>
    </details>
  );
};

const TestCoverageSection: React.FC<{ augmentation?: NodeAugmentation }> = ({ augmentation }) => {
  const gap = augmentation?.testGap;
  return (
    <details style={sectionStyle}>
      <summary style={summaryStyle}>Test Coverage</summary>
      <div style={{ padding: '6px 0 10px' }}>
        {!gap ? (
          <div style={{ fontSize: 11, color: '#484f58' }}>No test-gap data for this node.</div>
        ) : (() => {
          const riskPct = Math.round(gap.risk_score * 100);
          const barColor = riskPct < 33 ? '#33CC66' : riskPct < 66 ? '#FFB300' : '#FF4444';
          return (
            <>
              <div style={{
                height: 6,
                background: '#161b22',
                borderRadius: 3,
                overflow: 'hidden',
                marginBottom: 4,
              }}>
                <div style={{
                  width: `${riskPct}%`,
                  height: '100%',
                  background: barColor,
                  transition: 'width 0.3s ease',
                }} />
              </div>
              <div style={{ display: 'flex', gap: 12, fontSize: 11, color: '#8b949e' }}>
                <span>risk <span style={{ color: barColor, fontWeight: 600 }}>{riskPct}%</span></span>
                <span>churn <span style={{ color: '#e6edf3' }}>{gap.churn}</span></span>
                <span>cx <span style={{ color: '#e6edf3' }}>{gap.complexity}</span></span>
              </div>
            </>
          );
        })()}
      </div>
    </details>
  );
};

function formatAge(ts: string): string {
  if (!ts) return '';
  const ms = Date.now() - new Date(ts).getTime();
  if (!isFinite(ms) || ms < 0) return '';
  const s = Math.floor(ms / 1000);
  if (s < 60) return `${s}s ago`;
  const m = Math.floor(s / 60);
  if (m < 60) return `${m}m ago`;
  const h = Math.floor(m / 60);
  if (h < 24) return `${h}h ago`;
  return `${Math.floor(h / 24)}d ago`;
}

const RecentAlgedonicSection: React.FC<{ augmentation?: NodeAugmentation }> = ({ augmentation }) => {
  const recent = augmentation?.algedonic?.recent ?? [];
  return (
    <details style={sectionStyle}>
      <summary style={summaryStyle}>Recent Algedonic ({recent.length})</summary>
      <div style={{ padding: '6px 0 10px' }}>
        {recent.length === 0 ? (
          <div style={{ fontSize: 11, color: '#484f58' }}>No recent signals.</div>
        ) : (
          <ul style={{ listStyle: 'none', padding: 0, margin: 0 }}>
            {recent.map(s => {
              const sevColor =
                s.severity === 'emergency' ? '#FF4444' :
                s.severity === 'warning' ? '#FFB300' :
                s.type === 'pleasure' ? '#FFD700' : '#8b949e';
              return (
                <li
                  key={s.id || `${s.signal}-${s.timestamp}`}
                  style={{
                    fontSize: 11,
                    padding: '4px 6px',
                    marginBottom: 3,
                    background: '#0d1117',
                    borderLeft: `3px solid ${sevColor}`,
                    borderRadius: 3,
                    display: 'flex',
                    gap: 6,
                    alignItems: 'center',
                  }}
                >
                  <span style={{ color: sevColor, fontSize: 9, fontWeight: 'bold', textTransform: 'uppercase' }}>{s.severity}</span>
                  <span style={{ color: '#e6edf3', flex: 1, overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>{s.signal}</span>
                  <span style={{ color: '#484f58', fontSize: 10 }}>{formatAge(s.timestamp)}</span>
                </li>
              );
            })}
          </ul>
        )}
      </div>
    </details>
  );
};

// ---------------------------------------------------------------------------
// Props
// ---------------------------------------------------------------------------
export interface DetailPanelProps {
  node: GovernanceNode | null;
  graphIndex: GraphIndex | null;
  onClose: () => void;
  onNavigate: (nodeId: string) => void;
}

// ---------------------------------------------------------------------------
// Node type display names
// ---------------------------------------------------------------------------
const TYPE_LABELS: Record<GovernanceNodeType, string> = {
  constitution: 'Constitution',
  policy: 'Policy',
  persona: 'Persona',
  pipeline: 'Pipeline',
  department: 'Department',
  schema: 'Schema',
  test: 'Test Suite',
  ixql: 'IxQL File',
};

const TYPE_SHAPES: Record<GovernanceNodeType, string> = {
  constitution: 'Core Cluster',
  policy: 'Diamond Cloud',
  persona: 'Sphere Cloud',
  pipeline: 'Ring Stream',
  department: 'Nebula Cluster',
  schema: 'Cube Matrix',
  test: 'Diamond Cloud',
  ixql: 'Helix Stream',
};

// ---------------------------------------------------------------------------
// Governance shortcut definitions
// ---------------------------------------------------------------------------
const GOVERNANCE_SHORTCUTS: Array<{
  label: string;
  type: GovernanceNodeType;
  color: string;
}> = [
  { label: 'Constitutions', type: 'constitution', color: NODE_COLORS.constitution },
  { label: 'Policies', type: 'policy', color: NODE_COLORS.policy },
  { label: 'Personas', type: 'persona', color: NODE_COLORS.persona },
  { label: 'Schemas', type: 'schema', color: NODE_COLORS.schema },
];

// ---------------------------------------------------------------------------
// Breadcrumb — shows Root > Type > Node Name
// ---------------------------------------------------------------------------
const Breadcrumb: React.FC<{
  node: GovernanceNode;
  onClose: () => void;
}> = ({ node, onClose }) => {
  const typeLabel = TYPE_LABELS[node.type];

  return (
    <nav className="prime-radiant__breadcrumb" aria-label="Breadcrumb">
      <span
        className="prime-radiant__breadcrumb-item"
        onClick={onClose}
        role="button"
        tabIndex={0}
        onKeyDown={(e) => { if (e.key === 'Enter') onClose(); }}
      >
        Governance
      </span>
      <span className="prime-radiant__breadcrumb-separator">&gt;</span>
      <span className="prime-radiant__breadcrumb-item">
        {typeLabel}
      </span>
      <span className="prime-radiant__breadcrumb-separator">&gt;</span>
      <span className="prime-radiant__breadcrumb-item prime-radiant__breadcrumb-item--active">
        {node.name}
      </span>
    </nav>
  );
};

// ---------------------------------------------------------------------------
// ShortcutChips — quick-jump to governance node types
// ---------------------------------------------------------------------------
const ShortcutChips: React.FC<{
  graphIndex: GraphIndex | null;
  onNavigate: (nodeId: string) => void;
  activeType: GovernanceNodeType;
}> = ({ graphIndex, onNavigate, activeType }) => {
  const handleClick = useCallback((type: GovernanceNodeType) => {
    if (!graphIndex) return;
    // Find the first node of the given type
    for (const [id, node] of graphIndex.nodeMap) {
      if (node.type === type) {
        onNavigate(id);
        return;
      }
    }
  }, [graphIndex, onNavigate]);

  return (
    <div className="prime-radiant__shortcuts">
      {GOVERNANCE_SHORTCUTS.map((shortcut) => (
        <button
          key={shortcut.type}
          className={`prime-radiant__shortcut-chip${shortcut.type === activeType ? ' prime-radiant__shortcut-chip--active' : ''}`}
          onClick={() => handleClick(shortcut.type)}
          title={`Jump to ${shortcut.label}`}
        >
          <span
            className="prime-radiant__shortcut-dot"
            style={{ backgroundColor: shortcut.color }}
          />
          {shortcut.label}
        </button>
      ))}
    </div>
  );
};

// ---------------------------------------------------------------------------
// File extension icons
// ---------------------------------------------------------------------------
const EXT_ICONS: Record<string, string> = {
  md: '\u{1F4C4}',
  yaml: '\u2699\uFE0F',
  yml: '\u2699\uFE0F',
  json: '\u{1F4CB}',
  ts: '\u{1F4C4}',
  js: '\u{1F4C4}',
};

function fileIcon(node: FileTreeNode): string {
  if (node.type === 'directory') return '';
  // Special case for test files
  if (node.name.endsWith('.test.md') || node.name.includes('-cases.md')) return '\u{1F9EA}';
  return (node.extension && EXT_ICONS[node.extension]) || '\u{1F4C4}';
}

// ---------------------------------------------------------------------------
// FileTreeItem — recursive collapsible tree node
// ---------------------------------------------------------------------------
const FileTreeItem: React.FC<{
  node: FileTreeNode;
  depth: number;
  expanded: Set<string>;
  onToggle: (path: string) => void;
}> = ({ node, depth, expanded, onToggle }) => {
  const isDir = node.type === 'directory';
  const isOpen = expanded.has(node.path);
  const indent = depth * 16;

  return (
    <>
      <div
        className={`prime-radiant__file-tree-item ${isDir ? 'prime-radiant__file-tree-item--dir' : ''}`}
        style={{ paddingLeft: `${indent}px` }}
        onClick={isDir ? () => onToggle(node.path) : undefined}
      >
        {isDir ? (
          <span className="prime-radiant__file-tree-toggle">
            {isOpen ? '\u25BC' : '\u25B6'}
          </span>
        ) : (
          <span className="prime-radiant__file-tree-icon">{fileIcon(node)}</span>
        )}
        <span className="prime-radiant__file-tree-name">{node.name}</span>
      </div>
      {isDir && isOpen && node.children?.map(child => (
        <FileTreeItem
          key={child.path}
          node={child}
          depth={depth + 1}
          expanded={expanded}
          onToggle={onToggle}
        />
      ))}
    </>
  );
};

// ---------------------------------------------------------------------------
// Component
// ---------------------------------------------------------------------------
export const DetailPanel: React.FC<DetailPanelProps> = ({
  node,
  graphIndex,
  onClose,
  onNavigate,
}) => {
  const isOpen = node !== null;

  // Gather connections
  const connections: Array<{
    nodeId: string;
    name: string;
    type: GovernanceNodeType;
    color: string;
    direction: 'in' | 'out';
    edgeType: string;
  }> = [];

  if (node && graphIndex) {
    const outEdges = graphIndex.outEdges.get(node.id) ?? [];
    const inEdges = graphIndex.inEdges.get(node.id) ?? [];

    for (const edge of outEdges) {
      const target = graphIndex.nodeMap.get(edge.target);
      if (target) {
        connections.push({
          nodeId: target.id,
          name: target.name,
          type: target.type,
          color: target.color,
          direction: 'out',
          edgeType: edge.type,
        });
      }
    }

    for (const edge of inEdges) {
      const source = graphIndex.nodeMap.get(edge.source);
      if (source) {
        connections.push({
          nodeId: source.id,
          name: source.name,
          type: source.type,
          color: source.color,
          direction: 'in',
          edgeType: edge.type,
        });
      }
    }
  }

  // File tree expansion state — first-level directories expanded by default
  const [expandedPaths, setExpandedPaths] = useState<Set<string>>(new Set());
  const [treeInitialized, setTreeInitialized] = useState<string | null>(null);

  // Reset expanded state when node changes, expand first-level dirs
  if (node && node.id !== treeInitialized) {
    const initial = new Set<string>();
    if (node.fileTree) {
      for (const item of node.fileTree) {
        if (item.type === 'directory') initial.add(item.path);
      }
    }
    setExpandedPaths(initial);
    setTreeInitialized(node.id);
  }

  const handleToggle = useCallback((path: string) => {
    setExpandedPaths(prev => {
      const next = new Set(prev);
      if (next.has(path)) next.delete(path);
      else next.add(path);
      return next;
    });
  }, []);

  const health = node?.health;
  const healthStatus = health ? getHealthStatus(health.resilienceScore) : null;

  return (
    <div className={`prime-radiant__detail ${isOpen ? 'prime-radiant__detail--open' : ''}`}>
      {node && (
        <>
          {/* Header */}
          <div className="prime-radiant__detail-header">
            <h3
              className="prime-radiant__detail-title"
              style={{ color: node.color }}
            >
              {node.name}
            </h3>
            <button
              className="prime-radiant__detail-close"
              onClick={onClose}
              aria-label="Close detail panel"
            >
              x
            </button>
          </div>

          {/* Breadcrumb navigation */}
          <Breadcrumb node={node} onClose={onClose} />

          {/* Governance shortcut chips */}
          <ShortcutChips
            graphIndex={graphIndex}
            onNavigate={onNavigate}
            activeType={node.type}
          />

          {/* Body */}
          <div className="prime-radiant__detail-body">
            {/* Type badge */}
            <div className="prime-radiant__detail-section">
              <span className="prime-radiant__detail-label">Type</span>
              <span>
                <span
                  className="prime-radiant__detail-badge"
                  style={{
                    backgroundColor: `${node.color}22`,
                    color: node.color,
                    border: `1px solid ${node.color}44`,
                  }}
                >
                  {TYPE_SHAPES[node.type]} — {TYPE_LABELS[node.type]}
                </span>
              </span>
            </div>

            {/* Description */}
            <div className="prime-radiant__detail-section">
              <span className="prime-radiant__detail-label">Description</span>
              <span className="prime-radiant__detail-value">{node.description}</span>
            </div>

            {/* Repo */}
            {node.repo && (
              <div className="prime-radiant__detail-section">
                <span className="prime-radiant__detail-label">Repository</span>
                <span className="prime-radiant__detail-value">{node.repo}</span>
              </div>
            )}

            {/* Version */}
            {node.version && (
              <div className="prime-radiant__detail-section">
                <span className="prime-radiant__detail-label">Version</span>
                <span className="prime-radiant__detail-value">v{node.version}</span>
              </div>
            )}

            {/* Health metrics */}
            {health && healthStatus && (
              <>
                <div className="prime-radiant__detail-section">
                  <span className="prime-radiant__detail-label">Resilience Score</span>
                  <div className="prime-radiant__detail-health-bar">
                    <div
                      className="prime-radiant__detail-health-fill"
                      style={{
                        width: `${health.resilienceScore * 100}%`,
                        backgroundColor: HEALTH_COLORS[healthStatus],
                      }}
                    />
                  </div>
                  <span className="prime-radiant__detail-value" style={{ fontSize: '11px', color: '#8b949e' }}>
                    {(health.resilienceScore * 100).toFixed(0)}% — {healthStatus}
                  </span>
                </div>

                <div style={{ display: 'flex', gap: '16px' }}>
                  <div className="prime-radiant__detail-section" style={{ flex: 1 }}>
                    <span className="prime-radiant__detail-label">ERGOL</span>
                    <span className="prime-radiant__detail-value" style={{ color: '#FFD700' }}>
                      {health.ergolCount}
                    </span>
                  </div>
                  <div className="prime-radiant__detail-section" style={{ flex: 1 }}>
                    <span className="prime-radiant__detail-label">LOLLI</span>
                    <span
                      className="prime-radiant__detail-value"
                      style={{ color: health.lolliCount > 0 ? '#FF4444' : '#8b949e' }}
                    >
                      {health.lolliCount}
                    </span>
                  </div>
                </div>
              </>
            )}

            {/* Connections */}
            {connections.length > 0 && (
              <div className="prime-radiant__detail-section">
                <span className="prime-radiant__detail-label">
                  Connections ({connections.length})
                </span>
                <ul className="prime-radiant__detail-connections">
                  {connections.map((conn) => (
                    <li
                      key={`${conn.direction}-${conn.nodeId}`}
                      className="prime-radiant__detail-connection"
                      onClick={() => onNavigate(conn.nodeId)}
                    >
                      <span
                        className="prime-radiant__detail-connection-dot"
                        style={{ backgroundColor: conn.color }}
                      />
                      <span>
                        {conn.direction === 'out' ? '\u2192' : '\u2190'}{' '}
                        {conn.name}
                      </span>
                      <span style={{ marginLeft: 'auto', fontSize: '10px', color: '#484f58' }}>
                        {conn.edgeType}
                      </span>
                    </li>
                  ))}
                </ul>
              </div>
            )}

            {/* File tree / Contents */}
            {node.fileTree && node.fileTree.length > 0 && (
              <div className="prime-radiant__detail-section">
                <span className="prime-radiant__detail-label">
                  Contents ({node.fileTree.length} {node.fileTree.length === 1 ? 'item' : 'items'})
                </span>
                <div className="prime-radiant__file-tree">
                  {node.fileTree.map(item => (
                    <FileTreeItem
                      key={item.path}
                      node={item}
                      depth={0}
                      expanded={expandedPaths}
                      onToggle={handleToggle}
                    />
                  ))}
                </div>
              </div>
            )}

            {/* Dashboard channels — AI annotations / test coverage / algedonic */}
            <AnnotationsSection augmentation={node.augmentation} />
            <TestCoverageSection augmentation={node.augmentation} />
            <RecentAlgedonicSection augmentation={node.augmentation} />
          </div>
        </>
      )}
    </div>
  );
};
