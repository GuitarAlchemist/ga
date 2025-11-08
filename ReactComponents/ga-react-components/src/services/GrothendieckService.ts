/**
 * GrothendieckService
 * 
 * Frontend service for interacting with the Grothendieck Monoid API.
 * Provides methods for ICV computation, delta operations, shape discovery,
 * and fretboard heat map generation.
 */

export interface IntervalClassVectorResult {
  intervals: number[];
  pitchClassSet: number[];
}

export interface GrothendieckDelta {
  sourceIcv: number[];
  targetIcv: number[];
  delta: number[];
  l1Norm: number;
  harmonicCost: number;
}

export interface FretboardShape {
  pitchClassSet: number[];
  icv: number[];
  frets: number[];
  diagness: number;
  ergonomics: number;
  totalCost: number;
}

export interface ShapeTransition {
  from: FretboardShape;
  to: FretboardShape;
  delta: number[];
  cost: number;
}

export interface HeatMapEntry {
  pitchClassSet: number[];
  probability: number;
  cumulativeProbability: number;
}

export interface MarkovWalkResult {
  path: FretboardShape[];
  heatMap: HeatMapEntry[];
  totalSteps: number;
  uniqueShapes: number;
}

export interface GrothendieckServiceConfig {
  baseUrl: string;
  timeout?: number; // in ms
}

/**
 * GrothendieckService class for interacting with Grothendieck Monoid API
 */
export class GrothendieckService {
  private config: Required<GrothendieckServiceConfig>;

  constructor(config: GrothendieckServiceConfig) {
    this.config = {
      baseUrl: config.baseUrl,
      timeout: config.timeout ?? 30000
    };
  }

