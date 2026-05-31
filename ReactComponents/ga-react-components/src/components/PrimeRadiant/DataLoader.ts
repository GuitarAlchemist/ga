// src/components/PrimeRadiant/DataLoader.ts
// Loads governance data and builds the graph structure for 3D rendering

import type { GovernanceGraph, GovernanceNode, GovernanceEdge, GovernanceHealthStatus, HexavalentTruth, NodeAugmentation } from './types';
import { HEALTH_STATUS_COLORS } from './types';
import { LIVE_GOVERNANCE_GRAPH } from './liveData';
import { SAMPLE_GOVERNANCE_GRAPH } from './sampleData';
import * as signalR from '@microsoft/signalr';

// ---------------------------------------------------------------------------
// Belief state types (tetravalent: T/F/U/C)
// ---------------------------------------------------------------------------
export type TetravalentStatus = 'T' | 'F' | 'U' | 'C';

export interface EvidenceItem {
  source: string;
  claim: string;
  timestamp?: string;
  reliability: number;
}

export interface BeliefEvidence {
  supporting: EvidenceItem[];
  contradicting: EvidenceItem[];
}

export interface BeliefState {
  id: string;
  proposition: string;
  truth_value: TetravalentStatus;
  confidence: number;
  evidence?: BeliefEvidence;
  last_updated?: string;
  evaluated_by?: string;
  source_file?: string;
}

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
  /** Called when the backend requests a screenshot capture */
  onScreenshotRequest?: (reason: string) => void;
  /** Called when a single belief state is updated */
  onBeliefUpdate?: (belief: BeliefState) => void;
  /** Called when a full beliefs snapshot is received */
  onBeliefsSnapshot?: (beliefs: BeliefState[]) => void;
  /** Called when an algedonic signal arrives via SignalR */
  onAlgedonicSignal?: (signal: AlgedonicSignalEvent) => void;
  /** Called when viewer presence list changes */
  onViewersChanged?: (viewers: ViewerInfo[]) => void;
  /** Called when another client broadcasts its camera position (presentation mode) */
  onCameraSync?: (data: CameraSyncData) => void;
  /** Called when the backend commands navigation to a planet/moon */
  onNavigateToPlanet?: (target: string) => void;
}

// ---------------------------------------------------------------------------
// Algedonic signal event (matches backend AlgedonicSignalDto)
// ---------------------------------------------------------------------------
export interface AlgedonicSignalEvent {
  id: string;
  timestamp: string;
  signal: string;
  type: 'pain' | 'pleasure';
  source: string;
  severity: 'emergency' | 'warning' | 'info';
  status: 'active' | 'acknowledged' | 'resolved';
  description?: string;
  nodeId?: string;
}

// ---------------------------------------------------------------------------
// Viewer presence info (matches backend ViewerInfo record)
// ---------------------------------------------------------------------------
export interface ViewerInfo {
  connectionId: string;
  color: string;
  browser: string;
  connectedAt: string;
  displayName?: string;
  avatarUrl?: string | null;
}

export interface CameraSyncData {
  px: number; py: number; pz: number;
  lx: number; ly: number; lz: number;
  sender: string;
}

export interface LivePollingHandle {
  /** Stop polling and disconnect SignalR */
  stop: () => void;
  /** Submit a screenshot back to the server via SignalR */
  submitScreenshot: (base64Image: string, format: string) => Promise<void>;
  /** Broadcast camera position to other connected clients (presentation mode) */
  syncCamera: (px: number, py: number, pz: number, lx: number, ly: number, lz: number) => void;
}

