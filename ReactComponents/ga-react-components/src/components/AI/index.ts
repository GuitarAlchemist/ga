/**
 * AI Components Index
 * 
 * Exports all AI-related components and services
 */

export { IntelligentBSPVisualizer } from './IntelligentBSPVisualizer';
export type {
  IntelligentBSPLevel,
  BSPFloor,
  BSPLandmark,
  BSPPortal,
  BSPSafeZone,
  BSPChallengePath,
  IntelligentBSPVisualizerProps,
} from './IntelligentBSPVisualizer';

export { AdaptiveAIDashboard } from './AdaptiveAIDashboard';
export type {
  PlayerStats,
  PlayerStyleProfile,
  RecognizedPattern,
  AdaptiveAIDashboardProps,
  PerformanceHistoryPoint,
} from './AdaptiveAIDashboard';

export { AIApiService, aiApiService } from './AIApiService';
export type {
  ApiResponse,
  AdaptiveChallenge,
  ShapePrediction,
} from './AIApiService';

