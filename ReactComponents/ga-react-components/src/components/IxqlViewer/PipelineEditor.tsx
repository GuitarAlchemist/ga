// PipelineEditor — ComfyUI-style editor for IX pipelines (tracer bullet).
//
// Palette from ix_node_catalog, steps as React Flow nodes, edges as
// depends_on, live ix_pipeline_validate with errors pinned to their node,
// and Run via ix_pipeline_run. Talks to the local-only /ix-pipeline/*
// dev-server routes (vite.config.ts → dev-server/ixMcpBridge.ts).

import React, { useCallback, useEffect, useMemo, useState } from 'react';
import ReactFlow, {
  Background,
  Controls,
  Handle,
  MarkerType,
  Position,
  applyNodeChanges,
  type Connection,
  type Edge,
  type Node,
  type NodeChange,
  type NodeProps,
} from 'reactflow';
import 'reactflow/dist/style.css';

import {
  buildSpec,
  defaultArgsText,
  issuesByStep,
  nextStepId,
  type CatalogNode,
  type EditorEdge,
  type EditorStep,
  type ValidationReport,
} from './pipelineSpec';

const COLORS = { bg: '#0d1117', panel: '#161b22', border: '#30363d', text: '#c9d1d9', dim: '#8b949e', ok: '#4cb050', err: '#e05555', warn: '#d29922', accent: '#58a6ff' };

interface StepNodeData {
  step: EditorStep;
  errors: string[];
  warnings: string[];
  selected: boolean;
}

const StepNode: React.FC<NodeProps<StepNodeData>> = ({ data }) => {
  const border = data.errors.length ? COLORS.err : data.warnings.length ? COLORS.warn : COLORS.ok;
  return (
    <div
      title={[...data.errors, ...data.warnings].join('\n') || undefined}
      style={{
        background: COLORS.panel,
        color: COLORS.text,
        border: `2px solid ${data.selected ? COLORS.accent : border}`,
        borderRadius: 6,
        padding: '6px 10px',
        minWidth: 150,
        fontFamily: 'monospace',
        fontSize: 12,
      }}
    >
      <Handle type="target" position={Position.Top} />
      <div style={{ fontWeight: 600 }}>{data.step.id}</div>
      <div style={{ color: COLORS.dim }}>{data.step.tool}</div>
      {data.errors.length > 0 && <div style={{ color: COLORS.err, marginTop: 4 }}>✕ {data.errors.length} error(s)</div>}
      {data.errors.length === 0 && data.warnings.length > 0 && (
        <div style={{ color: COLORS.warn, marginTop: 4 }}>⚠ {data.warnings.length} warning(s)</div>
      )}
      <Handle type="source" position={Position.Bottom} />
    </div>
  );
};

const nodeTypes = { step: StepNode };

async function postJson(url: string, body: unknown): Promise<{ ok: boolean; result?: unknown; error?: string }> {
  const res = await fetch(url, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(body) });
  const json = (await res.json()) as { ok?: boolean; result?: unknown; error?: string };
  if (!res.ok) return { ok: false, error: json.error ?? `HTTP ${res.status}` };
  return { ok: json.ok === true, result: json.result, error: json.error };
}

