/**
 * Instrument Loader
 * 
 * Utilities to load and parse instrument configurations from Instruments.yaml
 */

import type { InstrumentConfig } from '../types/InstrumentConfig';
import { createInstrumentConfig } from '../types/InstrumentConfig';

/**
 * In-memory cache of loaded instruments
 */
let instrumentCache: Map<string, InstrumentConfig[]> | null = null;

/**
 * Parse YAML-like text into instrument database
 * 
 * This is a simple parser for the specific format of Instruments.yaml.
 * For production, consider using a proper YAML parser library.
 * 
 * @param yamlText - Raw YAML text
 * @returns Map of instrument family to configurations
 */
export function parseInstrumentsYAML(yamlText: string): Map<string, InstrumentConfig[]> {
  const instruments = new Map<string, InstrumentConfig[]>();
  const lines = yamlText.split('\n');
  
  let currentFamily: string | null = null;
  let currentVariant: string | null = null;
  let currentDisplayName: string | null = null;
  let currentFullName: string | null = null;
  let currentTuning: string | null = null;
  
  for (let i = 0; i < lines.length; i++) {
    const line = lines[i];
    const trimmed = line.trim();
    
    // Skip empty lines and comments
    if (!trimmed || trimmed.startsWith('#')) continue;
    
    // Detect instrument family (no indentation)
    if (!line.startsWith(' ') && line.includes(':')) {
      const match = line.match(/^(\w+):/);
      if (match) {
        currentFamily = match[1];
        currentVariant = null;
        currentDisplayName = null;
        currentFullName = null;
        currentTuning = null;
      }
      continue;
    }
    
    // Detect variant (2 spaces indentation)
    if (line.startsWith('  ') && !line.startsWith('    ') && line.includes(':')) {
      const match = line.match(/^\s{2}(\w+):/);
      if (match) {
        // Save previous variant if exists
        if (currentFamily && currentVariant && currentTuning) {
          const config = createInstrumentConfig(
            currentFamily,
            currentVariant,
            currentTuning,
            currentDisplayName || currentVariant,
            currentFullName || undefined
          );
          
          if (!instruments.has(currentFamily)) {
            instruments.set(currentFamily, []);
          }
          instruments.get(currentFamily)!.push(config);
        }
        
        currentVariant = match[1];
        currentDisplayName = null;
        currentFullName = null;
        currentTuning = null;
      }
      continue;
    }
    
    // Detect properties (4 spaces indentation)
    if (line.startsWith('    ') && line.includes(':')) {
      const match = line.match(/^\s{4}(\w+):\s*"?([^"]+)"?/);
      if (match) {
        const [, key, value] = match;
        
        if (key === 'DisplayName') {
          currentDisplayName = value.replace(/"/g, '');
        } else if (key === 'FullName') {
          currentFullName = value.replace(/"/g, '');
        } else if (key === 'Tuning') {
          currentTuning = value.replace(/"/g, '');
        }
      }
    }
  }
  
  // Save last variant
  if (currentFamily && currentVariant && currentTuning) {
    const config = createInstrumentConfig(
      currentFamily,
      currentVariant,
      currentTuning,
      currentDisplayName || currentVariant,
      currentFullName || undefined
    );
    
    if (!instruments.has(currentFamily)) {
      instruments.set(currentFamily, []);
    }
    instruments.get(currentFamily)!.push(config);
  }
  
  return instruments;
}

/**
 * Load instruments from YAML file
 * 
 * @param yamlPath - Path to Instruments.yaml file
 * @returns Map of instrument family to configurations
 */
export async function loadInstruments(
  yamlPath: string = '/config/Instruments.yaml'
): Promise<Map<string, InstrumentConfig[]>> {
  // Return cached instruments if available
  if (instrumentCache) {
    return instrumentCache;
  }
  
  try {
    const response = await fetch(yamlPath);
    if (!response.ok) {
      throw new Error(`Failed to load instruments: ${response.statusText}`);
    }
    
    const yamlText = await response.text();
    instrumentCache = parseInstrumentsYAML(yamlText);
    return instrumentCache;
  } catch (error) {
    console.error('Error loading instruments:', error);
    return new Map();
  }
}

/**
 * Get all instruments for a specific family
 * 
 * @param family - Instrument family (e.g., "Guitar", "BassGuitar", "Ukulele")
 * @returns Array of instrument configurations
 */
export async function getInstrumentsByFamily(
  family: string
): Promise<InstrumentConfig[]> {
  const instruments = await loadInstruments();
  return instruments.get(family) || [];
}

/**
 * Get a specific instrument configuration
 * 
 * @param family - Instrument family
 * @param variant - Instrument variant
 * @returns Instrument configuration or null if not found
 */
export async function getInstrument(
  family: string,
  variant: string
): Promise<InstrumentConfig | null> {
  const instruments = await getInstrumentsByFamily(family);
  return instruments.find(i => i.variant === variant) || null;
}

/**
 * Get all instrument families
 *
 * @returns Array of instrument family names
 */
export async function getInstrumentFamilies(): Promise<string[]> {
  const instruments = await loadInstruments();
  return Array.from(instruments.keys()).sort();
}

/**
 * Alias for getInstrumentFamilies (for compatibility)
 */
export const getAllInstrumentFamilies = getInstrumentFamilies;

/**
 * Search instruments by name
 * 
 * @param query - Search query
 * @returns Array of matching instrument configurations
 */
export async function searchInstruments(query: string): Promise<InstrumentConfig[]> {
  const instruments = await loadInstruments();
  const results: InstrumentConfig[] = [];
  const lowerQuery = query.toLowerCase();
  
  for (const configs of instruments.values()) {
    for (const config of configs) {
      if (
        config.family.toLowerCase().includes(lowerQuery) ||
        config.variant.toLowerCase().includes(lowerQuery) ||
        config.displayName.toLowerCase().includes(lowerQuery) ||
        config.fullName?.toLowerCase().includes(lowerQuery)
      ) {
        results.push(config);
      }
    }
  }
  
  return results;
}

/**
 * Get instrument statistics
 * 
 * @returns Statistics about loaded instruments
 */
export async function getInstrumentStats(): Promise<{
  totalFamilies: number;
  totalVariants: number;
  stringCounts: Map<number, number>;
  bodyStyles: Map<string, number>;
}> {
  const instruments = await loadInstruments();
  
  const stats = {
    totalFamilies: instruments.size,
    totalVariants: 0,
    stringCounts: new Map<number, number>(),
    bodyStyles: new Map<string, number>(),
  };
  
  for (const configs of instruments.values()) {
    stats.totalVariants += configs.length;
    
    for (const config of configs) {
      // Count string configurations
      const stringCount = config.tuning.length;
      stats.stringCounts.set(
        stringCount,
        (stats.stringCounts.get(stringCount) || 0) + 1
      );
      
      // Count body styles
      stats.bodyStyles.set(
        config.bodyStyle,
        (stats.bodyStyles.get(config.bodyStyle) || 0) + 1
      );
    }
  }
  
  return stats;
}

/**
 * Clear the instrument cache
 * Useful for testing or when reloading instruments
 */
export function clearInstrumentCache(): void {
  instrumentCache = null;
}

/**
 * Preset instrument configurations for common use cases
 */
export const PRESET_INSTRUMENTS = {
  standardGuitar: (): InstrumentConfig => ({
    family: 'Guitar',
    variant: 'Standard',
    displayName: 'Standard Guitar',
    tuning: ['E2', 'A2', 'D3', 'G3', 'B3', 'E4'],
    scaleLength: 650,
    nutWidth: 52,
    bridgeWidth: 70,
    fretCount: 19,
    bodyStyle: 'classical',
  }),
  
  standardBass: (): InstrumentConfig => ({
    family: 'BassGuitar',
    variant: 'Standard',
    displayName: 'Standard Bass',
    tuning: ['E1', 'A1', 'D2', 'G2'],
    scaleLength: 860,
    nutWidth: 45,
    bridgeWidth: 60,
    fretCount: 24,
    bodyStyle: 'bass',
  }),
  
  sopranoUkulele: (): InstrumentConfig => ({
    family: 'Ukulele',
    variant: 'SopranoConcertAndTenorC',
    displayName: 'Soprano Ukulele',
    tuning: ['G4', 'C4', 'E4', 'A4'],
    scaleLength: 330,
    nutWidth: 35,
    bridgeWidth: 40,
    fretCount: 12,
    bodyStyle: 'ukulele',
    hasRosette: true,
  }),
  
  bluegrassBanjo: (): InstrumentConfig => ({
    family: 'Banjo',
    variant: 'Bluegrass5Strings',
    displayName: 'Bluegrass Banjo',
    tuning: ['G4', 'D3', 'G3', 'B3', 'D4'],
    scaleLength: 660,
    nutWidth: 32,
    bridgeWidth: 35,
    fretCount: 22,
    bodyStyle: 'banjo',
    hasDroneString: true,
    droneStringPosition: 0.2,
  }),
};

