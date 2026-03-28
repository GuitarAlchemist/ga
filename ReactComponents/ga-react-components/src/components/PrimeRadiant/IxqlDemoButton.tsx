// src/components/PrimeRadiant/IxqlDemoButton.tsx
// IXQL Demo Tour — guided walkthrough of the IXQL grammar capabilities

import React, { useState, useCallback } from 'react';
import { parseIxqlCommand, type IxqlParseResult } from './IxqlControlParser';

// ---------------------------------------------------------------------------
// Demo steps — each showcases a different IXQL grammar variant
// ---------------------------------------------------------------------------

interface DemoStep {
  variant: string;       // which grammar variant this demonstrates
  label: string;         // short action label
  description: string;   // what this does in plain English
  cmd: string;           // the IXQL command
}

const DEMOS: DemoStep[] = [
  {
    variant: 'SELECT nodes',
    label: 'Highlight stale nodes',
    description: 'Finds governance nodes with high staleness and makes them glow red with a pulse effect.',
    cmd: 'SELECT nodes WHERE health.staleness > 0.5 SET glow = true, color = #FF4444, pulse = true',
  },
  {
    variant: 'SELECT edges',
    label: 'Light up ERGOL edges',
    description: 'Turns all governance operational links gold and widens them to show the active binding network.',
    cmd: 'SELECT edges SET color = #FFD700, width = 3',
  },
  {
    variant: 'CREATE PANEL',
    label: 'Create beliefs panel',
    description: 'Spawns a new side panel from live governance data — no React code needed, just one IXQL command.',
    cmd: 'CREATE PANEL demo-beliefs FROM /api/governance/file-content?path=governance/state/beliefs/core-beliefs.belief.json LAYOUT list-detail SHOW proposition, truth_value, confidence',
  },
  {
    variant: 'BIND HEALTH',
    label: 'Bind health rules',
    description: 'Attaches declarative health monitoring to the beliefs panel — status dot turns warn/error based on staleness.',
    cmd: 'BIND PANEL demo-beliefs HEALTH FROM /api/governance/file-content?path=governance/state/beliefs/core-beliefs.belief.json WHEN confidence < 0.5 SET error WHEN confidence < 0.7 SET warn ELSE SET ok',
  },
  {
    variant: 'ON...THEN',
    label: 'Reactive trigger',
    description: 'Sets up a push trigger: when belief health changes, automatically highlight belief nodes in gold.',
    cmd: 'ON health.demo-beliefs CHANGED THEN SELECT nodes WHERE type = belief SET glow = true, color = #FFD700',
  },
  {
    variant: 'RESET',
    label: 'Clean slate',
    description: 'Clears all visual overrides on nodes and edges, returning the graph to its default state.',
    cmd: 'RESET',
  },
];

// ---------------------------------------------------------------------------
// Icons
// ---------------------------------------------------------------------------

const LightningIcon: React.FC = () => (
  <svg width="18" height="18" viewBox="0 0 24 24" fill="currentColor" stroke="none">
    <path d="M13 2L3 14h9l-1 10 10-12h-9l1-10z" />
  </svg>
);

const CheckIcon: React.FC = () => (
  <svg width="10" height="10" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="3" strokeLinecap="round" strokeLinejoin="round">
    <polyline points="20 6 9 17 4 12" />
  </svg>
);

// ---------------------------------------------------------------------------
// Component
// ---------------------------------------------------------------------------

interface IxqlDemoButtonProps {
  onCommand: (result: IxqlParseResult) => void;
}

export const IxqlDemoButton: React.FC<IxqlDemoButtonProps> = ({ onCommand }) => {
  const [step, setStep] = useState(0);
  const [lastResult, setLastResult] = useState<'idle' | 'ok' | 'error'>('idle');
  const [expanded, setExpanded] = useState(false);

  const handleClick = useCallback(() => {
    const demo = DEMOS[step];
    const result = parseIxqlCommand(demo.cmd);
    onCommand(result);
    setLastResult(result.ok ? 'ok' : 'error');
    setStep((prev) => (prev + 1) % DEMOS.length);
    // Auto-collapse after reset (full cycle)
    if (step === DEMOS.length - 1) setExpanded(false);
  }, [step, onCommand]);

  const current = DEMOS[step];
  const progress = ((step) / DEMOS.length) * 100;

  return (
    <div className={`prime-radiant__demo-wrap ${expanded ? 'prime-radiant__demo-wrap--expanded' : ''}`}>
      {/* Expanded info card */}
      {expanded && (
        <div className="prime-radiant__demo-card">
          <div className="prime-radiant__demo-card-header">
            IXQL DEMO TOUR
            <button
              className="prime-radiant__demo-card-close"
              onClick={() => setExpanded(false)}
              aria-label="Collapse"
            >x</button>
          </div>

          {/* Progress bar */}
          <div className="prime-radiant__demo-progress">
            <div
              className="prime-radiant__demo-progress-fill"
              style={{ width: `${progress}%` }}
            />
          </div>

          {/* Step list */}
          <div className="prime-radiant__demo-steps">
            {DEMOS.map((d, i) => (
              <div
                key={i}
                className={`prime-radiant__demo-step ${i === step ? 'prime-radiant__demo-step--current' : ''} ${i < step ? 'prime-radiant__demo-step--done' : ''}`}
              >
                <span className="prime-radiant__demo-step-num">
                  {i < step ? <CheckIcon /> : i + 1}
                </span>
                <span className="prime-radiant__demo-step-label">{d.label}</span>
                <span className="prime-radiant__demo-step-variant">{d.variant}</span>
              </div>
            ))}
          </div>

          {/* Current step detail */}
          <div className="prime-radiant__demo-detail">
            <div className="prime-radiant__demo-detail-title">
              Next: {current.label}
            </div>
            <div className="prime-radiant__demo-detail-desc">
              {current.description}
            </div>
            <code className="prime-radiant__demo-detail-cmd">
              {current.cmd.length > 60 ? current.cmd.slice(0, 57) + '...' : current.cmd}
            </code>
          </div>

          {/* Run button */}
          <button
            className="prime-radiant__demo-run"
            onClick={handleClick}
          >
            <LightningIcon /> Run Step {step + 1}/{DEMOS.length}
          </button>

          {lastResult !== 'idle' && (
            <div className={`prime-radiant__demo-result ${lastResult === 'ok' ? 'prime-radiant__demo-result--ok' : 'prime-radiant__demo-result--error'}`}>
              {lastResult === 'ok' ? 'Executed successfully' : 'Parse error'}
            </div>
          )}
        </div>
      )}

      {/* Collapsed: just the button */}
      <button
        className={`prime-radiant__demo-btn ${step === 0 && lastResult === 'idle' ? 'prime-radiant__demo-btn--pulse' : ''}`}
        onClick={() => expanded ? handleClick() : setExpanded(true)}
        aria-label="IXQL Demo Tour"
        title="IXQL Demo Tour — click to explore"
      >
        <LightningIcon />
      </button>
      {!expanded && (
        <span className="prime-radiant__demo-label">
          IXQL Demo
        </span>
      )}
    </div>
  );
};