export function startLivePolling(config: LiveDataConfig): LivePollingHandle {
  const { url, hubUrl, intervalMs = 30000, onUpdate, onError, onStatusChange, onScreenshotRequest, onBeliefUpdate, onBeliefsSnapshot, onAlgedonicSignal, onViewersChanged, onCameraSync, onNavigateToPlanet } = config;
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
      // Attach JWT from sessionStorage so the server can populate
      // Context.User.Identity claims (displayName, avatar) in GovernanceHub.
      connection = new signalR.HubConnectionBuilder()
        .withUrl(hubUrl, {
          accessTokenFactory: () => {
            try { return sessionStorage.getItem('ga_jwt') ?? ''; }
            catch { return ''; }
          },
        })
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

      // Screenshot capture — backend requests a screenshot from connected clients
      connection.on('RequestScreenshot', (data: { reason?: string }) => {
        console.log('[Governance] Screenshot requested:', data.reason);
        onScreenshotRequest?.(data.reason ?? 'Backend request');
      });

      // Scene control — backend commands navigation to a planet/moon.
      // The ix harness rendering-invariant auditor uses this to
      // navigate + capture screenshots for belief-driven QA.
      connection.on('NavigateToPlanet', (data: { target: string }) => {
        console.log('[Governance] Navigate to planet:', data.target);
        onNavigateToPlanet?.(data.target);
      });

      // Belief state updates — tetravalent T/F/U/C
      connection.on('BeliefUpdate', (data: BeliefState) => {
        console.log('[Governance] Belief updated:', data.id, data.truth_value);
        onBeliefUpdate?.(data);
      });

      connection.on('BeliefsSnapshot', (data: BeliefState[]) => {
        console.log(`[Governance] Beliefs snapshot: ${data.length} beliefs`);
        onBeliefsSnapshot?.(data);
      });

      // Algedonic signal events — pain/pleasure governance alerts
      connection.on('AlgedonicSignal', (data: AlgedonicSignalEvent) => {
        console.log('[Governance] Algedonic signal:', data.signal, data.type);
        onAlgedonicSignal?.(data);
      });

      // Viewer presence tracking
      connection.on('ViewersChanged', (data: ViewerInfo[]) => {
        console.log(`[Governance] Viewers changed: ${data.length} online`);
        onViewersChanged?.(data);
      });

      // Camera sync — presentation mode broadcasts camera from leader to followers
      connection.on('CameraSync', (data: CameraSyncData) => {
        onCameraSync?.(data);
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

      // Subscribe to belief updates
      if (onBeliefUpdate || onBeliefsSnapshot) {
        await connection.invoke('SubscribeBeliefs');
      }

      // Request initial viewer list
      if (onViewersChanged) {
        await connection.invoke('GetViewers');
      }

      // Fetch recent algedonic signals on initial connect
      if (onAlgedonicSignal) {
        try {
          const baseUrl = hubUrl?.replace(/\/hubs\/.*$/, '') ?? '';
          const recentResponse = await fetch(`${baseUrl}/api/algedonic/recent`);
          if (recentResponse.ok) {
            const recentSignals = await recentResponse.json() as AlgedonicSignalEvent[];
            for (const sig of recentSignals) {
              onAlgedonicSignal(sig);
            }
          }
        } catch {
          console.warn('[Governance] Failed to fetch recent algedonic signals');
        }
      }

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

  // Return handle with cleanup and screenshot submission
  return {
    stop: () => {
      active = false;
      connection?.stop();
      connection = null;
      if (pollInterval) { clearInterval(pollInterval); pollInterval = null; }
    },
    submitScreenshot: async (base64Image: string, format: string) => {
      if (connection?.state === signalR.HubConnectionState.Connected) {
        await connection.invoke('SubmitScreenshot', base64Image, format);
      } else {
        console.warn('[Governance] Cannot submit screenshot — SignalR not connected');
      }
    },
    syncCamera: (px: number, py: number, pz: number, lx: number, ly: number, lz: number) => {
      if (connection?.state === signalR.HubConnectionState.Connected) {
        connection.invoke('SyncCamera', px, py, pz, lx, ly, lz).catch((err) => {
          console.warn('[Governance] SyncCamera failed:', err);
        });
      }
    },
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

// ---------------------------------------------------------------------------
// Dashboard channel enrichment — join dev-dashboard sidecars onto governance
// nodes by suffix-matching filePath. Powers three Prime Radiant visual
// channels (hexavalent rim, coverage halo, algedonic pulse) plus the three
// new DetailPanel sections. Tolerant: any failing fetch leaves that channel
// undefined on every node — the base graph never blocks on dev-data.
// ---------------------------------------------------------------------------

// Endpoint payload shapes (defensive — schemas may evolve)
interface AnnotationLocation { path: string; line_start: number; line_end: number }
interface AnnotationRecord {
  location: AnnotationLocation;
  truth_value: HexavalentTruth;
  kind: string;
  certainty: string;
  claim: string;
}
interface AnnotationsResponse { annotations?: AnnotationRecord[] }

interface TestGapRecord { path: string; risk_score: number; churn: number; complexity: number }
interface TestGapsResponse { data?: { files?: TestGapRecord[] }; files?: TestGapRecord[] }

interface AlgedonicRecord {
  id?: string;
  signal?: string;
  type?: 'pain' | 'pleasure';
  severity?: string;
  status?: string;
  timestamp?: string;
  source?: string;
  nodeId?: string;
}
type AlgedonicResponse =
  | AlgedonicRecord[]
  | { signals?: AlgedonicRecord[] }
  | { data?: AlgedonicRecord[] }
  | { unacked?: AlgedonicRecord[]; top3?: AlgedonicRecord[] };

// Path normalization — case-insensitive, forward-slash, no leading slash
function normalizePath(p: string): string {
  return p.replace(/\\/g, '/').replace(/^\.?\//, '').toLowerCase();
}

// Suffix match in either direction (annotation path may be absolute,
// node filePath is repo-relative)
function pathsMatch(a: string, b: string): boolean {
  if (!a || !b) return false;
  const na = normalizePath(a);
  const nb = normalizePath(b);
  return na === nb || na.endsWith('/' + nb) || nb.endsWith('/' + na);
}

// Dominant truth value: C wins if any present (per Demerzel hexavalent
// semantics). Otherwise mode, with ties broken toward the worst value
// (F > D > U > P > T).
const TRUTH_BADNESS: Record<HexavalentTruth, number> = { F: 5, D: 4, U: 3, P: 2, T: 1, C: 6 };
function dominantTruth(counts: Partial<Record<HexavalentTruth, number>>): HexavalentTruth | null {
  const keys = Object.keys(counts) as HexavalentTruth[];
  if (keys.length === 0) return null;
  if ((counts.C ?? 0) > 0) return 'C';
  let best: HexavalentTruth = keys[0];
  let bestCount = counts[best] ?? 0;
  for (const k of keys) {
    const c = counts[k] ?? 0;
    if (c > bestCount || (c === bestCount && TRUTH_BADNESS[k] > TRUTH_BADNESS[best])) {
      best = k;
      bestCount = c;
    }
  }
  return best;
}

/**
 * Fetch dev-dashboard sidecars and merge into governance nodes.
 * Tolerant: any failing fetch leaves that channel undefined on all nodes.
 * Path matching is suffix-based: an annotation/test-gap path matches a node
 * if the annotation path ends with the node's filePath (or vice versa).
 */
export async function enrichNodesWithDevData(graph: GovernanceGraph): Promise<GovernanceGraph> {
  const [annotationsResult, testGapsResult, algedonicResult] = await Promise.allSettled([
    fetch('/dev-data/ai-annotations').then(r => r.ok ? r.json() as Promise<AnnotationsResponse> : null),
    fetch('/dev-data/sentrux/test-gaps').then(r => r.ok ? r.json() as Promise<TestGapsResponse> : null),
    fetch('/dev-data/algedonic').then(r => r.ok ? r.json() as Promise<AlgedonicResponse> : null),
  ]);

  // ── Group annotations by path ──
  const annotationsByPath = new Map<string, AnnotationRecord[]>();
  if (annotationsResult.status === 'fulfilled' && annotationsResult.value?.annotations) {
    for (const ann of annotationsResult.value.annotations) {
      if (!ann.location?.path) continue;
      const key = normalizePath(ann.location.path);
      const list = annotationsByPath.get(key) ?? [];
      list.push(ann);
      annotationsByPath.set(key, list);
    }
  }

  // ── Group test-gaps by path ──
  const testGapsByPath = new Map<string, TestGapRecord>();
  if (testGapsResult.status === 'fulfilled' && testGapsResult.value) {
    const files = testGapsResult.value.data?.files ?? testGapsResult.value.files ?? [];
    for (const gap of files) {
      if (!gap.path) continue;
      testGapsByPath.set(normalizePath(gap.path), gap);
    }
  }

  // ── Normalize algedonic to a flat array, filter active + last 24h ──
  let algedonicRecords: AlgedonicRecord[] = [];
  if (algedonicResult.status === 'fulfilled' && algedonicResult.value) {
    const raw = algedonicResult.value;
    if (Array.isArray(raw)) algedonicRecords = raw;
    else if ('signals' in raw && Array.isArray(raw.signals)) algedonicRecords = raw.signals;
    else if ('data' in raw && Array.isArray(raw.data)) algedonicRecords = raw.data;
    // /dev-data/algedonic is projected by vite.config.ts projectAlgedonic() as
    // { unacked, top3 } — use the full unacked list (the 24h filter below
    // narrows it) so the algedonic channel populates from dev-data, not just
    // from SignalR. Without this branch the channel silently stays empty.
    else if ('unacked' in raw && Array.isArray(raw.unacked)) algedonicRecords = raw.unacked;
  }
  const cutoffMs = Date.now() - 24 * 60 * 60 * 1000;
  algedonicRecords = algedonicRecords.filter(s => {
    if (s.status && s.status !== 'active') return false;
    if (s.timestamp) {
      const ts = new Date(s.timestamp).getTime();
      if (isFinite(ts) && ts < cutoffMs) return false;
    }
    return true;
  });

  // ── Merge onto nodes ──
  const enrichedNodes = graph.nodes.map((node): GovernanceNode => {
    const filePath = node.filePath;
    const aug: NodeAugmentation = {};

    if (filePath && annotationsByPath.size > 0) {
      const matched: AnnotationRecord[] = [];
      const nKey = normalizePath(filePath);
      // Direct hit
      const direct = annotationsByPath.get(nKey);
      if (direct) matched.push(...direct);
      // Suffix matches either direction
      for (const [key, anns] of annotationsByPath) {
        if (key === nKey) continue;
        if (pathsMatch(key, filePath)) matched.push(...anns);
      }
      if (matched.length > 0) {
        const by_truth_value: Partial<Record<HexavalentTruth, number>> = {};
        for (const a of matched) {
          by_truth_value[a.truth_value] = (by_truth_value[a.truth_value] ?? 0) + 1;
        }
        const recent = matched.slice(-5).reverse().map(a => ({
          claim: a.claim,
          kind: a.kind,
          certainty: a.certainty,
          truth_value: a.truth_value,
          line_start: a.location.line_start,
          line_end: a.location.line_end,
        }));
        aug.annotations = {
          total: matched.length,
          by_truth_value,
          dominant: dominantTruth(by_truth_value),
          recent,
        };
      }
    }

    if (filePath && testGapsByPath.size > 0) {
      const nKey = normalizePath(filePath);
      let gap = testGapsByPath.get(nKey);
      if (!gap) {
        for (const [key, g] of testGapsByPath) {
          if (pathsMatch(key, filePath)) { gap = g; break; }
        }
      }
      if (gap) {
        aug.testGap = {
          risk_score: Math.max(0, Math.min(1, gap.risk_score)),
          churn: gap.churn,
          complexity: gap.complexity,
        };
      }
    }

    if (algedonicRecords.length > 0) {
      const recentSignals: NonNullable<NodeAugmentation['algedonic']>['recent'] = [];
      for (const s of algedonicRecords) {
        const matchesNode =
          (s.nodeId && s.nodeId === node.id) ||
          (s.source && filePath && pathsMatch(s.source, filePath)) ||
          (s.source && s.source === node.id);
        if (matchesNode) {
          recentSignals.push({
            id: s.id ?? '',
            signal: s.signal ?? '',
            type: s.type ?? 'pain',
            severity: s.severity ?? 'info',
            timestamp: s.timestamp ?? '',
          });
        }
      }
      if (recentSignals.length > 0) {
        // Newest first, cap at 3
        recentSignals.sort((a, b) => (b.timestamp || '').localeCompare(a.timestamp || ''));
        aug.algedonic = { recent: recentSignals.slice(0, 3) };
      }
    }

    // Only attach augmentation if at least one channel populated
    if (aug.annotations || aug.testGap || aug.algedonic) {
      return { ...node, augmentation: aug };
    }
    return node;
  });

  return { ...graph, nodes: enrichedNodes };
}
