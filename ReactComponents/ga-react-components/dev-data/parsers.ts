// Pure parser functions for dev-data middleware. Extracted from the
// vite.config.ts devDataPlugin() closure so they are unit-testable in
// isolation (see parsers.test.ts).
//
// All functions take string content (file contents) and return structured
// data. No filesystem, no network. Easy to fixture in tests.

export type SubSectionCategory = 'shipped' | 'active' | 'backlog';

export interface EpicSubSection {
    title: string;
    category: SubSectionCategory;
    item_count: number;
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
 * Parse a markdown backlog into epics + sub-sections + item counts.
 *
 * Structure expected:
 *   # H1 ignored (project title)
 *   ## Epic name           → BacklogEpic
 *   ### Sub-section name   → EpicSubSection (categorized via categorize())
 *   - bullet               → counted into the current sub-section
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
            currentSub = { title: '(untriaged)', category: categorize(epicTitle), item_count: 0 };
        } else if (h3 && currentEpic) {
            if (currentSub && currentSub.item_count > 0) currentEpic.sub_sections.push(currentSub);
            currentSub = { title: h3[1].trim(), category: categorize(h3[1].trim()), item_count: 0 };
        } else if (bullet && currentSub) {
            currentSub.item_count += 1;
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
