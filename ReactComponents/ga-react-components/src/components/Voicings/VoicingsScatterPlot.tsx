/**
 * VoicingsScatterPlot — d3 2D scatter renderer for the OPTIC-K t-SNE
 * artifact emitted by `ix-voicings tsne_voicings`.
 *
 * Loads `<src>` (default `/voicings-tsne.json` from the static dir),
 * renders a colored-by-instrument scatter with pan/zoom and a hover
 * tooltip. Designed to be the visible payoff for IX's `ix-manifold`
 * t-SNE work — drop-in renderer, no GA backend dependency.
 */

import React, { useEffect, useMemo, useRef, useState } from 'react';
import * as d3 from 'd3';

interface TsnePoint {
  id: number;
  instrument: string;
  x: number;
  y: number;
}

interface TsneArtifact {
  schema_version: number;
  perplexity: number;
  iterations: number;
  seed: number;
  n_sampled: number;
  n_total: number;
  dim: number;
  points: TsnePoint[];
}

interface Props {
  /** URL to the t-SNE JSON. Defaults to the public-served path. */
  src?: string;
  width?: number;
  height?: number;
}

const INSTRUMENT_COLORS: Record<string, string> = {
  guitar: '#4f8cf7',
  bass: '#f5a14e',
  ukulele: '#5fc97a',
  unknown: '#9aa0a6',
};

