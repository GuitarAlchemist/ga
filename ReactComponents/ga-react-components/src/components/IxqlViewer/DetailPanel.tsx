// DetailPanel — Side panel showing node details when clicked

import React from 'react';
import type { IxqlBinding, LolliReport, AmdahlReport } from './types';
import { MarkdownCard } from './MarkdownCard';

interface DetailPanelProps {
  binding: IxqlBinding | null;
  lolliReport: LolliReport | null;
  amdahlReport: AmdahlReport | null;
  onClose: () => void;
}

const statusColors = {
  live: '#4cb050',
  dead: '#e05555',
  external: '#888888',
};

const statusLabels = {
  live: 'LIVE — referenced downstream',
  dead: 'DEAD (LOLLI) — never referenced',
  external: 'EXTERNAL — output, not consumed by other pipelines',
};

export const DetailPanel: React.FC<DetailPanelProps> = ({
  binding,
  lolliReport,
  amdahlReport,
  onClose,
}) => {
  return (
    <div
      style={{
        width: 340,
        height: '100%',
        background: '#0d1117',
        borderLeft: '1px solid #30363d',
        overflow: 'auto',
        padding: 16,
        fontFamily: '-apple-system, BlinkMacSystemFont, sans-serif',
        color: '#c9d1d9',
        fontSize: 13,
      }}
    >
      {binding ? (
        <>
          <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
            <h3 style={{ margin: 0, color: '#e6edf3', fontSize: 16 }}>{binding.name}</h3>
            <button
              onClick={onClose}
              style={{
                background: 'none',
                border: 'none',
                color: '#8b949e',
                cursor: 'pointer',
                fontSize: 18,
              }}
            >
              x
            </button>
          </div>

          <div style={{ marginTop: 12 }}>
            <Label>Kind</Label>
            <Value>{binding.kind}</Value>
          </div>

          <div style={{ marginTop: 8 }}>
            <Label>Line</Label>
            <Value>{binding.line}</Value>
          </div>

          <div style={{ marginTop: 8 }}>
            <Label>Execution</Label>
            <Value>
              {binding.executionMode === 'parallel' ? '∥ Parallel' : '⚡ Serial'}
            </Value>
          </div>

          <div style={{ marginTop: 8 }}>
            <Label>LOLLI Status</Label>
            <span
              style={{
                color: statusColors[binding.lolliStatus],
                fontWeight: 600,
                fontSize: 12,
              }}
            >
              {statusLabels[binding.lolliStatus]}
            </span>
          </div>

          <div style={{ marginTop: 12 }}>
            <Label>References (uses)</Label>
            {binding.references.length > 0 ? (
              <ul style={{ margin: '4px 0', paddingLeft: 18, fontSize: 12 }}>
                {binding.references.map((r) => (
                  <li key={r} style={{ color: '#58a6ff' }}>
                    {r}
                  </li>
                ))}
              </ul>
            ) : (
              <Value>none</Value>
            )}
          </div>

          <div style={{ marginTop: 8 }}>
            <Label>Referenced by</Label>
            {binding.referencedBy.length > 0 ? (
              <ul style={{ margin: '4px 0', paddingLeft: 18, fontSize: 12 }}>
                {binding.referencedBy.map((r) => (
                  <li key={r} style={{ color: '#58a6ff' }}>
                    {r}
                  </li>
                ))}
              </ul>
            ) : (
              <Value>none (potential LOLLI)</Value>
            )}
          </div>

          <div style={{ marginTop: 12 }}>
            <Label>Expression</Label>
            <pre
              style={{
                background: '#161b22',
                border: '1px solid #30363d',
                borderRadius: 6,
                padding: 10,
                fontSize: 11,
                fontFamily: "'JetBrains Mono', monospace",
                color: '#a0c0e0',
                overflow: 'auto',
                maxHeight: 200,
                whiteSpace: 'pre-wrap',
                wordBreak: 'break-word',
              }}
            >
              {binding.expression}
            </pre>
          </div>

          {binding.plainComments.length > 0 && (
            <div style={{ marginTop: 12 }}>
              <Label>Comments</Label>
              {binding.plainComments.map((c, i) => (
                <div key={i} style={{ fontSize: 11, color: '#8b949e', fontStyle: 'italic' }}>
                  -- {c}
                </div>
              ))}
            </div>
          )}

          {binding.markdownComment && (
            <div style={{ marginTop: 12 }}>
              <Label>Documentation</Label>
              <MarkdownCard content={binding.markdownComment} bindingName={binding.name} />
            </div>
          )}
        </>
      ) : (
        <SummaryView lolliReport={lolliReport} amdahlReport={amdahlReport} />
      )}
    </div>
  );
};

