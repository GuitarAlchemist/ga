// src/components/PrimeRadiant/DataFetcher.ts
// Generic data source resolver for IXQL FROM clauses.
// Resolves API URLs, governance file paths, and graph:// protocol to data arrays.

import { evaluatePredicate, type IxqlPredicate } from './IxqlControlParser';

// ---------------------------------------------------------------------------
// Source type detection
// ---------------------------------------------------------------------------

type SourceKind = 'api' | 'governance' | 'graph-nodes' | 'graph-edges' | 'godot' | 'unknown';

// Godot MCP WebSocket connection (lazy singleton)
let godotWs: WebSocket | null = null;
let godotWsReady = false;
const godotPending = new Map<number, { resolve: (v: unknown) => void; reject: (e: Error) => void }>();
let godotMsgId = 0;

// SECURITY NOTE: Godot MCP uses unauthenticated WebSocket on localhost.
// Acceptable for local development; do NOT expose port 6505 externally.
function getGodotWs(): WebSocket {
  if (godotWs && godotWs.readyState === WebSocket.OPEN) return godotWs;

  const GODOT_MCP_PORT = 6505;
  godotWs = new WebSocket(`ws://localhost:${GODOT_MCP_PORT}`);
  godotWsReady = false;

  godotWs.onopen = () => {
    godotWsReady = true;
    console.info('[DataFetcher] Godot MCP connected on port', GODOT_MCP_PORT);
  };

  godotWs.onmessage = (event) => {
    try {
      const msg = JSON.parse(event.data);
      if (msg.id && godotPending.has(msg.id)) {
        const p = godotPending.get(msg.id)!;
        godotPending.delete(msg.id);
        if (msg.error) p.reject(new Error(msg.error.message));
        else p.resolve(msg.result);
      }
    } catch { /* non-JSON message */ }
  };

  godotWs.onerror = () => console.warn('[DataFetcher] Godot MCP WebSocket error');
  godotWs.onclose = () => { godotWsReady = false; godotWs = null; };

  return godotWs;
}

async function callGodotMcp(tool: string, args: Record<string, unknown> = {}): Promise<unknown> {
  return new Promise((resolve, reject) => {
    const ws = getGodotWs();
    const id = ++godotMsgId;
    godotPending.set(id, { resolve, reject });

    const send = () => {
      ws.send(JSON.stringify({
        jsonrpc: '2.0', id,
        method: 'tools/call',
        params: { name: tool, arguments: args },
      }));
    };

    if (godotWsReady) send();
    else ws.addEventListener('open', send, { once: true });

    // Timeout after 10s
    setTimeout(() => {
      if (godotPending.has(id)) {
        godotPending.delete(id);
        reject(new Error('Godot MCP timeout'));
      }
    }, 10000);
  });
}

/** Public: send a command to Godot MCP. Used by ReactiveEngine actions. */
export async function godotCommand(tool: string, args: Record<string, unknown> = {}): Promise<unknown> {
  return callGodotMcp(tool, args);
}

// Governance dot-notation shortcuts: governance.beliefs → /api/governance/beliefs
const GOVERNANCE_SHORTCUTS: Record<string, string> = {
  'governance.beliefs':     '/api/governance/beliefs',
  'governance.backlog':     '/api/governance/backlog',
  'governance.predictions': '/api/governance/predictions',
  'governance.graph':       '/api/governance',
  'governance.epistemic.beliefs':      '/api/governance/beliefs',
  'governance.epistemic.strategies':   '/api/governance/strategies',
  'governance.epistemic.tensor':       '/api/governance/tensor',
  'governance.epistemic.learners':     '/api/governance/learners',
  'governance.epistemic.journal':      '/api/governance/journal',
  'governance.epistemic.incompetence': '/api/governance/incompetence',
};

