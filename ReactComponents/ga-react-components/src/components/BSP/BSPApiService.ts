// BSP API Service for communicating with the backend BSP endpoints

export interface BSPRegion {
  name: string;
  tonalityType: string;
  tonalCenter: number;
  pitchClasses: string[];
}

export interface BSPElement {
  name: string;
  tonalityType: string;
  tonalCenter: number;
  pitchClasses: string[];
}

export interface BSPAnalysis {
  containedInRegion: boolean;
  commonTones: number;
  totalTones: number;
  fitPercentage: number;
}

export interface BSPSpatialQueryResponse {
  queryChord: string;
  radius: number;
  strategy: string;
  region: BSPRegion;
  elements: BSPElement[];
  confidence: number;
  queryTimeMs: number;
}

export interface BSPTonalContextResponse {
  queryChord: string;
  region: BSPRegion;
  confidence: number;
  queryTimeMs: number;
  analysis: BSPAnalysis;
}

export interface BSPChordRequest {
  name: string;
  pitchClasses: string;
}

export interface BSPProgressionRequest {
  chords: BSPChordRequest[];
}

export interface BSPChordAnalysis {
  name: string;
  pitchClasses: string;
  region: BSPRegion;
  confidence: number;
  queryTimeMs: number;
}

export interface BSPTransition {
  fromChord: string;
  toChord: string;
  distance: number;
  commonTones: number;
  smoothness: number;
}

export interface BSPOverallAnalysis {
  averageConfidence: number;
  averageDistance: number;
  averageSmoothness: number;
  totalCommonTones: number;
  progressionLength: number;
}

export interface BSPProgressionAnalysisResponse {
  progression: string[];
  chordAnalyses: BSPChordAnalysis[];
  transitions: BSPTransition[];
  overallAnalysis: BSPOverallAnalysis;
}

export interface BSPTreeInfoResponse {
  rootRegion: string;
  totalRegions: number;
  maxDepth: number;
  partitionStrategies: string[];
  supportedOperations: string[];
}

export interface BSPPartition {
  strategy: string;
  referencePoint: number;
  threshold: number;
  normal: number[];
}

export interface BSPNode {
  region: BSPRegion;
  partition?: BSPPartition;
  left?: BSPNode;
  right?: BSPNode;
  isLeaf: boolean;
  depth: number;
  elements: BSPElement[];
}

export interface BSPTreeStructureResponse {
  root: BSPNode;
  nodeCount: number;
  maxDepth: number;
  regionCount: number;
  partitionCount: number;
}

export interface ApiResponse<T> {
  success: boolean;
  data?: T;
  error?: string;
  errorDetails?: string;
  timestamp: string;
}

// Voicing and chord detail interfaces
export interface VoicingPosition {
  string: number;
  fret: number;
  finger?: number;
  note?: string;
}

export interface VoicingWithAnalysis {
  positions: VoicingPosition[];
  difficulty: string;
  fretRange: { min: number; max: number };
  cagedShape?: string;
  hasOpenStrings: boolean;
  hasMutedStrings: boolean;
  hasBarres: boolean;
  consonance: number;
  voiceLeading?: string;
  notes: string[];
  intervals: string[];
}

export interface ChordInContext {
  name: string;
  root: string;
  quality: string;
  extension: string;
  stackingType: string;
  intervals: string[];
  pitchClasses: number[];
  degreeInKey?: number;
  function?: string;
  naturallyOccurring: boolean;
}

export interface ScaleModeInfo {
  name: string;
  family: string;
  degree: number;
  notes: string[];
  intervals: string[];
  pitchClasses: number[];
  characteristics: string[];
}

export interface KeyInfo {
  name: string;
  tonalCenter: string;
  quality: string;
  scale: ScaleModeInfo;
  diatonicChords: ChordInContext[];
}

class BSPApiServiceClass {
  private baseUrl: string;

