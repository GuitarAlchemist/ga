// src/components/PrimeRadiant/JurisdictionLegend.tsx
// Small HUD badge that explains the Voronoi jurisdiction membranes.
// Governance clusters are wrapped in translucent shells (gold for
// constitutions, silver for departments, etc.) — without this legend,
// users see unexplained colored membranes. Clicking the badge expands
// an info panel describing what a jurisdiction means.

import { useState } from 'react';
import { CLUSTER_COLORS } from './shaders/VoronoiShellTSL';

const CLUSTER_LABELS: Record<string, string> = {
  constitution: 'Constitutional',
  department: 'Department',
  policy: 'Policy',
  persona: 'Persona',
  pipeline: 'Pipeline',
  schema: 'Schema',
  test: 'Test',
};

// Ordered so the most-likely-visible clusters come first in the legend.
const ORDERED_TYPES = ['constitution', 'department', 'policy', 'persona', 'pipeline', 'schema', 'test'];

export function JurisdictionLegend(): JSX.Element {
  const [expanded, setExpanded] = useState(false);

  const emitHover = (on: boolean) => {
    window.dispatchEvent(new CustomEvent('prime-radiant:jurisdictions-hover', { detail: { on } }));
  };

  return (
    <div
      className="prime-radiant__jurisdiction-legend"
      onClick={(e) => { e.stopPropagation(); setExpanded((v) => !v); }}
      onMouseEnter={() => emitHover(true)}
      onMouseLeave={() => emitHover(false)}
      title="Hover to reveal jurisdiction membranes · click to learn more"
    >
      <div className="prime-radiant__jurisdiction-legend-header">
        <span className="prime-radiant__jurisdiction-legend-title">JURISDICTIONS</span>
        <span className="prime-radiant__jurisdiction-legend-caret">{expanded ? '▾' : '▸'}</span>
      </div>

      <div className="prime-radiant__jurisdiction-legend-swatches">
        {ORDERED_TYPES.map((type) => {
          const color = CLUSTER_COLORS[type];
          if (!color) return null;
          const hex = '#' + color.getHexString();
          return (
            <div key={type} className="prime-radiant__jurisdiction-legend-item" title={CLUSTER_LABELS[type]}>
              <span
                className="prime-radiant__jurisdiction-legend-swatch"
                style={{ backgroundColor: hex, boxShadow: `0 0 6px ${hex}88` }}
              />
              {expanded && <span className="prime-radiant__jurisdiction-legend-label">{CLUSTER_LABELS[type]}</span>}
            </div>
          );
        })}
      </div>

      {expanded && (
        <div
          className="prime-radiant__jurisdiction-legend-body"
          onClick={(e) => e.stopPropagation()}
        >
          <div className="prime-radiant__jurisdiction-legend-body-title">
            What is a jurisdiction?
          </div>
          <div className="prime-radiant__jurisdiction-legend-body-desc">
            Each translucent membrane in the graph is a governance
            <em> jurisdiction</em> — the authority boundary of a cluster
            of related artifacts. Constitutions govern articles, departments
            govern personas, policies govern tests. The membrane makes
            visible where one domain&apos;s authority ends and another&apos;s
            begins.
          </div>
          <div className="prime-radiant__jurisdiction-legend-body-desc">
            Where two membranes meet, authority is shared or contested —
            this is <em>jurisdictional pressure</em>. Hover a membrane for
            its cluster type and member count.
          </div>
        </div>
      )}
    </div>
  );
}
