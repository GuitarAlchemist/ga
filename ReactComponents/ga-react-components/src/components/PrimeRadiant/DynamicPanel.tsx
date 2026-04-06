// src/components/PrimeRadiant/DynamicPanel.tsx
// Generic renderer for IXQL-created panels.
// Resolves data via DataFetcher, renders using the layout specified in CREATE PANEL.

import React, { useEffect, useState, useCallback } from 'react';
import { resolve, poll, resolveField, type GraphContext } from './DataFetcher';
import type { IxqlPredicate } from './IxqlControlParser';

// ---------------------------------------------------------------------------
// Types
// ---------------------------------------------------------------------------

export interface DynamicPanelDefinition {
  id: string;
  label: string;
  source: string;
  layout: 'list-detail' | 'dashboard' | 'status' | 'custom';
  wherePredicates: IxqlPredicate[];
  showFields: string[];
  filter: { field: string; mode: 'chips' | 'dropdown' | 'search' } | null;
}

interface DynamicPanelProps {
  definition: DynamicPanelDefinition;
  graphContext?: GraphContext;
  pollIntervalMs?: number;
}

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------

const DEFAULT_POLL_MS = 60_000;

function displayValue(val: unknown): string {
  if (val == null) return '—';
  if (typeof val === 'object') return JSON.stringify(val);
  return String(val);
}

function extractFilterValues(data: unknown[], field: string): string[] {
  const vals = new Set<string>();
  for (const item of data) {
    const v = resolveField(item, field);
    if (v != null) {
      if (Array.isArray(v)) v.forEach(x => vals.add(String(x)));
      else vals.add(String(v));
    }
  }
  return [...vals].sort();
}

function applyFilter(data: unknown[], field: string, selected: Set<string>): unknown[] {
  if (selected.size === 0) return data;
  return data.filter(item => {
    const v = resolveField(item, field);
    if (v == null) return false;
    if (Array.isArray(v)) return v.some(x => selected.has(String(x)));
    return selected.has(String(v));
  });
}

function applySearch(data: unknown[], field: string, query: string): unknown[] {
  if (!query) return data;
  const lower = query.toLowerCase();
  return data.filter(item => {
    const v = resolveField(item, field);
    return v != null && String(v).toLowerCase().includes(lower);
  });
}

// ---------------------------------------------------------------------------
// Layout renderers
// ---------------------------------------------------------------------------

const ListDetailLayout: React.FC<{ data: unknown[]; showFields: string[] }> = ({ data, showFields }) => {
  const [expanded, setExpanded] = useState<Set<number>>(new Set());
  const toggle = useCallback((idx: number) => {
    setExpanded(prev => {
      const next = new Set(prev);
      if (next.has(idx)) next.delete(idx); else next.add(idx);
      return next;
    });
  }, []);

  const titleField = showFields[0] ?? 'name';
  const detailFields = showFields.slice(1);

  return (
    <div className="prime-radiant__dynamic-list">
      {data.map((item, idx) => {
        const title = displayValue(resolveField(item, titleField));
        const isOpen = expanded.has(idx);
        return (
          <div key={idx} className="prime-radiant__dynamic-list-item">
            <div
              className="prime-radiant__dynamic-list-header"
              onClick={() => toggle(idx)}
            >
              <span className="prime-radiant__dynamic-list-title">{title}</span>
              <span className="prime-radiant__dynamic-list-toggle">{isOpen ? '▼' : '▶'}</span>
            </div>
            {isOpen && detailFields.length > 0 && (
              <div className="prime-radiant__dynamic-list-detail">
                {detailFields.map(field => (
                  <div key={field} className="prime-radiant__dynamic-list-field">
                    <span className="prime-radiant__dynamic-list-field-label">{field}</span>
                    <span className="prime-radiant__dynamic-list-field-value">
                      {displayValue(resolveField(item, field))}
                    </span>
                  </div>
                ))}
              </div>
            )}
          </div>
        );
      })}
    </div>
  );
};