  constructor() {
    // Default to localhost for development, can be configured via environment
    this.baseUrl = import.meta.env.VITE_API_BASE_URL || 'https://localhost:7184';
  }

  private async makeRequest<T>(endpoint: string, options: RequestInit = {}): Promise<T> {
    const url = `${this.baseUrl}/api/bsp${endpoint}`;

    const defaultOptions: RequestInit = {
      headers: {
        'Content-Type': 'application/json',
        ...options.headers,
      },
      ...options,
    };

    try {
      const response = await fetch(url, defaultOptions);



      if (!response.ok) {
        throw new Error(`HTTP error! status: ${response.status}`);
      }

      const apiResponse: ApiResponse<T> = await response.json();

      if (!apiResponse.success) {
        throw new Error(apiResponse.error || 'API request failed');
      }

      if (!apiResponse.data) {
        throw new Error('No data received from API');
      }

      return apiResponse.data;
    } catch (error) {
      // Silently fail - API is optional, demo mode will be used
      // Only log in development mode
      if (import.meta.env.DEV) {
        console.debug('BSP API not available (using demo mode):', endpoint);
      }
      throw error instanceof Error ? error : new Error('Unknown API error');
    }
  }

  /**
   * Perform a spatial query to find similar chords
   */
  async spatialQuery(
    pitchClasses: string, 
    radius: number = 0.5, 
    strategy: string = 'CircleOfFifths'
  ): Promise<BSPSpatialQueryResponse> {
    const params = new URLSearchParams({
      pitchClasses,
      radius: radius.toString(),
      strategy,
    });

    return this.makeRequest<BSPSpatialQueryResponse>(`/spatial-query?${params}`);
  }

  /**
   * Get tonal context for a chord
   */
  async getTonalContext(pitchClasses: string): Promise<BSPTonalContextResponse> {
    const params = new URLSearchParams({ pitchClasses });
    return this.makeRequest<BSPTonalContextResponse>(`/tonal-context?${params}`);
  }

  /**
   * Analyze a chord progression
   */
  async analyzeProgression(chords: BSPChordRequest[]): Promise<BSPProgressionAnalysisResponse> {
    const request: BSPProgressionRequest = { chords };
    
    return this.makeRequest<BSPProgressionAnalysisResponse>('/analyze-progression', {
      method: 'POST',
      body: JSON.stringify(request),
    });
  }

  /**
   * Get BSP tree information
   */
  async getTreeInfo(): Promise<BSPTreeInfoResponse> {
    return this.makeRequest<BSPTreeInfoResponse>('/tree-info');
  }

  /**
   * Get full BSP tree structure for visualization
   */
  async getTreeStructure(): Promise<BSPTreeStructureResponse> {
    return this.makeRequest<BSPTreeStructureResponse>('/tree-structure');
  }

  /**
   * Test connection to the BSP API
   */
  async testConnection(): Promise<boolean> {
    try {
      await this.getTreeInfo();
      return true;
    } catch (error) {
      console.error('BSP API connection test failed:', error);
      return false;
    }
  }

  /**
   * Get available partition strategies
   */
  getPartitionStrategies(): string[] {
    return [
      'CircleOfFifths',
      'ChromaticDistance', 
      'SetComplexity',
      'TonalHierarchy'
    ];
  }

  /**
   * Validate pitch classes format
   */
  validatePitchClasses(pitchClasses: string): boolean {
    if (!pitchClasses || pitchClasses.trim() === '') {
      return false;
    }

    const validPitchClasses = ['C', 'CSharp', 'D', 'DSharp', 'E', 'F', 'FSharp', 'G', 'GSharp', 'A', 'ASharp', 'B'];
    const parts = pitchClasses.split(',').map(p => p.trim());
    
    return parts.every(part => validPitchClasses.includes(part));
  }

  /**
   * Format pitch classes for display
   */
  formatPitchClasses(pitchClasses: string[]): string {
    return pitchClasses.map(pc => pc.replace('Sharp', '#')).join(', ');
  }

