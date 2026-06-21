// Pure parser functions for dev-data middleware. Extracted from the
// vite.config.ts devDataPlugin() closure so they are unit-testable in
// isolation (see parsers.test.ts).
//
// All functions take string content (file contents) and return structured
// data. No filesystem, no network. Easy to fixture in tests.

export type SubSectionCategory = 'shipped' | 'active' | 'backlog';

export interface BacklogItem {
    /** Bullet body with the leading marker (- / *) stripped. */
    text: string;
    /** Inherits from the enclosing sub-section's category. */
    status: SubSectionCategory;
    /** GitHub PR numbers extracted from `#NNN` patterns. */
    pr_refs?: number[];
    /** Doc paths extracted from `docs/...` patterns. */
    doc_refs?: string[];
    /** Original markdown line for debugging. */
    raw_line: string;
}

export interface EpicSubSection {
    title: string;
    category: SubSectionCategory;
    item_count: number;
    items: BacklogItem[];
}

export interface BacklogEpic {
    title: string;
    total_items: number;
    shipped: number;
    active: number;
    backlog: number;
    progress_pct: number;
    sub_sections: EpicSubSection[];
}

export interface BacklogSection {
    title: string;
    item_count: number;
}

export interface BacklogPayload {
    total_epics: number;
    total_items: number;
    total_shipped: number;
    overall_progress_pct: number;
    epics: BacklogEpic[];
    // Legacy view kept for back-compat with old ManifestViewer Backlog table
    total_sections: number;
    top_sections: BacklogSection[];
}

/**
 * Heuristic: classify a backlog sub-section by its title. Matches:
 * - 'shipped' as the first word or after a space (case-insensitive)
 * - 'active' / 'in progress' / 'in-progress' as the first word
 * Otherwise defaults to 'backlog'.
 *
 * Examples:
 *   categorize("Shipped (2026-03-28 session)") → 'shipped'
 *   categorize("Active Ideas")                  → 'active'
 *   categorize("In Progress")                   → 'active'
 *   categorize("New Ideas")                     → 'backlog'
 *   categorize("Recently shipped")              → 'shipped' (word-boundary match)
 */
export function categorize(title: string): SubSectionCategory {
    const t = title.toLowerCase();
    if (t.startsWith('shipped') || / shipped\b/.test(t) || t.includes('(shipped')) return 'shipped';
    if (t.startsWith('active') || / active\b/.test(t) || t.startsWith('in progress') || t.startsWith('in-progress')) return 'active';
    return 'backlog';
}

/**
 * Extract `#NNN` patterns from a line. Matches PR/issue references like `(#137)`,
 * `#138`, `PR #207`. Ignores in-word matches (e.g. `foo#1` won't match because
 * we require a non-word boundary before `#`). Returns numbers deduplicated in
 * order of first appearance.
 */
export function extractPrRefs(line: string): number[] {
    const refs: number[] = [];
    const seen = new Set<number>();
    const re = /(?:^|[^\w])#(\d+)\b/g;
    let m: RegExpExecArray | null;
    while ((m = re.exec(line)) !== null) {
        const n = Number(m[1]);
        if (!Number.isNaN(n) && !seen.has(n)) {
            seen.add(n);
            refs.push(n);
        }
    }
    return refs;
}

/**
 * Extract `docs/...` paths from a line. Matches Markdown-link targets and bare
 * paths (`docs/plans/foo.md`, `[link](docs/plans/foo.md)`). Stops at whitespace,
 * `)`, `]`, backtick, or end-of-line. Deduplicates in order of first appearance.
 */
