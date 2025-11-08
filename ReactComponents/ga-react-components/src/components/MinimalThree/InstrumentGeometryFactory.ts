/**
 * Instrument Geometry Factory
 * 
 * Procedurally generates 3D geometry for any stringed instrument based on InstrumentConfig.
 * Supports all instruments from the YAML database with adaptive geometry.
 */

import * as THREE from 'three';
import type { InstrumentConfig, InstrumentBodyStyle } from '../../types/InstrumentConfig';

export interface FretboardDimensions {
  length: number;        // Total fretboard length in mm
  nutWidth: number;      // Width at nut in mm
  bridgeWidth: number;   // Width at bridge in mm
  thickness: number;     // Fretboard thickness in mm
  fretHeight: number;    // Fret wire height in mm
}

export interface StringSpacing {
  positions: number[];   // Y positions of strings at nut (in mm from center)
  gauges: number[];      // String thicknesses in mm
}

export class InstrumentGeometryFactory {
  private static readonly FRET_CONSTANT = 17.817; // 12th root of 2, for fret spacing
  static readonly MM_TO_UNITS = 0.001;    // Convert mm to Three.js units (made public for capo positioning)

  /**
   * Calculate fret positions using equal temperament
   */
  static calculateFretPositions(scaleLength: number, fretCount: number): number[] {
    const positions: number[] = [0]; // Nut position
    
    for (let i = 1; i <= fretCount; i++) {
      const distance = scaleLength - (scaleLength / Math.pow(2, i / 12));
      positions.push(distance);
    }
    
    return positions;
  }

  /**
   * Calculate string spacing based on instrument type and string count
   */
  static calculateStringSpacing(instrument: InstrumentConfig): StringSpacing {
    const { tuning, nutWidth, bodyStyle } = instrument;
    const stringCount = tuning.length;

    // Calculate string positions (X coordinates from center, evenly spaced)
    const positions: number[] = [];
    const usableWidth = nutWidth * 0.8; // Use 80% of nut width for string spacing
    const spacing = stringCount > 1 ? usableWidth / (stringCount - 1) : 0;

    for (let i = 0; i < stringCount; i++) {
      if (stringCount === 1) {
        positions.push(0); // Single string at center
      } else {
        const position = (i * spacing) - (usableWidth / 2);
        positions.push(position);
      }
    }

    // Calculate string gauges based on instrument type and tuning
    const gauges = this.calculateStringGauges(instrument);

    return { positions, gauges };
  }

  /**
   * Calculate string gauges based on instrument type and pitch
   */
  private static calculateStringGauges(instrument: InstrumentConfig): number[] {
    const { tuning, bodyStyle } = instrument;
    
    // Base gauges for different instrument types (in mm)
    const baseGauges = {
      classical: [0.7, 0.8, 0.9, 1.0, 1.1, 1.2],      // Nylon strings
      acoustic: [0.3, 0.4, 0.6, 0.8, 1.0, 1.2],       // Steel strings
      electric: [0.25, 0.33, 0.43, 0.64, 0.86, 1.09], // Electric guitar
      bass: [2.7, 2.2, 1.8, 1.4],                     // Bass strings
      ukulele: [0.6, 0.7, 0.8, 0.9],                  // Ukulele strings
      banjo: [0.25, 0.33, 0.43, 0.64, 0.25],          // Banjo (5th string thinner)
      mandolin: [0.25, 0.33, 0.43, 0.64],             // Mandolin strings
      lute: [0.5, 0.6, 0.7, 0.8, 0.9, 1.0],          // Lute strings
      generic: [0.5, 0.6, 0.7, 0.8, 0.9, 1.0],       // Default
    };

    const gaugeSet = baseGauges[bodyStyle] || baseGauges.generic;
    const stringCount = tuning.length;
    
    // Scale gauges to match string count
    const gauges: number[] = [];
    for (let i = 0; i < stringCount; i++) {
      const index = Math.floor((i / stringCount) * gaugeSet.length);
      gauges.push(gaugeSet[Math.min(index, gaugeSet.length - 1)]);
    }

    return gauges;
  }

