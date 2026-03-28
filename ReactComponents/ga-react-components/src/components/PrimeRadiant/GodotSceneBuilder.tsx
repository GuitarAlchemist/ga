// src/components/PrimeRadiant/GodotSceneBuilder.tsx
// Scene builder panel — create and manipulate Godot scenes from the Prime Radiant UI.
// Connects to the running Godot editor via MCP WebSocket (ports 6505-6509).

import React, { useCallback, useEffect, useRef, useState } from 'react';
import { godotMcp, type ConnectionStatus } from './GodotMcpClient';

// ---------------------------------------------------------------------------
// Types
// ---------------------------------------------------------------------------

interface SceneTreeNode {
  name: string;
  type: string;
  path: string;
  children?: SceneTreeNode[];
}

interface CommandLogEntry {
  id: number;
  method: string;
  status: 'pending' | 'success' | 'error';
  result?: string;
  timestamp: number;
}

// Quick-action templates for common scene creation patterns
const QUICK_ACTIONS = [
  { label: 'Empty 3D Scene', icon: '🎬', method: 'create_scene', params: { scene_path: 'res://scenes/new_scene.tscn', root_type: 'Node3D' } },
  { label: 'Add Camera', icon: '📷', method: 'setup_camera_3d', params: { name: 'Camera3D', parent_path: '.' } },
  { label: 'Add Light', icon: '💡', method: 'setup_lighting', params: { type: 'outdoor' } },
  { label: 'Add Mesh (Sphere)', icon: '🔮', method: 'add_mesh_instance', params: { name: 'Sphere', mesh_type: 'sphere', parent_path: '.' } },
  { label: 'Add Mesh (Box)', icon: '📦', method: 'add_mesh_instance', params: { name: 'Box', mesh_type: 'box', parent_path: '.' } },
  { label: 'Add Node3D', icon: '📍', method: 'add_node', params: { name: 'Node3D', type: 'Node3D', parent_path: '.' } },
  { label: 'Add CharacterBody3D', icon: '🏃', method: 'add_node', params: { name: 'Player', type: 'CharacterBody3D', parent_path: '.' } },
  { label: 'Add RigidBody3D', icon: '🎱', method: 'add_node', params: { name: 'RigidBody', type: 'RigidBody3D', parent_path: '.' } },
  { label: 'Save Scene', icon: '💾', method: 'save_scene', params: {} },
] as const;

const NODE_TYPES = [
  'Node3D', 'Node2D', 'Control',
  'MeshInstance3D', 'Camera3D', 'DirectionalLight3D', 'OmniLight3D', 'SpotLight3D',
  'CharacterBody3D', 'RigidBody3D', 'StaticBody3D', 'Area3D',
  'CollisionShape3D', 'Timer', 'AudioStreamPlayer3D',
  'NavigationRegion3D', 'NavigationAgent3D',
  'GPUParticles3D', 'CPUParticles3D',
  'AnimationPlayer', 'AnimationTree',
  'Label3D', 'Sprite3D', 'SubViewport',
];

// ---------------------------------------------------------------------------
// Component
// ---------------------------------------------------------------------------

