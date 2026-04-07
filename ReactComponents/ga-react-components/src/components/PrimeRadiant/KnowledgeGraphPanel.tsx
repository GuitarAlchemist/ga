// src/components/PrimeRadiant/KnowledgeGraphPanel.tsx
// Visualizes the TARS knowledge graph with governance overlay — tree view + connections + search

import React, { useEffect, useState, useCallback, useMemo, useRef } from 'react';
import type { GovernanceNode, GovernanceNodeType, GovernanceEdge, GovernanceGraph } from './types';
import { HEALTH_STATUS_COLORS, NODE_COLORS } from './types';
import { buildGraphIndex, deriveGovernanceHealthStatus, loadGovernanceDataAsync } from './DataLoader';
import type { GraphIndex } from './DataLoader';

// ---------------------------------------------------------------------------
// Types
// ---------------------------------------------------------------------------
interface KnowledgeNode {
  id: string;
  name: string;
  type: GovernanceNodeType;
  description: string;
  healthStatus: string;
  healthColor: string;
  lastUpdated?: string;
  children: KnowledgeNode[];
  depth: number;
}

interface Connection {
  nodeId: string;
  name: string;
  type: GovernanceNodeType;
  edgeType: string;
  direction: 'upstream' | 'downstream';
  color: string;
}

interface KnowledgeStats {
  totalNodes: number;
  coveragePct: number;
  gaps: number;
  contradictions: number;
}

// Knowledge-relevant node types for filtering
const KNOWLEDGE_TYPES: Set<GovernanceNodeType> = new Set([
  'department', 'schema', 'test', 'pipeline',
]);

// All types we display in the tree (departments at top, rest nested)
const TREE_ROOT_TYPES: Set<GovernanceNodeType> = new Set(['department']);

const TYPE_LABELS: Record<GovernanceNodeType, string> = {
  constitution: 'Constitution',
  policy: 'Policy',
  persona: 'Persona',
  pipeline: 'Pipeline',
  department: 'Department',
  schema: 'Schema',
  test: 'Test Suite',
  ixql: 'IxQL',
};

const TYPE_BADGE_COLORS: Record<GovernanceNodeType, string> = {
  constitution: '#8A8A9A',
  department: '#4FC3F7',
  policy: '#FFD700',
  persona: '#73d13d',
  pipeline: '#c084fc',
  schema: '#34d399',
  test: '#f97316',
  ixql: '#ff6b9d',
};

// ---------------------------------------------------------------------------
// API helpers
// ---------------------------------------------------------------------------
function getApiBase(): string {
  const envBase = typeof import.meta !== 'undefined'
    ? (import.meta as { env?: Record<string, string> }).env?.VITE_API_BASE_URL
    : undefined;
  return envBase || (typeof window !== 'undefined' ? window.location.origin : 'http://localhost:5232');
}

// ---------------------------------------------------------------------------
// Build knowledge tree from flat governance nodes
// ---------------------------------------------------------------------------
function buildKnowledgeTree(
  graph: GovernanceGraph,
  graphIndex: GraphIndex,
): KnowledgeNode[] {
  const knowledgeNodes = graph.nodes.filter(n => KNOWLEDGE_TYPES.has(n.type));

  // Group: departments are roots, others nest under their connected department
  const departments = knowledgeNodes.filter(n => n.type === 'department');
  const others = knowledgeNodes.filter(n => n.type !== 'department');

  // Map child nodes to their parent department via edges
  const departmentChildren = new Map<string, GovernanceNode[]>();
  for (const dept of departments) {
    departmentChildren.set(dept.id, []);
  }

  const orphans: GovernanceNode[] = [];

  for (const node of others) {
    const inEdges = graphIndex.inEdges.get(node.id) ?? [];
    const outEdges = graphIndex.outEdges.get(node.id) ?? [];
    let parentDept: string | null = null;

    // Check if any connected node is a department
    for (const edge of [...inEdges, ...outEdges]) {
      const otherId = edge.source === node.id ? edge.target : edge.source;
      const other = graphIndex.nodeMap.get(otherId);
      if (other && other.type === 'department') {
        parentDept = otherId;
        break;
      }
    }

    if (parentDept && departmentChildren.has(parentDept)) {
      departmentChildren.get(parentDept)!.push(node);
    } else {
      orphans.push(node);
    }
  }

  function toKnowledgeNode(node: GovernanceNode, depth: number, children: GovernanceNode[]): KnowledgeNode {
    const healthStatus = deriveGovernanceHealthStatus(node);
    return {
      id: node.id,
      name: node.name,
      type: node.type,
      description: node.description,
      healthStatus,
      healthColor: HEALTH_STATUS_COLORS[healthStatus],
      lastUpdated: node.version ? `v${node.version}` : undefined,
      depth,
      children: children.map(c => toKnowledgeNode(c, depth + 1, [])),
    };
  }

  const tree: KnowledgeNode[] = departments.map(dept =>
    toKnowledgeNode(dept, 0, departmentChildren.get(dept.id) ?? []),
  );

  // Add orphans as top-level nodes
  for (const orphan of orphans) {
    tree.push(toKnowledgeNode(orphan, 0, []));
  }

  return tree;
}