  /**
   * Create fretboard geometry
   * Truncated to end around the typical pickup position (fret 19-20)
   */
  static createFretboard(instrument: InstrumentConfig): THREE.BufferGeometry {
    const { scaleLength, nutWidth, bridgeWidth, fretCount } = instrument;

    // Calculate fretboard end position (around fret 19-20, typical neck pickup position)
    const fretboardEndFret = Math.min(20, fretCount);
    const fretPositions = this.calculateFretPositions(scaleLength, fretCount);
    const fretboardLength = fretPositions[fretboardEndFret];

    const length = fretboardLength * this.MM_TO_UNITS;
    const nutW = nutWidth * this.MM_TO_UNITS;

    // Calculate width at fretboard end (interpolate between nut and bridge)
    const lengthRatio = fretboardLength / scaleLength;
    const endWidth = nutWidth + (bridgeWidth - nutWidth) * lengthRatio;
    const endW = endWidth * this.MM_TO_UNITS;

    const thickness = 0.008; // 8mm thickness

    // Create tapered fretboard shape (XZ plane)
    const shape = new THREE.Shape();
    shape.moveTo(-nutW / 2, 0);      // Left side at nut
    shape.lineTo(nutW / 2, 0);       // Right side at nut
    shape.lineTo(endW / 2, length);  // Right side at fretboard end
    shape.lineTo(-endW / 2, length); // Left side at fretboard end
    shape.closePath();

    const geometry = new THREE.ExtrudeGeometry(shape, {
      depth: thickness,
      bevelEnabled: false,
    });

    // Rotate and position correctly
    geometry.rotateX(-Math.PI / 2); // Rotate to XZ plane
    geometry.translate(0, 0, 0);    // Keep at origin

    return geometry;
  }

  /**
   * Create neck back geometry
   * Creates the rounded back of the neck that sits underneath the fretboard
   */
  static createNeckBack(instrument: InstrumentConfig): THREE.BufferGeometry {
    const { scaleLength, nutWidth, fretCount } = instrument;

    // Calculate neck length (same as truncated fretboard)
    const fretboardEndFret = Math.min(20, fretCount);
    const fretPositions = this.calculateFretPositions(scaleLength, fretCount);
    const neckLength = fretPositions[fretboardEndFret] * this.MM_TO_UNITS;

    const neckWidth = nutWidth * this.MM_TO_UNITS * 0.95;
    const neckThickness = nutWidth * this.MM_TO_UNITS * 0.35; // Flatter profile

    // Create half-ellipse shape for the cross-section (rounded back)
    const shape = new THREE.Shape();
    const segments = 32;

    // Draw half-ellipse (bottom half, curved part)
    for (let i = 0; i <= segments; i++) {
      const angle = Math.PI + (i / segments) * Math.PI; // π to 2π (bottom half)
      const x = (neckWidth / 2) * Math.cos(angle);
      const y = neckThickness * Math.sin(angle);

      if (i === 0) {
        shape.moveTo(x, y);
      } else {
        shape.lineTo(x, y);
      }
    }

    // Close the shape with a straight line across the top
    shape.lineTo(-neckWidth / 2, 0);

    // Extrude along the neck length
    const geometry = new THREE.ExtrudeGeometry(shape, {
      depth: neckLength,
      bevelEnabled: false,
      steps: 1,
    });

    // Rotate and position: neck back sits below fretboard
    geometry.rotateY(Math.PI / 2);
    geometry.rotateZ(Math.PI);
    geometry.translate(0, -0.004, 0); // Position below fretboard surface

    return geometry;
  }

  /**
   * Create truncated body section
   * Creates a small body section after the fretboard, truncated at the pickup position
   */
  static createTruncatedBody(instrument: InstrumentConfig): THREE.BufferGeometry {
    const { scaleLength, nutWidth, bridgeWidth, fretCount, bodyStyle } = instrument;

    // Calculate fretboard end position (around fret 19-20)
    const fretboardEndFret = Math.min(20, fretCount);
    const fretPositions = this.calculateFretPositions(scaleLength, fretCount);
    const fretboardLength = fretPositions[fretboardEndFret];

    // Body section extends a short distance beyond fretboard
    const bodyExtension = 50; // 50mm extension beyond fretboard
    const bodyStartZ = fretboardLength * this.MM_TO_UNITS;
    const bodyLength = bodyExtension * this.MM_TO_UNITS;

    // Calculate widths
    const lengthRatio = fretboardLength / scaleLength;
    const startWidth = nutWidth + (bridgeWidth - nutWidth) * lengthRatio;
    const startW = startWidth * this.MM_TO_UNITS;

    // Body widens slightly
    const endWidth = startWidth * 1.3;
    const endW = endWidth * this.MM_TO_UNITS;

    const thickness = 0.015; // 15mm thickness (thicker than fretboard)

    // Create body shape (XZ plane)
    const shape = new THREE.Shape();
    shape.moveTo(-startW / 2, 0);           // Left side at fretboard end
    shape.lineTo(startW / 2, 0);            // Right side at fretboard end
    shape.lineTo(endW / 2, bodyLength);     // Right side at body end
    shape.lineTo(-endW / 2, bodyLength);    // Left side at body end
    shape.closePath();

    const geometry = new THREE.ExtrudeGeometry(shape, {
      depth: thickness,
      bevelEnabled: true,
      bevelThickness: 0.002,
      bevelSize: 0.002,
      bevelSegments: 3,
    });

    // Rotate and position correctly
    geometry.rotateX(-Math.PI / 2);
    geometry.translate(0, -thickness / 2, bodyStartZ); // Position after fretboard

    return geometry;
  }

