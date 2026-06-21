// src/components/PrimeRadiant/usePanelData.ts
// Candidate #8 from /improve-codebase-architecture: one data seam for panels.
//
// DataFetcher already owns URL resolution, offline fallback, AbortController
// cancellation, and interval polling. Yet ~23 panels each hand-roll their own
// fetch() + useState([data, loading, error]) + fallback, and the same endpoint
// (e.g. /api/governance/beliefs) gets polled independently in several places.
//
// usePanelData is the React seam over DataFetcher.resolve / DataFetcher.poll:
// a panel's data block collapses to one line. Panels migrate to it incrementally.

import { useEffect, useState } from 'react';
import { poll, resolve } from './DataFetcher';

export interface PanelDataState<T> {
  /** Resolved rows (empty until first load completes). */
  data: T[];
  /** True until the first resolve/poll tick returns. */
  loading: boolean;
  /** Non-abort error from the first load, if any. */
  error: Error | null;
}

/**
 * Subscribe a panel to a DataFetcher source.
 *
 * @param source     A FROM source DataFetcher understands (e.g. 'governance.beliefs',
 *                   '/api/...', 'graph://nodes').
 * @param intervalMs When > 0, polls on that interval via DataFetcher.poll; otherwise
 *                   resolves once. Abort + cleanup are handled by DataFetcher.
 *
 * Panels needing WHERE predicates or graph context call DataFetcher.poll directly —
 * this hook covers the common "poll/resolve a source" case that dominates the duplication.
 */
export function usePanelData<T = Record<string, unknown>>(
  source: string,
  intervalMs = 0,
): PanelDataState<T> {
  const [state, setState] = useState<PanelDataState<T>>({ data: [], loading: true, error: null });

  useEffect(() => {
    let active = true;

    if (intervalMs > 0) {
      const unsubscribe = poll(source, intervalMs, (data) => {
        if (active) setState({ data: data as T[], loading: false, error: null });
      });
      return () => {
        active = false;
        unsubscribe();
      };
    }

    const controller = new AbortController();
    resolve(source, [], undefined, controller.signal)
      .then((data) => {
        if (active) setState({ data: data as T[], loading: false, error: null });
      })
      .catch((err: Error) => {
        if (active && err.name !== 'AbortError') {
          setState({ data: [], loading: false, error: err });
        }
      });

    return () => {
      active = false;
      controller.abort();
    };
  }, [source, intervalMs]);

  return state;
}
