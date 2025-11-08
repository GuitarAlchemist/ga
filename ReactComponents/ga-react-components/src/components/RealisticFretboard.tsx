// @ts-nocheck
import React, { useEffect, useRef, useState } from 'react';
import { Application, Graphics, Text, Container, Sprite, Texture, Filter, WebGPURenderer } from 'pixi.js';
import { Box, Typography, Stack, FormControl, InputLabel, Select, MenuItem, FormControlLabel, Switch } from '@mui/material';
import { GuitarModelStyle, getGuitarModel, getAllModels, GUITAR_CATEGORIES } from './GuitarModels';
import { hexToRgb } from './FretboardShaders';
import {
  fretPositionMM,
  stringX,
  getStringGauge,
  isStringWound,
  SCALES,
  Scale
} from './FretboardMath';

export interface FretboardPosition {
  string: number;
  fret: number;
  label?: string;
  color?: string;
  emphasized?: boolean;
}

export interface RealisticFretboardConfig {
  fretCount?: number;
  stringCount?: number;
  tuning?: string[];
  showFretNumbers?: boolean;
  showStringLabels?: boolean;
  width?: number;
  height?: number;
  spacingMode?: 'schematic' | 'realistic';
  guitarModel?: string; // Model ID from GuitarModels
  flipped?: boolean; // Flip the fretboard (nut on left)
  capoFret?: number; // Capo position (0 = no capo)
  leftHanded?: boolean; // Left-handed orientation (strings reversed)
}

interface RealisticFretboardProps {
  title?: string;
  positions?: FretboardPosition[];
  config?: RealisticFretboardConfig;
  onPositionClick?: (string: number, fret: number) => void;
  onPositionHover?: (string: number | null, fret: number | null) => void;
}

const DEFAULT_CONFIG: Required<RealisticFretboardConfig> = {
  fretCount: 22,
  stringCount: 6,
  tuning: ['E', 'B', 'G', 'D', 'A', 'E'],
  showFretNumbers: true,
  showStringLabels: true,
  width: 1700, // Increased to show headstock and guitar body
  height: 250,
  spacingMode: 'realistic',
  guitarModel: 'electric_fender_strat', // Fender Stratocaster by default
  flipped: true, // Nut on left by default
  capoFret: 0, // No capo by default
  leftHanded: false, // Right-handed by default
};

