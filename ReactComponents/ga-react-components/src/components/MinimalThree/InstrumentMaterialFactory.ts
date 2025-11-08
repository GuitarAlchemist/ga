/**
 * Instrument Material Factory
 * 
 * Creates WebGPU-optimized materials for different instrument components.
 * Provides realistic materials for wood, strings, metal, and inlays.
 */

import * as THREE from 'three';
import type { InstrumentConfig, InstrumentBodyStyle } from '../../types/InstrumentConfig';

export interface MaterialOptions {
  roughness?: number;
  metalness?: number;
  clearcoat?: number;
  clearcoatRoughness?: number;
  envMapIntensity?: number;
}

export class InstrumentMaterialFactory {
  private static textureLoader = new THREE.TextureLoader();
  private static materialCache = new Map<string, THREE.Material>();

  /**
   * Create wood material for fretboard/headstock
   */
  static createWoodMaterial(
    woodColor: number = 0x8B4513,
    bodyStyle: InstrumentBodyStyle = 'classical',
    options: MaterialOptions = {}
  ): THREE.MeshPhysicalMaterial {
    const cacheKey = `wood_${woodColor.toString(16)}_${bodyStyle}`;
    
    if (this.materialCache.has(cacheKey)) {
      return this.materialCache.get(cacheKey) as THREE.MeshPhysicalMaterial;
    }

    const material = new THREE.MeshPhysicalMaterial({
      color: new THREE.Color(woodColor),
      roughness: options.roughness ?? this.getWoodRoughness(bodyStyle),
      metalness: options.metalness ?? 0.0,
      clearcoat: options.clearcoat ?? this.getWoodClearcoat(bodyStyle),
      clearcoatRoughness: options.clearcoatRoughness ?? 0.1,
      envMapIntensity: options.envMapIntensity ?? 0.5,
    });

    // Add wood grain texture if available
    this.addWoodTexture(material, bodyStyle);

    this.materialCache.set(cacheKey, material);
    return material;
  }

  /**
   * Create string material based on string type
   */
  static createStringMaterial(
    stringType: 'nylon' | 'steel' | 'wound' = 'steel',
    gauge: number = 0.5
  ): THREE.MeshPhysicalMaterial {
    const cacheKey = `string_${stringType}_${gauge}`;
    
    if (this.materialCache.has(cacheKey)) {
      return this.materialCache.get(cacheKey) as THREE.MeshPhysicalMaterial;
    }

    let material: THREE.MeshPhysicalMaterial;

    switch (stringType) {
      case 'nylon':
        material = new THREE.MeshPhysicalMaterial({
          color: 0xFFF8DC, // Cornsilk
          roughness: 0.8,
          metalness: 0.0,
          transmission: 0.1,
          thickness: gauge * 0.001,
          envMapIntensity: 0.2,
        });
        break;

      case 'steel':
        material = new THREE.MeshPhysicalMaterial({
          color: 0xC0C0C0, // Silver
          roughness: 0.1,
          metalness: 0.9,
          envMapIntensity: 1.0,
        });
        break;

      case 'wound':
        material = new THREE.MeshPhysicalMaterial({
          color: 0xB87333, // Dark goldenrod (bronze wound)
          roughness: 0.3,
          metalness: 0.7,
          envMapIntensity: 0.8,
        });
        break;

      default:
        material = this.createStringMaterial('steel', gauge);
    }

    this.materialCache.set(cacheKey, material);
    return material;
  }

  /**
   * Create nut material (bone/synthetic)
   */
  static createNutMaterial(): THREE.MeshPhysicalMaterial {
    const cacheKey = 'nut_body';

    if (this.materialCache.has(cacheKey)) {
      return this.materialCache.get(cacheKey) as THREE.MeshPhysicalMaterial;
    }

    const material = new THREE.MeshPhysicalMaterial({
      color: 0xF5F5DC, // Bone color (beige)
      roughness: 0.5,
      metalness: 0.0,
      clearcoat: 0.2,
      clearcoatRoughness: 0.3,
      envMapIntensity: 0.4,
    });

    this.materialCache.set(cacheKey, material);
    return material;
  }

