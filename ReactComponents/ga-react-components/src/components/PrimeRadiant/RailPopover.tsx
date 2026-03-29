// src/components/PrimeRadiant/RailPopover.tsx
// Rich hover popover card for icon rail buttons — shows quick-glance stats
// before the user commits to opening the full panel.

import React, { useEffect, useState } from 'react';

/** Metrics displayed inside the popover card. */
export interface RailPopoverMetrics {
  /** Primary stat line, e.g. "3 commits today" */
  primary: string;
  /** Secondary stat line, e.g. "2 PRs open" */
  secondary?: string;
  /** Optional tertiary hint */
  hint?: string;
}

export interface RailPopoverProps {
  /** Panel type key (matches PanelId) */
  panelType: string;
  /** Human-readable label */
  label: string;
  /** Vertical offset (px) of the trigger button from the rail top */
  anchorTop: number;
  /** Whether the popover is visible */
  visible: boolean;
}

// ---------------------------------------------------------------------------
// Default metrics per panel type (placeholder / computed data)
// ---------------------------------------------------------------------------

const DEFAULT_METRICS: Record<string, RailPopoverMetrics> = {
  activity:   { primary: '7 commits today', secondary: '2 PRs open' },
  backlog:    { primary: '14 items', secondary: '3 high priority' },
  agent:      { primary: '4 sessions', secondary: '6 subagents' },
  seldon:     { primary: 'R: 94%', secondary: 'Markov: stable' },
  llm:        { primary: '3 active', secondary: '5 configured' },
  detail:     { primary: 'Node inspector', hint: 'Click to open' },
  algedonic:  { primary: '2 pain', secondary: '5 pleasure' },
  cicd:       { primary: '11 passing', secondary: '1 failing' },
  university: { primary: '3 courses', hint: 'Click to open' },
  claude:     { primary: '1 session', secondary: 'Idle' },
  notebook:   { primary: 'Live notebook', hint: 'Click to open' },
  library:    { primary: '28 entries', secondary: '4 recent' },
  godot:      { primary: '1 scene', hint: 'Click to open' },
  gis:        { primary: '3 layers', secondary: '12 pins' },
};

function metricsForPanel(panelType: string): RailPopoverMetrics {
  return DEFAULT_METRICS[panelType] ?? { primary: panelType, hint: 'Click to open' };
}

// ---------------------------------------------------------------------------
// Component
// ---------------------------------------------------------------------------

export const RailPopover: React.FC<RailPopoverProps> = ({
  panelType,
  label,
  anchorTop,
  visible,
}) => {
  // 300ms hover delay to avoid flicker
  const [show, setShow] = useState(false);

  useEffect(() => {
    if (visible) {
      const timer = setTimeout(() => setShow(true), 300);
      return () => clearTimeout(timer);
    }
    setShow(false);
    return undefined;
  }, [visible]);

  if (!show) return null;

  const metrics = metricsForPanel(panelType);

  return (
    <div
      className="rail-popover"
      style={{ top: anchorTop }}
      role="tooltip"
    >
      <div className="rail-popover__title">{label}</div>
      <div className="rail-popover__metric">{metrics.primary}</div>
      {metrics.secondary && (
        <div className="rail-popover__metric rail-popover__metric--secondary">
          {metrics.secondary}
        </div>
      )}
      {metrics.hint && (
        <div className="rail-popover__hint">{metrics.hint}</div>
      )}
    </div>
  );
};
