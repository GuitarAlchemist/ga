// src/components/PrimeRadiant/CaseLaw.ts
// Constitutional Case Law System — Phase C
// Violations packaged as constitutional cases with precedent search,
// hexavalent resolution mapping, and standing order detection.
// See: Demerzel deep dive — Constitutional Case Law

import { signalBus } from './DashboardSignalBus';
import type { HexavalentValue } from './HexavalentTemporal';

// ---------------------------------------------------------------------------
// Types
// ---------------------------------------------------------------------------

export interface ConstitutionalCase {
  case_id: string;
  timestamp: string;
  source: string;
  panel_type: 'grid' | 'viz' | 'form' | 'status';
  violated_articles: string[];
  predicates: CasePredicate[];
  data_snapshot: Record<string, unknown>;
  severity: 'info' | 'warning' | 'critical';
  rule_id: string;
  actions_executed: string[];
  notify_channel: string | null;
  confidence: Record<string, number> | null;
  resolution_type: 'auto-resolved' | 'human-resolved' | 'standing-order' | 'escalated' | 'dismissed' | 'pending';
  resolution_ixql: string[];
  resolution_notes: string | null;
  outcome: 'upheld' | 'overturned' | 'modified' | 'pending';
  resolved_at: string | null;
  resolved_by: string | null;
  precedent_cases: string[];
  supersedes: string | null;
  standing_order_id: string | null;
  false_positive: boolean | null;
  pattern_tags: string[];
}

export interface CasePredicate {
  field: string;
  operator: string;
  value: unknown;
}

export interface StandingOrder {
  standing_order_id: string;
  derived_from: string[];
  article: number;
  source_pattern: string;
  resolution_ixql: string[];
  confidence: Record<string, number>;
  activation_count: number;
  last_activated: string | null;
  effectiveness: number;
  status: 'active' | 'suspended' | 'promoted';
}

export interface CaseIndex {
  cases: CaseIndexEntry[];
  standing_orders: string[];
  stats: CaseStats;
}

export interface CaseIndexEntry {
  case_id: string;
  rule_id: string;
  violated_articles: string[];
  source: string;
  panel_type: string;
  severity: string;
  outcome: string;
  resolution_type: string;
  standing_order_id: string | null;
  timestamp: string;
  false_positive: boolean | null;
}

export interface CaseStats {
  total: number;
  pending: number;
  upheld: number;
  overturned: number;
  modified: number;
  false_positives: number;
}

// ---------------------------------------------------------------------------
// Precedent Search — 5-tier, string-based (no regex)
// ---------------------------------------------------------------------------

export type PrecedentTier = 1 | 2 | 3 | 4 | 5;

export interface PrecedentResult {
  tier: PrecedentTier;
  cases: CaseIndexEntry[];
  confidence: Record<HexavalentValue, number>;
  standingOrder: StandingOrder | null;
}

/** Extract article number from string like "Article 1 (Truthfulness)" */
function extractArticlePrefix(article: string): string {
  const parenIdx = article.indexOf('(');
  if (parenIdx > 0) return article.substring(0, parenIdx).trim();
  return article.trim();
}

/** Check if two article arrays have overlapping articles */
function hasArticleOverlap(a: string[], b: string[]): boolean {
  for (const articleA of a) {
    const prefixA = extractArticlePrefix(articleA);
    for (const articleB of b) {
      const prefixB = extractArticlePrefix(articleB);
      if (prefixA === prefixB) return true;
    }
  }
  return false;
}

// Confidence distributions per tier
const TIER_CONFIDENCE: Record<PrecedentTier, Record<HexavalentValue, number>> = {
  1: { T: 0.9, P: 0.1, U: 0, D: 0, F: 0, C: 0 },
  2: { T: 0.6, P: 0.3, U: 0.1, D: 0, F: 0, C: 0 },
  3: { T: 0, P: 0.6, U: 0.3, D: 0.1, F: 0, C: 0 },
  4: { T: 0, P: 0.4, U: 0.5, D: 0.1, F: 0, C: 0 },
  5: { T: 0, P: 0.3, U: 0.5, D: 0.2, F: 0, C: 0 },
};

/**
 * Search for precedent cases matching a new violation.
 * Uses cascading tiers — returns first tier with matches.
 */
