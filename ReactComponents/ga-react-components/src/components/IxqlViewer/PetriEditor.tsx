// PetriEditor — draw a Place/Transition net and ask IX what it can reach.
//
// Places are circles holding tokens, transitions are bars, arcs only join a
// place and a transition. Analyse sends the net to `ix_petri_analyze`
// through the same local-only, approval-gated /ix-pipeline/run route as the
// pipeline editor, and shows each verdict plus every dead marking with the
// shortest firing sequence that reaches it. The net is a specification
// oracle: it says which terminal states a runtime test must tell apart, it
// does not run anything (learn, csharp-advanced lesson 17).

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
import { Box, Button, TextField, Typography, useTheme } from '@mui/material';

import bufferPnml from './examples/petri-producer-consumer.pnml?raw';
import lifecyclePnml from './examples/petri-pipeline-lifecycle.pnml?raw';
import {
  analyzeArgs,
  canConnect,
  classifyDeadMarking,
  netProblems,
  parsePnml,
  petriLayout,
  readReport,
  type DeadMarking,
  type PetriNet,
  type PetriReport,
} from './petriNet';

interface PlaceData {
  id: string;
  tokens: number;
  /** Tokens in the dead marking being inspected, if any. */
  shown?: number;
  selected: boolean;
}

const tokenText = (n: number) => (n === 0 ? '' : n <= 4 ? '●'.repeat(n) : String(n));

const PlaceNode: React.FC<NodeProps<PlaceData>> = ({ data }) => {
  const inspecting = data.shown !== undefined;
  const n = inspecting ? data.shown! : data.tokens;
  return (
    <Box sx={{ display: 'flex', flexDirection: 'column', alignItems: 'center', width: 90 }}>
      <Box
        sx={{
          width: 52,
          height: 52,
          borderRadius: '50%',
          border: 2,
          borderColor: data.selected ? 'primary.main' : inspecting && n > 0 ? 'warning.main' : 'text.primary',
          bgcolor: 'background.paper',
          color: inspecting ? 'warning.main' : 'text.primary',
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'center',
          fontSize: n > 2 && n <= 4 ? 11 : 14,
          letterSpacing: -1,
          position: 'relative',
        }}
      >
        <Handle type="target" position={Position.Left} />
        {tokenText(n)}
        <Handle type="source" position={Position.Right} />
      </Box>
      <Typography variant="caption" sx={{ fontFamily: 'monospace', color: 'text.secondary', mt: 0.25 }}>
        {data.id}
      </Typography>
    </Box>
  );
};

interface TransitionData {
  id: string;
  selected: boolean;
  /** Position in the witness being inspected (1-based), if it fires there. */
  step?: number[];
}

const TransitionNode: React.FC<NodeProps<TransitionData>> = ({ data }) => (
  <Box sx={{ display: 'flex', flexDirection: 'column', alignItems: 'center', width: 90 }}>
    <Box
      sx={{
        width: 14,
        height: 52,
        bgcolor: data.selected ? 'primary.main' : data.step ? 'warning.main' : 'text.primary',
        borderRadius: 0.5,
        position: 'relative',
      }}
    >
      <Handle type="target" position={Position.Left} />
      <Handle type="source" position={Position.Right} />
    </Box>
    <Typography variant="caption" sx={{ fontFamily: 'monospace', color: 'text.secondary', mt: 0.25, textAlign: 'center' }}>
      {data.id}
      {data.step && ` #${data.step.join(',')}`}
    </Typography>
  </Box>
);

const nodeTypes = { place: PlaceNode, transition: TransitionNode };
const arcId = (a: { source: string; target: string }) => `${a.source}->${a.target}`;
const EMPTY: PetriNet = { name: 'net', places: [], transitions: [], arcs: [] };
const VERDICT_MARK = { holds: '✓', fails: '✕', unknown: '?' } as const;
const OUTCOME_COLOR = { success: 'success.main', failure: 'error.main', cancelled: 'warning.main', deadlock: 'error.main' } as const;

