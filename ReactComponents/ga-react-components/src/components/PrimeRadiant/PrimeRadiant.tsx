// src/components/PrimeRadiant/PrimeRadiant.tsx
// Main React component — canvas + controls + detail panel + search + overlays

import React, { Component, useCallback, useEffect, useRef, useState } from 'react';
import type { GovernanceGraph, GovernanceNode, PrimeRadiantProps } from './types';
import { NODE_COLORS, HEALTH_COLORS } from './types';
import { loadGovernanceData, buildGraphIndex, searchNodes, getHealthStatus } from './DataLoader';
import type { GraphIndex } from './DataLoader';
import { RadiantEngine } from './RadiantEngine';
import { DetailPanel } from './DetailPanel';
import './styles.css';

// ---------------------------------------------------------------------------
// Error Boundary
// ---------------------------------------------------------------------------
interface ErrorBoundaryState {
  hasError: boolean;
  error: Error | null;
}

class PrimeRadiantErrorBoundary extends Component<
  { children: React.ReactNode },
  ErrorBoundaryState
> {
  constructor(props: { children: React.ReactNode }) {
    super(props);
    this.state = { hasError: false, error: null };
  }

  static getDerivedStateFromError(error: Error): ErrorBoundaryState {
    return { hasError: true, error };
  }

  componentDidCatch(error: Error, info: React.ErrorInfo) {
    console.error('[PrimeRadiant] Error boundary caught:', error, info.componentStack);
  }

  render() {
    if (this.state.hasError) {
      return (
        <div style={{
          width: '100%',
          height: '100%',
          minHeight: 600,
          background: '#000010',
          display: 'flex',
          flexDirection: 'column',
          alignItems: 'center',
          justifyContent: 'center',
          color: '#e6edf3',
          fontFamily: 'monospace',
          gap: 16,
        }}>
          <div style={{ color: '#E06C75', fontSize: 18, fontWeight: 600 }}>
            Prime Radiant — Render Error
          </div>
          <div style={{ color: '#8b949e', fontSize: 13, maxWidth: 500, textAlign: 'center' }}>
            {this.state.error?.message ?? 'Unknown error during Three.js initialization'}
          </div>
          <div style={{ color: '#484f58', fontSize: 11 }}>
            Check browser console for details (F12)
          </div>
          <button
            onClick={() => this.setState({ hasError: false, error: null })}
            style={{
              marginTop: 8,
              padding: '8px 16px',
              background: '#21262d',
              color: '#58A6FF',
              border: '1px solid #30363d',
              borderRadius: 6,
              cursor: 'pointer',
              fontFamily: 'inherit',
            }}
          >
            Retry
          </button>
        </div>
      );
    }
    return this.props.children;
  }
}

// ---------------------------------------------------------------------------
// Legend items
// ---------------------------------------------------------------------------
const LEGEND_ITEMS: Array<{ label: string; color: string }> = [
  { label: 'Constitution', color: NODE_COLORS.constitution },
  { label: 'Policy', color: NODE_COLORS.policy },
  { label: 'Persona', color: NODE_COLORS.persona },
  { label: 'Pipeline', color: NODE_COLORS.pipeline },
  { label: 'Department', color: NODE_COLORS.department },
  { label: 'Schema', color: NODE_COLORS.schema },
  { label: 'Test', color: NODE_COLORS.test },
  { label: 'IxQL', color: NODE_COLORS.ixql },
];

