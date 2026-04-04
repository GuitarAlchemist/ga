// src/components/PrimeRadiant/TarsIxBridge.ts
// Canonical payload types + adapter boundary for the future TARS↔ix MCP bridge.
//
// Intent (from multi-AI brainstorm consensus 2026-03-31): don't build the
// MCP transport yet — that's premature. Instead lock the SHAPES of the
// payloads now, define one adapter boundary (`TarsIxAdapter`), and let
// callers use a no-op adapter until the real MCP client ships.
//
// When the MCP transport lands, only the adapter implementation changes.
// Every consumer (viz panels, epistemic dashboards, proof inspectors) keeps
// the same type contracts.

// ---------------------------------------------------------------------------
// 1. Query result — generic response to an IXQL SELECT/SHOW/TRACE query
// ---------------------------------------------------------------------------

/** Hexavalent logic value — T/P/U/D/F/C. */
export type HexavalentTruth = 'T' | 'P' | 'U' | 'D' | 'F' | 'C';

export interface QueryResult {
  /** Stable id of the query that produced these rows. */
  queryId: string;
  /** Cross-system correlation id (optional). */
  traceId?: string;
  /** Execution start / end in epoch ms. */
  startedAt: number;
  completedAt: number;
  /** Source system: tars | ix | ga | demerzel. */
  origin: 'tars' | 'ix' | 'ga' | 'demerzel';
  /** Row payloads. Shape depends on the query — callers narrow as needed. */
  rows: Record<string, unknown>[];
  /** Column metadata for table display. Optional. */
  columns?: { name: string; type: 'string' | 'number' | 'boolean' | 'truth' | 'date' }[];
  /** Count of rows before any client-side filter. */
  totalCount?: number;
  /** Errors or warnings surfaced by the source system. */
  diagnostics?: { level: 'info' | 'warn' | 'error'; message: string }[];
}

// ---------------------------------------------------------------------------
// 2. Violation event — something breached a constitutional/policy boundary
// ---------------------------------------------------------------------------

export interface ViolationEvent {
  /** Unique id assigned by the detector. */
  id: string;
  /** When the violation was detected. */
  detectedAt: number;
  /** Which rule / article / clause was breached. */
  ruleId: string;
  ruleKind: 'constitutional' | 'policy' | 'contract' | 'invariant' | 'test';
  /** Where in the system the violation occurred. */
  locus: { system: 'tars' | 'ix' | 'ga' | 'demerzel'; component?: string; artifactId?: string };
  /** Severity matches the project board's severity enum. */
  severity: 'constitutional' | 'policy' | 'operational' | 'enhancement';
  /** Human-readable message. */
  message: string;
  /** Machine-readable context — anything the rule check produced. */
  evidence?: Record<string, unknown>;
  /** Optional remediation hint. */
  suggestion?: string;
}

// ---------------------------------------------------------------------------
// 3. Proof snapshot — a frozen claim with supporting evidence
// ---------------------------------------------------------------------------

export interface ProofSnapshot {
  /** Unique proof id. */
  id: string;
  /** What's being asserted. */
  claim: string;
  /** Truth value from hexavalent logic. */
  truth: HexavalentTruth;
  /** Confidence 0.0–1.0. */
  confidence: number;
  /** When the proof was produced. */
  producedAt: number;
  /** Article / clause citations (governance anchors). */
  citations: { ruleId: string; ruleKind: ViolationEvent['ruleKind']; excerpt?: string }[];
  /** Evidence rows: observations, test runs, prior proofs, etc. */
  evidence: { kind: string; ref: string; payload?: Record<string, unknown> }[];
  /** Parent proofs this one composes (for compound arguments). */
  derivedFrom?: string[];
  /** Signature / hash for reproducibility. */
  digest?: string;
}

// ---------------------------------------------------------------------------
// 4. Epistemic node — a belief/claim in the epistemic graph
// ---------------------------------------------------------------------------