export function findPrecedent(
  index: CaseIndex,
  standingOrders: StandingOrder[],
  ruleId: string,
  violatedArticles: string[],
  source: string,
  panelType: string,
): PrecedentResult | null {
  const resolved = index.cases.filter(c => c.outcome === 'upheld');
  if (resolved.length === 0) return null;

  // Check standing orders first — they take priority
  for (const so of standingOrders) {
    if (so.status !== 'active') continue;
    const articleMatch = violatedArticles.some(a => {
      const prefix = extractArticlePrefix(a);
      return prefix === 'Article ' + so.article;
    });
    if (articleMatch && source.indexOf(so.source_pattern) >= 0) {
      return {
        tier: 1,
        cases: [],
        confidence: { T: 0.95, P: 0.05, U: 0, D: 0, F: 0, C: 0 },
        standingOrder: so,
      };
    }
  }

  // Tier 1: Exact rule_id match
  const tier1 = resolved.filter(c => c.rule_id === ruleId && c.false_positive !== true);
  if (tier1.length > 0) {
    return { tier: 1, cases: tier1, confidence: TIER_CONFIDENCE[1], standingOrder: null };
  }

  // Tier 2: Article + source match
  const tier2 = resolved.filter(c =>
    hasArticleOverlap(c.violated_articles, violatedArticles) &&
    c.source === source &&
    c.false_positive !== true
  );
  if (tier2.length > 0) {
    return { tier: 2, cases: tier2, confidence: TIER_CONFIDENCE[2], standingOrder: null };
  }

  // Tier 3: Article + panel type match
  const tier3 = resolved.filter(c =>
    hasArticleOverlap(c.violated_articles, violatedArticles) &&
    c.panel_type === panelType &&
    c.false_positive !== true
  );
  if (tier3.length > 0) {
    return { tier: 3, cases: tier3, confidence: TIER_CONFIDENCE[3], standingOrder: null };
  }

  // Tier 4: Article overlap with any severity (broadest article-based match)
  const tier4 = resolved.filter(c =>
    hasArticleOverlap(c.violated_articles, violatedArticles) &&
    c.false_positive !== true
  );
  if (tier4.length > 0) {
    return { tier: 4, cases: tier4, confidence: TIER_CONFIDENCE[4], standingOrder: null };
  }

  return null; // No precedent found
}

// ---------------------------------------------------------------------------
// Hexavalent Resolution Mapping
// ---------------------------------------------------------------------------

export type ResolutionAction = 'auto-resolve' | 'suggest' | 'investigate' | 'flag-review' | 'suppress' | 'escalate';

/**
 * Map precedent confidence to a resolution action.
 * T=auto, P=suggest, U=investigate, D=flag, F=suppress, C=escalate.
 */
export function resolveFromConfidence(confidence: Record<HexavalentValue, number>): ResolutionAction {
  // Find the dominant truth value
  let maxVal = 0;
  let dominant: HexavalentValue = 'U';
  for (const [key, value] of Object.entries(confidence)) {
    if (value > maxVal) {
      maxVal = value;
      dominant = key as HexavalentValue;
    }
  }

  switch (dominant) {
    case 'T': return 'auto-resolve';
    case 'P': return 'suggest';
    case 'U': return 'investigate';
    case 'D': return 'flag-review';
    case 'F': return 'suppress';
    case 'C': return 'escalate';
  }
}

// ---------------------------------------------------------------------------
// Case Filing
// ---------------------------------------------------------------------------

let caseSequence = 0;

/** Generate a unique case ID */
export function generateCaseId(articleNum: number, source: string): string {
  const now = new Date();
  const date = now.toISOString().substring(0, 10);
  caseSequence++;
  const seq = String(caseSequence).padStart(3, '0');
  // Sanitize source to kebab-case
  const cleanSource = source.toLowerCase()
    .split('.').join('-')
    .split(' ').join('-')
    .split('/').join('-');
  return `case-art${articleNum}-${cleanSource}-${date}-${seq}`;
}

/** Create a new case from a violation */
export function fileCase(
  ruleId: string,
  source: string,
  panelType: 'grid' | 'viz' | 'form' | 'status',
  violatedArticles: string[],
  predicates: CasePredicate[],
  severity: 'info' | 'warning' | 'critical',
  dataSnapshot: Record<string, unknown>,
  actionsExecuted: string[],
  precedent: PrecedentResult | null,
): ConstitutionalCase {
  // Extract article number from first violated article
  const firstArticle = violatedArticles[0] ?? 'Article 1';
  let articleNum = 1;
  // Extract number without regex — find "Article " then parse the number
  const articleIdx = firstArticle.indexOf('Article ');
  if (articleIdx >= 0) {
    const afterArticle = firstArticle.substring(articleIdx + 8);
    const spaceIdx = afterArticle.indexOf(' ');
    const parenIdx = afterArticle.indexOf('(');
    const endIdx = Math.min(
      spaceIdx >= 0 ? spaceIdx : afterArticle.length,
      parenIdx >= 0 ? parenIdx : afterArticle.length,
    );
    const numStr = afterArticle.substring(0, endIdx).trim();
    const parsed = parseInt(numStr, 10);
    if (!isNaN(parsed)) articleNum = parsed;
  }

  const caseId = generateCaseId(articleNum, source);
  const action = precedent ? resolveFromConfidence(precedent.confidence) : 'investigate';

  const newCase: ConstitutionalCase = {
    case_id: caseId,
    timestamp: new Date().toISOString(),
    source,
    panel_type: panelType,
    violated_articles: violatedArticles,
    predicates,
    data_snapshot: dataSnapshot,
    severity,
    rule_id: ruleId,
    actions_executed: actionsExecuted,
    notify_channel: severity === 'critical' ? 'algedonic' : null,
    confidence: precedent?.confidence ?? null,
    resolution_type: precedent?.standingOrder ? 'standing-order' :
                     action === 'auto-resolve' ? 'auto-resolved' :
                     action === 'suppress' ? 'dismissed' : 'pending',
    resolution_ixql: precedent?.standingOrder?.resolution_ixql ?? [],
    resolution_notes: precedent
      ? `Resolved via ${precedent.tier === 1 ? 'exact rule match' : `tier ${precedent.tier} precedent`}` +
        (precedent.standingOrder ? ` (standing order ${precedent.standingOrder.standing_order_id})` : '')
      : null,
    outcome: action === 'auto-resolve' || action === 'suppress' ? 'upheld' : 'pending',
    resolved_at: action === 'auto-resolve' || action === 'suppress' ? new Date().toISOString() : null,
    resolved_by: action === 'auto-resolve' ? 'system' : null,
    precedent_cases: precedent?.cases.map(c => c.case_id) ?? [],
    supersedes: null,
    standing_order_id: precedent?.standingOrder?.standing_order_id ?? null,
    false_positive: null,
    pattern_tags: [panelType, source, severity],
  };

  // Publish case filing signal
  signalBus.publish('case:filed', newCase, '__caseLaw__');

  return newCase;
}

