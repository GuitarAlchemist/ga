// src/components/PrimeRadiant/AdminInbox.tsx
// Admin Inbox — review triaged items with category sections and actions
// Wired to /api/governance/beliefs + /api/governance/backlog with fallback mode

import React, { useState, useCallback, useEffect, useRef } from 'react';

// ---------------------------------------------------------------------------
// Types
// ---------------------------------------------------------------------------

type TriageCategory = 'urgent' | 'review' | 'deferred' | 'done';
type DataSource = 'live' | 'fallback';

interface InboxItem {
  id: string;
  title: string;
  source: string;
  timestamp: number;        // epoch ms
  category: TriageCategory;
}

interface BeliefResponse {
  id: string;
  name?: string;
  title?: string;
  description?: string;
  status: string;
  source?: string;
  updated_at?: string;
  created_at?: string;
}

interface BacklogResponse {
  id: string;
  title?: string;
  name?: string;
  description?: string;
  source?: string;
  created_at?: string;
}

// ---------------------------------------------------------------------------
// Category config
// ---------------------------------------------------------------------------

const CATEGORY_META: Record<TriageCategory, { label: string; color: string; defaultOpen: boolean }> = {
  urgent:   { label: 'Urgent',   color: '#f85149', defaultOpen: true },
  review:   { label: 'Review',   color: '#d29922', defaultOpen: true },
  deferred: { label: 'Deferred', color: '#8b949e', defaultOpen: true },
  done:     { label: 'Done',     color: '#3fb950', defaultOpen: false },
};

const CATEGORY_ORDER: TriageCategory[] = ['urgent', 'review', 'deferred', 'done'];

const AUTO_REFRESH_MS = 60_000;

// ---------------------------------------------------------------------------
// Fallback data
// ---------------------------------------------------------------------------

function makeFallbackItems(): InboxItem[] {
  const now = Date.now();
  return [
    { id: 'fb-1', title: 'CI build failing on main',       source: 'GitHub Actions',    timestamp: now - 2 * 60_000,     category: 'urgent' },
    { id: 'fb-2', title: 'PR #42 needs review',            source: 'GitHub',            timestamp: now - 12 * 60_000,    category: 'review' },
    { id: 'fb-3', title: 'Update governance beliefs',      source: 'Demerzel recon',    timestamp: now - 25 * 60_000,    category: 'review' },
    { id: 'fb-4', title: 'Refactor BSP room generator',    source: 'Backlog',           timestamp: now - 3 * 3600_000,   category: 'deferred' },
    { id: 'fb-5', title: 'Fixed Ollama model loading',     source: 'Auto-fix',          timestamp: now - 45 * 60_000,    category: 'done' },
  ];
}

// ---------------------------------------------------------------------------
// API helpers
// ---------------------------------------------------------------------------

function beliefToCategory(status: string): TriageCategory {
  const lower = status.toLowerCase();
  if (lower === 'contradictory' || lower === 'unknown') return 'urgent';
  return 'review';
}

function parseTimestamp(dateStr?: string): number {
  if (!dateStr) return Date.now();
  const parsed = new Date(dateStr).getTime();
  return Number.isNaN(parsed) ? Date.now() : parsed;
}

function mapBelief(b: BeliefResponse): InboxItem {
  return {
    id: `belief-${b.id}`,
    title: b.title || b.name || b.description || `Belief ${b.id}`,
    source: b.source || 'Governance beliefs',
    timestamp: parseTimestamp(b.updated_at || b.created_at),
    category: beliefToCategory(b.status),
  };
}

function mapBacklogItem(b: BacklogResponse): InboxItem {
  return {
    id: `backlog-${b.id}`,
    title: b.title || b.name || b.description || `Backlog ${b.id}`,
    source: b.source || 'Governance backlog',
    timestamp: parseTimestamp(b.created_at),
    category: 'deferred',
  };
}

async function fetchGovernanceItems(): Promise<InboxItem[]> {
  const [beliefsRes, backlogRes] = await Promise.all([
    fetch('/api/governance/beliefs'),
    fetch('/api/governance/backlog'),
  ]);

  const items: InboxItem[] = [];

  if (beliefsRes.ok) {
    const beliefs: BeliefResponse[] = await beliefsRes.json();
    items.push(...beliefs.map(mapBelief));
  }

  if (backlogRes.ok) {
    const backlog: BacklogResponse[] = await backlogRes.json();
    items.push(...backlog.map(mapBacklogItem));
  }

  if (items.length === 0) {
    throw new Error('No governance data returned');
  }

  return items;
}

function extractRawId(compositeId: string): string {
  // Strip 'belief-' or 'backlog-' prefix to get the API id
  return compositeId.replace(/^(belief|backlog)-/, '');
}

