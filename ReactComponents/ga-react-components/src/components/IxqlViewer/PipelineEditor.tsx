// PipelineEditor — ComfyUI-style editor for IX pipelines (tracer bullet).
//
// Palette from ix_node_catalog, steps as React Flow nodes, edges as
// depends_on, live ix_pipeline_validate with errors pinned to their node,
// and Run via ix_pipeline_run. Talks to the local-only /ix-pipeline/*
// dev-server routes (vite.config.ts → dev-server/ixMcpBridge.ts).
// Agents can read the shown pipeline and propose a replacement; a proposal
// only reaches the graph through the user's Accept (see vite.config.ts).

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
  bundleEdges,
  defaultArgsText,
  diffSpecs,
  groupOf,
  groupedPositions,
  issuesByStep,
  nextStepId,
  specToGraph,
  type CatalogNode,
  type EditorEdge,
  type EditorStep,
  type SpecDiff,
  type ValidationReport,
} from './pipelineSpec';
import type { JevAdvice, ProposalSummary } from './jevAdvice';
import gaHarmonicFieldSpec from './examples/ga-harmonic-field.pipeline.json?raw';

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

interface SectionNodeData {
  name: string;
  count: number;
  width: number;
  height: number;
}

// Background frame for one section (ComfyUI-style group). Its handles are
// only anchors for the bundled edges between sections.
const SectionNode: React.FC<NodeProps<SectionNodeData>> = ({ data }) => (
  <Box
    sx={{
      width: data.width,
      height: data.height,
      border: 1,
      borderStyle: 'dashed',
      borderColor: 'divider',
      borderRadius: 2,
      bgcolor: 'action.hover',
      px: 1.5,
      py: 0.5,
      pointerEvents: 'none',
    }}
  >
    <Handle type="target" position={Position.Top} style={{ opacity: 0 }} />
    <Typography variant="caption" sx={{ color: 'text.secondary', fontWeight: 600, letterSpacing: 0.5 }}>
      {data.name} · {data.count}
    </Typography>
    <Handle type="source" position={Position.Bottom} style={{ opacity: 0 }} />
  </Box>
);

const nodeTypes = { step: StepNode, section: SectionNode };
const SECTION = 'section:';
// Rough step-node footprint, for sizing the section frames around them.
const STEP_W = 195;
const STEP_H = 72;

async function postJson(url: string, body: unknown): Promise<{ ok: boolean; result?: unknown; error?: string }> {
  const res = await fetch(url, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(body) });
  const json = (await res.json()) as { ok?: boolean; result?: unknown; error?: string };
  if (!res.ok) return { ok: false, error: json.error ?? `HTTP ${res.status}` };
  return { ok: json.ok === true, result: json.result, error: json.error };
}

/** A pending agent proposal, as the dev server stores it (not yet checked). */
interface Proposal {
  id: string;
  title: string;
  author: string;
  base_revision: number | null;
  created_at: string;
  spec: unknown;
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
  const [importError, setImportError] = useState<string | null>(null);
  // Bumped on every load so React Flow remounts and fits the new graph.
  const [graphVersion, setGraphVersion] = useState(0);

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
  const propertiesByTool = useMemo(
    () => Object.fromEntries((catalog ?? []).map((n) => [n.name, n.input_schema?.properties])),
    [catalog],
  );
  const { spec, argErrors } = useMemo(
    () => buildSpec(steps, edges, requiredByTool, propertiesByTool),
    [steps, edges, requiredByTool, propertiesByTool],
  );
  // One status for the header: the graph can be valid for IX while a step's
  // own arguments are not, and Run already requires both.
  const locallyValid = Object.keys(argErrors).length === 0;
  const pipelineValid = report?.valid === true && locallyValid;