// ---------------------------------------------------------------------------
// Standing Order Detection
// ---------------------------------------------------------------------------

/**
 * Check if a set of resolved cases should form a standing order.
 * Requires 3+ upheld cases with same rule_id and same resolution.
 */
export function detectStandingOrder(
  cases: ConstitutionalCase[],
): { shouldCreate: boolean; article: number; sourcePattern: string; resolutionIxql: string[] } | null {
  // Group by rule_id
  const byRule = new Map<string, ConstitutionalCase[]>();
  for (const c of cases) {
    if (c.outcome !== 'upheld' || c.false_positive === true) continue;
    const existing = byRule.get(c.rule_id);
    if (existing) existing.push(c);
    else byRule.set(c.rule_id, [c]);
  }

  for (const [, group] of byRule) {
    if (group.length < 3) continue;

    // Check all have same resolution IXQL
    const firstResolution = group[0].resolution_ixql.join('|');
    const allSameResolution = group.every(c => c.resolution_ixql.join('|') === firstResolution);
    if (!allSameResolution) continue;

    // Extract article number
    let articleNum = 1;
    const firstArticle = group[0].violated_articles[0] ?? '';
    const idx = firstArticle.indexOf('Article ');
    if (idx >= 0) {
      const num = parseInt(firstArticle.substring(idx + 8), 10);
      if (!isNaN(num)) articleNum = num;
    }

    return {
      shouldCreate: true,
      article: articleNum,
      sourcePattern: group[0].source,
      resolutionIxql: group[0].resolution_ixql,
    };
  }

  return null;
}

// ---------------------------------------------------------------------------
// In-memory case store (session-scoped, batched to governance layer)
// ---------------------------------------------------------------------------

class CaseLawStore {
  private cases: ConstitutionalCase[] = [];
  private index: CaseIndex = { cases: [], standing_orders: [], stats: { total: 0, pending: 0, upheld: 0, overturned: 0, modified: 0, false_positives: 0 } };
  private standingOrders: StandingOrder[] = [];

  /** File a new case */
  file(newCase: ConstitutionalCase): void {
    this.cases.push(newCase);
    this.index.cases.push({
      case_id: newCase.case_id,
      rule_id: newCase.rule_id,
      violated_articles: newCase.violated_articles,
      source: newCase.source,
      panel_type: newCase.panel_type,
      severity: newCase.severity,
      outcome: newCase.outcome,
      resolution_type: newCase.resolution_type,
      standing_order_id: newCase.standing_order_id,
      timestamp: newCase.timestamp,
      false_positive: newCase.false_positive,
    });
    this.index.stats.total++;
    if (newCase.outcome === 'pending') this.index.stats.pending++;
    else if (newCase.outcome === 'upheld') this.index.stats.upheld++;
  }

  /** Search for precedent */
  findPrecedent(ruleId: string, articles: string[], source: string, panelType: string): PrecedentResult | null {
    return findPrecedent(this.index, this.standingOrders, ruleId, articles, source, panelType);
  }

  /** Get all cases */
  getCases(): ConstitutionalCase[] { return this.cases; }

  /** Get index */
  getIndex(): CaseIndex { return this.index; }

  /** Get stats */
  getStats(): CaseStats { return this.index.stats; }

  /** Reset store for test isolation */
  reset(): void {
    this.cases = [];
    this.index = { cases: [], standing_orders: [], stats: { total: 0, pending: 0, upheld: 0, overturned: 0, modified: 0, false_positives: 0 } };
    this.standingOrders = [];
  }
}

export const caseLawStore = new CaseLawStore();