// ---------------------------------------------------------------------------
// Relative time formatter
// ---------------------------------------------------------------------------

function relativeTime(ts: number): string {
  const diff = Math.max(0, Date.now() - ts);
  const secs = Math.floor(diff / 1000);
  if (secs < 60) return `${secs}s ago`;
  const mins = Math.floor(secs / 60);
  if (mins < 60) return `${mins}m ago`;
  const hours = Math.floor(mins / 60);
  if (hours < 24) return `${hours}h ago`;
  const days = Math.floor(hours / 24);
  return `${days}d ago`;
}

// ---------------------------------------------------------------------------
// Animations
// ---------------------------------------------------------------------------

type ItemAnimation = 'fade-out' | 'shake-out' | null;

// ---------------------------------------------------------------------------
// Component
// ---------------------------------------------------------------------------

export const AdminInbox: React.FC = () => {
  const [items, setItems] = useState<InboxItem[]>(makeFallbackItems);
  const [dataSource, setDataSource] = useState<DataSource>('fallback');
  const [isRefreshing, setIsRefreshing] = useState(false);
  const [collapsedSections, setCollapsedSections] = useState<Set<TriageCategory>>(() => {
    const s = new Set<TriageCategory>();
    for (const cat of CATEGORY_ORDER) {
      if (!CATEGORY_META[cat].defaultOpen) s.add(cat);
    }
    return s;
  });
  const [animating, setAnimating] = useState<Map<string, ItemAnimation>>(new Map());
  const [askingDemerzel, setAskingDemerzel] = useState<Set<string>>(new Set());
  const refreshTimerRef = useRef<ReturnType<typeof setInterval> | null>(null);

  // ------- Fetch governance data -------
  const loadGovernanceData = useCallback(async () => {
    setIsRefreshing(true);
    try {
      const liveItems = await fetchGovernanceItems();
      setItems(liveItems);
      setDataSource('live');
    } catch {
      // Keep existing items (fallback on first load, or stale live data on refresh failure)
      setDataSource(prev => prev === 'live' ? prev : 'fallback');
    } finally {
      setIsRefreshing(false);
    }
  }, []);

  // Initial fetch + auto-refresh every 60s
  useEffect(() => {
    loadGovernanceData();
    refreshTimerRef.current = setInterval(loadGovernanceData, AUTO_REFRESH_MS);
    return () => {
      if (refreshTimerRef.current) clearInterval(refreshTimerRef.current);
    };
  }, [loadGovernanceData]);

  // Tick relative times every 30s
  const [, setTick] = useState(0);
  useEffect(() => {
    const iv = setInterval(() => setTick(t => t + 1), 30_000);
    return () => clearInterval(iv);
  }, []);

  const toggleSection = useCallback((cat: TriageCategory) => {
    setCollapsedSections(prev => {
      const next = new Set(prev);
      if (next.has(cat)) next.delete(cat);
      else next.add(cat);
      return next;
    });
  }, []);

  const animateAndRemove = useCallback((id: string, anim: ItemAnimation, then?: (item: InboxItem) => InboxItem | null) => {
    setAnimating(prev => new Map(prev).set(id, anim));
    setTimeout(() => {
      setAnimating(prev => { const m = new Map(prev); m.delete(id); return m; });
      setItems(prev => {
        if (!then) return prev.filter(i => i.id !== id);
        return prev.flatMap(i => {
          if (i.id !== id) return [i];
          const result = then(i);
          return result ? [result] : [];
        });
      });
    }, 400);
  }, []);

  const handleApprove = useCallback((id: string) => {
    // Fire PUT to mark belief as true, then animate locally
    if (id.startsWith('belief-')) {
      fetch(`/api/governance/beliefs/${extractRawId(id)}`, {
        method: 'PUT',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ status: 'true' }),
      }).catch(() => { /* proceed with local update on failure */ });
    }
    animateAndRemove(id, 'fade-out', item => ({ ...item, category: 'done' }));
  }, [animateAndRemove]);

  const handleReject = useCallback((id: string) => {
    // Fire PUT to mark belief as false, then animate out
    if (id.startsWith('belief-')) {
      fetch(`/api/governance/beliefs/${extractRawId(id)}`, {
        method: 'PUT',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ status: 'false' }),
      }).catch(() => { /* proceed with local removal on failure */ });
    }
    animateAndRemove(id, 'shake-out');
  }, [animateAndRemove]);

  const handleDefer = useCallback((id: string) => {
    animateAndRemove(id, 'fade-out', item => ({ ...item, category: 'deferred' }));
  }, [animateAndRemove]);

  const handleAskDemerzel = useCallback((id: string, title: string) => {
    setAskingDemerzel(prev => new Set(prev).add(id));
    // Fire-and-forget
    fetch('/api/chat', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ message: `Demerzel, what should I do about: ${title}` }),
    }).catch(() => { /* swallow */ });
    setTimeout(() => {
      setAskingDemerzel(prev => { const s = new Set(prev); s.delete(id); return s; });
    }, 2000);
  }, []);

  const handleManualRefresh = useCallback(() => {
    // Reset the auto-refresh timer on manual refresh
    if (refreshTimerRef.current) clearInterval(refreshTimerRef.current);
    loadGovernanceData();
    refreshTimerRef.current = setInterval(loadGovernanceData, AUTO_REFRESH_MS);
  }, [loadGovernanceData]);

  // Group items by category
  const grouped = new Map<TriageCategory, InboxItem[]>();
  for (const cat of CATEGORY_ORDER) grouped.set(cat, []);
  for (const item of items) {
    grouped.get(item.category)?.push(item);
  }

  const pendingCount = items.filter(i => i.category !== 'done').length;

  return (
    <div className="admin-inbox">
      <div className="admin-inbox__header">
        <span className="admin-inbox__title">
          Inbox
          {pendingCount > 0 && (
            <span className="admin-inbox__total-badge">{pendingCount}</span>
          )}
        </span>
        <span className="admin-inbox__header-controls">
          <span
            className={`admin-inbox__source-indicator admin-inbox__source-indicator--${dataSource}`}
            title={dataSource === 'live' ? 'Connected to governance API' : 'Using fallback data'}
          >
            {dataSource === 'live' ? 'Live' : 'Fallback'}
          </span>
          <button
            className="admin-inbox__refresh-btn"
            onClick={handleManualRefresh}
            disabled={isRefreshing}
            title="Refresh governance data"
          >
            {isRefreshing ? '\u21BB' : '\u21BB'}
          </button>
        </span>
      </div>

      <div className="admin-inbox__body">
        {CATEGORY_ORDER.map(cat => {
          const meta = CATEGORY_META[cat];
          const catItems = grouped.get(cat) ?? [];
          const isCollapsed = collapsedSections.has(cat);
          const count = catItems.length;

          return (
            <div key={cat} className="admin-inbox__section">
              <button
                className="admin-inbox__section-header"
                onClick={() => toggleSection(cat)}
                style={{ borderLeftColor: meta.color }}
              >
                <span className="admin-inbox__section-chevron">{isCollapsed ? '\u25B6' : '\u25BC'}</span>
                <span className="admin-inbox__section-label">{meta.label}</span>
                {count > 0 && (
                  <span className="admin-inbox__section-count" style={{ background: meta.color }}>
                    {count}
                  </span>
                )}
              </button>

              {!isCollapsed && (
                <div className="admin-inbox__section-items">
                  {catItems.length === 0 && (
                    <div className="admin-inbox__empty">No items</div>
                  )}
                  {catItems.map(item => {
                    const anim = animating.get(item.id);
                    const asking = askingDemerzel.has(item.id);
                    return (
                      <div
                        key={item.id}
                        className={`admin-inbox__item${anim === 'fade-out' ? ' admin-inbox__item--fade' : ''}${anim === 'shake-out' ? ' admin-inbox__item--shake' : ''}`}
                      >
                        <div className="admin-inbox__item-top">
                          <span
                            className="admin-inbox__category-badge"
                            style={{ background: meta.color }}
                          >
                            {meta.label}
                          </span>
                          <span className="admin-inbox__item-time">{relativeTime(item.timestamp)}</span>
                        </div>
                        <div className="admin-inbox__item-title">{item.title}</div>
                        <div className="admin-inbox__item-source">via {item.source}</div>

                        {cat !== 'done' && (
                          <div className="admin-inbox__actions">
                            <button
                              className="admin-inbox__action admin-inbox__action--approve"
                              onClick={() => handleApprove(item.id)}
                              title="Approve"
                            >
                              &#10003;
                            </button>
                            <button
                              className="admin-inbox__action admin-inbox__action--reject"
                              onClick={() => handleReject(item.id)}
                              title="Reject"
                            >
                              &#10007;
                            </button>
                            <button
                              className="admin-inbox__action admin-inbox__action--defer"
                              onClick={() => handleDefer(item.id)}
                              title="Defer"
                            >
                              &#8987;
                            </button>
                            <button
                              className={`admin-inbox__action admin-inbox__action--ask${asking ? ' admin-inbox__action--asking' : ''}`}
                              onClick={() => handleAskDemerzel(item.id, item.title)}
                              disabled={asking}
                              title="Ask Demerzel"
                            >
                              {asking ? '...' : 'D'}
                            </button>
                          </div>
                        )}
                      </div>
                    );
                  })}
                </div>
              )}
            </div>
          );
        })}
      </div>
    </div>
  );
};
