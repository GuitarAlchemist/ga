// src/components/PrimeRadiant/IxqlVizPanel.tsx
// D3 visualization panel driven by IXQL CREATE VIZ.
// Phase 2: force-graph, bar, sparkline, timeline.
// Uses SVG rendering (no WebGL — keeps context count low per spec).

import React, { useEffect, useState, useCallback, useRef, useMemo } from 'react';
import * as d3 from 'd3';

import type { VizSpec } from './IxqlWidgetSpec';
import { resolve, resolveField } from './DataFetcher';
import type { GraphContext } from './DataFetcher';
import { executePipeline } from './IxqlPipeEngine';
import { useSignals, usePublish } from './DashboardSignalBus';
import {
  generateVizProof, publishRenderProof, cognitiveChecksum, dataFingerprint,
  classifyDivergences,
} from './RenderProof';
import { IxqlTruthLatticePanel } from './IxqlTruthLatticePanel';

// ---------------------------------------------------------------------------
// Types
// ---------------------------------------------------------------------------

interface GraphNode {
  id: string;
  label: string;
  color: string;
  size: number;
  x?: number;
  y?: number;
  fx?: number | null;
  fy?: number | null;
}

interface GraphLink {
  source: string | GraphNode;
  target: string | GraphNode;
}

// ---------------------------------------------------------------------------
// Hexavalent colors for node coloring
// ---------------------------------------------------------------------------

const HEXAVALENT_NODE_COLORS: Record<string, string> = {
  T: '#22c55e', P: '#a3e635', U: '#6b7280',
  D: '#f97316', F: '#ef4444', C: '#d946ef',
  TRUE: '#22c55e', PROBABLE: '#a3e635', UNKNOWN: '#6b7280',
  DOUBTFUL: '#f97316', FALSE: '#ef4444', CONTRADICTORY: '#d946ef',
};

const DEFAULT_NODE_COLOR = '#33CC66';
const DEFAULT_NODE_SIZE = 6;

// ---------------------------------------------------------------------------
// Force Graph Renderer
// ---------------------------------------------------------------------------

const ForceGraphViz: React.FC<{
  nodes: GraphNode[];
  links: GraphLink[];
  width: number;
  height: number;
  onNodeClick?: (node: GraphNode) => void;
}> = ({ nodes, links, width, height, onNodeClick }) => {
  const svgRef = useRef<SVGSVGElement>(null);
  const simulationRef = useRef<d3.Simulation<GraphNode, GraphLink> | null>(null);

  useEffect(() => {
    if (!svgRef.current || nodes.length === 0) return;

    const svg = d3.select(svgRef.current);
    svg.selectAll('*').remove();

    const g = svg.append('g');

    // Zoom
    const zoom = d3.zoom<SVGSVGElement, unknown>()
      .scaleExtent([0.3, 5])
      .on('zoom', (event) => g.attr('transform', event.transform));
    svg.call(zoom);

    // Simulation
    const sim = d3.forceSimulation<GraphNode>(nodes)
      .force('link', d3.forceLink<GraphNode, GraphLink>(links).id(d => d.id).distance(60))
      .force('charge', d3.forceManyBody().strength(-120))
      .force('center', d3.forceCenter(width / 2, height / 2))
      .force('collision', d3.forceCollide().radius(d => (d as GraphNode).size + 4));

    simulationRef.current = sim;

    // Links
    const link = g.append('g')
      .selectAll('line')
      .data(links)
      .join('line')
      .attr('stroke', '#30363d')
      .attr('stroke-width', 1.5)
      .attr('stroke-opacity', 0.6);

    // Nodes
    const node = g.append('g')
      .selectAll('circle')
      .data(nodes)
      .join('circle')
      .attr('r', d => d.size)
      .attr('fill', d => d.color)
      .attr('stroke', '#0d1117')
      .attr('stroke-width', 1.5)
      .style('cursor', 'pointer')
      .on('click', (_event, d) => { if (onNodeClick) onNodeClick(d); });

    // Drag
    const drag = d3.drag<SVGCircleElement, GraphNode>()
      .on('start', (event, d) => {
        if (!event.active) sim.alphaTarget(0.3).restart();
        d.fx = d.x;
        d.fy = d.y;
      })
      .on('drag', (event, d) => {
        d.fx = event.x;
        d.fy = event.y;
      })
      .on('end', (event, d) => {
        if (!event.active) sim.alphaTarget(0);
        d.fx = null;
        d.fy = null;
      });
    node.call(drag);

    // Labels
    const label = g.append('g')
      .selectAll('text')
      .data(nodes)
      .join('text')
      .text(d => d.label)
      .attr('font-size', 10)
      .attr('font-family', "'JetBrains Mono', monospace")
      .attr('fill', '#8b949e')
      .attr('dx', d => d.size + 4)
      .attr('dy', 3);

    // Tick
    sim.on('tick', () => {
      link
        .attr('x1', d => (d.source as GraphNode).x ?? 0)
        .attr('y1', d => (d.source as GraphNode).y ?? 0)
        .attr('x2', d => (d.target as GraphNode).x ?? 0)
        .attr('y2', d => (d.target as GraphNode).y ?? 0);
      node
        .attr('cx', d => d.x ?? 0)
        .attr('cy', d => d.y ?? 0);
      label
        .attr('x', d => d.x ?? 0)
        .attr('y', d => d.y ?? 0);
    });

    return () => { sim.stop(); };
  }, [nodes, links, width, height, onNodeClick]);

  return (
    <svg
      ref={svgRef}
      width={width}
      height={height}
      style={{ background: 'rgba(13, 17, 23, 0.95)' }}
    />
  );
};