// ---------------------------------------------------------------------------
// Compute stats
// ---------------------------------------------------------------------------
function computeStats(graph: GovernanceGraph, graphIndex: GraphIndex): KnowledgeStats {
  const knowledgeNodes = graph.nodes.filter(n => KNOWLEDGE_TYPES.has(n.type));
  const totalNodes = knowledgeNodes.length;

  // Nodes with at least one test connection
  let withTests = 0;
  let contradictions = 0;

  for (const node of knowledgeNodes) {
    const connected = graphIndex.connectedNodes.get(node.id);
    if (connected) {
      const hasTest = Array.from(connected).some(cId => {
        const cn = graphIndex.nodeMap.get(cId);
        return cn?.type === 'test';
      });
      if (hasTest) withTests++;
    }

    const healthStatus = deriveGovernanceHealthStatus(node);
    if (healthStatus === 'contradictory') contradictions++;
  }

  const coveragePct = totalNodes > 0 ? Math.round((withTests / totalNodes) * 100) : 0;
  const gaps = totalNodes - withTests;

  return { totalNodes, coveragePct, gaps, contradictions };
}

// ---------------------------------------------------------------------------
// Get connections for a selected node
// ---------------------------------------------------------------------------
function getConnections(nodeId: string, graphIndex: GraphIndex): Connection[] {
  const connections: Connection[] = [];
  const outEdges = graphIndex.outEdges.get(nodeId) ?? [];
  const inEdges = graphIndex.inEdges.get(nodeId) ?? [];

  for (const edge of outEdges) {
    const target = graphIndex.nodeMap.get(edge.target);
    if (target) {
      connections.push({
        nodeId: target.id,
        name: target.name,
        type: target.type,
        edgeType: edge.type,
        direction: 'downstream',
        color: HEALTH_STATUS_COLORS[deriveGovernanceHealthStatus(target)],
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
        edgeType: edge.type,
        direction: 'upstream',
        color: HEALTH_STATUS_COLORS[deriveGovernanceHealthStatus(source)],
      });
    }
  }

  return connections;
}

// ---------------------------------------------------------------------------
// Edge type display labels
// ---------------------------------------------------------------------------
const EDGE_LABELS: Record<string, string> = {
  'constitutional-hierarchy': 'governs',
  'policy-persona': 'implements',
  'pipeline-flow': 'flows to',
  'cross-repo': 'cross-repo',
  'contains': 'contains',
  'lolli': 'dead ref',
};

// ---------------------------------------------------------------------------
// Sub-components
// ---------------------------------------------------------------------------

/** Stats bar at the top */
const StatsBar: React.FC<{ stats: KnowledgeStats; isDemo: boolean }> = ({ stats, isDemo }) => (
  <div className="knowledge-graph__stats-bar">
    <div className="knowledge-graph__stat">
      <span className="knowledge-graph__stat-value">{stats.totalNodes}</span>
      <span className="knowledge-graph__stat-label">Nodes</span>
    </div>
    <div className="knowledge-graph__stat">
      <span className="knowledge-graph__stat-value" style={{ color: '#33CC66' }}>
        {stats.coveragePct}%
      </span>
      <span className="knowledge-graph__stat-label">Coverage</span>
    </div>
    <div className="knowledge-graph__stat">
      <span
        className="knowledge-graph__stat-value"
        style={{ color: stats.gaps > 0 ? '#FFB300' : '#8b949e' }}
      >
        {stats.gaps}
      </span>
      <span className="knowledge-graph__stat-label">Gaps</span>
    </div>
    <div className="knowledge-graph__stat">
      <span
        className="knowledge-graph__stat-value"
        style={{ color: stats.contradictions > 0 ? '#FF44FF' : '#8b949e' }}
      >
        {stats.contradictions}
      </span>
      <span className="knowledge-graph__stat-label">Contradictions</span>
    </div>
    {isDemo && (
      <div className="knowledge-graph__stat">
        <span
          style={{
            display: 'inline-block',
            width: 8,
            height: 8,
            borderRadius: '50%',
            backgroundColor: '#eab308',
            boxShadow: '0 0 4px rgba(234,179,8,0.6)',
          }}
        />
        <span className="knowledge-graph__stat-label" style={{ color: '#eab308' }}>Demo</span>
      </div>
    )}
  </div>
);

