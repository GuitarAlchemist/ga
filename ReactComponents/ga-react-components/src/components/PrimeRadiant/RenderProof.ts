// src/components/PrimeRadiant/RenderProof.ts
// Godel Dual-Buffer Render Verification — Phase A
// Each IXQL panel publishes a structured proof of what it rendered.
// Enables semantic verification, cognitive checksums, and divergence detection.
// See: Demerzel deep dive — Godel Dual-Buffer Render Verification

import { signalBus } from './DashboardSignalBus';

// ---------------------------------------------------------------------------
// Types
// ---------------------------------------------------------------------------

export type CaptureQuality = 'clean' | 'unstable' | 'partial';

/** Common base for all render proofs */
interface RenderProofBase {
  panelId: string;
  panelType: 'grid' | 'viz' | 'form';
  timestamp: number;
  captureQuality: CaptureQuality;
  divergences: string[];
}

/** AG-Grid render proof */
export interface GridRenderProof extends RenderProofBase {
  panelType: 'grid';
  model: {
    totalRowCount: number;
    displayedRowCount: number;
    columnCount: number;
    columnNames: string[];
    paginationActive: boolean;
    currentPage: number;
  };
  cellTypes: {
    hexavalentCells: number;
    confidenceCells: number;
    nullCells: number;
    hexavalentDistribution: Record<string, number>;
  };
  pipeline: {
    stepsApplied: string[];
    preFilterCount: number;
    postFilterCount: number;
  };
}

/** D3 visualization render proof */
export interface VizRenderProof extends RenderProofBase {
  panelType: 'viz';
  vizKind: 'force-graph' | 'bar' | 'sparkline' | 'timeline';
  model: {
    dataPointCount: number;
    nodeCount: number;
    linkCount: number;
  };
  scales: {
    colorMappingCoverage: number;
    uniqueLabels: number;
  };
  simulation: {
    alpha: number;
    clippedNodes: number;
  } | null;
}

/** Form render proof */
export interface FormRenderProof extends RenderProofBase {
  panelType: 'form';
  spec: {
    fieldCount: number;
    fieldNames: string[];
    fieldTypes: string[];
    hexavalentMode: boolean;
    hasSubmitCommand: boolean;
  };
  values: {
    hexavalentDistribution: Record<string, number>;
    validationErrors: string[];
  };
}

export type RenderProof = GridRenderProof | VizRenderProof | FormRenderProof;

// ---------------------------------------------------------------------------
// Divergence severity
// ---------------------------------------------------------------------------

export type DivergenceSeverity = 'info' | 'warning' | 'critical';

export function classifyDivergences(divergences: string[]): DivergenceSeverity {
  if (divergences.length === 0) return 'info';
  if (divergences.length >= 5) return 'critical';
  // Data-loss patterns
  for (const d of divergences) {
    if (d.indexOf('missing') >= 0 || d.indexOf('zero ') >= 0 || d.indexOf('no ') >= 0) {
      return 'critical';
    }
  }
  return divergences.length >= 3 ? 'warning' : 'info';
}

// ---------------------------------------------------------------------------
// Cognitive Checksum
// ---------------------------------------------------------------------------

/**
 * Compute a lightweight cognitive checksum from panel data.
 * Uses FNV-1a 32-bit hash — fast, no crypto needed, just change detection.
 * The checksum changes when the query spec, data, or render state changes.
 */
export function cognitiveChecksum(specJson: string, dataFingerprint: string, sceneHash: string): string {
  const combined = specJson + '|' + dataFingerprint + '|' + sceneHash;
  let hash = 0x811c9dc5; // FNV offset basis
  for (let i = 0; i < combined.length; i++) {
    hash ^= combined.charCodeAt(i);
    hash = (hash * 0x01000193) | 0; // FNV prime
  }
  // Convert to hex string
  return (hash >>> 0).toString(16).padStart(8, '0');
}

/**
 * Generate a stable data fingerprint from row data.
 * Sorts keys, concatenates values — deterministic regardless of object key order.
 */
