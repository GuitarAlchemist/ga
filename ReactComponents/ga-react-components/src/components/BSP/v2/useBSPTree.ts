/**
 * Build the BSP tree the explorer renders.
 *
 * Originally tried to fetch from the backend `/api/bsp/tree-structure`
 * endpoint and fell back to a procedural demo tree when the API was
 * unreachable. After the 2026-05-17 rebuild this hook *only* serves the
 * curated musical tree — the backend tree is topologically faithful but
 * not musically useful (no key/scale/Roman-numeral metadata), and the
 * point of the page is to teach harmony, not to mirror a server's data
 * structure. We keep the same return shape so callers don't notice.
 *
 * If you need to inspect the live backend BSP tree, use
 * `BSPTreeVisualization` instead — that one stays API-driven.
 */

import { useEffect, useState } from 'react';
import type { BSPTreeStructureResponse } from '../BSPApiService';
import { buildMusicalTree } from './musicalTree';

export interface BSPTreeState {
  tree: BSPTreeStructureResponse | null;
  loading: boolean;
  source: 'curated';
  error: string | null;
}

export function useBSPTree(): BSPTreeState {
  const [state, setState] = useState<BSPTreeState>({
    tree: null,
    loading: true,
    source: 'curated',
    error: null,
  });

  useEffect(() => {
    // Synchronous, but kept in an effect so the loading flag flips
    // once after mount — keeps the SSR-safe pattern and gives the
    // R3F canvas a clean mount sequence.
    setState({
      tree: buildMusicalTree(),
      loading: false,
      source: 'curated',
      error: null,
    });
  }, []);

  return state;
}
