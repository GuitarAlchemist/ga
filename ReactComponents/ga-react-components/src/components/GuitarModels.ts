/**
 * Guitar model templates with visual styling for Pixi.js rendering
 */

import type { NeckProfile } from './NeckProfiles';

export type InlayStyle = 'dots' | 'blocks' | 'trapezoid' | 'triangle' | 'abalone' | 'tree' | 'crown' | 'none';

export type HeadstockStyle =
  | 'classical'    // Classical guitar slotted headstock
  | 'acoustic'     // Acoustic guitar 3x3 tuners (Martin/Gibson style)
  | 'electric';    // Electric guitar 6-inline tuners (Fender style)

export interface GuitarModelStyle {
  name: string;
  category: 'classical' | 'acoustic' | 'electric';
  brand: string;
  model: string;

  // Visual properties
  woodColor: number; // Hex color for fretboard wood
  stringColor: number; // Hex color for strings
  fretColor: number; // Hex color for frets
  nutColor: number; // Hex color for nut
  markerColor: number; // Hex color for fret markers

  // Fretboard properties
  fretCount: number;
  stringCount: number;
  tuning: string[];

  // Neck dimensions in mm
  scaleLength?: number;
  nutWidth?: number; // Width at nut in mm
  bridgeWidth?: number; // Width at bridge in mm

  // Neck profile
  neckProfileId?: string; // Reference to NeckProfile
  neckProfile?: NeckProfile; // Optional embedded profile

  // Headstock
  headstockStyle?: HeadstockStyle;

  // Inlay style options
  inlayStyle?: InlayStyle;
  inlayColor?: number; // Hex color for inlays
}

