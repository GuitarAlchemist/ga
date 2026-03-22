// IxqlGraph — React Flow graph rendering for IxQL pipelines

import React, { useMemo, useCallback } from 'react';
import ReactFlow, {
  Background,
  Controls,
  MiniMap,
  type Node,
  type Edge,
  type NodeMouseHandler,
  MarkerType,
} from 'reactflow';
import 'reactflow/dist/style.css';

import { nodeTypes } from './NodeTypes';
import type { IxqlGraph as IxqlGraphType, IxqlBinding } from './types';

interface IxqlGraphProps {
  graph: IxqlGraphType;
  highlightedChain: Set<string>;
  onNodeClick: (binding: IxqlBinding) => void;
  onNodeHover: (binding: IxqlBinding | null) => void;
}

const LAYOUT_X_SPACING = 300;
const LAYOUT_Y_SPACING = 120;

/**
 * Simple top-down layout: arrange nodes in topological order,
 * spreading fan_out children horizontally.
 */
function layoutNodes(graph: IxqlGraphType): Node[] {
  const { bindings, edges } = graph;

  // Build adjacency for topological sort
  const inDegree = new Map<string, number>();
  const children = new Map<string, string[]>();
  for (const b of bindings) {
    inDegree.set(b.id, 0);
    children.set(b.id, []);
  }
  for (const e of edges) {
    inDegree.set(e.target, (inDegree.get(e.target) || 0) + 1);
    children.get(e.source)?.push(e.target);
  }

  // Topological sort (BFS)
  const queue: string[] = [];
  for (const [id, deg] of inDegree) {
    if (deg === 0) queue.push(id);
  }

  const layers: string[][] = [];
  const visited = new Set<string>();
  const nodeLayer = new Map<string, number>();

  while (queue.length > 0) {
    const layerNodes: string[] = [...queue];
    queue.length = 0;
    const layer: string[] = [];

    for (const id of layerNodes) {
      if (visited.has(id)) continue;
      visited.add(id);
      layer.push(id);
      nodeLayer.set(id, layers.length);

      for (const child of children.get(id) || []) {
        const newDeg = (inDegree.get(child) || 1) - 1;
        inDegree.set(child, newDeg);
        if (newDeg === 0) queue.push(child);
      }
    }

    if (layer.length > 0) layers.push(layer);
  }

  // Add any unvisited nodes (disconnected) as final layer
  const unvisited = bindings.filter((b) => !visited.has(b.id));
  if (unvisited.length > 0) {
    layers.push(unvisited.map((b) => b.id));
  }

  // Position nodes
  const bindingMap = new Map(bindings.map((b) => [b.id, b]));
  const nodes: Node[] = [];

  for (let layerIdx = 0; layerIdx < layers.length; layerIdx++) {
    const layer = layers[layerIdx];
    const totalWidth = (layer.length - 1) * LAYOUT_X_SPACING;
    const startX = -totalWidth / 2;

    for (let nodeIdx = 0; nodeIdx < layer.length; nodeIdx++) {
      const id = layer[nodeIdx];
      const binding = bindingMap.get(id);
      if (!binding) continue;

      nodes.push({
        id,
        type: binding.kind,
        position: {
          x: startX + nodeIdx * LAYOUT_X_SPACING,
          y: layerIdx * LAYOUT_Y_SPACING,
        },
        data: { binding },
      });
    }
  }

  return nodes;
}

function buildEdges(graph: IxqlGraphType, highlightedChain: Set<string>): Edge[] {
  return graph.edges.map((e) => {
    const isHighlighted = highlightedChain.has(e.source) && highlightedChain.has(e.target);
    return {
      id: e.id,
      source: e.source,
      target: e.target,
      label: e.label,
      animated: isHighlighted,
      style: {
        stroke: isHighlighted
          ? '#58a6ff'
          : e.label === 'sequential'
            ? '#30363d'
            : '#555',
        strokeWidth: isHighlighted ? 2.5 : 1.5,
      },
      markerEnd: {
        type: MarkerType.ArrowClosed,
        color: isHighlighted ? '#58a6ff' : '#555',
      },
    };
  });
}

export const IxqlGraphView: React.FC<IxqlGraphProps> = ({
  graph,
  highlightedChain,
  onNodeClick,
  onNodeHover,
}) => {
  const nodes = useMemo(() => {
    const layouted = layoutNodes(graph);
    // Inject callbacks into node data
    return layouted.map((n) => ({
      ...n,
      data: { ...n.data, onNodeClick },
    }));
  }, [graph, onNodeClick]);

  const edges = useMemo(() => buildEdges(graph, highlightedChain), [graph, highlightedChain]);

  const handleNodeMouseEnter: NodeMouseHandler = useCallback(
    (_event, node) => {
      const binding = graph.bindings.find((b) => b.id === node.id);
      if (binding) onNodeHover(binding);
    },
    [graph, onNodeHover],
  );

  const handleNodeMouseLeave: NodeMouseHandler = useCallback(() => {
    onNodeHover(null);
  }, [onNodeHover]);

  return (
    <div style={{ width: '100%', height: '100%' }}>
      <ReactFlow
        nodes={nodes}
        edges={edges}
        nodeTypes={nodeTypes}
        onNodeMouseEnter={handleNodeMouseEnter}
        onNodeMouseLeave={handleNodeMouseLeave}
        fitView
        fitViewOptions={{ padding: 0.2 }}
        minZoom={0.1}
        maxZoom={2}
        attributionPosition="bottom-left"
      >
        <Background color="#21262d" gap={20} />
        <Controls
          style={{
            background: '#161b22',
            border: '1px solid #30363d',
            borderRadius: 6,
          }}
        />
        <MiniMap
          style={{
            background: '#0d1117',
            border: '1px solid #30363d',
          }}
          nodeColor={(node) => {
            const status = node.data?.binding?.lolliStatus;
            if (status === 'dead') return '#e05555';
            if (status === 'external') return '#888';
            return '#4cb050';
          }}
          maskColor="rgba(0,0,0,0.7)"
        />
      </ReactFlow>
    </div>
  );
};
