// McpControlProvider — Phase 1 (read-only) SignalR client for the
// /test#dev development dashboard, paired with the C# DevDashboardHub
// at /hubs/dev-dashboard.
//
// Mounts inside DevelopmentSection so the connection only opens when an
// operator is actually on /test#dev/*. Demos pages never open a hub.
//
// Listens for:
//   NavigateTo        — { subTab } → set the URL hash to #dev/{subTab}
//   Refresh           — { endpoint? } → bump a refresh counter for that
//                       endpoint (or all), letting fetcher hooks re-fetch
//   RequestState      — { requestId } → walk DOM + state, SubmitState back
//   RequestScreenshot — { requestId, subTab?, fullPage? } → html2canvas
//                       the dashboard root, SubmitScreenshot back
//
// Phase 2 (deferred) would gate writes (rescan, dismiss, run-action) on a
// Cloudflare Access JWT. For now read-only suffices.
//
// See docs/runbooks/mcp-dashboard-control.md for the full pattern + how to
// invoke from MCP.
import React, { useEffect, useRef } from 'react';
import * as signalR from '@microsoft/signalr';
import html2canvas from 'html2canvas';

type DevSubTab =
  | 'summary' | 'architecture' | 'product' | 'project'
  | 'qa' | 'sentrux' | 'harness' | 'annotations';

const VALID_TABS: ReadonlySet<DevSubTab> = new Set([
  'summary', 'architecture', 'product', 'project',
  'qa', 'sentrux', 'harness', 'annotations',
]);

/**
 * Global event bus used by fetcher hooks inside the dashboard to react to
 * remote refresh requests. Lightweight intentionally — Redux/RTK Query is
 * heavier than this needs to be for Phase 1.
 *
 * Hooks that want to be refresh-aware can subscribe like:
 *
 *   useEffect(() => {
 *     const off = window.addEventListener('mcp:dashboard:refresh', ...);
 *     return () => window.removeEventListener('mcp:dashboard:refresh', off);
 *   }, []);
 */
export interface DashboardRefreshDetail {
  endpoint: string | null; // null = refresh all
  timestamp: string;
}

/**
 * Walk the DOM to extract a structured snapshot of the dashboard's current
 * state. Read-only — we never mutate. Returns the same shape that the MCP
 * tool ga_dashboard_state exposes.
 */