/** Search input with debounce */
const SearchInput: React.FC<{
  value: string;
  onChange: (v: string) => void;
}> = ({ value, onChange }) => (
  <div className="knowledge-graph__search">
    <input
      type="text"
      className="knowledge-graph__search-input"
      placeholder="Filter knowledge nodes..."
      value={value}
      onChange={(e) => onChange(e.target.value)}
      aria-label="Search knowledge nodes"
    />
    {value && (
      <button
        className="knowledge-graph__search-clear"
        onClick={() => onChange('')}
        aria-label="Clear search"
      >
        x
      </button>
    )}
  </div>
);

/** Single tree node row */
const TreeNodeRow: React.FC<{
  node: KnowledgeNode;
  isExpanded: boolean;
  isSelected: boolean;
  matchesSearch: boolean;
  onToggle: () => void;
  onSelect: () => void;
}> = ({ node, isExpanded, isSelected, matchesSearch, onToggle, onSelect }) => {
  const hasChildren = node.children.length > 0;
  const indent = node.depth * 20;

  return (
    <div
      className={`knowledge-graph__tree-row${isSelected ? ' knowledge-graph__tree-row--selected' : ''}${matchesSearch ? ' knowledge-graph__tree-row--match' : ''}`}
      style={{ paddingLeft: `${indent + 8}px` }}
      onClick={onSelect}
      role="treeitem"
      aria-expanded={hasChildren ? isExpanded : undefined}
      aria-selected={isSelected}
      tabIndex={0}
      onKeyDown={(e) => {
        if (e.key === 'Enter') onSelect();
        if (e.key === ' ' && hasChildren) { e.preventDefault(); onToggle(); }
      }}
    >
      {/* Expand/collapse chevron */}
      {hasChildren ? (
        <span
          className="knowledge-graph__tree-chevron"
          onClick={(e) => { e.stopPropagation(); onToggle(); }}
        >
          {isExpanded ? '\u25BC' : '\u25B6'}
        </span>
      ) : (
        <span className="knowledge-graph__tree-chevron knowledge-graph__tree-chevron--leaf" />
      )}

      {/* Health dot */}
      <span
        className="knowledge-graph__health-dot"
        style={{ backgroundColor: node.healthColor }}
        title={node.healthStatus}
      />

      {/* Node name */}
      <span className="knowledge-graph__tree-name">{node.name}</span>

      {/* Type badge */}
      <span
        className="knowledge-graph__type-badge"
        style={{
          backgroundColor: `${TYPE_BADGE_COLORS[node.type]}22`,
          color: TYPE_BADGE_COLORS[node.type],
          border: `1px solid ${TYPE_BADGE_COLORS[node.type]}44`,
        }}
      >
        {TYPE_LABELS[node.type]}
      </span>

      {/* Last updated */}
      {node.lastUpdated && (
        <span className="knowledge-graph__tree-meta">{node.lastUpdated}</span>
      )}
    </div>
  );
};

