// Custom React Flow node renderers for IxQL node types

import React, { memo } from 'react';
import { Handle, Position } from 'reactflow';
import type { NodeProps } from 'reactflow';
import type { IxqlBinding, LolliStatus } from './types';

const LOLLI_COLORS: Record<LolliStatus, { bg: string; border: string; text: string }> = {
  live: { bg: '#1a3a2a', border: '#4cb050', text: '#a8e6a8' },
  dead: { bg: '#3a1a1a', border: '#e05555', text: '#f0a0a0' },
  external: { bg: '#2a2a2a', border: '#888888', text: '#cccccc' },
};

const KIND_ICONS: Record<string, string> = {
  binding: '=',
  fan_out: '⑂',
  parallel: '∥',
  when: '◇',
  filter: '⧫',
  head: '⊤',
  write: '✎',
  alert: '⚠',
  governance_gate: '🛡',
  compound: '◎',
};

interface BindingNodeData {
  binding: IxqlBinding;
  onNodeClick?: (binding: IxqlBinding) => void;
}

const nodeStyle = (lolliStatus: LolliStatus, executionMode: string): React.CSSProperties => {
  const colors = LOLLI_COLORS[lolliStatus];
  return {
    background: colors.bg,
    border: `2px solid ${colors.border}`,
    borderRadius: executionMode === 'parallel' ? 12 : 6,
    padding: '8px 14px',
    color: colors.text,
    fontSize: 13,
    fontFamily: "'JetBrains Mono', 'Fira Code', monospace",
    minWidth: 140,
    maxWidth: 280,
    cursor: 'pointer',
    transition: 'box-shadow 0.2s, transform 0.15s',
  };
};

export const BindingNode = memo(({ data }: NodeProps<BindingNodeData>) => {
  const { binding, onNodeClick } = data;
  const icon = KIND_ICONS[binding.kind] || '=';
  const modeIcon = binding.executionMode === 'parallel' ? ' ∥' : ' ⚡';

  return (
    <div
      style={nodeStyle(binding.lolliStatus, binding.executionMode)}
      onClick={() => onNodeClick?.(binding)}
      title={`${binding.name} (${binding.kind})`}
    >
      <Handle type="target" position={Position.Top} style={{ background: '#555' }} />
      <div style={{ display: 'flex', alignItems: 'center', gap: 6, marginBottom: 4 }}>
        <span style={{ fontSize: 16 }}>{icon}</span>
        <strong style={{ fontSize: 13 }}>{binding.name}</strong>
        <span style={{ fontSize: 10, opacity: 0.6, marginLeft: 'auto' }}>{modeIcon}</span>
      </div>
      <div
        style={{
          fontSize: 11,
          opacity: 0.7,
          overflow: 'hidden',
          textOverflow: 'ellipsis',
          whiteSpace: 'nowrap',
          maxWidth: 250,
        }}
      >
        {binding.expression.split('\n')[0].slice(0, 60)}
        {binding.expression.length > 60 ? '...' : ''}
      </div>
      {binding.plainComments.length > 0 && (
        <div style={{ fontSize: 10, opacity: 0.5, marginTop: 4, fontStyle: 'italic' }}>
          {binding.plainComments[0].slice(0, 50)}
        </div>
      )}
      <Handle type="source" position={Position.Bottom} style={{ background: '#555' }} />
    </div>
  );
});

BindingNode.displayName = 'BindingNode';

export const GovernanceGateNode = memo(({ data }: NodeProps<BindingNodeData>) => {
  const { binding, onNodeClick } = data;

  return (
    <div
      style={{
        ...nodeStyle(binding.lolliStatus, binding.executionMode),
        borderColor: '#f0883e',
        background: '#2a1f0a',
        borderStyle: 'double',
        borderWidth: 3,
        textAlign: 'center',
      }}
      onClick={() => onNodeClick?.(binding)}
    >
      <Handle type="target" position={Position.Top} style={{ background: '#f0883e' }} />
      <div style={{ fontSize: 20, marginBottom: 4 }}>🛡</div>
      <strong style={{ fontSize: 12, color: '#f0c070' }}>Governance Gate</strong>
      <div style={{ fontSize: 10, opacity: 0.6, marginTop: 2 }}>
        {binding.expression.split('\n')[0].slice(0, 50)}
      </div>
      <Handle type="source" position={Position.Bottom} style={{ background: '#f0883e' }} />
    </div>
  );
});

GovernanceGateNode.displayName = 'GovernanceGateNode';

export const WhenNode = memo(({ data }: NodeProps<BindingNodeData>) => {
  const { binding, onNodeClick } = data;

  return (
    <div
      style={{
        ...nodeStyle(binding.lolliStatus, binding.executionMode),
        borderRadius: 0,
        transform: 'rotate(0deg)',
        clipPath: 'polygon(50% 0%, 100% 50%, 50% 100%, 0% 50%)',
        padding: '20px 30px',
        textAlign: 'center',
        minWidth: 120,
      }}
      onClick={() => onNodeClick?.(binding)}
    >
      <Handle type="target" position={Position.Top} style={{ background: '#555' }} />
      <div style={{ fontSize: 10 }}>
        <strong>when</strong>
      </div>
      <div style={{ fontSize: 9, opacity: 0.7 }}>
        {binding.expression.replace(/^when\s+/, '').split(':')[0].slice(0, 30)}
      </div>
      <Handle type="source" position={Position.Bottom} style={{ background: '#555' }} />
    </div>
  );
});

WhenNode.displayName = 'WhenNode';

export const CompoundNode = memo(({ data }: NodeProps<BindingNodeData>) => {
  const { binding, onNodeClick } = data;
  const lines = binding.expression.split('\n').filter((l) => l.trim().length > 0);

  return (
    <div
      style={{
        ...nodeStyle(binding.lolliStatus, binding.executionMode),
        borderColor: '#c678dd',
        background: '#1a1a2e',
        borderRadius: 12,
      }}
      onClick={() => onNodeClick?.(binding)}
    >
      <Handle type="target" position={Position.Top} style={{ background: '#c678dd' }} />
      <div style={{ display: 'flex', alignItems: 'center', gap: 6, marginBottom: 6 }}>
        <span style={{ fontSize: 16 }}>◎</span>
        <strong style={{ color: '#d8a0f0' }}>compound</strong>
      </div>
      {lines.slice(0, 5).map((line, i) => (
        <div key={i} style={{ fontSize: 10, opacity: 0.7, lineHeight: 1.4 }}>
          {line.trim().slice(0, 50)}
        </div>
      ))}
      {lines.length > 5 && (
        <div style={{ fontSize: 10, opacity: 0.4 }}>+{lines.length - 5} more...</div>
      )}
      <Handle type="source" position={Position.Bottom} style={{ background: '#c678dd' }} />
    </div>
  );
});

CompoundNode.displayName = 'CompoundNode';

export const nodeTypes = {
  binding: BindingNode,
  fan_out: BindingNode,
  parallel: BindingNode,
  filter: BindingNode,
  head: BindingNode,
  write: BindingNode,
  alert: BindingNode,
  when: WhenNode,
  governance_gate: GovernanceGateNode,
  compound: CompoundNode,
};