  /**
   * Create nut hole material (dark, sunken appearance)
   */
  static createNutHoleMaterial(): THREE.MeshPhysicalMaterial {
    const cacheKey = 'nut_hole';

    if (this.materialCache.has(cacheKey)) {
      return this.materialCache.get(cacheKey) as THREE.MeshPhysicalMaterial;
    }

    const material = new THREE.MeshPhysicalMaterial({
      color: 0x1a1a1a, // Very dark gray/black
      roughness: 0.9,
      metalness: 0.0,
      envMapIntensity: 0.1,
    });

    this.materialCache.set(cacheKey, material);
    return material;
  }

  /**
   * Create fret material (metal)
   */
  static createFretMaterial(isNut: boolean = false): THREE.MeshPhysicalMaterial {
    const cacheKey = `fret_${isNut}`;

    if (this.materialCache.has(cacheKey)) {
      return this.materialCache.get(cacheKey) as THREE.MeshPhysicalMaterial;
    }

    const material = new THREE.MeshPhysicalMaterial({
      color: 0xC0C0C0, // Silver
      roughness: 0.1,
      metalness: 0.9,
      envMapIntensity: 1.0,
    });

    this.materialCache.set(cacheKey, material);
    return material;
  }

  /**
   * Create inlay material
   */
  static createInlayMaterial(
    bodyStyle: InstrumentBodyStyle = 'classical',
    inlayType: 'dots' | 'blocks' | 'abalone' | 'pearl' = 'dots'
  ): THREE.MeshPhysicalMaterial {
    const cacheKey = `inlay_${bodyStyle}_${inlayType}`;
    
    if (this.materialCache.has(cacheKey)) {
      return this.materialCache.get(cacheKey) as THREE.MeshPhysicalMaterial;
    }

    let material: THREE.MeshPhysicalMaterial;

    switch (inlayType) {
      case 'pearl':
        material = new THREE.MeshPhysicalMaterial({
          color: 0xFFF8DC,
          roughness: 0.1,
          metalness: 0.0,
          clearcoat: 1.0,
          clearcoatRoughness: 0.0,
          iridescence: 0.3,
          iridescenceIOR: 1.3,
          envMapIntensity: 0.8,
        });
        break;

      case 'abalone':
        material = new THREE.MeshPhysicalMaterial({
          color: 0x40E0D0,
          roughness: 0.1,
          metalness: 0.0,
          clearcoat: 1.0,
          clearcoatRoughness: 0.0,
          iridescence: 0.8,
          iridescenceIOR: 1.4,
          envMapIntensity: 1.0,
        });
        break;

      case 'blocks':
        material = new THREE.MeshPhysicalMaterial({
          color: 0xF5F5DC,
          roughness: 0.3,
          metalness: 0.0,
          clearcoat: 0.5,
          clearcoatRoughness: 0.1,
          envMapIntensity: 0.5,
        });
        break;

      case 'dots':
      default:
        material = new THREE.MeshPhysicalMaterial({
          color: 0xF5F5DC, // Bone/ivory
          roughness: 0.4,
          metalness: 0.0,
          clearcoat: 0.3,
          clearcoatRoughness: 0.2,
          envMapIntensity: 0.4,
        });
        break;
    }

    this.materialCache.set(cacheKey, material);
    return material;
  }

  /**
   * Create tuning peg material (metal)
   */
  static createTuningPegMaterial(
    finish: 'chrome' | 'gold' | 'black' | 'vintage' = 'chrome'
  ): THREE.MeshPhysicalMaterial {
    const cacheKey = `tuning_peg_${finish}`;
    
    if (this.materialCache.has(cacheKey)) {
      return this.materialCache.get(cacheKey) as THREE.MeshPhysicalMaterial;
    }

    let material: THREE.MeshPhysicalMaterial;

    switch (finish) {
      case 'gold':
        material = new THREE.MeshPhysicalMaterial({
          color: 0xFFD700,
          roughness: 0.1,
          metalness: 1.0,
          envMapIntensity: 1.0,
        });
        break;

      case 'black':
        material = new THREE.MeshPhysicalMaterial({
          color: 0x2C2C2C,
          roughness: 0.2,
          metalness: 0.8,
          envMapIntensity: 0.6,
        });
        break;

      case 'vintage':
        material = new THREE.MeshPhysicalMaterial({
          color: 0xB87333,
          roughness: 0.3,
          metalness: 0.7,
          envMapIntensity: 0.7,
        });
        break;

      case 'chrome':
      default:
        material = new THREE.MeshPhysicalMaterial({
          color: 0xC0C0C0,
          roughness: 0.05,
          metalness: 1.0,
          envMapIntensity: 1.0,
        });
        break;
    }

    this.materialCache.set(cacheKey, material);
    return material;
  }