export function dataFingerprint(rows: Record<string, unknown>[]): string {
  if (rows.length === 0) return 'empty';
  const parts: string[] = [];
  parts.push(String(rows.length));
  // Sample sqrt(N) evenly-spaced rows for adequate coverage
  // 100 rows → 10 samples, 1000 rows → ~32 samples, 10 rows → 4 samples
  const sampleCount = Math.min(rows.length, Math.max(3, Math.ceil(Math.sqrt(rows.length))));
  const step = rows.length / sampleCount;
  for (let s = 0; s < sampleCount; s++) {
    const idx = Math.min(Math.floor(s * step), rows.length - 1);
    const row = rows[idx];
    const keys = Object.keys(row).sort();
    for (const k of keys) {
      const v = row[k];
      parts.push(k + ':' + (v === null ? 'null' : v === undefined ? 'undef' : String(v)));
    }
  }
  return parts.join(';');
}

// ---------------------------------------------------------------------------
// Grid proof generation
// ---------------------------------------------------------------------------

const HEXAVALENT_VALUES = new Set(['T', 'P', 'U', 'D', 'F', 'C', 'TRUE', 'PROBABLE', 'UNKNOWN', 'DOUBTFUL', 'FALSE', 'CONTRADICTORY']);
const HEXAVALENT_NORMALIZE: Record<string, string> = {
  TRUE: 'T', PROBABLE: 'P', UNKNOWN: 'U', DOUBTFUL: 'D', FALSE: 'F', CONTRADICTORY: 'C',
  T: 'T', P: 'P', U: 'U', D: 'D', F: 'F', C: 'C',
};
const CONFIDENCE_KEYWORDS = ['confidence', 'score', 'resilience', 'staleness'];

export function generateGridProof(
  panelId: string,
  rowData: Record<string, unknown>[],
  columnNames: string[],
  pipelineSteps: string[],
  preFilterCount: number,
  paginationActive: boolean,
  currentPage: number,
): GridRenderProof {
  const divergences: string[] = [];

  // Count cell types
  let hexavalentCells = 0;
  let confidenceCells = 0;
  let nullCells = 0;
  const hexDist: Record<string, number> = { T: 0, P: 0, U: 0, D: 0, F: 0, C: 0 };

  for (const row of rowData) {
    for (const col of columnNames) {
      const val = row[col];
      if (val === null || val === undefined) {
        nullCells++;
        continue;
      }
      if (typeof val === 'string') {
        const upper = val.toUpperCase();
        if (HEXAVALENT_VALUES.has(upper)) {
          hexavalentCells++;
          const key = HEXAVALENT_NORMALIZE[upper];
          if (key && hexDist[key] !== undefined) hexDist[key]++;
        }
      }
      if (typeof val === 'number' && val >= 0 && val <= 1) {
        const lower = col.toLowerCase();
        if (CONFIDENCE_KEYWORDS.some(kw => lower.indexOf(kw) >= 0)) {
          confidenceCells++;
        }
      }
    }
  }

  // Divergence checks
  if (rowData.length === 0 && preFilterCount > 0) {
    divergences.push(`Pipeline reduced ${preFilterCount} rows to zero — all data filtered out`);
  }
  if (columnNames.length === 0 && rowData.length > 0) {
    divergences.push('Rows present but no columns defined');
  }

  return {
    panelId,
    panelType: 'grid',
    timestamp: Date.now(),
    captureQuality: 'clean',
    divergences,
    model: {
      totalRowCount: rowData.length,
      displayedRowCount: paginationActive ? Math.min(50, rowData.length) : rowData.length,
      columnCount: columnNames.length,
      columnNames,
      paginationActive,
      currentPage,
    },
    cellTypes: {
      hexavalentCells,
      confidenceCells,
      nullCells,
      hexavalentDistribution: hexDist,
    },
    pipeline: {
      stepsApplied: pipelineSteps,
      preFilterCount,
      postFilterCount: rowData.length,
    },
  };
}

// ---------------------------------------------------------------------------
// Viz proof generation
// ---------------------------------------------------------------------------

