// src/components/PrimeRadiant/BeliefHeatmap.tsx
// Belief confidence heatmap — domains x time periods, tetravalent color-coded

import React, { useState } from 'react';

// ---------------------------------------------------------------------------
// Types
// ---------------------------------------------------------------------------
type TetravalentValue = 'T' | 'F' | 'U' | 'C';

interface BeliefCell {
  confidence: number;  // 0.0 - 1.0
  value: TetravalentValue;
}

interface BeliefDomain {
  name: string;
  shortName: string;
  color: string;
  cells: BeliefCell[];  // one per time period (last 7 days, index 0 = oldest)
}

// ---------------------------------------------------------------------------
// Mock data — realistic belief decay patterns
// ---------------------------------------------------------------------------
const TIME_LABELS = ['6d', '5d', '4d', '3d', '2d', '1d', 'now'];

const mockDomains: BeliefDomain[] = [
  {
    name: 'Governance',
    shortName: 'GOV',
    color: '#FFD700',
    cells: [
      { confidence: 0.85, value: 'T' },
      { confidence: 0.87, value: 'T' },
      { confidence: 0.82, value: 'T' },
      { confidence: 0.78, value: 'T' },
      { confidence: 0.64, value: 'U' },  // red team revealed gaps
      { confidence: 0.75, value: 'T' },  // recovery after fixes
      { confidence: 0.82, value: 'T' },
    ],
  },
  {
    name: 'Music Theory',
    shortName: 'MUS',
    color: '#FFB300',
    cells: [
      { confidence: 0.72, value: 'T' },
      { confidence: 0.70, value: 'T' },
      { confidence: 0.68, value: 'U' },
      { confidence: 0.55, value: 'U' },  // belief decay — stale data
      { confidence: 0.48, value: 'U' },
      { confidence: 0.45, value: 'U' },  // investigation triggered
      { confidence: 0.60, value: 'U' },  // partial recovery
    ],
  },
  {
    name: 'Infrastructure',
    shortName: 'INF',
    color: '#4FC3F7',
    cells: [
      { confidence: 0.92, value: 'T' },
      { confidence: 0.90, value: 'T' },
      { confidence: 0.91, value: 'T' },
      { confidence: 0.88, value: 'T' },
      { confidence: 0.90, value: 'T' },
      { confidence: 0.85, value: 'T' },
      { confidence: 0.89, value: 'T' },
    ],
  },
  {
    name: 'Research',
    shortName: 'RES',
    color: '#c4b5fd',
    cells: [
      { confidence: 0.65, value: 'U' },
      { confidence: 0.60, value: 'U' },
      { confidence: 0.55, value: 'U' },
      { confidence: 0.50, value: 'U' },
      { confidence: 0.35, value: 'C' },  // contradictory findings
      { confidence: 0.40, value: 'C' },  // still conflicting
      { confidence: 0.52, value: 'U' },  // escalation helped
    ],
  },
  {
    name: 'Alignment',
    shortName: 'ALN',
    color: '#33CC66',
    cells: [
      { confidence: 0.78, value: 'T' },
      { confidence: 0.80, value: 'T' },
      { confidence: 0.82, value: 'T' },
      { confidence: 0.79, value: 'T' },
      { confidence: 0.76, value: 'T' },
      { confidence: 0.81, value: 'T' },
      { confidence: 0.84, value: 'T' },
    ],
  },
  {
    name: 'Cross-Repo',
    shortName: 'XRP',
    color: '#ff85c0',
    cells: [
      { confidence: 0.70, value: 'T' },
      { confidence: 0.65, value: 'U' },
      { confidence: 0.58, value: 'U' },
      { confidence: 0.30, value: 'F' },  // schema incompatibility detected
      { confidence: 0.35, value: 'F' },
      { confidence: 0.55, value: 'U' },  // fix in progress
      { confidence: 0.72, value: 'T' },  // fixed
    ],
  },
];

// ---------------------------------------------------------------------------
// Color mapping — tetravalent + confidence
// ---------------------------------------------------------------------------
function cellColor(cell: BeliefCell): string {
  // Tetravalent overrides for F and C
  if (cell.value === 'C') return '#FF44FF';  // magenta — contradictory
  if (cell.value === 'F') return '#FF4444';  // red — false

  // Gradient for T and U based on confidence
  if (cell.confidence >= 0.8) return '#33CC66';   // green — high confidence T
  if (cell.confidence >= 0.7) return '#66DD88';   // light green
  if (cell.confidence >= 0.6) return '#AACC44';   // yellow-green
  if (cell.confidence >= 0.5) return '#DDBB33';   // yellow
  if (cell.confidence >= 0.4) return '#FFB300';   // amber
  if (cell.confidence >= 0.3) return '#FF8833';   // orange
  return '#FF4444';                                // red — very low
}

