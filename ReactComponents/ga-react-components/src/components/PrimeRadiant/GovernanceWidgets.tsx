// src/components/PrimeRadiant/GovernanceWidgets.tsx
// Small, self-contained embeddable widgets for governance data.
// Each uses inline styles matching the Prime Radiant dark theme.

import React, { useState, useMemo } from 'react';

// ---------------------------------------------------------------------------
// Shared constants
// ---------------------------------------------------------------------------
const COLORS = {
  bg: '#0d1117',
  bgLight: '#161b22',
  border: '#30363d',
  text: '#c9d1d9',
  textMuted: '#8b949e',
  gold: '#FFD700',
  green: '#33CC66',
  red: '#FF4444',
  amber: '#FFB300',
  purple: '#c084fc',
} as const;

const FONT = "'JetBrains Mono', monospace";

const STATUS_COLORS: Record<string, string> = {
  T: COLORS.green,
  F: COLORS.red,
  U: COLORS.amber,
  C: COLORS.purple,
};

const STATUS_LABELS: Record<string, string> = {
  T: 'True',
  F: 'False',
  U: 'Unknown',
  C: 'Contradictory',
};

// ---------------------------------------------------------------------------
// 1. BeliefWidget
// ---------------------------------------------------------------------------
export interface BeliefWidgetProps {
  name: string;
  status: 'T' | 'F' | 'U' | 'C';
  confidence: number;
  evidence?: string;
  onClick?: () => void;
}

export const BeliefWidget: React.FC<BeliefWidgetProps> = ({
  name,
  status,
  confidence,
  evidence,
  onClick,
}) => {
  const color = STATUS_COLORS[status];
  const clampedConf = Math.max(0, Math.min(1, confidence));

  return (
    <div
      onClick={onClick}
      title={evidence ?? `${name}: ${STATUS_LABELS[status]} (${(clampedConf * 100).toFixed(0)}%)`}
      style={{
        display: 'inline-flex',
        alignItems: 'center',
        gap: 8,
        background: COLORS.bg,
        border: `1px solid ${COLORS.border}`,
        borderRadius: 6,
        padding: '8px 12px',
        fontFamily: FONT,
        fontSize: 12,
        color: COLORS.text,
        cursor: onClick ? 'pointer' : 'default',
        minWidth: 180,
        maxWidth: 240,
        height: 52,
        boxSizing: 'border-box',
        transition: 'border-color 0.2s',
      }}
    >
      {/* Status badge */}
      <span
        style={{
          display: 'inline-flex',
          alignItems: 'center',
          justifyContent: 'center',
          width: 26,
          height: 26,
          borderRadius: 4,
          background: `${color}22`,
          color,
          fontWeight: 700,
          fontSize: 13,
          flexShrink: 0,
        }}
      >
        {status}
      </span>

      {/* Name + confidence bar */}
      <div style={{ flex: 1, minWidth: 0 }}>
        <div
          style={{
            overflow: 'hidden',
            textOverflow: 'ellipsis',
            whiteSpace: 'nowrap',
            fontSize: 11,
            marginBottom: 4,
          }}
        >
          {name}
        </div>
        <div
          style={{
            height: 4,
            borderRadius: 2,
            background: COLORS.border,
            overflow: 'hidden',
          }}
        >
          <div
            style={{
              width: `${clampedConf * 100}%`,
              height: '100%',
              borderRadius: 2,
              background: color,
              transition: 'width 0.3s ease',
            }}
          />
        </div>
      </div>

      {/* Confidence number */}
      <span style={{ fontSize: 10, color: COLORS.textMuted, flexShrink: 0 }}>
        {(clampedConf * 100).toFixed(0)}%
      </span>
    </div>
  );
};

// ---------------------------------------------------------------------------
// 2. AlgedonicWidget
// ---------------------------------------------------------------------------
export interface AlgedonicWidgetProps {
  type: 'pain' | 'pleasure';
  severity: 'low' | 'medium' | 'high' | 'critical';
  source: string;
  message: string;
  timestamp?: string;
}

const SEVERITY_OPACITY: Record<string, number> = {
  low: 0.4,
  medium: 0.6,
  high: 0.8,
  critical: 1.0,
};

const SEVERITY_BORDER: Record<string, number> = {
  low: 1,
  medium: 1,
  high: 2,
  critical: 2,
};

