// src/components/PrimeRadiant/GodotSceneInspectorPanel.tsx
// Panel that inspects a running Godot scene via the Godot MCP server.
// Shows scene tree, node properties, and basic stats.
//
// Graceful fallback to DEMO_SCENE when /api/godot/scene-tree is unavailable.
// Polls every 5s with a 3s per-request timeout so it never blocks the UI.

import React, { useState, useEffect, useCallback, useMemo } from 'react';

// ---------------------------------------------------------------------------
// Types
// ---------------------------------------------------------------------------

interface GodotNode {
  name: string;
  type: string;
  path: string;
  children: GodotNode[];
  properties?: Record<string, unknown>;
}

interface GodotSceneInfo {
  currentScene: string;
  rootNode: GodotNode;
  nodeCount: number;
  fps: number;
}

// ---------------------------------------------------------------------------
// Node type -> colour mapping (badge / label tint)
// ---------------------------------------------------------------------------

function nodeTypeColor(type: string): string {
  if (type.startsWith('Camera')) return '#4aa3ff';
  if (type.includes('Mesh')) return '#4ade80';
  if (type.includes('Collision')) return '#f59e0b';
  if (type.startsWith('Node3D') || type === 'Node3D') return '#9ca3af';
  if (type.includes('Light')) return '#fde047';
  if (type.includes('Environment')) return '#a78bfa';
  if (type.includes('Body')) return '#fb7185';
  return '#7dd3fc';
}

// ---------------------------------------------------------------------------
// Recursive tree row — memoized so unrelated toggles don't re-render siblings
// ---------------------------------------------------------------------------

interface TreeRowProps {
  node: GodotNode;
  depth: number;
  selected: string | null;
  expanded: Set<string>;
  onToggle: (path: string) => void;
  onSelect: (path: string) => void;
}

const TreeRow: React.FC<TreeRowProps> = React.memo(({ node, depth, selected, expanded, onToggle, onSelect }) => {
  const isExpanded = expanded.has(node.path);
  const isSelected = selected === node.path;
  const hasChildren = node.children && node.children.length > 0;
  const color = nodeTypeColor(node.type);

  return (
    <>
      <div
        className="godot-inspector__row"
        style={{
          display: 'flex',
          alignItems: 'center',
          gap: 6,
          padding: '2px 6px',
          paddingLeft: 6 + depth * 14,
          cursor: 'pointer',
          background: isSelected ? 'rgba(74, 163, 255, 0.18)' : 'transparent',
          borderLeft: isSelected ? '2px solid #4aa3ff' : '2px solid transparent',
          fontFamily: 'ui-monospace, SFMono-Regular, Menlo, monospace',
          fontSize: 12,
          lineHeight: '18px',
        }}
        onClick={() => onSelect(node.path)}
      >
        <span
          onClick={(e) => {
            e.stopPropagation();
            if (hasChildren) onToggle(node.path);
          }}
          style={{
            display: 'inline-block',
            width: 12,
            textAlign: 'center',
            color: '#6b7280',
            userSelect: 'none',
          }}
        >
          {hasChildren ? (isExpanded ? '▾' : '▸') : ' '}
        </span>
        <span
          style={{
            fontSize: 10,
            padding: '0 4px',
            borderRadius: 3,
            background: `${color}22`,
            color,
            border: `1px solid ${color}55`,
          }}
        >
          {node.type}
        </span>
        <span style={{ color: '#e5e7eb' }}>{node.name}</span>
      </div>
      {isExpanded && hasChildren && node.children.map((child) => (
        <TreeRow
          key={child.path}
          node={child}
          depth={depth + 1}
          selected={selected}
          expanded={expanded}
          onToggle={onToggle}
          onSelect={onSelect}
        />
      ))}
    </>
  );
});
TreeRow.displayName = 'GodotInspectorTreeRow';

// ---------------------------------------------------------------------------
// Helper: locate a node by path in the tree
// ---------------------------------------------------------------------------

function findNodeByPath(root: GodotNode, path: string): GodotNode | null {
  if (root.path === path) return root;
  for (const child of root.children ?? []) {
    const hit = findNodeByPath(child, path);
    if (hit) return hit;
  }
  return null;
}

// ---------------------------------------------------------------------------
// Main panel
// ---------------------------------------------------------------------------