  /**
   * Create strumming zone - a semi-transparent area over the strings near the body
   * This indicates where the player would strum or pick the strings
   */
  static createStrummingZone(instrument: InstrumentConfig): THREE.BufferGeometry {
    const { scaleLength, nutWidth, bridgeWidth, fretCount } = instrument;

    // Calculate fretboard end position (around fret 19-20)
    const fretboardEndFret = Math.min(20, fretCount);
    const fretPositions = this.calculateFretPositions(scaleLength, fretCount);
    const fretboardLength = fretPositions[fretboardEndFret];

    // Strumming zone starts at the end of the fretboard
    const zoneStartZ = fretboardLength * this.MM_TO_UNITS;
    const zoneLength = 40 * this.MM_TO_UNITS; // 40mm long strumming zone

    // Calculate width at fretboard end
    const lengthRatio = fretboardLength / scaleLength;
    const zoneWidth = (nutWidth + (bridgeWidth - nutWidth) * lengthRatio) * this.MM_TO_UNITS;

    // Create a thin rectangular plane for the strumming zone
    const geometry = new THREE.PlaneGeometry(zoneWidth, zoneLength);

    // Rotate to lie flat on the fretboard (XZ plane)
    geometry.rotateX(-Math.PI / 2);

    // Position above the strings (slightly elevated)
    geometry.translate(0, 0.003, zoneStartZ + zoneLength / 2);

    return geometry;
  }

  /**
   * Create nut geometry with realistic string slots
   * The nut is a separate piece from frets with sunken holes for strings
   */
  static createNut(instrument: InstrumentConfig): THREE.BufferGeometry[] {
    const { nutWidth } = instrument;
    const spacing = this.calculateStringSpacing(instrument);
    const geometries: THREE.BufferGeometry[] = [];

    // Main nut body dimensions
    const nutHeight = 0.006; // 6mm height
    const nutDepth = 0.004; // 4mm depth (along fretboard)
    const width = nutWidth * this.MM_TO_UNITS;

    // Create main nut body
    const nutBody = new THREE.BoxGeometry(width, nutHeight, nutDepth);
    nutBody.translate(0, nutHeight / 2, 0); // Position at nut location
    geometries.push(nutBody);

    // Create string holes (small cylinders subtracted from nut)
    // These are visual indicators - actual CSG subtraction would be complex
    for (let i = 0; i < spacing.positions.length; i++) {
      const xPos = spacing.positions[i] * this.MM_TO_UNITS;
      const stringRadius = spacing.gauges[i] * this.MM_TO_UNITS / 2;
      const holeRadius = stringRadius * 1.5; // Slightly larger than string
      const holeDepth = nutHeight * 0.4; // Hole depth (40% of nut height)

      // Create a small cylinder for the hole (dark material will make it look sunken)
      const holeGeometry = new THREE.CylinderGeometry(holeRadius, holeRadius, holeDepth, 8);
      holeGeometry.rotateZ(Math.PI / 2); // Rotate to align with X-axis
      holeGeometry.translate(xPos, nutHeight * 0.7, 0); // Position on top of nut
      geometries.push(holeGeometry);
    }

    return geometries;
  }

