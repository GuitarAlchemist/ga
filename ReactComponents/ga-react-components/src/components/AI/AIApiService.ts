/**
 * AI API Service
 * 
 * Service for communicating with the Intelligent BSP and Adaptive AI backend APIs
 */

import axios, { AxiosInstance } from 'axios';

// ==================
// Types
// ==================

export interface ApiResponse<T> {
  success: boolean;
  data?: T;
  error?: string;
  details?: string;
}

export interface IntelligentBSPLevel {
  floors: BSPFloor[];
  landmarks: BSPLandmark[];
  portals: BSPPortal[];
  safeZones: BSPSafeZone[];
  challengePaths: BSPChallengePath[];
  learningPath: string[];
  difficulty: number;
  metadata: Record<string, any>;
}

export interface BSPFloor {
  floorId: number;
  name: string;
  shapeIds: string[];
  color: string;
}

export interface BSPLandmark {
  shapeId: string;
  name: string;
  importance: number;
  type: string;
}

export interface BSPPortal {
  shapeId: string;
  name: string;
  strength: number;
  type: string;
}

export interface BSPSafeZone {
  shapeId: string;
  name: string;
  stability: number;
  type: string;
}

export interface BSPChallengePath {
  name: string;
  shapeIds: string[];
  period: number;
  difficulty: number;
}

export interface PlayerStats {
  totalAttempts: number;
  successRate: number;
  averageTime: number;
  currentDifficulty: number;
  learningRate: number;
  currentAttractor: string | null;
}

export interface AdaptiveChallenge {
  shapeIds: string[];
  quality: number;
  entropy: number;
  complexity: number;
  predictability: number;
  diversity: number;
  strategy: string;
  currentDifficulty: number;
  learningRate: number;
}

export interface PlayerStyleProfile {
  preferredComplexity: number;
  explorationRate: number;
  topChordFamilies: Record<string, number>;
  favoriteProgressionCount: number;
  totalProgressionsAnalyzed: number;
}

export interface RecognizedPattern {
  pattern: string;
  frequency: number;
  probability: number;
}

export interface ShapePrediction {
  shapeId: string;
  probability: number;
  confidence: number;
}

// ==================
// API Service
// ==================

export class AIApiService {
  private client: AxiosInstance;

  constructor(baseURL: string = 'https://localhost:7001') {
    this.client = axios.create({
      baseURL,
      headers: {
        'Content-Type': 'application/json',
      },
    });
  }

  // ==================
  // Intelligent BSP API
  // ==================

  async generateLevel(
    pitchClassSets: string[],
    tuning?: string,
    options?: {
      chordFamilyCount?: number;
      landmarkCount?: number;
      bridgeChordCount?: number;
      learningPathLength?: number;
    }
  ): Promise<IntelligentBSPLevel> {
    const response = await this.client.post<ApiResponse<IntelligentBSPLevel>>(
      '/api/intelligent-bsp/generate-level',
      {
        pitchClassSets,
        tuning,
        ...options,
      }
    );

    if (!response.data.success || !response.data.data) {
      throw new Error(response.data.error || 'Failed to generate level');
    }

    return response.data.data;
  }

  async getLevelStats(
    pitchClassSets: string[],
    tuning?: string,
    options?: {
      chordFamilyCount?: number;
      landmarkCount?: number;
      bridgeChordCount?: number;
      learningPathLength?: number;
    }
  ): Promise<any> {
    const response = await this.client.post<ApiResponse<any>>(
      '/api/intelligent-bsp/level-stats',
      {
        pitchClassSets,
        tuning,
        ...options,
      }
    );

    if (!response.data.success || !response.data.data) {
      throw new Error(response.data.error || 'Failed to get level stats');
    }

    return response.data.data;
  }

  // ==================
  // Adaptive AI API
  // ==================

  async recordPerformance(
    playerId: string,
    success: boolean,
    timeMs: number,
    attempts: number,
    shapeId: string
  ): Promise<PlayerStats> {
    const response = await this.client.post<ApiResponse<PlayerStats>>(
      '/api/adaptive-ai/record-performance',
      {
        playerId,
        success,
        timeMs,
        attempts,
        shapeId,
      }
    );

    if (!response.data.success || !response.data.data) {
      throw new Error(response.data.error || 'Failed to record performance');
    }

    return response.data.data;
  }

  async generateChallenge(
    playerId: string,
    pitchClassSets: string[],
    recentProgression: string[],
    tuning?: string
  ): Promise<AdaptiveChallenge> {
    const response = await this.client.post<ApiResponse<AdaptiveChallenge>>(
      '/api/adaptive-ai/generate-challenge',
      {
        playerId,
        pitchClassSets,
        recentProgression,
        tuning,
      }
    );

    if (!response.data.success || !response.data.data) {
      throw new Error(response.data.error || 'Failed to generate challenge');
    }

    return response.data.data;
  }

