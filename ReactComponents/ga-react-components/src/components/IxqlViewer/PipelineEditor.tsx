// PipelineEditor — ComfyUI-style editor for IX pipelines (tracer bullet).
//
// Palette from ix_node_catalog, steps as React Flow nodes, edges as
// depends_on, live ix_pipeline_validate with errors pinned to their node,
// and Run via ix_pipeline_run. Talks to the local-only /ix-pipeline/*
// dev-server routes (vite.config.ts → dev-server/ixMcpBridge.ts).

import React, { useCallback, useEffect, useMemo, useRef, useState } from 'react';
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
import { Box, Button, List, ListItemButton, TextField, Typography, useTheme } from '@mui/material';

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

interface StepNodeData {
  step: EditorStep;
  errors: string[];
  warnings: string[];
  selected: boolean;
}

const StepNode: React.FC<NodeProps<StepNodeData>> = ({ data }) => {
  const status = data.errors.length ? 'error.main' : data.warnings.length ? 'warning.main' : 'success.main';
  return (
    <Box
      title={[...data.errors, ...data.warnings].join('\n') || undefined}
      sx={{
        bgcolor: 'background.paper',
        color: 'text.primary',
        border: 2,
        borderColor: data.selected ? 'primary.main' : status,
        borderRadius: 1.5,
        px: 1.25,
        py: 0.75,
        minWidth: 150,
        fontFamily: 'monospace',
        fontSize: 12,
      }}
    >
      <Handle type="target" position={Position.Top} />
      <Box sx={{ fontWeight: 600 }}>{data.step.id}</Box>
      <Box sx={{ color: 'text.secondary' }}>{data.step.tool}</Box>
      {data.errors.length > 0 && <Box sx={{ color: 'error.main', mt: 0.5 }}>✕ {data.errors.length} error(s)</Box>}
      {data.errors.length === 0 && data.warnings.length > 0 && (
        <Box sx={{ color: 'warning.main', mt: 0.5 }}>⚠ {data.warnings.length} warning(s)</Box>
      )}
      <Handle type="source" position={Position.Bottom} />
    </Box>
  );
};

const nodeTypes = { step: StepNode };

async function postJson(url: string, body: unknown): Promise<{ ok: boolean; result?: unknown; error?: string }> {
  const res = await fetch(url, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(body) });
  const json = (await res.json()) as { ok?: boolean; result?: unknown; error?: string };
  if (!res.ok) return { ok: false, error: json.error ?? `HTTP ${res.status}` };
  return { ok: json.ok === true, result: json.result, error: json.error };
}

const Issue: React.FC<{ kind: 'error' | 'warning'; children: React.ReactNode }> = ({ kind, children }) => (
  <Typography variant="caption" component="div" sx={{ color: `${kind}.main` }}>
    {kind === 'error' ? '✕' : '⚠'} {children}
  </Typography>
);

