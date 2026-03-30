// src/components/PrimeRadiant/IxqlGridPanel.tsx
// AG-Grid panel driven by IXQL CREATE PANEL KIND grid.
// Renders a PanelSpec as a fully-featured data grid with:
// - Auto-generated columns from PROJECT clause
// - Polling via REFRESH or live updates via LIVE (SignalR)
// - Tetravalent cell rendering for T/F/U/C values
// - PUBLISH/SUBSCRIBE signal integration
// - GOVERNED BY article badge

import React, { useEffect, useState, useMemo, useCallback, useRef } from 'react';
import { AgGridReact } from 'ag-grid-react';
import type { ColDef, RowClickedEvent, GridReadyEvent, GridApi } from 'ag-grid-community';
import 'ag-grid-community/styles/ag-grid.css';
import 'ag-grid-community/styles/ag-theme-alpine.css';

import type { PanelSpec, CompiledProjectionField } from './IxqlWidgetSpec';
import { applyProjection } from './IxqlWidgetSpec';
import { executePipeline } from './IxqlPipeEngine';
import { resolve } from './DataFetcher';
import type { IxqlPredicate } from './IxqlControlParser';
import { signalBus, useSignals, usePublish } from './DashboardSignalBus';

// ---------------------------------------------------------------------------
// Tetravalent cell renderer
// ---------------------------------------------------------------------------

const TETRAVALENT_COLORS: Record<string, string> = {
  TRUE: '#33CC66',
  FALSE: '#FF4444',
  UNKNOWN: '#888888',
  CONTRADICTORY: '#FF44FF',
  T: '#33CC66',
  F: '#FF4444',
  U: '#888888',
  C: '#FF44FF',
};

const TETRAVALENT_LABELS: Record<string, string> = {
  TRUE: 'T',
  FALSE: 'F',
  UNKNOWN: 'U',
  CONTRADICTORY: 'C',
  T: 'T',
  F: 'F',
  U: 'U',
  C: 'C',
};

function isTetravalent(value: unknown): boolean {
  if (typeof value !== 'string') return false;
  return value.toUpperCase() in TETRAVALENT_COLORS;
}

const TetravalentCellRenderer: React.FC<{ value: unknown }> = ({ value }) => {
  const str = String(value).toUpperCase();
  const color = TETRAVALENT_COLORS[str] ?? '#888';
  const label = TETRAVALENT_LABELS[str] ?? String(value);
  return (
    <span style={{
      display: 'inline-flex',
      alignItems: 'center',
      gap: 4,
    }}>
      <span style={{
        width: 8,
        height: 8,
        borderRadius: '50%',
        backgroundColor: color,
        display: 'inline-block',
        flexShrink: 0,
      }} />
      <span style={{ color, fontWeight: 600 }}>{label}</span>
    </span>
  );
};

// ---------------------------------------------------------------------------
// Confidence bar renderer (0.0–1.0 values)
// ---------------------------------------------------------------------------

const CONFIDENCE_KEYWORDS = ['confidence', 'score', 'resilience', 'staleness'];

function isConfidenceValue(name: string, value: unknown): boolean {
  if (typeof value !== 'number') return false;
  if (value < 0 || value > 1) return false;
  const lower = name.toLowerCase();
  return CONFIDENCE_KEYWORDS.some(kw => lower.includes(kw));
}

const ConfidenceCellRenderer: React.FC<{ value: number }> = ({ value }) => {
  const pct = Math.round(value * 100);
  const color = value >= 0.7 ? '#33CC66' : value >= 0.5 ? '#FFB300' : '#FF4444';
  return (
    <div style={{ display: 'flex', alignItems: 'center', gap: 6, width: '100%' }}>
      <div style={{
        flex: 1,
        height: 6,
        borderRadius: 3,
        backgroundColor: 'rgba(255,255,255,0.1)',
        overflow: 'hidden',
      }}>
        <div style={{
          width: `${pct}%`,
          height: '100%',
          borderRadius: 3,
          backgroundColor: color,
          transition: 'width 0.3s ease',
        }} />
      </div>
      <span style={{ fontSize: 11, color, minWidth: 32, textAlign: 'right' }}>
        {pct}%
      </span>
    </div>
  );
};

