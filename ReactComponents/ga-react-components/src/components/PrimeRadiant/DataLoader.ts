// src/components/PrimeRadiant/DataLoader.ts
// Loads governance data and builds the graph structure for 3D rendering

import type { GovernanceGraph, GovernanceNode, GovernanceEdge, GovernanceHealthStatus } from './types';
import { HEALTH_STATUS_COLORS } from './types';
import { LIVE_GOVERNANCE_GRAPH } from './liveData';
import { SAMPLE_GOVERNANCE_GRAPH } from './sampleData';
import * as signalR from '@microsoft/signalr';

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

// Async version — fetches from API first, falls back to static data
export async function loadGovernanceDataAsync(apiUrl?: string): Promise<GovernanceGraph> {
  if (apiUrl) {
    try {
      const response = await fetch(apiUrl);
      if (response.ok) {
        const rawGraph = await response.json() as GovernanceGraph;
        return applyHealthColors(rawGraph);
      }
    } catch {
      console.warn('[DataLoader] API fetch failed, falling back to static data');
    }
  }
  return loadGovernanceData();
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
  /** URL to fetch governance graph JSON (e.g., /api/governance) */
  url: string;
  /** SignalR hub URL for real-time push updates (e.g., /hubs/governance). Preferred over polling. */
  hubUrl?: string;
  /** Poll interval in ms (default: 30000 = 30s). Only used when SignalR is unavailable. */
  intervalMs?: number;
  /** Called when new data arrives — update graph in-place, don't rebuild */
  onUpdate: (graph: GovernanceGraph) => void;
  /** Called on fetch/connection error */
  onError?: (error: Error) => void;
  /** Called when connection status changes */
  onStatusChange?: (status: 'connected' | 'polling' | 'disconnected') => void;
}

export function startLivePolling(config: LiveDataConfig): () => void {
  const { url, hubUrl, intervalMs = 30000, onUpdate, onError, onStatusChange } = config;
  let active = true;
  let connection: signalR.HubConnection | null = null;
  let pollInterval: ReturnType<typeof setInterval> | null = null;

  const processGraph = (rawGraph: GovernanceGraph) => {
    const graph = applyHealthColors(rawGraph);
    onUpdate(graph);
  };

  // ─── SignalR connection (preferred) ───
  const connectSignalR = async () => {
    if (!active || !hubUrl) return;

    try {
      connection = new signalR.HubConnectionBuilder()
        .withUrl(hubUrl)
        .withAutomaticReconnect([0, 1000, 2000, 5000, 10000, 30000]) // exponential backoff
        .configureLogging(signalR.LogLevel.Warning)
        .build();

      // Handle server-pushed events
      connection.on('GraphUpdate', (data: GovernanceGraph) => {
        processGraph(data);
      });

      connection.on('NodeChanged', (data: { nodeId: string; health: unknown; healthStatus: string; color: string }) => {
        // Partial update — single node
        onUpdate({ nodes: [data as unknown as GovernanceNode], edges: [], globalHealth: { resilienceScore: 0, lolliCount: 0, ergolCount: 0 }, timestamp: new Date().toISOString() } as GovernanceGraph);
      });

      connection.on('Connected', (data: { connections: number }) => {
        console.log(`[Governance] Connected (${data.connections} clients)`);
      });

      connection.onreconnecting(() => {
        onStatusChange?.('disconnected');
        // Start polling as fallback while reconnecting
        if (!pollInterval) startPolling();
      });

      connection.onreconnected(() => {
        onStatusChange?.('connected');
        // Stop polling fallback
        if (pollInterval) { clearInterval(pollInterval); pollInterval = null; }
        // Request fresh data
        connection?.invoke('Subscribe').catch(() => {});
      });

      connection.onclose(() => {
        if (!active) return;
        onStatusChange?.('disconnected');
        if (!pollInterval) startPolling();
      });

      await connection.start();
      onStatusChange?.('connected');

      // Stop polling if it was running
      if (pollInterval) { clearInterval(pollInterval); pollInterval = null; }

      // Subscribe to governance group and get initial data
      await connection.invoke('Subscribe');

    } catch (err) {
      onError?.(new Error(`SignalR connection failed: ${(err as Error).message}`));
      connection = null;
      // Fall back to polling
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
      if (!connection) onStatusChange?.('polling');
    } catch (err) {
      onError?.(err as Error);
    }
  };

  const startPolling = () => {
    if (pollInterval) return;
    onStatusChange?.('polling');
    poll();
    pollInterval = setInterval(poll, intervalMs);
  };

  // ─── Start: SignalR first, poll as fallback ───
  if (hubUrl) {
    connectSignalR();
  } else {
    startPolling();
  }

  // Return cleanup function
  return () => {
    active = false;
    connection?.stop();
    connection = null;
    if (pollInterval) { clearInterval(pollInterval); pollInterval = null; }
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
