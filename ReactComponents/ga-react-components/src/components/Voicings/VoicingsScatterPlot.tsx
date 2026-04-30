/**
 * VoicingsScatterPlot — d3 + canvas 2D scatter renderer for the
 * OPTIC-K t-SNE artifact emitted by `ix-voicings tsne_voicings`.
 *
 * Loads `<src>` (default `/voicings-tsne.json` from the static dir).
 * Renders a colored-by-instrument scatter on `<canvas>` (handles
 * 300K+ points smoothly), with axes + grid on an overlaid SVG.
 * Hover hit-testing via `d3-quadtree` for tooltip lookup.
 *
 * Designed to be the visible payoff for IX's `ix-manifold` t-SNE
 * work — drop-in renderer, no GA backend dependency. Scales to the
 * full 313K-voicing OPTIC-K corpus.
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

const MARGIN = { top: 20, right: 20, bottom: 40, left: 50 };

export const VoicingsScatterPlot: React.FC<Props> = ({
  src = '/voicings-tsne.json',
  width = 1200,
  height = 720,
}) => {
  const canvasRef = useRef<HTMLCanvasElement | null>(null);
  const svgRef = useRef<SVGSVGElement | null>(null);
  const [artifact, setArtifact] = useState<TsneArtifact | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [hover, setHover] = useState<TsnePoint | null>(null);

  const innerW = width - MARGIN.left - MARGIN.right;
  const innerH = height - MARGIN.top - MARGIN.bottom;

  // Load JSON once.
  useEffect(() => {
    let cancelled = false;
    const t0 = performance.now();
    fetch(src)
      .then((r) => {
        if (!r.ok) throw new Error(`HTTP ${r.status} loading ${src}`);
        return r.json();
      })
      .then((data: TsneArtifact) => {
        if (!cancelled) {
          // eslint-disable-next-line no-console
          console.log(`[VoicingsScatterPlot] loaded ${data.n_sampled} pts in ${(performance.now() - t0).toFixed(0)}ms`);
          setArtifact(data);
        }
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

  // Memoize scales so quadtree hit-testing can use them outside the
  // draw effect.
  const scales = useMemo(() => {
    if (!artifact) return null;
    const xs = artifact.points.map((p) => p.x);
    const ys = artifact.points.map((p) => p.y);
    const xExtent = d3.extent(xs) as [number, number];
    const yExtent = d3.extent(ys) as [number, number];
    const padX = (xExtent[1] - xExtent[0]) * 0.05;
    const padY = (yExtent[1] - yExtent[0]) * 0.05;
    return {
      xScale: d3.scaleLinear().domain([xExtent[0] - padX, xExtent[1] + padX]).range([0, innerW]),
      yScale: d3.scaleLinear().domain([yExtent[0] - padY, yExtent[1] + padY]).range([innerH, 0]),
    };
  }, [artifact, innerW, innerH]);

  // Quadtree on screen-space coordinates (post-scale) for hit testing.
  const quadtree = useMemo(() => {
    if (!artifact || !scales) return null;
    const tree = d3
      .quadtree<TsnePoint>()
      .x((d) => scales.xScale(d.x))
      .y((d) => scales.yScale(d.y))
      .addAll(artifact.points);
    return tree;
  }, [artifact, scales]);

  // Draw + zoom + hover wiring.
  useEffect(() => {
    if (!artifact || !scales || !canvasRef.current || !svgRef.current) return;

    const canvas = canvasRef.current;
    const svg = d3.select(svgRef.current);
    svg.selectAll('*').remove();

    // High-DPI canvas
    const dpr = window.devicePixelRatio || 1;
    canvas.width = width * dpr;
    canvas.height = height * dpr;
    canvas.style.width = `${width}px`;
    canvas.style.height = `${height}px`;
    const ctx = canvas.getContext('2d')!;
    ctx.scale(dpr, dpr);

    // Build axes layer
    const axisG = svg
      .attr('width', width)
      .attr('height', height)
      .attr('viewBox', `0 0 ${width} ${height}`)
      .append('g')
      .attr('transform', `translate(${MARGIN.left},${MARGIN.top})`);

    let currentTransform = d3.zoomIdentity;

    const draw = () => {
      const { xScale, yScale } = scales;
      const tx = currentTransform.rescaleX(xScale);
      const ty = currentTransform.rescaleY(yScale);

      // Clear
      ctx.clearRect(0, 0, width, height);

      // Background
      ctx.fillStyle = '#0e1116';
      ctx.fillRect(0, 0, width, height);

      // Axes
      axisG.selectAll('*').remove();
      const xAxis = d3.axisBottom(tx).tickSizeInner(-innerH).tickSizeOuter(0);
      const yAxis = d3.axisLeft(ty).tickSizeInner(-innerW).tickSizeOuter(0);
      axisG.append('g').attr('transform', `translate(0,${innerH})`).call(xAxis as any).selectAll('line').attr('stroke', '#1f2630');
      axisG.append('g').call(yAxis as any).selectAll('line').attr('stroke', '#1f2630');
      axisG.selectAll('.tick text').attr('fill', '#7a8290').style('font-size', '10px');
      axisG.selectAll('.domain').attr('stroke', '#2a313c');

      // Points
      const r = Math.max(0.6, Math.min(2.4, 1.4 * Math.sqrt(currentTransform.k)));
      ctx.translate(MARGIN.left, MARGIN.top);
      // Group by instrument so we set fillStyle once per group
      const byInstrument = new Map<string, TsnePoint[]>();
      for (const p of artifact.points) {
        const arr = byInstrument.get(p.instrument);
        if (arr) arr.push(p);
        else byInstrument.set(p.instrument, [p]);
      }
      for (const [inst, points] of byInstrument) {
        ctx.fillStyle = INSTRUMENT_COLORS[inst] ?? INSTRUMENT_COLORS.unknown;
        ctx.globalAlpha = artifact.points.length > 50000 ? 0.55 : 0.78;
        ctx.beginPath();
        for (const p of points) {
          const cx = tx(p.x);
          const cy = ty(p.y);
          if (cx < -r || cx > innerW + r || cy < -r || cy > innerH + r) continue;
          ctx.moveTo(cx + r, cy);
          ctx.arc(cx, cy, r, 0, Math.PI * 2);
        }
        ctx.fill();
      }
      ctx.globalAlpha = 1;
      ctx.translate(-MARGIN.left, -MARGIN.top);

      // Hover marker
      if (hover) {
        const hx = MARGIN.left + tx(hover.x);
        const hy = MARGIN.top + ty(hover.y);
        ctx.strokeStyle = '#ffffff';
        ctx.lineWidth = 2;
        ctx.beginPath();
        ctx.arc(hx, hy, r + 4, 0, Math.PI * 2);
        ctx.stroke();
      }
    };

    draw();

    const zoom = d3
      .zoom<HTMLCanvasElement, unknown>()
      .scaleExtent([0.5, 50])
      .on('zoom', (event) => {
        currentTransform = event.transform;
        draw();
      });
    d3.select(canvas).call(zoom as any);

    return () => {
      d3.select(canvas).on('.zoom', null);
    };
  }, [artifact, scales, width, height, innerW, innerH, hover]);

  // Hover hit-test via quadtree (screen-space).
  const onMouseMove = (e: React.MouseEvent<HTMLCanvasElement>) => {
    if (!quadtree || !canvasRef.current) return;
    const rect = canvasRef.current.getBoundingClientRect();
    const sx = e.clientX - rect.left - MARGIN.left;
    const sy = e.clientY - rect.top - MARGIN.top;
    if (sx < 0 || sx > innerW || sy < 0 || sy > innerH) {
      if (hover) setHover(null);
      return;
    }
    const found = quadtree.find(sx, sy, 8);
    setHover(found ?? null);
  };

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
              {inst} ({n.toLocaleString()})
            </span>
          ))}
        </span>
      </div>
      <div style={{ position: 'relative', width, height }}>
        <canvas
          ref={canvasRef}
          onMouseMove={onMouseMove}
          onMouseLeave={() => setHover(null)}
          style={{ position: 'absolute', top: 0, left: 0, cursor: 'crosshair' }}
        />
        <svg
          ref={svgRef}
          style={{ position: 'absolute', top: 0, left: 0, pointerEvents: 'none' }}
        />
        {hover && (
          <div
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
              color: '#e6e8eb',
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