// ---------------------------------------------------------------------------
// Governed-by badge
// ---------------------------------------------------------------------------

const GovernedByBadge: React.FC<{ articles: number[] }> = ({ articles }) => {
  if (articles.length === 0) return null;
  return (
    <span
      style={{
        display: 'inline-flex',
        gap: 3,
        marginLeft: 8,
        fontSize: 10,
        opacity: 0.7,
      }}
      title={`Governed by Article${articles.length > 1 ? 's' : ''} ${articles.join(', ')}`}
    >
      {articles.map(a => (
        <span
          key={a}
          style={{
            backgroundColor: 'rgba(136, 136, 255, 0.15)',
            color: '#8888FF',
            borderRadius: 3,
            padding: '1px 4px',
            fontWeight: 600,
          }}
        >
          A{a}
        </span>
      ))}
    </span>
  );
};

// ---------------------------------------------------------------------------
// Column auto-generation
// ---------------------------------------------------------------------------

function buildColDefs(
  projection: PanelSpec['projection'],
  sampleRow: Record<string, unknown> | null,
): ColDef[] {
  // If we have a projection, use it
  if (projection && projection.fields.length > 0) {
    return projection.fields.map((field: CompiledProjectionField) => {
      const col: ColDef = {
        headerName: formatHeaderName(field.name),
        field: field.name,
        sortable: true,
        filter: true,
        resizable: true,
      };
      return col;
    });
  }

  // No projection — infer columns from sample data
  if (!sampleRow) return [];
  return Object.keys(sampleRow).map(key => ({
    headerName: formatHeaderName(key),
    field: key,
    sortable: true,
    filter: true,
    resizable: true,
  }));
}

function formatHeaderName(field: string): string {
  // Split on underscores and camelCase boundaries, then title-case
  const words: string[] = [];
  let current = '';
  for (let i = 0; i < field.length; i++) {
    const ch = field[i];
    if (ch === '_') {
      if (current) { words.push(current); current = ''; }
    } else if (i > 0 && ch >= 'A' && ch <= 'Z' && field[i - 1] >= 'a' && field[i - 1] <= 'z') {
      if (current) words.push(current);
      current = ch;
    } else {
      current += ch;
    }
  }
  if (current) words.push(current);
  return words.map(w => w.charAt(0).toUpperCase() + w.slice(1)).join(' ');
}

// Post-process ColDefs with smart cell renderers based on data
function enhanceColDefs(
  colDefs: ColDef[],
  rows: Record<string, unknown>[],
): ColDef[] {
  if (rows.length === 0) return colDefs;
  const sample = rows[0];

  return colDefs.map(col => {
    const field = col.field;
    if (!field) return col;
    const val = sample[field];

    // Tetravalent detection
    if (isTetravalent(val)) {
      return {
        ...col,
        cellRenderer: (params: { value: unknown }) =>
          React.createElement(TetravalentCellRenderer, { value: params.value }),
        width: 100,
      };
    }

    // Confidence bar detection
    if (isConfidenceValue(field, val)) {
      return {
        ...col,
        cellRenderer: (params: { value: unknown }) =>
          React.createElement(ConfidenceCellRenderer, { value: Number(params.value) }),
        width: 160,
      };
    }

    return col;
  });
}

// ---------------------------------------------------------------------------
// Main component
// ---------------------------------------------------------------------------

export interface IxqlGridPanelProps {
  spec: PanelSpec;
}

