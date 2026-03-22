// IxQL Pipeline Viewer — Main component
// Renders IxQL pipelines as interactive, explorable visualizations

import React, { useState, useCallback, useMemo } from 'react';
import { ReactFlowProvider } from 'reactflow';

import { parseIxql } from './IxqlParser';
import { analyzeLolli } from './LolliAnalyzer';
import { analyzeAmdahl } from './AmdahlAnalyzer';
import { IxqlGraphView } from './IxqlGraph';
import { DetailPanel } from './DetailPanel';
import type { IxqlBinding, IxqlGraph, LolliReport, AmdahlReport, ViewMode } from './types';
import './styles.css';

// --- Sample pipelines for demo ---
import { SAMPLE_CONSCIENCE_CYCLE, SAMPLE_ML_FEEDBACK } from './sampleData';

interface IxqlViewerProps {
  /** Pre-loaded IxQL source. If not provided, shows load UI. */
  initialSource?: string;
  /** Component height (default: 100%) */
  height?: string | number;
}

export const IxqlViewer: React.FC<IxqlViewerProps> = ({
  initialSource,
  height = '100%',
}) => {
  const [source, setSource] = useState<string>(initialSource || '');
  const [pasteInput, setPasteInput] = useState('');
  const [viewMode, setViewMode] = useState<ViewMode>('split');
  const [selectedBinding, setSelectedBinding] = useState<IxqlBinding | null>(null);
  const [hoveredBinding, setHoveredBinding] = useState<IxqlBinding | null>(null);

  // Parse + analyze
  const analysis = useMemo(() => {
    if (!source.trim()) return null;

    const graph = parseIxql(source);
    const lolliReport = analyzeLolli(graph);
    const amdahlReport = analyzeAmdahl(graph);
    return { graph, lolliReport, amdahlReport };
  }, [source]);

  // Highlighted chain: all upstream + downstream of hovered node
  const highlightedChain = useMemo(() => {
    const chain = new Set<string>();
    if (!hoveredBinding || !analysis) return chain;

    // Walk upstream
    const walkUp = (id: string) => {
      if (chain.has(id)) return;
      chain.add(id);
      const b = analysis.graph.bindings.find((b) => b.id === id);
      if (b) b.references.forEach(walkUp);
    };

    // Walk downstream
    const walkDown = (id: string) => {
      if (chain.has(id)) return;
      chain.add(id);
      const b = analysis.graph.bindings.find((b) => b.id === id);
      if (b) b.referencedBy.forEach(walkDown);
    };

    walkUp(hoveredBinding.id);
    walkDown(hoveredBinding.id);
    return chain;
  }, [hoveredBinding, analysis]);

  const handleLoadPaste = useCallback(() => {
    if (pasteInput.trim()) {
      setSource(pasteInput);
      setSelectedBinding(null);
    }
  }, [pasteInput]);

  const handleLoadSample = useCallback((sample: string) => {
    setSource(sample);
    setSelectedBinding(null);
    setPasteInput('');
  }, []);

  const handleNodeClick = useCallback((binding: IxqlBinding) => {
    setSelectedBinding(binding);
  }, []);

  const handleNodeHover = useCallback((binding: IxqlBinding | null) => {
    setHoveredBinding(binding);
  }, []);

  // Source view with syntax highlighting
  const renderSourceView = () => {
    if (!source) return null;
    const lines = source.split('\n');

    return (
      <div className="ixql-viewer__source-pane">
        <pre className="ixql-viewer__source-pre">
          {lines.map((line, i) => {
            const trimmed = line.trim();
            let lineClass = '';
            if (trimmed.startsWith('---')) lineClass = 'ixql-viewer__source-line--markdown-comment';
            else if (trimmed.startsWith('--')) lineClass = 'ixql-viewer__source-line--comment';
            else if (trimmed.match(/^\w[\w_]*\s*<-/)) lineClass = 'ixql-viewer__source-line--binding';
            else if (trimmed.startsWith('→')) lineClass = 'ixql-viewer__source-line--arrow';
            else if (/^(when|fan_out|parallel|filter|head|write|alert|governance_gate|compound)/.test(trimmed))
              lineClass = 'ixql-viewer__source-line--keyword';

            return (
              <div key={i} className={`ixql-viewer__source-line ${lineClass}`}>
                <span className="ixql-viewer__source-line-num">{i + 1}</span>
                <span>{line || ' '}</span>
              </div>
            );
          })}
        </pre>
      </div>
    );
  };

  // No source loaded — show load UI
  if (!analysis) {
    return (
      <div className="ixql-viewer" style={{ height }}>
        <div className="ixql-viewer__header">
          <div className="ixql-viewer__title">IxQL Pipeline Viewer</div>
        </div>
        <div className="ixql-viewer__load-area">
          <div style={{ fontSize: 24, marginBottom: 8 }}>IxQL Pipeline Viewer</div>
          <div style={{ fontSize: 13, maxWidth: 500, textAlign: 'center', lineHeight: 1.6 }}>
            Paste an IxQL pipeline below or load a sample to visualize it as an interactive graph
            with LOLLI analysis and Amdahl's Law metrics.
          </div>
          <textarea
            className="ixql-viewer__load-textarea"
            value={pasteInput}
            onChange={(e) => setPasteInput(e.target.value)}
            placeholder="Paste IxQL pipeline source here..."
          />
          <div style={{ display: 'flex', gap: 8 }}>
            <button className="ixql-viewer__load-btn" onClick={handleLoadPaste}>
              Visualize
            </button>
            <button
              className="ixql-viewer__load-btn"
              style={{ background: '#21262d', border: '1px solid #30363d' }}
              onClick={() => handleLoadSample(SAMPLE_CONSCIENCE_CYCLE)}
            >
              Conscience Cycle
            </button>
            <button
              className="ixql-viewer__load-btn"
              style={{ background: '#21262d', border: '1px solid #30363d' }}
              onClick={() => handleLoadSample(SAMPLE_ML_FEEDBACK)}
            >
              ML Feedback Loop
            </button>
          </div>
        </div>
      </div>
    );
  }

  const { graph, lolliReport, amdahlReport } = analysis;

  return (
    <div className="ixql-viewer" style={{ height }}>
      {/* Header bar with badges and view toggle */}
      <div className="ixql-viewer__header">
        <div className="ixql-viewer__title">
          IxQL Pipeline Viewer
          <span style={{ fontSize: 11, color: '#8b949e', fontWeight: 400 }}>
            {graph.bindings.length} bindings | {graph.edges.length} edges
          </span>
        </div>

        <div className="ixql-viewer__badges">
          {/* LOLLI badge */}
          <span
            className={`ixql-viewer__badge ${
              lolliReport.lolliScore > 0
                ? 'ixql-viewer__badge--lolli-dirty'
                : 'ixql-viewer__badge--lolli-clean'
            }`}
          >
            {lolliReport.deadBindings} dead / {lolliReport.totalBindings} total ={' '}
            {lolliReport.lolliScore.toFixed(0)}% LOLLI
          </span>

          {/* Amdahl badge */}
          <span className="ixql-viewer__badge ixql-viewer__badge--amdahl">
            Serial: {(amdahlReport.serialFraction * 100).toFixed(0)}% | Max speedup at N=4:{' '}
            {amdahlReport.speedupAtN(4).toFixed(1)}x
          </span>
        </div>

        <div className="ixql-viewer__controls">
          {(['graph', 'source', 'split'] as ViewMode[]).map((mode) => (
            <button
              key={mode}
              className={`ixql-viewer__view-btn ${
                viewMode === mode ? 'ixql-viewer__view-btn--active' : ''
              }`}
              onClick={() => setViewMode(mode)}
            >
              {mode === 'graph' ? 'Graph' : mode === 'source' ? 'Source' : 'Split'}
            </button>
          ))}
          <button
            className="ixql-viewer__view-btn"
            onClick={() => {
              setSource('');
              setSelectedBinding(null);
              setPasteInput('');
            }}
          >
            Load New
          </button>
        </div>
      </div>

      {/* Main body */}
      <div className="ixql-viewer__body">
        {/* Graph pane */}
        {(viewMode === 'graph' || viewMode === 'split') && (
          <div className="ixql-viewer__graph-pane">
            <ReactFlowProvider>
              <IxqlGraphView
                graph={graph}
                highlightedChain={highlightedChain}
                onNodeClick={handleNodeClick}
                onNodeHover={handleNodeHover}
              />
            </ReactFlowProvider>
          </div>
        )}

        {/* Source pane */}
        {(viewMode === 'source' || viewMode === 'split') && renderSourceView()}

        {/* Detail panel */}
        <DetailPanel
          binding={selectedBinding}
          lolliReport={lolliReport}
          amdahlReport={amdahlReport}
          onClose={() => setSelectedBinding(null)}
        />
      </div>
    </div>
  );
};