export const RealisticFretboard: React.FC<RealisticFretboardProps> = ({
  title = 'Realistic Fretboard',
  positions = [],
  config = {},
  onPositionClick,
  onPositionHover,
}) => {
  const canvasRef = useRef<HTMLDivElement>(null);
  const appRef = useRef<Application | null>(null);
  const [selectedModel, setSelectedModel] = useState(config.guitarModel || DEFAULT_CONFIG.guitarModel);
  const [capoFret, setCapoFret] = useState(config.capoFret || DEFAULT_CONFIG.capoFret);
  const [isLeftHanded, setIsLeftHanded] = useState(config.leftHanded || DEFAULT_CONFIG.leftHanded);
  const [hoveredPosition, setHoveredPosition] = useState<{ string: number; fret: number } | null>(null);

  const fretboardConfig = { ...DEFAULT_CONFIG, ...config, guitarModel: selectedModel, capoFret, leftHanded: isLeftHanded };
  const {
    fretCount,
    stringCount,
    tuning,
    showFretNumbers,
    showStringLabels,
    width,
    height,
    spacingMode,
    guitarModel,
    flipped,
    leftHanded,
  } = fretboardConfig;

  const guitarStyle = getGuitarModel(guitarModel);

  useEffect(() => {
    if (!canvasRef.current) return;

    // Flag to track if component is still mounted
    let isMounted = true;

    const initPixi = async () => {
      // Clear any existing canvas first (prevents duplicates in React StrictMode)
      if (canvasRef.current) {
        canvasRef.current.innerHTML = '';
      }

      // Destroy any existing app before creating a new one
      if (appRef.current) {
        try {
          appRef.current.destroy(true, true);
        } catch (e) {
          console.warn('Error destroying previous Pixi app:', e);
        }
        appRef.current = null;
      }

      // Check for WebGPU support
      if (!('gpu' in navigator)) {
        console.warn('WebGPU is not supported in this browser. Falling back to WebGL.');
      }

      const app = new Application();
      await app.init({
        width,
        height,
        backgroundColor: 0x1a1a1a,
        antialias: true, // Enable antialiasing
        resolution: Math.max(window.devicePixelRatio || 1, 2), // Use at least 2x resolution for better quality
        autoDensity: true, // Automatically adjust CSS size based on resolution
        preference: 'webgpu', // Explicitly prefer WebGPU
        // Will automatically fall back to WebGL if WebGPU is not available
      });

      // Check if component was unmounted during async init
      if (!isMounted) {
        try {
          app.destroy(true, true);
        } catch (e) {
          console.warn('Error destroying app during unmount:', e);
        }
        return;
      }

      // Log which renderer is being used
      if (app.renderer instanceof WebGPURenderer) {
        console.log('✅ RealisticFretboard: Using WebGPU renderer');
      } else {
        console.log('⚠️ RealisticFretboard: Using WebGL renderer (WebGPU not available)');
      }

      if (!canvasRef.current || !isMounted) {
        try {
          app.destroy(true, true);
        } catch (e) {
          console.warn('Error destroying app:', e);
        }
        return;
      }

      canvasRef.current.appendChild(app.canvas as HTMLCanvasElement);
      appRef.current = app;

      const fretboardContainer = new Container();
      app.stage.addChild(fretboardContainer);

      // Full canvas background (behind the trapezoid neck)
      const canvasBackground = new Graphics();
      canvasBackground.rect(0, 0, width, height);
      canvasBackground.fill(0x1a1a1a); // Dark background color
      fretboardContainer.addChild(canvasBackground);

      const labelWidth = 50; // Reduced - no headstock needed
      const rightMargin = 120; // Reduced - just enough for strum zone
      const topMargin = 30;
      const bottomMargin = 30;

      // Helper function to draw guitar body portion
      const drawGuitarBody = (
        bodyX: number,
        bodyY: number,
        bodyWidth: number,
        bodyHeight: number,
        category: 'classical' | 'acoustic' | 'electric'
      ) => {
        const bodyContainer = new Container();

        if (category === 'classical') {
          // Classical guitar body with rosette
          const bodyWoodColor = guitarStyle.woodColor;
          const lighterWood = Math.min(0xffffff, bodyWoodColor + 0x1a1410);

          // Main body curve (partial circle)
          const bodyGraphic = new Graphics();
          const bodyRadius = bodyHeight * 0.6;

          // Draw curved body edge
          bodyGraphic.moveTo(bodyX, bodyY - bodyHeight / 2);
          bodyGraphic.bezierCurveTo(
            bodyX + bodyWidth * 0.3, bodyY - bodyHeight / 2,
            bodyX + bodyWidth * 0.5, bodyY - bodyHeight * 0.4,
            bodyX + bodyWidth, bodyY
          );
          bodyGraphic.bezierCurveTo(
            bodyX + bodyWidth * 0.5, bodyY + bodyHeight * 0.4,
            bodyX + bodyWidth * 0.3, bodyY + bodyHeight / 2,
            bodyX, bodyY + bodyHeight / 2
          );
          bodyGraphic.lineTo(bodyX, bodyY - bodyHeight / 2);
          bodyGraphic.fill(bodyWoodColor);
          bodyContainer.addChild(bodyGraphic);

          // Add wood grain to body
          const bodyGrainLines = 30;
          for (let i = 0; i < bodyGrainLines; i++) {
            const t = i / bodyGrainLines;
            const grainY = bodyY - bodyHeight / 2 + t * bodyHeight;
            if (Math.random() > 0.6) {
              const grainLine = new Graphics();
              grainLine.moveTo(bodyX, grainY);
              grainLine.lineTo(bodyX + bodyWidth * 0.8, grainY);
              const darkerColor = Math.max(0, bodyWoodColor - 0x0f0a08);
              grainLine.stroke({ color: darkerColor, width: 0.5, alpha: 0.1 });
              bodyContainer.addChild(grainLine);
            }
          }

          // Draw rosette (decorative circle around sound hole)
          const rosetteX = bodyX + bodyWidth * 0.6;
          const rosetteY = bodyY;
          const rosetteRadius = bodyHeight * 0.25;

          // Sound hole (dark circle)
          const soundHole = new Graphics();
          soundHole.circle(rosetteX, rosetteY, rosetteRadius * 0.7);
          soundHole.fill(0x000000);
          bodyContainer.addChild(soundHole);

          // Rosette rings (decorative concentric circles)
          const rosetteRings = [
            { radius: rosetteRadius * 1.0, color: 0x8b4513, width: 2 },
            { radius: rosetteRadius * 0.95, color: 0xffd700, width: 1 },
            { radius: rosetteRadius * 0.9, color: 0x654321, width: 2 },
            { radius: rosetteRadius * 0.85, color: 0xffd700, width: 1 },
            { radius: rosetteRadius * 0.8, color: 0x8b4513, width: 2 },
          ];

          rosetteRings.forEach(ring => {
            const rosetteRing = new Graphics();
            rosetteRing.circle(rosetteX, rosetteY, ring.radius);
            rosetteRing.stroke({ color: ring.color, width: ring.width });
            bodyContainer.addChild(rosetteRing);
          });

          // Add decorative mosaic pattern in rosette
          const mosaicSegments = 24;
          for (let i = 0; i < mosaicSegments; i++) {
            const angle = (i / mosaicSegments) * Math.PI * 2;
            const mosaicRadius = rosetteRadius * 0.92;
            const mosaicX = rosetteX + Math.cos(angle) * mosaicRadius;
            const mosaicY = rosetteY + Math.sin(angle) * mosaicRadius;
            const mosaicDot = new Graphics();
            mosaicDot.circle(mosaicX, mosaicY, 1.5);
            const mosaicColor = i % 2 === 0 ? 0xffd700 : 0x8b4513;
            mosaicDot.fill(mosaicColor);
            bodyContainer.addChild(mosaicDot);
          }

        } else if (category === 'acoustic') {
          // Acoustic guitar body (similar to classical but different proportions)
          const bodyWoodColor = guitarStyle.woodColor;

          // Main body curve
          const bodyGraphic = new Graphics();
          bodyGraphic.moveTo(bodyX, bodyY - bodyHeight / 2);
          bodyGraphic.bezierCurveTo(
            bodyX + bodyWidth * 0.4, bodyY - bodyHeight / 2,
            bodyX + bodyWidth * 0.6, bodyY - bodyHeight * 0.3,
            bodyX + bodyWidth, bodyY
          );
          bodyGraphic.bezierCurveTo(
            bodyX + bodyWidth * 0.6, bodyY + bodyHeight * 0.3,
            bodyX + bodyWidth * 0.4, bodyY + bodyHeight / 2,
            bodyX, bodyY + bodyHeight / 2
          );
          bodyGraphic.lineTo(bodyX, bodyY - bodyHeight / 2);
          bodyGraphic.fill(bodyWoodColor);
          bodyContainer.addChild(bodyGraphic);

          // Pickguard (for acoustic guitars)
          const pickguard = new Graphics();
          const pgX = bodyX + bodyWidth * 0.5;
          const pgY = bodyY + bodyHeight * 0.15;
          pickguard.moveTo(pgX, pgY);
          pickguard.bezierCurveTo(
            pgX + 30, pgY - 20,
            pgX + 40, pgY + 10,
            pgX + 35, pgY + 40
          );
          pickguard.bezierCurveTo(
            pgX + 20, pgY + 35,
            pgX + 10, pgY + 20,
            pgX, pgY
          );
          pickguard.fill({ color: 0x000000, alpha: 0.3 });
          bodyContainer.addChild(pickguard);

        } else if (category === 'electric') {
          // Electric guitar body (more angular, modern)
          const bodyWoodColor = guitarStyle.woodColor;

          // Simplified electric body shape
          const bodyGraphic = new Graphics();
          bodyGraphic.moveTo(bodyX, bodyY - bodyHeight / 2);
          bodyGraphic.lineTo(bodyX + bodyWidth * 0.7, bodyY - bodyHeight / 2);
          bodyGraphic.bezierCurveTo(
            bodyX + bodyWidth, bodyY - bodyHeight / 3,
            bodyX + bodyWidth, bodyY + bodyHeight / 3,
            bodyX + bodyWidth * 0.7, bodyY + bodyHeight / 2
          );
          bodyGraphic.lineTo(bodyX, bodyY + bodyHeight / 2);
          bodyGraphic.lineTo(bodyX, bodyY - bodyHeight / 2);
          bodyGraphic.fill(bodyWoodColor);
          bodyContainer.addChild(bodyGraphic);

          // Add glossy highlight for electric guitars
          const highlight = new Graphics();
          highlight.moveTo(bodyX + 10, bodyY - bodyHeight / 3);
          highlight.bezierCurveTo(
            bodyX + bodyWidth * 0.3, bodyY - bodyHeight / 4,
            bodyX + bodyWidth * 0.4, bodyY,
            bodyX + bodyWidth * 0.3, bodyY + bodyHeight / 4
          );
          highlight.stroke({ color: 0xffffff, width: 2, alpha: 0.2 });
          bodyContainer.addChild(highlight);

          // Add pickups (neck and bridge pickups)
          const pickupWidth = 50;
          const pickupHeight = bodyHeight * 0.6;

          // Neck pickup (closer to neck)
          const neckPickupX = bodyX + 20;
          const neckPickup = new Graphics();
          neckPickup.rect(neckPickupX, bodyY - pickupHeight / 2, pickupWidth, pickupHeight);
          neckPickup.fill(0x1a1a1a); // Black pickup
          bodyContainer.addChild(neckPickup);

          // Pickup pole pieces (6 dots for strings)
          for (let i = 0; i < 6; i++) {
            const poleY = bodyY - pickupHeight / 2 + (i + 1) * (pickupHeight / 7);
            const pole = new Graphics();
            pole.circle(neckPickupX + pickupWidth / 2, poleY, 3);
            pole.fill(0xc0c0c0); // Silver pole pieces
            bodyContainer.addChild(pole);
          }

          // Bridge pickup (closer to bridge)
          const bridgePickupX = bodyX + 80;
          const bridgePickup = new Graphics();
          bridgePickup.rect(bridgePickupX, bodyY - pickupHeight / 2, pickupWidth, pickupHeight);
          bridgePickup.fill(0x1a1a1a); // Black pickup
          bodyContainer.addChild(bridgePickup);

          // Pickup pole pieces
          for (let i = 0; i < 6; i++) {
            const poleY = bodyY - pickupHeight / 2 + (i + 1) * (pickupHeight / 7);
            const pole = new Graphics();
            pole.circle(bridgePickupX + pickupWidth / 2, poleY, 3);
            pole.fill(0xc0c0c0); // Silver pole pieces
            bodyContainer.addChild(pole);
          }

          // Volume/tone knobs
          const knobY = bodyY + bodyHeight * 0.35;
          const knob1X = bodyX + 30;
          const knob2X = bodyX + 60;

          [knob1X, knob2X].forEach(knobX => {
            // Knob body
            const knob = new Graphics();
            knob.circle(knobX, knobY, 8);
            knob.fill(0x2a2a2a); // Dark knob
            bodyContainer.addChild(knob);

            // Knob highlight
            const knobHighlight = new Graphics();
            knobHighlight.circle(knobX - 2, knobY - 2, 3);
            knobHighlight.fill({ color: 0xffffff, alpha: 0.3 });
            bodyContainer.addChild(knobHighlight);

            // Knob indicator line
            const indicator = new Graphics();
            indicator.moveTo(knobX, knobY);
            indicator.lineTo(knobX, knobY - 6);
            indicator.stroke({ color: 0xffffff, width: 1.5 });
            bodyContainer.addChild(indicator);
          });
        }

        fretboardContainer.addChild(bodyContainer);
      };

      // Helper function to draw headstock and tuning pegs
      // Returns array of peg positions for string routing
      const drawHeadstock = (
        headstockX: number,
        headstockY: number,
        headstockWidth: number,
        headstockHeight: number,
        style: string,
        woodColor: number,
        stringCount: number,
        stringPositions: number[] // Y positions of strings at nut
      ): { x: number; y: number }[] => {
        const headstockContainer = new Container();
        const pegColor = 0x2a2a2a; // Dark metal
        const buttonColor = 0xf5f5dc; // Bone/plastic
        const pegPositions: { x: number; y: number }[] = [];

        if (style === 'classical-slotted') {
          // Classical guitar slotted headstock - extends upward (90 degrees rotated)
          const headLength = headstockWidth * 1.5; // Length extending upward
          const headWidth = headstockHeight * 0.8; // Width (horizontal)

          // Main headstock shape - simple rectangular extending upward
          const headstock = new Graphics();
          headstock.rect(
            headstockX - headWidth / 2,
            headstockY - headLength,
            headWidth,
            headLength
          );
          headstock.fill(woodColor);
          headstockContainer.addChild(headstock);

          // Add subtle wood grain/edge detail
          const headstockEdge = new Graphics();
          headstockEdge.rect(
            headstockX - headWidth / 2,
            headstockY - headLength,
            2,
            headLength
          );
          headstockEdge.fill({ color: 0x000000, alpha: 0.2 });
          headstockContainer.addChild(headstockEdge);

          // Tuning pegs (3 per side) - arranged along the headstock
          const pegRadius = 3;
          const buttonRadius = 5;
          const pegSpacing = headLength / 4;

          for (let i = 0; i < 3; i++) {
            const pegY = headstockY - headLength * 0.8 + i * pegSpacing;

            // Left side pegs (treble strings)
            const leftPegX = headstockX - headWidth * 0.35;
            const leftPeg = new Graphics();
            leftPeg.circle(leftPegX, pegY, pegRadius);
            leftPeg.fill(pegColor);
            headstockContainer.addChild(leftPeg);

            const leftButton = new Graphics();
            leftButton.circle(leftPegX - 8, pegY, buttonRadius);
            leftButton.fill(buttonColor);
            headstockContainer.addChild(leftButton);

            pegPositions[i * 2] = { x: leftPegX, y: pegY };

            // Right side pegs (bass strings)
            const rightPegX = headstockX + headWidth * 0.35;
            const rightPeg = new Graphics();
            rightPeg.circle(rightPegX, pegY, pegRadius);
            rightPeg.fill(pegColor);
            headstockContainer.addChild(rightPeg);

            const rightButton = new Graphics();
            rightButton.circle(rightPegX + 8, pegY, buttonRadius);
            rightButton.fill(buttonColor);
            headstockContainer.addChild(rightButton);

            pegPositions[i * 2 + 1] = { x: rightPegX, y: pegY };
          }

        } else if (style === 'fender-6-inline' || style === 'ibanez-6-inline' || style === 'jackson-6-inline') {
          // 6-inline headstock (all tuners on one side) - extends upward with curved Fender shape
          const headLength = headstockWidth * 1.8; // Length extending upward
          const headWidth = headstockHeight * 0.6; // Width (horizontal)

          // Main headstock shape - Fender style with characteristic curve
          const headstock = new Graphics();

          // Draw Fender-style curved headstock shape
          const baseY = headstockY;
          const topY = headstockY - headLength;
          const leftX = headstockX - headWidth / 2;
          const rightX = headstockX + headWidth / 2;

          // Start at bottom left
          headstock.moveTo(leftX, baseY);

          // Left side - straight up
          headstock.lineTo(leftX, topY + headLength * 0.3);

          // Top curve (characteristic Fender curve)
          headstock.bezierCurveTo(
            leftX, topY + headLength * 0.15,  // Control point 1
            leftX + headWidth * 0.2, topY,     // Control point 2
            rightX - headWidth * 0.1, topY     // End point (slightly rounded top right)
          );

          // Right side - curve back down
          headstock.bezierCurveTo(
            rightX, topY + headLength * 0.05,  // Control point 1
            rightX, topY + headLength * 0.2,   // Control point 2
            rightX, baseY                       // End point (bottom right)
          );

          // Bottom edge
          headstock.lineTo(leftX, baseY);

          headstock.fill(woodColor);
          headstockContainer.addChild(headstock);

          // Add subtle edge detail for depth
          const headstockEdge = new Graphics();
          headstockEdge.moveTo(leftX, baseY);
          headstockEdge.lineTo(leftX, topY + headLength * 0.3);
          headstockEdge.stroke({ color: 0x000000, alpha: 0.2, width: 1.5 });
          headstockContainer.addChild(headstockEdge);

          // 6 tuning pegs on one side (left side)
          const pegSpacing = headLength / 7.5;
          const pegRadius = 3;
          const buttonRadius = 5;

          for (let i = 0; i < 6; i++) {
            const pegY = headstockY - headLength * 0.85 + i * pegSpacing;
            const pegX = headstockX - headWidth * 0.3;

            // Peg shaft
            const peg = new Graphics();
            peg.circle(pegX, pegY, pegRadius);
            peg.fill(pegColor);
            headstockContainer.addChild(peg);

            // Tuning button
            const button = new Graphics();
            button.circle(pegX - 8, pegY, buttonRadius);
            button.fill(buttonColor);
            headstockContainer.addChild(button);

            pegPositions[i] = { x: pegX, y: pegY };
          }

        } else if (style === 'gibson-3x3' || style === 'martin-3x3' || style === 'prs-3x3') {
          // 3x3 headstock (3 tuners per side)
          const headWidth = headstockWidth;
          const headHeight = headstockHeight;

          // Determine if this is electric (asymmetric) or acoustic (flat)
          const isElectric = style === 'gibson-3x3' || style === 'prs-3x3';
          const angleOffset = isElectric ? 25 : 0; // Electric guitars have angled headstock

          // Main headstock shape (Gibson/Martin style with angled top)
          const headstock = new Graphics();
          headstock.moveTo(headstockX, headstockY - headHeight / 2);
          headstock.lineTo(headstockX - headWidth * 0.4 - angleOffset * 0.4, headstockY - headHeight / 1.8);
          headstock.lineTo(headstockX - headWidth * 0.6 - angleOffset * 0.6, headstockY - headHeight / 1.8);
          headstock.lineTo(headstockX - headWidth - angleOffset, headstockY - headHeight / 2);
          headstock.lineTo(headstockX - headWidth - angleOffset, headstockY + headHeight / 2);
          headstock.lineTo(headstockX, headstockY + headHeight / 2);
          headstock.lineTo(headstockX, headstockY - headHeight / 2);
          headstock.fill(woodColor);
          headstockContainer.addChild(headstock);

          // 3 tuning pegs per side
          const pegSpacing = headHeight / 4;
          const pegRadius = 4;
          const buttonRadius = 6;

          for (let i = 0; i < 3; i++) {
            const pegY = headstockY - headHeight / 3 + i * pegSpacing;

            // Left side pegs (treble side)
            const leftPegX = headstockX - headWidth * 0.2 - angleOffset * 0.2;
            const leftPeg = new Graphics();
            leftPeg.circle(leftPegX, pegY, pegRadius);
            leftPeg.fill(pegColor);
            headstockContainer.addChild(leftPeg);

            const leftButton = new Graphics();
            leftButton.circle(leftPegX + 10, pegY, buttonRadius);
            leftButton.fill(buttonColor);
            headstockContainer.addChild(leftButton);

            pegPositions[i] = { x: leftPegX, y: pegY };

            // Right side pegs (bass side)
            const rightPegX = headstockX - headWidth * 0.8 - angleOffset * 0.8;
            const rightPeg = new Graphics();
            rightPeg.circle(rightPegX, pegY, pegRadius);
            rightPeg.fill(pegColor);
            headstockContainer.addChild(rightPeg);

            const rightButton = new Graphics();
            rightButton.circle(rightPegX - 10, pegY, buttonRadius);
            rightButton.fill(buttonColor);
            headstockContainer.addChild(rightButton);

            pegPositions[i + 3] = { x: rightPegX, y: pegY };
          }
        }

        fretboardContainer.addChild(headstockContainer);
        return pegPositions;
      };

      // Calculate neck width based on guitar model
      // Use nutWidth and bridgeWidth if available, otherwise use defaults
      const nutWidth = guitarStyle.nutWidth || 43;
      const bridgeWidth = guitarStyle.bridgeWidth || 55;

      // Use most of the available height for the neck
      const availableHeight = height - topMargin - bottomMargin;
      const scaledHeight = availableHeight * 0.85; // Use 85% of available height for neck

      // Base string spacing at the nut
      const baseStringSpacing = scaledHeight / (stringCount + 1);

      const calculateFretPosition = (fretNumber: number): number => {
        const playableWidth = width - labelWidth - rightMargin;
        let position: number;

        if (spacingMode === 'realistic') {
          // Logarithmic fret spacing: frets get closer together as you move up the neck
          // Formula: position = scaleLength * (1 - 2^(-fret/12))
          // This makes fret 0-1 large, then progressively smaller
          const guitarNeckLength = playableWidth / (1 - Math.pow(0.5, fretCount / 12));

          // Calculate position using standard formula (fret 0 at left, fret 22 at right)
          position = labelWidth + guitarNeckLength * (1 - Math.pow(0.5, fretNumber / 12));

          // When flipped=true, we want nut on LEFT and bridge on RIGHT
          // But the formula naturally puts fret 0 on the left with LARGE spacing
          // So when flipped, we DON'T flip - we keep it as is!
          // When NOT flipped, we need to mirror it
          if (!flipped) {
            position = width - position;
          }
        } else {
          const fretSpacing = playableWidth / fretCount;
          position = labelWidth + fretNumber * fretSpacing;

          // Flip if needed (mirror around center)
          if (flipped) {
            position = width - position;
          }
        }

        return position;
      };

      // Calculate neck width at different positions (increases towards bridge)
      const getNeckWidthAtFret = (fretNumber: number): number => {
        const t = fretNumber / fretCount; // 0 at nut, 1 at bridge
        // Linear interpolation from nut width to bridge width
        return nutWidth + (bridgeWidth - nutWidth) * t;
      };

      const fretMarkers = new Set([3, 5, 7, 9, 12, 15, 17, 19, 21, 24]);

      // Draw trapezoid fretboard background (neck wood)
      // The neck tapers from narrow at nut to wide at bridge
      const nutXRaw = calculateFretPosition(0);
      const bridgeXRaw = calculateFretPosition(fretCount);
      const centerY = topMargin + (height - topMargin - bottomMargin) / 2;

      // After the fret position fix, fret 0 is now on the LEFT and fret 22 is on the RIGHT
      // So nutXRaw is left position, bridgeXRaw is right position
      const leftX = nutXRaw;
      const rightX = bridgeXRaw;

      // Calculate the actual visual heights at nut and bridge
      const nutVisualHeight = scaledHeight * (nutWidth / bridgeWidth);
      const bridgeVisualHeight = scaledHeight * (bridgeWidth / bridgeWidth); // = scaledHeight

      // Trapezoid coordinates: nut (narrow) on left, bridge (wide) on right
      const trapezoidPoints = [
        leftX, centerY - nutVisualHeight / 2,        // Top-left (nut top - narrow)
        rightX, centerY - bridgeVisualHeight / 2,    // Top-right (bridge top - wide)
        rightX, centerY + bridgeVisualHeight / 2,    // Bottom-right (bridge bottom - wide)
        leftX, centerY + nutVisualHeight / 2,        // Bottom-left (nut bottom - narrow)
      ];

      // Draw guitar body portion (before the neck)
      // Position the body at the right edge (bridge end)
      // Body is WIDER than the neck at the bridge
      // Draw strum zone (green rectangle) instead of guitar body
      const strumZoneWidth = 100; // Width of strum zone
      const strumZoneHeight = bridgeVisualHeight * 1.2; // Height matches string spread
      const strumZoneX = rightX; // Start at bridge position
      const strumZoneY = centerY;

      const strumZone = new Graphics();
      strumZone.rect(
        strumZoneX,
        strumZoneY - strumZoneHeight / 2,
        strumZoneWidth,
        strumZoneHeight
      );
      strumZone.fill({ color: 0x00ff00, alpha: 0.2 }); // Semi-transparent green
      fretboardContainer.addChild(strumZone);

      // Calculate string positions at nut for routing to tuning pegs
      const stringPositionsAtNut: number[] = [];
      for (let s = 0; s < stringCount; s++) {
        const t = s / (stringCount - 1);
        const stringY = centerY - nutVisualHeight / 2 + t * nutVisualHeight;
        stringPositionsAtNut.push(stringY);
      }

      // Main fretboard wood background (draw BEFORE headstock so headstock appears on top)
      const fretboardWood = new Graphics();
      fretboardWood.poly(trapezoidPoints);
      fretboardWood.fill(guitarStyle.woodColor);
      fretboardContainer.addChild(fretboardWood);

      // Enhanced wood grain effect with realistic patterns
      // Create a noise-based seed for consistent randomness
      const seededRandom = (seed: number) => {
        const x = Math.sin(seed) * 10000;
        return x - Math.floor(x);
      };

      // Add subtle color variation to the wood base
      const woodVariationOverlay = new Graphics();
      const variationPatches = 30;
      for (let i = 0; i < variationPatches; i++) {
        const seed = i * 123.456;
        const patchX = leftX + seededRandom(seed) * (rightX - leftX);
        const patchY = centerY - bridgeVisualHeight / 2 + seededRandom(seed + 1) * bridgeVisualHeight;
        const patchSize = 20 + seededRandom(seed + 2) * 40;
        const colorVariation = seededRandom(seed + 3) > 0.5 ? 0x0a0604 : -0x0a0604;
        const varColor = Math.max(0, Math.min(0xffffff, guitarStyle.woodColor + colorVariation));

        woodVariationOverlay.circle(patchX, patchY, patchSize);
        woodVariationOverlay.fill({ color: varColor, alpha: 0.15 });
      }
      fretboardContainer.addChild(woodVariationOverlay);

      // Draw realistic wood grain lines with natural variation
      const grainLines = 150;
      for (let i = 0; i < grainLines; i++) {
        const seed = i * 789.123;
        const t = i / grainLines;
        const baseY = centerY - bridgeVisualHeight / 2 + t * bridgeVisualHeight;

        // Add natural waviness to grain lines
        const waveAmplitude = 0.5 + seededRandom(seed) * 1.5;
        const waveFrequency = 0.02 + seededRandom(seed + 1) * 0.03;

        // Vary grain line density (some areas denser than others)
        const densityThreshold = 0.3 + Math.abs(Math.sin(t * Math.PI * 3)) * 0.4;
        if (seededRandom(seed + 2) > densityThreshold) {
          const woodGrainLine = new Graphics();

          // Draw wavy grain line across the neck
          const segments = 50;
          for (let j = 0; j <= segments; j++) {
            const xProgress = j / segments;
            const x = leftX + xProgress * (rightX - leftX);
            const y = baseY + Math.sin(xProgress * Math.PI * 2 * waveFrequency + seed) * waveAmplitude;

            if (j === 0) {
              woodGrainLine.moveTo(x, y);
            } else {
              woodGrainLine.lineTo(x, y);
            }
          }

          // Vary grain line darkness and thickness
          const opacity = 0.03 + seededRandom(seed + 3) * 0.12;
          const lineWidth = 0.3 + seededRandom(seed + 4) * 0.8;
          const darkerColor = Math.max(0, guitarStyle.woodColor - (0x0f0a08 + Math.floor(seededRandom(seed + 5) * 0x0a0805)));

          woodGrainLine.stroke({ color: darkerColor, width: lineWidth, alpha: opacity });
          fretboardContainer.addChild(woodGrainLine);
        }
      }

      // Add occasional darker knots and imperfections
      const knots = 3 + Math.floor(seededRandom(456) * 4);
      for (let i = 0; i < knots; i++) {
        const seed = i * 234.567;
        const knotX = leftX + seededRandom(seed) * (rightX - leftX);
        const knotY = centerY - bridgeVisualHeight / 2 + seededRandom(seed + 1) * bridgeVisualHeight;
        const knotSize = 3 + seededRandom(seed + 2) * 8;

        // Draw concentric circles for knot effect
        const knotRings = 3 + Math.floor(seededRandom(seed + 3) * 3);
        for (let ring = 0; ring < knotRings; ring++) {
          const knotGraphic = new Graphics();
          const ringRadius = knotSize * (1 - ring / knotRings);
          knotGraphic.circle(knotX, knotY, ringRadius);
          const knotDarkness = 0x1a1410 + Math.floor(ring * 0x050402);
          const knotColor = Math.max(0, guitarStyle.woodColor - knotDarkness);
          knotGraphic.fill({ color: knotColor, alpha: 0.3 - ring * 0.08 });
          fretboardContainer.addChild(knotGraphic);
        }
      }

      // Add subtle vertical grain direction (wood fibers run lengthwise)
      const verticalGrainLines = 40;
      for (let i = 0; i < verticalGrainLines; i++) {
        const seed = i * 345.678;
        if (seededRandom(seed) > 0.6) {
          const x = leftX + seededRandom(seed + 1) * (rightX - leftX);
          const verticalGrain = new Graphics();

          // Draw subtle vertical line with slight curve
          const segments = 30;
          for (let j = 0; j <= segments; j++) {
            const yProgress = j / segments;
            const y = centerY - bridgeVisualHeight / 2 + yProgress * bridgeVisualHeight;
            const xOffset = Math.sin(yProgress * Math.PI * 2 + seed) * 0.5;

            if (j === 0) {
              verticalGrain.moveTo(x + xOffset, y);
            } else {
              verticalGrain.lineTo(x + xOffset, y);
            }
          }

          const vGrainColor = Math.max(0, guitarStyle.woodColor - 0x0a0805);
          verticalGrain.stroke({ color: vGrainColor, width: 0.2, alpha: 0.04 });
          fretboardContainer.addChild(verticalGrain);
        }
      }

      // Function to draw inlays based on style
      const drawInlay = (x: number, y: number, style: string, color: number, radius: number) => {
        const inlay = new Graphics();

        switch (style) {
          case 'dots':
            // Simple circular dot
            inlay.circle(x, y, radius);
            inlay.fill({ color });
            break;

          case 'blocks':
            // Rectangular block
            inlay.rect(x - radius * 0.8, y - radius * 0.6, radius * 1.6, radius * 1.2);
            inlay.fill({ color });
            break;

          case 'trapezoid':
            // Trapezoid shape (wider at top)
            inlay.poly([
              x - radius * 0.6, y - radius * 0.8,
              x + radius * 0.6, y - radius * 0.8,
              x + radius * 0.4, y + radius * 0.8,
              x - radius * 0.4, y + radius * 0.8,
            ]);
            inlay.fill({ color });
            break;

          case 'triangle':
            // Triangle pointing up
            inlay.poly([
              x, y - radius * 0.9,
              x + radius * 0.8, y + radius * 0.6,
              x - radius * 0.8, y + radius * 0.6,
            ]);
            inlay.fill({ color });
            break;

          case 'abalone':
            // Abalone shell effect (oval with shimmer)
            inlay.ellipse(x, y, radius * 1.2, radius * 0.8);
            inlay.fill({ color });
            break;

          case 'tree':
            // Tree/pine tree shape
            inlay.poly([
              x, y - radius * 0.9,
              x + radius * 0.3, y - radius * 0.3,
              x + radius * 0.6, y + radius * 0.3,
              x + radius * 0.2, y + radius * 0.3,
              x + radius * 0.5, y + radius * 0.9,
              x - radius * 0.5, y + radius * 0.9,
              x - radius * 0.2, y + radius * 0.3,
              x - radius * 0.6, y + radius * 0.3,
              x - radius * 0.3, y - radius * 0.3,
            ]);
            inlay.fill({ color });
            break;

          case 'crown':
            // Crown shape
            inlay.poly([
              x - radius * 0.8, y + radius * 0.8,
              x - radius * 0.5, y - radius * 0.4,
              x - radius * 0.2, y + radius * 0.2,
              x, y - radius * 0.8,
              x + radius * 0.2, y + radius * 0.2,
              x + radius * 0.5, y - radius * 0.4,
              x + radius * 0.8, y + radius * 0.8,
            ]);
            inlay.fill({ color });
            break;

          default:
            // Fallback to dots
            inlay.circle(x, y, radius);
            inlay.fill({ color });
        }

        return inlay;
      };

      // Draw frets with realistic appearance
      for (let i = 0; i <= fretCount; i++) {
        const x = calculateFretPosition(i);
        const isNut = i === 0;

        // Calculate neck width at this fret position for proper scaling
        const neckWidthAtFret = getNeckWidthAtFret(i);
        const widthRatio = neckWidthAtFret / bridgeWidth; // Use bridge width as reference (max width)
        const scaledFretHeight = scaledHeight * widthRatio;

        // Center the fret vertically within the available space
        const fretCenterY = topMargin + (height - topMargin - bottomMargin) / 2;
        const fretY = fretCenterY - scaledFretHeight / 2;
        const fretHeight = scaledFretHeight;

        if (isNut) {
          // Realistic nut - slightly thicker than frets but not too large
          const nutWidth = 6;

          // Main nut body
          const nutBody = new Graphics();
          nutBody.rect(x - nutWidth / 2, fretY - 10, nutWidth, fretHeight + 20);
          nutBody.fill(guitarStyle.nutColor);
          fretboardContainer.addChild(nutBody);

          // Left highlight (light reflection)
          const nutHighlight = new Graphics();
          nutHighlight.rect(x - nutWidth / 2, fretY - 10, 2, fretHeight + 20);
          nutHighlight.fill({ color: 0xffffff, alpha: 0.5 });
          fretboardContainer.addChild(nutHighlight);

          // Right shadow (depth)
          const nutShadow = new Graphics();
          nutShadow.rect(x + nutWidth / 2 - 3, fretY - 10, 3, fretHeight + 20);
          nutShadow.fill({ color: 0x000000, alpha: 0.4 });
          fretboardContainer.addChild(nutShadow);

          // Center line for 3D effect
          const nutCenter = new Graphics();
          nutCenter.rect(x - 1, fretY - 10, 2, fretHeight + 20);
          nutCenter.fill({ color: 0xffffff, alpha: 0.2 });
          fretboardContainer.addChild(nutCenter);
        } else {
          // Regular frets - enhanced metallic appearance with crown effect
          const fretWidth = 2.8;
          const fretColor = guitarStyle.fretColor;

          // Main fret body with gradient effect
          const fret = new Graphics();
          fret.rect(x - fretWidth / 2, fretY, fretWidth, fretHeight);
          fret.fill(fretColor);
          fretboardContainer.addChild(fret);

          // Center highlight (crown peak - bright reflection)
          const fretCenterHighlight = new Graphics();
          fretCenterHighlight.rect(x - fretWidth / 4, fretY, fretWidth / 2, fretHeight);
          fretCenterHighlight.fill({ color: 0xffffff, alpha: 0.7 });
          fretboardContainer.addChild(fretCenterHighlight);

          // Left bevel (darker side)
          const fretLeftBevel = new Graphics();
          fretLeftBevel.rect(x - fretWidth / 2, fretY, fretWidth / 4, fretHeight);
          fretLeftBevel.fill({ color: 0x4a4a4a, alpha: 0.4 });
          fretboardContainer.addChild(fretLeftBevel);

          // Right bevel (shadow side)
          const fretRightBevel = new Graphics();
          fretRightBevel.rect(x + fretWidth / 4, fretY, fretWidth / 4, fretHeight);
          fretRightBevel.fill({ color: 0x1a1a1a, alpha: 0.5 });
          fretboardContainer.addChild(fretRightBevel);

          // Fine edge highlight for metallic sheen
          const fretEdgeHighlight = new Graphics();
          fretEdgeHighlight.rect(x - fretWidth / 2 + 0.3, fretY, 0.4, fretHeight);
          fretEdgeHighlight.fill({ color: 0xffffff, alpha: 0.4 });
          fretboardContainer.addChild(fretEdgeHighlight);
        }

        if (fretMarkers.has(i) && i > 0) {
          const x1 = calculateFretPosition(i - 1);
          const x2 = calculateFretPosition(i);
          const markerX = (x1 + x2) / 2;
          const markerY = topMargin + (height - 60) / 2;
          const markerRadius = 5.5;
          const inlayStyle = guitarStyle.inlayStyle || 'dots';
          const inlayColor = guitarStyle.inlayColor || guitarStyle.markerColor;

          // Glow effect (soft halo around marker)
          const markerGlow = new Graphics();
          markerGlow.circle(markerX, markerY, markerRadius * 1.8);
          markerGlow.fill({ color: inlayColor, alpha: 0.15 });
          fretboardContainer.addChild(markerGlow);

          // Draw inlay based on guitar model style
          const inlay = drawInlay(markerX, markerY, inlayStyle, inlayColor, markerRadius);
          fretboardContainer.addChild(inlay);

          // Iridescent highlight (pearl shimmer)
          const markerShimmer = new Graphics();
          markerShimmer.circle(markerX - 1.5, markerY - 1.5, markerRadius * 0.6);
          markerShimmer.fill({ color: 0xffffff, alpha: 0.6 });
          fretboardContainer.addChild(markerShimmer);

          // Subtle shadow for depth
          const markerShadow = new Graphics();
          markerShadow.circle(markerX + 1, markerY + 1, markerRadius * 0.4);
          markerShadow.fill({ color: 0x000000, alpha: 0.2 });
          fretboardContainer.addChild(markerShadow);

          // Double marker at 12th fret
          if (i === 12) {
            const markerY2 = topMargin + (height - 60) * 0.75;

            // Glow for second marker
            const markerGlow2 = new Graphics();
            markerGlow2.circle(markerX, markerY2, markerRadius * 1.8);
            markerGlow2.fill({ color: inlayColor, alpha: 0.15 });
            fretboardContainer.addChild(markerGlow2);

            // Draw second inlay
            const inlay2 = drawInlay(markerX, markerY2, inlayStyle, inlayColor, markerRadius);
            fretboardContainer.addChild(inlay2);

            const markerShimmer2 = new Graphics();
            markerShimmer2.circle(markerX - 1.5, markerY2 - 1.5, markerRadius * 0.6);
            markerShimmer2.fill({ color: 0xffffff, alpha: 0.6 });
            fretboardContainer.addChild(markerShimmer2);

            const markerShadow2 = new Graphics();
            markerShadow2.circle(markerX + 1, markerY2 + 1, markerRadius * 0.4);
            markerShadow2.fill({ color: 0x000000, alpha: 0.2 });
            fretboardContainer.addChild(markerShadow2);
          }
        }
      }

      // Draw bridge at the end (realistic bridge based on guitar type)
      const bridgeNeckWidth = getNeckWidthAtFret(fretCount);
      const bridgeWidthRatio = bridgeNeckWidth / bridgeWidth;
      const bridgeHeight = scaledHeight * bridgeWidthRatio;
      const bridgeCenterY = topMargin + (height - topMargin - bottomMargin) / 2;
      const bridgeY = bridgeCenterY - bridgeHeight / 2;

      if (guitarStyle.category === 'classical' || guitarStyle.category === 'acoustic') {
        // Classical/Acoustic bridge (wooden, wider)
        const bridgeThickness = 12;
        const bridgeWoodColor = Math.max(0, guitarStyle.woodColor - 0x1a1410);

        // Main bridge body (wood)
        const bridgeBody = new Graphics();
        bridgeBody.rect(rightX - bridgeThickness / 2, bridgeY - 8, bridgeThickness, bridgeHeight + 16);
        bridgeBody.fill(bridgeWoodColor);
        fretboardContainer.addChild(bridgeBody);

        // Bridge saddle (bone/plastic, lighter color)
        const saddleThickness = 3;
        const saddleBody = new Graphics();
        saddleBody.rect(rightX - saddleThickness / 2, bridgeY - 5, saddleThickness, bridgeHeight + 10);
        saddleBody.fill(0xf5f5dc); // Bone color
        fretboardContainer.addChild(saddleBody);

        // Saddle highlight
        const saddleHighlight = new Graphics();
        saddleHighlight.rect(rightX - saddleThickness / 2, bridgeY - 5, 1, bridgeHeight + 10);
        saddleHighlight.fill({ color: 0xffffff, alpha: 0.4 });
        fretboardContainer.addChild(saddleHighlight);

      } else {
        // Electric bridge (metallic, more compact)
        const bridgeThickness = 8;

        // Main bridge body (metal)
        const bridgeBody = new Graphics();
        bridgeBody.rect(rightX - bridgeThickness / 2, bridgeY - 5, bridgeThickness, bridgeHeight + 10);
        bridgeBody.fill({ color: 0x2a2a2a }); // Dark metallic
        fretboardContainer.addChild(bridgeBody);

        // Bridge highlight (metallic shine)
        const bridgeHighlight = new Graphics();
        bridgeHighlight.rect(rightX - bridgeThickness / 2, bridgeY - 5, 2, bridgeHeight + 10);
        bridgeHighlight.fill({ color: 0xffffff, alpha: 0.3 });
        fretboardContainer.addChild(bridgeHighlight);

        // Bridge shadow
        const bridgeShadow = new Graphics();
        bridgeShadow.rect(rightX + bridgeThickness / 2 - 2, bridgeY - 5, 2, bridgeHeight + 10);
        bridgeShadow.fill({ color: 0x000000, alpha: 0.5 });
        fretboardContainer.addChild(bridgeShadow);

        // Individual saddles for each string (electric guitars)
        for (let s = 0; s < stringCount; s++) {
          const stringSpacing = bridgeHeight / (stringCount + 1);
          const saddleY = bridgeY + (s + 1) * stringSpacing;
          const saddle = new Graphics();
          saddle.rect(rightX - 4, saddleY - 2, 8, 4);
          saddle.fill({ color: 0x696969 }); // Gray metal
          fretboardContainer.addChild(saddle);

          // Saddle screw (detail)
          const screw = new Graphics();
          screw.circle(rightX, saddleY, 1);
          screw.fill({ color: 0x404040 });
          fretboardContainer.addChild(screw);
        }
      }

      // Headstock removed - not needed for this view
      // Create empty peg positions array (strings will start at nut)
      const pegPositions: { x: number; y: number }[] = stringPositionsAtNut.map((y) => ({ x: leftX, y }));

      // Draw strings with realistic cylindrical appearance and curves
      // Calculate string Y positions with dynamic spacing (wider at bridge)
      const getStringY = (stringIndex: number): number => {
        // Calculate average Y position across the neck (centered and symmetric)
        let totalY = 0;
        const samples = 10;
        const stringCenterY = topMargin + (height - topMargin - bottomMargin) / 2;

        for (let sample = 0; sample <= samples; sample++) {
          const fretPos = (sample / samples) * fretCount;
          const neckWidthAtFret = getNeckWidthAtFret(fretPos);
          const widthRatio = neckWidthAtFret / bridgeWidth; // Use bridge width as reference
          const scaledNeckHeight = scaledHeight * widthRatio;
          const scaledSpacing = scaledNeckHeight / (stringCount + 1);
          const stringAreaStart = stringCenterY - scaledNeckHeight / 2;

          totalY += stringAreaStart + (stringIndex + 1) * scaledSpacing;
        }
        return totalY / (samples + 1);
      };

      for (let i = 0; i < stringCount; i++) {
        // For left-handed, reverse the string order
        const stringIndex = leftHanded ? stringCount - 1 - i : i;

        // Strings extend from tuning pegs to through the body
        // Get peg position for this string (if available)
        const hasPegPositions = pegPositions && pegPositions.length > 0;
        const pegPos = hasPegPositions && pegPositions[i]
          ? pegPositions[i]
          : { x: leftX - 50, y: stringPositionsAtNut[i] };

        // String starts at tuning peg, goes through nut, across frets, through bridge, into strum zone
        const stringStartX = hasPegPositions ? pegPos.x : leftX; // Start at tuning peg or nut
        const stringEndX = rightX + strumZoneWidth; // Extend through strum zone
        const stringLength = Math.abs(stringEndX - stringStartX);

        // Determine string material and properties based on guitar type
        const isClassical = guitarStyle.category === 'classical';
        const isAcoustic = guitarStyle.category === 'acoustic';
        const isElectric = guitarStyle.category === 'electric';

        // String winding and material properties
        // Classical: strings 1-3 are nylon (unwound), 4-6 are nylon core with metal wrap
        // Acoustic/Electric: strings 1-2 are plain steel, 3-6 are wound
        let isWound: boolean;
        let stringMaterial: 'nylon' | 'steel';
        let stringThickness: number;

        if (isClassical) {
          // Classical guitar: nylon strings
          stringMaterial = 'nylon';
          isWound = stringIndex <= 2; // Low E, A, D are wound (bass strings)
          // Nylon strings are thicker than steel
          // Gauges: High E=0.71mm, B=0.81mm, G=1.02mm, D=0.76mm wound, A=0.84mm wound, Low E=1.09mm wound
          const nylonGauges = [1.09, 0.84, 0.76, 1.02, 0.81, 0.71]; // Low E to High E
          stringThickness = nylonGauges[stringIndex] * 2.5; // Scale for visibility
        } else {
          // Acoustic/Electric: steel strings
          stringMaterial = 'steel';
          isWound = stringIndex <= 3; // Low E, A, D, G are wound
          // Steel string gauges (light set): High E=0.25mm, B=0.33mm, G=0.43mm, D=0.66mm, A=0.91mm, Low E=1.17mm
          const steelGauges = [1.17, 0.91, 0.66, 0.43, 0.33, 0.25]; // Low E to High E
          stringThickness = steelGauges[stringIndex] * 2.8; // Scale for visibility
        }

        // Realistic string curve (slight bow due to tension)
        const curveAmount = (stringIndex - stringCount / 2) * 0.3; // Slight curve based on string position

        // Fan-out effect: strings should be parallel at the nut and fan out symmetrically toward the bridge
        // This creates a trapezoidal string pattern
        // The fan-out is achieved by the neck taper itself - strings follow the fretboard width
        // No additional angular offset is needed for realistic appearance

        // Main string body with realistic curve and dynamic width
        const stringBody = new Graphics();

        // Draw curved string using multiple line segments for smooth appearance
        const segments = Math.ceil(stringLength / 5);

        // Top edge of string (follows neck taper and fans out with angle)
        for (let seg = 0; seg <= segments; seg++) {
          const t = seg / segments;
          const currentX = stringStartX + t * stringLength;

          let by: number;

          // Before nut: straight line from peg to nut (only if we have peg positions)
          if (hasPegPositions && currentX < leftX) {
            const pegT = (currentX - stringStartX) / (leftX - stringStartX);
            by = pegPos.y + pegT * (stringPositionsAtNut[i] - pegPos.y);
          }
          // After bridge: straight line into body
          else if (currentX > rightX) {
            const bodyT = (currentX - rightX) / (stringEndX - rightX);
            // String continues at bridge height into body
            const bridgeStringY = centerY - bridgeVisualHeight / 2 + (i / (stringCount - 1)) * bridgeVisualHeight;
            by = bridgeStringY; // Maintain bridge height into body
          }
          // Between nut and bridge: follow fretboard taper
          else {
            const fretAtSegment = hasPegPositions
              ? ((currentX - leftX) / (rightX - leftX)) * fretCount
              : t * fretCount;
            const neckWidthAtSegment = getNeckWidthAtFret(fretAtSegment);
            const widthRatio = neckWidthAtSegment / bridgeWidth;
            const scaledNeckHeight = scaledHeight * widthRatio;
            const scaledSpacing = scaledNeckHeight / (stringCount + 1);
            const stringAreaStart = centerY - scaledNeckHeight / 2;
            by = stringAreaStart + (stringIndex + 1) * scaledSpacing + (stringIndex - stringCount / 2) * curveAmount * t;
          }

          if (seg === 0) {
            stringBody.moveTo(currentX, by - stringThickness / 2);
          } else {
            stringBody.lineTo(currentX, by - stringThickness / 2);
          }
        }

        // Bottom edge of string (reverse order)
        for (let seg = segments; seg >= 0; seg--) {
          const t = seg / segments;
          const currentX = stringStartX + t * stringLength;

          let by: number;

          // Before nut: straight line from peg to nut (only if we have peg positions)
          if (hasPegPositions && currentX < leftX) {
            const pegT = (currentX - stringStartX) / (leftX - stringStartX);
            by = pegPos.y + pegT * (stringPositionsAtNut[i] - pegPos.y);
          }
          // After bridge: straight line into body
          else if (currentX > rightX) {
            const bridgeStringY = centerY - bridgeVisualHeight / 2 + (i / (stringCount - 1)) * bridgeVisualHeight;
            by = bridgeStringY;
          }
          // Between nut and bridge: follow fretboard taper
          else {
            const fretAtSegment = hasPegPositions
              ? ((currentX - leftX) / (rightX - leftX)) * fretCount
              : t * fretCount;
            const neckWidthAtSegment = getNeckWidthAtFret(fretAtSegment);
            const widthRatio = neckWidthAtSegment / bridgeWidth;
            const scaledNeckHeight = scaledHeight * widthRatio;
            const scaledSpacing = scaledNeckHeight / (stringCount + 1);
            const stringAreaStart = centerY - scaledNeckHeight / 2;
            by = stringAreaStart + (stringIndex + 1) * scaledSpacing + (stringIndex - stringCount / 2) * curveAmount * t;
          }

          stringBody.lineTo(currentX, by + stringThickness / 2);
        }

        stringBody.closePath();

        // Use different colors for nylon vs steel strings
        let stringColor: number;
        if (stringMaterial === 'nylon') {
          // Nylon strings: translucent with slight amber/cream tint
          // Treble strings (unwound) are more translucent, bass strings (wound) are darker
          stringColor = isWound ? 0x8b7355 : 0xf5f5dc; // Wound: bronze-ish, Plain: cream/beige
        } else {
          // Steel strings: metallic silver/gray
          stringColor = isWound ? 0x8b7355 : 0xc0c0c0; // Wound: bronze, Plain: silver
        }

        stringBody.fill({ color: stringColor, alpha: isWound ? 0.9 : (stringMaterial === 'nylon' ? 0.7 : 0.85) });
        fretboardContainer.addChild(stringBody);

        // Wrapped wire pattern for wound strings - more realistic
        if (isWound) {
          // Adjust wrap spacing based on string material
          const wrapSpacing = stringMaterial === 'nylon' ? 5 : 6; // Nylon wound strings have tighter wrapping
          const wrapColor = stringMaterial === 'nylon' ? 0x8b7355 : 0x3a3a3a; // Bronze for nylon, dark for steel

          for (let x = stringStartX; x < stringStartX + stringLength; x += wrapSpacing) {
            const t = (x - stringStartX) / stringLength;
            const fretAtSegment = t * fretCount;
            const neckWidthAtSegment = getNeckWidthAtFret(fretAtSegment);
            const widthRatio = neckWidthAtSegment / bridgeWidth; // Use bridge width as reference
            const scaledNeckHeight = scaledHeight * widthRatio;
            const scaledSpacing = scaledNeckHeight / (stringCount + 1);

            // Center the strings vertically
            const stringDrawCenterY = topMargin + (height - topMargin - bottomMargin) / 2;
            const stringAreaStart = stringDrawCenterY - scaledNeckHeight / 2;

            // Calculate base Y position (centered and symmetric)
            const curveY = stringAreaStart + (stringIndex + 1) * scaledSpacing + (stringIndex - stringCount / 2) * curveAmount * t;

            // Main wrap line
            const wrapLine = new Graphics();
            wrapLine.rect(x, curveY - stringThickness / 2 - 0.5, 1.2, stringThickness + 1);
            wrapLine.fill({ color: wrapColor, alpha: 0.5 });
            fretboardContainer.addChild(wrapLine);

            // Wrap highlight
            const wrapHighlight = new Graphics();
            wrapHighlight.rect(x + 0.3, curveY - stringThickness / 2 - 0.3, 0.4, stringThickness * 0.6);
            wrapHighlight.fill({ color: 0xffffff, alpha: stringMaterial === 'nylon' ? 0.3 : 0.4 });
            fretboardContainer.addChild(wrapHighlight);
          }
        }

        // Top highlight (light reflection on top of string) - improved
        const stringHighlight = new Graphics();
        const highlightSegments = Math.ceil(stringLength / 5);
        for (let seg = 0; seg <= highlightSegments; seg++) {
          const t = seg / highlightSegments;
          const fretAtSegment = t * fretCount;
          const neckWidthAtSegment = getNeckWidthAtFret(fretAtSegment);
          const widthRatio = neckWidthAtSegment / bridgeWidth; // Use bridge width as reference
          const scaledNeckHeight = scaledHeight * widthRatio;
          const scaledSpacing = scaledNeckHeight / (stringCount + 1);

          // Center the strings vertically
          const stringPosCalcCenterY = topMargin + (height - topMargin - bottomMargin) / 2;
          const stringAreaStart = stringPosCalcCenterY - scaledNeckHeight / 2;

          // Calculate base Y position (centered and symmetric)
          const bx = stringStartX + t * stringLength;
          const by = stringAreaStart + (stringIndex + 1) * scaledSpacing + (stringIndex - stringCount / 2) * curveAmount * t;

          if (seg === 0) {
            stringHighlight.moveTo(bx, by - stringThickness / 2 - 0.3);
          } else {
            stringHighlight.lineTo(bx, by - stringThickness / 2 - 0.3);
          }
        }
        stringHighlight.stroke({ color: 0xffffff, width: 1.2, alpha: 0.8 });
        fretboardContainer.addChild(stringHighlight);

        // Bottom shadow (depth) - improved
        const stringShadow = new Graphics();
        for (let seg = 0; seg <= highlightSegments; seg++) {
          const t = seg / highlightSegments;
          const fretAtSegment = t * fretCount;
          const neckWidthAtSegment = getNeckWidthAtFret(fretAtSegment);
          const widthRatio = neckWidthAtSegment / bridgeWidth; // Use bridge width as reference
          const scaledNeckHeight = scaledHeight * widthRatio;
          const scaledSpacing = scaledNeckHeight / (stringCount + 1);

          // Center the strings vertically
          const nutStringCenterY = topMargin + (height - topMargin - bottomMargin) / 2;
          const stringAreaStart = nutStringCenterY - scaledNeckHeight / 2;

          // Calculate base Y position (centered and symmetric)
          const bx = stringStartX + t * stringLength;
          const by = stringAreaStart + (stringIndex + 1) * scaledSpacing + (stringIndex - stringCount / 2) * curveAmount * t;

          if (seg === 0) {
            stringShadow.moveTo(bx, by + stringThickness / 2 + 0.3);
          } else {
            stringShadow.lineTo(bx, by + stringThickness / 2 + 0.3);
          }
        }
        stringShadow.stroke({ color: 0x000000, width: 0.8, alpha: 0.6 });
        fretboardContainer.addChild(stringShadow);

        // Subtle AO shadow under string (simplified - just at end)
        const t = 1;
        const fretAtEnd = t * fretCount;
        const neckWidthAtEnd = getNeckWidthAtFret(fretAtEnd);
        const widthRatioEnd = neckWidthAtEnd / bridgeWidth; // Use bridge width as reference
        const scaledNeckHeightEnd = scaledHeight * widthRatioEnd;
        const scaledSpacingEnd = scaledNeckHeightEnd / (stringCount + 1);

        // Center the strings vertically
        const shadowCalcCenterY = topMargin + (height - topMargin - bottomMargin) / 2;
        const stringAreaStartEnd = shadowCalcCenterY - scaledNeckHeightEnd / 2;
        const shadowY = stringAreaStartEnd + (stringIndex + 1) * scaledSpacingEnd + (stringIndex - stringCount / 2) * curveAmount * t;

        const stringAO = new Graphics();
        stringAO.rect(stringStartX, shadowY + stringThickness / 2 + 0.5, stringLength, 1.5);
        stringAO.fill({ color: 0x000000, alpha: 0.15 });
        fretboardContainer.addChild(stringAO);

        if (showStringLabels) {
          // For left-handed, use the reversed tuning
          const tuningNote = leftHanded ? tuning[stringCount - 1 - i] : tuning[i];
          // Use average Y position for label
          const labelY = getStringY(stringIndex);
          const label = new Text({
            text: tuningNote,
            style: { fontSize: 14, fill: 0xffffff, fontWeight: 'bold' },
          });
          label.x = 10;
          label.y = labelY - 7;
          fretboardContainer.addChild(label);
        }
      }

      // Draw fret numbers
      if (showFretNumbers) {
        for (let i = 0; i <= fretCount; i++) {
          const x = calculateFretPosition(i);
          // Display fret numbers 0 to 22 (left to right when flipped=true)
          const displayFretNum = i;
          const fretNum = new Text({
            text: displayFretNum.toString(),
            style: { fontSize: 12, fill: 0xcccccc },
          });
          fretNum.x = x - 6;
          fretNum.y = height - 20;
          fretboardContainer.addChild(fretNum);
        }
      }

      // Draw position markers
      positions.forEach((pos) => {
        const x1 = calculateFretPosition(pos.fret);
        const x2 = calculateFretPosition(pos.fret + 1);
        const markerX = (x1 + x2) / 2;
        // Use dynamic string Y position
        const markerY = getStringY(pos.string);
        const color = pos.color ? parseInt(pos.color.replace('#', ''), 16) : 0x4dabf7;
        const radius = pos.emphasized ? 10 : 8;

        const posMarker = new Graphics();
        posMarker.circle(markerX, markerY, radius);
        posMarker.fill(color);

        if (pos.emphasized) {
          posMarker.stroke({ color: 0xff0000, width: 2 });
        }

        posMarker.interactive = true;
        posMarker.cursor = 'pointer';
        posMarker.on('pointerdown', () => {
          onPositionClick?.(pos.string, pos.fret);
        });
        posMarker.on('pointerover', () => {
          setHoveredPosition({ string: pos.string, fret: pos.fret });
          onPositionHover?.(pos.string, pos.fret);
          // Highlight on hover
          posMarker.alpha = 1;
        });
        posMarker.on('pointerout', () => {
          setHoveredPosition(null);
          onPositionHover?.(null, null);
          // Reset alpha
          posMarker.alpha = 0.8;
        });

        fretboardContainer.addChild(posMarker);

        if (pos.label) {
          const labelText = new Text({
            text: pos.label,
            style: { fontSize: 10, fill: 0xffffff, fontWeight: 'bold' },
          });
          labelText.x = markerX - 5;
          labelText.y = markerY - 5;
          fretboardContainer.addChild(labelText);
        }
      });

      // Draw capo if set (positioned slightly before the fret for realism)
      if (capoFret > 0 && capoFret <= fretCount) {
        // Position capo between frets (70% of the way from previous fret to current fret)
        const fretX1 = calculateFretPosition(capoFret - 1);
        const fretX2 = calculateFretPosition(capoFret);
        const capoX = fretX1 + (fretX2 - fretX1) * 0.7; // 70% towards the fret
        const capoHeight = height - topMargin - bottomMargin;

        // Capo body (metallic bar with rounded appearance)
        const capoBody = new Graphics();
        const capoWidth = 9;
        capoBody.rect(capoX - capoWidth / 2, topMargin - 5, capoWidth, capoHeight + 10);
        capoBody.fill(0xb8b8b8); // Brushed silver
        fretboardContainer.addChild(capoBody);

        // Capo left highlight (metallic shine - bright edge)
        const capoHighlight = new Graphics();
        capoHighlight.rect(capoX - capoWidth / 2 + 0.5, topMargin - 5, 2.5, capoHeight + 10);
        capoHighlight.fill({ color: 0xffffff, alpha: 0.8 });
        fretboardContainer.addChild(capoHighlight);

        // Capo center (subtle reflection)
        const capoCenter = new Graphics();
        capoCenter.rect(capoX - 1, topMargin - 5, 2, capoHeight + 10);
        capoCenter.fill({ color: 0xffffff, alpha: 0.3 });
        fretboardContainer.addChild(capoCenter);

        // Capo right shadow (depth)
        const capoShadow = new Graphics();
        capoShadow.rect(capoX + capoWidth / 2 - 2.5, topMargin - 5, 2.5, capoHeight + 10);
        capoShadow.fill({ color: 0x2a2a2a, alpha: 0.6 });
        fretboardContainer.addChild(capoShadow);

        // Capo label
        const capoLabel = new Text({
          text: `Capo ${capoFret}`,
          style: { fontSize: 11, fill: 0xffffff, fontWeight: 'bold' },
        });
        capoLabel.x = capoX - 22;
        capoLabel.y = topMargin - 28;
        fretboardContainer.addChild(capoLabel);
      }

      app.render();
    };

    initPixi();

    return () => {
      isMounted = false;
      if (appRef.current) {
        try {
          appRef.current.destroy(true, true);
        } catch (e) {
          console.warn('Error destroying Pixi app in cleanup:', e);
        }
        appRef.current = null;
      }
    };
  }, [fretCount, stringCount, tuning, showFretNumbers, showStringLabels, width, height, spacingMode, positions, onPositionClick, guitarModel, guitarStyle, flipped, capoFret, leftHanded]);

  return (
    <Stack spacing={2}>
      {title && <Typography variant="h6">{title}</Typography>}

      {/* Controls */}
      <Box sx={{ display: 'flex', gap: 2, alignItems: 'center', flexWrap: 'wrap' }}>
        {/* Guitar Type Selector - Simplified */}
        <FormControl sx={{ minWidth: 200 }}>
          <InputLabel id="realistic-guitar-type-label">Guitar Type</InputLabel>
          <Select
            labelId="realistic-guitar-type-label"
            id="realistic-guitar-type-select"
            value={guitarStyle.category}
            label="Guitar Type"
            onChange={(e) => {
              // Find first model of selected category
              const category = e.target.value as keyof typeof GUITAR_CATEGORIES;
              const firstModelId = GUITAR_CATEGORIES[category]?.[0];
              if (firstModelId) {
                setSelectedModel(firstModelId);
              }
            }}
          >
            <MenuItem value="classical">Classical</MenuItem>
            <MenuItem value="acoustic">Acoustic</MenuItem>
            <MenuItem value="electric">Electric</MenuItem>
          </Select>
        </FormControl>

        {/* Capo Selector */}
        <FormControl sx={{ minWidth: 150 }}>
          <InputLabel id="realistic-capo-position-label">Capo Position</InputLabel>
          <Select
            labelId="realistic-capo-position-label"
            id="realistic-capo-position-select"
            value={capoFret}
            label="Capo Position"
            onChange={(e) => setCapoFret(Number(e.target.value))}
          >
            <MenuItem value={0}>No Capo</MenuItem>
            {Array.from({ length: fretCount }, (_, i) => i + 1).map((fret) => (
              <MenuItem key={fret} value={fret}>
                Fret {fret}
              </MenuItem>
            ))}
          </Select>
        </FormControl>

        {/* Left-Handed Toggle */}
        <FormControlLabel
          control={
            <Switch
              checked={isLeftHanded}
              onChange={(e) => setIsLeftHanded(e.target.checked)}
            />
          }
          label="Left-Handed"
        />
      </Box>

      <Box sx={{ position: 'relative' }}>
        <Box
          ref={canvasRef}
          sx={{
            border: '1px solid #ddd',
            borderRadius: 1,
            overflow: 'hidden',
            backgroundColor: '#1a1a1a',
          }}
        />

        {/* Hover position tooltip */}
        {hoveredPosition && (
          <Box
            sx={{
              position: 'absolute',
              bottom: 8,
              left: 8,
              bgcolor: 'rgba(0,0,0,0.8)',
              color: 'white',
              px: 1.5,
              py: 0.75,
              borderRadius: 1,
              fontSize: '0.875rem',
              pointerEvents: 'none',
            }}
          >
            String {hoveredPosition.string + 1}, Fret {hoveredPosition.fret}
          </Box>
        )}
      </Box>

      <Typography variant="caption" sx={{ color: '#666' }}>
        {guitarStyle.brand} {guitarStyle.model} • Spacing: {spacingMode === 'realistic' ? 'Realistic (Logarithmic)' : 'Schematic (Linear)'}
      </Typography>
    </Stack>
  );
};

export default RealisticFretboard;