export const GUITAR_MODELS: Record<string, GuitarModelStyle> = {
  // Classical Guitars
  'classical_yamaha_cg': {
    name: 'Yamaha CG Series',
    category: 'classical',
    brand: 'Yamaha',
    model: 'CG-192',
    woodColor: 0x8b4513, // Saddle brown
    stringColor: 0xd4af37, // Gold
    fretColor: 0xb8860b, // Dark goldenrod
    nutColor: 0xf5f5dc, // Beige/bone
    markerColor: 0xffd700, // Gold
    fretCount: 22,
    stringCount: 6,
    tuning: ['E', 'B', 'G', 'D', 'A', 'E'],
    scaleLength: 650,
    nutWidth: 52,
    bridgeWidth: 62,
    neckProfileId: 'classical-standard',
    headstockStyle: 'classical',
    inlayStyle: 'dots',
    inlayColor: 0xffd700, // Gold dots
  },
  'classical_torres': {
    name: 'Torres Style',
    category: 'classical',
    brand: 'Torres',
    model: 'Replica',
    woodColor: 0x654321, // Dark brown
    stringColor: 0xd4af37, // Gold
    fretColor: 0xa0826d, // Tan
    nutColor: 0xf5deb3, // Wheat
    markerColor: 0xffd700, // Gold
    fretCount: 22,
    stringCount: 6,
    tuning: ['E', 'B', 'G', 'D', 'A', 'E'],
    scaleLength: 665,
    nutWidth: 52,
    bridgeWidth: 62,
    neckProfileId: 'classical-standard',
    headstockStyle: 'classical',
    inlayStyle: 'blocks',
    inlayColor: 0xffd700, // Gold blocks
  },
  'classical_alhambra': {
    name: 'Alhambra 9P',
    category: 'classical',
    brand: 'Alhambra',
    model: '9P',
    woodColor: 0x704214, // Sepia
    stringColor: 0xd4af37, // Gold
    fretColor: 0xb8860b, // Dark goldenrod
    nutColor: 0xf5f5dc, // Beige
    markerColor: 0xffd700, // Gold
    fretCount: 22,
    stringCount: 6,
    tuning: ['E', 'B', 'G', 'D', 'A', 'E'],
    scaleLength: 650,
    nutWidth: 52,
    bridgeWidth: 62,
    neckProfileId: 'classical-standard',
    headstockStyle: 'classical',
    inlayStyle: 'abalone',
    inlayColor: 0x00d4ff, // Cyan abalone
  },

  // Acoustic Guitars
  'acoustic_martin_d28': {
    name: 'Martin D-28',
    category: 'acoustic',
    brand: 'Martin',
    model: 'D-28',
    woodColor: 0x3d2817, // Dark wood
    stringColor: 0xc0c0c0, // Silver
    fretColor: 0x8b7355, // Burlywood
    nutColor: 0xf5f5dc, // Bone
    markerColor: 0xffd700, // Gold
    fretCount: 22,
    stringCount: 6,
    tuning: ['E', 'B', 'G', 'D', 'A', 'E'],
    scaleLength: 645,
    nutWidth: 43,
    bridgeWidth: 55,
    neckProfileId: 'modern-c',
    headstockStyle: 'acoustic',
    inlayStyle: 'trapezoid',
    inlayColor: 0xffd700, // Gold trapezoids
  },
  'acoustic_taylor_814': {
    name: 'Taylor 814ce',
    category: 'acoustic',
    brand: 'Taylor',
    model: '814ce',
    woodColor: 0x4a3728, // Medium brown
    stringColor: 0xc0c0c0, // Silver
    fretColor: 0x9a7c6d, // Tan
    nutColor: 0xf5f5dc, // Bone
    markerColor: 0xffd700, // Gold
    fretCount: 22,
    stringCount: 6,
    tuning: ['E', 'B', 'G', 'D', 'A', 'E'],
    scaleLength: 648,
    nutWidth: 43,
    bridgeWidth: 55,
    neckProfileId: 'modern-c',
    headstockStyle: 'acoustic',
    inlayStyle: 'triangle',
    inlayColor: 0xffd700, // Gold triangles
  },
  'acoustic_gibson_j45': {
    name: 'Gibson J-45',
    category: 'acoustic',
    brand: 'Gibson',
    model: 'J-45',
    woodColor: 0x5c4033, // Dark brown
    stringColor: 0xc0c0c0, // Silver
    fretColor: 0xa0826d, // Tan
    nutColor: 0xf5f5dc, // Bone
    markerColor: 0xffd700, // Gold
    fretCount: 22,
    stringCount: 6,
    tuning: ['E', 'B', 'G', 'D', 'A', 'E'],
    scaleLength: 645,
    nutWidth: 43,
    bridgeWidth: 55,
    neckProfileId: 'rounded-profile',
    headstockStyle: 'acoustic',
    inlayStyle: 'tree',
    inlayColor: 0xffd700, // Gold tree inlays
  },

  // Electric Guitars
  'electric_fender_strat': {
    name: 'Fender Stratocaster',
    category: 'electric',
    brand: 'Fender',
    model: 'Stratocaster',
    woodColor: 0x2a2a2a, // Dark gray/black
    stringColor: 0xc0c0c0, // Silver
    fretColor: 0x696969, // Dim gray
    nutColor: 0xf5f5dc, // Bone
    markerColor: 0xffffff, // White
    fretCount: 22,
    stringCount: 6,
    tuning: ['E', 'B', 'G', 'D', 'A', 'E'],
    scaleLength: 648,
    nutWidth: 42,
    bridgeWidth: 52,
    neckProfileId: 'modern-c',
    headstockStyle: 'electric',
    inlayStyle: 'dots',
    inlayColor: 0xffffff, // White dots
  },

  'electric_fender_telecaster': {
    name: 'Fender Telecaster',
    category: 'electric',
    brand: 'Fender',
    model: 'Telecaster',
    woodColor: 0x8B4513, // Saddle brown (classic Telecaster maple neck)
    stringColor: 0xc0c0c0, // Silver
    fretColor: 0x696969, // Dim gray
    nutColor: 0xf5f5dc, // Bone
    markerColor: 0x000000, // Black dots (classic Telecaster style)
    fretCount: 22,
    stringCount: 6,
    tuning: ['E', 'B', 'G', 'D', 'A', 'E'],
    scaleLength: 648, // 25.5" scale length (standard Fender)
    nutWidth: 42.8, // 1.685" nut width (standard Telecaster)
    bridgeWidth: 52,
    neckProfileId: 'vintage-c', // Classic Telecaster neck profile
    headstockStyle: 'electric',
    inlayStyle: 'dots',
    inlayColor: 0x000000, // Black dots
  },
  'electric_gibson_les_paul': {
    name: 'Gibson Les Paul',
    category: 'electric',
    brand: 'Gibson',
    model: 'Les Paul',
    woodColor: 0x3d2817, // Dark brown
    stringColor: 0xc0c0c0, // Silver
    fretColor: 0x8b7355, // Burlywood
    nutColor: 0xf5f5dc, // Bone
    markerColor: 0xffd700, // Gold
    fretCount: 22,
    stringCount: 6,
    tuning: ['E', 'B', 'G', 'D', 'A', 'E'],
    scaleLength: 628,
    nutWidth: 42,
    bridgeWidth: 52,
    neckProfileId: 'slim-taper',
    headstockStyle: 'electric',
    inlayStyle: 'crown',
    inlayColor: 0xffd700, // Gold crown inlays
  },
  'electric_ibanez_rg': {
    name: 'Ibanez RG',
    category: 'electric',
    brand: 'Ibanez',
    model: 'RG Series',
    woodColor: 0x1a1a1a, // Almost black
    stringColor: 0xc0c0c0, // Silver
    fretColor: 0x505050, // Dark gray
    nutColor: 0xf5f5dc, // Bone
    markerColor: 0xff6b6b, // Red
    fretCount: 22,
    stringCount: 6,
    tuning: ['E', 'B', 'G', 'D', 'A', 'E'],
    scaleLength: 648,
    nutWidth: 41,
    bridgeWidth: 50,
    neckProfileId: 'wizard',
    headstockStyle: 'electric',
    inlayStyle: 'blocks',
    inlayColor: 0xff6b6b, // Red blocks
  },
};

export const GUITAR_CATEGORIES = {
  classical: [
    'classical_yamaha_cg',
    'classical_torres',
    'classical_alhambra',
  ],
  acoustic: [
    'acoustic_martin_d28',
    'acoustic_taylor_814',
    'acoustic_gibson_j45',
  ],
  electric: [
    'electric_fender_strat',
    'electric_fender_telecaster',
    'electric_gibson_les_paul',
    'electric_ibanez_rg',
  ],
};

export function getGuitarModel(modelId: string): GuitarModelStyle {
  return GUITAR_MODELS[modelId] || GUITAR_MODELS['classical_yamaha_cg'];
}

export function getModelsByCategory(category: 'classical' | 'acoustic' | 'electric'): GuitarModelStyle[] {
  const modelIds = GUITAR_CATEGORIES[category];
  return modelIds.map(id => GUITAR_MODELS[id]);
}

export function getAllModels(): GuitarModelStyle[] {
  return Object.values(GUITAR_MODELS);
}

