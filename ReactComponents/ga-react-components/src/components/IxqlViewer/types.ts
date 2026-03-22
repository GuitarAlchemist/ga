// IxQL Pipeline Viewer — TypeScript types

export type NodeKind =
  | 'binding'
  | 'fan_out'
  | 'parallel'
  | 'when'
  | 'filter'
  | 'head'
  | 'write'
  | 'alert'
  | 'governance_gate'
  | 'compound'
  | 'comment';

export type LolliStatus = 'live' | 'dead' | 'external';

export interface IxqlBinding {
  id: string;
  name: string;
  expression: string;
  kind: NodeKind;
  line: number;
  /** Names of bindings this one references */
  references: string[];
  /** Names of bindings that reference this one */
  referencedBy: string[];
  /** Whether this is a serial or parallel stage */
  executionMode: 'serial' | 'parallel';
  /** Markdown comment attached to this binding (from --- blocks) */
  markdownComment?: string;
  /** Plain comments (from -- lines) */
  plainComments: string[];
  /** LOLLI status after analysis */
  lolliStatus: LolliStatus;
  /** Estimated relative cost for Amdahl computation */
  estimatedCost: number;
}

export interface IxqlEdge {
  id: string;
  source: string;
  target: string;
  label?: string;
}

export interface IxqlGraph {
  bindings: IxqlBinding[];
  edges: IxqlEdge[];
  rawSource: string;
}

export interface LolliReport {
  totalBindings: number;
  liveBindings: number;
  deadBindings: number;
  externalBindings: number;
  lolliScore: number; // percentage dead
  deadNames: string[];
}

export interface AmdahlReport {
  totalStages: number;
  serialStages: number;
  parallelStages: number;
  serialFraction: number;
  speedupAtN: (n: number) => number;
  theoreticalMax: number;
}

export type ViewMode = 'graph' | 'source' | 'split';
