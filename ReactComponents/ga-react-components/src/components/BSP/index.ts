export { BSPInterface } from './BSPInterface';
export { BSPApiService } from './BSPApiService';
export { BSPMetricsDashboard } from './BSPMetricsDashboard';
export { BSPSpatialVisualization } from './BSPSpatialVisualization';
export { BSPExportShare } from './BSPExportShare';
export { BSPTutorial } from './BSPTutorial';
export { BSPTreeVisualization } from './BSPTreeVisualization';
export { ThreeHarmonicNavigator } from './ThreeHarmonicNavigator';
export { HarmonicNavigator3D } from './HarmonicNavigator3D';
// BSPDoomExplorer was a 6,250-line monolith that depended on the API being
// reachable; the live `/test/bsp-doom-explorer` page was blank when it
// wasn't. v2 is a from-scratch react-three-fiber + drei rewrite split
// across ~10 focused files with a procedural fallback tree.
//
// The v1 component is still importable as `BSPDoomExplorerLegacy` for
// rollback, but the default export here points at v2 so all consumers
// (test page, demos table, etc.) pick it up automatically.
export { BSPDoomExplorerV2 as BSPDoomExplorer } from './v2';
export { BSPDoomExplorer as BSPDoomExplorerLegacy } from './BSPDoomExplorer';
export type { BSPDoomExplorerV2Props } from './v2';
export { AnkhReticle3D } from './AnkhReticle3D';
export { Sunburst3D } from './Sunburst3D';
export { Sunburst3DDemo } from './Sunburst3DDemo';
export { ImmersiveMusicalWorld } from './ImmersiveMusicalWorld';
export { ImmersiveMusicalWorldDemo } from './ImmersiveMusicalWorldDemo';
export { default as BSPElementDetailPanel } from './BSPElementDetailPanel';
export type { BSPElementData } from './BSPElementDetailPanel';
export { LODManager } from './LODManager';
export type { LODLevel, LODObject, LODManagerOptions, PerformanceStats } from './LODManager';
export { PerformanceMonitor } from './PerformanceMonitor';
export type { PerformanceMonitorProps } from './PerformanceMonitor';
export type { SunburstNode, Sunburst3DProps } from './Sunburst3D';
export type { MusicalNode, ImmersiveMusicalWorldProps } from './ImmersiveMusicalWorld';
export type {
  BSPRegion,
  BSPElement,
  BSPAnalysis,
  BSPSpatialQueryResponse,
  BSPTonalContextResponse,
  BSPChordRequest,
  BSPProgressionRequest,
  BSPChordAnalysis,
  BSPTransition,
  BSPOverallAnalysis,
  BSPProgressionAnalysisResponse,
  BSPTreeInfoResponse,
  ApiResponse
} from './BSPApiService';
export type {
  HarmonicRegion,
  PluckerLine,
  HarmonicRotor,
  BSPNode,
  MusicalPredicate,
  Tuning,
  DataUrls
} from './HarmonicNavigator3D';