export function generateVizProof(
  panelId: string,
  vizKind: 'force-graph' | 'bar' | 'sparkline' | 'timeline',
  data: Record<string, unknown>[],
  nodeCount: number,
  linkCount: number,
  colorMappingCoverage: number,
  uniqueLabels: number,
  simulationAlpha: number | null,
  clippedNodes: number,
): VizRenderProof {
  const divergences: string[] = [];

  // Force-graph divergence checks
  if (vizKind === 'force-graph') {
    if (nodeCount === 0 && data.length > 0) {
      divergences.push(`Data has ${data.length} rows but zero nodes rendered`);
    }
    if (clippedNodes > 0) {
      divergences.push(`${clippedNodes} nodes clipped outside viewport`);
    }
  }

  // Bar chart: label vs data count
  if (vizKind === 'bar') {
    if (uniqueLabels < data.length) {
      divergences.push(`Bar chart has ${data.length} data points but only ${uniqueLabels} unique labels — ${data.length - uniqueLabels} bars may overlap`);
    }
  }

  // Color mapping coverage
  if (colorMappingCoverage < 1.0 && colorMappingCoverage > 0) {
    const pct = Math.round(colorMappingCoverage * 100);
    divergences.push(`Color mapping covers ${pct}% of data points — ${100 - pct}% use default color`);
  }

  return {
    panelId,
    panelType: 'viz',
    timestamp: Date.now(),
    captureQuality: simulationAlpha !== null && simulationAlpha > 0.01 ? 'unstable' : 'clean',
    divergences,
    vizKind,
    model: {
      dataPointCount: data.length,
      nodeCount,
      linkCount,
    },
    scales: {
      colorMappingCoverage,
      uniqueLabels,
    },
    simulation: simulationAlpha !== null ? {
      alpha: simulationAlpha,
      clippedNodes,
    } : null,
  };
}

// ---------------------------------------------------------------------------
// Form proof generation
// ---------------------------------------------------------------------------

export function generateFormProof(
  panelId: string,
  fieldNames: string[],
  fieldTypes: string[],
  hexavalentMode: boolean,
  hasSubmitCommand: boolean,
  currentValues: Record<string, unknown>,
): FormRenderProof {
  const divergences: string[] = [];

  // Count hexavalent distribution in current values
  const hexDist: Record<string, number> = { T: 0, P: 0, U: 0, D: 0, F: 0, C: 0 };
  for (const name of fieldNames) {
    const val = currentValues[name];
    if (typeof val === 'string' && HEXAVALENT_VALUES.has(val.toUpperCase())) {
      const key = val.toUpperCase().charAt(0);
      if (hexDist[key] !== undefined) hexDist[key]++;
    }
  }

  // Divergence checks
  if (hasSubmitCommand && fieldNames.length === 0) {
    divergences.push('Form has submit command but no fields defined');
  }

  return {
    panelId,
    panelType: 'form',
    timestamp: Date.now(),
    captureQuality: 'clean',
    divergences,
    spec: {
      fieldCount: fieldNames.length,
      fieldNames,
      fieldTypes,
      hexavalentMode,
      hasSubmitCommand,
    },
    values: {
      hexavalentDistribution: hexDist,
      validationErrors: [],
    },
  };
}

// ---------------------------------------------------------------------------
// Publish render proof as a signal bus event
// ---------------------------------------------------------------------------

export function publishRenderProof(proof: RenderProof): void {
  signalBus.publish(
    '__renderProof__' + proof.panelId,
    proof,
    proof.panelId,
  );

  // If divergences exist, publish a divergence signal
  if (proof.divergences.length > 0) {
    signalBus.publish(
      '__renderDivergence__',
      {
        panelId: proof.panelId,
        panelType: proof.panelType,
        divergences: proof.divergences,
        severity: classifyDivergences(proof.divergences),
        timestamp: proof.timestamp,
      },
      '__renderVerifier__',
    );
  }
}

// ---------------------------------------------------------------------------
// All public API is exported from function/type declarations above.
// ---------------------------------------------------------------------------
