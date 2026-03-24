// src/components/PrimeRadiant/DetailPanel.tsx
// React side panel showing selected governance node details

import React from 'react';
import type { GovernanceNode, GovernanceEdge, GovernanceNodeType } from './types';
import { NODE_COLORS, HEALTH_COLORS } from './types';
import { getHealthStatus } from './DataLoader';
import type { GraphIndex } from './DataLoader';

// ---------------------------------------------------------------------------
// Props
// ---------------------------------------------------------------------------
export interface DetailPanelProps {
  node: GovernanceNode | null;
  graphIndex: GraphIndex | null;
  onClose: () => void;
  onNavigate: (nodeId: string) => void;
}

// ---------------------------------------------------------------------------
// Node type display names
// ---------------------------------------------------------------------------
const TYPE_LABELS: Record<GovernanceNodeType, string> = {
  constitution: 'Constitution',
  policy: 'Policy',
  persona: 'Persona',
  pipeline: 'Pipeline',
  department: 'Department',
  schema: 'Schema',
  test: 'Test Suite',
  ixql: 'IxQL File',
};

const TYPE_SHAPES: Record<GovernanceNodeType, string> = {
  constitution: 'Core Cluster',
  policy: 'Diamond Cloud',
  persona: 'Sphere Cloud',
  pipeline: 'Ring Stream',
  department: 'Nebula Cluster',
  schema: 'Cube Matrix',
  test: 'Diamond Cloud',
  ixql: 'Helix Stream',
};

// ---------------------------------------------------------------------------
// Component
// ---------------------------------------------------------------------------
export const DetailPanel: React.FC<DetailPanelProps> = ({
  node,
  graphIndex,
  onClose,
  onNavigate,
}) => {
  const isOpen = node !== null;

  // Gather connections
  const connections: Array<{
    nodeId: string;
    name: string;
    type: GovernanceNodeType;
    color: string;
    direction: 'in' | 'out';
    edgeType: string;
  }> = [];

  if (node && graphIndex) {
    const outEdges = graphIndex.outEdges.get(node.id) ?? [];
    const inEdges = graphIndex.inEdges.get(node.id) ?? [];

    for (const edge of outEdges) {
      const target = graphIndex.nodeMap.get(edge.target);
      if (target) {
        connections.push({
          nodeId: target.id,
          name: target.name,
          type: target.type,
          color: target.color,
          direction: 'out',
          edgeType: edge.type,
        });
      }
    }

    for (const edge of inEdges) {
      const source = graphIndex.nodeMap.get(edge.source);
      if (source) {
        connections.push({
          nodeId: source.id,
          name: source.name,
          type: source.type,
          color: source.color,
          direction: 'in',
          edgeType: edge.type,
        });
      }
    }
  }

  const health = node?.health;
  const healthStatus = health ? getHealthStatus(health.resilienceScore) : null;

  return (
    <div className={`prime-radiant__detail ${isOpen ? 'prime-radiant__detail--open' : ''}`}>
      {node && (
        <>
          {/* Header */}
          <div className="prime-radiant__detail-header">
            <h3
              className="prime-radiant__detail-title"
              style={{ color: node.color }}
            >
              {node.name}
            </h3>
            <button
              className="prime-radiant__detail-close"
              onClick={onClose}
              aria-label="Close detail panel"
            >
              x
            </button>
          </div>

          {/* Body */}
          <div className="prime-radiant__detail-body">
            {/* Type badge */}
            <div className="prime-radiant__detail-section">
              <span className="prime-radiant__detail-label">Type</span>
              <span>
                <span
                  className="prime-radiant__detail-badge"
                  style={{
                    backgroundColor: `${node.color}22`,
                    color: node.color,
                    border: `1px solid ${node.color}44`,
                  }}
                >
                  {TYPE_SHAPES[node.type]} — {TYPE_LABELS[node.type]}
                </span>
              </span>
            </div>

            {/* Description */}
            <div className="prime-radiant__detail-section">
              <span className="prime-radiant__detail-label">Description</span>
              <span className="prime-radiant__detail-value">{node.description}</span>
            </div>

            {/* Repo */}
            {node.repo && (
              <div className="prime-radiant__detail-section">
                <span className="prime-radiant__detail-label">Repository</span>
                <span className="prime-radiant__detail-value">{node.repo}</span>
              </div>
            )}

            {/* Version */}
            {node.version && (
              <div className="prime-radiant__detail-section">
                <span className="prime-radiant__detail-label">Version</span>
                <span className="prime-radiant__detail-value">v{node.version}</span>
              </div>
            )}

            {/* Health metrics */}
            {health && healthStatus && (
              <>
                <div className="prime-radiant__detail-section">
                  <span className="prime-radiant__detail-label">Resilience Score</span>
                  <div className="prime-radiant__detail-health-bar">
                    <div
                      className="prime-radiant__detail-health-fill"
                      style={{
                        width: `${health.resilienceScore * 100}%`,
                        backgroundColor: HEALTH_COLORS[healthStatus],
                      }}
                    />
                  </div>
                  <span className="prime-radiant__detail-value" style={{ fontSize: '11px', color: '#8b949e' }}>
                    {(health.resilienceScore * 100).toFixed(0)}% — {healthStatus}
                  </span>
                </div>

                <div style={{ display: 'flex', gap: '16px' }}>
                  <div className="prime-radiant__detail-section" style={{ flex: 1 }}>
                    <span className="prime-radiant__detail-label">ERGOL</span>
                    <span className="prime-radiant__detail-value" style={{ color: '#FFD700' }}>
                      {health.ergolCount}
                    </span>
                  </div>
                  <div className="prime-radiant__detail-section" style={{ flex: 1 }}>
                    <span className="prime-radiant__detail-label">LOLLI</span>
                    <span
                      className="prime-radiant__detail-value"
                      style={{ color: health.lolliCount > 0 ? '#FF4444' : '#8b949e' }}
                    >
                      {health.lolliCount}
                    </span>
                  </div>
                </div>
              </>
            )}

            {/* Connections */}
            {connections.length > 0 && (
              <div className="prime-radiant__detail-section">
                <span className="prime-radiant__detail-label">
                  Connections ({connections.length})
                </span>
                <ul className="prime-radiant__detail-connections">
                  {connections.map((conn) => (
                    <li
                      key={`${conn.direction}-${conn.nodeId}`}
                      className="prime-radiant__detail-connection"
                      onClick={() => onNavigate(conn.nodeId)}
                    >
                      <span
                        className="prime-radiant__detail-connection-dot"
                        style={{ backgroundColor: conn.color }}
                      />
                      <span>
                        {conn.direction === 'out' ? '\u2192' : '\u2190'}{' '}
                        {conn.name}
                      </span>
                      <span style={{ marginLeft: 'auto', fontSize: '10px', color: '#484f58' }}>
                        {conn.edgeType}
                      </span>
                    </li>
                  ))}
                </ul>
              </div>
            )}
          </div>
        </>
      )}
    </div>
  );
};