export const IxqlGridPanel: React.FC<IxqlGridPanelProps> = ({ spec }) => {
  const [rowData, setRowData] = useState<Record<string, unknown>[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const gridApiRef = useRef<GridApi | null>(null);
  const publishSignal = usePublish(spec.id);

  // Subscribe to signals this panel depends on
  const subscribedSignals = useSignals(spec.subscribe);

  // Fetch data
  const fetchData = useCallback(async () => {
    try {
      const raw = await resolve(
        spec.binding.source,
        spec.binding.wherePredicates as IxqlPredicate[],
      );

      let rows = raw as Record<string, unknown>[];

      // Apply PIPE transforms if defined
      if (spec.pipeline) {
        rows = executePipeline(rows, spec.pipeline.steps);
      }

      // Apply projection if defined
      if (spec.projection) {
        rows = rows.map(row => applyProjection(row, spec.projection!));
      }

      setRowData(rows);
      setError(null);
    } catch (err) {
      setError(String(err));
    } finally {
      setLoading(false);
    }
  }, [spec.binding.source, spec.binding.wherePredicates, spec.projection]);

  // Initial load + polling
  useEffect(() => {
    fetchData();

    if (spec.refresh && spec.refresh > 0) {
      const interval = setInterval(fetchData, spec.refresh);
      return () => clearInterval(interval);
    }
  }, [fetchData, spec.refresh]);

  // Re-fetch when subscribed signals change
  useEffect(() => {
    if (subscribedSignals.size > 0) {
      fetchData();
    }
  }, [subscribedSignals, fetchData]);

  // Build column definitions
  const colDefs = useMemo(() => {
    const base = buildColDefs(spec.projection, rowData[0] ?? null);
    return enhanceColDefs(base, rowData);
  }, [spec.projection, rowData]);

  // Row selection → publish signal
  const onRowClicked = useCallback((event: RowClickedEvent) => {
    if (spec.publish && event.data) {
      publishSignal(spec.publish.as, event.data);
    }
  }, [spec.publish, publishSignal]);

  const onGridReady = useCallback((params: GridReadyEvent) => {
    gridApiRef.current = params.api;
    params.api.sizeColumnsToFit();
  }, []);

  // Default col def for consistent behavior
  const defaultColDef = useMemo<ColDef>(() => ({
    sortable: true,
    filter: true,
    resizable: true,
    minWidth: 80,
  }), []);

  if (loading) {
    return (
      <div className="ixql-grid-panel ixql-grid-panel--loading">
        <div className="ixql-grid-panel__header">
          <span className="ixql-grid-panel__title">{spec.id}</span>
          <GovernedByBadge articles={spec.governedBy} />
        </div>
        <div className="ixql-grid-panel__loading">Loading...</div>
      </div>
    );
  }

  if (error) {
    return (
      <div className="ixql-grid-panel ixql-grid-panel--error">
        <div className="ixql-grid-panel__header">
          <span className="ixql-grid-panel__title">{spec.id}</span>
          <GovernedByBadge articles={spec.governedBy} />
        </div>
        <div className="ixql-grid-panel__error">{error}</div>
      </div>
    );
  }

  return (
    <div className="ixql-grid-panel">
      <div className="ixql-grid-panel__header">
        <span className="ixql-grid-panel__title">{spec.id}</span>
        <span className="ixql-grid-panel__count">{rowData.length} rows</span>
        {spec.live && (
          <span className="ixql-grid-panel__live-badge" title="Live updates via SignalR">
            LIVE
          </span>
        )}
        {spec.pipeline && spec.pipeline.steps.length > 0 && (
          <span className="ixql-grid-panel__pipe-badge"
            title={`${spec.pipeline.steps.length} transform step${spec.pipeline.steps.length > 1 ? 's' : ''}`}>
            PIPE {spec.pipeline.steps.length}
          </span>
        )}
        <GovernedByBadge articles={spec.governedBy} />
        {spec.refresh && (
          <span className="ixql-grid-panel__refresh-badge" title={`Refreshes every ${spec.refresh / 1000}s`}>
            ⟳ {spec.refresh >= 60000 ? `${spec.refresh / 60000}m` : `${spec.refresh / 1000}s`}
          </span>
        )}
      </div>

      <div
        className="ag-theme-alpine-dark ixql-grid-panel__grid"
        style={{ width: '100%', height: 'calc(100% - 32px)' }}
      >
        <AgGridReact
          rowData={rowData}
          columnDefs={colDefs}
          defaultColDef={defaultColDef}
          onGridReady={onGridReady}
          onRowClicked={onRowClicked}
          rowSelection="single"
          animateRows={true}
          pagination={rowData.length > 50}
          paginationPageSize={50}
          domLayout={rowData.length <= 20 ? 'autoHeight' : 'normal'}
          suppressCellFocus={true}
          headerHeight={32}
          rowHeight={28}
        />
      </div>
    </div>
  );
};