// Offline fallback data — used when API is unreachable
const OFFLINE_FALLBACK: Record<string, Record<string, unknown>[]> = {
  'governance.beliefs': [
    { id: 'belief-001', proposition: 'Governance artifacts are append-only', truth_value: 'T', confidence: 0.95, article: 1, updated_at: '2026-03-30T12:00:00Z' },
    { id: 'belief-002', proposition: 'TARS-ix bridge is production-ready', truth_value: 'P', confidence: 0.72, article: 9, updated_at: '2026-03-31T08:00:00Z' },
    { id: 'belief-003', proposition: 'Hexavalent logic handles all edge cases', truth_value: 'P', confidence: 0.68, article: 1, updated_at: '2026-03-31T10:00:00Z' },
    { id: 'belief-004', proposition: 'Parser covers all IXQL commands', truth_value: 'D', confidence: 0.45, article: 7, updated_at: '2026-03-30T18:00:00Z' },
    { id: 'belief-005', proposition: 'Signal bus handles 50+ concurrent panels', truth_value: 'U', confidence: 0.5, article: 8, updated_at: '2026-03-29T14:00:00Z' },
    { id: 'belief-006', proposition: 'Constitutional gate blocks all bypass vectors', truth_value: 'P', confidence: 0.78, article: 9, updated_at: '2026-03-31T09:00:00Z' },
    { id: 'belief-007', proposition: 'Render proofs detect all divergences', truth_value: 'D', confidence: 0.42, article: 8, updated_at: '2026-03-31T06:00:00Z' },
    { id: 'belief-008', proposition: 'Case law precedent search is sound', truth_value: 'T', confidence: 0.91, article: 7, updated_at: '2026-03-31T11:00:00Z' },
    { id: 'belief-009', proposition: 'Form transition constraints prevent invalid state', truth_value: 'T', confidence: 0.88, article: 3, updated_at: '2026-03-31T07:00:00Z' },
    { id: 'belief-010', proposition: 'DecayTracker has zero allocation in tick', truth_value: 'C', confidence: 0.55, article: 8, updated_at: '2026-03-31T10:30:00Z' },
  ],
  'governance.backlog': [
    { id: 'backlog-001', title: 'Fix SHOW EPISTEMIC routing', status: 'done', severity: 3, type: 'bug' },
    { id: 'backlog-002', title: 'Truth Lattice Panel', status: 'done', severity: 5, type: 'feature' },
    { id: 'backlog-003', title: 'TARS-ix MCP bridge', status: 'deferred', severity: 4, type: 'feature' },
    { id: 'backlog-004', title: 'Evidence Ribbon widget', status: 'planned', severity: 3, type: 'feature' },
    { id: 'backlog-005', title: 'Contradiction Matrix', status: 'planned', severity: 4, type: 'feature' },
  ],
};

function isSameOrigin(url: string): boolean {
  if (url.startsWith('/')) return true; // relative paths are same-origin
  if (typeof window === 'undefined') return false;
  const origin = window.location.origin;
  return url.startsWith(origin + '/') || url.startsWith(origin + '?');
}

function classifySource(source: string): SourceKind {
  const s = source.trim();
  if (s === 'graph://nodes') return 'graph-nodes';
  if (s === 'graph://edges') return 'graph-edges';
  if (s.startsWith('godot://')) return 'godot';
  if (GOVERNANCE_SHORTCUTS[s]) return 'api';  // governance.X → api
  if (s.startsWith('/api/')) return 'api';     // relative API paths — safe
  // External URLs: only allow same-origin to prevent data exfiltration
  if (s.startsWith('http://') || s.startsWith('https://')) {
    if (isSameOrigin(s)) return 'api';
    console.warn(`[DataFetcher] Blocked cross-origin source: ${s}`);
    return 'unknown';
  }
  if (s.startsWith('governance/') || s.startsWith('governance\\')) return 'governance';
  return 'unknown';
}

function toApiUrl(source: string, kind: SourceKind): string {
  const s = source.trim();
  // Resolve governance shortcuts first
  if (GOVERNANCE_SHORTCUTS[s]) return GOVERNANCE_SHORTCUTS[s];
  switch (kind) {
    case 'api':
      return s;
    case 'governance':
      return `/api/governance/file-content?filePath=${encodeURIComponent(s)}`;
    default:
      return s;
  }
}