/** Connections view for selected node */
const ConnectionsView: React.FC<{
  connections: Connection[];
  onNavigate: (nodeId: string) => void;
}> = ({ connections, onNavigate }) => {
  if (connections.length === 0) {
    return (
      <div className="knowledge-graph__connections-empty">
        No connections found
      </div>
    );
  }

  const upstream = connections.filter(c => c.direction === 'upstream');
  const downstream = connections.filter(c => c.direction === 'downstream');

  return (
    <div className="knowledge-graph__connections">
      {upstream.length > 0 && (
        <div className="knowledge-graph__connection-group">
          <span className="knowledge-graph__connection-heading">
            Upstream ({upstream.length})
          </span>
          {upstream.map((conn) => (
            <div
              key={`up-${conn.nodeId}`}
              className="knowledge-graph__connection-item"
              onClick={() => onNavigate(conn.nodeId)}
              role="button"
              tabIndex={0}
              onKeyDown={(e) => { if (e.key === 'Enter') onNavigate(conn.nodeId); }}
            >
              <span
                className="knowledge-graph__connection-dot"
                style={{ backgroundColor: conn.color }}
              />
              <span className="knowledge-graph__connection-name">
                {'\u2190'} {conn.name}
              </span>
              <span className="knowledge-graph__connection-edge">
                {EDGE_LABELS[conn.edgeType] ?? conn.edgeType}
              </span>
              <span
                className="knowledge-graph__type-badge knowledge-graph__type-badge--small"
                style={{
                  backgroundColor: `${TYPE_BADGE_COLORS[conn.type]}22`,
                  color: TYPE_BADGE_COLORS[conn.type],
                  border: `1px solid ${TYPE_BADGE_COLORS[conn.type]}44`,
                }}
              >
                {TYPE_LABELS[conn.type]}
              </span>
            </div>
          ))}
        </div>
      )}
      {downstream.length > 0 && (
        <div className="knowledge-graph__connection-group">
          <span className="knowledge-graph__connection-heading">
            Downstream ({downstream.length})
          </span>
          {downstream.map((conn) => (
            <div
              key={`down-${conn.nodeId}`}
              className="knowledge-graph__connection-item"
              onClick={() => onNavigate(conn.nodeId)}
              role="button"
              tabIndex={0}
              onKeyDown={(e) => { if (e.key === 'Enter') onNavigate(conn.nodeId); }}
            >
              <span
                className="knowledge-graph__connection-dot"
                style={{ backgroundColor: conn.color }}
              />
              <span className="knowledge-graph__connection-name">
                {'\u2192'} {conn.name}
              </span>
              <span className="knowledge-graph__connection-edge">
                {EDGE_LABELS[conn.edgeType] ?? conn.edgeType}
              </span>
              <span
                className="knowledge-graph__type-badge knowledge-graph__type-badge--small"
                style={{
                  backgroundColor: `${TYPE_BADGE_COLORS[conn.type]}22`,
                  color: TYPE_BADGE_COLORS[conn.type],
                  border: `1px solid ${TYPE_BADGE_COLORS[conn.type]}44`,
                }}
              >
                {TYPE_LABELS[conn.type]}
              </span>
            </div>
          ))}
        </div>
      )}
    </div>
  );
};

// ---------------------------------------------------------------------------
// Styles — inline style object for dark theme (matches Prime Radiant panels)
// ---------------------------------------------------------------------------
const styles: Record<string, React.CSSProperties> = {
  container: {
    fontFamily: "'JetBrains Mono', monospace",
    fontSize: '0.75rem',
    color: '#c9d1d9',
    backgroundColor: 'transparent',
    display: 'flex',
    flexDirection: 'column',
    maxHeight: '70vh',
    overflowY: 'auto',
  },
  header: {
    display: 'flex',
    alignItems: 'center',
    justifyContent: 'space-between',
    padding: '8px 12px',
    borderBottom: '1px solid #21262d',
    cursor: 'pointer',
    userSelect: 'none',
  },
  title: {
    fontSize: '0.8rem',
    fontWeight: 600,
    color: '#e6edf3',
  },
  count: {
    fontSize: '0.65rem',
    color: '#6b7280',
    marginLeft: 8,
  },
  body: {
    display: 'flex',
    flexDirection: 'column',
    gap: 0,
  },
  statsBar: {
    display: 'flex',
    gap: 16,
    padding: '8px 12px',
    borderBottom: '1px solid #21262d',
    flexWrap: 'wrap',
  },
  stat: {
    display: 'flex',
    flexDirection: 'column',
    alignItems: 'center',
    gap: 2,
  },
  statValue: {
    fontSize: '0.85rem',
    fontWeight: 700,
    color: '#e6edf3',
  },
  statLabel: {
    fontSize: '0.6rem',
    color: '#6b7280',
    textTransform: 'uppercase' as const,
    letterSpacing: '0.5px',
  },
  search: {
    position: 'relative',
    padding: '6px 12px',
    borderBottom: '1px solid #21262d',
  },
  searchInput: {
    width: '100%',
    padding: '6px 28px 6px 8px',
    backgroundColor: '#0d1117',
    border: '1px solid #30363d',
    borderRadius: 4,
    color: '#c9d1d9',
    fontSize: '0.72rem',
    fontFamily: "'JetBrains Mono', monospace",
    outline: 'none',
    boxSizing: 'border-box' as const,
  },
  searchClear: {
    position: 'absolute',
    right: 18,
    top: '50%',
    transform: 'translateY(-50%)',
    background: 'none',
    border: 'none',
    color: '#6b7280',
    cursor: 'pointer',
    fontSize: '0.7rem',
    padding: '2px 4px',
  },
  treeContainer: {
    maxHeight: '35vh',
    overflowY: 'auto',
    padding: '4px 0',
  },
  treeRow: {
    display: 'flex',
    alignItems: 'center',
    gap: 6,
    padding: '4px 8px',
    cursor: 'pointer',
    transition: 'background-color 0.15s',
    borderLeft: '2px solid transparent',
  },
  treeRowHover: {
    backgroundColor: '#161b22',
  },
  treeRowSelected: {
    backgroundColor: '#1c2128',
    borderLeftColor: '#FFD700',
  },
  treeRowMatch: {
    backgroundColor: '#1a1f2b',
  },
  chevron: {
    width: 14,
    fontSize: '0.55rem',
    color: '#6b7280',
    cursor: 'pointer',
    flexShrink: 0,
    textAlign: 'center' as const,
  },
  chevronLeaf: {
    width: 14,
    flexShrink: 0,
  },
  healthDot: {
    width: 6,
    height: 6,
    borderRadius: '50%',
    flexShrink: 0,
  },
  treeName: {
    flex: 1,
    overflow: 'hidden',
    textOverflow: 'ellipsis',
    whiteSpace: 'nowrap' as const,
  },
  typeBadge: {
    fontSize: '0.55rem',
    padding: '1px 5px',
    borderRadius: 3,
    fontWeight: 500,
    flexShrink: 0,
  },
  typeBadgeSmall: {
    fontSize: '0.5rem',
    padding: '0px 4px',
    borderRadius: 2,
    fontWeight: 500,
    flexShrink: 0,
  },
  treeMeta: {
    fontSize: '0.6rem',
    color: '#484f58',
    flexShrink: 0,
  },
  sectionTitle: {
    fontSize: '0.7rem',
    fontWeight: 600,
    color: '#8b949e',
    padding: '8px 12px 4px',
    borderTop: '1px solid #21262d',
  },
  connectionItem: {
    display: 'flex',
    alignItems: 'center',
    gap: 6,
    padding: '4px 12px',
    cursor: 'pointer',
    transition: 'background-color 0.15s',
  },
  connectionDot: {
    width: 6,
    height: 6,
    borderRadius: '50%',
    flexShrink: 0,
  },
  connectionName: {
    flex: 1,
    overflow: 'hidden',
    textOverflow: 'ellipsis',
    whiteSpace: 'nowrap' as const,
  },
  connectionEdge: {
    fontSize: '0.55rem',
    color: '#484f58',
    flexShrink: 0,
  },
  connectionsEmpty: {
    padding: '12px',
    color: '#484f58',
    fontSize: '0.7rem',
    textAlign: 'center' as const,
  },
  tarsBridge: {
    padding: '12px',
    color: '#484f58',
    fontSize: '0.65rem',
    textAlign: 'center' as const,
    borderTop: '1px solid #21262d',
    fontStyle: 'italic',
  },
  toggleChevron: {
    fontSize: '0.65rem',
    color: '#6b7280',
  },
  refreshBtn: {
    background: 'none',
    border: '1px solid #30363d',
    borderRadius: 4,
    color: '#8b949e',
    cursor: 'pointer',
    padding: '2px 6px',
    fontSize: '0.65rem',
    fontFamily: "'JetBrains Mono', monospace",
    transition: 'opacity 0.2s',
  },
};