export const GodotSceneBuilder: React.FC = () => {
  const [status, setStatus] = useState<ConnectionStatus>('disconnected');
  const [sceneTree, setSceneTree] = useState<SceneTreeNode | null>(null);
  const [selectedNode, setSelectedNode] = useState<string | null>(null);
  const [log, setLog] = useState<CommandLogEntry[]>([]);
  const [commandInput, setCommandInput] = useState('');
  const [paramsInput, setParamsInput] = useState('{}');
  const logEndRef = useRef<HTMLDivElement>(null);
  const logIdRef = useRef(0);

  // --- Add Node dialog state ---
  const [showAddNode, setShowAddNode] = useState(false);
  const [newNodeName, setNewNodeName] = useState('');
  const [newNodeType, setNewNodeType] = useState('Node3D');

  // --- Create Scene dialog state ---
  const [showCreateScene, setShowCreateScene] = useState(false);
  const [newScenePath, setNewScenePath] = useState('res://scenes/');
  const [newSceneRoot, setNewSceneRoot] = useState('Node3D');

  // --- Connection lifecycle ---
  useEffect(() => {
    const unsub = godotMcp.onStatus(setStatus);
    godotMcp.connect();
    return () => { unsub(); };
  }, []);

  // Auto-scroll log
  useEffect(() => {
    logEndRef.current?.scrollIntoView({ behavior: 'smooth' });
  }, [log]);

  // --- Execute MCP command with logging ---
  const execute = useCallback(async (method: string, params: Record<string, unknown>) => {
    const id = ++logIdRef.current;
    setLog(prev => [...prev.slice(-49), { id, method, status: 'pending', timestamp: Date.now() }]);

    try {
      const result = await godotMcp.call(method, params);
      const resultStr = typeof result === 'string' ? result : JSON.stringify(result, null, 2);
      setLog(prev => prev.map(e => e.id === id ? { ...e, status: 'success', result: resultStr } : e));
      return result;
    } catch (err) {
      const errMsg = err instanceof Error ? err.message : String(err);
      setLog(prev => prev.map(e => e.id === id ? { ...e, status: 'error', result: errMsg } : e));
      throw err;
    }
  }, []);

  // --- Refresh scene tree ---
  const refreshTree = useCallback(async () => {
    try {
      const tree = await execute('get_scene_tree', {});
      if (tree && typeof tree === 'object') {
        setSceneTree(tree as SceneTreeNode);
      }
    } catch {
      // logged by execute
    }
  }, [execute]);

  // Auto-fetch tree on connect
  useEffect(() => {
    if (status === 'connected') {
      refreshTree();
    }
  }, [status, refreshTree]);

  // --- Quick action handler ---
  const handleQuickAction = useCallback(async (action: typeof QUICK_ACTIONS[number]) => {
    await execute(action.method, { ...action.params });
    // Refresh tree after mutations
    if (action.method !== 'save_scene') {
      setTimeout(refreshTree, 300);
    }
  }, [execute, refreshTree]);

  // --- Raw command handler ---
  const handleRawCommand = useCallback(async () => {
    if (!commandInput.trim()) return;
    try {
      const params = JSON.parse(paramsInput);
      await execute(commandInput.trim(), params);
      setTimeout(refreshTree, 300);
    } catch (err) {
      if (err instanceof SyntaxError) {
        const id = ++logIdRef.current;
        setLog(prev => [...prev.slice(-49), { id, method: 'parse_error', status: 'error', result: 'Invalid JSON params', timestamp: Date.now() }]);
      }
    }
  }, [commandInput, paramsInput, execute, refreshTree]);

  // --- Add node handler ---
  const handleAddNode = useCallback(async () => {
    if (!newNodeName.trim()) return;
    const parent = selectedNode || '.';
    await execute('add_node', { name: newNodeName.trim(), type: newNodeType, parent_path: parent });
    setShowAddNode(false);
    setNewNodeName('');
    setTimeout(refreshTree, 300);
  }, [newNodeName, newNodeType, selectedNode, execute, refreshTree]);

  // --- Create scene handler ---
  const handleCreateScene = useCallback(async () => {
    if (!newScenePath.trim()) return;
    await execute('create_scene', { scene_path: newScenePath.trim(), root_type: newSceneRoot });
    setShowCreateScene(false);
    setNewScenePath('res://scenes/');
    setTimeout(refreshTree, 500);
  }, [newScenePath, newSceneRoot, execute, refreshTree]);

  // --- Delete node ---
  const handleDeleteNode = useCallback(async () => {
    if (!selectedNode || selectedNode === '.') return;
    await execute('delete_node', { node_path: selectedNode });
    setSelectedNode(null);
    setTimeout(refreshTree, 300);
  }, [selectedNode, execute, refreshTree]);

  // --- Render scene tree recursively ---
  const renderTreeNode = (node: SceneTreeNode, depth: number = 0): React.ReactNode => (
    <div key={node.path}>
      <button
        className={`gsb__tree-node ${selectedNode === node.path ? 'gsb__tree-node--selected' : ''}`}
        style={{ paddingLeft: `${8 + depth * 14}px` }}
        onClick={() => setSelectedNode(node.path === selectedNode ? null : node.path)}
        title={`${node.type} — ${node.path}`}
      >
        <span className="gsb__tree-icon">{getNodeIcon(node.type)}</span>
        <span className="gsb__tree-name">{node.name}</span>
        <span className="gsb__tree-type">{node.type}</span>
      </button>
      {node.children?.map(child => renderTreeNode(child, depth + 1))}
    </div>
  );

  // ------------------------------------------------------------------
  // Render
  // ------------------------------------------------------------------
  return (
    <div className="gsb">
      {/* Connection status bar */}
      <div className={`gsb__status gsb__status--${status}`}>
        <span className="gsb__status-dot" />
        <span className="gsb__status-text">
          {status === 'connected' ? `Godot MCP ${godotMcp.connectedEndpoint?.includes('/ws/') ? '(proxy)' : `:${godotMcp.connectedPort}`}` :
           status === 'connecting' ? 'Connecting...' :
           status === 'error' ? 'Godot not found' : 'Disconnected'}
        </span>
        {status !== 'connected' && (
          <button className="gsb__reconnect-btn" onClick={() => godotMcp.connect()}>Retry</button>
        )}
        {status === 'connected' && (
          <button className="gsb__refresh-btn" onClick={refreshTree} title="Refresh tree">
            <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2"><path d="M1 4v6h6"/><path d="M3.51 15a9 9 0 1 0 2.13-9.36L1 10"/></svg>
          </button>
        )}
      </div>

      {status === 'connected' && (
        <>
          {/* Quick actions */}
          <div className="gsb__section">
            <div className="gsb__section-header">Quick Actions</div>
            <div className="gsb__quick-actions">
              {QUICK_ACTIONS.map((action, i) => (
                <button
                  key={i}
                  className="gsb__quick-btn"
                  onClick={() => handleQuickAction(action)}
                  title={`${action.method}(${JSON.stringify(action.params)})`}
                >
                  <span>{action.icon}</span>
                  <span>{action.label}</span>
                </button>
              ))}
            </div>
          </div>

          {/* Scene tree */}
          <div className="gsb__section">
            <div className="gsb__section-header">
              Scene Tree
              <div className="gsb__section-actions">
                <button className="gsb__mini-btn" onClick={() => setShowCreateScene(true)} title="New Scene">+ Scene</button>
                <button className="gsb__mini-btn" onClick={() => setShowAddNode(true)} title="Add Node">+ Node</button>
                {selectedNode && selectedNode !== '.' && (
                  <button className="gsb__mini-btn gsb__mini-btn--danger" onClick={handleDeleteNode} title="Delete selected">Del</button>
                )}
              </div>
            </div>
            <div className="gsb__tree">
              {sceneTree ? renderTreeNode(sceneTree) : (
                <div className="gsb__tree-empty">No scene loaded</div>
              )}
            </div>
          </div>

          {/* Add Node dialog */}
          {showAddNode && (
            <div className="gsb__dialog">
              <div className="gsb__dialog-title">Add Node{selectedNode ? ` to ${selectedNode}` : ''}</div>
              <input
                className="gsb__input"
                placeholder="Node name"
                value={newNodeName}
                onChange={e => setNewNodeName(e.target.value)}
                onKeyDown={e => e.key === 'Enter' && handleAddNode()}
                autoFocus
              />
              <select className="gsb__select" value={newNodeType} onChange={e => setNewNodeType(e.target.value)}>
                {NODE_TYPES.map(t => <option key={t} value={t}>{t}</option>)}
              </select>
              <div className="gsb__dialog-actions">
                <button className="gsb__btn" onClick={handleAddNode}>Add</button>
                <button className="gsb__btn gsb__btn--ghost" onClick={() => setShowAddNode(false)}>Cancel</button>
              </div>
            </div>
          )}

          {/* Create Scene dialog */}
          {showCreateScene && (
            <div className="gsb__dialog">
              <div className="gsb__dialog-title">Create Scene</div>
              <input
                className="gsb__input"
                placeholder="res://scenes/my_scene.tscn"
                value={newScenePath}
                onChange={e => setNewScenePath(e.target.value)}
                onKeyDown={e => e.key === 'Enter' && handleCreateScene()}
                autoFocus
              />
              <select className="gsb__select" value={newSceneRoot} onChange={e => setNewSceneRoot(e.target.value)}>
                <option value="Node3D">Node3D</option>
                <option value="Node2D">Node2D</option>
                <option value="Control">Control</option>
                <option value="Node">Node</option>
              </select>
              <div className="gsb__dialog-actions">
                <button className="gsb__btn" onClick={handleCreateScene}>Create</button>
                <button className="gsb__btn gsb__btn--ghost" onClick={() => setShowCreateScene(false)}>Cancel</button>
              </div>
            </div>
          )}

          {/* Raw command input */}
          <div className="gsb__section">
            <div className="gsb__section-header">Command</div>
            <div className="gsb__command-row">
              <input
                className="gsb__input gsb__input--method"
                placeholder="method (e.g. add_node)"
                value={commandInput}
                onChange={e => setCommandInput(e.target.value)}
                onKeyDown={e => e.key === 'Enter' && handleRawCommand()}
              />
            </div>
            <textarea
              className="gsb__textarea"
              placeholder='{"name": "Foo", "type": "Node3D"}'
              value={paramsInput}
              onChange={e => setParamsInput(e.target.value)}
              rows={3}
            />
            <button className="gsb__btn gsb__btn--exec" onClick={handleRawCommand}>Execute</button>
          </div>

          {/* Command log */}
          <div className="gsb__section gsb__section--log">
            <div className="gsb__section-header">Log</div>
            <div className="gsb__log">
              {log.length === 0 && <div className="gsb__log-empty">No commands yet</div>}
              {log.map(entry => (
                <div key={entry.id} className={`gsb__log-entry gsb__log-entry--${entry.status}`}>
                  <span className="gsb__log-method">{entry.method}</span>
                  <span className={`gsb__log-status gsb__log-status--${entry.status}`}>
                    {entry.status === 'pending' ? '...' : entry.status === 'success' ? 'OK' : 'ERR'}
                  </span>
                  {entry.result && (
                    <pre className="gsb__log-result">{entry.result.length > 200 ? entry.result.slice(0, 200) + '...' : entry.result}</pre>
                  )}
                </div>
              ))}
              <div ref={logEndRef} />
            </div>
          </div>
        </>
      )}

      {/* Disconnected state */}
      {(status === 'disconnected' || status === 'error') && (
        <div className="gsb__disconnected">
          <p>Open the Godot editor with the MCP addon enabled to connect.</p>
          <p className="gsb__hint">The Godot MCP Pro addon listens on ports 6505-6509.</p>
        </div>
      )}
    </div>
  );
};

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------

function getNodeIcon(type: string): string {
  if (type.includes('Camera')) return '📷';
  if (type.includes('Light')) return '💡';
  if (type.includes('Mesh')) return '🔮';
  if (type.includes('Body')) return '🎱';
  if (type.includes('Collision')) return '🛡';
  if (type.includes('Particle')) return '✨';
  if (type.includes('Audio')) return '🔊';
  if (type.includes('Animation')) return '🎬';
  if (type.includes('Navigation')) return '🧭';
  if (type.includes('Timer')) return '⏱';
  if (type.includes('Label') || type.includes('Sprite')) return '🏷';
  if (type === 'Node3D') return '📍';
  if (type === 'Node2D') return '📐';
  if (type === 'Control') return '🖼';
  return '⚪';
}