export const VoicingsScatterPlot: React.FC<Props> = ({
  src = '/voicings-tsne.json',
  width = 900,
  height = 640,
}) => {
  const svgRef = useRef<SVGSVGElement | null>(null);
  const tooltipRef = useRef<HTMLDivElement | null>(null);
  const [artifact, setArtifact] = useState<TsneArtifact | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [hover, setHover] = useState<TsnePoint | null>(null);

  // Load JSON once.
  useEffect(() => {
    let cancelled = false;
    fetch(src)
      .then((r) => {
        if (!r.ok) throw new Error(`HTTP ${r.status} loading ${src}`);
        return r.json();
      })
      .then((data: TsneArtifact) => {
        if (!cancelled) setArtifact(data);
      })
      .catch((e) => {
        if (!cancelled) setError(String(e));
      });
    return () => {
      cancelled = true;
    };
  }, [src]);

  const instrumentCounts = useMemo(() => {
    if (!artifact) return new Map<string, number>();
    const m = new Map<string, number>();
    for (const p of artifact.points) m.set(p.instrument, (m.get(p.instrument) ?? 0) + 1);
    return m;
  }, [artifact]);

  // Render scatter when data lands or size changes.
  useEffect(() => {
    if (!artifact || !svgRef.current) return;
    const svg = d3.select(svgRef.current);
    svg.selectAll('*').remove();

    const margin = { top: 20, right: 20, bottom: 40, left: 50 };
    const innerW = width - margin.left - margin.right;
    const innerH = height - margin.top - margin.bottom;

    const xs = artifact.points.map((p) => p.x);
    const ys = artifact.points.map((p) => p.y);
    const xExtent = d3.extent(xs) as [number, number];
    const yExtent = d3.extent(ys) as [number, number];
    const padX = (xExtent[1] - xExtent[0]) * 0.05;
    const padY = (yExtent[1] - yExtent[0]) * 0.05;

    const xScale = d3
      .scaleLinear()
      .domain([xExtent[0] - padX, xExtent[1] + padX])
      .range([0, innerW]);
    const yScale = d3
      .scaleLinear()
      .domain([yExtent[0] - padY, yExtent[1] + padY])
      .range([innerH, 0]);

    const root = svg
      .attr('width', width)
      .attr('height', height)
      .attr('viewBox', `0 0 ${width} ${height}`)
      .style('background', '#0e1116');

    const g = root.append('g').attr('transform', `translate(${margin.left},${margin.top})`);

    // Subtle grid.
    const xAxis = d3.axisBottom(xScale).tickSizeInner(-innerH).tickSizeOuter(0);
    const yAxis = d3.axisLeft(yScale).tickSizeInner(-innerW).tickSizeOuter(0);
    g.append('g')
      .attr('transform', `translate(0,${innerH})`)
      .call(xAxis as any)
      .selectAll('line')
      .attr('stroke', '#1f2630');
    g.append('g')
      .call(yAxis as any)
      .selectAll('line')
      .attr('stroke', '#1f2630');
    g.selectAll('.tick text').attr('fill', '#7a8290').style('font-size', '10px');
    g.selectAll('.domain').attr('stroke', '#2a313c');

    // Zoomable point layer.
    const pointsG = g.append('g').attr('class', 'points');
    const circles = pointsG
      .selectAll('circle')
      .data(artifact.points)
      .join('circle')
      .attr('cx', (d) => xScale(d.x))
      .attr('cy', (d) => yScale(d.y))
      .attr('r', 3.2)
      .attr('fill', (d) => INSTRUMENT_COLORS[d.instrument] ?? INSTRUMENT_COLORS.unknown)
      .attr('fill-opacity', 0.78)
      .attr('stroke', 'none')
      .style('cursor', 'pointer');

    circles
      .on('mouseenter', function (_event, d) {
        setHover(d);
        d3.select(this).attr('r', 6).attr('fill-opacity', 1);
      })
      .on('mouseleave', function () {
        setHover(null);
        d3.select(this).attr('r', 3.2).attr('fill-opacity', 0.78);
      });

    // Pan + zoom. Point radius stays constant in screen pixels by
    // counter-scaling within the transform.
    const zoom = d3
      .zoom<SVGSVGElement, unknown>()
      .scaleExtent([0.5, 20])
      .on('zoom', (event) => {
        pointsG.attr('transform', event.transform.toString());
        circles.attr('r', 3.2 / event.transform.k);
      });
    root.call(zoom as any);
  }, [artifact, width, height]);

  if (error) {
    return (
      <div style={{ padding: 16, color: '#ff7a7a', fontFamily: 'monospace' }}>
        Failed to load t-SNE artifact: {error}
      </div>
    );
  }

  if (!artifact) {
    return (
      <div style={{ padding: 16, color: '#9aa0a6', fontFamily: 'monospace' }}>
        Loading {src}…
      </div>
    );
  }

  return (
    <div style={{ position: 'relative', color: '#e6e8eb', fontFamily: 'system-ui, sans-serif' }}>
      <div
        style={{
          padding: '8px 12px',
          fontSize: 12,
          color: '#9aa0a6',
          background: '#0e1116',
          borderBottom: '1px solid #1f2630',
        }}
      >
        <strong style={{ color: '#e6e8eb' }}>OPTIC-K voicings — t-SNE 2D projection</strong>
        {' · '}
        {artifact.n_sampled.toLocaleString()} of {artifact.n_total.toLocaleString()} sampled
        {' · '}
        dim {artifact.dim} → 2 · perplexity {artifact.perplexity}, {artifact.iterations} iters
        {' · '}seed {artifact.seed}
        <span style={{ marginLeft: 16 }}>
          {Array.from(instrumentCounts.entries()).map(([inst, n]) => (
            <span key={inst} style={{ marginRight: 12 }}>
              <span
                style={{
                  display: 'inline-block',
                  width: 10,
                  height: 10,
                  background: INSTRUMENT_COLORS[inst] ?? INSTRUMENT_COLORS.unknown,
                  borderRadius: '50%',
                  marginRight: 4,
                  verticalAlign: 'middle',
                }}
              />
              {inst} ({n})
            </span>
          ))}
        </span>
      </div>
      <div style={{ position: 'relative' }}>
        <svg ref={svgRef} />
        {hover && (
          <div
            ref={tooltipRef}
            style={{
              position: 'absolute',
              top: 8,
              right: 8,
              background: '#11151b',
              border: '1px solid #2a313c',
              padding: '6px 10px',
              fontSize: 12,
              fontFamily: 'monospace',
              borderRadius: 4,
              pointerEvents: 'none',
            }}
          >
            <div>id: {hover.id}</div>
            <div>instrument: {hover.instrument}</div>
            <div>
              ({hover.x.toFixed(2)}, {hover.y.toFixed(2)})
            </div>
          </div>
        )}
      </div>
    </div>
  );
};

export default VoicingsScatterPlot;
