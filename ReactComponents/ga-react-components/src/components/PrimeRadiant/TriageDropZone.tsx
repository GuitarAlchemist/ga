// src/components/PrimeRadiant/TriageDropZone.tsx
// Universal triage inbox — drag/paste URLs, images, text, files for AI classification.
// Items are triaged as: signal (algedonic), reference (library), task (backlog), or knowledge (seldon).

import React, { useState, useCallback, useRef, useEffect } from 'react';

// ---------------------------------------------------------------------------
// Types
// ---------------------------------------------------------------------------

export type TriageCategory = 'signal' | 'reference' | 'task' | 'knowledge';

export interface TriageItem {
  id: string;
  type: 'url' | 'text' | 'image' | 'file';
  content: string;           // URL string, text content, or filename
  preview?: string;           // truncated preview
  category: TriageCategory | 'pending';
  timestamp: string;
  summary?: string;           // AI-generated summary
}

const CATEGORY_META: Record<TriageCategory, { label: string; icon: string; color: string; panel: string }> = {
  signal:    { label: 'Weak Signal',  icon: '⚡', color: '#FFB300', panel: 'algedonic' },
  reference: { label: 'Reference',    icon: '📚', color: '#4FC3F7', panel: 'library' },
  task:      { label: 'Task',         icon: '📋', color: '#73d13d', panel: 'backlog' },
  knowledge: { label: 'Knowledge',    icon: '🔮', color: '#CE93D8', panel: 'seldon' },
};

// ---------------------------------------------------------------------------
// Auto-classify based on content patterns
// ---------------------------------------------------------------------------

function autoClassify(content: string, type: string): TriageCategory {
  const lower = content.toLowerCase();

  // URL patterns
  if (type === 'url' || lower.startsWith('http')) {
    if (lower.includes('signal') || lower.includes('faible') || lower.includes('weak') || lower.includes('foresight') || lower.includes('futur'))
      return 'signal';
    if (lower.includes('github.com') && lower.includes('issue'))
      return 'task';
    if (lower.includes('arxiv') || lower.includes('paper') || lower.includes('doi.org') || lower.includes('wiki'))
      return 'reference';
    return 'knowledge';
  }

  // Text patterns
  if (lower.includes('bug') || lower.includes('fix') || lower.includes('todo') || lower.includes('should'))
    return 'task';
  if (lower.includes('research') || lower.includes('paper') || lower.includes('according to'))
    return 'reference';
  if (lower.includes('trend') || lower.includes('emerging') || lower.includes('signal') || lower.includes('risk'))
    return 'signal';

  return 'knowledge';
}

function generateId(): string {
  return `triage-${Date.now()}-${Math.random().toString(36).slice(2, 6)}`;
}

function truncate(s: string, n: number): string {
  return s.length > n ? s.slice(0, n - 1) + '…' : s;
}

// ---------------------------------------------------------------------------
// Persistence
// ---------------------------------------------------------------------------

// ---------------------------------------------------------------------------
// AI Summary (best-effort, non-blocking)
// ---------------------------------------------------------------------------

async function fetchSummary(url: string): Promise<string | null> {
  const controller = new AbortController();
  const timeout = setTimeout(() => controller.abort(), 10_000);

  try {
    // Try the chatbot API first
    const res = await fetch('/api/chatbot/ask', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ question: `Summarize this URL in one sentence for governance triage: ${url}` }),
      signal: controller.signal,
    });
    if (res.ok) {
      const data = await res.json();
      const text = data?.answer ?? data?.response ?? data?.message;
      if (typeof text === 'string' && text.trim()) return text.trim();
    }
  } catch { /* fall through */ }

  try {
    // Fallback to chat endpoint
    const res = await fetch('/api/chat', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ message: `Summarize: ${url}` }),
      signal: controller.signal,
    });
    if (res.ok) {
      const data = await res.json();
      const text = data?.answer ?? data?.response ?? data?.message;
      if (typeof text === 'string' && text.trim()) return text.trim();
    }
  } catch { /* fall through */ }

  clearTimeout(timeout);
  return null;
}

// ---------------------------------------------------------------------------
// Persistence
// ---------------------------------------------------------------------------

const STORAGE_KEY = 'ixql-triage-inbox';

function loadItems(): TriageItem[] {
  try {
    const raw = localStorage.getItem(STORAGE_KEY);
    return raw ? JSON.parse(raw) : [];
  } catch { return []; }
}