export function extractDocRefs(line: string): string[] {
    const refs: string[] = [];
    const seen = new Set<string>();
    const re = /\bdocs\/[^\s)\]`]+/g;
    let m: RegExpExecArray | null;
    while ((m = re.exec(line)) !== null) {
        // Trim trailing punctuation that's commonly attached to a sentence
        // boundary but not part of the path itself.
        const cleaned = m[0].replace(/[.,;:]+$/, '');
        if (!seen.has(cleaned)) {
            seen.add(cleaned);
            refs.push(cleaned);
        }
    }
    return refs;
}

/**
 * Convert a bullet line into a BacklogItem. Strips the leading `- ` or `* `
 * marker, extracts PR refs and doc refs.
 */
function bulletToItem(line: string, status: SubSectionCategory): BacklogItem {
    const text = line.replace(/^[-*]\s+/, '').trim();
    const pr_refs = extractPrRefs(line);
    const doc_refs = extractDocRefs(line);
    const item: BacklogItem = { text, status, raw_line: line };
    if (pr_refs.length > 0) item.pr_refs = pr_refs;
    if (doc_refs.length > 0) item.doc_refs = doc_refs;
    return item;
}

/**
 * Parse a markdown backlog into epics + sub-sections + items.
 *
 * Structure expected:
 *   # H1 ignored (project title)
 *   ## Epic name           → BacklogEpic
 *   ### Sub-section name   → EpicSubSection (categorized via categorize())
 *   - bullet               → BacklogItem under the current sub-section
 *
 * Bullets that appear directly under an H2 (before any H3) inherit the H2's
 * category so an epic titled "Shipped 2026" with direct bullets reports
 * shipped progress instead of being silently rolled up as backlog.
 *
 * Tolerates CRLF line endings.
 */
export function parseBacklog(content: string): BacklogPayload {
    const lines = content.split(/\r?\n/);
    const epics: BacklogEpic[] = [];
    let currentEpic: BacklogEpic | null = null;
    let currentSub: EpicSubSection | null = null;

    for (const line of lines) {
        const h2 = line.match(/^##\s+(.+?)\s*$/);
        const h3 = line.match(/^###\s+(.+?)\s*$/);
        const bullet = /^[-*]\s/.test(line);

        if (h2) {
            if (currentEpic) {
                if (currentSub && currentSub.item_count > 0) currentEpic.sub_sections.push(currentSub);
                epics.push(currentEpic);
            }
            const epicTitle = h2[1].trim();
            currentEpic = {
                title: epicTitle,
                total_items: 0,
                shipped: 0,
                active: 0,
                backlog: 0,
                progress_pct: 0,
                sub_sections: [],
            };
            currentSub = { title: '(untriaged)', category: categorize(epicTitle), item_count: 0, items: [] };
        } else if (h3 && currentEpic) {
            if (currentSub && currentSub.item_count > 0) currentEpic.sub_sections.push(currentSub);
            const subTitle = h3[1].trim();
            currentSub = { title: subTitle, category: categorize(subTitle), item_count: 0, items: [] };
        } else if (bullet && currentSub) {
            currentSub.item_count += 1;
            currentSub.items.push(bulletToItem(line, currentSub.category));
        }
    }
    if (currentEpic) {
        if (currentSub && currentSub.item_count > 0) currentEpic.sub_sections.push(currentSub);
        epics.push(currentEpic);
    }

    for (const e of epics) {
        for (const s of e.sub_sections) {
            e.total_items += s.item_count;
            if (s.category === 'shipped') e.shipped += s.item_count;
            else if (s.category === 'active') e.active += s.item_count;
            else e.backlog += s.item_count;
        }
        e.progress_pct = e.total_items > 0 ? Math.round((e.shipped / e.total_items) * 100) : 0;
    }

    const totalItems = epics.reduce((sum, e) => sum + e.total_items, 0);
    const totalShipped = epics.reduce((sum, e) => sum + e.shipped, 0);

    return {
        total_epics: epics.length,
        total_items: totalItems,
        total_shipped: totalShipped,
        overall_progress_pct: totalItems > 0 ? Math.round((totalShipped / totalItems) * 100) : 0,
        epics,
        total_sections: epics.length,
        top_sections: epics.slice(0, 8).map((e) => ({ title: e.title, item_count: e.total_items })),
    };
}

/**
 * Extract a doc title from the first ~15 lines of a markdown file. Order of
 * preference: H1 heading → `title:` YAML frontmatter line → null (caller
 * should fall back to a filename-derived slug).
 */
export function extractDocTitle(content: string): string | null {
    const head = content.split(/\r?\n/).slice(0, 15);
    const h1 = head.find((l) => /^#\s/.test(l));
    if (h1) return h1.replace(/^#\s+/, '').trim();
    const fm = head.find((l) => /^title:\s/.test(l));
    if (fm) return fm.replace(/^title:\s+/, '').trim().replace(/^["']|["']$/g, '');
    return null;
}

/**
 * Bin ISO-8601 timestamps into per-day commit counts over a sliding window
 * ending on the given `now`. Returns one entry per day, days that had no
 * activity included with count: 0.
 */
export interface ActivityByDay { date: string; count: number }
export function binActivityByDay(timestamps: string[], days: number, now: Date = new Date()): ActivityByDay[] {
    const bins: Record<string, number> = {};
    for (let i = days - 1; i >= 0; i -= 1) {
        const d = new Date(now);
        d.setDate(d.getDate() - i);
        bins[d.toISOString().slice(0, 10)] = 0;
    }
    for (const ts of timestamps) {
        if (!ts) continue;
        const day = ts.slice(0, 10);
        if (day in bins) bins[day] += 1;
    }
    return Object.entries(bins).map(([date, count]) => ({ date, count }));
}

// ─── /loop and /goal runtime tracker projection ─────────────────────────
// Reads the append-only JSONL emitted by .claude/hooks/loops-goals-tracker.ps1
// and projects it into "currently active" + "recently completed" buckets.
// Schema of each JSONL line (one per event):
//   { id, kind, started_at, session_id, prompt_or_condition, turn_count,
//     last_activity_at, status, event, branch }
// Status transitions: active → paused → completed → archived.

export type LoopsGoalsKind = 'loop' | 'goal';
export type LoopsGoalsStatus = 'active' | 'paused' | 'completed' | 'archived';

export interface LoopsGoalsRecord {
    id: string;
    kind: LoopsGoalsKind;
    started_at: string;
    session_id: string;
    prompt_or_condition: string;
    turn_count: number;
    last_activity_at: string;
    status: LoopsGoalsStatus;
    branch?: string | null;
}

export interface LoopsGoalsActiveView extends LoopsGoalsRecord {
    age_min: number;
    last_activity_min_ago: number;
}

export interface LoopsGoalsProjection {
    fetched_at: string;
    active_loops: LoopsGoalsActiveView[];
    active_goals: LoopsGoalsActiveView[];
    completed_recent: LoopsGoalsRecord[];
    total_records: number;
}

/**
 * Project an array of JSONL lines into the dashboard's view of active +
 * recently-completed loops/goals. Latest record-per-id wins, archived rows
 * are dropped, completed rows older than `archiveCutoffDays` are dropped.
 * Pure function — `now` is injected for testability.
 */
export function projectLoopsGoals(
    lines: string[],
    now: Date = new Date(),
    archiveCutoffDays = 7,
): LoopsGoalsProjection {
    const byId = new Map<string, LoopsGoalsRecord>();
    let totalRecords = 0;
    for (const line of lines) {
        const trim = (line ?? '').trim();
        if (!trim) continue;
        totalRecords += 1;
        try {
            const rec = JSON.parse(trim) as Partial<LoopsGoalsRecord>;
            if (!rec.id || !rec.kind || !rec.status) continue;
            const existing = byId.get(rec.id);
            const recAct = rec.last_activity_at ?? rec.started_at ?? '';
            const existingAct = existing?.last_activity_at ?? existing?.started_at ?? '';
            // Compare numerically — hook events use second precision
            // (`yyyy-MM-ddTHH:mm:ssZ`) while dashboard stop events use
            // `toISOString()` with milliseconds. Lexicographic compare on
            // mixed formats made `...00.123Z` sort BEFORE `...00Z`, so a
            // newer completion event could be ignored and the row stayed
            // active after pressing Stop. `Date.parse` collapses both to
            // epoch ms; NaN falls back to ordering an unparseable value
            // before a parseable one so we never starve a valid update.
            const recMs = Date.parse(recAct);
            const existingMs = Date.parse(existingAct);
            const recCmp = Number.isNaN(recMs) ? -Infinity : recMs;
            const existingCmp = Number.isNaN(existingMs) ? -Infinity : existingMs;
            if (!existing || recCmp >= existingCmp) {
                byId.set(rec.id, {
                    id: rec.id,
                    kind: rec.kind,
                    started_at: rec.started_at ?? '',
                    session_id: rec.session_id ?? 'unknown',
                    prompt_or_condition: rec.prompt_or_condition ?? '',
                    turn_count: typeof rec.turn_count === 'number' ? rec.turn_count : 0,
                    last_activity_at: rec.last_activity_at ?? rec.started_at ?? '',
                    status: rec.status,
                    branch: rec.branch ?? null,
                });
            }
        } catch { /* skip malformed */ }
    }

    const minutesSince = (iso: string): number => {
        if (!iso) return -1;
        const t = Date.parse(iso);
        if (Number.isNaN(t)) return -1;
        return Math.max(0, Math.floor((now.getTime() - t) / 60000));
    };

    const decorate = (r: LoopsGoalsRecord): LoopsGoalsActiveView => ({
        ...r,
        age_min: minutesSince(r.started_at),
        last_activity_min_ago: minutesSince(r.last_activity_at),
    });

    const active_loops: LoopsGoalsActiveView[] = [];
    const active_goals: LoopsGoalsActiveView[] = [];
    const completed_recent: LoopsGoalsRecord[] = [];
    const cutoffMs = now.getTime() - archiveCutoffDays * 24 * 60 * 60 * 1000;

    for (const rec of byId.values()) {
        if (rec.status === 'archived') continue;
        if (rec.status === 'active' || rec.status === 'paused') {
            const view = decorate(rec);
            if (rec.kind === 'loop') active_loops.push(view);
            else active_goals.push(view);
        } else if (rec.status === 'completed') {
            const t = Date.parse(rec.last_activity_at);
            if (!Number.isNaN(t) && t >= cutoffMs) completed_recent.push(rec);
        }
    }

    // Newest activity first
    active_loops.sort((a, b) => b.last_activity_at.localeCompare(a.last_activity_at));
    active_goals.sort((a, b) => b.last_activity_at.localeCompare(a.last_activity_at));
    completed_recent.sort((a, b) => b.last_activity_at.localeCompare(a.last_activity_at));

    return {
        fetched_at: now.toISOString(),
        active_loops,
        active_goals,
        completed_recent: completed_recent.slice(0, 20),
        total_records: totalRecords,
    };
}

// ── Business-value scorecard ────────────────────────────────────────────
// Parses the federated value catalog (state/value/catalog.jsonl, emitted by
// the sibling ix repo's `ix-value` generator — a RICE→stars rollup across
// repos). One JSON object per line; malformed lines are skipped, never fatal
// (mirrors the tolerant ingest on the ix side). See:
//   ix/crates/ix-value, ix/docs/contracts/business-value.contract.md

export interface ValueRecord {
    schema_version: string;
    /** Item id (authored) or repo name (for rollup rows). */
    id: string;
    repo: string;
    /** 'demo' / 'epic' for items; 'repo' for the generated rollup row. */
    kind: 'demo' | 'repo' | 'epic';
    title: string;
    reach: number;
    impact: number;
    confidence: number;
    /** round(geomean(R,I,C)) clamped to 1..=5. */
    stars: number;
    /** geomean(R,I,C) / 5 ∈ (0,1] — the sortable continuous score. */
    score01: number;
    rationale?: string;
}

/**
 * Parse the value catalog JSONL into records. Tolerant: blank lines are
 * ignored and a line that is not a well-formed record (bad JSON, or missing
 * the load-bearing `kind`/`stars` fields) is dropped rather than throwing —
 * a single corrupt line must not blank the whole scorecard.
 */
export function parseValueCatalog(content: string): ValueRecord[] {
    const out: ValueRecord[] = [];
    for (const line of content.split('\n')) {
        const trimmed = line.trim();
        if (!trimmed) continue;
        try {
            const r = JSON.parse(trimmed) as Partial<ValueRecord>;
            if (typeof r.id !== 'string' || typeof r.kind !== 'string') continue;
            if (typeof r.stars !== 'number' || typeof r.score01 !== 'number') continue;
            out.push(r as ValueRecord);
        } catch {
            // skip malformed line
        }
    }
    return out;
}

// ── Maintain-gate advisory verdict (ga#446 / Phase C) ───────────────────
// Parses the fused cross-signal maintain verdict IX emits in scorecard shape,
// federated into state/quality/maintain-gate/last.json (producer ix
// ix_maintain_snapshot; federation ix#146; schema ga#445). It is ADVISORY /
// non-binding until IX Phase-3b (`advisory:true`) — GA surfaces it as the
// fused verdict our per-axis gates individually lack, NOT another gate. See:
//   docs/contracts/maintain-gate-snapshot.schema.json (allOf quality-snapshot)

/** Hexavalent fused verdict. LOCKED enum (cross-repo contract surface). */
export type MaintainStatus = 'T' | 'P' | 'U' | 'D' | 'F' | 'C';
/** Coarse routing of `status`. */
export type MaintainDecision = 'accept' | 'reject' | 'escalate';
/** Traffic-light oracle status (shared quality-snapshot envelope). */
export type MaintainOracleStatus = 'ok' | 'warn' | 'error';
/** Per-lens sub-signal verdict. */
export type MaintainSignalStatus = 'ok' | 'bad' | 'unknown';
/** LOCKED sub-signal keys. */
export type MaintainLens = 'metric' | 'guardrail' | 'convergence' | 'drift' | 'provenance';

export interface MaintainSignal {
    lens: MaintainLens;
    status: MaintainSignalStatus;
    detail?: string;
}

export interface MaintainTrend {
    total?: number;
    accepts?: number;
    rejects?: number;
    escalates?: number;
    reward_hacks?: number;
    latest_status?: string | null;
}

export interface MaintainVerdictSnapshot {
    schema_version?: string;
    domain: string;
    emitted_at: string;
    metric_name: string;
    metric_value: number;
    oracle_status: MaintainOracleStatus | string;
    summary: string;
    advisory: boolean;
    status: MaintainStatus | string;
    decision: MaintainDecision | string;
    signals: MaintainSignal[];
    maintain_trend?: MaintainTrend;
}

/**
 * Parse the maintain-gate snapshot JSON. Tolerant: returns null on bad JSON or
 * when the load-bearing contract fields are absent, so the tile renders an
 * honest "no data" / "stale" state rather than throwing or faking green.
 */
export function parseMaintainGate(content: string): MaintainVerdictSnapshot | null {
    let parsed: unknown;
    try {
        parsed = JSON.parse(content);
    } catch {
        return null;
    }
    if (!parsed || typeof parsed !== 'object') return null;
    const r = parsed as Partial<MaintainVerdictSnapshot>;
    if (typeof r.status !== 'string' || typeof r.emitted_at !== 'string') return null;
    if (typeof r.oracle_status !== 'string') return null;
    return {
        schema_version: r.schema_version,
        domain: r.domain ?? 'maintain-gate',
        emitted_at: r.emitted_at,
        metric_name: r.metric_name ?? 'maintain_yield_delta',
        metric_value: typeof r.metric_value === 'number' ? r.metric_value : Number.NaN,
        oracle_status: r.oracle_status,
        summary: r.summary ?? '',
        advisory: r.advisory !== false, // default to advisory (fail-safe to non-binding)
        status: r.status,
        decision: r.decision ?? 'escalate',
        signals: Array.isArray(r.signals) ? (r.signals as MaintainSignal[]) : [],
        maintain_trend: r.maintain_trend,
    };
}

/**
 * Hours elapsed since `emitted_at`. Returns null when the timestamp is absent
 * or unparseable (so the caller treats "unknown freshness" as stale, never as
 * fresh). `now` injected for testability.
 */
export function maintainAgeHours(emittedAt: string | null | undefined, now: Date = new Date()): number | null {
    if (!emittedAt) return null;
    const t = Date.parse(emittedAt);
    if (Number.isNaN(t)) return null;
    return Math.max(0, (now.getTime() - t) / 3_600_000);
}

/**
 * A snapshot is stale once it is older than `maxAgeHours` (default 36h — a
 * daily-federated artifact that hasn't refreshed in >1.5 days is no longer
 * "today"). Unknown freshness (null age) is treated as stale: the green-but-
 * dead guard the issue asks for — a missing/old snapshot must NOT read fresh.
 */
export function isMaintainStale(ageHours: number | null, maxAgeHours = 36): boolean {
    if (ageHours == null) return true;
    return ageHours > maxAgeHours;
}
