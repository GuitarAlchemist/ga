// src/components/PrimeRadiant/DataLoader.ts
// Loads governance data and builds the graph structure for 3D rendering

import type { GovernanceGraph, GovernanceNode, GovernanceEdge, GovernanceHealthStatus } from './types';
import { HEALTH_STATUS_COLORS } from './types';
import { LIVE_GOVERNANCE_GRAPH } from './liveData';
import { SAMPLE_GOVERNANCE_GRAPH } from './sampleData';

// ---------------------------------------------------------------------------
// Adjacency index for fast lookups
// ---------------------------------------------------------------------------
export interface GraphIndex {
  nodeMap: Map<string, GovernanceNode>;
  outEdges: Map<string, GovernanceEdge[]>;
  inEdges: Map<string, GovernanceEdge[]>;
  connectedNodes: Map<string, Set<string>>;
  connectedEdges: Map<string, Set<string>>;
}

// ---------------------------------------------------------------------------
// Build index from raw graph data
// ---------------------------------------------------------------------------
export function buildGraphIndex(graph: GovernanceGraph): GraphIndex {
  const nodeMap = new Map<string, GovernanceNode>();
  const outEdges = new Map<string, GovernanceEdge[]>();
  const inEdges = new Map<string, GovernanceEdge[]>();
  const connectedNodes = new Map<string, Set<string>>();
  const connectedEdges = new Map<string, Set<string>>();

  for (const node of graph.nodes) {
    nodeMap.set(node.id, node);
    outEdges.set(node.id, []);
    inEdges.set(node.id, []);
    connectedNodes.set(node.id, new Set());
    connectedEdges.set(node.id, new Set());
  }

  for (const edge of graph.edges) {
    outEdges.get(edge.source)?.push(edge);
    inEdges.get(edge.target)?.push(edge);

    connectedNodes.get(edge.source)?.add(edge.target);
    connectedNodes.get(edge.target)?.add(edge.source);

    connectedEdges.get(edge.source)?.add(edge.id);
    connectedEdges.get(edge.target)?.add(edge.id);
  }

  return { nodeMap, outEdges, inEdges, connectedNodes, connectedEdges };
}

// ---------------------------------------------------------------------------
// Load data — uses live Demerzel state by default, falls back to sample data
// ---------------------------------------------------------------------------
export function loadGovernanceData(data?: GovernanceGraph): GovernanceGraph {
  let graph: GovernanceGraph;
  if (data) {
    graph = data;
  } else if (LIVE_GOVERNANCE_GRAPH && LIVE_GOVERNANCE_GRAPH.nodes.length > 0) {
    graph = LIVE_GOVERNANCE_GRAPH;
  } else {
    graph = SAMPLE_GOVERNANCE_GRAPH;
  }
  // Apply semantic health-based colors to all nodes
  return applyHealthColors(graph);
}

// ---------------------------------------------------------------------------
// Get health status from resilience score (legacy 3-state)
// ---------------------------------------------------------------------------
export function getHealthStatus(score: number): 'healthy' | 'watch' | 'freeze' {
  if (score >= 0.8) return 'healthy';
  if (score >= 0.5) return 'watch';
  return 'freeze';
}

// ---------------------------------------------------------------------------
// Derive governance health status from node health metrics
// ---------------------------------------------------------------------------
export function deriveGovernanceHealthStatus(node: GovernanceNode): GovernanceHealthStatus {
  const health = node.health;
  if (!health) return 'unknown';
  if (health.lolliCount > 0) return 'error';
  if (health.resilienceScore < 0.5) return 'warning';
  if (health.resilienceScore >= 0.8 && health.ergolCount > 0) return 'healthy';
  if (health.resilienceScore >= 0.5) return 'healthy';
  return 'unknown';
}

// ---------------------------------------------------------------------------
// Apply health-based colors to graph nodes
// ---------------------------------------------------------------------------
export function applyHealthColors(graph: GovernanceGraph): GovernanceGraph {
  const coloredNodes = graph.nodes.map(node => {
    const healthStatus = deriveGovernanceHealthStatus(node);
    return {
      ...node,
      healthStatus,
      color: HEALTH_STATUS_COLORS[healthStatus],
    };
  });
  return { ...graph, nodes: coloredNodes };
}

// ---------------------------------------------------------------------------
// Search nodes by name or description
// ---------------------------------------------------------------------------
export function searchNodes(
  graph: GovernanceGraph,
  query: string,
): GovernanceNode[] {
  if (!query.trim()) return [];
  const lower = query.toLowerCase();
  return graph.nodes.filter(
    (n) =>
      n.name.toLowerCase().includes(lower) ||
      n.description.toLowerCase().includes(lower) ||
      n.type.toLowerCase().includes(lower),
  );
}