export const AlgedonicWidget: React.FC<AlgedonicWidgetProps> = ({
  type,
  severity,
  source,
  message,
  timestamp,
}) => {
  const isPain = type === 'pain';
  const baseColor = isPain ? COLORS.red : COLORS.green;
  const opacity = SEVERITY_OPACITY[severity];
  const borderWidth = SEVERITY_BORDER[severity];
  const glowSpread = severity === 'critical' ? 8 : severity === 'high' ? 5 : 2;

  return (
    <div
      style={{
        background: COLORS.bg,
        border: `${borderWidth}px solid ${baseColor}`,
        borderRadius: 6,
        padding: '8px 12px',
        fontFamily: FONT,
        fontSize: 12,
        color: COLORS.text,
        boxShadow: `0 0 ${glowSpread}px ${baseColor}${Math.round(opacity * 99)
          .toString(16)
          .padStart(2, '0')}`,
        maxWidth: 320,
        boxSizing: 'border-box',
      }}
    >
      {/* Header row */}
      <div
        style={{
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'space-between',
          marginBottom: 4,
        }}
      >
        <span style={{ display: 'flex', alignItems: 'center', gap: 6 }}>
          <span
            style={{
              display: 'inline-block',
              width: 8,
              height: 8,
              borderRadius: '50%',
              background: baseColor,
              opacity,
              boxShadow: severity === 'critical' ? `0 0 6px ${baseColor}` : undefined,
              animation: severity === 'critical' ? 'algedonic-pulse 1.2s infinite' : undefined,
            }}
          />
          <span style={{ fontWeight: 600, color: baseColor, textTransform: 'uppercase', fontSize: 10 }}>
            {type} / {severity}
          </span>
        </span>
        {timestamp && (
          <span style={{ fontSize: 9, color: COLORS.textMuted }}>{timestamp}</span>
        )}
      </div>

      {/* Source */}
      <div style={{ fontSize: 10, color: COLORS.textMuted, marginBottom: 2 }}>{source}</div>

      {/* Message */}
      <div style={{ fontSize: 11, lineHeight: 1.4 }}>{message}</div>

      {/* Inline keyframes for critical pulse */}
      {severity === 'critical' && (
        <style>{`
          @keyframes algedonic-pulse {
            0%, 100% { opacity: ${opacity}; }
            50% { opacity: 0.2; }
          }
        `}</style>
      )}
    </div>
  );
};

// ---------------------------------------------------------------------------
// 3. HexavalentWidget
// ---------------------------------------------------------------------------
export interface HexavalentWidgetProps {
  values: { T: number; F: number; U: number; C: number; dT: number; dF: number };
  label?: string;
}

const HEX_AXES = ['T', 'F', 'U', 'C', 'dT', 'dF'] as const;
const HEX_AXIS_COLORS: Record<string, string> = {
  T: COLORS.green,
  F: COLORS.red,
  U: COLORS.amber,
  C: COLORS.purple,
  dT: '#66ddaa',
  dF: '#ff7777',
};

function polarToCart(angleDeg: number, radius: number, cx: number, cy: number) {
  const rad = ((angleDeg - 90) * Math.PI) / 180;
  return { x: cx + radius * Math.cos(rad), y: cy + radius * Math.sin(rad) };
}