  /**
   * Create capo material
   */
  static createCapoMaterial(): THREE.MeshPhysicalMaterial {
    const cacheKey = 'capo';
    
    if (this.materialCache.has(cacheKey)) {
      return this.materialCache.get(cacheKey) as THREE.MeshPhysicalMaterial;
    }

    const material = new THREE.MeshPhysicalMaterial({
      color: 0x2C2C2C, // Dark gray/black
      roughness: 0.4,
      metalness: 0.2,
      envMapIntensity: 0.3,
    });

    this.materialCache.set(cacheKey, material);
    return material;
  }

  /**
   * Create position marker material (for notes/chords)
   */
  static createPositionMarkerMaterial(
    color: number = 0x4DABF7,
    emphasized: boolean = false
  ): THREE.MeshPhysicalMaterial {
    const cacheKey = `marker_${color.toString(16)}_${emphasized}`;
    
    if (this.materialCache.has(cacheKey)) {
      return this.materialCache.get(cacheKey) as THREE.MeshPhysicalMaterial;
    }

    const material = new THREE.MeshPhysicalMaterial({
      color: new THREE.Color(color),
      roughness: 0.3,
      metalness: 0.0,
      transmission: emphasized ? 0.0 : 0.1,
      opacity: emphasized ? 1.0 : 0.9,
      transparent: !emphasized,
      emissive: emphasized ? new THREE.Color(color).multiplyScalar(0.1) : new THREE.Color(0),
      envMapIntensity: 0.5,
    });

    this.materialCache.set(cacheKey, material);
    return material;
  }

  /**
   * Create strumming zone material - semi-transparent with a subtle color
   */
  static createStrummingZoneMaterial(): THREE.MeshBasicMaterial {
    const cacheKey = 'strumming_zone';

    if (this.materialCache.has(cacheKey)) {
      return this.materialCache.get(cacheKey) as THREE.MeshBasicMaterial;
    }

    const material = new THREE.MeshBasicMaterial({
      color: 0x4DABF7, // Light blue color
      transparent: true,
      opacity: 0.15, // Very subtle
      side: THREE.DoubleSide,
      depthWrite: false, // Don't write to depth buffer so it doesn't occlude other objects
    });

    this.materialCache.set(cacheKey, material);
    return material;
  }

  /**
   * Get appropriate string materials for an instrument
   */
  static getStringMaterials(instrument: InstrumentConfig): THREE.MeshPhysicalMaterial[] {
    const { bodyStyle, tuning } = instrument;
    const materials: THREE.MeshPhysicalMaterial[] = [];

    // Determine string types based on instrument
    const stringTypes = this.getStringTypes(bodyStyle, tuning.length);

    for (let i = 0; i < tuning.length; i++) {
      const stringType = stringTypes[i] || 'steel';
      const gauge = this.getStringGauge(bodyStyle, i, tuning.length);
      materials.push(this.createStringMaterial(stringType, gauge));
    }

    return materials;
  }

