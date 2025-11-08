/**
 * Guitar Neck Profile Types and Specifications
 * Based on real-world neck profiles from Ibanez, Fender, Gibson, and other manufacturers
 */

/**
 * Neck profile shape types
 */
export type NeckProfileShape = 
  | 'C'           // Classic rounded C-shape (Fender standard)
  | 'D'           // Flatter D-shape (Gibson standard)
  | 'U'           // Thick U-shape (vintage Fender)
  | 'V'           // V-shape (vintage guitars)
  | 'Wizard'      // Ibanez Wizard - thin and flat
  | 'Wizard II'   // Ibanez Wizard II - slightly thicker
  | 'Wizard III'  // Ibanez Wizard III - modern thin
  | 'Super Wizard' // Ibanez Super Wizard - ultra-thin
  | 'Nitro Wizard' // Ibanez Nitro Wizard - thin with satin finish
  | 'Oval C'      // Oval C-shape (modern Fender)
  | 'Soft V'      // Soft V-shape (vintage-inspired)
  | 'Asymmetric'  // Asymmetric profile (bass side thicker)
  | 'Compound'    // Compound radius (changes along neck)
  | 'Modern C'    // Modern C-shape (flatter than classic)
  | 'Slim Taper'  // Gibson Slim Taper
  | 'Chunky C'    // Thick C-shape
  | 'Flat Oval';  // Flat oval (classical guitars)

/**
 * Fretboard radius types (in inches)
 */
export type FretboardRadius = 
  | 7.25   // Vintage Fender
  | 9.5    // Modern Fender
  | 10     // Gibson standard
  | 12     // Ibanez standard
  | 14     // Modern flat
  | 15.75  // Jackson/Charvel
  | 16     // Very flat (shred guitars)
  | 20     // Extremely flat (classical)
  | 'compound-10-16'  // Compound radius
  | 'compound-12-16'; // Compound radius

/**
 * Neck finish types
 */
export type NeckFinish = 
  | 'gloss'       // Glossy polyurethane
  | 'satin'       // Satin/matte finish
  | 'oil'         // Oil finish (natural feel)
  | 'nitro'       // Nitrocellulose lacquer
  | 'raw'         // Unfinished wood
  | 'roasted';    // Roasted maple (darker, smoother)

/**
 * Fretboard material types
 */
export type FretboardMaterial = 
  | 'rosewood'    // Classic rosewood
  | 'maple'       // Maple (often with finish)
  | 'ebony'       // Ebony (dark, smooth)
  | 'pau-ferro'   // Pau Ferro (rosewood alternative)
  | 'richlite'    // Richlite (synthetic)
  | 'jatoba'      // Jatoba (Brazilian cherry)
  | 'laurel'      // Laurel
  | 'baked-maple' // Baked/roasted maple
  | 'cedar';      // Cedar (classical guitars)

/**
 * Neck profile specification
 */
export interface NeckProfile {
  name: string;
  shape: NeckProfileShape;
  description: string;
  
  // Dimensions (in mm)
  thickness1stFret: number;   // Thickness at 1st fret
  thickness12thFret: number;  // Thickness at 12th fret
  
  // Fretboard specs
  radius: FretboardRadius;
  material: FretboardMaterial;
  finish: NeckFinish;
  
  // Visual properties for rendering
  visualCurve: number;        // 0-1, how rounded the profile appears
  asymmetry: number;          // 0-1, 0 = symmetric, 1 = fully asymmetric
  
  // Common on these guitar types
  commonOn: string[];
  
  // Era/style
  era?: 'vintage' | 'modern' | 'contemporary';
  playStyle?: string[];       // e.g., ['shred', 'jazz', 'blues']
}

/**
 * Comprehensive neck profile database
 */