export const PipelineEditor: React.FC = () => {
  const [catalog, setCatalog] = useState<CatalogNode[] | null>(null);
  const [catalogError, setCatalogError] = useState<string | null>(null);
  const [filter, setFilter] = useState('');
  const [steps, setSteps] = useState<EditorStep[]>([]);
  const [edges, setEdges] = useState<EditorEdge[]>([]);
  const [positions, setPositions] = useState<Record<string, { x: number; y: number }>>({});
  const [selectedId, setSelectedId] = useState<string | null>(null);
  const [report, setReport] = useState<ValidationReport | null>(null);
  const [validateError, setValidateError] = useState<string | null>(null);
  const [runOutput, setRunOutput] = useState<string | null>(null);
  const [running, setRunning] = useState(false);

  useEffect(() => {
    fetch('/ix-pipeline/catalog')
      .then(async (res) => {
        const json = (await res.json()) as { nodes?: CatalogNode[]; error?: string; hint?: string };
        if (!res.ok || !json.nodes) throw new Error([json.error, json.hint].filter(Boolean).join(' — ') || `HTTP ${res.status}`);
        setCatalog([...json.nodes].sort((a, b) => a.name.localeCompare(b.name)));
      })
      .catch((e: unknown) => setCatalogError(e instanceof Error ? e.message : String(e)));
  }, []);

  const requiredByTool = useMemo(
    () => Object.fromEntries((catalog ?? []).map((n) => [n.name, n.required_inputs])),
    [catalog],
  );
  const { spec, argErrors } = useMemo(() => buildSpec(steps, edges, requiredByTool), [steps, edges, requiredByTool]);

  // Live validation, debounced.
  useEffect(() => {
    if (steps.length === 0) { setReport(null); setValidateError(null); return; }
    const handle = setTimeout(() => {
      postJson('/ix-pipeline/validate', spec)
        .then((r) => {
          if (r.ok) { setReport(r.result as ValidationReport); setValidateError(null); }
          else setValidateError(r.error ?? 'validation failed');
        })
        .catch((e: unknown) => setValidateError(String(e)));
    }, 500);
    return () => clearTimeout(handle);
  }, [spec, steps.length]);

  const issues = useMemo(() => issuesByStep(report), [report]);

  const addStep = useCallback((node: CatalogNode) => {
    const id = nextStepId(steps);
    const n = steps.length;
    setPositions((p) => ({ ...p, [id]: { x: 40 + (n % 4) * 200, y: 40 + Math.floor(n / 4) * 130 } }));
    setSteps([...steps, { id, tool: node.name, argsText: defaultArgsText(node) }]);
    setSelectedId(id);
  }, [steps]);

  const removeStep = useCallback((id: string) => {
    setSteps((prev) => prev.filter((s) => s.id !== id));
    setEdges((prev) => prev.filter((e) => e.source !== id && e.target !== id));
    setSelectedId(null);
  }, []);

  const flowNodes: Node<StepNodeData>[] = useMemo(
    () =>
      steps.map((step) => {
        const i = issues.get(step.id);
        const errors = [...(argErrors[step.id] ? [argErrors[step.id]] : []), ...(i?.errors ?? [])];
        return {
          id: step.id,
          type: 'step',
          position: positions[step.id] ?? { x: 0, y: 0 },
          data: { step, errors, warnings: i?.warnings ?? [], selected: step.id === selectedId },
        };
      }),
    [steps, positions, issues, argErrors, selectedId],
  );

  const flowEdges: Edge[] = useMemo(
    () =>
      edges.map((e) => ({
        id: `${e.source}->${e.target}`,
        source: e.source,
        target: e.target,
        style: { stroke: '#555', strokeWidth: 1.5 },
        markerEnd: { type: MarkerType.ArrowClosed, color: '#555' },
      })),
    [edges],
  );

  const onNodesChange = useCallback((changes: NodeChange[]) => {
    setPositions((prev) => {
      const next = applyNodeChanges(changes, Object.entries(prev).map(([id, position]) => ({ id, position, data: null })));
      return Object.fromEntries(next.map((n) => [n.id, n.position]));
    });
  }, []);

  const onConnect = useCallback((c: Connection) => {
    if (!c.source || !c.target || c.source === c.target) return;
    setEdges((prev) =>
      prev.some((e) => e.source === c.source && e.target === c.target) ? prev : [...prev, { source: c.source!, target: c.target! }],
    );
  }, []);

  const onEdgesDelete = useCallback((deleted: Edge[]) => {
    const gone = new Set(deleted.map((d) => d.id));
    setEdges((prev) => prev.filter((e) => !gone.has(`${e.source}->${e.target}`)));
  }, []);

  const run = useCallback(() => {
    setRunning(true);
    setRunOutput(null);
    postJson('/ix-pipeline/run', spec)
      .then((r) => setRunOutput(r.ok ? JSON.stringify(r.result, null, 2) : `Error: ${r.error}`))
      .catch((e: unknown) => setRunOutput(`Error: ${String(e)}`))
      .finally(() => setRunning(false));
  }, [spec]);

  const selected = steps.find((s) => s.id === selectedId) ?? null;
  const selectedNode = selected ? catalog?.find((n) => n.name === selected.tool) : undefined;
  const filtered = (catalog ?? []).filter((n) => n.name.toLowerCase().includes(filter.toLowerCase()));
  const canRun = steps.length > 0 && report?.valid === true && Object.keys(argErrors).length === 0 && !running;
  const globalIssues = issues.get('');

  const panel: React.CSSProperties = { background: COLORS.panel, borderColor: COLORS.border, color: COLORS.text, overflowY: 'auto', padding: 10, fontSize: 12 };

  return (
    <div style={{ display: 'flex', height: '100%', minHeight: 600, background: COLORS.bg, fontFamily: 'system-ui, sans-serif' }}>
      <aside style={{ ...panel, width: 240, borderRight: `1px solid ${COLORS.border}` }}>
        <div style={{ fontWeight: 600, marginBottom: 6 }}>Nodes {catalog ? `(${catalog.length})` : ''}</div>
        <input
          value={filter}
          onChange={(e) => setFilter(e.target.value)}
          placeholder="filter…"
          style={{ width: '100%', marginBottom: 8, background: COLORS.bg, color: COLORS.text, border: `1px solid ${COLORS.border}`, padding: 4 }}
        />
        {catalogError && <div style={{ color: COLORS.err }}>Catalog unavailable: {catalogError}</div>}
        {!catalog && !catalogError && <div style={{ color: COLORS.dim }}>Loading catalog…</div>}
        {filtered.map((n) => (
          <button
            key={n.name}
            onClick={() => addStep(n)}
            title={n.description}
            style={{ display: 'block', width: '100%', textAlign: 'left', background: 'transparent', color: COLORS.text, border: 'none', padding: '3px 2px', cursor: 'pointer', fontFamily: 'monospace' }}
          >
            + {n.name}
          </button>
        ))}
      </aside>

      <main style={{ flex: 1, position: 'relative' }}>
        <ReactFlow
          nodes={flowNodes}
          edges={flowEdges}
          nodeTypes={nodeTypes}
          onNodesChange={onNodesChange}
          onConnect={onConnect}
          onEdgesDelete={onEdgesDelete}
          onNodeClick={(_e, n) => setSelectedId(n.id)}
          onPaneClick={() => setSelectedId(null)}
          deleteKeyCode={['Delete']}
          fitView
          minZoom={0.2}
          maxZoom={2}
        >
          <Background color="#21262d" gap={20} />
          <Controls />
        </ReactFlow>
        {steps.length === 0 && (
          <div style={{ position: 'absolute', top: '45%', width: '100%', textAlign: 'center', color: COLORS.dim, pointerEvents: 'none' }}>
            Click a node on the left to add a step; drag from a node's bottom handle to another's top to make it depend on it.
          </div>
        )}
      </main>

      <aside style={{ ...panel, width: 340, borderLeft: `1px solid ${COLORS.border}` }}>
        <div style={{ display: 'flex', gap: 8, alignItems: 'center', marginBottom: 8 }}>
          <strong>Pipeline</strong>
          <span style={{ color: report?.valid ? COLORS.ok : COLORS.dim }}>
            {steps.length === 0 ? 'empty' : report ? (report.valid ? '✓ valid' : '✕ invalid') : 'validating…'}
          </span>
          <button onClick={run} disabled={!canRun} style={{ marginLeft: 'auto' }}>
            {running ? 'Running…' : 'Run'}
          </button>
        </div>
        {validateError && <div style={{ color: COLORS.err }}>Validator: {validateError}</div>}
        {globalIssues?.errors.map((m) => <div key={m} style={{ color: COLORS.err }}>✕ {m}</div>)}
        {globalIssues?.warnings.map((m) => <div key={m} style={{ color: COLORS.warn }}>⚠ {m}</div>)}
        {report?.execution_order && <div style={{ color: COLORS.dim }}>Order: {report.execution_order.join(' → ')}</div>}

        {selected && (
          <section style={{ marginTop: 12 }}>
            <div style={{ display: 'flex', alignItems: 'center' }}>
              <strong>{selected.id}</strong>&nbsp;<span style={{ color: COLORS.dim }}>{selected.tool}</span>
              <button onClick={() => removeStep(selected.id)} style={{ marginLeft: 'auto' }}>Delete</button>
            </div>
            {selectedNode && <p style={{ color: COLORS.dim }}>{selectedNode.description}</p>}
            {selectedNode?.input_schema?.properties && (
              <ul style={{ paddingLeft: 16, color: COLORS.dim }}>
                {Object.entries(selectedNode.input_schema.properties).map(([k, v]) => (
                  <li key={k}>
                    <code>{k}</code>{selectedNode.required_inputs.includes(k) ? '*' : ''} {v.type ? `(${v.type})` : ''} {v.description ?? ''}
                  </li>
                ))}
              </ul>
            )}
            <div>arguments (JSON; <code>$stepId.field</code> references another step's output)</div>
            <textarea
              value={selected.argsText}
              onChange={(e) => {
                const v = e.target.value;
                setSteps((prev) => prev.map((s) => (s.id === selected.id ? { ...s, argsText: v } : s)));
              }}
              rows={8}
              spellCheck={false}
              style={{ width: '100%', fontFamily: 'monospace', background: COLORS.bg, color: COLORS.text, border: `1px solid ${COLORS.border}` }}
            />
            {[...(argErrors[selected.id] ? [argErrors[selected.id]] : []), ...(issues.get(selected.id)?.errors ?? [])].map((m) => (
              <div key={m} style={{ color: COLORS.err }}>✕ {m}</div>
            ))}
            {issues.get(selected.id)?.warnings.map((m) => <div key={m} style={{ color: COLORS.warn }}>⚠ {m}</div>)}
          </section>
        )}

        {runOutput && (
          <section style={{ marginTop: 12 }}>
            <strong>Run result</strong>
            <pre style={{ whiteSpace: 'pre-wrap', background: COLORS.bg, padding: 6, maxHeight: 300, overflow: 'auto' }}>{runOutput}</pre>
          </section>
        )}

        <details style={{ marginTop: 12 }}>
          <summary>Spec (ix_pipeline_run)</summary>
          <pre style={{ whiteSpace: 'pre-wrap', background: COLORS.bg, padding: 6 }}>{JSON.stringify(spec, null, 2)}</pre>
        </details>
      </aside>
    </div>
  );
};