export const GodotSceneInspectorPanel: React.FC = () => {
  const [sceneInfo, setSceneInfo] = useState<GodotSceneInfo | null>(null);
  const [selectedNode, setSelectedNode] = useState<string | null>(null);
  const [expandedNodes, setExpandedNodes] = useState<Set<string>>(() => new Set(['/root/Main']));
  const [isLive, setIsLive] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [lastFetch, setLastFetch] = useState<number>(0);

  const fetchSceneInfo = useCallback(async () => {
    try {
      const res = await fetch('/api/godot/scene-tree', {
        signal: AbortSignal.timeout(3000),
      });
      if (!res.ok) throw new Error(`HTTP ${res.status}`);
      const data: GodotSceneInfo = await res.json();
      setSceneInfo(data);
      setIsLive(true);
      setError(null);
    } catch (err) {
      setIsLive(false);
      setError(err instanceof Error ? err.message : 'Unknown error');
      setSceneInfo((prev) => prev ?? DEMO_SCENE);
    } finally {
      setLastFetch(Date.now());
    }
  }, []);

  useEffect(() => {
    fetchSceneInfo();
    const interval = setInterval(fetchSceneInfo, 5000);
    return () => clearInterval(interval);
  }, [fetchSceneInfo]);

  const handleToggle = useCallback((path: string) => {
    setExpandedNodes((prev) => {
      const next = new Set(prev);
      if (next.has(path)) next.delete(path); else next.add(path);
      return next;
    });
  }, []);

  const handleSelect = useCallback((path: string) => {
    setSelectedNode(path);
  }, []);

  const handleExpandAll = useCallback(() => {
    if (!sceneInfo) return;
    const all = new Set<string>();
    const walk = (n: GodotNode) => {
      all.add(n.path);
      n.children?.forEach(walk);
    };
    walk(sceneInfo.rootNode);
    setExpandedNodes(all);
  }, [sceneInfo]);

  const handleCollapseAll = useCallback(() => {
    setExpandedNodes(new Set());
  }, []);

  const handleExportJson = useCallback(() => {
    if (!sceneInfo) return;
    const json = JSON.stringify(sceneInfo, null, 2);
    const blob = new Blob([json], { type: 'application/json' });
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = `godot-scene-tree-${Date.now()}.json`;
    a.click();
    URL.revokeObjectURL(url);
  }, [sceneInfo]);

  const selectedNodeData = useMemo(() => {
    if (!sceneInfo || !selectedNode) return null;
    return findNodeByPath(sceneInfo.rootNode, selectedNode);
  }, [sceneInfo, selectedNode]);

  // ------------------------------------------------------------------
  // Render
  // ------------------------------------------------------------------

  if (!sceneInfo) {
    return (
      <div style={{ padding: 12, color: '#9ca3af', fontFamily: 'ui-monospace, monospace', fontSize: 12 }}>
        Loading Godot scene...
      </div>
    );
  }

  return (
    <div
      className="godot-inspector"
      style={{
        display: 'flex',
        flexDirection: 'column',
        height: '100%',
        color: '#e5e7eb',
        fontFamily: 'system-ui, -apple-system, sans-serif',
        fontSize: 12,
      }}
    >
      {/* Header */}
      <div
        style={{
          padding: '10px 12px',
          borderBottom: '1px solid rgba(255,255,255,0.08)',
          display: 'flex',
          flexDirection: 'column',
          gap: 6,
        }}
      >
        <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
          <span
            title={isLive ? 'Live — Godot MCP connected' : 'Demo — Godot MCP unavailable'}
            style={{
              width: 8,
              height: 8,
              borderRadius: '50%',
              background: isLive ? '#4ade80' : '#f59e0b',
              boxShadow: isLive ? '0 0 6px #4ade80' : '0 0 6px #f59e0b',
            }}
          />
          <span style={{ fontWeight: 600, color: '#f3f4f6' }}>Godot Scene Inspector</span>
          <span style={{ marginLeft: 'auto', color: '#6b7280', fontSize: 10 }}>
            {isLive ? 'LIVE' : 'DEMO'}
          </span>
        </div>
        <div style={{ color: '#9ca3af', fontFamily: 'ui-monospace, monospace', fontSize: 11, wordBreak: 'break-all' }}>
          {sceneInfo.currentScene}
        </div>
        <div style={{ display: 'flex', gap: 12, color: '#6b7280', fontSize: 11 }}>
          <span>nodes: <strong style={{ color: '#e5e7eb' }}>{sceneInfo.nodeCount}</strong></span>
          <span>fps: <strong style={{ color: sceneInfo.fps >= 55 ? '#4ade80' : sceneInfo.fps >= 30 ? '#f59e0b' : '#fb7185' }}>{sceneInfo.fps}</strong></span>
          {error && !isLive && (
            <span style={{ color: '#f59e0b', marginLeft: 'auto' }} title={error}>offline</span>
          )}
        </div>
      </div>

      {/* Scene tree */}
      <div
        style={{
          flex: '1 1 auto',
          overflow: 'auto',
          padding: '6px 0',
          minHeight: 120,
        }}
      >
        <TreeRow
          node={sceneInfo.rootNode}
          depth={0}
          selected={selectedNode}
          expanded={expandedNodes}
          onToggle={handleToggle}
          onSelect={handleSelect}
        />
      </div>

      {/* Property inspector */}
      {selectedNodeData && (
        <div
          style={{
            flex: '0 0 auto',
            maxHeight: 200,
            overflow: 'auto',
            borderTop: '1px solid rgba(255,255,255,0.08)',
            padding: '8px 12px',
            background: 'rgba(0,0,0,0.2)',
          }}
        >
          <div style={{ color: '#9ca3af', fontSize: 10, textTransform: 'uppercase', letterSpacing: 0.5, marginBottom: 6 }}>
            Properties — {selectedNodeData.name}
          </div>
          {selectedNodeData.properties && Object.keys(selectedNodeData.properties).length > 0 ? (
            <table style={{ width: '100%', borderCollapse: 'collapse', fontFamily: 'ui-monospace, monospace', fontSize: 11 }}>
              <tbody>
                {Object.entries(selectedNodeData.properties).map(([key, value]) => (
                  <tr key={key}>
                    <td style={{ color: '#9ca3af', padding: '2px 8px 2px 0', verticalAlign: 'top', whiteSpace: 'nowrap' }}>{key}</td>
                    <td style={{ color: '#e5e7eb', wordBreak: 'break-all' }}>{typeof value === 'object' ? JSON.stringify(value) : String(value)}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          ) : (
            <div style={{ color: '#6b7280', fontSize: 11, fontStyle: 'italic' }}>
              No properties reported for this node.
            </div>
          )}
          <div style={{ color: '#6b7280', fontSize: 10, marginTop: 6, fontFamily: 'ui-monospace, monospace' }}>
            {selectedNodeData.path}
          </div>
        </div>
      )}

      {/* Actions */}
      <div
        style={{
          flex: '0 0 auto',
          borderTop: '1px solid rgba(255,255,255,0.08)',
          padding: '8px 12px',
          display: 'flex',
          gap: 6,
          flexWrap: 'wrap',
          background: 'rgba(0,0,0,0.15)',
        }}
      >
        <button
          type="button"
          onClick={fetchSceneInfo}
          style={btnStyle}
          title="Refresh from Godot MCP"
        >
          Refresh
        </button>
        <button type="button" onClick={handleExpandAll} style={btnStyle}>Expand all</button>
        <button type="button" onClick={handleCollapseAll} style={btnStyle}>Collapse all</button>
        <button type="button" onClick={handleExportJson} style={btnStyle}>Export JSON</button>
        <span style={{ marginLeft: 'auto', color: '#6b7280', fontSize: 10, alignSelf: 'center' }}>
          {lastFetch ? `updated ${new Date(lastFetch).toLocaleTimeString()}` : ''}
        </span>
      </div>
    </div>
  );
};

const btnStyle: React.CSSProperties = {
  background: 'rgba(255,255,255,0.06)',
  border: '1px solid rgba(255,255,255,0.12)',
  color: '#e5e7eb',
  padding: '4px 10px',
  borderRadius: 4,
  cursor: 'pointer',
  fontSize: 11,
  fontFamily: 'inherit',
};

// ---------------------------------------------------------------------------
// Demo fallback data (used when /api/godot/scene-tree is unavailable)
// ---------------------------------------------------------------------------

const DEMO_SCENE: GodotSceneInfo = {
  currentScene: 'res://scenes/main.tscn (demo)',
  rootNode: {
    name: 'Main',
    type: 'Node3D',
    path: '/root/Main',
    children: [
      {
        name: 'Camera3D',
        type: 'Camera3D',
        path: '/root/Main/Camera3D',
        children: [],
        properties: { fov: 75, near: 0.05, far: 4000 },
      },
      {
        name: 'Environment',
        type: 'WorldEnvironment',
        path: '/root/Main/Environment',
        children: [],
      },
      {
        name: 'Player',
        type: 'CharacterBody3D',
        path: '/root/Main/Player',
        children: [
          { name: 'Mesh', type: 'MeshInstance3D', path: '/root/Main/Player/Mesh', children: [] },
          { name: 'Collision', type: 'CollisionShape3D', path: '/root/Main/Player/Collision', children: [] },
        ],
      },
    ],
  },
  nodeCount: 6,
  fps: 60,
};
