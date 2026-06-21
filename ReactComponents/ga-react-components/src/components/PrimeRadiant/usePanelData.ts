// src/components/PrimeRadiant/usePanelData.ts
// Candidate #8 from /improve-codebase-architecture: one data seam for panels.
//
// DataFetcher already owns URL resolution, offline fallback, AbortController
// cancellation, and interval polling. Yet ~23 panels each hand-roll their own
// fetch() + useState([data, loading, error]) + fallback, and the same endpoint
// (e.g. /api/governance/beliefs) gets polled independently in several places.
//
// usePanelData is the React seam over DataFetcher.resolve: a panel's data block
// collapses to one line. Panels migrate to it incrementally.

import { useEffect, useState } from 'react';
import { resolve } from './DataFetcher';

export interface PanelDataState<T> {
  /** Resolved rows (empty until first load completes). */
  data: T[];
  /** True until the current source's first load returns. */
  loading: boolean;
  /** Non-abort error from the most recent load, if any. */
  error: Error | null;
}

/**
 * Subscribe a panel to a DataFetcher source.
 *
 * @param source     A FROM source DataFetcher understands (e.g. 'governance.beliefs',
 *                   '/api/...', 'graph://nodes').
 * @param intervalMs When > 0, re-resolves on that interval; otherwise resolves once.
 *                   In-flight requests are aborted on unmount / dependency change.
 *
 * Panels needing WHERE predicates or graph context call DataFetcher.poll/resolve
 * directly — this hook covers the common "poll/resolve a source" case that dominates
 * the duplication.
 *
 * Polling is implemented over resolve() (not DataFetcher.poll) so a non-abort error
 * surfaces into `error` rather than becoming an unhandled rejection that leaves the
 * hook stuck in `loading`.
 */
export function usePanelData<T = Record<string, unknown>>(
  source: string,
  intervalMs = 0,
): PanelDataState<T> {
  const [state, setState] = useState<PanelDataState<T>>({ data: [], loading: true, error: null });

  useEffect(() => {
    let active = true;
    const controller = new AbortController();

    // Reset on (re)subscribe so a source/interval change doesn't leave the previous
    // source's rows on screen with loading=false until the new first load returns.
    setState({ data: [], loading: true, error: null });

    const load = async () => {
      try {
        const data = await resolve(source, [], undefined, controller.signal);
        if (active) setState({ data: data as T[], loading: false, error: null });
      } catch (err) {
        // AbortError is expected on unmount / dependency change — ignore it.
        if (active && (err as Error).name !== 'AbortError') {
          setState({ data: [], loading: false, error: err as Error });
        }
      }
    };

    void load();
    const id = intervalMs > 0 ? window.setInterval(() => void load(), intervalMs) : undefined;

    return () => {
      active = false;
      controller.abort();
      if (id !== undefined) window.clearInterval(id);
    };
  }, [source, intervalMs]);

  return state;
}
