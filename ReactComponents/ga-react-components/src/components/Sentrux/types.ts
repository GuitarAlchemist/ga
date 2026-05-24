// Types for the Sentrux tab. The shapes mirror the MCP tool response
// envelopes that the vite middleware unpacks (it strips the JSON-RPC
// frame and returns the inner tool payload directly).
//
// Sentrux runs as an MCP stdio server (see .mcp.json). Each /dev-data/sentrux/*
// endpoint spawns `sentrux.exe mcp`, issues an initialize → scan → <tool> chain,
// and returns the inner tool result as JSON. If sentrux.exe is missing or
// errors, the endpoint returns `{ ok: false, error, hint }` and the cards
// render a graceful empty state.

export interface SentruxEnvelope<T> {
  ok: boolean;
  generated_at: string;
  data?: T;
  error?: string;
  hint?: string;
  duration_ms?: number;
}

export interface SentruxHealth {
  quality_signal: number;
  bottleneck?: string;
  cross_module_edges?: number;
  total_import_edges?: number;
  root_causes?: Record<string, { raw: number; score: number }>;
  scanned?: string;
  files?: number;
  lines?: number;
  version?: string;
  // Optional fields the middleware may attach
  last_scan_at?: string;
}

export interface SentruxRuleViolation {
  severity?: string;
  file?: string;
  rule?: string;
  message?: string;
  // Sentrux's exact rule-violation schema may vary by version; keep a
  // permissive extra-fields bag for forward compat.
  [k: string]: unknown;
}

export interface SentruxRulesPayload {
  passed?: boolean;
  rule_count?: number;
  violations?: SentruxRuleViolation[];
  // Raw text fallback when sentrux returns a human-readable summary
  // instead of a structured list.
  text?: string;
}

export interface SentruxTestGap {
  file: string;
  complexity?: number;
  imports?: number;
  risk_score?: number;
  // Permissive bag for fields like loc, last_modified, etc.
  [k: string]: unknown;
}

// Sentrux's free tier returns aggregate coverage stats; Pro tier returns
// a per-file `files[]` array of riskiest untested sources. We support both.
export interface SentruxTestGapsPayload {
  files?: SentruxTestGap[];
  total_untested?: number;
  // Aggregate-only shape (free tier)
  source_files?: number;
  test_files?: number;
  tested?: number;
  untested?: number;
  coverage_ratio?: number;
  coverage_score?: number;
  text?: string;
}

export interface SentruxDsmCycle {
  files?: string[];
  size?: number;
  text?: string;
  [k: string]: unknown;
}

export interface SentruxDsmHotspot {
  file: string;
  fan_in?: number;
  fan_out?: number;
  [k: string]: unknown;
}

export interface SentruxDsmCluster {
  files_count?: number;
  internal_edges?: number;
  level?: number;
  [k: string]: unknown;
}

export interface SentruxDsmPayload {
  // Pro-tier optional cycle/hotspot detail
  cycles?: SentruxDsmCycle[];
  hotspots?: SentruxDsmHotspot[];
  matrix_size?: number;
  // Free-tier aggregate shape
  size?: number;
  edge_count?: number;
  density?: number;
  clusters?: SentruxDsmCluster[];
  level_breaks?: number;
  above_diagonal?: number;
  below_diagonal?: number;
  same_level?: number;
  propagation_cost?: number;
  interpretation?: string;
  text?: string;
}
