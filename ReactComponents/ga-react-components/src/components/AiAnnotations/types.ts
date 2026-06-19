// Shape mirrors docs/contracts/ai-annotation.schema.json over in ix.
// We keep this duplication intentional — the dashboard is a downstream
// consumer; the contract owner is ix.

export type TruthValue = 'T' | 'P' | 'U' | 'D' | 'F' | 'C';

export type AnnotationKind =
  | 'invariant'
  | 'assumption'
  | 'hypothesis'
  | 'contract'
  | 'smell'
  | 'decision'
  | 'hint'
  // Schema v2 (2026-05-24) — additive kinds for the value × complexity heatmap.
  | 'business-value'
  | 'hot-path';

export type Certainty =
  | 'test'
  | 'formal-proof'
  | 'manually-reviewed'
  | 'assumed'
  | 'uncertain'
  | 'inferred'
  | 'dismissed';

export interface Annotation {
  schema_version: number;
  id: string;
  kind: AnnotationKind;
  claim: string;
  truth_value: TruthValue;
  certainty: Certainty;
  confidence: number;
  source: {
    author: string;
    model?: string;
    evidence?: string;
  };
  location: {
    path: string;
    line_start: number;
    line_end: number;
  };
  created_at: string;
  updated_at: string;
  stale?: boolean;
  reconciliation?: {
    test_match?: string | null;
    promoted_to_c_from?: TruthValue[];
    weighted_truth_value?: TruthValue;
    weighted_confidence?: number;
  };
}

export interface AiAnnotationsPayload {
  generated_at: string;
  total: number;
  by_truth_value: Record<string, number>;
  by_certainty: Record<string, number>;
  by_kind?: Record<string, number>;
  verified_by_test?: number;
  stale?: number;
  contradictory?: number;
  annotations: Annotation[];
  /** True when neither the extractor nor the reconciler have produced data yet. */
  empty?: boolean;
}