// ---------------------------------------------------------------------------
// Component
// ---------------------------------------------------------------------------
export const PrimeRadiant: React.FC<PrimeRadiantProps> = ({
  data,
  width = '100%',
  height = '100%',
  onNodeSelect,
  showDetailPanel = true,
  showSearchBar = true,
  showTimeSlider = true,
  className = '',
}) => {
  const containerRef = useRef<HTMLDivElement>(null);
  const canvasRef = useRef<HTMLCanvasElement>(null);
  const engineRef = useRef<RadiantEngine | null>(null);

  const [selectedNode, setSelectedNode] = useState<GovernanceNode | null>(null);
  const [hoveredNodeId, setHoveredNodeId] = useState<string | null>(null);
  const [searchQuery, setSearchQuery] = useState('');
  const [searchResults, setSearchResults] = useState<GovernanceNode[]>([]);
  const [graphData, setGraphData] = useState<GovernanceGraph | null>(null);
  const [graphIndex, setGraphIndex] = useState<GraphIndex | null>(null);
  const [timeValue, setTimeValue] = useState(100);
  const [isLoading, setIsLoading] = useState(true);
  const [initError, setInitError] = useState<string | null>(null);

  // ─── Load data and initialize engine ───
  useEffect(() => {
    const canvas = canvasRef.current;
    const container = containerRef.current;
    if (!canvas || !container) {
      console.error('[PrimeRadiant] Canvas or container ref is null');
      return;
    }

    console.log('[PrimeRadiant] Initializing...');
    setIsLoading(true);
    setInitError(null);

    try {
      const graph = loadGovernanceData(data);
      setGraphData(graph);
      setGraphIndex(buildGraphIndex(graph));

      const handleNodeSelect = (node: GovernanceNode | null) => {
        setSelectedNode(node);
        onNodeSelect?.(node);
      };

      const handleHoverChange = (nodeId: string | null) => {
        setHoveredNodeId(nodeId);
      };

      const engine = new RadiantEngine(canvas, container, handleNodeSelect, handleHoverChange);
      engine.init(graph);
      engineRef.current = engine;
      setIsLoading(false);
      console.log('[PrimeRadiant] Initialization complete');

      // Resize observer
      const resizeObserver = new ResizeObserver(() => {
        engine.resize();
      });
      resizeObserver.observe(container);

      return () => {
        resizeObserver.disconnect();
        engine.dispose();
        engineRef.current = null;
      };
    } catch (err) {
      const message = err instanceof Error ? err.message : String(err);
      console.error('[PrimeRadiant] Initialization failed:', err);
      setInitError(message);
      setIsLoading(false);
    }
  }, [data]); // eslint-disable-line react-hooks/exhaustive-deps

  // ─── Search ───
  const handleSearch = useCallback(
    (query: string) => {
      setSearchQuery(query);
      if (graphData) {
        setSearchResults(searchNodes(graphData, query));
      }
    },
    [graphData],
  );

  const handleSearchSelect = useCallback(
    (nodeId: string) => {
      engineRef.current?.focusOnNode(nodeId);
      setSearchQuery('');
      setSearchResults([]);
    },
    [],
  );

  // ─── Detail panel navigation ───
  const handleNavigate = useCallback((nodeId: string) => {
    engineRef.current?.focusOnNode(nodeId);
  }, []);

  const handleCloseDetail = useCallback(() => {
    setSelectedNode(null);
    onNodeSelect?.(null);
  }, [onNodeSelect]);

  // ─── Health status ───
  const healthStatus = graphData
    ? getHealthStatus(graphData.globalHealth.resilienceScore)
    : 'healthy';

  // ─── Tooltip for hovered node ───
  const hoveredNode = hoveredNodeId && graphData
    ? graphData.nodes.find((n) => n.id === hoveredNodeId) ?? null
    : null;

  return (
    <PrimeRadiantErrorBoundary>
    <div
      ref={containerRef}
      className={`prime-radiant ${className}`}
      style={{ width, height }}
    >
      {/* 3D Canvas */}
      <canvas ref={canvasRef} className="prime-radiant__canvas" />

      {/* Loading overlay */}
      {isLoading && (
        <div style={{
          position: 'absolute',
          inset: 0,
          display: 'flex',
          flexDirection: 'column',
          alignItems: 'center',
          justifyContent: 'center',
          background: '#000010',
          zIndex: 50,
          gap: 12,
        }}>
          <div style={{ color: '#FFD700', fontSize: 18, fontWeight: 600 }}>
            Initializing Prime Radiant...
          </div>
          <div style={{ color: '#484f58', fontSize: 12 }}>
            Building governance visualization
          </div>
        </div>
      )}

      {/* Error overlay */}
      {initError && (
        <div style={{
          position: 'absolute',
          inset: 0,
          display: 'flex',
          flexDirection: 'column',
          alignItems: 'center',
          justifyContent: 'center',
          background: '#000010',
          zIndex: 50,
          gap: 12,
        }}>
          <div style={{ color: '#E06C75', fontSize: 18, fontWeight: 600 }}>
            Prime Radiant — Init Error
          </div>
          <div style={{ color: '#8b949e', fontSize: 13, maxWidth: 500, textAlign: 'center' }}>
            {initError}
          </div>
          <div style={{ color: '#484f58', fontSize: 11 }}>
            Check browser console for details (F12)
          </div>
        </div>
      )}

      {/* Search bar */}
      {showSearchBar && (
        <div className="prime-radiant__search">
          <span className="prime-radiant__search-icon">/</span>
          <input
            type="text"
            placeholder="Search governance artifacts..."
            value={searchQuery}
            onChange={(e) => handleSearch(e.target.value)}
            onKeyDown={(e) => {
              if (e.key === 'Escape') {
                setSearchQuery('');
                setSearchResults([]);
              }
              if (e.key === 'Enter' && searchResults.length > 0) {
                handleSearchSelect(searchResults[0].id);
              }
            }}
          />
          {/* Search results dropdown */}
          {searchResults.length > 0 && searchQuery && (
            <div
              style={{
                position: 'absolute',
                top: '100%',
                left: 0,
                right: 0,
                marginTop: 4,
                background: 'rgba(22, 27, 34, 0.95)',
                border: '1px solid #30363d',
                borderRadius: 8,
                overflow: 'hidden',
                maxHeight: 240,
                overflowY: 'auto',
              }}
            >
              {searchResults.slice(0, 8).map((result) => (
                <div
                  key={result.id}
                  onClick={() => handleSearchSelect(result.id)}
                  style={{
                    padding: '8px 12px',
                    cursor: 'pointer',
                    display: 'flex',
                    alignItems: 'center',
                    gap: 8,
                    fontSize: 12,
                    borderBottom: '1px solid #21262d',
                  }}
                  onMouseOver={(e) => {
                    (e.currentTarget as HTMLDivElement).style.background = '#161b22';
                  }}
                  onMouseOut={(e) => {
                    (e.currentTarget as HTMLDivElement).style.background = 'transparent';
                  }}
                >
                  <span
                    style={{
                      width: 8,
                      height: 8,
                      borderRadius: '50%',
                      backgroundColor: result.color,
                      flexShrink: 0,
                    }}
                  />
                  <span style={{ color: '#e6edf3' }}>{result.name}</span>
                  <span style={{ marginLeft: 'auto', color: '#484f58', fontSize: 10 }}>
                    {result.type}
                  </span>
                </div>
              ))}
            </div>
          )}
        </div>
      )}

      {/* Health indicator */}
      <div className="prime-radiant__health">
        <span className={`prime-radiant__health-dot prime-radiant__health-dot--${healthStatus}`} />
        <span>
          Resilience:{' '}
          <span style={{ color: HEALTH_COLORS[healthStatus], fontWeight: 600 }}>
            {graphData ? `${(graphData.globalHealth.resilienceScore * 100).toFixed(0)}%` : '---'}
          </span>
        </span>
        {graphData && (
          <>
            <span style={{ color: '#30363d' }}>|</span>
            <span>
              ERGOL: <span style={{ color: '#FFD700' }}>{graphData.globalHealth.ergolCount}</span>
            </span>
            <span style={{ color: '#30363d' }}>|</span>
            <span>
              LOLLI: <span style={{ color: graphData.globalHealth.lolliCount > 0 ? '#FF4444' : '#8b949e' }}>
                {graphData.globalHealth.lolliCount}
              </span>
            </span>
          </>
        )}
      </div>

      {/* Legend */}
      <div className="prime-radiant__legend">
        {LEGEND_ITEMS.map((item) => (
          <div key={item.label} className="prime-radiant__legend-item">
            <span
              className="prime-radiant__legend-swatch"
              style={{ backgroundColor: item.color }}
            />
            <span className="prime-radiant__legend-label">{item.label}</span>
          </div>
        ))}
      </div>

      {/* Time slider */}
      {showTimeSlider && (
        <div className="prime-radiant__timeline">
          <span className="prime-radiant__timeline-label">Governance Evolution</span>
          <input
            type="range"
            min={0}
            max={100}
            value={timeValue}
            onChange={(e) => setTimeValue(Number(e.target.value))}
          />
          <span className="prime-radiant__timeline-label">
            {timeValue === 100 ? 'Now' : `${timeValue}%`}
          </span>
        </div>
      )}

      {/* Tooltip */}
      {hoveredNode && !selectedNode && (
        <div
          className="prime-radiant__tooltip"
          style={{
            left: '50%',
            top: 16,
            transform: 'translateX(-50%)',
          }}
        >
          <div className="prime-radiant__tooltip-name" style={{ color: hoveredNode.color }}>
            {hoveredNode.name}
          </div>
          <div className="prime-radiant__tooltip-type">{hoveredNode.type}</div>
          <div className="prime-radiant__tooltip-desc">{hoveredNode.description}</div>
        </div>
      )}

      {/* Detail panel */}
      {showDetailPanel && (
        <DetailPanel
          node={selectedNode}
          graphIndex={graphIndex}
          onClose={handleCloseDetail}
          onNavigate={handleNavigate}
        />
      )}
    </div>
    </PrimeRadiantErrorBoundary>
  );
};