const SummaryView: React.FC<{
  lolliReport: LolliReport | null;
  amdahlReport: AmdahlReport | null;
}> = ({ lolliReport, amdahlReport }) => (
  <div>
    <h3 style={{ margin: '0 0 16px', color: '#e6edf3', fontSize: 16 }}>Pipeline Summary</h3>

    {lolliReport && (
      <div style={{ marginBottom: 20 }}>
        <Label>LOLLI Analysis</Label>
        <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 8, marginTop: 8 }}>
          <StatBox label="Total" value={lolliReport.totalBindings} color="#c9d1d9" />
          <StatBox label="Live" value={lolliReport.liveBindings} color="#4cb050" />
          <StatBox label="Dead" value={lolliReport.deadBindings} color="#e05555" />
          <StatBox label="External" value={lolliReport.externalBindings} color="#888" />
        </div>
        <div
          style={{
            marginTop: 10,
            padding: '8px 12px',
            background: lolliReport.lolliScore > 0 ? '#3a1a1a' : '#1a3a2a',
            border: `1px solid ${lolliReport.lolliScore > 0 ? '#e05555' : '#4cb050'}`,
            borderRadius: 6,
            textAlign: 'center',
            fontWeight: 600,
            fontSize: 14,
            color: lolliReport.lolliScore > 0 ? '#f0a0a0' : '#a8e6a8',
          }}
        >
          LOLLI Score: {lolliReport.lolliScore.toFixed(0)}%
          <div style={{ fontSize: 10, fontWeight: 400, opacity: 0.7, marginTop: 2 }}>
            {lolliReport.deadBindings} dead / {lolliReport.totalBindings} total
          </div>
        </div>
        {lolliReport.deadNames.length > 0 && (
          <div style={{ marginTop: 8 }}>
            <div style={{ fontSize: 11, color: '#e05555' }}>Dead bindings:</div>
            {lolliReport.deadNames.map((n) => (
              <div key={n} style={{ fontSize: 11, color: '#f0a0a0', paddingLeft: 8 }}>
                {n}
              </div>
            ))}
          </div>
        )}
      </div>
    )}

    {amdahlReport && (
      <div>
        <Label>Amdahl's Law</Label>
        <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 8, marginTop: 8 }}>
          <StatBox label="Serial" value={amdahlReport.serialStages} color="#e06c75" />
          <StatBox label="Parallel" value={amdahlReport.parallelStages} color="#56b6c2" />
        </div>
        <div
          style={{
            marginTop: 10,
            padding: '8px 12px',
            background: '#161b22',
            border: '1px solid #30363d',
            borderRadius: 6,
            fontSize: 12,
          }}
        >
          <div>
            Serial fraction: <strong>{(amdahlReport.serialFraction * 100).toFixed(0)}%</strong>
          </div>
          <div style={{ marginTop: 4 }}>
            Max speedup at N=4:{' '}
            <strong>{amdahlReport.speedupAtN(4).toFixed(1)}x</strong>
          </div>
          <div style={{ marginTop: 4 }}>
            Max speedup at N=8:{' '}
            <strong>{amdahlReport.speedupAtN(8).toFixed(1)}x</strong>
          </div>
          <div style={{ marginTop: 4, opacity: 0.7 }}>
            Theoretical max (N=inf):{' '}
            <strong>
              {amdahlReport.theoreticalMax === Infinity
                ? '∞'
                : amdahlReport.theoreticalMax.toFixed(1) + 'x'}
            </strong>
          </div>
        </div>
      </div>
    )}

    {!lolliReport && !amdahlReport && (
      <div style={{ color: '#8b949e', textAlign: 'center', marginTop: 40 }}>
        Load an IxQL pipeline to see analysis
      </div>
    )}
  </div>
);

const Label: React.FC<{ children: React.ReactNode }> = ({ children }) => (
  <div
    style={{
      fontSize: 10,
      textTransform: 'uppercase',
      letterSpacing: 0.5,
      color: '#8b949e',
      marginBottom: 2,
    }}
  >
    {children}
  </div>
);

const Value: React.FC<{ children: React.ReactNode }> = ({ children }) => (
  <div style={{ fontSize: 13, color: '#c9d1d9' }}>{children}</div>
);

const StatBox: React.FC<{ label: string; value: number; color: string }> = ({
  label,
  value,
  color,
}) => (
  <div
    style={{
      background: '#161b22',
      border: '1px solid #30363d',
      borderRadius: 6,
      padding: '6px 10px',
      textAlign: 'center',
    }}
  >
    <div style={{ fontSize: 18, fontWeight: 700, color }}>{value}</div>
    <div style={{ fontSize: 10, color: '#8b949e' }}>{label}</div>
  </div>
);