  /**
   * Parse pitch classes from user input
   */
  parsePitchClasses(input: string): string {
    return input
      .split(',')
      .map(pc => pc.trim().replace('#', 'Sharp'))
      .join(',');
  }

  /**
   * Get common chord examples
   */
  getChordExamples(): { name: string; pitchClasses: string }[] {
    return [
      { name: 'C Major', pitchClasses: 'C,E,G' },
      { name: 'A Minor', pitchClasses: 'A,C,E' },
      { name: 'F Major', pitchClasses: 'F,A,C' },
      { name: 'G Major', pitchClasses: 'G,B,D' },
      { name: 'D Minor', pitchClasses: 'D,F,A' },
      { name: 'E Minor', pitchClasses: 'E,G,B' },
      { name: 'C7', pitchClasses: 'C,E,G,ASharp' },
      { name: 'Am7', pitchClasses: 'A,C,E,G' },
      { name: 'Dm7', pitchClasses: 'D,F,A,C' },
      { name: 'G7', pitchClasses: 'G,B,D,F' },
    ];
  }

  /**
   * Get common progression examples
   */
  getProgressionExamples(): { name: string; chords: BSPChordRequest[] }[] {
    return [
      {
        name: 'I-vi-IV-V (C Major)',
        chords: [
          { name: 'C Major', pitchClasses: 'C,E,G' },
          { name: 'A Minor', pitchClasses: 'A,C,E' },
          { name: 'F Major', pitchClasses: 'F,A,C' },
          { name: 'G Major', pitchClasses: 'G,B,D' },
        ]
      },
      {
        name: 'ii-V-I (Jazz)',
        chords: [
          { name: 'Dm7', pitchClasses: 'D,F,A,C' },
          { name: 'G7', pitchClasses: 'G,B,D,F' },
          { name: 'CMaj7', pitchClasses: 'C,E,G,B' },
        ]
      },
      {
        name: 'vi-IV-I-V (Pop)',
        chords: [
          { name: 'A Minor', pitchClasses: 'A,C,E' },
          { name: 'F Major', pitchClasses: 'F,A,C' },
          { name: 'C Major', pitchClasses: 'C,E,G' },
          { name: 'G Major', pitchClasses: 'G,B,D' },
        ]
      }
    ];
  }

  /**
   * Get voicings for a specific chord
   */
  async getVoicingsForChord(
    chordName: string,
    options?: {
      maxDifficulty?: string;
      minFret?: number;
      maxFret?: number;
      cagedShape?: string;
      limit?: number;
    }
  ): Promise<VoicingWithAnalysis[]> {
    const params = new URLSearchParams();
    if (options?.maxDifficulty) params.append('maxDifficulty', options.maxDifficulty);
    if (options?.minFret !== undefined) params.append('minFret', options.minFret.toString());
    if (options?.maxFret !== undefined) params.append('maxFret', options.maxFret.toString());
    if (options?.cagedShape) params.append('cagedShape', options.cagedShape);
    if (options?.limit) params.append('limit', options.limit.toString());

    const queryString = params.toString();
    const url = `${this.baseUrl}/api/contextual-chords/voicings/${encodeURIComponent(chordName)}${queryString ? `?${queryString}` : ''}`;

    try {
      const response = await fetch(url, {
        headers: { 'Content-Type': 'application/json' },
      });

      if (!response.ok) {
        throw new Error(`HTTP error! status: ${response.status}`);
      }

      const apiResponse: ApiResponse<VoicingWithAnalysis[]> = await response.json();
      return apiResponse.data || [];
    } catch (error) {
      // Silently fail - API is optional, return empty array for demo mode
      if (import.meta.env.DEV) {
        console.debug('Voicings API not available (using demo mode)');
      }
      return [];
    }
  }

