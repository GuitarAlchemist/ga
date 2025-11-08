// @ts-nocheck
/**
 * WebGPU Fretboard Component
 * Following Pixi.js v8 WebGPU best practices
 */
import React, { useEffect, useRef } from 'react';
import { Container, Graphics, Text } from 'pixi.js';
import { Box, Typography } from '@mui/material';
import { WebGPUFretboardProps, DEFAULT_CONFIG } from './types';
import { createRenderer } from './renderer';
import { fretXmm, stringYmm, makeMmToPx, STRING_GAUGES_INCH, stringThicknessPx } from './spacing';
import { capoTransform } from './capo';
import { applyLeftHanded, reverseIfLeftHanded } from './handedness';
import { makeLightingFilter } from './filters';

export const WebGPUFretboard: React.FC<WebGPUFretboardProps> = ({
  title = 'WebGPU Fretboard',
  positions = [],
  config = {},
  onPositionClick,
}) => {
  const canvasRef = useRef<HTMLCanvasElement>(null);
  const containerRef = useRef<HTMLDivElement>(null);

  const cfg = { ...DEFAULT_CONFIG, ...config };
  const {
    scaleLengthMM,
    nutWidthMM,
    fretCount,
    stringCount,
    tuning,
    capoFret,
    leftHanded,
    showFretNumbers,
    showStringLabels,
    showInlays,
    viewportWidth,
    viewportHeight,
  } = cfg;

  useEffect(() => {
    if (!canvasRef.current) return;

    let renderer: any;
    let stage: Container;

    const init = async () => {
      // Create WebGPU renderer
      renderer = await createRenderer(canvasRef.current!);
      
      // Create stage
      stage = new Container();
      stage.sortableChildren = true;

      // Calculate coordinate transform
      const visibleRangeMM = fretXmm(fretCount, scaleLengthMM);
      const mmToPx = makeMmToPx(viewportWidth, visibleRangeMM);
      const pxPerMM = viewportWidth / visibleRangeMM;

      // Capo transform
      const capo = capoTransform(scaleLengthMM, capoFret);
      const mmOffset = capo.mmOffset;

      // Helper: fret position in pixels (after capo offset)
      const fretPx = (n: number) => Math.round(mmToPx(fretXmm(n, scaleLengthMM) - mmOffset));

      // Helper: string Y position in pixels
      const stringPx = (s: number) => {
        const centerY = viewportHeight / 2;
        return centerY + mmToPx(stringYmm(s, stringCount, nutWidthMM));
      };

      // Create layers (z-order)
      const boardLayer = new Container();
      boardLayer.zIndex = 0;
      
      const inlayLayer = new Container();
      inlayLayer.zIndex = 5;
      
      const fretLayer = new Container();
      fretLayer.zIndex = 10;
      
      const labelLayer = new Container();
      labelLayer.zIndex = 15;
      
      const stringShadowLayer = new Container();
      stringShadowLayer.zIndex = 20;
      
      const stringLayer = new Container();
      stringLayer.zIndex = 30;
      
      const notesLayer = new Container();
      notesLayer.zIndex = 40;
      
      const overlayLayer = new Container();
      overlayLayer.zIndex = 50;

      stage.addChild(
        boardLayer,
        inlayLayer,
        fretLayer,
        labelLayer,
        stringShadowLayer,
        stringLayer,
        notesLayer,
        overlayLayer
      );

      // Draw fretboard wood
      const board = new Graphics();
      board.rect(0, 0, viewportWidth, viewportHeight);
      board.fill(0x3d2817); // Dark wood color
      boardLayer.addChild(board);

      // Apply lighting filter
      try {
        const lightingFilter = makeLightingFilter();
        boardLayer.filters = [lightingFilter];
      } catch (e) {
        console.warn('WebGPU filter not available, using fallback', e);
      }

      // Draw frets
      for (let i = 0; i <= fretCount; i++) {
        const x = fretPx(i);
        const fret = new Graphics();
        fret.rect(x - 1, 0, 2, viewportHeight);
        fret.fill(i === 0 ? 0xf5f5dc : 0xc0c0c0); // Nut is bone color, frets are silver
        fretLayer.addChild(fret);
      }

      // Draw inlays
      if (showInlays) {
        const inlayFrets = [3, 5, 7, 9, 12, 15, 17, 19, 21, 24];
        const centerY = viewportHeight / 2;
        
        inlayFrets.forEach(fretNum => {
          if (fretNum > fretCount) return;
          
          const x1 = fretPx(fretNum - 1);
          const x2 = fretPx(fretNum);
          const midX = (x1 + x2) / 2;
          
          const inlay = new Graphics();
          if (fretNum === 12 || fretNum === 24) {
            // Double dot
            inlay.circle(midX, centerY - 15, 4);
            inlay.circle(midX, centerY + 15, 4);
          } else {
            // Single dot
            inlay.circle(midX, centerY, 4);
          }
          inlay.fill(0xf5f5dc); // Bone/pearl color
          inlayLayer.addChild(inlay);
        });
      }

      // Draw strings
      const gauges = reverseIfLeftHanded(STRING_GAUGES_INCH, leftHanded);
      
      for (let s = 0; s < stringCount; s++) {
        const y = stringPx(s);
        const thickness = stringThicknessPx(gauges[s], pxPerMM);
        
        // String shadow
        const shadow = new Graphics();
        shadow.rect(0, y - 1, viewportWidth, 3);
        shadow.fill({ color: 0x000000, alpha: 0.12 });
        stringShadowLayer.addChild(shadow);
        
        // String body
        const string = new Graphics();
        string.rect(0, y - thickness / 2, viewportWidth, thickness);
        string.fill(0xc0c0c0); // Silver color
        stringLayer.addChild(string);
        
        // String highlight
        const highlight = new Graphics();
        highlight.rect(0, y - thickness / 2 - 0.5, viewportWidth, 1);
        highlight.fill({ color: 0xffffff, alpha: 0.6 });
        stringLayer.addChild(highlight);
      }

      // Mask strings under capo
      if (capoFret > 0) {
        const mask = new Graphics();
        mask.rect(fretPx(0), 0, viewportWidth, viewportHeight);
        mask.fill(0xffffff);
        stringLayer.mask = mask;
      }

      // Draw fret numbers
      if (showFretNumbers) {
        for (let i = 1; i <= fretCount; i++) {
          const x1 = fretPx(i - 1);
          const x2 = fretPx(i);
          const midX = (x1 + x2) / 2;
          
          // Skip if too close to capo
          if (midX < fretPx(0) + 8) continue;
          
          const label = new Text({
            text: i.toString(),
            style: { fontSize: 10, fill: 0x999999 },
          });
          label.anchor.set(0.5);
          label.x = midX;
          label.y = viewportHeight - 10;
          labelLayer.addChild(label);
        }
      }

      // Draw string labels
      if (showStringLabels) {
        const labels = reverseIfLeftHanded(tuning, leftHanded);
        for (let s = 0; s < stringCount; s++) {
          const y = stringPx(s);
          const label = new Text({
            text: labels[s],
            style: { fontSize: 11, fill: 0xcccccc, fontWeight: 'bold' },
          });
          label.anchor.set(1, 0.5);
          label.x = -5;
          label.y = y;
          labelLayer.addChild(label);
        }
      }

      // Draw position markers
      positions.forEach(pos => {
        const x1 = fretPx(pos.fret);
        const x2 = fretPx(pos.fret + 1);
        const midX = (x1 + x2) / 2;
        const y = stringPx(pos.string);
        
        const marker = new Graphics();
        const radius = pos.emphasized ? 10 : 8;
        const color = pos.color ? parseInt(pos.color.replace('#', ''), 16) : 0x4dabf7;
        
        marker.circle(midX, y, radius);
        marker.fill(color);
        
        if (pos.label) {
          const text = new Text({
            text: pos.label,
            style: { fontSize: 10, fill: 0xffffff, fontWeight: 'bold' },
          });
          text.anchor.set(0.5);
          text.x = midX;
          text.y = y;
          notesLayer.addChild(text);
        }
        
        marker.eventMode = 'static';
        marker.cursor = 'pointer';
        marker.on('pointerdown', () => onPositionClick?.(pos.string, pos.fret));
        
        notesLayer.addChild(marker);
      });

      // Apply left-handed mirroring
      applyLeftHanded(stage, viewportWidth, leftHanded);

      // Render
      renderer.render(stage);
    };

    init().catch(console.error);

    return () => {
      renderer?.destroy();
    };
  }, [cfg, positions, onPositionClick]);

  return (
    <Box ref={containerRef}>
      {title && <Typography variant="h6" gutterBottom>{title}</Typography>}
      <canvas
        ref={canvasRef}
        width={viewportWidth}
        height={viewportHeight}
        style={{ border: '1px solid #ddd', borderRadius: 4 }}
      />
    </Box>
  );
};

export default WebGPUFretboard;
