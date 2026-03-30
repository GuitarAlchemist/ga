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
};

function classifySource(source: string): SourceKind {
  const s = source.trim();
  if (s === 'graph://nodes') return 'graph-nodes';
  if (s === 'graph://edges') return 'graph-edges';
  if (s.startsWith('godot://')) return 'godot';
  if (GOVERNANCE_SHORTCUTS[s]) return 'api';  // governance.X → api
  if (s.startsWith('/api/') || s.startsWith('http://') || s.startsWith('https://')) return 'api';
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
        const res = await fetch(url);
        if (!res.ok) {
          console.warn(`[DataFetcher] ${res.status} from ${url}`);
          return [];
        }
        const json: unknown = await res.json();
        raw = Array.isArray(json) ? json : [json];
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