  /**
   * Create fret wire geometries
   */
  static createFrets(instrument: InstrumentConfig): THREE.BufferGeometry[] {
    const { scaleLength, nutWidth, bridgeWidth, fretCount } = instrument;
    const fretPositions = this.calculateFretPositions(scaleLength, fretCount);
    const frets: THREE.BufferGeometry[] = [];

    // Skip fret 0 (nut) - it's now created separately
    for (let i = 1; i <= fretCount; i++) {
      const position = fretPositions[i] * this.MM_TO_UNITS;
      const progress = position / (scaleLength * this.MM_TO_UNITS);
      const width = (nutWidth + (bridgeWidth - nutWidth) * progress) * this.MM_TO_UNITS;

      // Create fret wire (thin cylinder across the width)
      const radius = 0.001; // 1mm radius
      const height = 0.003; // 3mm height

      const geometry = new THREE.CylinderGeometry(radius, radius, width, 8);
      geometry.rotateZ(Math.PI / 2); // Rotate to align across width (X-axis)
      geometry.translate(0, height / 2, position); // Position at fret location

      frets.push(geometry);
    }

    return frets;
  }

  /**
   * Create string geometries
   * Strings extend to the end of the truncated body
   */
  static createStrings(instrument: InstrumentConfig): THREE.BufferGeometry[] {
    const { scaleLength, fretCount, bodyStyle } = instrument;

    // Calculate string length to match truncated body
    const fretboardEndFret = Math.min(20, fretCount);
    const fretPositions = this.calculateFretPositions(scaleLength, fretCount);
    const fretboardLength = fretPositions[fretboardEndFret];
    const bodyExtension = 50; // 50mm extension (matches createTruncatedBody)
    const totalLength = (fretboardLength + bodyExtension) * this.MM_TO_UNITS;

    const spacing = this.calculateStringSpacing(instrument);
    const strings: THREE.BufferGeometry[] = [];

    // String action (height above fretboard) varies by instrument type
    // Electric guitars have lower action than acoustic/classical
    const stringAction = bodyStyle === 'electric' ? 0.0012 : 0.002; // 1.2mm for electric, 2mm for others

    for (let i = 0; i < spacing.positions.length; i++) {
      const xPos = spacing.positions[i] * this.MM_TO_UNITS; // X position, not Y
      const radius = spacing.gauges[i] * this.MM_TO_UNITS / 2;

      // Create string along Z-axis (length to end of truncated body)
      const geometry = new THREE.CylinderGeometry(radius, radius, totalLength, 6);
      geometry.rotateX(Math.PI / 2); // Rotate to align with Z-axis
      geometry.translate(xPos, stringAction, totalLength / 2); // Position at string location, slightly above fretboard

      strings.push(geometry);
    }

    return strings;
  }

  /**
   * Create position marker inlays
   */
  static createInlays(instrument: InstrumentConfig): THREE.BufferGeometry[] {
    const { scaleLength, fretCount, nutWidth, bridgeWidth } = instrument;
    const fretPositions = this.calculateFretPositions(scaleLength, fretCount);
    const inlays: THREE.BufferGeometry[] = [];
    
    // Standard inlay positions
    const inlayFrets = [3, 5, 7, 9, 12, 15, 17, 19, 21, 24];
    const doubleInlayFrets = [12, 24];

    for (const fretNum of inlayFrets) {
      if (fretNum > fretCount) continue;
      
      const fretPos = fretPositions[fretNum - 1] * this.MM_TO_UNITS;
      const nextFretPos = fretPositions[fretNum] * this.MM_TO_UNITS;
      const midPos = (fretPos + nextFretPos) / 2;
      
      const progress = midPos / (scaleLength * this.MM_TO_UNITS);
      const boardWidth = (nutWidth + (bridgeWidth - nutWidth) * progress) * this.MM_TO_UNITS;
      
      const radius = Math.min(0.003, boardWidth / 8); // Adaptive size
      const depth = 0.001; // 1mm deep
      
      if (doubleInlayFrets.includes(fretNum)) {
        // Double dot inlay
        const inlay1 = new THREE.CylinderGeometry(radius, radius, depth, 8);
        inlay1.rotateX(Math.PI / 2); // Rotate to lie flat on fretboard
        inlay1.translate(boardWidth / 4, 0.001, midPos);
        inlays.push(inlay1);

        const inlay2 = new THREE.CylinderGeometry(radius, radius, depth, 8);
        inlay2.rotateX(Math.PI / 2); // Rotate to lie flat on fretboard
        inlay2.translate(-boardWidth / 4, 0.001, midPos);
        inlays.push(inlay2);
      } else {
        // Single dot inlay
        const inlay = new THREE.CylinderGeometry(radius, radius, depth, 8);
        inlay.rotateX(Math.PI / 2); // Rotate to lie flat on fretboard
        inlay.translate(0, 0.001, midPos);
        inlays.push(inlay);
      }
    }

    return inlays;
  }