  async suggestShapes(
    playerId: string,
    pitchClassSets: string[],
    currentProgression: string[],
    tuning?: string,
    topK?: number
  ): Promise<any[]> {
    const response = await this.client.post<ApiResponse<any[]>>(
      '/api/adaptive-ai/suggest-shapes',
      {
        playerId,
        pitchClassSets,
        currentProgression,
        tuning,
        topK,
      }
    );

    if (!response.data.success || !response.data.data) {
      throw new Error(response.data.error || 'Failed to suggest shapes');
    }

    return response.data.data;
  }

  async getPlayerStats(playerId: string): Promise<PlayerStats> {
    const response = await this.client.get<ApiResponse<PlayerStats>>(
      `/api/adaptive-ai/player-stats/${playerId}`
    );

    if (!response.data.success || !response.data.data) {
      throw new Error(response.data.error || 'Failed to get player stats');
    }

    return response.data.data;
  }

  async resetSession(playerId: string): Promise<void> {
    const response = await this.client.post<ApiResponse<any>>(
      `/api/adaptive-ai/reset-session/${playerId}`
    );

    if (!response.data.success) {
      throw new Error(response.data.error || 'Failed to reset session');
    }
  }

  // ==================
  // Advanced AI API
  // ==================

  async learnStyle(
    playerId: string,
    pitchClassSets: string[],
    progression: string[],
    tuning?: string
  ): Promise<PlayerStyleProfile> {
    const response = await this.client.post<ApiResponse<PlayerStyleProfile>>(
      '/api/advanced-ai/learn-style',
      {
        playerId,
        pitchClassSets,
        progression,
        tuning,
      }
    );

    if (!response.data.success || !response.data.data) {
      throw new Error(response.data.error || 'Failed to learn style');
    }

    return response.data.data;
  }

  async generateStyleMatched(
    playerId: string,
    pitchClassSets: string[],
    tuning?: string,
    targetLength?: number
  ): Promise<{ shapeIds: string[]; matchScore: number }> {
    const response = await this.client.post<ApiResponse<any>>(
      '/api/advanced-ai/generate-style-matched',
      {
        playerId,
        pitchClassSets,
        tuning,
        targetLength,
      }
    );

    if (!response.data.success || !response.data.data) {
      throw new Error(response.data.error || 'Failed to generate style-matched progression');
    }

    return response.data.data;
  }

  async getStyleProfile(playerId: string): Promise<PlayerStyleProfile> {
    const response = await this.client.get<ApiResponse<PlayerStyleProfile>>(
      `/api/advanced-ai/style-profile/${playerId}`
    );

    if (!response.data.success || !response.data.data) {
      throw new Error(response.data.error || 'Failed to get style profile');
    }

    return response.data.data;
  }

  async getPatterns(playerId: string, topK: number = 10): Promise<RecognizedPattern[]> {
    const response = await this.client.get<ApiResponse<RecognizedPattern[]>>(
      `/api/advanced-ai/patterns/${playerId}?topK=${topK}`
    );

    if (!response.data.success || !response.data.data) {
      throw new Error(response.data.error || 'Failed to get patterns');
    }

    return response.data.data;
  }

  async predictNext(
    playerId: string,
    currentShape: string,
    topK: number = 5
  ): Promise<ShapePrediction[]> {
    const response = await this.client.post<ApiResponse<ShapePrediction[]>>(
      '/api/advanced-ai/predict-next',
      {
        playerId,
        currentShape,
        topK,
      }
    );

    if (!response.data.success || !response.data.data) {
      throw new Error(response.data.error || 'Failed to predict next shapes');
    }

    return response.data.data;
  }

  async getTransitionMatrix(playerId: string): Promise<Record<string, Record<string, number>>> {
    const response = await this.client.get<ApiResponse<Record<string, Record<string, number>>>>(
      `/api/advanced-ai/transition-matrix/${playerId}`
    );

    if (!response.data.success || !response.data.data) {
      throw new Error(response.data.error || 'Failed to get transition matrix');
    }

    return response.data.data;
  }

  async recommendProgressions(
    playerId: string,
    pitchClassSets: string[],
    tuning?: string,
    topK: number = 5
  ): Promise<string[][]> {
    const response = await this.client.post<ApiResponse<string[][]>>(
      '/api/advanced-ai/recommend-progressions',
      {
        playerId,
        pitchClassSets,
        tuning,
        topK,
      }
    );

    if (!response.data.success || !response.data.data) {
      throw new Error(response.data.error || 'Failed to recommend progressions');
    }

    return response.data.data;
  }
}

// Export singleton instance
export const aiApiService = new AIApiService();

