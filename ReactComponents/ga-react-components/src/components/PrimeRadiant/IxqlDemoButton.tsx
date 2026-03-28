// src/components/PrimeRadiant/IxqlDemoButton.tsx
// Yellow lightning demo button — cycles through IXQL showcase sequences

import React, { useState } from 'react';
import { parseIxqlCommand, type IxqlParseResult } from './IxqlControlParser';

const DEMOS = [
  { label: 'Highlight stale nodes', cmd: 'SELECT nodes WHERE health.staleness > 0.5 SET glow = true, color = #FF4444, pulse = true' },
  { label: 'Light up ERGOL edges', cmd: 'SELECT edges SET color = #FFD700, width = 3' },
  { label: 'Create beliefs panel', cmd: 'CREATE PANEL demo-beliefs FROM /api/governance/file-content?path=governance/state/beliefs LAYOUT list-detail SHOW name, confidence, staleness' },
  { label: 'Bind health rules', cmd: 'BIND PANEL demo-beliefs HEALTH FROM /api/governance/file-content?path=governance/state/beliefs WHEN staleness > 0.7 SET warn WHEN staleness > 0.9 SET error ELSE SET ok' },
  { label: 'Reactive trigger', cmd: 'ON health.demo-beliefs CHANGED THEN SELECT nodes WHERE type = belief SET glow = true, color = #FFD700' },
  { label: 'Reset all', cmd: 'RESET' },
];

const LightningIcon: React.FC = () => (
  <svg width="18" height="18" viewBox="0 0 24 24" fill="currentColor" stroke="none">
    <path d="M13 2L3 14h9l-1 10 10-12h-9l1-10z" />
  </svg>
);

interface IxqlDemoButtonProps {
  onCommand: (result: IxqlParseResult) => void;
}

export const IxqlDemoButton: React.FC<IxqlDemoButtonProps> = ({ onCommand }) => {
  const [step, setStep] = useState(0);

  const handleClick = () => {
    const demo = DEMOS[step];
    const result = parseIxqlCommand(demo.cmd);
    onCommand(result);
    setStep((prev) => (prev + 1) % DEMOS.length);
  };

  return (
    <div className="prime-radiant__demo-wrap">
      <button
        className={`prime-radiant__demo-btn ${step === 0 ? 'prime-radiant__demo-btn--pulse' : ''}`}
        onClick={handleClick}
        aria-label="Run IXQL demo"
        title={`Demo ${step + 1}/5: ${DEMOS[step].label}`}
      >
        <LightningIcon />
      </button>
      <span className="prime-radiant__demo-label">
        {step + 1}/5: {DEMOS[step].label}
      </span>
    </div>
  );
};