export const HexavalentWidget: React.FC<HexavalentWidgetProps> = ({ values, label }) => {
  const size = 140;
  const cx = size / 2;
  const cy = size / 2;
  const maxR = 52;

  const points = useMemo(() => {
    return HEX_AXES.map((axis, i) => {
      const angle = (360 / 6) * i;
      const v = Math.max(0, Math.min(1, values[axis]));
      return polarToCart(angle, v * maxR, cx, cy);
    });
  }, [values, cx, cy, maxR]);

  const polygon = points.map((p) => `${p.x},${p.y}`).join(' ');

  // Grid rings
  const rings = [0.25, 0.5, 0.75, 1.0];

  return (
    <div
      style={{
        display: 'inline-flex',
        flexDirection: 'column',
        alignItems: 'center',
        background: COLORS.bg,
        border: `1px solid ${COLORS.border}`,
        borderRadius: 6,
        padding: 8,
        fontFamily: FONT,
        fontSize: 10,
        color: COLORS.text,
        boxSizing: 'border-box',
      }}
    >
      {label && (
        <div style={{ fontSize: 10, color: COLORS.textMuted, marginBottom: 4 }}>{label}</div>
      )}
      <svg width={size} height={size} viewBox={`0 0 ${size} ${size}`}>
        {/* Grid rings */}
        {rings.map((r) => {
          const ringPts = HEX_AXES.map((_, i) =>
            polarToCart((360 / 6) * i, r * maxR, cx, cy)
          );
          return (
            <polygon
              key={r}
              points={ringPts.map((p) => `${p.x},${p.y}`).join(' ')}
              fill="none"
              stroke={COLORS.border}
              strokeWidth={0.5}
            />
          );
        })}

        {/* Axis lines */}
        {HEX_AXES.map((_, i) => {
          const end = polarToCart((360 / 6) * i, maxR, cx, cy);
          return (
            <line
              key={i}
              x1={cx}
              y1={cy}
              x2={end.x}
              y2={end.y}
              stroke={COLORS.border}
              strokeWidth={0.5}
            />
          );
        })}

        {/* Data polygon */}
        <polygon
          points={polygon}
          fill={`${COLORS.gold}18`}
          stroke={COLORS.gold}
          strokeWidth={1.5}
          strokeLinejoin="round"
        />

        {/* Data points + labels */}
        {HEX_AXES.map((axis, i) => {
          const p = points[i];
          const labelPos = polarToCart((360 / 6) * i, maxR + 14, cx, cy);
          return (
            <g key={axis}>
              <circle cx={p.x} cy={p.y} r={2.5} fill={HEX_AXIS_COLORS[axis]} />
              <text
                x={labelPos.x}
                y={labelPos.y}
                textAnchor="middle"
                dominantBaseline="central"
                fill={HEX_AXIS_COLORS[axis]}
                fontSize={9}
                fontFamily={FONT}
              >
                {axis}
              </text>
            </g>
          );
        })}
      </svg>
    </div>
  );
};

// ---------------------------------------------------------------------------
// 4. StateWidget
// ---------------------------------------------------------------------------
export interface StateWidgetProps {
  phase: 'Plan' | 'Do' | 'Check' | 'Act';
  label: string;
  progress?: number;
  details?: string;
}

const PHASE_COLORS: Record<string, string> = {
  Plan: COLORS.amber,
  Do: COLORS.green,
  Check: '#58a6ff',
  Act: COLORS.gold,
};

export const StateWidget: React.FC<StateWidgetProps> = ({
  phase,
  label,
  progress,
  details,
}) => {
  const phaseColor = PHASE_COLORS[phase];
  const clampedProgress = progress != null ? Math.max(0, Math.min(1, progress)) : undefined;

  return (
    <div
      title={details ?? `${phase}: ${label}`}
      style={{
        display: 'inline-flex',
        alignItems: 'center',
        gap: 8,
        background: COLORS.bg,
        border: `1px solid ${COLORS.border}`,
        borderRadius: 6,
        padding: '8px 12px',
        fontFamily: FONT,
        fontSize: 12,
        color: COLORS.text,
        minWidth: 180,
        maxWidth: 300,
        boxSizing: 'border-box',
      }}
    >
      {/* Phase indicator */}
      <span
        style={{
          display: 'inline-flex',
          alignItems: 'center',
          justifyContent: 'center',
          width: 36,
          height: 24,
          borderRadius: 4,
          background: `${phaseColor}22`,
          color: phaseColor,
          fontWeight: 700,
          fontSize: 10,
          textTransform: 'uppercase',
          flexShrink: 0,
          letterSpacing: 0.5,
        }}
      >
        {phase}
      </span>

      {/* Label + progress */}
      <div style={{ flex: 1, minWidth: 0 }}>
        <div
          style={{
            overflow: 'hidden',
            textOverflow: 'ellipsis',
            whiteSpace: 'nowrap',
            fontSize: 11,
            marginBottom: clampedProgress != null ? 4 : 0,
          }}
        >
          {label}
        </div>
        {clampedProgress != null && (
          <div
            style={{
              height: 3,
              borderRadius: 2,
              background: COLORS.border,
              overflow: 'hidden',
            }}
          >
            <div
              style={{
                width: `${clampedProgress * 100}%`,
                height: '100%',
                borderRadius: 2,
                background: phaseColor,
                transition: 'width 0.3s ease',
              }}
            />
          </div>
        )}
      </div>

      {clampedProgress != null && (
        <span style={{ fontSize: 10, color: COLORS.textMuted, flexShrink: 0 }}>
          {(clampedProgress * 100).toFixed(0)}%
        </span>
      )}
    </div>
  );
};

// ---------------------------------------------------------------------------
// 5. IxqlPreview
// ---------------------------------------------------------------------------
export interface IxqlPreviewProps {
  code: string;
  result?: string;
  status?: 'idle' | 'running' | 'complete' | 'error';
  onClick?: () => void;
}