/** Push an item into the triage inbox from outside the component */
export function pushToTriage(type: TriageItem['type'], content: string, category?: TriageCategory): void {
  const items = loadItems();
  const item: TriageItem = {
    id: generateId(),
    type,
    content,
    preview: truncate(content, 80),
    category: category ?? autoClassify(content, type),
    timestamp: new Date().toISOString(),
  };
  items.unshift(item);
  saveItems(items);
  // Notify the component via custom event
  window.dispatchEvent(new CustomEvent('triage-updated'));
}

function saveItems(items: TriageItem[]): void {
  localStorage.setItem(STORAGE_KEY, JSON.stringify(items.slice(0, 50))); // keep last 50
}

// ---------------------------------------------------------------------------
// Component
// ---------------------------------------------------------------------------

interface TriageDropZoneProps {
  onNavigateToPanel?: (panelId: string) => void;
}

export const TriageDropZone: React.FC<TriageDropZoneProps> = ({ onNavigateToPanel }) => {
  const [items, setItems] = useState<TriageItem[]>(loadItems);
  const [dragOver, setDragOver] = useState(false);
  const [expanded, setExpanded] = useState(false);
  const [pasteMode, setPasteMode] = useState(false);
  const inputRef = useRef<HTMLInputElement>(null);

  // Persist on change
  useEffect(() => { saveItems(items); }, [items]);

  // Listen for external pushToTriage calls
  useEffect(() => {
    const handler = () => { setItems(loadItems()); setExpanded(true); };
    window.addEventListener('triage-updated', handler);
    return () => window.removeEventListener('triage-updated', handler);
  }, []);

  const addItem = useCallback((type: TriageItem['type'], content: string) => {
    const category = autoClassify(content, type);
    const item: TriageItem = {
      id: generateId(),
      type,
      content,
      preview: truncate(content, 80),
      category,
      timestamp: new Date().toISOString(),
    };
    setItems(prev => [item, ...prev]);

    // Best-effort AI summary for URLs
    if (type === 'url') {
      const itemId = item.id;
      // Mark as loading
      setItems(prev => prev.map(i => i.id === itemId ? { ...i, summary: '...' } : i));
      fetchSummary(content).then(summary => {
        if (summary) {
          setItems(prev => prev.map(i => i.id === itemId ? { ...i, summary } : i));
        } else {
          // Remove the loading placeholder
          setItems(prev => prev.map(i => i.id === itemId ? { ...i, summary: undefined } : i));
        }
      });
    }
  }, []);

  // ─── Drop handler ───
  const handleDrop = useCallback((e: React.DragEvent) => {
    e.preventDefault();
    setDragOver(false);
    setExpanded(true);

    // URLs from drag
    const url = e.dataTransfer.getData('text/uri-list') || e.dataTransfer.getData('text/plain');
    if (url && (url.startsWith('http://') || url.startsWith('https://'))) {
      addItem('url', url.trim());
      return;
    }

    // Text
    const text = e.dataTransfer.getData('text/plain');
    if (text && text.trim()) {
      addItem('text', text.trim());
      return;
    }

    // Files
    for (const file of Array.from(e.dataTransfer.files)) {
      if (file.type.startsWith('image/')) {
        addItem('image', file.name);
      } else {
        addItem('file', file.name);
      }
    }
  }, [addItem]);

  // ─── Paste handler ───
  const handlePaste = useCallback(() => {
    const val = inputRef.current?.value?.trim();
    if (!val) return;
    const type = val.startsWith('http') ? 'url' : 'text';
    addItem(type, val);
    if (inputRef.current) inputRef.current.value = '';
    setPasteMode(false);
  }, [addItem]);

  // ─── Reclassify ───
  const reclassify = useCallback((id: string, newCat: TriageCategory) => {
    setItems(prev => prev.map(item =>
      item.id === id ? { ...item, category: newCat } : item,
    ));
  }, []);

  // ─── Remove ───
  const removeItem = useCallback((id: string) => {
    setItems(prev => prev.filter(item => item.id !== id));
  }, []);

  // ─── Navigate to panel ───
  const goToPanel = useCallback((cat: TriageCategory) => {
    if (onNavigateToPanel) onNavigateToPanel(CATEGORY_META[cat].panel);
  }, [onNavigateToPanel]);

  const pendingCount = items.length;

  return (
    <div
      className={`prime-radiant__triage ${dragOver ? 'prime-radiant__triage--dragover' : ''} ${expanded ? 'prime-radiant__triage--expanded' : ''}`}
      onDragOver={(e) => { e.preventDefault(); setDragOver(true); }}
      onDragLeave={() => setDragOver(false)}
      onDrop={handleDrop}
    >
      {/* Collapsed bar */}
      {!expanded && (
        <div
          className="prime-radiant__triage-bar"
          onClick={() => setExpanded(true)}
        >
          <span className="prime-radiant__triage-bar-icon">📥</span>
          <span className="prime-radiant__triage-bar-label">
            {dragOver ? 'Drop here for AI triage' : `Triage Inbox${pendingCount > 0 ? ` (${pendingCount})` : ''}`}
          </span>
          {pendingCount > 0 && (
            <span className="prime-radiant__triage-bar-badge">{pendingCount}</span>
          )}
        </div>
      )}

      {/* Expanded inbox */}
      {expanded && (
        <div className="prime-radiant__triage-panel">
          <div className="prime-radiant__triage-header">
            <span>📥 TRIAGE INBOX</span>
            <div className="prime-radiant__triage-header-actions">
              <button
                className="prime-radiant__triage-paste-btn"
                onClick={() => setPasteMode(!pasteMode)}
                title="Paste URL or text"
              >+</button>
              <button
                className="prime-radiant__triage-close"
                onClick={() => setExpanded(false)}
              >x</button>
            </div>
          </div>

          {/* Paste input */}
          {pasteMode && (
            <div className="prime-radiant__triage-paste">
              <input
                ref={inputRef}
                className="prime-radiant__triage-paste-input"
                placeholder="Paste URL, text, or idea..."
                onKeyDown={(e) => e.key === 'Enter' && handlePaste()}
                autoFocus
              />
              <button className="prime-radiant__triage-paste-go" onClick={handlePaste}>
                Triage
              </button>
            </div>
          )}

          {/* Category legend */}
          <div className="prime-radiant__triage-legend">
            {Object.entries(CATEGORY_META).map(([key, meta]) => (
              <span key={key} className="prime-radiant__triage-legend-item" style={{ color: meta.color }}>
                {meta.icon} {meta.label}
              </span>
            ))}
          </div>

          {/* Items list */}
          <div className="prime-radiant__triage-items">
            {items.length === 0 && (
              <div className="prime-radiant__triage-empty">
                Drop URLs, images, text, or files here for AI triage
              </div>
            )}
            {items.map(item => {
              const cat = item.category === 'pending' ? null : CATEGORY_META[item.category as TriageCategory];
              return (
                <div key={item.id} className="prime-radiant__triage-item">
                  <div className="prime-radiant__triage-item-header">
                    <span className="prime-radiant__triage-item-type">
                      {item.type === 'url' ? '🔗' : item.type === 'image' ? '🖼️' : item.type === 'file' ? '📄' : '💬'}
                    </span>
                    <span className="prime-radiant__triage-item-preview">{item.preview}</span>
                    <button className="prime-radiant__triage-item-remove" onClick={() => removeItem(item.id)}>×</button>
                  </div>
                  {item.summary === '...' && (
                    <div className="prime-radiant__triage-item-summary"><em>Summarizing...</em></div>
                  )}
                  {item.summary && item.summary !== '...' && (
                    <div className="prime-radiant__triage-item-summary">{item.summary}</div>
                  )}
                  <div className="prime-radiant__triage-item-actions">
                    {cat && (
                      <span
                        className="prime-radiant__triage-item-category"
                        style={{ color: cat.color, borderColor: cat.color }}
                        onClick={() => goToPanel(item.category as TriageCategory)}
                        title={`Open ${cat.panel} panel`}
                      >
                        {cat.icon} {cat.label}
                      </span>
                    )}
                    {/* Reclassify buttons */}
                    {(Object.keys(CATEGORY_META) as TriageCategory[])
                      .filter(k => k !== item.category)
                      .map(k => (
                        <button
                          key={k}
                          className="prime-radiant__triage-reclass"
                          onClick={() => reclassify(item.id, k)}
                          title={`Reclassify as ${CATEGORY_META[k].label}`}
                        >
                          {CATEGORY_META[k].icon}
                        </button>
                      ))}
                  </div>
                </div>
              );
            })}
          </div>

          {/* Drop target indicator */}
          {dragOver && (
            <div className="prime-radiant__triage-drop-target">
              Drop to triage
            </div>
          )}
        </div>
      )}
    </div>
  );
};