  // Live validation, debounced. Every spec change invalidates the previous
  // report at once (so Run cannot fire on a spec that was never validated),
  // and a response is applied only if it belongs to the latest spec.
  useEffect(() => {
    const seq = ++validationSeq.current;
    setReport(null);
    setValidateError(null);
    // A result shown next to an edited graph would be misattributed; `run`
    // also drops any response that finishes after this change.
    setRunOutput(null);
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

  // Agent connectivity. The shown pipeline is mirrored to /ix-pipeline/current
  // so an agent can read what it is patching; its proposals arrive as whole
  // specs and wait here until the user accepts or rejects them.
  const [revision, setRevision] = useState<number | null>(null);
  const [proposals, setProposals] = useState<Proposal[]>([]);
  useEffect(() => {
    const handle = setTimeout(() => {
      fetch('/ix-pipeline/current', { method: 'PUT', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ spec }) })
        .then(async (res) => { if (res.ok) setRevision(((await res.json()) as { revision: number }).revision); })
        .catch(() => setRevision(null));
    }, 300);
    return () => clearTimeout(handle);
  }, [spec]);
  useEffect(() => {
    let alive = true;
    const poll = () => fetch('/ix-pipeline/proposals')
      .then(async (res) => {
        const json = (await res.json()) as { proposals?: Proposal[] };
        if (alive && res.ok && json.proposals) setProposals(json.proposals);
      })
      .catch(() => { /* dev server restarting; the next poll retries */ });
    void poll();
    const timer = setInterval(poll, 2000);
    return () => { alive = false; clearInterval(timer); };
  }, []);
  const reviewed = useMemo(() => proposals.map((p) => {
    try {
      const graph = specToGraph(p.spec);
      return { p, diff: diffSpecs(spec, buildSpec(graph.steps, graph.edges).spec), error: null };
    } catch (e) {
      return { p, diff: null, error: e instanceof Error ? e.message : String(e) };
    }
  }), [proposals, spec]);
  // Jev shadow advice, per proposal: shown and recorded with the user's
  // decision, never read by Accept or Reject.
  const [advice, setAdvice] = useState<Record<string, { advice?: JevAdvice; error?: string; asking?: boolean }>>({});
  const [jevStats, setJevStats] = useState<{ calls: number; spend_usd: number; cap_usd: number; decided: number; agreed: number } | null>(null);
  useEffect(() => {
    fetch('/ix-pipeline/advise/stats')
      .then(async (res) => { if (res.ok) setJevStats(await res.json()); })
      .catch(() => { /* no ledger yet */ });
  }, []);
  const askJev = useCallback((p: Proposal, diff: SpecDiff) => {
    const approval = new Map((catalog ?? []).map((n) => [n.name, n.approval]));
    const graph = specToGraph(p.spec);
    const toolAfter = new Map(graph.steps.map((s) => [s.id, s.tool]));
    const toolBefore = new Map(spec.steps.map((s) => [s.id, s.tool]));
    const summary: ProposalSummary = {
      title: p.title,
      author: p.author,
      based_on_current: p.base_revision === null || p.base_revision === revision,
      added: diff.added.map((id) => {
        const tool = toolAfter.get(id) ?? '';
        return { id, tool, tier: approval.get(tool)?.tier, effect: approval.get(tool)?.effect };
      }),
      removed: diff.removed.map((id) => ({ id, tool: toolBefore.get(id) ?? '' })),
      changed: Object.entries(diff.changed).map(([id, fields]) => ({ id, fields })),
      steps_before: spec.steps.length,
      steps_after: graph.steps.length,
    };
    setAdvice((a) => ({ ...a, [p.id]: { asking: true } }));
    fetch('/ix-pipeline/advise', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ proposal_id: p.id, summary }) })
      .then(async (res) => {
        const json = (await res.json()) as { advice?: JevAdvice; stats?: typeof jevStats; error?: string };
        if (!res.ok || !json.advice) throw new Error(json.error ?? `HTTP ${res.status}`);
        setAdvice((a) => ({ ...a, [p.id]: { advice: json.advice } }));
        if (json.stats) setJevStats(json.stats);
      })
      .catch((e: unknown) => setAdvice((a) => ({ ...a, [p.id]: { error: e instanceof Error ? e.message : String(e) } })));
  }, [catalog, spec, revision]);
  const dismiss = useCallback((id: string, decision: 'accept' | 'reject') => {
    setProposals((prev) => prev.filter((p) => p.id !== id));
    void fetch(`/ix-pipeline/proposals/${encodeURIComponent(id)}`, { method: 'DELETE' });
    if (advice[id]?.advice) {
      fetch('/ix-pipeline/advise/outcome', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ proposal_id: id, decision }) })
        .then(async (res) => { if (res.ok) setJevStats(await res.json()); })
        .catch(() => { /* the ledger misses one outcome; nothing acts on it */ });
    }
  }, [advice]);

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

  const sectionNodes: Node<SectionNodeData>[] = useMemo(() => {
    const boxes = new Map<string, { minX: number; minY: number; maxX: number; maxY: number; count: number }>();
    for (const step of steps) {
      const p = positions[step.id];
      if (!p) continue;
      const g = groupOf(step);
      const b = boxes.get(g) ?? { minX: p.x, minY: p.y, maxX: p.x, maxY: p.y, count: 0 };
      boxes.set(g, {
        minX: Math.min(b.minX, p.x), minY: Math.min(b.minY, p.y),
        maxX: Math.max(b.maxX, p.x), maxY: Math.max(b.maxY, p.y), count: b.count + 1,
      });
    }
    return [...boxes].map(([name, b]) => ({
      id: SECTION + name,
      type: 'section',
      position: { x: b.minX - 16, y: b.minY - 30 },
      data: { name, count: b.count, width: b.maxX - b.minX + STEP_W + 32, height: b.maxY - b.minY + STEP_H + 46 },
      draggable: false,
      selectable: false,
      deletable: false,
      focusable: false,
      zIndex: -1,
    }));
  }, [steps, positions]);

  const stepNodes: Node<StepNodeData>[] = useMemo(
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
  const flowNodes: Node[] = useMemo(() => [...sectionNodes, ...stepNodes], [sectionNodes, stepNodes]);

  const edgeColor = theme.palette.text.secondary;
  const accentColor = theme.palette.primary.main;
  // Edges between sections are drawn as one bundle per pair of sections.
  // A step's own edges are drawn in full while it is selected, and edges
  // inside a section always are.
  const flowEdges: Edge[] = useMemo(() => {
    const group = new Map(steps.map((s) => [s.id, groupOf(s)]));
    const bundles: Edge[] = bundleEdges(steps, edges).map((b) => ({
      id: `bundle:${b.source}->${b.target}`,
      source: SECTION + b.source,
      target: SECTION + b.target,
      label: b.count > 1 ? `×${b.count}` : undefined,
      selectable: false,
      deletable: false,
      style: { stroke: edgeColor, strokeWidth: 1.5 + Math.min(b.count, 10) * 0.25, opacity: 0.8 },
      labelStyle: { fill: edgeColor, fontSize: 11 },
      labelBgStyle: { fill: theme.palette.background.default },
      markerEnd: { type: MarkerType.ArrowClosed, color: edgeColor },
    }));
    const detailed = edges
      .filter((e) => {
        const id = `${e.source}->${e.target}`;
        return group.get(e.source) === group.get(e.target)
          || e.source === selectedId || e.target === selectedId || id === selectedEdgeId;
      })
      .map((e) => {
        const id = `${e.source}->${e.target}`;
        const color = id === selectedEdgeId || e.source === selectedId || e.target === selectedId ? accentColor : edgeColor;
        return {
          id,
          source: e.source,
          target: e.target,
          selected: id === selectedEdgeId,
          style: { stroke: color, strokeWidth: id === selectedEdgeId ? 2.5 : 1.5 },
          markerEnd: { type: MarkerType.ArrowClosed, color },
        };
      });
    return [...bundles, ...detailed];
  }, [steps, edges, edgeColor, accentColor, selectedEdgeId, selectedId, theme.palette.background.default]);

  // `steps` is the model: a keyboard "remove" deletes the step and its edges;
  // position changes only move it.
  const onNodesChange = useCallback((changes: NodeChange[]) => {
    const removed = changes.flatMap((c) => (c.type === 'remove' && !c.id.startsWith(SECTION) ? [c.id] : []));
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

  // Replaces the whole graph. Nothing is kept from the previous one: a
  // half-merged import would validate a pipeline nobody wrote.
  const loadSpec = useCallback((specJson: string) => {
    try {
      const graph = specToGraph(JSON.parse(specJson));
      setSteps(graph.steps);
      setEdges(graph.edges);
      setPositions(groupedPositions(graph.steps, graph.edges));
      setSelectedId(null);
      setSelectedEdgeId(null);
      setImportError(null);
      setGraphVersion((v) => v + 1);
    } catch (e) {
      setImportError(e instanceof Error ? e.message : String(e));
    }
  }, []);

  const importFile = useCallback((e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    e.target.value = '';
    if (file) file.text().then(loadSpec, (err: unknown) => setImportError(String(err)));
  }, [loadSpec]);

  const exportSpec = useCallback(() => {
    const url = URL.createObjectURL(new Blob([JSON.stringify(spec, null, 2)], { type: 'application/json' }));
    const a = document.createElement('a');
    a.href = url;
    a.download = 'pipeline.json';
    a.click();
    URL.revokeObjectURL(url);
  }, [spec]);

  const run = useCallback(() => {
    // Bound to the spec validation generation: an edit bumps validationSeq,
    // and a run started before it no longer publishes its output.
    const seq = validationSeq.current;
    setRunning(true);
    setRunOutput(null);
    postJson('/ix-pipeline/run', spec)
      .then((r) => {
        if (seq === validationSeq.current) setRunOutput(r.ok ? JSON.stringify(r.result, null, 2) : `Error: ${r.error}`);
      })
      .catch((e: unknown) => {
        if (seq === validationSeq.current) setRunOutput(`Error: ${String(e)}`);
      })
      .finally(() => setRunning(false));
  }, [spec]);

  const selected = steps.find((s) => s.id === selectedId) ?? null;
  const selectedNode = selected ? catalog?.find((n) => n.name === selected.tool) : undefined;
  const filtered = (catalog ?? []).filter((n) => n.name.toLowerCase().includes(filter.toLowerCase()));
  const canRun = steps.length > 0 && pipelineValid && !running;
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

      <Box
        component="main"
        sx={{
          flex: 1,
          position: 'relative',
          // reactflow's stylesheet paints its chrome white; follow the theme so
          // the canvas stays readable in dark mode.
          '& .react-flow__controls-button': {
            bgcolor: 'background.paper',
            color: 'text.primary',
            borderBottomColor: 'divider',
            '& svg': { fill: 'currentColor' },
            '&:hover': { bgcolor: 'action.hover' },
          },
          '& .react-flow__attribution': { bgcolor: 'transparent', '& a': { color: 'text.secondary' } },
          '& .react-flow__edge-path': { stroke: theme.palette.text.secondary },
          '& .react-flow__handle': { bgcolor: 'text.primary', borderColor: 'background.paper' },
        }}
      >
        <ReactFlow
          key={graphVersion}
          nodes={flowNodes}
          edges={flowEdges}
          nodeTypes={nodeTypes}
          onNodesChange={onNodesChange}
          onConnect={onConnect}
          onEdgesDelete={onEdgesDelete}
          onNodeClick={(_e, n) => { if (!n.id.startsWith(SECTION)) { setSelectedId(n.id); setSelectedEdgeId(null); } }}
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
          <Typography variant="caption" sx={{ color: pipelineValid ? 'success.main' : 'text.secondary' }}>
            {steps.length === 0 ? 'empty' : report || !locallyValid ? (pipelineValid ? '✓ valid' : '✕ invalid') : 'validating…'}
          </Typography>
          <Button variant="contained" size="small" onClick={run} disabled={!canRun} sx={{ ml: 'auto' }}>
            {running ? 'Running…' : 'Run'}
          </Button>
        </Box>
        <Box sx={{ display: 'flex', gap: 0.5, mb: 1 }}>
          <Button
            size="small"
            variant="outlined"
            onClick={() => loadSpec(gaHarmonicFieldSpec)}
            title="C major harmonic field: ICV motion, substitutions, harmonic paths, clustering, T/S/D functions (53 steps)"
          >
            GA example
          </Button>
          <Button size="small" variant="outlined" component="label">
            Import
            <input hidden type="file" accept=".json,application/json" onChange={importFile} />
          </Button>
          <Button size="small" variant="outlined" onClick={exportSpec} disabled={steps.length === 0}>
            Export
          </Button>
        </Box>
        {importError && <Issue kind="error">Import: {importError}</Issue>}
        {validateError && <Issue kind="error">Validator: {validateError}</Issue>}
        {globalIssues?.errors.map((m) => <Issue key={m} kind="error">{m}</Issue>)}
        {globalIssues?.warnings.map((m) => <Issue key={m} kind="warning">{m}</Issue>)}
        {report?.execution_order && (
          <Box component="details" sx={{ color: 'text.secondary', fontSize: 12 }}>
            <summary>Execution order ({report.execution_order.length})</summary>
            {report.execution_order.join(' → ')}
          </Box>
        )}

        {reviewed.length > 0 && (
          <Box component="section" sx={{ mt: 1.5 }}>
            <Typography variant="subtitle2">Agent proposals ({reviewed.length})</Typography>
            {jevStats && (
              <Typography variant="caption" component="div" color="text.secondary">
                Jev shadow: {jevStats.calls} calls · ${jevStats.spend_usd.toFixed(5)} of ${jevStats.cap_usd} · agreed with you {jevStats.agreed}/{jevStats.decided}
              </Typography>
            )}
            {reviewed.map(({ p, diff, error }) => {
              const changedIds = diff ? Object.keys(diff.changed) : [];
              const noop = diff && diff.added.length + diff.removed.length + changedIds.length === 0;
              return (
                <Box key={p.id} sx={{ border: 1, borderColor: 'primary.main', borderRadius: 1, p: 0.75, my: 0.5 }}>
                  <Typography variant="body2" sx={{ fontWeight: 600 }}>{p.title}</Typography>
                  <Typography variant="caption" component="div" color="text.secondary">
                    by {p.author}
                    {p.base_revision !== null && revision !== null && p.base_revision !== revision
                      && ` · based on revision ${p.base_revision}, the graph is now at ${revision}: accepting also undoes your edits since`}
                  </Typography>
                  {error && <Issue kind="error">not a valid pipeline: {error}</Issue>}
                  {diff && (
                    <Typography variant="caption" component="div" sx={{ fontFamily: 'monospace' }}>
                      {noop && 'no change'}
                      {diff.added.length > 0 && <Box sx={{ color: 'success.main' }}>+ {diff.added.join(', ')}</Box>}
                      {diff.removed.length > 0 && <Box sx={{ color: 'error.main' }}>− {diff.removed.join(', ')}</Box>}
                      {changedIds.map((id) => <Box key={id} sx={{ color: 'warning.main' }}>~ {id} ({diff.changed[id].join(', ')})</Box>)}
                    </Typography>
                  )}
                  {advice[p.id]?.advice && (() => {
                    const a = advice[p.id].advice!;
                    return (
                      <Typography variant="caption" component="div" color="text.secondary" sx={{ mt: 0.5 }} title="Shadow advice: recorded next to your decision, never acted on">
                        Jev (shadow): <b>{a.recommendation}</b> ({Math.round(a.confidence * 100)}%) · risk {a.risk.toFixed(1)}/2 · matches title {Math.round(a.matches_title * 100)}%
                      </Typography>
                    );
                  })()}
                  {advice[p.id]?.error && <Issue kind="warning">Jev: {advice[p.id].error}</Issue>}
                  <Box sx={{ display: 'flex', gap: 0.5, mt: 0.5 }}>
                    <Button
                      size="small"
                      variant="contained"
                      disabled={!diff}
                      title="Replaces the graph; it is then validated as usual and nothing runs until you click Run"
                      onClick={() => { loadSpec(JSON.stringify(p.spec)); dismiss(p.id, 'accept'); }}
                    >
                      Accept
                    </Button>
                    <Button size="small" onClick={() => dismiss(p.id, 'reject')}>Reject</Button>
                    {diff && !advice[p.id]?.advice && (
                      <Button
                        size="small"
                        disabled={advice[p.id]?.asking}
                        onClick={() => askJev(p, diff)}
                        title="One paid TypeSafe call (a fraction of a cent); sends step ids, tool names and approval tiers, never arguments"
                        sx={{ ml: 'auto' }}
                      >
                        {advice[p.id]?.asking ? 'Asking…' : 'Ask Jev'}
                      </Button>
                    )}
                  </Box>
                </Box>
              );
            })}
          </Box>
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