// ---------------------------------------------------------------------------
// Bar Chart Renderer
// ---------------------------------------------------------------------------

const BarChartViz: React.FC<{
  data: Record<string, unknown>[];
  labelField: string;
  valueField: string;
  colorField: string | null;
  width: number;
  height: number;
}> = ({ data, labelField, valueField, colorField, width, height }) => {
  const svgRef = useRef<SVGSVGElement>(null);

  useEffect(() => {
    if (!svgRef.current || data.length === 0) return;

    const svg = d3.select(svgRef.current);
    svg.selectAll('*').remove();

    const margin = { top: 16, right: 16, bottom: 40, left: 50 };
    const w = width - margin.left - margin.right;
    const h = height - margin.top - margin.bottom;
    const g = svg.append('g').attr('transform', `translate(${margin.left},${margin.top})`);

    const labels = data.map(d => String(resolveField(d, labelField) ?? ''));
    const values = data.map(d => Number(resolveField(d, valueField) ?? 0));

    const x = d3.scaleBand().domain(labels).range([0, w]).padding(0.3);
    const y = d3.scaleLinear().domain([0, d3.max(values) ?? 1]).nice().range([h, 0]);

    // Bars
    g.selectAll('rect')
      .data(data)
      .join('rect')
      .attr('x', (_d, i) => x(labels[i]) ?? 0)
      .attr('y', (_d, i) => y(values[i]))
      .attr('width', x.bandwidth())
      .attr('height', (_d, i) => h - y(values[i]))
      .attr('rx', 3)
      .attr('fill', (d) => {
        if (colorField) {
          const cv = String(resolveField(d, colorField) ?? '').toUpperCase();
          return HEXAVALENT_NODE_COLORS[cv] ?? DEFAULT_NODE_COLOR;
        }
        return '#ffd700';
      });

    // X axis
    g.append('g')
      .attr('transform', `translate(0,${h})`)
      .call(d3.axisBottom(x))
      .selectAll('text')
      .attr('fill', '#8b949e')
      .attr('font-size', 9)
      .attr('transform', 'rotate(-30)')
      .style('text-anchor', 'end');
    g.selectAll('.domain, .tick line').attr('stroke', '#30363d');

    // Y axis
    g.append('g')
      .call(d3.axisLeft(y).ticks(5))
      .selectAll('text').attr('fill', '#8b949e').attr('font-size', 9);
    g.selectAll('.domain, .tick line').attr('stroke', '#30363d');
  }, [data, labelField, valueField, colorField, width, height]);

  return (
    <svg ref={svgRef} width={width} height={height}
      style={{ background: 'rgba(13, 17, 23, 0.95)' }} />
  );
};

// ---------------------------------------------------------------------------
// Sparkline Renderer
// ---------------------------------------------------------------------------

const SparklineViz: React.FC<{
  data: Record<string, unknown>[];
  valueField: string;
  labelField: string | null;
  width: number;
  height: number;
}> = ({ data, valueField, labelField, width, height }) => {
  const svgRef = useRef<SVGSVGElement>(null);

  useEffect(() => {
    if (!svgRef.current || data.length === 0) return;

    const svg = d3.select(svgRef.current);
    svg.selectAll('*').remove();

    const pad = 16;
    const values = data.map(d => Number(resolveField(d, valueField) ?? 0));
    const labels = labelField ? data.map(d => String(resolveField(d, labelField) ?? '')) : null;

    const x = d3.scaleLinear().domain([0, values.length - 1]).range([pad, width - pad]);
    const y = d3.scaleLinear().domain([d3.min(values) ?? 0, d3.max(values) ?? 1]).nice().range([height - pad, pad]);

    const line = d3.line<number>()
      .x((_d, i) => x(i))
      .y(d => y(d))
      .curve(d3.curveMonotoneX);

    svg.append('path')
      .datum(values)
      .attr('fill', 'none')
      .attr('stroke', '#ffd700')
      .attr('stroke-width', 2)
      .attr('d', line);

    svg.selectAll('circle')
      .data(values)
      .join('circle')
      .attr('cx', (_d, i) => x(i))
      .attr('cy', d => y(d))
      .attr('r', 3)
      .attr('fill', '#ffd700');

    if (labels) {
      svg.selectAll('.label')
        .data(labels)
        .join('text')
        .attr('x', (_d, i) => x(i))
        .attr('y', height - 2)
        .attr('text-anchor', 'middle')
        .attr('fill', '#6b7280')
        .attr('font-size', 8)
        .text(d => d);
    }
  }, [data, valueField, labelField, width, height]);

  return (
    <svg ref={svgRef} width={width} height={height}
      style={{ background: 'rgba(13, 17, 23, 0.95)' }} />
  );
};