const DashboardLayout: React.FC<{ data: unknown[]; showFields: string[] }> = ({ data, showFields }) => {
  const labelField = showFields[0] ?? 'name';
  const valueFields = showFields.slice(1);

  return (
    <div className="prime-radiant__dynamic-dashboard">
      {data.map((item, idx) => (
        <div key={idx} className="prime-radiant__dynamic-card">
          <div className="prime-radiant__dynamic-card-label">
            {displayValue(resolveField(item, labelField))}
          </div>
          {valueFields.map(field => (
            <div key={field} className="prime-radiant__dynamic-card-metric">
              <span className="prime-radiant__dynamic-card-metric-label">{field}</span>
              <span className="prime-radiant__dynamic-card-metric-value">
                {displayValue(resolveField(item, field))}
              </span>
            </div>
          ))}
        </div>
      ))}
    </div>
  );
};

const STATUS_DOT_COLORS: Record<string, string> = {
  active: '#33CC66',
  ok: '#33CC66',
  healthy: '#33CC66',
  warn: '#FFB300',
  warning: '#FFB300',
  limited: '#FFB300',
  error: '#FF4444',
  critical: '#FF0000',
  depleted: '#FF4444',
  offline: '#484f58',
};

const StatusLayout: React.FC<{ data: unknown[]; showFields: string[] }> = ({ data, showFields }) => {
  const nameField = showFields[0] ?? 'name';
  const statusField = showFields[1] ?? 'status';
  const metaFields = showFields.slice(2);

  return (
    <div className="prime-radiant__dynamic-status">
      {data.map((item, idx) => {
        const name = displayValue(resolveField(item, nameField));
        const status = String(resolveField(item, statusField) ?? 'ok').toLowerCase();
        const dotColor = STATUS_DOT_COLORS[status] ?? '#484f58';
        return (
          <div key={idx} className="prime-radiant__dynamic-status-row">
            <span
              className="prime-radiant__dynamic-status-dot"
              style={{ backgroundColor: dotColor }}
            />
            <span className="prime-radiant__dynamic-status-name">{name}</span>
            <span className="prime-radiant__dynamic-status-label">{status}</span>
            {metaFields.map(field => (
              <span key={field} className="prime-radiant__dynamic-status-meta">
                {displayValue(resolveField(item, field))}
              </span>
            ))}
          </div>
        );
      })}
    </div>
  );
};

// ---------------------------------------------------------------------------
// Filter UI
// ---------------------------------------------------------------------------

const FilterChips: React.FC<{
  values: string[];
  selected: Set<string>;
  onToggle: (val: string) => void;
}> = ({ values, selected, onToggle }) => (
  <div className="prime-radiant__dynamic-filter-chips">
    {values.map(v => (
      <button
        key={v}
        className={`prime-radiant__dynamic-chip ${selected.has(v) ? 'prime-radiant__dynamic-chip--active' : ''}`}
        onClick={() => onToggle(v)}
      >
        {v}
      </button>
    ))}
  </div>
);

const FilterDropdown: React.FC<{
  values: string[];
  selected: Set<string>;
  onToggle: (val: string) => void;
}> = ({ values, selected, onToggle }) => (
  <select
    className="prime-radiant__dynamic-filter-dropdown"
    value={selected.size === 1 ? [...selected][0] : ''}
    onChange={e => onToggle(e.target.value)}
  >
    <option value="">All</option>
    {values.map(v => <option key={v} value={v}>{v}</option>)}
  </select>
);

const FilterSearch: React.FC<{
  query: string;
  onChange: (val: string) => void;
}> = ({ query, onChange }) => (
  <input
    className="prime-radiant__dynamic-filter-search"
    type="text"
    placeholder="Search..."
    value={query}
    onChange={e => onChange(e.target.value)}
  />
);

// ---------------------------------------------------------------------------
// Main component
// ---------------------------------------------------------------------------