function captureDashboardState(): Record<string, unknown> {
  // current_tab — from the URL hash, same source DevelopmentSection uses
  const hashMatch = window.location.hash.match(/^#dev\/(\w+)$/);
  const current_tab = (hashMatch && VALID_TABS.has(hashMatch[1] as DevSubTab))
    ? hashMatch[1]
    : 'summary';

  // visible_components — every Card heading currently in the document.
  // Good signal for "what's rendered right now" without enumerating the
  // entire React tree. Empty list if no cards visible.
  const headings = Array.from(document.querySelectorAll('h1, h2, h3, h4, h5, h6'))
    .map((el) => (el.textContent ?? '').trim())
    .filter((t) => t.length > 0 && t.length < 80);
  // De-dupe while preserving order
  const seen = new Set<string>();
  const visible_components = headings.filter((h) => {
    if (seen.has(h)) return false;
    seen.add(h);
    return true;
  });

  // algedonic_unacked_count — count rendered MUI Chips that look like
  // unacked algedonic markers. Convention: ".algedonic-unacked" class OR a
  // chip whose text contains "unacked" or "PAIN".
  const unackedNodes = document.querySelectorAll(
    '.algedonic-unacked, [data-algedonic-unacked="true"]'
  );
  let algedonic_unacked_count = unackedNodes.length;
  if (algedonic_unacked_count === 0) {
    // Fallback: textual scan of small chips for "unacked" or "pain"
    const chips = document.querySelectorAll('[class*="MuiChip-root"]');
    algedonic_unacked_count = Array.from(chips).filter((c) => {
      const t = (c.textContent ?? '').toLowerCase();
      return t.includes('unacked') || t.includes('pain');
    }).length;
  }

  // in_flight_pr_count — same DOM-marker convention.
  const prNodes = document.querySelectorAll(
    '[data-in-flight-pr="true"], .in-flight-pr'
  );
  const in_flight_pr_count = prNodes.length;

  // scroll_position — useful for "did the user actually see this card?"
  const scroll_position = {
    x: Math.round(window.scrollX),
    y: Math.round(window.scrollY),
  };

  // viewport_size
  const viewport_size = {
    w: window.innerWidth,
    h: window.innerHeight,
  };

  return {
    current_tab,
    visible_components,
    algedonic_unacked_count,
    in_flight_pr_count,
    scroll_position,
    viewport_size,
    captured_at: new Date().toISOString(),
  };
}

/**
 * Capture a screenshot of the dashboard root via html2canvas. Returns a
 * raw base64 PNG (no data URL prefix) suitable for SignalR transport.
 *
 * fullPage=true captures the entire scrollable height. Otherwise just the
 * viewport — faster, and usually enough.
 */
async function captureDashboardScreenshot(fullPage: boolean): Promise<{ base64: string; format: string }> {
  // Prefer the dashboard root (avoids the breadcrumb chrome). Fall back to
  // document.body if it's not findable.
  const root = document.querySelector('[data-dashboard-root="true"]') as HTMLElement
            ?? document.body;

  const canvas = await html2canvas(root, {
    backgroundColor: '#ffffff',
    // html2canvas honors window.scrollY by default. For a full-page
    // capture we let it span the scrollable area; for the viewport
    // we constrain to window inner dimensions.
    width: fullPage ? root.scrollWidth : Math.min(root.scrollWidth, window.innerWidth),
    height: fullPage ? root.scrollHeight : Math.min(root.scrollHeight, window.innerHeight),
    windowWidth: window.innerWidth,
    windowHeight: window.innerHeight,
    scale: window.devicePixelRatio > 1 ? 1.5 : 1, // cap at 1.5x to keep payload reasonable
    logging: false,
    useCORS: true,
  });

  // toDataURL → strip the "data:image/png;base64," prefix; the hub stores
  // the raw base64 and re-emits it from the MCP tool. Mirrors how
  // GovernanceHub handles Prime Radiant screenshots.
  const dataUrl = canvas.toDataURL('image/png');
  const base64 = dataUrl.substring(dataUrl.indexOf(',') + 1);
  return { base64, format: 'image/png' };
}

/**
 * Mount this inside DevelopmentSection so it only connects when a user is
 * actually on /test#dev/*. The hub disconnects on unmount.
 */
export const McpControlProvider: React.FC<{ children: React.ReactNode }> = ({ children }) => {
  const connectionRef = useRef<signalR.HubConnection | null>(null);

  useEffect(() => {
    let cancelled = false;

    const connection = new signalR.HubConnectionBuilder()
      .withUrl('/hubs/dev-dashboard')
      .withAutomaticReconnect([0, 2000, 5000, 15000, 30000])
      .configureLogging(signalR.LogLevel.Warning)
      .build();

    connectionRef.current = connection;

    // ─── NavigateTo: switch sub-tab via URL hash ───
    // DevelopmentSection already listens for `hashchange`, so setting the
    // hash drives setSubTab without us reaching into its setter.
    connection.on('NavigateTo', (data: { subTab: string }) => {
      if (!data?.subTab) return;
      if (!VALID_TABS.has(data.subTab as DevSubTab)) {
        console.warn('[McpControl] Ignoring NavigateTo with invalid subTab:', data.subTab);
        return;
      }
      const target = `#dev/${data.subTab}`;
      if (window.location.hash !== target) {
        window.location.hash = target;
        // Force a hashchange event in case the browser merged it.
        window.dispatchEvent(new HashChangeEvent('hashchange'));
      }
    });

    // ─── Refresh: bump a global event so fetchers re-pull ───
    connection.on('Refresh', (data: { endpoint?: string | null }) => {
      const detail: DashboardRefreshDetail = {
        endpoint: data?.endpoint ?? null,
        timestamp: new Date().toISOString(),
      };
      window.dispatchEvent(new CustomEvent('mcp:dashboard:refresh', { detail }));
    });

    // ─── RequestState: reply via SubmitState ───
    connection.on('RequestState', async (data: { requestId: string }) => {
      if (!data?.requestId) return;
      try {
        const state = captureDashboardState();
        await connection.invoke('SubmitState', data.requestId, JSON.stringify(state));
      } catch (err) {
        console.warn('[McpControl] SubmitState failed:', err);
      }
    });

    // ─── RequestScreenshot: capture + reply ───
    connection.on('RequestScreenshot', async (data: { requestId: string; fullPage?: boolean }) => {
      if (!data?.requestId) return;
      try {
        const { base64, format } = await captureDashboardScreenshot(Boolean(data.fullPage));
        await connection.invoke('SubmitScreenshot', data.requestId, base64, format);
      } catch (err) {
        console.warn('[McpControl] SubmitScreenshot failed:', err);
      }
    });

    // ─── Start ───
    connection.start()
      .then(() => {
        if (cancelled) {
          connection.stop().catch(() => {});
          return;
        }
        connection.invoke('Subscribe').catch((err) => {
          console.warn('[McpControl] Subscribe failed:', err);
        });
        console.log('[McpControl] Connected to /hubs/dev-dashboard');
      })
      .catch((err) => {
        // Don't spam the console — backend may simply not be running in a
        // GitHub-Pages-only build, and the dashboard still has to render.
        console.warn('[McpControl] SignalR connect failed (dashboard will still work, just no remote control):', err?.message ?? err);
      });

    return () => {
      cancelled = true;
      connection.stop().catch(() => {});
      connectionRef.current = null;
    };
  }, []);

  return <>{children}</>;
};

export default McpControlProvider;