// ---------------------------------------------------------------------------
// Graph context — passed in so DataFetcher has no React coupling
// ---------------------------------------------------------------------------

export interface GraphContext {
  nodes: Record<string, unknown>[];
  edges: Record<string, unknown>[];
}

// ---------------------------------------------------------------------------
// Resolve a FROM source to an array of data items
// ---------------------------------------------------------------------------

export async function resolve(
  source: string,
  predicates: IxqlPredicate[] = [],
  graphContext?: GraphContext,
): Promise<unknown[]> {
  const kind = classifySource(source);

  let raw: unknown[];

  try {
    switch (kind) {
      case 'graph-nodes':
        raw = graphContext?.nodes ?? [];
        break;
      case 'graph-edges':
        raw = graphContext?.edges ?? [];
        break;
      case 'godot': {
        // godot://scene_tree → get_game_scene_tree
        // godot://project_info → get_project_info
        // godot://nodes?type=X → find_nodes_by_type
        // godot://properties/NodePath → get_node_properties
        const godotPath = source.replace('godot://', '').trim();
        const toolMap: Record<string, string> = {
          scene_tree: 'get_game_scene_tree',
          project_info: 'get_project_info',
          scripts: 'list_scripts',
          signals: 'get_signals',
          output: 'get_output_log',
          performance: 'get_performance_monitors',
          errors: 'get_editor_errors',
          status: 'get_project_info',
        };
        const tool = toolMap[godotPath] ?? godotPath;
        try {
          const result = await callGodotMcp(tool);
          raw = Array.isArray(result) ? result : [result as Record<string, unknown>];
        } catch (err) {
          console.warn(`[DataFetcher] Godot MCP call failed for "${tool}":`, err);
          raw = [{ status: 'disconnected', error: String(err) }];
        }
        break;
      }
      case 'api':
      case 'governance': {
        const url = toApiUrl(source, kind);
        try {
          const res = await fetch(url);
          if (!res.ok) {
            console.warn(`[DataFetcher] ${res.status} from ${url} — trying offline fallback`);
            raw = OFFLINE_FALLBACK[source.trim()] ?? [];
            break;
          }
          const json: unknown = await res.json();
          raw = Array.isArray(json) ? json : [json];
        } catch {
          // Network error — use offline fallback
          console.warn(`[DataFetcher] Network error for ${url} — using offline fallback`);
          raw = OFFLINE_FALLBACK[source.trim()] ?? [];
        }
        break;
      }
      default:
        console.warn(`[DataFetcher] Unknown source type: ${source}`);
        return [];
    }
  } catch (err) {
    console.warn(`[DataFetcher] Failed to resolve "${source}":`, err);
    return [];
  }

  // Apply WHERE predicates client-side
  if (predicates.length === 0) return raw;

  return raw.filter(item =>
    predicates.every(p => evaluatePredicate(p, item as Record<string, unknown>)),
  );
}

// ---------------------------------------------------------------------------
// Dot-path field resolution — e.g. "health.staleness" from nested object
// ---------------------------------------------------------------------------

export function resolveField(obj: unknown, path: string): unknown {
  const parts = path.split('.');
  let current: unknown = obj;
  for (const part of parts) {
    if (current == null || typeof current !== 'object') return undefined;
    current = (current as Record<string, unknown>)[part];
  }
  return current;
}

// ---------------------------------------------------------------------------
// Poll a source on an interval, returns unsubscribe function
// ---------------------------------------------------------------------------

export function poll(
  source: string,
  intervalMs: number,
  callback: (data: unknown[]) => void,
  predicates: IxqlPredicate[] = [],
  graphContext?: GraphContext,
): () => void {
  let active = true;

  const tick = async () => {
    if (!active) return;
    const data = await resolve(source, predicates, graphContext);
    if (active) callback(data);
  };

  // Initial fetch
  tick();

  const id = window.setInterval(tick, intervalMs);
  return () => {
    active = false;
    window.clearInterval(id);
  };
}