export const NECK_PROFILES: Record<string, NeckProfile> = {
  // Ibanez Profiles
  'wizard': {
    name: 'Wizard',
    shape: 'Wizard',
    description: 'Ibanez signature thin, flat profile for fast playing',
    thickness1stFret: 19,
    thickness12thFret: 21,
    radius: 12,
    material: 'maple',
    finish: 'satin',
    visualCurve: 0.3,
    asymmetry: 0,
    commonOn: ['Ibanez RG', 'Ibanez S', 'Ibanez Prestige'],
    era: 'modern',
    playStyle: ['shred', 'metal', 'rock'],
  },
  'wizard-ii': {
    name: 'Wizard II',
    shape: 'Wizard II',
    description: 'Slightly thicker than original Wizard, more comfortable for rhythm',
    thickness1stFret: 20,
    thickness12thFret: 22,
    radius: 12,
    material: 'maple',
    finish: 'satin',
    visualCurve: 0.35,
    asymmetry: 0,
    commonOn: ['Ibanez RG', 'Ibanez Premium'],
    era: 'modern',
    playStyle: ['rock', 'metal', 'versatile'],
  },
  'super-wizard': {
    name: 'Super Wizard',
    shape: 'Super Wizard',
    description: 'Ultra-thin profile for maximum speed',
    thickness1stFret: 17,
    thickness12thFret: 19,
    radius: 16,
    material: 'maple',
    finish: 'satin',
    visualCurve: 0.25,
    asymmetry: 0,
    commonOn: ['Ibanez J.Custom', 'Ibanez Prestige'],
    era: 'contemporary',
    playStyle: ['shred', 'technical'],
  },
  'nitro-wizard': {
    name: 'Nitro Wizard',
    shape: 'Nitro Wizard',
    description: 'Wizard profile with nitrocellulose finish for vintage feel',
    thickness1stFret: 19,
    thickness12thFret: 21,
    radius: 12,
    material: 'maple',
    finish: 'nitro',
    visualCurve: 0.3,
    asymmetry: 0,
    commonOn: ['Ibanez AZ', 'Ibanez Prestige'],
    era: 'contemporary',
    playStyle: ['blues', 'rock', 'versatile'],
  },

  // Fender Profiles
  'modern-c': {
    name: 'Modern C',
    shape: 'Modern C',
    description: 'Fender modern C-shape, flatter than vintage',
    thickness1stFret: 21,
    thickness12thFret: 23,
    radius: 9.5,
    material: 'maple',
    finish: 'gloss',
    visualCurve: 0.5,
    asymmetry: 0,
    commonOn: ['Fender Stratocaster', 'Fender Telecaster'],
    era: 'modern',
    playStyle: ['rock', 'blues', 'pop'],
  },
  'vintage-c': {
    name: 'Vintage C',
    shape: 'C',
    description: 'Classic rounded C-shape from 1960s Fenders',
    thickness1stFret: 22,
    thickness12thFret: 24,
    radius: 7.25,
    material: 'maple',
    finish: 'gloss',
    visualCurve: 0.6,
    asymmetry: 0,
    commonOn: ['Fender Stratocaster', 'Fender Telecaster'],
    era: 'vintage',
    playStyle: ['blues', 'classic rock', 'country'],
  },
  'soft-v': {
    name: 'Soft V',
    shape: 'Soft V',
    description: 'Vintage-inspired V-shape, comfortable and distinctive',
    thickness1stFret: 23,
    thickness12thFret: 25,
    radius: 7.25,
    material: 'maple',
    finish: 'nitro',
    visualCurve: 0.4,
    asymmetry: 0,
    commonOn: ['Fender Custom Shop', 'Vintage Reissues'],
    era: 'vintage',
    playStyle: ['blues', 'rockabilly', 'vintage'],
  },
  'chunky-u': {
    name: 'Chunky U',
    shape: 'U',
    description: 'Thick U-shape from 1950s Fenders, substantial feel',
    thickness1stFret: 24,
    thickness12thFret: 26,
    radius: 7.25,
    material: 'maple',
    finish: 'nitro',
    visualCurve: 0.7,
    asymmetry: 0,
    commonOn: ['Fender 50s Reissues'],
    era: 'vintage',
    playStyle: ['blues', 'rockabilly'],
  },

  // Gibson Profiles
  'slim-taper': {
    name: 'Slim Taper',
    shape: 'Slim Taper',
    description: 'Gibson 60s slim taper, fast and comfortable',
    thickness1stFret: 20,
    thickness12thFret: 22,
    radius: 10,
    material: 'rosewood',
    finish: 'gloss',
    visualCurve: 0.45,
    asymmetry: 0,
    commonOn: ['Gibson Les Paul', 'Gibson SG'],
    era: 'vintage',
    playStyle: ['rock', 'blues', 'jazz'],
  },
  'rounded-profile': {
    name: 'Rounded Profile',
    shape: 'D',
    description: 'Gibson 50s rounded profile, chunky and substantial',
    thickness1stFret: 22,
    thickness12thFret: 25,
    radius: 10,
    material: 'rosewood',
    finish: 'gloss',
    visualCurve: 0.6,
    asymmetry: 0,
    commonOn: ['Gibson Les Paul', 'Gibson ES-335'],
    era: 'vintage',
    playStyle: ['blues', 'jazz', 'classic rock'],
  },
  'asymmetric-slim-taper': {
    name: 'Asymmetric Slim Taper',
    shape: 'Asymmetric',
    description: 'Modern Gibson asymmetric profile, thicker on bass side',
    thickness1stFret: 20,
    thickness12thFret: 22,
    radius: 10,
    material: 'ebony',
    finish: 'satin',
    visualCurve: 0.45,
    asymmetry: 0.3,
    commonOn: ['Gibson Modern Collection'],
    era: 'contemporary',
    playStyle: ['rock', 'metal', 'versatile'],
  },

  // Classical Guitar Profiles
  'classical-standard': {
    name: 'Classical Standard',
    shape: 'Flat Oval',
    description: 'Traditional classical guitar neck, wide and flat',
    thickness1stFret: 20,
    thickness12thFret: 22,
    radius: 20,
    material: 'cedar',
    finish: 'satin',
    visualCurve: 0.5,
    asymmetry: 0,
    commonOn: ['Classical Guitars'],
    era: 'vintage',
    playStyle: ['classical', 'flamenco', 'fingerstyle'],
  },

  // Modern Shred Profiles
  'ultra-thin': {
    name: 'Ultra Thin',
    shape: 'Modern C',
    description: 'Modern ultra-thin profile for maximum speed',
    thickness1stFret: 18,
    thickness12thFret: 20,
    radius: 16,
    material: 'ebony',
    finish: 'satin',
    visualCurve: 0.25,
    asymmetry: 0,
    commonOn: ['Jackson', 'Charvel', 'ESP'],
    era: 'contemporary',
    playStyle: ['shred', 'metal', 'technical'],
  },
};

/**
 * Get neck profile by ID
 */
export function getNeckProfile(profileId: string): NeckProfile {
  return NECK_PROFILES[profileId] || NECK_PROFILES['modern-c'];
}

/**
 * Get all neck profiles
 */
export function getAllNeckProfiles(): NeckProfile[] {
  return Object.values(NECK_PROFILES);
}

/**
 * Get neck profiles by shape
 */
export function getNeckProfilesByShape(shape: NeckProfileShape): NeckProfile[] {
  return Object.values(NECK_PROFILES).filter(profile => profile.shape === shape);
}

/**
 * Get neck profiles by era
 */
export function getNeckProfilesByEra(era: 'vintage' | 'modern' | 'contemporary'): NeckProfile[] {
  return Object.values(NECK_PROFILES).filter(profile => profile.era === era);
}

/**
 * Get recommended neck profile for play style
 */
export function getProfilesForPlayStyle(playStyle: string): NeckProfile[] {
  return Object.values(NECK_PROFILES).filter(
    profile => profile.playStyle?.includes(playStyle)
  );
}