  /**
   * Get chords for a specific key
   */
  async getChordsForKey(
    keyName: string,
    options?: {
      extension?: string;
      stackingType?: string;
      onlyNaturallyOccurring?: boolean;
      includeSecondaryDominants?: boolean;
      includeBorrowedChords?: boolean;
      limit?: number;
    }
  ): Promise<ChordInContext[]> {
    const params = new URLSearchParams();
    if (options?.extension) params.append('extension', options.extension);
    if (options?.stackingType) params.append('stackingType', options.stackingType);
    if (options?.onlyNaturallyOccurring !== undefined) params.append('onlyNaturallyOccurring', options.onlyNaturallyOccurring.toString());
    if (options?.includeSecondaryDominants !== undefined) params.append('includeSecondaryDominants', options.includeSecondaryDominants.toString());
    if (options?.includeBorrowedChords !== undefined) params.append('includeBorrowedChords', options.includeBorrowedChords.toString());
    if (options?.limit) params.append('limit', options.limit.toString());

    const queryString = params.toString();
    const url = `${this.baseUrl}/api/contextual-chords/keys/${encodeURIComponent(keyName)}${queryString ? `?${queryString}` : ''}`;

    try {
      const response = await fetch(url, {
        headers: { 'Content-Type': 'application/json' },
      });

      if (!response.ok) {
        throw new Error(`HTTP error! status: ${response.status}`);
      }

      const apiResponse: ApiResponse<ChordInContext[]> = await response.json();
      return apiResponse.data || [];
    } catch (error) {
      // Silently fail - API is optional, return empty array for demo mode
      if (import.meta.env.DEV) {
        console.debug('Chords for key API not available (using demo mode)');
      }
      return [];
    }
  }

  /**
   * Get chords for a specific scale
   */
  async getChordsForScale(
    scaleName: string,
    options?: {
      extension?: string;
      stackingType?: string;
      limit?: number;
    }
  ): Promise<ChordInContext[]> {
    const params = new URLSearchParams();
    if (options?.extension) params.append('extension', options.extension);
    if (options?.stackingType) params.append('stackingType', options.stackingType);
    if (options?.limit) params.append('limit', options.limit.toString());

    const queryString = params.toString();
    const url = `${this.baseUrl}/api/contextual-chords/scales/${encodeURIComponent(scaleName)}${queryString ? `?${queryString}` : ''}`;

    try {
      const response = await fetch(url, {
        headers: { 'Content-Type': 'application/json' },
      });

      if (!response.ok) {
        throw new Error(`HTTP error! status: ${response.status}`);
      }

      const apiResponse: ApiResponse<ChordInContext[]> = await response.json();
      return apiResponse.data || [];
    } catch (error) {
      // Silently fail - API is optional, return empty array for demo mode
      if (import.meta.env.DEV) {
        console.debug('Chords for scale API not available (using demo mode)');
      }
      return [];
    }
  }

  /**
   * Get chords for a specific mode
   */
  async getChordsForMode(
    modeName: string,
    options?: {
      extension?: string;
      stackingType?: string;
      limit?: number;
    }
  ): Promise<ChordInContext[]> {
    const params = new URLSearchParams();
    if (options?.extension) params.append('extension', options.extension);
    if (options?.stackingType) params.append('stackingType', options.stackingType);
    if (options?.limit) params.append('limit', options.limit.toString());

    const queryString = params.toString();
    const url = `${this.baseUrl}/api/contextual-chords/modes/${encodeURIComponent(modeName)}${queryString ? `?${queryString}` : ''}`;

    try {
      const response = await fetch(url, {
        headers: { 'Content-Type': 'application/json' },
      });

      if (!response.ok) {
        throw new Error(`HTTP error! status: ${response.status}`);
      }

      const apiResponse: ApiResponse<ChordInContext[]> = await response.json();
      return apiResponse.data || [];
    } catch (error) {
      // Silently fail - API is optional, return empty array for demo mode
      if (import.meta.env.DEV) {
        console.debug('Chords for mode API not available (using demo mode)');
      }
      return [];
    }
  }
}

// Export singleton instance
export const BSPApiService = new BSPApiServiceClass();