function cellOpacity(cell: BeliefCell): number {
  // Active/resolved states affect opacity
  return 0.5 + cell.confidence * 0.5;
}

// ---------------------------------------------------------------------------
// Component
// ---------------------------------------------------------------------------
export const BeliefHeatmap: React.FC = () => {
  const [collapsed, setCollapsed] = useState(false);
  const [hoveredCell, setHoveredCell] = useState<{ domain: string; day: number } | null>(null);

  const hoveredData = hoveredCell
    ? mockDomains.find((d) => d.name === hoveredCell.domain)?.cells[hoveredCell.day]
    : null;

  return (
    <div className="prime-radiant__belief-heatmap">
      {/* Header */}
      <div
        className="prime-radiant__belief-heatmap-header"
        onClick={() => setCollapsed(!collapsed)}
      >
        <span className="prime-radiant__belief-heatmap-title">
          Belief Confidence
          <span className="prime-radiant__belief-heatmap-badge">
            {mockDomains.length} domains
          </span>
        </span>
        <span className="prime-radiant__belief-heatmap-toggle">
          {collapsed ? '\u25B6' : '\u25BC'}
        </span>
      </div>

      {!collapsed && (
        <div className="prime-radiant__belief-heatmap-body">
          {/* Time axis header */}
          <div className="prime-radiant__belief-heatmap-row prime-radiant__belief-heatmap-row--header">
            <span className="prime-radiant__belief-heatmap-label" />
            {TIME_LABELS.map((t, i) => (
              <span key={i} className="prime-radiant__belief-heatmap-time">
                {t}
              </span>
            ))}
          </div>

          {/* Domain rows */}
          {mockDomains.map((domain) => (
            <div key={domain.name} className="prime-radiant__belief-heatmap-row">
              <span
                className="prime-radiant__belief-heatmap-label"
                style={{ color: domain.color }}
                title={domain.name}
              >
                {domain.shortName}
              </span>
              {domain.cells.map((cell, i) => (
                <span
                  key={i}
                  className="prime-radiant__belief-heatmap-cell"
                  style={{
                    backgroundColor: cellColor(cell),
                    opacity: cellOpacity(cell),
                  }}
                  onMouseEnter={() => setHoveredCell({ domain: domain.name, day: i })}
                  onMouseLeave={() => setHoveredCell(null)}
                  title={`${domain.name} ${TIME_LABELS[i]}: ${cell.value} (${(cell.confidence * 100).toFixed(0)}%)`}
                >
                  {cell.value}
                </span>
              ))}
            </div>
          ))}

          {/* Tooltip */}
          {hoveredCell && hoveredData && (
            <div className="prime-radiant__belief-heatmap-tooltip">
              <span style={{ color: mockDomains.find((d) => d.name === hoveredCell.domain)?.color }}>
                {hoveredCell.domain}
              </span>
              {' '}
              <span style={{ color: '#8b949e' }}>{TIME_LABELS[hoveredCell.day]}</span>
              {' \u2014 '}
              <span style={{ color: cellColor(hoveredData), fontWeight: 600 }}>
                {hoveredData.value}
              </span>
              {' '}
              <span style={{ color: '#6b7280' }}>
                ({(hoveredData.confidence * 100).toFixed(0)}%)
              </span>
            </div>
          )}

          {/* Legend */}
          <div className="prime-radiant__belief-heatmap-legend">
            <span style={{ color: '#33CC66' }}>T</span>
            <span style={{ color: '#DDBB33' }}>U</span>
            <span style={{ color: '#FF4444' }}>F</span>
            <span style={{ color: '#FF44FF' }}>C</span>
            <span className="prime-radiant__belief-heatmap-legend-label">
              high
            </span>
            <span className="prime-radiant__belief-heatmap-gradient" />
            <span className="prime-radiant__belief-heatmap-legend-label">
              low
            </span>
          </div>
        </div>
      )}
    </div>
  );
};
