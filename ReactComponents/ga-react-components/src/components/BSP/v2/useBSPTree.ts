/**
 * Fetch the BSP tree from the backend; fall back to a procedural demo
 * tree when the API is unreachable. The fallback is what makes the
 * "broken" live page work — the previous component depended on the API
 * landing successfully and showed nothing if it didn't.
 */

import { useEffect, useState } from 'react';
import { BSPApiService, type BSPTreeStructureResponse } from '../BSPApiService';
import { buildDemoTree } from './demoTree';

export interface BSPTreeState {
  tree: BSPTreeStructureResponse | null;
  loading: boolean;
  source: 'api' | 'demo' | 'pending';
  error: string | null;
}

export function useBSPTree(): BSPTreeState {
  const [state, setState] = useState<BSPTreeState>({
    tree: null,
    loading: true,
    source: 'pending',
    error: null,
  });

  useEffect(() => {
    let cancelled = false;
    (async () => {
      try {
        const tree = await BSPApiService.getTreeStructure();
        if (!cancelled) {
          setState({ tree, loading: false, source: 'api', error: null });
        }
      } catch (err) {
        // Demo-mode fallback so the page always renders.
        if (!cancelled) {
          setState({
            tree: buildDemoTree(),
            loading: false,
            source: 'demo',
            error: err instanceof Error ? err.message : String(err),
          });
        }
      }
    })();
    return () => {
      cancelled = true;
    };
  }, []);

  return state;
}
