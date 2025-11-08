import React, { useState, useEffect, useRef } from 'react';
import * as d3 from 'd3';
import './GraphitiKnowledgeGraph.css';

interface GraphNode {
  id: string;
  name: string;
  type: 'user' | 'chord' | 'scale' | 'progression' | 'session';
  group: number;
  x?: number;
  y?: number;
  fx?: number | null;
  fy?: number | null;
}

interface GraphLink {
  source: string | GraphNode;
  target: string | GraphNode;
  type: 'learned' | 'practiced' | 'progression' | 'related';
  strength: number;
}

interface GraphData {
  nodes: GraphNode[];
  links: GraphLink[];
}

interface GraphitiKnowledgeGraphProps {
  userId?: string;
  width?: number;
  height?: number;
  onNodeClick?: (node: GraphNode) => void;
  onLinkClick?: (link: GraphLink) => void;
}

export const GraphitiKnowledgeGraph: React.FC<GraphitiKnowledgeGraphProps> = ({
  userId,
  width = 800,
  height = 600,
  onNodeClick,
  onLinkClick
}) => {
  const svgRef = useRef<SVGSVGElement>(null);
  const [graphData, setGraphData] = useState<GraphData>({ nodes: [], links: [] });
  const [loading, setLoading] = useState(false);
  const [selectedNode, setSelectedNode] = useState<GraphNode | null>(null);

  // Fetch graph data from Graphiti API
  const fetchGraphData = async () => {
    if (!userId) return;
    
    setLoading(true);
    try {
      // Search for user's learning data
      const response = await fetch('/api/graphiti/search', {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
        },
        body: JSON.stringify({
          query: `user ${userId} learning progress chords scales practice sessions`,
          search_type: 'hybrid',
          limit: 50,
          user_id: userId
        })
      });

      if (response.ok) {
        const searchResult = await response.json();
        const mockData = generateMockGraphData(userId, searchResult);
        setGraphData(mockData);
      }
    } catch (error) {
      console.error('Failed to fetch graph data:', error);
      // Generate mock data for demo
      const mockData = generateMockGraphData(userId);
      setGraphData(mockData);
    } finally {
      setLoading(false);
    }
  };

  // Generate mock graph data for demonstration
  const generateMockGraphData = (userId: string, searchResult?: any): GraphData => {
    const nodes: GraphNode[] = [
      { id: userId, name: 'You', type: 'user', group: 0 },
      { id: 'C', name: 'C Major', type: 'chord', group: 1 },
      { id: 'G', name: 'G Major', type: 'chord', group: 1 },
      { id: 'Am', name: 'A Minor', type: 'chord', group: 1 },
      { id: 'F', name: 'F Major', type: 'chord', group: 1 },
      { id: 'Cmaj7', name: 'C Major 7', type: 'chord', group: 2 },
      { id: 'C-scale', name: 'C Major Scale', type: 'scale', group: 3 },
      { id: 'session1', name: 'Practice Session 1', type: 'session', group: 4 },
      { id: 'session2', name: 'Practice Session 2', type: 'session', group: 4 },
      { id: 'prog1', name: 'I-V-vi-IV', type: 'progression', group: 5 }
    ];

    const links: GraphLink[] = [
      { source: userId, target: 'C', type: 'learned', strength: 0.9 },
      { source: userId, target: 'G', type: 'learned', strength: 0.8 },
      { source: userId, target: 'Am', type: 'learned', strength: 0.7 },
      { source: userId, target: 'F', type: 'practiced', strength: 0.6 },
      { source: 'C', target: 'G', type: 'progression', strength: 0.8 },
      { source: 'G', target: 'Am', type: 'progression', strength: 0.8 },
      { source: 'Am', target: 'F', type: 'progression', strength: 0.8 },
      { source: 'C', target: 'Cmaj7', type: 'related', strength: 0.9 },
      { source: 'C', target: 'C-scale', type: 'related', strength: 0.7 },
      { source: userId, target: 'session1', type: 'practiced', strength: 0.5 },
      { source: userId, target: 'session2', type: 'practiced', strength: 0.6 },
      { source: 'session1', target: 'C', type: 'practiced', strength: 0.8 },
      { source: 'session2', target: 'Cmaj7', type: 'practiced', strength: 0.7 },
      { source: 'prog1', target: 'C', type: 'progression', strength: 0.9 },
      { source: 'prog1', target: 'G', type: 'progression', strength: 0.9 },
      { source: 'prog1', target: 'Am', type: 'progression', strength: 0.9 },
      { source: 'prog1', target: 'F', type: 'progression', strength: 0.9 }
    ];

    return { nodes, links };
  };

  // Initialize D3 visualization
  useEffect(() => {
    if (!svgRef.current || graphData.nodes.length === 0) return;

    const svg = d3.select(svgRef.current);
    svg.selectAll('*').remove(); // Clear previous render

    // Create simulation
    const simulation = d3.forceSimulation<GraphNode>(graphData.nodes)
      .force('link', d3.forceLink<GraphNode, GraphLink>(graphData.links)
        .id(d => d.id)
        .strength(d => d.strength)
        .distance(100))
      .force('charge', d3.forceManyBody().strength(-300))
      .force('center', d3.forceCenter(width / 2, height / 2))
      .force('collision', d3.forceCollide().radius(30));

    // Create links
    const link = svg.append('g')
      .attr('class', 'links')
      .selectAll('line')
      .data(graphData.links)
      .enter().append('line')
      .attr('class', d => `link link-${d.type}`)
      .attr('stroke-width', d => Math.sqrt(d.strength * 5))
      .on('click', (event, d) => {
        event.stopPropagation();
        onLinkClick?.(d);
      });

    // Create nodes
    const node = svg.append('g')
      .attr('class', 'nodes')
      .selectAll('g')
      .data(graphData.nodes)
      .enter().append('g')
      .attr('class', 'node')
      .call(d3.drag<SVGGElement, GraphNode>()
        .on('start', (event, d) => {
          if (!event.active) simulation.alphaTarget(0.3).restart();
          d.fx = d.x;
          d.fy = d.y;
        })
        .on('drag', (event, d) => {
          d.fx = event.x;
          d.fy = event.y;
        })
        .on('end', (event, d) => {
          if (!event.active) simulation.alphaTarget(0);
          d.fx = null;
          d.fy = null;
        }));

    // Add circles to nodes
    node.append('circle')
      .attr('r', d => d.type === 'user' ? 20 : 15)
      .attr('class', d => `node-${d.type}`)
      .on('click', (event, d) => {
        event.stopPropagation();
        setSelectedNode(d);
        onNodeClick?.(d);
      });

    // Add labels to nodes
    node.append('text')
      .text(d => d.name)
      .attr('x', 0)
      .attr('y', d => d.type === 'user' ? 25 : 20)
      .attr('text-anchor', 'middle')
      .attr('class', 'node-label');

    // Update positions on simulation tick
    simulation.on('tick', () => {
      link
        .attr('x1', d => (d.source as GraphNode).x!)
        .attr('y1', d => (d.source as GraphNode).y!)
        .attr('x2', d => (d.target as GraphNode).x!)
        .attr('y2', d => (d.target as GraphNode).y!);

      node
        .attr('transform', d => `translate(${d.x},${d.y})`);
    });

    return () => {
      simulation.stop();
    };
  }, [graphData, width, height, onNodeClick, onLinkClick]);

  // Fetch data when userId changes
  useEffect(() => {
    fetchGraphData();
  }, [userId]);

  return (
    <div className="graphiti-knowledge-graph">
      <div className="graph-controls">
        <button onClick={fetchGraphData} disabled={loading}>
          {loading ? 'Loading...' : 'Refresh Graph'}
        </button>
        {selectedNode && (
          <div className="selected-node-info">
            <h4>Selected: {selectedNode.name}</h4>
            <p>Type: {selectedNode.type}</p>
          </div>
        )}
      </div>
      
      <svg
        ref={svgRef}
        width={width}
        height={height}
        className="knowledge-graph-svg"
        onClick={() => setSelectedNode(null)}
      />
      
      <div className="graph-legend">
        <div className="legend-item">
          <div className="legend-color node-user"></div>
          <span>User</span>
        </div>
        <div className="legend-item">
          <div className="legend-color node-chord"></div>
          <span>Chord</span>
        </div>
        <div className="legend-item">
          <div className="legend-color node-scale"></div>
          <span>Scale</span>
        </div>
        <div className="legend-item">
          <div className="legend-color node-session"></div>
          <span>Session</span>
        </div>
        <div className="legend-item">
          <div className="legend-color node-progression"></div>
          <span>Progression</span>
        </div>
      </div>
    </div>
  );
};