  /**
   * Create headstock geometry based on instrument type
   */
  static createHeadstock(instrument: InstrumentConfig): THREE.BufferGeometry {
    const { nutWidth, bodyStyle, tuning } = instrument;
    const stringCount = tuning.length;
    
    // Headstock dimensions based on instrument type
    const dimensions = this.getHeadstockDimensions(bodyStyle, stringCount);
    const width = Math.max(nutWidth * 1.2, dimensions.width) * this.MM_TO_UNITS;
    const length = dimensions.length * this.MM_TO_UNITS;
    const thickness = dimensions.thickness * this.MM_TO_UNITS;

    let geometry: THREE.BufferGeometry;

    switch (bodyStyle) {
      case 'classical':
        geometry = this.createClassicalHeadstock(width, length, thickness);
        break;
      case 'acoustic':
      case 'electric':
        geometry = this.createModernHeadstock(width, length, thickness);
        break;
      case 'bass':
        geometry = this.createBassHeadstock(width, length, thickness);
        break;
      case 'ukulele':
        geometry = this.createUkuleleHeadstock(width, length, thickness);
        break;
      case 'banjo':
        geometry = this.createBanjoHeadstock(width, length, thickness);
        break;
      default:
        geometry = this.createGenericHeadstock(width, length, thickness);
    }

    // Position headstock at the nut end (negative Z)
    geometry.translate(0, thickness / 2, -length / 2);

    return geometry;
  }

  /**
   * Get headstock dimensions for different instrument types
   */
  private static getHeadstockDimensions(bodyStyle: InstrumentBodyStyle, stringCount: number) {
    const baseDimensions = {
      classical: { width: 80, length: 120, thickness: 15 },
      acoustic: { width: 70, length: 100, thickness: 12 },
      electric: { width: 65, length: 90, thickness: 10 },
      bass: { width: 80, length: 140, thickness: 18 },
      ukulele: { width: 50, length: 70, thickness: 8 },
      banjo: { width: 60, length: 80, thickness: 10 },
      mandolin: { width: 45, length: 80, thickness: 8 },
      lute: { width: 70, length: 100, thickness: 12 },
      generic: { width: 60, length: 90, thickness: 12 },
    };

    const base = baseDimensions[bodyStyle] || baseDimensions.generic;
    
    // Scale dimensions based on string count
    const scaleFactor = Math.max(1, stringCount / 6);
    return {
      width: base.width * scaleFactor,
      length: base.length,
      thickness: base.thickness,
    };
  }

  /**
   * Create classical guitar headstock (slotted)
   */
  private static createClassicalHeadstock(width: number, length: number, thickness: number): THREE.BufferGeometry {
    const shape = new THREE.Shape();
    shape.moveTo(-width / 2, 0);
    shape.lineTo(width / 2, 0);
    shape.lineTo(width / 2, length * 0.8);
    shape.lineTo(width / 3, length);
    shape.lineTo(-width / 3, length);
    shape.lineTo(-width / 2, length * 0.8);
    shape.closePath();

    return new THREE.ExtrudeGeometry(shape, {
      depth: thickness,
      bevelEnabled: true,
      bevelSize: 0.001,
      bevelThickness: 0.001,
    });
  }

  /**
   * Create modern headstock (solid)
   */
  private static createModernHeadstock(width: number, length: number, thickness: number): THREE.BufferGeometry {
    const shape = new THREE.Shape();
    shape.moveTo(-width / 2, 0);
    shape.lineTo(width / 2, 0);
    shape.lineTo(width / 2, length);
    shape.lineTo(-width / 2, length);
    shape.closePath();

    return new THREE.ExtrudeGeometry(shape, {
      depth: thickness,
      bevelEnabled: true,
      bevelSize: 0.001,
      bevelThickness: 0.001,
    });
  }

  /**
   * Create bass headstock (larger)
   */
  private static createBassHeadstock(width: number, length: number, thickness: number): THREE.BufferGeometry {
    return this.createModernHeadstock(width, length, thickness);
  }