// ---------------------------------------------------------------------------
// Main component
// ---------------------------------------------------------------------------

export interface IxqlVizPanelProps {
  spec: VizSpec;
  graphContext?: GraphContext;
}

export const IxqlVizPanel: React.FC<IxqlVizPanelProps> = ({ spec, graphContext }) => {
  const [data, setData] = useState<Record<string, unknown>[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [renderDivergences, setRenderDivergences] = useState<string[]>([]);
  const [checksum, setChecksum] = useState('');
  const containerRef = useRef<HTMLDivElement>(null);
  const [dimensions, setDimensions] = useState({ width: 600, height: 400 });
  const publishSignal = usePublish(spec.id);
  const subscribedSignals = useSignals(spec.subscribe);

  // Measure container
  useEffect(() => {
    if (!containerRef.current) return;
    const obs = new ResizeObserver((entries) => {
      const entry = entries[0];
      if (entry) {
        setDimensions({
          width: Math.max(200, entry.contentRect.width),
          height: Math.max(150, entry.contentRect.height),
        });
      }
    });
    obs.observe(containerRef.current);
    return () => obs.disconnect();
  }, []);

  // Fetch data
  const fetchData = useCallback(async () => {
    try {
      const raw = await resolve(spec.binding.source, spec.binding.wherePredicates, graphContext);
      let rows = raw as Record<string, unknown>[];
      if (spec.pipeline) {
        rows = executePipeline(rows, spec.pipeline.steps);
      }
      setData(rows);
      setError(null);

      // Generate viz render proof
      const labelField = spec.labelField ?? 'name';
      const labels = rows.map(r => String(r[labelField] ?? ''));
      const uniqueLabels = new Set(labels).size;
      const colorField = spec.colorField;
      let colorCoverage = 1.0;
      if (colorField) {
        const mapped = rows.filter(r => {
          const v = String(r[colorField] ?? '').toUpperCase();
          return v in HEXAVALENT_NODE_COLORS;
        });
        colorCoverage = rows.length > 0 ? mapped.length / rows.length : 1.0;
      }

      const proof = generateVizProof(
        spec.id,
        spec.kind,
        rows,
        spec.kind === 'force-graph' ? rows.length : 0,
        0, // link count computed in graphData memo
        colorCoverage,
        uniqueLabels,
        null, // simulation alpha measured later
        0,
      );
      setRenderDivergences(proof.divergences);

      const specStr = JSON.stringify({ source: spec.binding.source, kind: spec.kind });
      const fp = dataFingerprint(rows);
      const scene = `kind:${spec.kind};pts:${rows.length};labels:${uniqueLabels}`;
      setChecksum(cognitiveChecksum(specStr, fp, scene));

      publishRenderProof(proof);
    } catch (err) {
      setError(String(err));
    } finally {
      setLoading(false);
    }
  }, [spec.binding.source, spec.binding.wherePredicates, spec.pipeline, spec.id, spec.kind, spec.labelField, spec.colorField, graphContext]);

  useEffect(() => {
    fetchData();
    if (spec.refresh && spec.refresh > 0) {
      const interval = setInterval(fetchData, spec.refresh);
      return () => clearInterval(interval);
    }
  }, [fetchData, spec.refresh]);

  // Re-fetch when subscribed signals change (timestamp-gated to prevent re-fetch loops)
  const lastSubTimestamp = useRef(0);
  useEffect(() => {
    if (subscribedSignals.size === 0) return;
    let latestTs = 0;
    for (const [, sig] of subscribedSignals) {
      if (sig.timestamp > latestTs) latestTs = sig.timestamp;
    }
    if (latestTs > lastSubTimestamp.current) {
      lastSubTimestamp.current = latestTs;
      fetchData();
    }
  }, [subscribedSignals, fetchData]);

  // Build graph nodes/links for force-graph
  const graphData = useMemo(() => {
    if (spec.kind !== 'force-graph') return { nodes: [] as GraphNode[], links: [] as GraphLink[] };

    const labelKey = spec.labelField ?? spec.nodeField ?? 'name';
    const colorKey = spec.colorField;
    const sizeKey = spec.sizeField;

    const nodes: GraphNode[] = data.map((row, i) => {
      const id = String(resolveField(row, 'id') ?? resolveField(row, 'name') ?? i);
      const label = String(resolveField(row, labelKey) ?? id);
      const colorVal = colorKey ? String(resolveField(row, colorKey) ?? '').toUpperCase() : '';
      const color = HEXAVALENT_NODE_COLORS[colorVal] ?? DEFAULT_NODE_COLOR;
      const size = sizeKey ? Math.max(3, Math.min(20, Number(resolveField(row, sizeKey) ?? DEFAULT_NODE_SIZE) * 10)) : DEFAULT_NODE_SIZE;
      return { id, label, color, size };
    });

    // Build links from edges if specified, otherwise from row relationships
    const links: GraphLink[] = [];
    if (spec.edgeFrom && spec.edgeTo) {
      for (const row of data) {
        const from = String(resolveField(row, spec.edgeFrom!) ?? '');
        const to = String(resolveField(row, spec.edgeTo!) ?? '');
        if (from && to) links.push({ source: from, target: to });
      }
    }

    return { nodes, links };
  }, [data, spec.kind, spec.labelField, spec.nodeField, spec.colorField, spec.sizeField, spec.edgeFrom, spec.edgeTo]);

  const handleNodeClick = useCallback((node: GraphNode) => {
    if (spec.publish) {
      publishSignal(spec.publish.as, node);
    }
  }, [spec.publish, publishSignal]);

  if (loading) {
    return (
      <div className="ixql-viz-panel ixql-viz-panel--loading">
        <div className="ixql-viz-panel__header">
          <span className="ixql-viz-panel__title">{spec.id}</span>
        </div>
        <div style={{ padding: 24, color: '#8b949e', fontSize: 12 }}>Loading...</div>
      </div>
    );
  }

  if (error) {
    return (
      <div className="ixql-viz-panel ixql-viz-panel--error">
        <div className="ixql-viz-panel__header">
          <span className="ixql-viz-panel__title">{spec.id}</span>
        </div>
        <div style={{ padding: 24, color: '#f85149', fontSize: 12 }}>{error}</div>
      </div>
    );
  }

  const labelField = spec.labelField ?? 'name';
  const sizeField = spec.sizeField ?? 'value';

  return (
    <div className="ixql-viz-panel" ref={containerRef}>
      <div className="ixql-viz-panel__header">
        <span className="ixql-viz-panel__title">{spec.id}</span>
        <span style={{ fontSize: 10, color: '#6b7280', marginLeft: 'auto' }}>
          {spec.kind} | {data.length} items
        </span>
        {renderDivergences.length > 0 && (
          <span
            style={{
              background: classifyDivergences(renderDivergences) === 'critical' ? '#ef444422' : '#f9731622',
              border: `1px solid ${classifyDivergences(renderDivergences) === 'critical' ? '#ef444444' : '#f9731644'}`,
              borderRadius: 4,
              color: classifyDivergences(renderDivergences) === 'critical' ? '#ef4444' : '#f97316',
              fontSize: 9,
              fontWeight: 'bold',
              padding: '1px 6px',
            }}
            title={renderDivergences.join('\n') + '\nChecksum: ' + checksum}
          >
            {renderDivergences.length} div.
          </span>
        )}
      </div>
      <div className="ixql-viz-panel__body" style={{ flex: 1, minHeight: 0 }}>
        {spec.kind === 'force-graph' && (
          <ForceGraphViz
            nodes={graphData.nodes}
            links={graphData.links}
            width={dimensions.width}
            height={dimensions.height - 32}
            onNodeClick={handleNodeClick}
          />
        )}
        {spec.kind === 'bar' && (
          <BarChartViz
            data={data}
            labelField={labelField}
            valueField={sizeField}
            colorField={spec.colorField}
            width={dimensions.width}
            height={dimensions.height - 32}
          />
        )}
        {spec.kind === 'sparkline' && (
          <SparklineViz
            data={data}
            valueField={sizeField}
            labelField={spec.labelField}
            width={dimensions.width}
            height={dimensions.height - 32}
          />
        )}
        {spec.kind === 'timeline' && (
          <div style={{ padding: 24, color: '#6b7280', fontSize: 12 }}>
            Timeline visualization — coming in Phase 2b
          </div>
        )}
        {spec.kind === 'truth-lattice' && (
          <IxqlTruthLatticePanel spec={spec} graphContext={graphContext} />
        )}
      </div>
    </div>
  );
};