// ---------------------------------------------------------------------------
// Main component
// ---------------------------------------------------------------------------
export const KnowledgeGraphPanel: React.FC = () => {
  const [graph, setGraph] = useState<GovernanceGraph | null>(null);
  const [graphIndex, setGraphIndex] = useState<GraphIndex | null>(null);
  const [selectedNode, setSelectedNode] = useState<string | null>(null);
  const [searchTerm, setSearchTerm] = useState('');
  const [debouncedSearch, setDebouncedSearch] = useState('');
  const [expanded, setExpanded] = useState<Set<string>>(new Set());
  const [collapsed, setCollapsed] = useState(false);
  const [isDemo, setIsDemo] = useState(true);
  const [refreshing, setRefreshing] = useState(false);
  const debounceRef = useRef<ReturnType<typeof setTimeout> | null>(null);

  // Debounce search input (300ms)
  useEffect(() => {
    if (debounceRef.current) clearTimeout(debounceRef.current);
    debounceRef.current = setTimeout(() => {
      setDebouncedSearch(searchTerm);
    }, 300);
    return () => {
      if (debounceRef.current) clearTimeout(debounceRef.current);
    };
  }, [searchTerm]);

  // Fetch governance data
  const refresh = useCallback(async () => {
    setRefreshing(true);
    try {
      const base = getApiBase();
      const apiUrl = `${base}/api/governance`;
      const data = await loadGovernanceDataAsync(apiUrl);
      setGraph(data);
      const idx = buildGraphIndex(data);
      setGraphIndex(idx);
      // If we got data from the API (more than sample), mark as live
      setIsDemo(data.nodes.length === 0);
    } catch {
      // loadGovernanceDataAsync already falls back to static data
      setIsDemo(true);
    }
    setRefreshing(false);
  }, []);

  useEffect(() => {
    refresh();
    const id = setInterval(refresh, 30_000);
    return () => clearInterval(id);
  }, [refresh]);

  // Build knowledge tree
  const knowledgeTree = useMemo(() => {
    if (!graph || !graphIndex) return [];
    return buildKnowledgeTree(graph, graphIndex);
  }, [graph, graphIndex]);

  // Compute stats
  const stats = useMemo(() => {
    if (!graph || !graphIndex) return { totalNodes: 0, coveragePct: 0, gaps: 0, contradictions: 0 };
    return computeStats(graph, graphIndex);
  }, [graph, graphIndex]);

  // Filter tree by search
  const filterTree = useCallback((nodes: KnowledgeNode[], term: string): KnowledgeNode[] => {
    if (!term) return nodes;
    const lower = term.toLowerCase();
    return nodes.reduce<KnowledgeNode[]>((acc, node) => {
      const nameMatch = node.name.toLowerCase().includes(lower);
      const typeMatch = node.type.toLowerCase().includes(lower);
      const filteredChildren = filterTree(node.children, term);
      if (nameMatch || typeMatch || filteredChildren.length > 0) {
        acc.push({ ...node, children: filteredChildren });
      }
      return acc;
    }, []);
  }, []);

  const filteredTree = useMemo(
    () => filterTree(knowledgeTree, debouncedSearch),
    [knowledgeTree, debouncedSearch, filterTree],
  );

  // Matching node IDs for highlighting
  const matchingIds = useMemo(() => {
    if (!debouncedSearch) return new Set<string>();
    const lower = debouncedSearch.toLowerCase();
    const ids = new Set<string>();
    function collect(nodes: KnowledgeNode[]) {
      for (const n of nodes) {
        if (n.name.toLowerCase().includes(lower) || n.type.toLowerCase().includes(lower)) {
          ids.add(n.id);
        }
        collect(n.children);
      }
    }
    collect(knowledgeTree);
    return ids;
  }, [knowledgeTree, debouncedSearch]);

  // Connections for selected node
  const connections = useMemo(() => {
    if (!selectedNode || !graphIndex) return [];
    return getConnections(selectedNode, graphIndex);
  }, [selectedNode, graphIndex]);

  // Selected node data
  const selectedNodeData = useMemo(() => {
    if (!selectedNode || !graphIndex) return null;
    return graphIndex.nodeMap.get(selectedNode) ?? null;
  }, [selectedNode, graphIndex]);

  // Toggle tree expansion
  const handleToggle = useCallback((nodeId: string) => {
    setExpanded(prev => {
      const next = new Set(prev);
      if (next.has(nodeId)) next.delete(nodeId);
      else next.add(nodeId);
      return next;
    });
  }, []);

  // Select node (and expand its parent chain)
  const handleSelect = useCallback((nodeId: string) => {
    setSelectedNode(prev => prev === nodeId ? null : nodeId);
  }, []);

  // Navigate to a connected node
  const handleNavigate = useCallback((nodeId: string) => {
    setSelectedNode(nodeId);
    // Auto-expand to show the node
    setExpanded(prev => {
      const next = new Set(prev);
      next.add(nodeId);
      return next;
    });
  }, []);

  // Render tree nodes recursively
  const renderTree = useCallback((nodes: KnowledgeNode[]): React.ReactNode => {
    return nodes.map(node => {
      const isExpanded = expanded.has(node.id);
      const isSelected = selectedNode === node.id;
      const matchesSearch = matchingIds.has(node.id);
      const hasChildren = node.children.length > 0;

      return (
        <React.Fragment key={node.id}>
          <div
            style={{
              ...styles.treeRow,
              paddingLeft: `${node.depth * 20 + 8}px`,
              ...(isSelected ? styles.treeRowSelected : {}),
              ...(matchesSearch && !isSelected ? styles.treeRowMatch : {}),
            }}
            onClick={() => handleSelect(node.id)}
            role="treeitem"
            aria-expanded={hasChildren ? isExpanded : undefined}
            aria-selected={isSelected}
            tabIndex={0}
            onKeyDown={(e) => {
              if (e.key === 'Enter') handleSelect(node.id);
              if (e.key === ' ' && hasChildren) { e.preventDefault(); handleToggle(node.id); }
            }}
            onMouseEnter={(e) => { (e.currentTarget as HTMLElement).style.backgroundColor = isSelected ? '#1c2128' : '#161b22'; }}
            onMouseLeave={(e) => {
              (e.currentTarget as HTMLElement).style.backgroundColor =
                isSelected ? '#1c2128' : matchesSearch ? '#1a1f2b' : 'transparent';
            }}
          >
            {/* Chevron */}
            {hasChildren ? (
              <span
                style={styles.chevron}
                onClick={(e) => { e.stopPropagation(); handleToggle(node.id); }}
              >
                {isExpanded ? '\u25BC' : '\u25B6'}
              </span>
            ) : (
              <span style={styles.chevronLeaf} />
            )}

            {/* Health dot */}
            <span
              style={{ ...styles.healthDot, backgroundColor: node.healthColor }}
              title={node.healthStatus}
            />

            {/* Name */}
            <span style={styles.treeName}>{node.name}</span>

            {/* Type badge */}
            <span
              style={{
                ...styles.typeBadge,
                backgroundColor: `${TYPE_BADGE_COLORS[node.type]}22`,
                color: TYPE_BADGE_COLORS[node.type],
                border: `1px solid ${TYPE_BADGE_COLORS[node.type]}44`,
              }}
            >
              {TYPE_LABELS[node.type]}
            </span>

            {/* Version / meta */}
            {node.lastUpdated && (
              <span style={styles.treeMeta}>{node.lastUpdated}</span>
            )}
          </div>

          {/* Children */}
          {hasChildren && isExpanded && renderTree(node.children)}
        </React.Fragment>
      );
    });
  }, [expanded, selectedNode, matchingIds, handleSelect, handleToggle]);

  // ---------------------------------------------------------------------------
  // Render
  // ---------------------------------------------------------------------------
  if (!graph) {
    return (
      <div className="prime-radiant__activity">
        <div className="prime-radiant__activity-header">
          <span className="prime-radiant__activity-title">Knowledge Graph</span>
        </div>
        <div style={{ padding: 16, color: '#6b7280', fontSize: '0.75rem' }}>Loading...</div>
      </div>
    );
  }

  return (
    <div className="prime-radiant__activity" style={styles.container}>
      {/* Header */}
      <div
        className="prime-radiant__activity-header"
        style={styles.header}
        onClick={() => setCollapsed(!collapsed)}
      >
        <span style={{ display: 'flex', alignItems: 'center', gap: 6 }}>
          <span className="prime-radiant__activity-title" style={styles.title}>
            Knowledge Graph
          </span>
          <span
            style={{
              display: 'inline-block',
              width: 8,
              height: 8,
              borderRadius: '50%',
              backgroundColor: isDemo ? '#eab308' : '#22c55e',
              boxShadow: isDemo
                ? '0 0 4px rgba(234,179,8,0.6)'
                : '0 0 4px rgba(34,197,94,0.6)',
            }}
            title={isDemo ? 'Static data — API unavailable' : 'Live — connected to API'}
          />
          <span style={{ fontSize: '0.6rem', color: isDemo ? '#eab308' : '#22c55e' }}>
            {isDemo ? 'Static' : 'Live'}
          </span>
          <span style={styles.count}>
            {stats.totalNodes} nodes
          </span>
        </span>
        <span style={{ display: 'flex', alignItems: 'center', gap: 6 }}>
          <button
            style={{
              ...styles.refreshBtn,
              opacity: refreshing ? 0.5 : 1,
              cursor: refreshing ? 'wait' : 'pointer',
            }}
            onClick={(e) => {
              e.stopPropagation();
              refresh();
            }}
            disabled={refreshing}
            title="Refresh knowledge graph"
          >
            {refreshing ? '\u21BB ...' : '\u21BB'}
          </button>
          <span style={styles.toggleChevron}>
            {collapsed ? '\u25B6' : '\u25BC'}
          </span>
        </span>
      </div>

      {!collapsed && (
        <div style={styles.body}>
          {/* Stats bar */}
          <div style={styles.statsBar}>
            <div style={styles.stat}>
              <span style={styles.statValue}>{stats.totalNodes}</span>
              <span style={styles.statLabel}>Nodes</span>
            </div>
            <div style={styles.stat}>
              <span style={{ ...styles.statValue, color: '#33CC66' }}>{stats.coveragePct}%</span>
              <span style={styles.statLabel}>Coverage</span>
            </div>
            <div style={styles.stat}>
              <span style={{ ...styles.statValue, color: stats.gaps > 0 ? '#FFB300' : '#8b949e' }}>
                {stats.gaps}
              </span>
              <span style={styles.statLabel}>Gaps</span>
            </div>
            <div style={styles.stat}>
              <span style={{ ...styles.statValue, color: stats.contradictions > 0 ? '#FF44FF' : '#8b949e' }}>
                {stats.contradictions}
              </span>
              <span style={styles.statLabel}>Contradictions</span>
            </div>
          </div>

          {/* Search */}
          <div style={styles.search}>
            <input
              type="text"
              style={styles.searchInput}
              placeholder="Filter knowledge nodes..."
              value={searchTerm}
              onChange={(e) => setSearchTerm(e.target.value)}
              aria-label="Search knowledge nodes"
            />
            {searchTerm && (
              <button
                style={styles.searchClear}
                onClick={() => setSearchTerm('')}
                aria-label="Clear search"
              >
                x
              </button>
            )}
          </div>

          {/* Tree view */}
          <div style={styles.treeContainer} role="tree" aria-label="Knowledge graph tree">
            {filteredTree.length > 0 ? (
              renderTree(filteredTree)
            ) : (
              <div style={styles.connectionsEmpty}>
                {debouncedSearch ? 'No matching nodes' : 'No knowledge nodes found'}
              </div>
            )}
          </div>

          {/* Connections view — shown when a node is selected */}
          {selectedNode && selectedNodeData && (
            <>
              <div style={styles.sectionTitle}>
                Connections for {selectedNodeData.name} ({connections.length})
              </div>
              {connections.length > 0 ? (
                <div style={{ maxHeight: '20vh', overflowY: 'auto' }}>
                  {/* Upstream */}
                  {connections.filter(c => c.direction === 'upstream').length > 0 && (
                    <>
                      <div style={{ ...styles.sectionTitle, fontSize: '0.6rem', padding: '4px 12px' }}>
                        Upstream (policies, constitutions)
                      </div>
                      {connections.filter(c => c.direction === 'upstream').map(conn => (
                        <div
                          key={`up-${conn.nodeId}`}
                          style={styles.connectionItem}
                          onClick={() => handleNavigate(conn.nodeId)}
                          role="button"
                          tabIndex={0}
                          onKeyDown={(e) => { if (e.key === 'Enter') handleNavigate(conn.nodeId); }}
                          onMouseEnter={(e) => { (e.currentTarget as HTMLElement).style.backgroundColor = '#161b22'; }}
                          onMouseLeave={(e) => { (e.currentTarget as HTMLElement).style.backgroundColor = 'transparent'; }}
                        >
                          <span style={{ ...styles.connectionDot, backgroundColor: conn.color }} />
                          <span style={styles.connectionName}>{'\u2190'} {conn.name}</span>
                          <span style={styles.connectionEdge}>
                            {EDGE_LABELS[conn.edgeType] ?? conn.edgeType}
                          </span>
                          <span
                            style={{
                              ...styles.typeBadgeSmall,
                              backgroundColor: `${TYPE_BADGE_COLORS[conn.type]}22`,
                              color: TYPE_BADGE_COLORS[conn.type],
                              border: `1px solid ${TYPE_BADGE_COLORS[conn.type]}44`,
                            }}
                          >
                            {TYPE_LABELS[conn.type]}
                          </span>
                        </div>
                      ))}
                    </>
                  )}
                  {/* Downstream */}
                  {connections.filter(c => c.direction === 'downstream').length > 0 && (
                    <>
                      <div style={{ ...styles.sectionTitle, fontSize: '0.6rem', padding: '4px 12px' }}>
                        Downstream (tests, schemas)
                      </div>
                      {connections.filter(c => c.direction === 'downstream').map(conn => (
                        <div
                          key={`down-${conn.nodeId}`}
                          style={styles.connectionItem}
                          onClick={() => handleNavigate(conn.nodeId)}
                          role="button"
                          tabIndex={0}
                          onKeyDown={(e) => { if (e.key === 'Enter') handleNavigate(conn.nodeId); }}
                          onMouseEnter={(e) => { (e.currentTarget as HTMLElement).style.backgroundColor = '#161b22'; }}
                          onMouseLeave={(e) => { (e.currentTarget as HTMLElement).style.backgroundColor = 'transparent'; }}
                        >
                          <span style={{ ...styles.connectionDot, backgroundColor: conn.color }} />
                          <span style={styles.connectionName}>{'\u2192'} {conn.name}</span>
                          <span style={styles.connectionEdge}>
                            {EDGE_LABELS[conn.edgeType] ?? conn.edgeType}
                          </span>
                          <span
                            style={{
                              ...styles.typeBadgeSmall,
                              backgroundColor: `${TYPE_BADGE_COLORS[conn.type]}22`,
                              color: TYPE_BADGE_COLORS[conn.type],
                              border: `1px solid ${TYPE_BADGE_COLORS[conn.type]}44`,
                            }}
                          >
                            {TYPE_LABELS[conn.type]}
                          </span>
                        </div>
                      ))}
                    </>
                  )}
                </div>
              ) : (
                <div style={styles.connectionsEmpty}>No connections found</div>
              )}
            </>
          )}

          {/* TARS bridge placeholder */}
          <div style={styles.tarsBridge}>
            TARS knowledge bridge not connected — showing governance graph overlay
          </div>
        </div>
      )}
    </div>
  );
};