  /**
   * Create ukulele headstock (smaller)
   */
  private static createUkuleleHeadstock(width: number, length: number, thickness: number): THREE.BufferGeometry {
    const shape = new THREE.Shape();
    const radius = width / 3;
    
    shape.moveTo(-width / 2, 0);
    shape.lineTo(width / 2, 0);
    shape.lineTo(width / 2, length - radius);
    shape.absarc(0, length - radius, radius, 0, Math.PI, false);
    shape.lineTo(-width / 2, length - radius);
    shape.closePath();

    return new THREE.ExtrudeGeometry(shape, {
      depth: thickness,
      bevelEnabled: true,
      bevelSize: 0.001,
      bevelThickness: 0.001,
    });
  }

  /**
   * Create banjo headstock
   */
  private static createBanjoHeadstock(width: number, length: number, thickness: number): THREE.BufferGeometry {
    return this.createModernHeadstock(width * 0.8, length * 0.9, thickness);
  }

  /**
   * Create generic headstock
   */
  private static createGenericHeadstock(width: number, length: number, thickness: number): THREE.BufferGeometry {
    return this.createModernHeadstock(width, length, thickness);
  }

  /**
   * Create capo geometry
   */
  static createCapo(instrument: InstrumentConfig, fretNumber: number): THREE.BufferGeometry {
    if (fretNumber <= 0) {
      return new THREE.BufferGeometry(); // Empty geometry
    }

    const { scaleLength, nutWidth, bridgeWidth } = instrument;
    const fretPositions = this.calculateFretPositions(scaleLength, instrument.fretCount);
    const position = fretPositions[fretNumber] * this.MM_TO_UNITS;

    const progress = position / (scaleLength * this.MM_TO_UNITS);
    const width = (nutWidth + (bridgeWidth - nutWidth) * progress) * this.MM_TO_UNITS;

    const geometry = new THREE.CylinderGeometry(0.004, 0.004, width, 8);
    geometry.rotateZ(Math.PI / 2); // Rotate to align across width (X-axis)
    geometry.translate(0, 0.008, position); // Above strings at fret position

    return geometry;
  }

  /**
   * Create tuning peg geometries
   */
  static createTuningPegs(instrument: InstrumentConfig): THREE.BufferGeometry[] {
    const { tuning, bodyStyle } = instrument;
    const stringCount = tuning.length;
    const pegs: THREE.BufferGeometry[] = [];
    
    const pegRadius = 0.003;
    const pegLength = 0.015;
    
    // Calculate peg positions based on headstock style
    const positions = this.calculateTuningPegPositions(instrument);
    
    for (let i = 0; i < stringCount; i++) {
      const pos = positions[i];
      const geometry = new THREE.CylinderGeometry(pegRadius, pegRadius, pegLength, 8);
      geometry.translate(pos.x, pos.y, pos.z);
      pegs.push(geometry);
    }

    return pegs;
  }

  /**
   * Calculate tuning peg positions based on instrument type
   */
  private static calculateTuningPegPositions(instrument: InstrumentConfig): THREE.Vector3[] {
    const { tuning, bodyStyle, nutWidth } = instrument;
    const stringCount = tuning.length;
    const positions: THREE.Vector3[] = [];
    
    const headstockLength = this.getHeadstockDimensions(bodyStyle, stringCount).length * this.MM_TO_UNITS;
    const spacing = (nutWidth * this.MM_TO_UNITS) / (stringCount + 1);

    switch (bodyStyle) {
      case 'classical':
        // Slotted headstock - alternating sides
        for (let i = 0; i < stringCount; i++) {
          const side = i % 2 === 0 ? -1 : 1;
          const x = side * (nutWidth * this.MM_TO_UNITS) / 3;
          const y = 0.008;
          const z = -headstockLength * 0.3 - (Math.floor(i / 2) * 0.02);
          positions.push(new THREE.Vector3(x, y, z));
        }
        break;
        
      case 'electric':
      case 'acoustic':
      case 'bass':
      default:
        // Inline tuners
        for (let i = 0; i < stringCount; i++) {
          const x = (i - (stringCount - 1) / 2) * spacing;
          const y = 0.008;
          const z = -headstockLength * 0.8;
          positions.push(new THREE.Vector3(x, y, z));
        }
        break;
    }

    return positions;
  }
}
