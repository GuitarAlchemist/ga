// Shared types for the Harness tab redesign. Mirrors the schema in
// state/harness/items.json (schema_version 1.1.0). Adding a field here
// without updating items.json is harmless (TS optional); removing one
// requires a coordinated schema bump.

export type HarnessStatus = 'shipped' | 'in_flight' | 'todo' | 'ready-for-install';

export interface HarnessItem {
  number: number;
  title: string;
  category: string;
  effort: 'S' | 'M' | 'L';
  impact: 'M' | 'H';
  status: HarnessStatus;
  pr_number: number | null;
  pr_state: string | null;
  merged_at?: string;
  merge_sha?: string;
  evidence_url: string | null;
  owner: string;
  notes?: string;
  /** Optional skill name to expose as an "Invoke" button on the card. */
  skill?: string;
  /** Optional screenshot URL — rendered as a thumbnail at the top of the card. */
  screenshot_url?: string;
}

export interface HarnessRelatedPr {
  number: number;
  title: string;
  state: string;
  evidence_url: string;
  note?: string;
}

export interface HarnessPrinciple {
  name: string;
  techniques: string[];
}

export interface HarnessBaseline {
  value: number | string;
  target?: number | string;
  as_of?: string;
  source?: string;
  urls?: string[];
  note?: string;
}

export interface HarnessPayload {
  generated_at: string;
  schema_version?: string;
  last_updated?: string;
  items: HarnessItem[];
  related_prs?: HarnessRelatedPr[];
  principles?: HarnessPrinciple[];
  baselines?: Record<string, HarnessBaseline>;
}

export interface StatusMeta {
  label: string;
  color: string;        // hex; used by both Donut + Timeline
  muiColor: 'success' | 'warning' | 'info' | 'default';
}

export function statusMeta(s: HarnessStatus): StatusMeta {
  switch (s) {
    case 'shipped':
      return { label: 'shipped', color: '#2e7d32', muiColor: 'success' };
    case 'ready-for-install':
      return { label: 'ready', color: '#0288d1', muiColor: 'info' };
    case 'in_flight':
      return { label: 'in flight', color: '#ed6c02', muiColor: 'warning' };
    case 'todo':
    default:
      return { label: 'todo', color: '#9e9e9e', muiColor: 'default' };
  }
}