export const PetriEditor: React.FC = () => {
  const theme = useTheme();
  const [net, setNet] = useState<PetriNet>(EMPTY);
  const [positions, setPositions] = useState<Record<string, { x: number; y: number }>>({});
  const [selectedId, setSelectedId] = useState<string | null>(null);
  const [selectedArc, setSelectedArc] = useState<string | null>(null);
  const [report, setReport] = useState<PetriReport | null>(null);
  const [inspected, setInspected] = useState<DeadMarking | null>(null);
  const [analysing, setAnalysing] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [graphVersion, setGraphVersion] = useState(0);
  const seq = useRef(0);

  // A verdict belongs to the net it was computed for; any edit drops it.
  useEffect(() => {
    seq.current++;
    setReport(null);
    setInspected(null);
    setError(null);
  }, [net]);
  const initialTokens = useMemo(() => Object.fromEntries(net.places.map((p) => [p.id, p.tokens])), [net.places]);

  const problems = useMemo(() => netProblems(net), [net]);

  const load = useCallback((pnml: string) => {
    try {
      const parsed = parsePnml(pnml);
      setNet(parsed);
      setPositions(petriLayout(parsed));
      setSelectedId(null);
      setSelectedArc(null);
      setGraphVersion((v) => v + 1);
    } catch (e) {
      setError(`PNML: ${e instanceof Error ? e.message : String(e)}`);
    }
  }, []);

  const importFile = useCallback((e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    e.target.value = '';
    if (file) file.text().then(load, (err: unknown) => setError(String(err)));
  }, [load]);

  const freshId = (prefix: string) => {
    const taken = new Set([...net.places.map((p) => p.id), ...net.transitions]);
    let n = 1;
    while (taken.has(`${prefix}${n}`)) n++;
    return `${prefix}${n}`;
  };

  const add = (kind: 'place' | 'transition') => {
    const id = freshId(kind === 'place' ? 'p' : 't');
    const count = net.places.length + net.transitions.length;
    setPositions((p) => ({ ...p, [id]: { x: 40 + (count % 5) * 170, y: 40 + Math.floor(count / 5) * 110 } }));
    setNet((n) => (kind === 'place'
      ? { ...n, places: [...n.places, { id, tokens: 0 }] }
      : { ...n, transitions: [...n.transitions, id] }));
    setSelectedId(id);
  };

  const removeNodes = useCallback((ids: string[]) => {
    const gone = new Set(ids);
    setNet((n) => ({
      ...n,
      places: n.places.filter((p) => !gone.has(p.id)),
      transitions: n.transitions.filter((t) => !gone.has(t)),
      arcs: n.arcs.filter((a) => !gone.has(a.source) && !gone.has(a.target)),
    }));
    setSelectedId((cur) => (cur && gone.has(cur) ? null : cur));
  }, []);

  const onNodesChange = useCallback((changes: NodeChange[]) => {
    const removed = changes.flatMap((c) => (c.type === 'remove' ? [c.id] : []));
    if (removed.length > 0) removeNodes(removed);
    const moves = changes.filter((c) => c.type !== 'remove');
    if (moves.length === 0) return;
    setPositions((prev) => {
      const next = applyNodeChanges(moves, Object.entries(prev).map(([id, position]) => ({ id, position, data: null })));
      return Object.fromEntries(next.map((n) => [n.id, n.position]));
    });
  }, [removeNodes]);

  const onConnect = useCallback((c: Connection) => {
    if (!c.source || !c.target) return;
    const source = c.source;
    const target = c.target;
    setNet((n) => {
      if (!canConnect(n, source, target) || n.arcs.some((a) => a.source === source && a.target === target)) return n;
      return { ...n, arcs: [...n.arcs, { source, target, weight: 1 }] };
    });
  }, []);

  const onEdgesDelete = useCallback((deleted: Edge[]) => {
    const gone = new Set(deleted.map((e) => e.id));
    setNet((n) => ({ ...n, arcs: n.arcs.filter((a) => !gone.has(arcId(a))) }));
    setSelectedArc(null);
  }, []);

  const witnessSteps = useMemo(() => {
    const steps = new Map<string, number[]>();
    inspected?.witness.forEach((t, i) => steps.set(t, [...(steps.get(t) ?? []), i + 1]));
    return steps;
  }, [inspected]);

  const flowNodes: Node[] = useMemo(() => [
    ...net.places.map((p) => ({
      id: p.id,
      type: 'place',
      position: positions[p.id] ?? { x: 0, y: 0 },
      selected: p.id === selectedId,
      data: { id: p.id, tokens: p.tokens, shown: inspected ? inspected.tokens[p.id] ?? 0 : undefined, selected: p.id === selectedId },
    })),
    ...net.transitions.map((t) => ({
      id: t,
      type: 'transition',
      position: positions[t] ?? { x: 0, y: 0 },
      selected: t === selectedId,
      data: { id: t, selected: t === selectedId, step: witnessSteps.get(t) },
    })),
  ], [net, positions, selectedId, inspected, witnessSteps]);

  const edgeColor = theme.palette.text.secondary;
  const flowEdges: Edge[] = useMemo(() => net.arcs.map((a) => {
    const id = arcId(a);
    const color = id === selectedArc ? theme.palette.primary.main : edgeColor;
    return {
      id,
      source: a.source,
      target: a.target,
      selected: id === selectedArc,
      label: a.weight > 1 ? String(a.weight) : undefined,
      labelStyle: { fill: edgeColor },
      labelBgStyle: { fill: theme.palette.background.default },
      style: { stroke: color, strokeWidth: id === selectedArc ? 2.5 : 1.5 },
      markerEnd: { type: MarkerType.ArrowClosed, color },
    };
  }), [net.arcs, selectedArc, edgeColor, theme.palette.primary.main, theme.palette.background.default]);

  const analyse = useCallback(() => {
    const mine = ++seq.current;
    setAnalysing(true);
    setError(null);
    fetch('/ix-pipeline/run', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ steps: [{ id: 'petri', tool: 'ix_petri_analyze', arguments: analyzeArgs(net) }] }),
    })
      .then(async (res) => {
        const json = (await res.json()) as { ok?: boolean; result?: { results?: Record<string, unknown> }; error?: string };
        if (mine !== seq.current) return;
        if (!res.ok || json.ok !== true) setError(json.error ?? `HTTP ${res.status}`);
        else setReport(readReport(json.result?.results?.petri));
      })
      .catch((e: unknown) => { if (mine === seq.current) setError(String(e)); })
      .finally(() => { if (mine === seq.current) setAnalysing(false); });
  }, [net]);

  const selectedPlace = net.places.find((p) => p.id === selectedId);
  const arc = net.arcs.find((a) => arcId(a) === selectedArc);
  const sidePanel = { p: 1.5, overflow: 'auto', bgcolor: 'background.paper', color: 'text.primary' } as const;

  return (
    <Box sx={{ display: 'flex', height: '100%', minHeight: 0 }}>
      <Box component="aside" sx={{ ...sidePanel, width: 220, borderRight: 1, borderColor: 'divider', display: 'flex', flexDirection: 'column', gap: 1 }}>
        <Typography variant="subtitle2">Petri net</Typography>
        <Button size="small" variant="outlined" onClick={() => add('place')}>+ Place</Button>
        <Button size="small" variant="outlined" onClick={() => add('transition')}>+ Transition</Button>
        <Typography variant="caption" color="text.secondary">
          Drag from a node's right handle to another node's left one. Arcs only join a place and a transition. Delete removes the selection.
        </Typography>
        <Typography variant="subtitle2" sx={{ mt: 1 }}>Course examples</Typography>
        <Button size="small" onClick={() => load(bufferPnml)} title="learn, petri-nets lesson 1">Bounded buffer (L1)</Button>
        <Button size="small" onClick={() => load(lifecyclePnml)} title="learn, petri-nets lesson 14 / csharp-advanced lesson 17">Pipeline lifecycle (L14)</Button>
        <Button size="small" component="label">
          Import PNML
          <input hidden type="file" accept=".pnml,.xml" onChange={importFile} />
        </Button>
      </Box>

      <Box component="main" sx={{
        flex: 1,
        position: 'relative',
        '& .react-flow__controls-button': {
          bgcolor: 'background.paper', color: 'text.primary', borderBottomColor: 'divider',
          '& svg': { fill: 'currentColor' },
        },
        '& .react-flow__attribution': { bgcolor: 'transparent', '& a': { color: 'text.secondary' } },
      }}>
        <ReactFlow
          key={graphVersion}
          nodes={flowNodes}
          edges={flowEdges}
          nodeTypes={nodeTypes}
          onNodesChange={onNodesChange}
          onConnect={onConnect}
          onEdgesDelete={onEdgesDelete}
          onNodeClick={(_e, n) => { setSelectedId(n.id); setSelectedArc(null); }}
          onEdgeClick={(_e, ed) => { setSelectedArc(ed.id); setSelectedId(null); }}
          onPaneClick={() => { setSelectedId(null); setSelectedArc(null); }}
          deleteKeyCode={['Delete']}
          fitView
          minZoom={0.2}
          maxZoom={2}
        >
          <Background color={theme.palette.divider} gap={20} />
          <Controls />
        </ReactFlow>
        {net.places.length + net.transitions.length === 0 && (
          <Typography color="text.secondary" sx={{ position: 'absolute', top: '45%', width: '100%', textAlign: 'center', pointerEvents: 'none' }}>
            Load a course example or add places and transitions.
          </Typography>
        )}
      </Box>

      <Box component="aside" sx={{ ...sidePanel, width: 340, borderLeft: 1, borderColor: 'divider' }}>
        <Box sx={{ display: 'flex', alignItems: 'center', gap: 1, mb: 1 }}>
          <Typography variant="subtitle2">{net.name}</Typography>
          <Typography variant="caption" color="text.secondary">
            {net.places.length} places · {net.transitions.length} transitions · {net.arcs.length} arcs
          </Typography>
        </Box>
        {problems.map((m) => <Typography key={m} variant="caption" component="div" color="error.main">✕ {m}</Typography>)}
        <Button
          variant="contained"
          size="small"
          fullWidth
          onClick={analyse}
          disabled={analysing || problems.length > 0 || net.places.length === 0}
          sx={{ my: 1 }}
        >
          {analysing ? 'Analysing…' : 'Analyse (ix_petri_analyze)'}
        </Button>
        {error && <Typography variant="caption" component="div" color="error.main">✕ {error}</Typography>}

        {selectedPlace && (
          <TextField
            label={`tokens in ${selectedPlace.id}`}
            type="number"
            size="small"
            fullWidth
            value={selectedPlace.tokens}
            inputProps={{ min: 0 }}
            onChange={(e) => {
              const v = Math.max(0, Math.floor(Number(e.target.value) || 0));
              setNet((n) => ({ ...n, places: n.places.map((p) => (p.id === selectedPlace.id ? { ...p, tokens: v } : p)) }));
            }}
            sx={{ my: 1 }}
          />
        )}
        {arc && (
          <TextField
            label={`weight of ${arc.source} → ${arc.target}`}
            type="number"
            size="small"
            fullWidth
            value={arc.weight}
            inputProps={{ min: 1 }}
            onChange={(e) => {
              const v = Math.max(1, Math.floor(Number(e.target.value) || 1));
              setNet((n) => ({ ...n, arcs: n.arcs.map((a) => (arcId(a) === selectedArc ? { ...a, weight: v } : a)) }));
            }}
            sx={{ my: 1 }}
          />
        )}

        {report && (
          <Box component="section">
            <Typography variant="subtitle2" sx={{ mt: 1 }}>
              {report.states} reachable markings{report.truncated ? ' (truncated: undecided verdicts are ?)' : ''}
            </Typography>
            {report.properties.map((p) => (
              <Typography
                key={p.key}
                variant="body2"
                sx={{ color: p.verdict === 'holds' ? 'success.main' : p.verdict === 'fails' ? 'error.main' : 'text.secondary' }}
              >
                {VERDICT_MARK[p.verdict]} {p.label}{p.detail ? ` — ${p.detail}` : ''}
              </Typography>
            ))}
            <Typography variant="subtitle2" sx={{ mt: 1.5 }}>
              Dead markings ({report.deadMarkings.length})
            </Typography>
            <Typography variant="caption" color="text.secondary" component="p">
              Each terminal state a runtime test must tell apart, with the shortest firing sequence reaching it. The
              outcome is read from the place names, and a success leaves no token outside an outcome place except a
              resource back to its initial count; click one to show it on the net.
            </Typography>
            {report.deadMarkings.map((m, i) => {
              const outcome = classifyDeadMarking(m, initialTokens);
              const marking = Object.entries(m.tokens).map(([p, n]) => `${p}=${n}`).join(' ');
              const active = inspected === m;
              return (
                <Box
                  key={i}
                  onClick={() => setInspected(active ? null : m)}
                  sx={{
                    p: 0.75, my: 0.5, borderRadius: 1, cursor: 'pointer', border: 1,
                    borderColor: active ? 'primary.main' : 'divider',
                  }}
                >
                  <Typography variant="caption" sx={{ color: OUTCOME_COLOR[outcome], fontWeight: 600 }}>{outcome}</Typography>
                  <Typography variant="caption" component="div" sx={{ fontFamily: 'monospace' }}>{marking}</Typography>
                  <Typography variant="caption" component="div" color="text.secondary">
                    {m.witness.length ? m.witness.join(' → ') : '(initial marking)'}
                  </Typography>
                </Box>
              );
            })}
          </Box>
        )}
      </Box>
    </Box>
  );
};
