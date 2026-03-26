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

// ---------------------------------------------------------------------------
// Live data fetching — polls a backend endpoint for fresh governance state
// ---------------------------------------------------------------------------

export interface LiveDataConfig {
  /** URL to fetch governance graph JSON (e.g., /api/governance or /governance-data.json) */
  url: string;
  /** WebSocket URL for real-time push updates (e.g., ws://localhost:7001/ws/governance). Preferred over polling when available. */
  wsUrl?: string;
  /** Poll interval in ms (default: 30000 = 30s). Only used when WebSocket is unavailable. */
  intervalMs?: number;
  /** Called when new data arrives — update graph in-place, don't rebuild */
  onUpdate: (graph: GovernanceGraph) => void;
  /** Called on fetch/connection error */
  onError?: (error: Error) => void;
  /** Called when connection status changes */
  onStatusChange?: (status: 'connected' | 'polling' | 'disconnected') => void;
}

export function startLivePolling(config: LiveDataConfig): () => void {
  const { url, wsUrl, intervalMs = 30000, onUpdate, onError, onStatusChange } = config;
  let active = true;
  let ws: WebSocket | null = null;
  let pollInterval: ReturnType<typeof setInterval> | null = null;
  let reconnectTimeout: ReturnType<typeof setTimeout> | null = null;
  let reconnectDelay = 1000; // exponential backoff starting at 1s

  const processGraph = (rawGraph: GovernanceGraph) => {
    const graph = applyHealthColors(rawGraph);
    onUpdate(graph);
  };

  // ─── WebSocket connection (preferred) ───
  const connectWs = () => {
    if (!active || !wsUrl) return;

    try {
      ws = new WebSocket(wsUrl);

      ws.onopen = () => {
        reconnectDelay = 1000; // reset backoff
        onStatusChange?.('connected');
        // Stop polling if it was running as fallback
        if (pollInterval) { clearInterval(pollInterval); pollInterval = null; }
      };

      ws.onmessage = (event) => {
        try {
          const data = JSON.parse(event.data);
          // Support both full graph and partial update messages
          if (data.type === 'health-update' && data.nodes) {
            // Partial update — just health data for specific nodes
            processGraph({ ...data, nodes: data.nodes, edges: data.edges ?? [] });
          } else if (data.nodes && data.edges) {
            // Full graph
            processGraph(data as GovernanceGraph);
          }
        } catch (err) {
          onError?.(new Error(`WS parse error: ${(err as Error).message}`));
        }
      };

      ws.onclose = () => {
        ws = null;
        if (!active) return;
        onStatusChange?.('disconnected');
        // Reconnect with exponential backoff, fall back to polling
        reconnectTimeout = setTimeout(() => {
          reconnectDelay = Math.min(reconnectDelay * 2, 30000);
          connectWs();
        }, reconnectDelay);
        // Start polling as fallback while disconnected
        if (!pollInterval) startPolling();
      };

      ws.onerror = () => {
        onError?.(new Error('WebSocket connection error'));
        ws?.close();
      };
    } catch (err) {
      onError?.(err as Error);
      // WebSocket not available — fall back to polling
      if (!pollInterval) startPolling();
    }
  };

  // ─── HTTP polling (fallback) ───
  const poll = async () => {
    if (!active) return;
    try {
      const response = await fetch(url);
      if (!response.ok) throw new Error(`HTTP ${response.status}`);
      const rawGraph = await response.json() as GovernanceGraph;
      processGraph(rawGraph);
      onStatusChange?.('polling');
    } catch (err) {
      onError?.(err as Error);
    }
  };

  const startPolling = () => {
    if (pollInterval) return;
    onStatusChange?.('polling');
    poll(); // initial fetch
    pollInterval = setInterval(poll, intervalMs);
  };

  // ─── Start: WebSocket first, poll as fallback ───
  if (wsUrl) {
    connectWs();
  } else {
    startPolling();
  }

  // Return cleanup function
  return () => {
    active = false;
    if (ws) { ws.close(); ws = null; }
    if (pollInterval) { clearInterval(pollInterval); pollInterval = null; }
    if (reconnectTimeout) { clearTimeout(reconnectTimeout); reconnectTimeout = null; }
  };
}

// ---------------------------------------------------------------------------
// In-place node health update — mutates existing nodes without graph rebuild
// ---------------------------------------------------------------------------
export function updateNodeHealth(
  existingNodes: GovernanceNode[],
  freshNodes: GovernanceNode[],
): { updated: string[]; changed: boolean } {
  const freshMap = new Map(freshNodes.map(n => [n.id, n]));
  const updated: string[] = [];
  let changed = false;

  for (const node of existingNodes) {
    const fresh = freshMap.get(node.id);
    if (!fresh?.health) continue;

    const oldScore = node.health?.resilienceScore;
    const newScore = fresh.health.resilienceScore;

    if (oldScore !== newScore || node.health?.ergolCount !== fresh.health.ergolCount || node.health?.lolliCount !== fresh.health.lolliCount) {
      node.health = fresh.health;
      node.healthStatus = deriveGovernanceHealthStatus(node);
      node.color = HEALTH_STATUS_COLORS[node.healthStatus];
      updated.push(node.id);
      changed = true;
    }
  }

  return { updated, changed };
}