const IXQL_KEYWORDS = [
  'SELECT', 'FROM', 'WHERE', 'FILTER', 'MAP', 'REDUCE', 'JOIN',
  'ORDER', 'BY', 'GROUP', 'LIMIT', 'AS', 'ON', 'INTO', 'WITH',
  'PIPE', 'EMIT', 'STREAM', 'TRANSFORM',
];

const IXQL_STATUS_COLORS: Record<string, string> = {
  idle: COLORS.textMuted,
  running: COLORS.amber,
  complete: COLORS.green,
  error: COLORS.red,
};

function highlightIxql(code: string): React.ReactNode[] {
  const pattern = new RegExp(`\\b(${IXQL_KEYWORDS.join('|')})\\b`, 'gi');
  const parts: React.ReactNode[] = [];
  let lastIndex = 0;
  let match: RegExpExecArray | null;

  while ((match = pattern.exec(code)) !== null) {
    if (match.index > lastIndex) {
      parts.push(code.slice(lastIndex, match.index));
    }
    parts.push(
      <span key={match.index} style={{ color: COLORS.gold, fontWeight: 600 }}>
        {match[0].toUpperCase()}
      </span>
    );
    lastIndex = match.index + match[0].length;
  }
  if (lastIndex < code.length) {
    parts.push(code.slice(lastIndex));
  }
  return parts;
}

export const IxqlPreview: React.FC<IxqlPreviewProps> = ({
  code,
  result,
  status = 'idle',
  onClick,
}) => {
  const statusColor = IXQL_STATUS_COLORS[status];
  const [hoverRun, setHoverRun] = useState(false);

  return (
    <div
      style={{
        background: COLORS.bg,
        border: `1px solid ${COLORS.border}`,
        borderRadius: 6,
        fontFamily: FONT,
        fontSize: 12,
        color: COLORS.text,
        maxWidth: 480,
        overflow: 'hidden',
        boxSizing: 'border-box',
      }}
    >
      {/* Toolbar */}
      <div
        style={{
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'space-between',
          padding: '4px 10px',
          background: COLORS.bgLight,
          borderBottom: `1px solid ${COLORS.border}`,
        }}
      >
        <span style={{ fontSize: 10, color: COLORS.textMuted, letterSpacing: 0.5 }}>IXQL</span>
        <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
          {/* Status dot */}
          <span style={{ display: 'flex', alignItems: 'center', gap: 4, fontSize: 10 }}>
            <span
              style={{
                display: 'inline-block',
                width: 6,
                height: 6,
                borderRadius: '50%',
                background: statusColor,
                animation: status === 'running' ? 'ixql-spin 1s linear infinite' : undefined,
              }}
            />
            <span style={{ color: statusColor }}>{status}</span>
          </span>
          {/* Run button */}
          <button
            onClick={onClick}
            onMouseEnter={() => setHoverRun(true)}
            onMouseLeave={() => setHoverRun(false)}
            style={{
              display: 'inline-flex',
              alignItems: 'center',
              gap: 4,
              padding: '2px 8px',
              borderRadius: 3,
              border: `1px solid ${COLORS.green}`,
              background: hoverRun ? `${COLORS.green}33` : 'transparent',
              color: COLORS.green,
              fontFamily: FONT,
              fontSize: 10,
              cursor: 'pointer',
              transition: 'background 0.15s',
            }}
          >
            <span style={{ fontSize: 8 }}>&#9654;</span> Run
          </button>
        </div>
      </div>

      {/* Code area */}
      <pre
        style={{
          margin: 0,
          padding: '8px 12px',
          fontSize: 11,
          lineHeight: 1.5,
          whiteSpace: 'pre-wrap',
          wordBreak: 'break-word',
          overflowX: 'auto',
        }}
      >
        {highlightIxql(code)}
      </pre>

      {/* Result area */}
      {result != null && (
        <div
          style={{
            borderTop: `1px solid ${COLORS.border}`,
            padding: '6px 12px',
            background: COLORS.bgLight,
            fontSize: 10,
            lineHeight: 1.5,
            color: status === 'error' ? COLORS.red : COLORS.textMuted,
            whiteSpace: 'pre-wrap',
            wordBreak: 'break-word',
            maxHeight: 120,
            overflowY: 'auto',
          }}
        >
          {result}
        </div>
      )}

      {/* Inline keyframes for running spinner */}
      {status === 'running' && (
        <style>{`
          @keyframes ixql-spin {
            0% { opacity: 1; }
            50% { opacity: 0.3; }
            100% { opacity: 1; }
          }
        `}</style>
      )}
    </div>
  );
};
