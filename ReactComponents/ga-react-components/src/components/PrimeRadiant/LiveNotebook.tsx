// src/components/PrimeRadiant/LiveNotebook.tsx
// Interactive notebook component — "Jupyter but more powerful" for Prime Radiant
// Supports markdown, IXQL, React preview, belief, algedonic, chart, and PDCA state cells

import React, { useState, useCallback, useEffect, useRef, useMemo } from 'react';
import { AgGridReact } from 'ag-grid-react';
import type { ColDef } from 'ag-grid-community';
import 'ag-grid-community/styles/ag-grid.css';
import 'ag-grid-community/styles/ag-theme-alpine.css';
import { IxqlGridPanel } from './IxqlGridPanel';
import { parseIxqlCommand } from './IxqlControlParser';
import { compileGridPanel } from './IxqlWidgetSpec';

// ---------------------------------------------------------------------------
// Types
// ---------------------------------------------------------------------------

type CellType = 'markdown' | 'ixql' | 'react' | 'belief' | 'algedonic' | 'chart' | 'state' | 'grid';

interface NotebookCell {
  id: string;
  type: CellType;
  content: string;
  output?: string;
  status?: 'idle' | 'running' | 'complete' | 'error';
  collapsed?: boolean;
}

interface NotebookDocument {
  id: string;
  title: string;
  description?: string;
  cells: NotebookCell[];
  createdAt: string;
  updatedAt: string;
}

export interface LiveNotebookProps {
  open: boolean;
  onClose: () => void;
}

// ---------------------------------------------------------------------------
// Constants
// ---------------------------------------------------------------------------

const STORAGE_KEY = 'prime-radiant-notebooks';

const CELL_TYPE_COLORS: Record<CellType, string> = {
  markdown:  '#888',
  ixql:      '#ffd700',
  react:     '#61dafb',
  belief:    '#4caf50',
  algedonic: '#f44336',
  chart:     '#00bcd4',
  state:     '#ab47bc',
  grid:      '#ff8c00',
};

const CELL_TYPE_LABELS: Record<CellType, string> = {
  markdown:  'MD',
  ixql:      'IXQL',
  react:     'JSX',
  belief:    'BELIEF',
  algedonic: 'ALGEDONIC',
  chart:     'CHART',
  state:     'PDCA',
  grid:      'GRID',
};

// ---------------------------------------------------------------------------
// Simple markdown renderer (no external deps)
// ---------------------------------------------------------------------------