export interface EpistemicNode {
  id: string;
  /** Natural-language proposition being tracked. */
  proposition: string;
  /** Current hexavalent truth value. */
  truth: HexavalentTruth;
  /** Confidence 0.0–1.0. */
  confidence: number;
  /** Which repo / agent owns this belief. */
  owner: 'tars' | 'ix' | 'ga' | 'demerzel' | 'unknown';
  /** Governance anchor: article, clause, policy, test. */
  anchor?: { ruleId: string; ruleKind: ViolationEvent['ruleKind'] };
  /** When the belief was last updated. */
  updatedAt: number;
  /** Related beliefs (dependency / contradiction / reinforcement). */
  edges?: EpistemicEdge[];
  /** Staleness 0.0–1.0 — how decayed this belief is vs its last verification. */
  staleness?: number;
}

export interface EpistemicEdge {
  targetId: string;
  kind: 'supports' | 'contradicts' | 'derives-from' | 'cites';
  weight?: number;
}

// ---------------------------------------------------------------------------
// 5. Decay state — time-varying confidence/freshness for a belief or artifact
// ---------------------------------------------------------------------------

export interface DecayState {
  /** Which belief/artifact this state describes. */
  targetId: string;
  targetKind: 'belief' | 'proof' | 'artifact' | 'test';
  /** 0.0 = fresh, 1.0 = fully decayed. */
  staleness: number;
  /** Half-life in seconds — used to compute next staleness projection. */
  halfLifeSeconds: number;
  /** Last time the target was verified / refreshed. */
  lastVerifiedAt: number;
  /** Projected staleness at each lookahead tick (optional). */
  projection?: { atMs: number; staleness: number }[];
}

// ---------------------------------------------------------------------------
// Adapter boundary — the ONE seam between consumers and the transport.
// ---------------------------------------------------------------------------

/**
 * All cross-system traffic goes through this interface. Today the default
 * implementation is a no-op/mock; when the MCP bridge ships, a new class
 * implements this and consumers keep working.
 *
 * Every method returns a promise — the bridge will be async (MCP RPC).
 */
export interface TarsIxAdapter {
  /** Run a query against a target system and return canonical rows. */
  query(
    opts: { system: 'tars' | 'ix' | 'ga' | 'demerzel'; statement: string },
    signal?: AbortSignal,
  ): Promise<QueryResult>;

  /** Subscribe to violations. Returns unsubscribe fn. */
  onViolation(listener: (ev: ViolationEvent) => void): () => void;

  /** Fetch a frozen proof by id. */
  getProof(id: string, signal?: AbortSignal): Promise<ProofSnapshot | null>;

  /** Read one epistemic node (belief) by id. */
  getEpistemicNode(id: string, signal?: AbortSignal): Promise<EpistemicNode | null>;

  /** Read the current decay state of any tracked target. */
  getDecayState(targetId: string, signal?: AbortSignal): Promise<DecayState | null>;
}

// ---------------------------------------------------------------------------
// Default no-op adapter — safe to ship; lets UI render empty states.
// Replace with MCP-backed impl when the transport is live.
// ---------------------------------------------------------------------------

export class NoopTarsIxAdapter implements TarsIxAdapter {
  async query(opts: { system: string; statement: string }): Promise<QueryResult> {
    return {
      queryId: `noop-${Date.now()}`,
      startedAt: Date.now(),
      completedAt: Date.now(),
      origin: opts.system as QueryResult['origin'],
      rows: [],
      diagnostics: [{ level: 'info', message: 'TarsIxAdapter: no transport configured' }],
    };
  }
  onViolation(): () => void { return () => {}; }
  async getProof(): Promise<ProofSnapshot | null> { return null; }
  async getEpistemicNode(): Promise<EpistemicNode | null> { return null; }
  async getDecayState(): Promise<DecayState | null> { return null; }
}

/** Singleton default adapter. Swap at app boot when MCP lands. */
let _adapter: TarsIxAdapter = new NoopTarsIxAdapter();

export function getTarsIxAdapter(): TarsIxAdapter { return _adapter; }
export function setTarsIxAdapter(adapter: TarsIxAdapter): void { _adapter = adapter; }