  /**
   * Get string types for different instruments
   */
  private static getStringTypes(
    bodyStyle: InstrumentBodyStyle,
    stringCount: number
  ): ('nylon' | 'steel' | 'wound')[] {
    switch (bodyStyle) {
      case 'classical':
        // Classical guitar: nylon trebles, wound bass
        return stringCount === 6 
          ? ['nylon', 'nylon', 'nylon', 'wound', 'wound', 'wound']
          : Array(stringCount).fill('nylon');

      case 'bass':
        // Bass: all wound strings
        return Array(stringCount).fill('wound');

      case 'acoustic':
        // Acoustic guitar: steel trebles, wound bass
        return stringCount === 6
          ? ['steel', 'steel', 'steel', 'wound', 'wound', 'wound']
          : Array(stringCount).fill('steel');

      case 'electric':
        // Electric guitar: steel trebles, wound bass
        return stringCount === 6
          ? ['steel', 'steel', 'steel', 'wound', 'wound', 'wound']
          : Array(stringCount).fill('steel');

      case 'ukulele':
        // Ukulele: nylon or steel depending on type
        return Array(stringCount).fill('nylon');

      case 'banjo':
        // Banjo: steel strings
        return Array(stringCount).fill('steel');

      case 'mandolin':
        // Mandolin: steel strings
        return Array(stringCount).fill('steel');

      default:
        return Array(stringCount).fill('steel');
    }
  }

  /**
   * Get string gauge for specific string position
   */
  private static getStringGauge(
    bodyStyle: InstrumentBodyStyle,
    stringIndex: number,
    totalStrings: number
  ): number {
    // Base gauges in mm
    const gaugeRanges = {
      classical: { min: 0.7, max: 1.2 },
      acoustic: { min: 0.3, max: 1.2 },
      electric: { min: 0.25, max: 1.1 },
      bass: { min: 1.4, max: 2.7 },
      ukulele: { min: 0.6, max: 0.9 },
      banjo: { min: 0.25, max: 0.64 },
      mandolin: { min: 0.25, max: 0.64 },
      lute: { min: 0.5, max: 1.0 },
      generic: { min: 0.5, max: 1.0 },
    };

    const range = gaugeRanges[bodyStyle] || gaugeRanges.generic;
    const progress = stringIndex / (totalStrings - 1);
    
    return range.min + (range.max - range.min) * progress;
  }

  /**
   * Get wood roughness based on instrument type
   */
  private static getWoodRoughness(bodyStyle: InstrumentBodyStyle): number {
    switch (bodyStyle) {
      case 'classical': return 0.8; // Matte finish
      case 'acoustic': return 0.6;  // Semi-gloss
      case 'electric': return 0.3;  // Glossy
      case 'bass': return 0.4;      // Semi-gloss
      case 'ukulele': return 0.7;   // Natural finish
      case 'banjo': return 0.9;     // Raw wood
      case 'mandolin': return 0.4;  // Glossy
      case 'lute': return 0.9;      // Historical finish
      default: return 0.6;
    }
  }

  /**
   * Get wood clearcoat based on instrument type
   */
  private static getWoodClearcoat(bodyStyle: InstrumentBodyStyle): number {
    switch (bodyStyle) {
      case 'classical': return 0.1; // Minimal clearcoat
      case 'acoustic': return 0.5;  // Medium clearcoat
      case 'electric': return 1.0;  // High gloss
      case 'bass': return 0.7;      // Good clearcoat
      case 'ukulele': return 0.2;   // Light clearcoat
      case 'banjo': return 0.0;     // No clearcoat
      case 'mandolin': return 0.8;  // High gloss
      case 'lute': return 0.0;      // Historical - no clearcoat
      default: return 0.5;
    }
  }

  /**
   * Add wood texture to material (placeholder for future texture loading)
   */
  private static addWoodTexture(
    material: THREE.MeshPhysicalMaterial,
    bodyStyle: InstrumentBodyStyle
  ): void {
    // TODO: Load and apply wood grain textures
    // This would load appropriate wood textures based on body style
    // For now, we rely on procedural materials
    
    // Example implementation:
    // const textureUrl = this.getWoodTextureUrl(bodyStyle);
    // if (textureUrl) {
    //   const texture = this.textureLoader.load(textureUrl);
    //   texture.wrapS = texture.wrapT = THREE.RepeatWrapping;
    //   texture.repeat.set(4, 1); // Adjust for wood grain direction
    //   material.map = texture;
    // }
  }

  /**
   * Clear material cache (useful for memory management)
   */
  static clearCache(): void {
    for (const material of this.materialCache.values()) {
      material.dispose();
    }
    this.materialCache.clear();
  }

  /**
   * Get material cache statistics
   */
  static getCacheStats(): { count: number; types: string[] } {
    return {
      count: this.materialCache.size,
      types: Array.from(this.materialCache.keys()),
    };
  }
}