export const PipelineEditor: React.FC = () => {
  const theme = useTheme();
  const [catalog, setCatalog] = useState<CatalogNode[] | null>(null);
  const [catalogError, setCatalogError] = useState<string | null>(null);
  const [filter, setFilter] = useState('');
  const [steps, setSteps] = useState<EditorStep[]>([]);
  const [edges, setEdges] = useState<EditorEdge[]>([]);
  const [positions, setPositions] = useState<Record<string, { x: number; y: number }>>({});
  const [selectedId, setSelectedId] = useState<string | null>(null);
  const [selectedEdgeId, setSelectedEdgeId] = useState<string | null>(null);
  const [report, setReport] = useState<ValidationReport | null>(null);
  const [validateError, setValidateError] = useState<string | null>(null);
  const [runOutput, setRunOutput] = useState<string | null>(null);
  const [running, setRunning] = useState(false);
  const validationSeq = useRef(0);

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

  // Live validation, debounced. Every spec change invalidates the previous
  // report at once (so Run cannot fire on a spec that was never validated),
  // and a response is applied only if it belongs to the latest spec.
  useEffect(() => {
    const seq = ++validationSeq.current;
    setReport(null);
    setValidateError(null);
    if (spec.steps.length === 0) return;
    const handle = setTimeout(() => {
      postJson('/ix-pipeline/validate', spec)
        .then((r) => {
          if (seq !== validationSeq.current) return;
          if (r.ok) setReport(r.result as ValidationReport);
          else setValidateError(r.error ?? 'validation failed');
        })
        .catch((e: unknown) => {
          if (seq === validationSeq.current) setValidateError(String(e));
        });
    }, 500);
    return () => clearTimeout(handle);
  }, [spec]);

  const issues = useMemo(() => issuesByStep(report), [report]);

  const addStep = useCallback((node: CatalogNode) => {
    const id = nextStepId(steps);
    const n = steps.length;
    setPositions((p) => ({ ...p, [id]: { x: 40 + (n % 4) * 200, y: 40 + Math.floor(n / 4) * 130 } }));
    setSteps([...steps, { id, tool: node.name, argsText: defaultArgsText(node) }]);
    setSelectedId(id);
  }, [steps]);

  const removeSteps = useCallback((ids: string[]) => {
    const gone = new Set(ids);
    setSteps((prev) => prev.filter((s) => !gone.has(s.id)));
    setEdges((prev) => prev.filter((e) => !gone.has(e.source) && !gone.has(e.target)));
    setPositions((prev) => Object.fromEntries(Object.entries(prev).filter(([id]) => !gone.has(id))));
    setSelectedId((cur) => (cur && gone.has(cur) ? null : cur));
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
          // React Flow only deletes (Delete key) what it sees as selected.
          selected: step.id === selectedId,
          data: { step, errors, warnings: i?.warnings ?? [], selected: step.id === selectedId },
        };
      }),
    [steps, positions, issues, argErrors, selectedId],
  );

  const edgeColor = theme.palette.text.secondary;
  const accentColor = theme.palette.primary.main;
  const flowEdges: Edge[] = useMemo(
    () =>
      edges.map((e) => {
        const id = `${e.source}->${e.target}`;
        const color = id === selectedEdgeId ? accentColor : edgeColor;
        return {
          id,
          source: e.source,
          target: e.target,
          selected: id === selectedEdgeId,
          style: { stroke: color, strokeWidth: id === selectedEdgeId ? 2.5 : 1.5 },
          markerEnd: { type: MarkerType.ArrowClosed, color },
        };
      }),
    [edges, edgeColor, accentColor, selectedEdgeId],
  );

  // `steps` is the model: a keyboard "remove" deletes the step and its edges;
  // position changes only move it.
  const onNodesChange = useCallback((changes: NodeChange[]) => {
    const removed = changes.flatMap((c) => (c.type === 'remove' ? [c.id] : []));
    if (removed.length > 0) removeSteps(removed);
    const moves = changes.filter((c) => c.type !== 'remove');
    if (moves.length === 0) return;
    setPositions((prev) => {
      const next = applyNodeChanges(moves, Object.entries(prev).map(([id, position]) => ({ id, position, data: null })));
      return Object.fromEntries(next.map((n) => [n.id, n.position]));
    });
  }, [removeSteps]);

  const onConnect = useCallback((c: Connection) => {
    if (!c.source || !c.target || c.source === c.target) return;
    const { source, target } = c;
    setEdges((prev) => (prev.some((e) => e.source === source && e.target === target) ? prev : [...prev, { source, target }]));
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
  const selectedErrors = selected
    ? [...(argErrors[selected.id] ? [argErrors[selected.id]] : []), ...(issues.get(selected.id)?.errors ?? [])]
    : [];

  const sidePanel = { bgcolor: 'background.paper', color: 'text.primary', overflowY: 'auto', p: 1.25, fontSize: 12 } as const;
  const codeBlock = { whiteSpace: 'pre-wrap', bgcolor: 'background.default', p: 0.75, fontFamily: 'monospace', fontSize: 12, m: 0 } as const;

  return (
    <Box sx={{ display: 'flex', height: '100%', minHeight: 600, bgcolor: 'background.default' }}>
      <Box component="aside" sx={{ ...sidePanel, width: 240, borderRight: 1, borderColor: 'divider' }}>
        <Typography variant="subtitle2">Nodes {catalog ? `(${catalog.length})` : ''}</Typography>
        <TextField
          value={filter}
          onChange={(e) => setFilter(e.target.value)}
          placeholder="filter…"
          size="small"
          fullWidth
          sx={{ my: 1 }}
        />
        {catalogError && <Issue kind="error">Catalog unavailable: {catalogError}</Issue>}
        {!catalog && !catalogError && <Typography variant="caption" color="text.secondary">Loading catalog…</Typography>}
        <List dense disablePadding>
          {filtered.map((n) => (
            <ListItemButton key={n.name} onClick={() => addStep(n)} title={n.description} sx={{ py: 0.25, px: 0.5, fontFamily: 'monospace', fontSize: 12 }}>
              + {n.name}
            </ListItemButton>
          ))}
        </List>
      </Box>

      <Box component="main" sx={{ flex: 1, position: 'relative' }}>
        <ReactFlow
          nodes={flowNodes}
          edges={flowEdges}
          nodeTypes={nodeTypes}
          onNodesChange={onNodesChange}
          onConnect={onConnect}
          onEdgesDelete={onEdgesDelete}
          onNodeClick={(_e, n) => { setSelectedId(n.id); setSelectedEdgeId(null); }}
          onEdgeClick={(_e, ed) => { setSelectedEdgeId(ed.id); setSelectedId(null); }}
          onPaneClick={() => { setSelectedId(null); setSelectedEdgeId(null); }}
          deleteKeyCode={['Delete']}
          fitView
          minZoom={0.2}
          maxZoom={2}
        >
          <Background color={theme.palette.divider} gap={20} />
          <Controls />
        </ReactFlow>
        {steps.length === 0 && (
          <Typography
            color="text.secondary"
            sx={{ position: 'absolute', top: '45%', width: '100%', textAlign: 'center', pointerEvents: 'none' }}
          >
            Click a node on the left to add a step; drag from a node's bottom handle to another's top to make it depend on it.
          </Typography>
        )}
      </Box>

      <Box component="aside" sx={{ ...sidePanel, width: 340, borderLeft: 1, borderColor: 'divider' }}>
        <Box sx={{ display: 'flex', gap: 1, alignItems: 'center', mb: 1 }}>
          <Typography variant="subtitle2">Pipeline</Typography>
          <Typography variant="caption" sx={{ color: report?.valid ? 'success.main' : 'text.secondary' }}>
            {steps.length === 0 ? 'empty' : report ? (report.valid ? '✓ valid' : '✕ invalid') : 'validating…'}
          </Typography>
          <Button variant="contained" size="small" onClick={run} disabled={!canRun} sx={{ ml: 'auto' }}>
            {running ? 'Running…' : 'Run'}
          </Button>
        </Box>
        {validateError && <Issue kind="error">Validator: {validateError}</Issue>}
        {globalIssues?.errors.map((m) => <Issue key={m} kind="error">{m}</Issue>)}
        {globalIssues?.warnings.map((m) => <Issue key={m} kind="warning">{m}</Issue>)}
        {report?.execution_order && (
          <Typography variant="caption" color="text.secondary">Order: {report.execution_order.join(' → ')}</Typography>
        )}

        {selected && (
          <Box component="section" sx={{ mt: 1.5 }}>
            <Box sx={{ display: 'flex', alignItems: 'center', gap: 1 }}>
              <Typography variant="subtitle2">{selected.id}</Typography>
              <Typography variant="caption" color="text.secondary">{selected.tool}</Typography>
              <Button size="small" color="error" onClick={() => removeSteps([selected.id])} sx={{ ml: 'auto' }}>Delete</Button>
            </Box>
            {selectedNode && <Typography variant="caption" component="p" color="text.secondary">{selectedNode.description}</Typography>}
            {selectedNode?.input_schema?.properties && (
              <Box component="ul" sx={{ pl: 2, color: 'text.secondary', my: 1 }}>
                {Object.entries(selectedNode.input_schema.properties).map(([k, v]) => (
                  <li key={k}>
                    <code>{k}</code>{selectedNode.required_inputs.includes(k) ? '*' : ''} {v.type ? `(${v.type})` : ''} {v.description ?? ''}
                  </li>
                ))}
              </Box>
            )}
            <TextField
              label="arguments (JSON; $stepId.field references another step's output)"
              value={selected.argsText}
              onChange={(e) => {
                const v = e.target.value;
                setSteps((prev) => prev.map((s) => (s.id === selected.id ? { ...s, argsText: v } : s)));
              }}
              multiline
              minRows={6}
              fullWidth
              spellCheck={false}
              InputProps={{ sx: { fontFamily: 'monospace', fontSize: 12 } }}
              sx={{ mt: 1 }}
            />
            {selectedErrors.map((m) => <Issue key={m} kind="error">{m}</Issue>)}
            {issues.get(selected.id)?.warnings.map((m) => <Issue key={m} kind="warning">{m}</Issue>)}
          </Box>
        )}

        {runOutput && (
          <Box component="section" sx={{ mt: 1.5 }}>
            <Typography variant="subtitle2">Run result</Typography>
            <Box component="pre" sx={{ ...codeBlock, maxHeight: 300, overflow: 'auto' }}>{runOutput}</Box>
          </Box>
        )}

        <Box component="details" sx={{ mt: 1.5 }}>
          <summary>Spec (ix_pipeline_run)</summary>
          <Box component="pre" sx={codeBlock}>{JSON.stringify(spec, null, 2)}</Box>
        </Box>
      </Box>
    </Box>
  );
};
