// src/components/PrimeRadiant/KeyboardLegend.tsx
// HUD badge listing keyboard shortcuts for runtime layer + post-FX toggles.
// Collapsed: shows "⌨ KEYS". Click to expand and see each binding.
// Persistent preferences for layer visibility come from localStorage so
// flipping a toggle here will match state restored on next reload.

import { useState } from 'react';

interface Binding {
  key: string;
  label: string;
  category: 'layer' | 'fx';
}

const BINDINGS: Binding[] = [
  { key: 'm', label: 'Milky Way', category: 'layer' },
  { key: 'Shift+S', label: 'Skybox', category: 'layer' },
  { key: 'Shift+J', label: 'Jurisdictions', category: 'layer' },
  { key: 'Shift+B', label: 'Solar system', category: 'layer' },
  { key: 'a', label: 'Moebius (audit)', category: 'fx' },
  { key: 'c', label: 'Caustics', category: 'fx' },
  { key: 'd', label: 'Dispersion', category: 'fx' },
];

export function KeyboardLegend(): JSX.Element {
  const [expanded, setExpanded] = useState(false);

  return (
    <div
      className="prime-radiant__keyboard-legend"
      onClick={(e) => { e.stopPropagation(); setExpanded((v) => !v); }}
      title="Click to see keyboard shortcuts"
    >
      <div className="prime-radiant__keyboard-legend-header">
        <span className="prime-radiant__keyboard-legend-icon">⌨</span>
        <span className="prime-radiant__keyboard-legend-title">KEYS</span>
        <span className="prime-radiant__keyboard-legend-caret">{expanded ? '▾' : '▸'}</span>
      </div>

      {expanded && (
        <div
          className="prime-radiant__keyboard-legend-body"
          onClick={(e) => e.stopPropagation()}
        >
          <div className="prime-radiant__keyboard-legend-section">
            <div className="prime-radiant__keyboard-legend-section-title">Layers</div>
            {BINDINGS.filter((b) => b.category === 'layer').map((b) => (
              <div key={b.key} className="prime-radiant__keyboard-legend-row">
                <kbd className="prime-radiant__keyboard-legend-kbd">{b.key}</kbd>
                <span>{b.label}</span>
              </div>
            ))}
          </div>
          <div className="prime-radiant__keyboard-legend-section">
            <div className="prime-radiant__keyboard-legend-section-title">Post-FX</div>
            {BINDINGS.filter((b) => b.category === 'fx').map((b) => (
              <div key={b.key} className="prime-radiant__keyboard-legend-row">
                <kbd className="prime-radiant__keyboard-legend-kbd">{b.key}</kbd>
                <span>{b.label}</span>
              </div>
            ))}
          </div>
        </div>
      )}
    </div>
  );
}
