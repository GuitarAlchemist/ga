// src/components/PrimeRadiant/DemerzelCriticOverlay.tsx
// Floating overlay showing Demerzel's self-improvement process in real-time
// Displays: analysis state, quality score, issues, IXQL commands, beliefs, signals

import React, { useState, useEffect, useCallback } from 'react';
import type { VisualCriticResult } from './VisualCriticLoop';

export interface CriticState {
  phase: 'idle' | 'capturing' | 'analyzing' | 'executing' | 'complete';
  result: VisualCriticResult | null;
  history: VisualCriticResult[];
  lastAnalysis: Date | null;
}

interface DemerzelCriticOverlayProps {
  state: CriticState;
  visible?: boolean;
  onToggle?: () => void;
}

const PHASE_LABELS: Record<CriticState['phase'], string> = {
  idle: 'Observing...',
  capturing: 'Capturing viewport...',
  analyzing: 'Analyzing visual quality...',
  executing: 'Applying IXQL corrections...',
  complete: 'Analysis complete',
};

const PHASE_ICONS: Record<CriticState['phase'], string> = {
  idle: '◉',
  capturing: '◎',
  analyzing: '◈',
  executing: '◆',
  complete: '◇',
};

function QualityBar({ quality }: { quality: number }) {
  const filled = Math.max(0, Math.min(10, quality));
  const color = quality >= 7 ? '#33CC66' : quality >= 4 ? '#FFB300' : '#FF4444';
  return (
    <div className="demerzel-critic__quality-bar">
      <div className="demerzel-critic__quality-fill" style={{ width: `${filled * 10}%`, background: color }} />
      <span className="demerzel-critic__quality-label">{quality}/10</span>
    </div>
  );
}

function SignalBadge({ type, severity }: { type: string; severity: string }) {
  const colors: Record<string, string> = {
    'pain-critical': '#FF0000',
    'pain-warning': '#FF4444',
    'pain-info': '#FF8800',
    'pleasure-info': '#33CC66',
    'pleasure-warning': '#88CC33',
  };
  const color = colors[`${type}-${severity}`] || '#8b949e';
  return (
    <span className="demerzel-critic__signal-badge" style={{ color, borderColor: color }}>
      {type === 'pain' ? '▼' : '▲'} {type}/{severity}
    </span>
  );
}

export const DemerzelCriticOverlay: React.FC<DemerzelCriticOverlayProps> = ({
  state,
  visible = true,
  onToggle,
}) => {
  const [expanded, setExpanded] = useState(false);
  const { phase, result, history, lastAnalysis } = state;

  if (!visible) return null;

  const isActive = phase !== 'idle' && phase !== 'complete';
  const trend = history.length >= 2
    ? history[history.length - 1].quality - history[history.length - 2].quality
    : 0;

  return (
    <div className={`demerzel-critic ${expanded ? 'demerzel-critic--expanded' : ''}`}>
      {/* Compact header — always visible */}
      <div className="demerzel-critic__header" onClick={() => setExpanded(!expanded)}>
        <span className={`demerzel-critic__pulse ${isActive ? 'demerzel-critic__pulse--active' : ''}`}>
          {PHASE_ICONS[phase]}
        </span>
        <span className="demerzel-critic__title">Demerzel</span>
        {result && (
          <>
            <QualityBar quality={result.quality} />
            {trend !== 0 && (
              <span className="demerzel-critic__trend" style={{ color: trend > 0 ? '#33CC66' : '#FF4444' }}>
                {trend > 0 ? '↑' : '↓'}{Math.abs(trend)}
              </span>
            )}
          </>
        )}
        <span className="demerzel-critic__phase">{PHASE_LABELS[phase]}</span>
      </div>

      {/* Expanded detail panel */}
      {expanded && (
        <div className="demerzel-critic__body">
          {/* Methodology */}
          <div className="demerzel-critic__section">
            <div className="demerzel-critic__section-title">Methodology</div>
            <div className="demerzel-critic__methodology">
              <div className={`demerzel-critic__step ${phase === 'capturing' ? 'demerzel-critic__step--active' : phase === 'analyzing' || phase === 'executing' || phase === 'complete' ? 'demerzel-critic__step--done' : ''}`}>
                1. Capture canvas screenshot
              </div>
              <div className={`demerzel-critic__step ${phase === 'analyzing' ? 'demerzel-critic__step--active' : phase === 'executing' || phase === 'complete' ? 'demerzel-critic__step--done' : ''}`}>
                2. Claude vision analysis (quality 1-10)
              </div>
              <div className={`demerzel-critic__step ${phase === 'executing' ? 'demerzel-critic__step--active' : phase === 'complete' ? 'demerzel-critic__step--done' : ''}`}>
                3. Execute IXQL corrections
              </div>
              <div className={`demerzel-critic__step ${phase === 'complete' ? 'demerzel-critic__step--done' : ''}`}>
                4. Emit algedonic signal + update beliefs
              </div>
            </div>
          </div>

          {/* Current analysis */}
          {result && (
            <>
              {/* Signal */}
              <div className="demerzel-critic__section">
                <div className="demerzel-critic__section-title">Algedonic Signal</div>
                <SignalBadge type={result.signal_type ?? 'pleasure'} severity={result.signal_severity ?? 'info'} />
                <span className="demerzel-critic__signal-desc">{result.signal_description}</span>
              </div>

              {/* Issues */}
              {result.issues && result.issues.length > 0 && (
                <div className="demerzel-critic__section">
                  <div className="demerzel-critic__section-title">
                    Issues Found ({result.issues.length})
                  </div>
                  <ul className="demerzel-critic__issues">
                    {result.issues.map((issue, i) => (
                      <li key={i}>{issue}</li>
                    ))}
                  </ul>
                </div>
              )}

              {/* IXQL commands */}
              {result.ixql_commands && result.ixql_commands.length > 0 && (
                <div className="demerzel-critic__section">
                  <div className="demerzel-critic__section-title">
                    IXQL Corrections ({result.ixql_commands.length})
                  </div>
                  <div className="demerzel-critic__ixql">
                    {result.ixql_commands.map((cmd, i) => (
                      <code key={i} className="demerzel-critic__ixql-cmd">{cmd}</code>
                    ))}
                  </div>
                </div>
              )}

              {/* Suggestions */}
              {result.suggestions && result.suggestions.length > 0 && (
                <div className="demerzel-critic__section">
                  <div className="demerzel-critic__section-title">Demerzel's Thoughts</div>
                  <ul className="demerzel-critic__thoughts">
                    {result.suggestions.map((s, i) => (
                      <li key={i}>{s}</li>
                    ))}
                  </ul>
                </div>
              )}
            </>
          )}

          {/* History trend */}
          {history.length > 1 && (
            <div className="demerzel-critic__section">
              <div className="demerzel-critic__section-title">Quality Trend</div>
              <div className="demerzel-critic__trend-chart">
                {history.slice(-10).map((h, i) => (
                  <div
                    key={i}
                    className="demerzel-critic__trend-bar"
                    style={{
                      height: `${h.quality * 10}%`,
                      background: h.quality >= 7 ? '#33CC66' : h.quality >= 4 ? '#FFB300' : '#FF4444',
                    }}
                    title={`${h.quality}/10`}
                  />
                ))}
              </div>
            </div>
          )}

          {/* Timing */}
          {lastAnalysis && (
            <div className="demerzel-critic__footer">
              Last analysis: {lastAnalysis.toLocaleTimeString()} · Cycle every 90s
            </div>
          )}
        </div>
      )}
    </div>
  );
};