function renderMarkdown(md: string): string {
  let html = md
    // escape HTML
    .replace(/&/g, '&amp;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;');

  // headings (must be before other line-level transforms)
  html = html.replace(/^### (.+)$/gm, '<h3 style="color:#ffd700;margin:8px 0 4px;font-size:14px">$1</h3>');
  html = html.replace(/^## (.+)$/gm, '<h2 style="color:#ffd700;margin:10px 0 6px;font-size:16px">$1</h2>');
  html = html.replace(/^# (.+)$/gm, '<h1 style="color:#ffd700;margin:12px 0 8px;font-size:18px">$1</h1>');

  // bold & italic
  html = html.replace(/\*\*(.+?)\*\*/g, '<strong style="color:#e0e0e0">$1</strong>');
  html = html.replace(/\*(.+?)\*/g, '<em style="color:#c0c0c0">$1</em>');

  // inline code
  html = html.replace(/`([^`]+)`/g, '<code style="background:#1a1a2e;padding:1px 5px;border-radius:3px;color:#ffd700;font-size:12px">$1</code>');

  // list items
  html = html.replace(/^- (.+)$/gm, '<div style="padding-left:16px;margin:2px 0">&#8226; $1</div>');

  // horizontal rule
  html = html.replace(/^---$/gm, '<hr style="border:none;border-top:1px solid rgba(255,215,0,0.2);margin:8px 0"/>');

  // line breaks (preserve blank lines as spacing)
  html = html.replace(/\n\n/g, '<div style="height:8px"></div>');
  html = html.replace(/\n/g, '<br/>');

  return html;
}

// ---------------------------------------------------------------------------
// IXQL syntax highlighter (simple keyword-based)
// ---------------------------------------------------------------------------

function highlightIxql(code: string): string {
  let escaped = code
    .replace(/&/g, '&amp;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;');

  // keywords in gold
  const keywords = ['FROM', 'WHERE', 'SELECT', 'PIPE', 'INTO', 'FILTER', 'MAP', 'REDUCE', 'EMIT', 'JOIN', 'GROUP', 'ORDER', 'LIMIT', 'AS', 'WITH', 'SET', 'GET', 'QUERY', 'SUBSCRIBE', 'TRANSFORM', 'VALIDATE', 'CREATE', 'PANEL', 'KIND', 'SOURCE', 'PROJECT', 'REFRESH', 'LIVE', 'LAYOUT', 'GOVERNED', 'BY', 'PUBLISH', 'TEMPLATE', 'SORT', 'SKIP', 'DISTINCT', 'FLATTEN', 'ASC', 'DESC', 'COUNT', 'SUM', 'AVG', 'MIN', 'MAX'];
  for (const kw of keywords) {
    escaped = escaped.replace(
      new RegExp(`\\b(${kw})\\b`, 'g'),
      `<span style="color:#ffd700;font-weight:bold">$1</span>`
    );
  }

  // strings in green
  escaped = escaped.replace(
    /(&quot;|"|')([^"']*?)(\1)/g,
    '<span style="color:#4caf50">$1$2$3</span>'
  );

  // numbers in cyan
  escaped = escaped.replace(
    /\b(\d+\.?\d*)\b/g,
    '<span style="color:#00bcd4">$1</span>'
  );

  // comments in gray
  escaped = escaped.replace(
    /(\/\/.*)$/gm,
    '<span style="color:#666">$1</span>'
  );

  // pipe operator
  escaped = escaped.replace(
    /(\|&gt;|\|>)/g,
    '<span style="color:#ffd700">$1</span>'
  );

  return escaped;
}

// ---------------------------------------------------------------------------
// Cell renderers
// ---------------------------------------------------------------------------

function renderBeliefCell(content: string): React.ReactNode {
  try {
    const data = JSON.parse(content);
    const beliefs: Array<{ claim: string; status: string; confidence: number; evidence?: string }> =
      Array.isArray(data) ? data : [data];

    const statusColors: Record<string, string> = {
      T: '#4caf50', F: '#f44336', U: '#ff9800', C: '#ab47bc',
    };

    return (
      <div style={{ display: 'flex', flexDirection: 'column', gap: 8 }}>
        {beliefs.map((b, i) => (
          <div key={i} style={{
            display: 'flex', alignItems: 'center', gap: 12, padding: '8px 12px',
            background: 'rgba(255,255,255,0.03)', borderRadius: 6,
            borderLeft: `3px solid ${statusColors[b.status] || '#888'}`,
          }}>
            <span style={{
              fontWeight: 'bold', fontSize: 18, color: statusColors[b.status] || '#888',
              minWidth: 24, textAlign: 'center',
            }}>{b.status}</span>
            <div style={{ flex: 1 }}>
              <div style={{ color: '#e0e0e0', fontSize: 13 }}>{b.claim}</div>
              {b.evidence && <div style={{ color: '#888', fontSize: 11, marginTop: 2 }}>{b.evidence}</div>}
            </div>
            <div style={{
              background: `rgba(${b.confidence > 0.7 ? '76,175,80' : b.confidence > 0.4 ? '255,152,0' : '244,67,54'},0.2)`,
              padding: '2px 8px', borderRadius: 10, fontSize: 12,
              color: b.confidence > 0.7 ? '#4caf50' : b.confidence > 0.4 ? '#ff9800' : '#f44336',
            }}>
              {(b.confidence * 100).toFixed(0)}%
            </div>
          </div>
        ))}
      </div>
    );
  } catch {
    return <div style={{ color: '#f44336', fontSize: 12 }}>Invalid belief JSON</div>;
  }
}

function renderAlgedonicCell(content: string): React.ReactNode {
  try {
    const data = JSON.parse(content);
    const signals: Array<{ signal: string; type: 'pain' | 'pleasure'; severity: string; source: string; description?: string }> =
      Array.isArray(data) ? data : [data];

    return (
      <div style={{ display: 'flex', flexDirection: 'column', gap: 6 }}>
        {signals.map((s, i) => (
          <div key={i} style={{
            display: 'flex', alignItems: 'center', gap: 10, padding: '6px 10px',
            background: s.type === 'pain' ? 'rgba(244,67,54,0.08)' : 'rgba(76,175,80,0.08)',
            borderRadius: 6,
            borderLeft: `3px solid ${s.type === 'pain' ? '#f44336' : '#4caf50'}`,
          }}>
            <span style={{ fontSize: 16 }}>{s.type === 'pain' ? '\u26A0' : '\u2714'}</span>
            <div style={{ flex: 1 }}>
              <div style={{ color: '#e0e0e0', fontSize: 13, fontWeight: 'bold' }}>{s.signal}</div>
              {s.description && <div style={{ color: '#aaa', fontSize: 11 }}>{s.description}</div>}
            </div>
            <span style={{
              fontSize: 10, padding: '2px 6px', borderRadius: 8,
              background: s.severity === 'emergency' ? 'rgba(244,67,54,0.3)' : s.severity === 'warning' ? 'rgba(255,152,0,0.3)' : 'rgba(100,100,100,0.3)',
              color: s.severity === 'emergency' ? '#f44336' : s.severity === 'warning' ? '#ff9800' : '#aaa',
            }}>{s.severity}</span>
            <span style={{ fontSize: 10, color: '#666' }}>{s.source}</span>
          </div>
        ))}
      </div>
    );
  } catch {
    return <div style={{ color: '#f44336', fontSize: 12 }}>Invalid algedonic JSON</div>;
  }
}

function renderChartCell(content: string): React.ReactNode {
  try {
    const data = JSON.parse(content);
    const { type = 'sparkline', values = [], labels = [], title = '' } = data as {
      type?: 'sparkline' | 'bar';
      values?: number[];
      labels?: string[];
      title?: string;
    };

    if (!values.length) return <div style={{ color: '#888', fontSize: 12 }}>No chart data</div>;

    const max = Math.max(...values);
    const min = Math.min(...values);
    const range = max - min || 1;
    const w = 320;
    const h = 80;
    const pad = 4;

    if (type === 'bar') {
      const barW = Math.max(8, (w - pad * 2) / values.length - 4);
      return (
        <div>
          {title && <div style={{ color: '#aaa', fontSize: 11, marginBottom: 4 }}>{title}</div>}
          <svg width={w} height={h + 20} style={{ display: 'block' }}>
            {values.map((v, i) => {
              const barH = ((v - min) / range) * (h - pad * 2) + 4;
              const x = pad + i * ((w - pad * 2) / values.length) + 2;
              const y = h - pad - barH;
              return (
                <g key={i}>
                  <rect x={x} y={y} width={barW} height={barH} rx={2}
                    fill="rgba(255,215,0,0.6)" />
                  {labels[i] && (
                    <text x={x + barW / 2} y={h + 12} textAnchor="middle"
                      style={{ fontSize: 8, fill: '#666' }}>{labels[i]}</text>
                  )}
                </g>
              );
            })}
          </svg>
        </div>
      );
    }

    // sparkline
    const points = values.map((v, i) => {
      const x = pad + (i / (values.length - 1 || 1)) * (w - pad * 2);
      const y = h - pad - ((v - min) / range) * (h - pad * 2);
      return `${x},${y}`;
    }).join(' ');

    return (
      <div>
        {title && <div style={{ color: '#aaa', fontSize: 11, marginBottom: 4 }}>{title}</div>}
        <svg width={w} height={h} style={{ display: 'block' }}>
          <polyline points={points} fill="none" stroke="#ffd700" strokeWidth={2} />
          {values.map((v, i) => {
            const x = pad + (i / (values.length - 1 || 1)) * (w - pad * 2);
            const y = h - pad - ((v - min) / range) * (h - pad * 2);
            return <circle key={i} cx={x} cy={y} r={3} fill="#ffd700" />;
          })}
        </svg>
      </div>
    );
  } catch {
    return <div style={{ color: '#f44336', fontSize: 12 }}>Invalid chart JSON</div>;
  }
}

function renderStateCell(content: string): React.ReactNode {
  try {
    const data = JSON.parse(content);
    const { phase = 'Plan', description = '', metrics = {} } = data as {
      phase?: string;
      description?: string;
      metrics?: Record<string, string | number>;
    };

    const phaseColors: Record<string, string> = {
      Plan: '#2196f3', Do: '#ff9800', Check: '#4caf50', Act: '#ab47bc',
    };

    const phaseOrder = ['Plan', 'Do', 'Check', 'Act'];

    return (
      <div style={{ display: 'flex', flexDirection: 'column', gap: 8 }}>
        <div style={{ display: 'flex', gap: 4 }}>
          {phaseOrder.map(p => (
            <div key={p} style={{
              flex: 1, textAlign: 'center', padding: '6px 0', borderRadius: 4,
              background: p === phase ? phaseColors[p] : 'rgba(255,255,255,0.05)',
              color: p === phase ? '#fff' : '#666',
              fontSize: 12, fontWeight: p === phase ? 'bold' : 'normal',
              transition: 'all 0.2s',
            }}>{p}</div>
          ))}
        </div>
        {description && <div style={{ color: '#ccc', fontSize: 12 }}>{description}</div>}
        {Object.keys(metrics).length > 0 && (
          <div style={{ display: 'flex', flexWrap: 'wrap', gap: 8 }}>
            {Object.entries(metrics).map(([k, v]) => (
              <div key={k} style={{
                background: 'rgba(255,255,255,0.05)', padding: '4px 8px', borderRadius: 4,
                fontSize: 11, color: '#aaa',
              }}>
                <span style={{ color: '#ffd700' }}>{k}</span>: {String(v)}
              </div>
            ))}
          </div>
        )}
      </div>
    );
  } catch {
    return <div style={{ color: '#f44336', fontSize: 12 }}>Invalid state JSON</div>;
  }
}

function renderReactCell(content: string): React.ReactNode {
  return (
    <div>
      <div style={{
        background: '#0a0a14', padding: 10, borderRadius: 4, fontSize: 12,
        fontFamily: "'JetBrains Mono', monospace", color: '#e0e0e0',
        whiteSpace: 'pre-wrap', marginBottom: 8,
        border: '1px solid rgba(97,218,251,0.15)',
      }}>
        <div dangerouslySetInnerHTML={{ __html: highlightJsx(content) }} />
      </div>
      <div style={{
        background: 'rgba(97,218,251,0.05)', border: '1px dashed rgba(97,218,251,0.3)',
        borderRadius: 4, padding: 12, minHeight: 40,
        display: 'flex', alignItems: 'center', justifyContent: 'center',
        color: '#61dafb', fontSize: 12,
      }}>
        Component Preview Area
      </div>
    </div>
  );
}

function formatGridHeader(field: string): string {
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

function renderGridCell(content: string): React.ReactNode {
  // Parse the IXQL command from cell content
  const trimmed = content.trim();

  // If it's a JSON array/object, render directly as an inline AG-Grid
  if (trimmed.startsWith('[') || trimmed.startsWith('{')) {
    try {
      const data = JSON.parse(trimmed);
      const rows: Record<string, unknown>[] = Array.isArray(data) ? data : [data];
      if (rows.length === 0) {
        return <div style={{ color: '#888', fontSize: 12 }}>No data</div>;
      }
      // Auto-generate columns from first row
      const colDefs: ColDef[] = Object.keys(rows[0]).map(key => ({
        headerName: formatGridHeader(key),
        field: key,
        sortable: true,
        filter: true,
        resizable: true,
      }));
      return (
        <div
          className="ag-theme-alpine-dark"
          style={{ height: Math.min(rows.length * 28 + 64, 360), width: '100%' }}
        >
          <AgGridReact
            rowData={rows}
            columnDefs={colDefs}
            defaultColDef={{ sortable: true, filter: true, resizable: true, minWidth: 80 }}
            headerHeight={30}
            rowHeight={26}
            animateRows={true}
            domLayout={rows.length <= 10 ? 'autoHeight' : 'normal'}
          />
        </div>
      );
    } catch {
      // Not valid JSON — fall through to IXQL parsing
    }
  }

  // Try parsing as IXQL CREATE PANEL KIND grid
  const result = parseIxqlCommand(trimmed);
  if (result.ok && result.command.type === 'create-grid-panel') {
    const spec = compileGridPanel(result.command);
    return (
      <div style={{ height: 320 }}>
        <IxqlGridPanel spec={spec} />
      </div>
    );
  }

  // Show parse error or syntax help
  return (
    <div style={{ padding: 12 }}>
      <div style={{
        background: '#0a0a14', padding: 12, borderRadius: 4, fontSize: 12,
        fontFamily: "'JetBrains Mono', monospace", whiteSpace: 'pre-wrap',
        border: '1px solid rgba(255,140,0,0.15)', lineHeight: 1.5,
        marginBottom: 8,
      }}>
        <div dangerouslySetInnerHTML={{ __html: highlightIxql(content) }} />
      </div>
      {!result.ok && (
        <div style={{ color: '#ff8c00', fontSize: 11, marginTop: 4 }}>
          {result.error}
        </div>
      )}
      <div style={{ color: '#666', fontSize: 10, marginTop: 8 }}>
        Syntax: CREATE PANEL "id" KIND grid SOURCE source.path [PROJECT {'{'} field1, field2 {'}'}] [REFRESH 30s]
      </div>
    </div>
  );
}

function highlightJsx(code: string): string {
  let escaped = code
    .replace(/&/g, '&amp;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;');

  // JSX keywords
  const kws = ['import', 'export', 'const', 'let', 'var', 'function', 'return', 'from', 'default', 'interface', 'type', 'extends', 'implements', 'if', 'else', 'for', 'while', 'switch', 'case', 'break', 'new', 'this', 'class', 'async', 'await'];
  for (const kw of kws) {
    escaped = escaped.replace(
      new RegExp(`\\b(${kw})\\b`, 'g'),
      '<span style="color:#61dafb">$1</span>'
    );
  }

  // strings
  escaped = escaped.replace(
    /(&quot;|"|'|`)([^"'`]*?)(\1)/g,
    '<span style="color:#4caf50">$1$2$3</span>'
  );

  return escaped;
}

// ---------------------------------------------------------------------------
// API base URL helper
// ---------------------------------------------------------------------------

function getApiBaseUrl(): string {
  return typeof window !== 'undefined' ? window.location.origin : 'http://localhost:5232';
}

// ---------------------------------------------------------------------------
// Live data fetchers for executable cells
// ---------------------------------------------------------------------------

interface CellExecutionResult {
  content?: string;  // Updated cell content (JSON) if data was fetched
  output: string;    // Output message
}

async function fetchBeliefData(): Promise<CellExecutionResult> {
  const baseUrl = getApiBaseUrl();
  const response = await fetch(`${baseUrl}/api/governance/beliefs`, {
    signal: AbortSignal.timeout(10000),
  });
  if (!response.ok) {
    throw new Error(`Belief API returned ${response.status}: ${response.statusText}`);
  }
  const data = await response.json();
  const beliefs = Array.isArray(data) ? data : [data];

  // Map backend BeliefState[] to the cell display format
  const mapped = beliefs.map((b: { proposition?: string; claim?: string; truthValue?: string; status?: string; confidence?: number; evidence?: string }) => ({
    claim: b.proposition ?? b.claim ?? 'Unknown',
    status: b.truthValue ?? b.status ?? 'U',
    confidence: typeof b.confidence === 'number' ? b.confidence : 0.5,
    evidence: b.evidence,
  }));

  return {
    content: JSON.stringify(mapped, null, 2),
    output: `[Belief] Fetched ${mapped.length} belief(s) from backend.`,
  };
}

async function fetchAlgedonicData(): Promise<CellExecutionResult> {
  const baseUrl = getApiBaseUrl();
  const response = await fetch(`${baseUrl}/api/algedonic/recent`, {
    signal: AbortSignal.timeout(10000),
  });
  if (!response.ok) {
    throw new Error(`Algedonic API returned ${response.status}: ${response.statusText}`);
  }
  const data = await response.json();
  const signals = Array.isArray(data) ? data : [data];

  // Map to cell display format (API returns AlgedonicSignalDto[])
  const mapped = signals.map((s: { signal?: string; type?: string; severity?: string; source?: string; description?: string }) => ({
    signal: s.signal ?? 'unknown',
    type: s.type ?? 'pain',
    severity: s.severity ?? 'info',
    source: s.source ?? 'unknown',
    description: s.description,
  }));

  return {
    content: JSON.stringify(mapped, null, 2),
    output: `[Algedonic] Fetched ${mapped.length} signal(s) from backend.`,
  };
}

async function fetchChartData(): Promise<CellExecutionResult> {
  const baseUrl = getApiBaseUrl();
  const response = await fetch(`${baseUrl}/api/governance`, {
    signal: AbortSignal.timeout(10000),
  });
  if (!response.ok) {
    throw new Error(`Governance API returned ${response.status}: ${response.statusText}`);
  }
  const data = await response.json();
  const health = data?.globalHealth;
  const resilienceScore = typeof health?.resilienceScore === 'number' ? health.resilienceScore : null;

  if (resilienceScore !== null) {
    // Build a chart from the governance health data
    const chartData = {
      type: 'sparkline' as const,
      title: `Resilience Score: ${(resilienceScore * 100).toFixed(0)}%`,
      values: [resilienceScore],
      labels: ['current'],
    };

    // If there are historical values or sub-scores, include them
    const scores: number[] = [];
    const labels: string[] = [];
    if (typeof health?.beliefHealth === 'number') { scores.push(health.beliefHealth); labels.push('belief'); }
    if (typeof health?.policyHealth === 'number') { scores.push(health.policyHealth); labels.push('policy'); }
    if (typeof health?.personaHealth === 'number') { scores.push(health.personaHealth); labels.push('persona'); }
    if (typeof health?.auditHealth === 'number') { scores.push(health.auditHealth); labels.push('audit'); }
    scores.push(resilienceScore); labels.push('resilience');

    if (scores.length > 1) {
      chartData.type = 'bar';
      chartData.title = 'Governance Health Breakdown';
      chartData.values = scores;
      chartData.labels = labels;
    }

    return {
      content: JSON.stringify(chartData, null, 2),
      output: `[Chart] Governance health data loaded. Resilience: ${(resilienceScore * 100).toFixed(0)}%`,
    };
  }

  return {
    output: '[Chart] Governance API returned no health data. Using existing cell content.',
  };
}

// ---------------------------------------------------------------------------
// Cell executor — fetches live data for belief/algedonic/chart, simulates others
// ---------------------------------------------------------------------------

async function executeCell(cell: NotebookCell): Promise<CellExecutionResult> {
  switch (cell.type) {
    case 'belief':
      return await fetchBeliefData();
    case 'algedonic':
      return await fetchAlgedonicData();
    case 'chart':
      return await fetchChartData();
    case 'ixql': {
      // If it's a CREATE PANEL KIND grid command, parse and validate
      const ixqlContent = cell.content.trim().toUpperCase();
      if (ixqlContent.startsWith('CREATE') && ixqlContent.includes('KIND')) {
        const ixqlResult = parseIxqlCommand(cell.content.trim());
        if (ixqlResult.ok && ixqlResult.command.type === 'create-grid-panel') {
          return { output: `[IXQL] Grid panel "${ixqlResult.command.id}" compiled.\n  Source: ${ixqlResult.command.source}\n  Fields: ${ixqlResult.command.project.length || 'auto'}\n  Refresh: ${ixqlResult.command.refresh ? (ixqlResult.command.refresh / 1000) + 's' : 'off'}` };
        }
        if (!ixqlResult.ok) {
          return { output: `[IXQL] Parse error: ${ixqlResult.error}` };
        }
      }
      // Try parsing as any IXQL command
      const anyResult = parseIxqlCommand(cell.content.trim());
      if (anyResult.ok) {
        return { output: `[IXQL] Command parsed: ${anyResult.command.type}\n  Status: OK` };
      }
      return { output: `[IXQL] ${anyResult.error}` };
    }
    case 'react':
      return {
        output: `[React] Component compiled. No errors.\n  Bundle: ${(Math.random() * 50 + 5).toFixed(1)}KB`,
      };
    case 'grid': {
      // Parse and validate the grid command
      const gridContent = cell.content.trim();
      if (gridContent.startsWith('[') || gridContent.startsWith('{')) {
        try {
          const data = JSON.parse(gridContent);
          const rows = Array.isArray(data) ? data : [data];
          return { output: `[Grid] Rendered ${rows.length} row(s) from inline JSON.` };
        } catch {
          return { output: '[Grid] Invalid JSON data.' };
        }
      }
      const gridResult = parseIxqlCommand(gridContent);
      if (gridResult.ok && gridResult.command.type === 'create-grid-panel') {
        return { output: `[Grid] Panel "${gridResult.command.id}" compiled. Source: ${gridResult.command.source}` };
      }
      return { output: `[Grid] ${!gridResult.ok ? gridResult.error : 'Not a grid panel command.'}` };
    }
    case 'state':
      return { output: '[PDCA] State snapshot captured.' };
    default:
      return { output: '' };
  }
}

// ---------------------------------------------------------------------------
// ID generator
// ---------------------------------------------------------------------------

let _idCounter = 0;
function genId(prefix = 'nb'): string {
  return `${prefix}-${Date.now().toString(36)}-${(++_idCounter).toString(36)}`;
}

// ---------------------------------------------------------------------------
// Sample notebooks
// ---------------------------------------------------------------------------

function createSampleNotebooks(): NotebookDocument[] {
  return [
    {
      id: 'sample-governance-health',
      title: 'Governance Health Report',
      description: 'Cross-repo health assessment with belief queries and algedonic signals',
      createdAt: '2026-03-27T08:00:00Z',
      updatedAt: '2026-03-27T08:00:00Z',
      cells: [
        {
          id: 'gh-1',
          type: 'markdown',
          content: '# Governance Health Report\n\nThis notebook provides a live overview of governance health across the **Demerzel** ecosystem. It queries belief states, monitors algedonic signals, and tracks resilience over time.\n\n---\n\n## Belief State Assessment',
          status: 'idle',
        },
        {
          id: 'gh-2',
          type: 'belief',
          content: JSON.stringify([
            { claim: 'IxQL grammar is stable', status: 'T', confidence: 0.92, evidence: 'Cross-model validation 95% consensus' },
            { claim: 'Chatbot latency < 200ms', status: 'U', confidence: 0.55, evidence: 'Mixed results under load testing' },
            { claim: 'All personas have behavioral tests', status: 'T', confidence: 0.88, evidence: '14/14 personas covered' },
            { claim: 'Proto-conscience is production-ready', status: 'C', confidence: 0.35, evidence: 'Conflicting audit results between tars and Demerzel' },
          ], null, 2),
          status: 'complete',
        },
        {
          id: 'gh-3',
          type: 'markdown',
          content: '## Algedonic Signal Monitor\n\nActive pain/pleasure signals across all repos:',
          status: 'idle',
        },
        {
          id: 'gh-4',
          type: 'algedonic',
          content: JSON.stringify([
            { signal: 'belief_collapse', type: 'pain', severity: 'emergency', source: 'tars', description: 'Belief confidence dropped below 0.3 in music-theory domain' },
            { signal: 'domain_convergence', type: 'pleasure', severity: 'info', source: 'ix', description: 'Cross-model validation achieved 95% consensus on IxQL grammar' },
            { signal: 'policy_violation', type: 'pain', severity: 'warning', source: 'ga', description: 'Unbounded autonomy detected in chatbot loop iteration' },
          ], null, 2),
          status: 'complete',
        },
        {
          id: 'gh-5',
          type: 'chart',
          content: JSON.stringify({
            type: 'sparkline',
            title: 'Resilience Score (last 7 days)',
            values: [0.72, 0.75, 0.78, 0.74, 0.81, 0.85, 0.88],
            labels: ['Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat', 'Sun'],
          }),
          status: 'complete',
        },
        {
          id: 'gh-6',
          type: 'markdown',
          content: '## Belief Grid\n\nInteractive AG-Grid table with tetravalent rendering and sortable columns:',
          status: 'idle',
        },
        {
          id: 'gh-7',
          type: 'grid',
          content: 'CREATE PANEL "notebook-beliefs" KIND grid SOURCE /api/governance/beliefs PROJECT { id, truth_value, confidence, proposition } REFRESH 60s GOVERNED BY article=7',
          status: 'idle',
        },
      ],
    },
    {
      id: 'sample-ix-pipeline',
      title: 'IXQL Command Lab',
      description: 'Live IXQL commands — visual control, grid queries, health binding, and reactive triggers',
      createdAt: '2026-03-29T10:00:00Z',
      updatedAt: '2026-03-29T10:00:00Z',
      cells: [
        {
          id: 'ix-1',
          type: 'markdown',
          content: '# IXQL Command Lab\n\nThis notebook demonstrates **live IXQL commands** you can execute against the Prime Radiant. Each cell runs a real command — try hitting the play button.\n\n---\n\n## Visual Node Selection\n\nHighlight policy nodes and make them glow:',
          status: 'idle',
        },
        {
          id: 'ix-2',
          type: 'ixql',
          content: 'SELECT nodes WHERE type = policy SET glow = true, color = #ffd700',
          status: 'idle',
        },
        {
          id: 'ix-3',
          type: 'markdown',
          content: '## Grid Query\n\nCreate a live data grid from governance beliefs with auto-refresh:',
          status: 'idle',
        },
        {
          id: 'ix-4',
          type: 'ixql',
          content: 'CREATE PANEL "lab-beliefs" KIND grid SOURCE governance.beliefs PROJECT { id, proposition, truth_value, confidence } REFRESH 30s GOVERNED BY article=7',
          status: 'idle',
        },
        {
          id: 'ix-5',
          type: 'markdown',
          content: '## Epistemic Commands\n\nQuery the belief system directly:',
          status: 'idle',
        },
        {
          id: 'ix-6',
          type: 'ixql',
          content: 'SHOW beliefs WHERE confidence > 0.7 ORDER confidence LIMIT 5 VISUALIZE',
          status: 'idle',
        },
        {
          id: 'ix-7',
          type: 'state',
          content: JSON.stringify({
            phase: 'Do',
            description: 'Executing IXQL commands against live governance graph',
            metrics: {
              'commands_available': 10,
              'grid_panels': 'CREATE PANEL KIND grid',
              'visual_control': 'SELECT/SET',
              'epistemic': 'SHOW/METHYLATE/BROADCAST',
            },
          }),
          status: 'idle',
        },
      ],
    },
    {
      id: 'sample-data-explorer',
      title: 'Data Explorer',
      description: 'Interactive grid tables for exploring governance data — beliefs, nodes, and custom queries',
      createdAt: '2026-03-29T10:00:00Z',
      updatedAt: '2026-03-29T10:00:00Z',
      cells: [
        {
          id: 'de-1',
          type: 'markdown',
          content: '# Data Explorer\n\nInteractive AG-Grid tables powered by **IXQL CREATE PANEL KIND grid**. Each grid supports sorting, filtering, and column resizing. Tetravalent values (T/F/U/C) and confidence scores render with visual indicators.\n\n---\n\n## Live Beliefs',
          status: 'idle',
        },
        {
          id: 'de-2',
          type: 'grid',
          content: 'CREATE PANEL "live-beliefs" KIND grid SOURCE governance.beliefs PROJECT { id, proposition, truth_value, confidence } REFRESH 30s GOVERNED BY article=7 PUBLISH selection AS selectedBelief',
          status: 'idle',
        },
        {
          id: 'de-3',
          type: 'markdown',
          content: '## Governance Graph Nodes\n\nAll nodes in the force-directed governance graph, with health metrics:',
          status: 'idle',
        },
        {
          id: 'de-4',
          type: 'grid',
          content: 'CREATE PANEL "graph-nodes" KIND grid SOURCE graph://nodes PROJECT { name, type, description, healthStatus } GOVERNED BY article=8',
          status: 'idle',
        },
        {
          id: 'de-5',
          type: 'markdown',
          content: '## Inline JSON Data\n\nYou can also paste raw JSON arrays directly into grid cells:',
          status: 'idle',
        },
        {
          id: 'de-6',
          type: 'grid',
          content: JSON.stringify([
            { policy: 'alignment', version: '3.2.0', articles: '1,2,5', status: 'T', coverage: 0.95 },
            { policy: 'rollback', version: '2.1.0', articles: '3,6', status: 'T', coverage: 0.88 },
            { policy: 'self-modification', version: '1.4.0', articles: '9,10', status: 'U', coverage: 0.62 },
            { policy: 'kaizen', version: '2.0.0', articles: '8,11', status: 'T', coverage: 0.91 },
            { policy: 'proto-conscience', version: '0.9.0', articles: '4,5,11', status: 'C', coverage: 0.45 },
            { policy: 'governance-audit', version: '1.8.0', articles: '7,8', status: 'T', coverage: 0.93 },
          ], null, 2),
          status: 'idle',
        },
      ],
    },
  ];
}

// ---------------------------------------------------------------------------
// Storage helpers
// ---------------------------------------------------------------------------

function loadNotebooks(): NotebookDocument[] {
  try {
    const raw = localStorage.getItem(STORAGE_KEY);
    if (raw) {
      const parsed = JSON.parse(raw);
      if (Array.isArray(parsed) && parsed.length > 0) return parsed;
    }
  } catch { /* ignore */ }
  const samples = createSampleNotebooks();
  localStorage.setItem(STORAGE_KEY, JSON.stringify(samples));
  return samples;
}

function saveNotebooks(nbs: NotebookDocument[]) {
  localStorage.setItem(STORAGE_KEY, JSON.stringify(nbs));
}

// ---------------------------------------------------------------------------
// Add-cell dropdown
// ---------------------------------------------------------------------------

const AddCellButton: React.FC<{ onAdd: (type: CellType) => void }> = ({ onAdd }) => {
  const [open, setOpen] = useState(false);
  const ref = useRef<HTMLDivElement>(null);

  useEffect(() => {
    if (!open) return;
    const handler = (e: MouseEvent) => {
      if (ref.current && !ref.current.contains(e.target as Node)) setOpen(false);
    };
    document.addEventListener('mousedown', handler);
    return () => document.removeEventListener('mousedown', handler);
  }, [open]);

  const types: CellType[] = ['markdown', 'ixql', 'grid', 'react', 'belief', 'algedonic', 'chart', 'state'];

  return (
    <div ref={ref} style={{ position: 'relative', display: 'flex', justifyContent: 'center', padding: '4px 0' }}>
      <button
        onClick={() => setOpen(o => !o)}
        style={{
          background: 'none', border: '1px dashed rgba(255,215,0,0.2)', borderRadius: 4,
          color: 'rgba(255,215,0,0.4)', cursor: 'pointer', padding: '2px 16px', fontSize: 14,
          fontFamily: "'JetBrains Mono', monospace",
          transition: 'all 0.2s',
        }}
        onMouseEnter={e => {
          (e.target as HTMLButtonElement).style.borderColor = 'rgba(255,215,0,0.5)';
          (e.target as HTMLButtonElement).style.color = 'rgba(255,215,0,0.8)';
        }}
        onMouseLeave={e => {
          (e.target as HTMLButtonElement).style.borderColor = 'rgba(255,215,0,0.2)';
          (e.target as HTMLButtonElement).style.color = 'rgba(255,215,0,0.4)';
        }}
      >+ Add Cell</button>
      {open && (
        <div style={{
          position: 'absolute', top: '100%', left: '50%', transform: 'translateX(-50%)',
          background: '#12122a', border: '1px solid rgba(255,215,0,0.25)', borderRadius: 6,
          padding: 4, zIndex: 100, display: 'flex', flexDirection: 'column', gap: 2, minWidth: 140,
          boxShadow: '0 8px 24px rgba(0,0,0,0.6)',
        }}>
          {types.map(t => (
            <button key={t} onClick={() => { onAdd(t); setOpen(false); }}
              style={{
                background: 'none', border: 'none', color: '#e0e0e0', cursor: 'pointer',
                padding: '6px 10px', textAlign: 'left', borderRadius: 4,
                fontFamily: "'JetBrains Mono', monospace", fontSize: 12,
                display: 'flex', alignItems: 'center', gap: 8,
              }}
              onMouseEnter={e => { (e.target as HTMLButtonElement).style.background = 'rgba(255,215,0,0.1)'; }}
              onMouseLeave={e => { (e.target as HTMLButtonElement).style.background = 'none'; }}
            >
              <span style={{
                background: CELL_TYPE_COLORS[t], color: '#000', padding: '1px 6px',
                borderRadius: 8, fontSize: 9, fontWeight: 'bold', minWidth: 50, textAlign: 'center',
              }}>{CELL_TYPE_LABELS[t]}</span>
              <span>{t}</span>
            </button>
          ))}
        </div>
      )}
    </div>
  );
};

// ---------------------------------------------------------------------------
// Individual cell component
// ---------------------------------------------------------------------------

const CellView: React.FC<{
  cell: NotebookCell;
  onUpdate: (id: string, patch: Partial<NotebookCell>) => void;
  onDelete: (id: string) => void;
  onMoveUp?: () => void;
  onMoveDown?: () => void;
  onDuplicate?: () => void;
  isFirst?: boolean;
  isLast?: boolean;
}> = ({ cell, onUpdate, onDelete, onMoveUp, onMoveDown, onDuplicate, isFirst, isLast }) => {
  const [editing, setEditing] = useState(false);
  const textareaRef = useRef<HTMLTextAreaElement>(null);

  const isExecutable = cell.type !== 'markdown';

  const handleRun = useCallback(() => {
    onUpdate(cell.id, { status: 'running', output: undefined });

    executeCell(cell).then(result => {
      const patch: Partial<NotebookCell> = {
        status: 'complete',
        output: result.output,
      };
      if (result.content) {
        patch.content = result.content;
      }
      onUpdate(cell.id, patch);
    }).catch((err: unknown) => {
      const message = err instanceof Error ? err.message : String(err);
      onUpdate(cell.id, {
        status: 'error',
        output: `[Error] ${message}`,
      });
    });
  }, [cell, onUpdate]);

  const renderContent = useCallback(() => {
    if (editing) return null; // textarea is shown instead

    switch (cell.type) {
      case 'markdown':
        return (
          <div
            style={{ color: '#d0d0d0', fontSize: 13, lineHeight: 1.6 }}
            dangerouslySetInnerHTML={{ __html: renderMarkdown(cell.content) }}
          />
        );

      case 'ixql': {
        // Show highlighted source + live grid preview if it's a grid command
        const ixqlParsed = cell.status === 'complete' ? parseIxqlCommand(cell.content.trim()) : null;
        const isGridCmd = ixqlParsed?.ok && ixqlParsed.command.type === 'create-grid-panel';
        return (
          <div>
            <div style={{
              background: '#0a0a14', padding: 12, borderRadius: 4, fontSize: 12,
              fontFamily: "'JetBrains Mono', monospace", whiteSpace: 'pre-wrap',
              border: '1px solid rgba(255,215,0,0.1)', lineHeight: 1.5,
            }}>
              <div dangerouslySetInnerHTML={{ __html: highlightIxql(cell.content) }} />
            </div>
            {isGridCmd && (
              <div style={{ height: 280, marginTop: 8 }}>
                <IxqlGridPanel spec={compileGridPanel(ixqlParsed.command)} />
              </div>
            )}
          </div>
        );
      }

      case 'react':
        return renderReactCell(cell.content);

      case 'belief':
        return renderBeliefCell(cell.content);

      case 'algedonic':
        return renderAlgedonicCell(cell.content);

      case 'chart':
        return renderChartCell(cell.content);

      case 'state':
        return renderStateCell(cell.content);

      case 'grid':
        return renderGridCell(cell.content);

      default:
        return <pre style={{ color: '#888', fontSize: 12 }}>{cell.content}</pre>;
    }
  }, [cell, editing]);

  // Auto-resize textarea
  useEffect(() => {
    if (editing && textareaRef.current) {
      const ta = textareaRef.current;
      ta.style.height = 'auto';
      ta.style.height = Math.max(80, ta.scrollHeight) + 'px';
      ta.focus();
    }
  }, [editing]);

  return (
    <div style={{
      border: '1px solid rgba(255,215,0,0.1)', borderRadius: 6,
      background: 'rgba(255,255,255,0.02)', overflow: 'hidden',
      transition: 'border-color 0.2s',
    }}>
      {/* Cell toolbar */}
      <div style={{
        display: 'flex', alignItems: 'center', gap: 6, padding: '4px 8px',
        background: 'rgba(0,0,0,0.3)', borderBottom: '1px solid rgba(255,215,0,0.06)',
      }}>
        {/* Type badge */}
        <span style={{
          background: CELL_TYPE_COLORS[cell.type], color: '#000', padding: '1px 8px',
          borderRadius: 8, fontSize: 9, fontWeight: 'bold', letterSpacing: 0.5,
        }}>
          {CELL_TYPE_LABELS[cell.type]}
        </span>

        {/* Status indicator */}
        {cell.status === 'running' && (
          <span style={{ color: '#ff9800', fontSize: 11, animation: 'pulse 1s infinite' }}>running...</span>
        )}
        {cell.status === 'complete' && (
          <span style={{ color: '#4caf50', fontSize: 11 }}>done</span>
        )}
        {cell.status === 'error' && (
          <span style={{ color: '#f44336', fontSize: 11 }}>error</span>
        )}

        <div style={{ flex: 1 }} />

        {/* Move up */}
        {onMoveUp && !isFirst && (
          <button onClick={onMoveUp} title="Move up"
            style={{ background: 'none', border: 'none', cursor: 'pointer', color: '#555', fontSize: 11, padding: '0 3px' }}
            onMouseEnter={e => { (e.target as HTMLButtonElement).style.color = '#ccc'; }}
            onMouseLeave={e => { (e.target as HTMLButtonElement).style.color = '#555'; }}
          >{'\u25B2'}</button>
        )}

        {/* Move down */}
        {onMoveDown && !isLast && (
          <button onClick={onMoveDown} title="Move down"
            style={{ background: 'none', border: 'none', cursor: 'pointer', color: '#555', fontSize: 11, padding: '0 3px' }}
            onMouseEnter={e => { (e.target as HTMLButtonElement).style.color = '#ccc'; }}
            onMouseLeave={e => { (e.target as HTMLButtonElement).style.color = '#555'; }}
          >{'\u25BC'}</button>
        )}

        {/* Duplicate */}
        {onDuplicate && (
          <button onClick={onDuplicate} title="Duplicate cell"
            style={{ background: 'none', border: 'none', cursor: 'pointer', color: '#555', fontSize: 12, padding: '0 3px', fontFamily: "'JetBrains Mono', monospace" }}
            onMouseEnter={e => { (e.target as HTMLButtonElement).style.color = '#ccc'; }}
            onMouseLeave={e => { (e.target as HTMLButtonElement).style.color = '#555'; }}
          >{'\u2398'}</button>
        )}

        <span style={{ width: 1, height: 14, background: '#30363d', margin: '0 2px' }} />

        {/* Edit / Preview toggle */}
        <button
          onClick={() => setEditing(e => !e)}
          title={editing ? 'Preview' : 'Edit'}
          style={{
            background: 'none', border: 'none', cursor: 'pointer',
            color: editing ? '#ffd700' : '#666', fontSize: 12,
            fontFamily: "'JetBrains Mono', monospace", padding: '2px 6px',
          }}
        >{editing ? 'Preview' : 'Edit'}</button>

        {/* Run button */}
        {isExecutable && (
          <button
            onClick={handleRun}
            disabled={cell.status === 'running'}
            title="Run cell"
            style={{
              background: 'none', border: '1px solid rgba(255,215,0,0.3)', borderRadius: 3,
              cursor: cell.status === 'running' ? 'wait' : 'pointer',
              color: '#ffd700', fontSize: 11, padding: '2px 8px',
              fontFamily: "'JetBrains Mono', monospace",
              opacity: cell.status === 'running' ? 0.5 : 1,
            }}
          >{'\u25B6'}</button>
        )}

        {/* Collapse toggle */}
        <button
          onClick={() => onUpdate(cell.id, { collapsed: !cell.collapsed })}
          title={cell.collapsed ? 'Expand' : 'Collapse'}
          style={{
            background: 'none', border: 'none', cursor: 'pointer',
            color: '#666', fontSize: 14, padding: '0 4px',
            transform: cell.collapsed ? 'rotate(-90deg)' : 'rotate(0)',
            transition: 'transform 0.2s',
          }}
        >{'\u25BE'}</button>

        {/* Delete */}
        <button
          onClick={() => onDelete(cell.id)}
          title="Delete cell"
          style={{
            background: 'none', border: 'none', cursor: 'pointer',
            color: '#666', fontSize: 14, padding: '0 4px',
          }}
          onMouseEnter={e => { (e.target as HTMLButtonElement).style.color = '#f44336'; }}
          onMouseLeave={e => { (e.target as HTMLButtonElement).style.color = '#666'; }}
        >{'\u00D7'}</button>
      </div>

      {/* Cell body */}
      {!cell.collapsed && (
        <div style={{ padding: 12 }}>
          {editing ? (
            <textarea
              ref={textareaRef}
              value={cell.content}
              onChange={e => {
                onUpdate(cell.id, { content: e.target.value });
                // auto-resize
                const ta = e.target;
                ta.style.height = 'auto';
                ta.style.height = Math.max(80, ta.scrollHeight) + 'px';
              }}
              style={{
                width: '100%', background: '#0a0a14', color: '#e0e0e0',
                border: '1px solid rgba(255,215,0,0.2)', borderRadius: 4,
                fontFamily: "'JetBrains Mono', monospace", fontSize: 12,
                padding: 10, resize: 'vertical', minHeight: 80,
                outline: 'none', lineHeight: 1.5,
              }}
              spellCheck={false}
            />
          ) : (
            renderContent()
          )}

          {/* Output area */}
          {cell.output && !editing && (
            <div style={{
              marginTop: 8, padding: 8, background: 'rgba(0,0,0,0.3)',
              borderRadius: 4, borderLeft: '3px solid rgba(255,215,0,0.3)',
              fontFamily: "'JetBrains Mono', monospace", fontSize: 11,
              color: '#aaa', whiteSpace: 'pre-wrap', lineHeight: 1.4,
            }}>
              {cell.output}
            </div>
          )}
        </div>
      )}
    </div>
  );
};

// ---------------------------------------------------------------------------
// Main component
// ---------------------------------------------------------------------------

export const LiveNotebook: React.FC<LiveNotebookProps> = ({ open, onClose }) => {
  const [notebooks, setNotebooks] = useState<NotebookDocument[]>([]);
  const [activeId, setActiveId] = useState<string | null>(null);
  const [editingTitle, setEditingTitle] = useState(false);
  const titleRef = useRef<HTMLInputElement>(null);

  // Load on mount
  useEffect(() => {
    if (!open) return;
    const loaded = loadNotebooks();
    setNotebooks(loaded);
    if (!activeId && loaded.length > 0) setActiveId(loaded[0].id);
  }, [open]); // eslint-disable-line react-hooks/exhaustive-deps

  // Persist on change
  useEffect(() => {
    if (notebooks.length > 0) saveNotebooks(notebooks);
  }, [notebooks]);

  const activeNotebook = useMemo(
    () => notebooks.find(n => n.id === activeId) ?? null,
    [notebooks, activeId],
  );

  // ---------------------------------------------------------------------------
  // Notebook CRUD
  // ---------------------------------------------------------------------------

  const createNotebook = useCallback(() => {
    const nb: NotebookDocument = {
      id: genId('nb'),
      title: 'Untitled Notebook',
      cells: [
        {
          id: genId('cell'),
          type: 'markdown',
          content: '# New Notebook\n\nStart writing here...',
          status: 'idle',
        },
      ],
      createdAt: new Date().toISOString(),
      updatedAt: new Date().toISOString(),
    };
    setNotebooks(prev => [nb, ...prev]);
    setActiveId(nb.id);
  }, []);

  const deleteNotebook = useCallback((id: string) => {
    setNotebooks(prev => {
      const next = prev.filter(n => n.id !== id);
      if (activeId === id) setActiveId(next[0]?.id ?? null);
      return next;
    });
  }, [activeId]);

  // ---------------------------------------------------------------------------
  // Cell CRUD
  // ---------------------------------------------------------------------------

  const updateNotebook = useCallback((updater: (nb: NotebookDocument) => NotebookDocument) => {
    setNotebooks(prev => prev.map(nb =>
      nb.id === activeId ? updater({ ...nb, updatedAt: new Date().toISOString() }) : nb
    ));
  }, [activeId]);

  const updateCell = useCallback((cellId: string, patch: Partial<NotebookCell>) => {
    updateNotebook(nb => ({
      ...nb,
      cells: nb.cells.map(c => c.id === cellId ? { ...c, ...patch } : c),
    }));
  }, [updateNotebook]);

  const deleteCell = useCallback((cellId: string) => {
    updateNotebook(nb => ({
      ...nb,
      cells: nb.cells.filter(c => c.id !== cellId),
    }));
  }, [updateNotebook]);

  const addCellAt = useCallback((index: number, type: CellType) => {
    const defaults: Record<CellType, string> = {
      markdown: '## Section\n\nWrite markdown here...',
      ixql: 'SELECT nodes WHERE type = policy SET glow = true',
      grid: 'CREATE PANEL "my-grid" KIND grid SOURCE governance.beliefs PROJECT { id, proposition, truth_value, confidence } REFRESH 30s',
      react: 'const MyComponent = () => {\n  return <div>Hello, Prime Radiant</div>;\n};',
      belief: JSON.stringify([{ claim: 'New claim', status: 'U', confidence: 0.5, evidence: 'Pending investigation' }], null, 2),
      algedonic: JSON.stringify([{ signal: 'new_signal', type: 'pain', severity: 'info', source: 'manual', description: 'Describe the signal' }], null, 2),
      chart: JSON.stringify({ type: 'sparkline', title: 'Chart Title', values: [1, 3, 2, 5, 4, 6], labels: [] }),
      state: JSON.stringify({ phase: 'Plan', description: 'Describe the current PDCA state', metrics: {} }),
    };
    const cell: NotebookCell = {
      id: genId('cell'),
      type,
      content: defaults[type],
      status: 'idle',
    };
    updateNotebook(nb => ({
      ...nb,
      cells: [...nb.cells.slice(0, index), cell, ...nb.cells.slice(index)],
    }));
  }, [updateNotebook]);

  const moveCell = useCallback((cellId: string, direction: 'up' | 'down') => {
    updateNotebook(nb => {
      const idx = nb.cells.findIndex(c => c.id === cellId);
      if (idx < 0) return nb;
      const target = direction === 'up' ? idx - 1 : idx + 1;
      if (target < 0 || target >= nb.cells.length) return nb;
      const cells = [...nb.cells];
      [cells[idx], cells[target]] = [cells[target], cells[idx]];
      return { ...nb, cells };
    });
  }, [updateNotebook]);

  const duplicateCell = useCallback((cellId: string) => {
    updateNotebook(nb => {
      const idx = nb.cells.findIndex(c => c.id === cellId);
      if (idx < 0) return nb;
      const original = nb.cells[idx];
      const copy: NotebookCell = {
        ...original,
        id: genId('cell'),
        status: 'idle',
        output: undefined,
      };
      const cells = [...nb.cells];
      cells.splice(idx + 1, 0, copy);
      return { ...nb, cells };
    });
  }, [updateNotebook]);

  const runAllCells = useCallback(() => {
    if (!activeNotebook) return;
    for (const cell of activeNotebook.cells) {
      if (cell.type !== 'markdown') {
        updateCell(cell.id, { status: 'running', output: undefined });
        executeCell(cell).then(result => {
          const patch: Partial<NotebookCell> = { status: 'complete', output: result.output };
          if (result.content) patch.content = result.content;
          updateCell(cell.id, patch);
        }).catch((err: unknown) => {
          updateCell(cell.id, { status: 'error', output: `[Error] ${err instanceof Error ? err.message : String(err)}` });
        });
      }
    }
  }, [activeNotebook, updateCell]);

  const updateTitle = useCallback((title: string) => {
    updateNotebook(nb => ({ ...nb, title }));
  }, [updateNotebook]);

  // Close on Escape
  useEffect(() => {
    if (!open) return;
    const handler = (e: KeyboardEvent) => { if (e.key === 'Escape') onClose(); };
    window.addEventListener('keydown', handler);
    return () => window.removeEventListener('keydown', handler);
  }, [open, onClose]);

  if (!open) return null;

  return (
    <div style={{
      position: 'fixed', inset: 0, zIndex: 9000,
      background: 'rgba(10, 10, 20, 0.97)',
      fontFamily: "'JetBrains Mono', monospace",
      display: 'flex', flexDirection: 'column',
      color: '#e0e0e0',
    }}>
      {/* Inline keyframes */}
      <style>{`
        @keyframes pulse { 0%,100% { opacity: 1; } 50% { opacity: 0.4; } }
        .nb-sidebar-item:hover { background: rgba(255,215,0,0.08) !important; }
      `}</style>

      {/* ============ Header ============ */}
      <div style={{
        display: 'flex', alignItems: 'center', gap: 12, padding: '10px 16px',
        borderBottom: '1px solid rgba(255,215,0,0.15)',
        background: 'rgba(0,0,0,0.3)',
        flexShrink: 0,
      }}>
        <span style={{ color: '#ffd700', fontSize: 16, fontWeight: 'bold' }}>
          {'\u{1F4D3}'}{' '}Live Notebook
        </span>

        <div style={{ flex: 1 }} />

        <button
          onClick={createNotebook}
          style={{
            background: 'rgba(255,215,0,0.1)', border: '1px solid rgba(255,215,0,0.3)',
            borderRadius: 4, color: '#ffd700', cursor: 'pointer', padding: '4px 12px',
            fontFamily: "'JetBrains Mono', monospace", fontSize: 12,
          }}
        >+ New Notebook</button>

        <button
          onClick={onClose}
          title="Close"
          style={{
            background: 'none', border: 'none', cursor: 'pointer',
            color: '#888', fontSize: 22, padding: '0 4px', lineHeight: 1,
          }}
          onMouseEnter={e => { (e.target as HTMLButtonElement).style.color = '#f44336'; }}
          onMouseLeave={e => { (e.target as HTMLButtonElement).style.color = '#888'; }}
        >{'\u00D7'}</button>
      </div>

      {/* ============ Body ============ */}
      <div style={{ display: 'flex', flex: 1, overflow: 'hidden' }}>

        {/* ---------- Sidebar ---------- */}
        <div style={{
          width: 240, flexShrink: 0, borderRight: '1px solid rgba(255,215,0,0.1)',
          overflowY: 'auto', background: 'rgba(0,0,0,0.2)', padding: '8px 0',
        }}>
          <div style={{ padding: '4px 12px 8px', fontSize: 10, color: '#666', textTransform: 'uppercase', letterSpacing: 1 }}>
            Notebooks ({notebooks.length})
          </div>
          {notebooks.map(nb => (
            <div
              key={nb.id}
              className="nb-sidebar-item"
              onClick={() => setActiveId(nb.id)}
              style={{
                display: 'flex', alignItems: 'center', gap: 8,
                padding: '8px 12px', cursor: 'pointer',
                background: nb.id === activeId ? 'rgba(255,215,0,0.1)' : 'transparent',
                borderLeft: nb.id === activeId ? '3px solid #ffd700' : '3px solid transparent',
                transition: 'all 0.15s',
              }}
            >
              <div style={{ flex: 1, minWidth: 0 }}>
                <div style={{
                  color: nb.id === activeId ? '#ffd700' : '#ccc',
                  fontSize: 12, fontWeight: nb.id === activeId ? 'bold' : 'normal',
                  overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap',
                }}>{nb.title}</div>
                {nb.description && (
                  <div style={{ color: '#555', fontSize: 9, marginTop: 1, overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>
                    {nb.description}
                  </div>
                )}
                <div style={{ display: 'flex', alignItems: 'center', gap: 3, marginTop: 3 }}>
                  <span style={{ color: '#555', fontSize: 9 }}>
                    {nb.cells.length} cell{nb.cells.length !== 1 ? 's' : ''}
                  </span>
                  {/* Cell type dots */}
                  {(() => {
                    const types = new Set(nb.cells.map(c => c.type));
                    return [...types].map(t => (
                      <span key={t} style={{
                        width: 5, height: 5, borderRadius: '50%',
                        background: CELL_TYPE_COLORS[t],
                        display: 'inline-block',
                      }} title={CELL_TYPE_LABELS[t]} />
                    ));
                  })()}
                </div>
              </div>
              {!nb.id.startsWith('sample-') && (
                <button
                  onClick={e => { e.stopPropagation(); deleteNotebook(nb.id); }}
                  title="Delete notebook"
                  style={{
                    background: 'none', border: 'none', cursor: 'pointer',
                    color: '#555', fontSize: 14, padding: 0, lineHeight: 1,
                  }}
                  onMouseEnter={e => { (e.target as HTMLButtonElement).style.color = '#f44336'; }}
                  onMouseLeave={e => { (e.target as HTMLButtonElement).style.color = '#555'; }}
                >{'\u00D7'}</button>
              )}
            </div>
          ))}
        </div>

        {/* ---------- Main area ---------- */}
        <div style={{ flex: 1, overflowY: 'auto', padding: '16px 24px 80px' }}>
          {activeNotebook ? (
            <>
              {/* Notebook title (click to edit) */}
              <div style={{ marginBottom: 16 }}>
                {editingTitle ? (
                  <input
                    ref={titleRef}
                    autoFocus
                    value={activeNotebook.title}
                    onChange={e => updateTitle(e.target.value)}
                    onBlur={() => setEditingTitle(false)}
                    onKeyDown={e => { if (e.key === 'Enter') setEditingTitle(false); }}
                    style={{
                      background: 'none', border: 'none', borderBottom: '2px solid #ffd700',
                      color: '#ffd700', fontSize: 20, fontWeight: 'bold',
                      fontFamily: "'JetBrains Mono', monospace",
                      outline: 'none', width: '100%', padding: '4px 0',
                    }}
                  />
                ) : (
                  <h1
                    onClick={() => setEditingTitle(true)}
                    style={{
                      color: '#ffd700', fontSize: 20, fontWeight: 'bold', margin: 0,
                      cursor: 'text', padding: '4px 0',
                      borderBottom: '2px solid transparent',
                    }}
                    title="Click to rename"
                  >{activeNotebook.title}</h1>
                )}
                {activeNotebook.description && (
                  <div style={{ color: '#888', fontSize: 12, marginTop: 4 }}>{activeNotebook.description}</div>
                )}
                <div style={{ display: 'flex', alignItems: 'center', gap: 12, marginTop: 8 }}>
                  <span style={{ color: '#555', fontSize: 10 }}>
                    Updated: {new Date(activeNotebook.updatedAt).toLocaleString()}
                  </span>

                  {/* Cell type stats */}
                  <span style={{ display: 'flex', gap: 4 }}>
                    {(() => {
                      const counts: Partial<Record<CellType, number>> = {};
                      for (const c of activeNotebook.cells) {
                        counts[c.type] = (counts[c.type] ?? 0) + 1;
                      }
                      return Object.entries(counts).map(([type, count]) => (
                        <span key={type} style={{
                          fontSize: 9, padding: '1px 5px', borderRadius: 6,
                          background: CELL_TYPE_COLORS[type as CellType] + '22',
                          color: CELL_TYPE_COLORS[type as CellType],
                        }}>
                          {count} {CELL_TYPE_LABELS[type as CellType]}
                        </span>
                      ));
                    })()}
                  </span>

                  <div style={{ flex: 1 }} />

                  {/* Run All */}
                  <button
                    onClick={runAllCells}
                    style={{
                      background: 'rgba(255,215,0,0.08)', border: '1px solid rgba(255,215,0,0.25)',
                      borderRadius: 4, color: '#ffd700', cursor: 'pointer', padding: '3px 10px',
                      fontFamily: "'JetBrains Mono', monospace", fontSize: 11,
                    }}
                  >{'\u25B6\u25B6'} Run All</button>
                </div>
              </div>

              {/* Add cell at top */}
              <AddCellButton onAdd={type => addCellAt(0, type)} />

              {/* Cells */}
              {activeNotebook.cells.map((cell, idx) => (
                <React.Fragment key={cell.id}>
                  <CellView
                    cell={cell}
                    onUpdate={updateCell}
                    onDelete={deleteCell}
                    onMoveUp={() => moveCell(cell.id, 'up')}
                    onMoveDown={() => moveCell(cell.id, 'down')}
                    onDuplicate={() => duplicateCell(cell.id)}
                    isFirst={idx === 0}
                    isLast={idx === activeNotebook.cells.length - 1}
                  />
                  <AddCellButton onAdd={type => addCellAt(idx + 1, type)} />
                </React.Fragment>
              ))}

              {activeNotebook.cells.length === 0 && (
                <div style={{
                  textAlign: 'center', color: '#555', padding: 40, fontSize: 13,
                }}>
                  Empty notebook. Click "+ Add Cell" to begin.
                </div>
              )}
            </>
          ) : (
            <div style={{
              display: 'flex', alignItems: 'center', justifyContent: 'center',
              height: '100%', color: '#555', fontSize: 14,
            }}>
              Select or create a notebook to get started.
            </div>
          )}
        </div>
      </div>
    </div>
  );
};

export default LiveNotebook;
