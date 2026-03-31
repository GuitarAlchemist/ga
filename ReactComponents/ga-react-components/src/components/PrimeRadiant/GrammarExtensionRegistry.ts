// src/components/PrimeRadiant/GrammarExtensionRegistry.ts
// Living Grammar Extension Registry — Phase D
// Enables hot-loading of new PIPE step keywords via TARS bridge proposals.
// Extensions desugar to existing PipeStep types — no new semantics.
// See: Demerzel deep dive — Living Grammar

import type { PipeStep, IxqlPredicate } from './IxqlControlParser';
import { signalBus } from './DashboardSignalBus';

// ---------------------------------------------------------------------------
// Types
// ---------------------------------------------------------------------------

export type ExtensionStatus = 'trial' | 'stable' | 'deprecated';

export interface ExtensionArg {
  name: string;
  type: 'integer' | 'field' | 'direction' | 'predicate';
  optional: boolean;
}

/**
 * A pipe step extension — a new keyword that desugars to existing PipeStep[].
 * The desugaring function receives parsed arguments and returns existing steps.
 */
export interface PipeStepExtension {
  keyword: string;             // e.g., "TOP"
  status: ExtensionStatus;
  args: ExtensionArg[];
  desugar: (parsedArgs: Record<string, unknown>) => PipeStep[];
  addedAt: number;             // timestamp
  proposalId: string | null;   // TARS proposal reference
  usageCount: number;          // tracked for lifecycle management
  description: string;         // human-readable description
}

export interface GrammarVersion {
  major: number;
  minor: number;
  patch: number;
}

/** Grammar proposal from TARS */
export interface GrammarProposal {
  proposal_id: string;
  keyword: string;
  syntax_ebnf: string;
  description: string;
  args: ExtensionArg[];
  desugar_spec: DesugarSpec[];
  evidence: {
    occurrences: number;
    first_seen: string;
    last_seen: string;
  };
  confidence: number;
  constitutional_alignment: { article: number; reason: string }[];
  violations: string[];
}

export interface DesugarSpec {
  type: string;
  field?: string;
  direction?: string;
  count?: string;
  predicates?: string;
}

// ---------------------------------------------------------------------------
// Constitutional Gate — blocks governance-circumventing keywords
// ---------------------------------------------------------------------------

/** Keywords that must never be registered as extensions */
const BLOCKED_PATTERNS: { keywords: string[]; articles: number[]; reason: string }[] = [
  {
    keywords: ['IGNORE', 'SUPPRESS', 'HIDE', 'BYPASS', 'SKIP_GOVERNANCE'],
    articles: [7, 8, 9],
    reason: 'Keywords that suppress governance signals violate auditability/observability/bounded-autonomy',
  },
  {
    keywords: ['DELETE_ALL', 'DROP_ALL', 'PURGE', 'DESTROY'],
    articles: [3],
    reason: 'Irreversible bulk operations violate reversibility without explicit confirmation gate',
  },
  {
    keywords: ['FABRICATE', 'FAKE', 'MOCK_DATA'],
    articles: [1, 5],
    reason: 'Generating false data violates truthfulness and non-deception',
  },
  {
    keywords: ['SILENT', 'MUTE', 'QUIET'],
    articles: [2],
    reason: 'Suppressing reasoning explanations violates transparency',
  },
];

export interface ConstitutionalGateResult {
  allowed: boolean;
  violations: string[];
}

/**
 * Check if a proposed keyword passes the constitutional gate.
 * Returns allowed=false with violation reasons if blocked.
 */
export function constitutionalGate(keyword: string): ConstitutionalGateResult {
  // Normalize: strip zero-width chars, combining marks, and non-alphanumeric chars
  // Prevents unicode bypass (e.g., I\u200BGNORE, I\u0307GNORE)
  let normalized = '';
  for (let i = 0; i < keyword.length; i++) {
    const code = keyword.charCodeAt(i);
    // Keep only ASCII alphanumeric and underscore
    if ((code >= 65 && code <= 90) || (code >= 97 && code <= 122) ||
        (code >= 48 && code <= 57) || code === 95) {
      normalized += keyword[i];
    }
  }
  const upper = normalized.toUpperCase();
  const violations: string[] = [];

  for (const pattern of BLOCKED_PATTERNS) {
    for (const blocked of pattern.keywords) {
      // Exact match only — prevents over-blocking (e.g., SIGNORE != IGNORE)
      if (upper === blocked) {
        violations.push(
          `Keyword "${keyword}" blocked by Articles ${pattern.articles.join(', ')}: ${pattern.reason}`
        );
      }
    }
  }

  return {
    allowed: violations.length === 0,
    violations,
  };
}

