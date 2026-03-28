// src/components/PrimeRadiant/DataFetcher.ts
// Generic data source resolver for IXQL FROM clauses.
// Resolves API URLs, governance file paths, and graph:// protocol to data arrays.

import { evaluatePredicate, type IxqlPredicate } from './IxqlControlParser';

// ---------------------------------------------------------------------------
// Source type detection
// ---------------------------------------------------------------------------

type SourceKind = 'api' | 'governance' | 'graph-nodes' | 'graph-edges' | 'unknown';

function classifySource(source: string): SourceKind {
  const s = source.trim();
  if (s === 'graph://nodes') return 'graph-nodes';
  if (s === 'graph://edges') return 'graph-edges';
  if (s.startsWith('/api/') || s.startsWith('http://') || s.startsWith('https://')) return 'api';
  if (s.startsWith('governance/') || s.startsWith('governance\\')) return 'governance';
  return 'unknown';
}

function toApiUrl(source: string, kind: SourceKind): string {
  switch (kind) {
    case 'api':
      return source.trim();
    case 'governance':
      return `/api/governance/file-content?path=${encodeURIComponent(source.trim())}`;
    default:
      return source.trim();
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