  /**
   * Compute Interval Class Vector for a pitch class set
   */
  async computeIcv(pitchClassSet: number[]): Promise<IntervalClassVectorResult> {
    const response = await this.fetchWithTimeout(
      `${this.config.baseUrl}/api/grothendieck/icv`,
      {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ pitchClassSet })
      }
    );

    if (!response.ok) {
      throw new Error(`Failed to compute ICV: ${response.statusText}`);
    }

    return await response.json() as IntervalClassVectorResult;
  }

  /**
   * Compute delta between two pitch class sets
   */
  async computeDelta(source: number[], target: number[]): Promise<GrothendieckDelta> {
    const response = await this.fetchWithTimeout(
      `${this.config.baseUrl}/api/grothendieck/delta`,
      {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ source, target })
      }
    );

    if (!response.ok) {
      throw new Error(`Failed to compute delta: ${response.statusText}`);
    }

    return await response.json() as GrothendieckDelta;
  }

  /**
   * Get all fretboard shapes for a tuning
   */
  async getShapes(tuning: number[], maxFret: number = 12): Promise<FretboardShape[]> {
    const queryParams = new URLSearchParams({
      tuning: tuning.join(','),
      maxFret: maxFret.toString()
    });

    const response = await this.fetchWithTimeout(
      `${this.config.baseUrl}/api/grothendieck/shapes?${queryParams.toString()}`
    );

    if (!response.ok) {
      throw new Error(`Failed to get shapes: ${response.statusText}`);
    }

    return await response.json() as FretboardShape[];
  }

  /**
   * Get nearby shapes (neighbors in the shape graph)
   */
  async getNearbyShapes(
    pitchClassSet: number[],
    tuning: number[],
    maxFret: number = 12,
    maxCost: number = 5.0
  ): Promise<ShapeTransition[]> {
    const queryParams = new URLSearchParams({
      pitchClassSet: pitchClassSet.join(','),
      tuning: tuning.join(','),
      maxFret: maxFret.toString(),
      maxCost: maxCost.toString()
    });

    const response = await this.fetchWithTimeout(
      `${this.config.baseUrl}/api/grothendieck/nearby-shapes?${queryParams.toString()}`
    );

    if (!response.ok) {
      throw new Error(`Failed to get nearby shapes: ${response.statusText}`);
    }

    return await response.json() as ShapeTransition[];
  }

  /**
   * Generate fretboard heat map using Markov walker
   */
  async generateHeatMap(
    startPitchClassSet: number[],
    tuning: number[],
    steps: number = 1000,
    temperature: number = 1.0,
    maxFret: number = 12
  ): Promise<HeatMapEntry[]> {
    const queryParams = new URLSearchParams({
      startPitchClassSet: startPitchClassSet.join(','),
      tuning: tuning.join(','),
      steps: steps.toString(),
      temperature: temperature.toString(),
      maxFret: maxFret.toString()
    });

    const response = await this.fetchWithTimeout(
      `${this.config.baseUrl}/api/grothendieck/heat-map?${queryParams.toString()}`
    );

    if (!response.ok) {
      throw new Error(`Failed to generate heat map: ${response.statusText}`);
    }

    return await response.json() as HeatMapEntry[];
  }

  /**
   * Generate practice path (filtered Markov walk)
   */
  async generatePracticePath(
    startPitchClassSet: number[],
    tuning: number[],
    steps: number = 100,
    temperature: number = 1.0,
    maxFret: number = 12,
    minDiagness: number = 0.0,
    maxErgonomics: number = 10.0
  ): Promise<MarkovWalkResult> {
    const queryParams = new URLSearchParams({
      startPitchClassSet: startPitchClassSet.join(','),
      tuning: tuning.join(','),
      steps: steps.toString(),
      temperature: temperature.toString(),
      maxFret: maxFret.toString(),
      minDiagness: minDiagness.toString(),
      maxErgonomics: maxErgonomics.toString()
    });

    const response = await this.fetchWithTimeout(
      `${this.config.baseUrl}/api/grothendieck/practice-path?${queryParams.toString()}`
    );

    if (!response.ok) {
      throw new Error(`Failed to generate practice path: ${response.statusText}`);
    }

    return await response.json() as MarkovWalkResult;
  }

  /**
   * Find shortest path between two shapes
   */
  async findShortestPath(
    source: number[],
    target: number[],
    tuning: number[],
    maxFret: number = 12
  ): Promise<FretboardShape[]> {
    const queryParams = new URLSearchParams({
      source: source.join(','),
      target: target.join(','),
      tuning: tuning.join(','),
      maxFret: maxFret.toString()
    });

    const response = await this.fetchWithTimeout(
      `${this.config.baseUrl}/api/grothendieck/shortest-path?${queryParams.toString()}`
    );

    if (!response.ok) {
      throw new Error(`Failed to find shortest path: ${response.statusText}`);
    }

    return await response.json() as FretboardShape[];
  }

  /**
   * Stream heat map generation (NDJSON)
   */
  async *streamHeatMap(
    startPitchClassSet: number[],
    tuning: number[],
    steps: number = 1000,
    temperature: number = 1.0,
    maxFret: number = 12
  ): AsyncGenerator<HeatMapEntry, void, unknown> {
    const queryParams = new URLSearchParams({
      startPitchClassSet: startPitchClassSet.join(','),
      tuning: tuning.join(','),
      steps: steps.toString(),
      temperature: temperature.toString(),
      maxFret: maxFret.toString()
    });

    const response = await this.fetchWithTimeout(
      `${this.config.baseUrl}/api/grothendieck/heat-map/stream?${queryParams.toString()}`
    );

    if (!response.ok) {
      throw new Error(`Failed to stream heat map: ${response.statusText}`);
    }

    const reader = response.body?.getReader();
    if (!reader) {
      throw new Error('Response body is not readable');
    }

    const decoder = new TextDecoder();
    let buffer = '';

    try {
      while (true) {
        const { done, value } = await reader.read();
        if (done) break;

        buffer += decoder.decode(value, { stream: true });
        const lines = buffer.split('\n');
        buffer = lines.pop() || '';

        for (const line of lines) {
          if (line.trim()) {
            yield JSON.parse(line) as HeatMapEntry;
          }
        }
      }
    } finally {
      reader.releaseLock();
    }
  }

  // Private methods

  private async fetchWithTimeout(url: string, options?: RequestInit): Promise<Response> {
    const controller = new AbortController();
    const timeoutId = setTimeout(() => controller.abort(), this.config.timeout);

    try {
      const response = await fetch(url, {
        ...options,
        signal: controller.signal
      });
      return response;
    } catch (error) {
      if (error instanceof Error && error.name === 'AbortError') {
        throw new Error(`Request timeout after ${this.config.timeout}ms`);
      }
      throw error;
    } finally {
      clearTimeout(timeoutId);
    }
  }
}

/**
 * Create a singleton instance of GrothendieckService
 */
let grothendieckServiceInstance: GrothendieckService | null = null;

export function createGrothendieckService(config: GrothendieckServiceConfig): GrothendieckService {
  grothendieckServiceInstance = new GrothendieckService(config);
  return grothendieckServiceInstance;
}

export function getGrothendieckService(): GrothendieckService {
  if (!grothendieckServiceInstance) {
    throw new Error('GrothendieckService not initialized. Call createGrothendieckService() first.');
  }
  return grothendieckServiceInstance;
}
