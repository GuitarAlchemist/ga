// src/components/PrimeRadiant/DeepLink.ts
// URL deep-linking for Prime Radiant — read/write state from URL params.
// Enables shareable URLs like ?panel=tribunal&q=Cmaj7&node=auto-remediation-policy

import { useEffect, useCallback, useRef } from 'react';
import type { PanelId } from './PanelRegistry';

// ---------------------------------------------------------------------------
// Types
// ---------------------------------------------------------------------------

export interface DeepLinkState {
  panel?: PanelId;
  node?: string;         // node ID to select/zoom
  query?: string;        // search or tribunal query
  signal?: string;       // signal to expand in AlgedonicPanel
  lang?: string;         // library language
  dept?: string;         // faculty department
  group?: string;        // icon rail group to expand
}

// ---------------------------------------------------------------------------
// Read deep link state from current URL
// ---------------------------------------------------------------------------

export function readDeepLink(): DeepLinkState {
  if (typeof window === 'undefined') return {};
  const params = new URLSearchParams(window.location.search);
  const state: DeepLinkState = {};

  const panel = params.get('panel');
  if (panel) state.panel = panel as PanelId;

  const node = params.get('node');
  if (node) state.node = node;

  const query = params.get('q') ?? params.get('query');
  if (query) state.query = query;

  const signal = params.get('signal');
  if (signal) state.signal = signal;

  const lang = params.get('lang');
  if (lang) state.lang = lang;

  const dept = params.get('dept');
  if (dept) state.dept = dept;

  const group = params.get('group');
  if (group) state.group = group;

  return state;
}

// ---------------------------------------------------------------------------
// Write deep link state to URL (without page reload)
// ---------------------------------------------------------------------------

export function writeDeepLink(state: DeepLinkState): void {
  if (typeof window === 'undefined') return;
  const params = new URLSearchParams();

  if (state.panel) params.set('panel', state.panel);
  if (state.node) params.set('node', state.node);
  if (state.query) params.set('q', state.query);
  if (state.signal) params.set('signal', state.signal);
  if (state.lang) params.set('lang', state.lang);
  if (state.dept) params.set('dept', state.dept);
  if (state.group) params.set('group', state.group);

  const search = params.toString();
  const newUrl = search
    ? `${window.location.pathname}?${search}`
    : window.location.pathname;

  window.history.replaceState(null, '', newUrl);
}

// ---------------------------------------------------------------------------
// Generate a shareable URL for current state
// ---------------------------------------------------------------------------

export function getShareableUrl(state: DeepLinkState): string {
  const params = new URLSearchParams();

  if (state.panel) params.set('panel', state.panel);
  if (state.node) params.set('node', state.node);
  if (state.query) params.set('q', state.query);
  if (state.signal) params.set('signal', state.signal);
  if (state.lang) params.set('lang', state.lang);
  if (state.dept) params.set('dept', state.dept);

  const search = params.toString();
  const base = typeof window !== 'undefined'
    ? `${window.location.origin}${window.location.pathname}`
    : '';
  return search ? `${base}?${search}` : base;
}

// ---------------------------------------------------------------------------
// Copy shareable URL to clipboard
// ---------------------------------------------------------------------------

export async function shareCurrentState(state: DeepLinkState): Promise<boolean> {
  const url = getShareableUrl(state);
  try {
    await navigator.clipboard.writeText(url);
    return true;
  } catch {
    // Fallback: select + copy
    const input = document.createElement('input');
    input.value = url;
    document.body.appendChild(input);
    input.select();
    document.execCommand('copy');
    document.body.removeChild(input);
    return true;
  }
}

// ---------------------------------------------------------------------------
// React hook — reads on mount, updates URL on state change
// ---------------------------------------------------------------------------

export interface UseDeepLinkOptions {
  activePanel: PanelId | null;
  selectedNodeId: string | null;
  onPanelChange: (panel: PanelId) => void;
  onNodeSelect: (nodeId: string) => void;
}

export interface UseDeepLinkResult {
  initialState: DeepLinkState;
  share: () => Promise<boolean>;
  updateUrl: (patch: Partial<DeepLinkState>) => void;
}

export function useDeepLink(opts: UseDeepLinkOptions): UseDeepLinkResult {
  const initialState = useRef(readDeepLink());
  const currentState = useRef<DeepLinkState>(initialState.current);

  // Apply initial deep link on mount
  useEffect(() => {
    const s = initialState.current;
    if (s.panel) opts.onPanelChange(s.panel);
    if (s.node) opts.onNodeSelect(s.node);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  // Update URL when panel/node changes
  useEffect(() => {
    currentState.current = {
      ...currentState.current,
      panel: opts.activePanel ?? undefined,
      node: opts.selectedNodeId ?? undefined,
    };
    writeDeepLink(currentState.current);
  }, [opts.activePanel, opts.selectedNodeId]);

  const updateUrl = useCallback((patch: Partial<DeepLinkState>) => {
    currentState.current = { ...currentState.current, ...patch };
    writeDeepLink(currentState.current);
  }, []);

  const share = useCallback(() => {
    return shareCurrentState(currentState.current);
  }, []);

  return { initialState: initialState.current, share, updateUrl };
}