// ---------------------------------------------------------------------------
// Extension Registry
// ---------------------------------------------------------------------------

class ExtensionRegistryImpl {
  private extensions = new Map<string, PipeStepExtension>();
  private version: GrammarVersion = { major: 1, minor: 0, patch: 0 };
  private telemetryBuffer: TelemetryEvent[] = [];

  /** Register a new extension. Returns false if constitutional gate blocks it. */
  register(ext: PipeStepExtension): boolean {
    const gate = constitutionalGate(ext.keyword);
    if (!gate.allowed) {
      signalBus.publish('grammar:rejected', {
        keyword: ext.keyword,
        violations: gate.violations,
      }, '__grammarRegistry__');
      return false;
    }

    const upper = ext.keyword.toUpperCase();
    this.extensions.set(upper, ext);

    // Version bump
    if (ext.status === 'trial') this.version.patch++;
    else if (ext.status === 'stable') this.version.minor++;

    signalBus.publish('grammar:registered', {
      keyword: ext.keyword,
      status: ext.status,
      version: this.versionString(),
    }, '__grammarRegistry__');

    return true;
  }

  /** Unregister an extension */
  unregister(keyword: string): void {
    this.extensions.delete(keyword.toUpperCase());
    this.version.patch++;
  }

  /** Check if a keyword is registered */
  has(keyword: string): boolean {
    return this.extensions.has(keyword.toUpperCase());
  }

  /** Get an extension by keyword */
  get(keyword: string): PipeStepExtension | undefined {
    return this.extensions.get(keyword.toUpperCase());
  }

  /** Get all registered keywords */
  getKeywords(): string[] {
    return Array.from(this.extensions.keys());
  }

  /** Get all extensions */
  getAll(): PipeStepExtension[] {
    return Array.from(this.extensions.values());
  }

  /** Get current grammar version */
  getVersion(): GrammarVersion {
    return { ...this.version };
  }

  /** Get grammar version as string */
  versionString(): string {
    return `${this.version.major}.${this.version.minor}.${this.version.patch}`;
  }

  /** Increment usage count for a keyword */
  recordUsage(keyword: string): void {
    const ext = this.extensions.get(keyword.toUpperCase());
    if (ext) ext.usageCount++;
  }

  /** Promote a trial keyword to stable */
  promote(keyword: string): boolean {
    const ext = this.extensions.get(keyword.toUpperCase());
    if (!ext || ext.status !== 'trial') return false;
    ext.status = 'stable';
    this.version.minor++;
    signalBus.publish('grammar:promoted', { keyword, version: this.versionString() }, '__grammarRegistry__');
    return true;
  }

  /** Deprecate a keyword */
  deprecate(keyword: string): boolean {
    const ext = this.extensions.get(keyword.toUpperCase());
    if (!ext || ext.status === 'deprecated') return false;
    ext.status = 'deprecated';
    this.version.patch++;
    signalBus.publish('grammar:deprecated', { keyword, version: this.versionString() }, '__grammarRegistry__');
    return true;
  }

  // -----------------------------------------------------------------------
  // Telemetry — track IXQL usage patterns for TARS
  // -----------------------------------------------------------------------

  /** Record a pipe step execution for telemetry */
  recordTelemetry(pipeSteps: string[], commandType: string): void {
    this.telemetryBuffer.push({
      commandType,
      pipeSteps,
      timestamp: Date.now(),
    });

    // Flush at 50 events (or when TARS bridge requests)
    if (this.telemetryBuffer.length >= 50) {
      this.flushTelemetry();
    }
  }

  /** Get and clear telemetry buffer */
  flushTelemetry(): TelemetryEvent[] {
    const buffer = this.telemetryBuffer;
    this.telemetryBuffer = [];
    return buffer;
  }

  /** Reset registry for test isolation */
  reset(): void {
    this.extensions.clear();
    this.version = { major: 1, minor: 0, patch: 0 };
    this.telemetryBuffer = [];
  }

  // -----------------------------------------------------------------------
  // Proposal processing — accept TARS grammar proposals
  // -----------------------------------------------------------------------