export const DynamicPanel: React.FC<DynamicPanelProps> = ({
  definition,
  graphContext,
  pollIntervalMs = DEFAULT_POLL_MS,
}) => {
  const [data, setData] = useState<unknown[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(false);
  const [filterSelected, setFilterSelected] = useState<Set<string>>(new Set());
  const [searchQuery, setSearchQuery] = useState('');

  // Fetch + poll
  useEffect(() => {
    let mounted = true;

    const load = async () => {
      const result = await resolve(definition.source, definition.wherePredicates, graphContext);
      if (!mounted) return;
      setData(result);
      setLoading(false);
      setError(result.length === 0 && definition.source.startsWith('/api'));
    };

    load();

    const unsub = poll(
      definition.source,
      pollIntervalMs,
      (result) => {
        if (mounted) {
          setData(result);
          setError(false);
        }
      },
      definition.wherePredicates,
      graphContext,
    );

    return () => {
      mounted = false;
      unsub();
    };
  }, [definition.source, definition.wherePredicates, graphContext, pollIntervalMs]);

  // Filter data
  const filteredData = (() => {
    if (!definition.filter) return data;
    if (definition.filter.mode === 'search') {
      return applySearch(data, definition.filter.field, searchQuery);
    }
    return applyFilter(data, definition.filter.field, filterSelected);
  })();

  const filterValues = definition.filter && definition.filter.mode !== 'search'
    ? extractFilterValues(data, definition.filter.field)
    : [];

  const toggleFilter = useCallback((val: string) => {
    setFilterSelected(prev => {
      if (definition.filter?.mode === 'dropdown') {
        // Dropdown: single select (toggle off if same)
        return prev.has(val) ? new Set() : new Set([val]);
      }
      // Chips: multi select
      const next = new Set(prev);
      if (next.has(val)) next.delete(val); else next.add(val);
      return next;
    });
  }, [definition.filter?.mode]);

  // Loading state
  if (loading) {
    return (
      <div className="prime-radiant__dynamic-panel">
        <div className="prime-radiant__dynamic-panel-header">
          <span className="prime-radiant__dynamic-panel-title">{definition.label}</span>
        </div>
        <div className="prime-radiant__dynamic-panel-loading">Loading...</div>
      </div>
    );
  }

  // Error state
  if (error && data.length === 0) {
    return (
      <div className="prime-radiant__dynamic-panel">
        <div className="prime-radiant__dynamic-panel-header">
          <span className="prime-radiant__dynamic-panel-title">{definition.label}</span>
        </div>
        <div className="prime-radiant__dynamic-panel-error">Failed to load data</div>
      </div>
    );
  }

  // Render layout
  const renderLayout = () => {
    switch (definition.layout) {
      case 'list-detail':
        return <ListDetailLayout data={filteredData} showFields={definition.showFields} />;
      case 'dashboard':
        return <DashboardLayout data={filteredData} showFields={definition.showFields} />;
      case 'status':
        return <StatusLayout data={filteredData} showFields={definition.showFields} />;
      case 'custom':
        return <div className="prime-radiant__dynamic-panel-custom">Custom layout — no renderer</div>;
    }
  };

  return (
    <div className="prime-radiant__dynamic-panel">
      <div className="prime-radiant__dynamic-panel-header">
        <span className="prime-radiant__dynamic-panel-title">{definition.label}</span>
        <span className="prime-radiant__dynamic-panel-count">{filteredData.length}</span>
      </div>

      {/* Filter UI */}
      {definition.filter && definition.filter.mode === 'chips' && (
        <FilterChips values={filterValues} selected={filterSelected} onToggle={toggleFilter} />
      )}
      {definition.filter && definition.filter.mode === 'dropdown' && (
        <FilterDropdown values={filterValues} selected={filterSelected} onToggle={toggleFilter} />
      )}
      {definition.filter && definition.filter.mode === 'search' && (
        <FilterSearch query={searchQuery} onChange={setSearchQuery} />
      )}

      {/* Data */}
      {filteredData.length === 0 ? (
        <div className="prime-radiant__dynamic-panel-empty">No data available</div>
      ) : (
        renderLayout()
      )}
    </div>
  );
};