  /** Process a grammar proposal from TARS */
  processProposal(proposal: GrammarProposal): { accepted: boolean; reason: string } {
    // Constitutional gate
    const gate = constitutionalGate(proposal.keyword);
    if (!gate.allowed) {
      return { accepted: false, reason: gate.violations.join('; ') };
    }

    // Confidence threshold for auto-approval
    if (proposal.confidence < 0.7) {
      return { accepted: false, reason: `Confidence ${proposal.confidence} below threshold 0.7` };
    }

    // Usage threshold
    if (proposal.evidence.occurrences < 10) {
      return { accepted: false, reason: `Only ${proposal.evidence.occurrences} occurrences, need >= 10` };
    }

    // Check for name collision
    if (this.has(proposal.keyword)) {
      return { accepted: false, reason: `Keyword "${proposal.keyword}" already registered` };
    }

    // Build desugar function from spec
    const desugar = buildDesugarFunction(proposal.desugar_spec);

    const status: ExtensionStatus = 'trial';

    const ext: PipeStepExtension = {
      keyword: proposal.keyword,
      status,
      args: proposal.args,
      desugar,
      addedAt: Date.now(),
      proposalId: proposal.proposal_id,
      usageCount: 0,
      description: proposal.description,
    };

    const registered = this.register(ext);
    if (!registered) {
      return { accepted: false, reason: 'Constitutional gate rejected registration' };
    }

    return { accepted: true, reason: `Registered as ${status}` };
  }
}

export interface TelemetryEvent {
  commandType: string;
  pipeSteps: string[];
  timestamp: number;
}

// ---------------------------------------------------------------------------
// Desugar function builder — converts DesugarSpec[] to a desugar function
// ---------------------------------------------------------------------------

function buildDesugarFunction(specs: DesugarSpec[]): (args: Record<string, unknown>) => PipeStep[] {
  return (args: Record<string, unknown>): PipeStep[] => {
    const steps: PipeStep[] = [];
    for (const spec of specs) {
      switch (spec.type) {
        case 'sort': {
          const field = spec.field?.startsWith('$') ? String(args[spec.field.substring(1)] ?? '') : (spec.field ?? '');
          const dir = spec.direction?.startsWith('$') ? String(args[spec.direction.substring(1)] ?? 'ASC') : (spec.direction ?? 'ASC');
          steps.push({ type: 'sort', field, direction: dir as 'ASC' | 'DESC' });
          break;
        }
        case 'limit': {
          const count = spec.count?.startsWith('$') ? Number(args[spec.count.substring(1)] ?? 10) : parseInt(spec.count ?? '10', 10);
          steps.push({ type: 'limit', count });
          break;
        }
        case 'skip': {
          const count = spec.count?.startsWith('$') ? Number(args[spec.count.substring(1)] ?? 0) : parseInt(spec.count ?? '0', 10);
          steps.push({ type: 'skip', count });
          break;
        }
        case 'filter': {
          steps.push({ type: 'filter', predicates: (args['predicates'] as IxqlPredicate[]) ?? [] });
          break;
        }
        case 'distinct': {
          const field = spec.field?.startsWith('$') ? String(args[spec.field.substring(1)] ?? '') : (spec.field ?? null);
          steps.push({ type: 'distinct', field });
          break;
        }
        case 'flatten': {
          const field = spec.field?.startsWith('$') ? String(args[spec.field.substring(1)] ?? '') : (spec.field ?? '');
          steps.push({ type: 'flatten', field });
          break;
        }
      }
    }
    return steps;
  };
}

// ---------------------------------------------------------------------------
// Singleton
// ---------------------------------------------------------------------------

export const extensionRegistry = new ExtensionRegistryImpl();

// ---------------------------------------------------------------------------
// Register built-in sugar extensions (example: TOP)
// ---------------------------------------------------------------------------

extensionRegistry.register({
  keyword: 'TOP',
  status: 'trial',
  args: [
    { name: 'count', type: 'integer', optional: false },
    { name: 'by_field', type: 'field', optional: true },
    { name: 'direction', type: 'direction', optional: true },
  ],
  desugar: (args) => {
    const steps: PipeStep[] = [];
    if (args['by_field']) {
      steps.push({
        type: 'sort',
        field: String(args['by_field']),
        direction: (String(args['direction'] ?? 'DESC') as 'ASC' | 'DESC'),
      });
    }
    steps.push({ type: 'limit', count: Number(args['count'] ?? 10) });
    return steps;
  },
  addedAt: Date.now(),
  proposalId: null,
  usageCount: 0,
  description: 'TOP N [BY field [ASC|DESC]] — shorthand for SORT + LIMIT',
});
