// dev-data/parsers — unit tests for the dashboard's pure parser functions.
// Covers the categorize() heuristic, parseBacklog() epic/sub-section
// extraction (with regression coverage for the CRLF + pre-H3 inheritance
// bugs caught during /octo:review), extractDocTitle(), and binActivityByDay().

import { describe, it, expect } from 'vitest';
import {
    categorize,
    parseBacklog,
    extractDocTitle,
    binActivityByDay,
    projectLoopsGoals,
} from './parsers';

describe('categorize', () => {
    it('returns shipped when title starts with "Shipped"', () => {
        expect(categorize('Shipped (2026-03-28 session)')).toBe('shipped');
        expect(categorize('shipped items')).toBe('shipped');
    });

    it('returns active for Active / In Progress / In-Progress', () => {
        expect(categorize('Active Ideas')).toBe('active');
        expect(categorize('In Progress')).toBe('active');
        expect(categorize('In-Progress Items')).toBe('active');
    });

    it('returns backlog by default', () => {
        expect(categorize('New Ideas')).toBe('backlog');
        expect(categorize('Ear Training & Recognition')).toBe('backlog');
        expect(categorize('')).toBe('backlog');
    });

    it('matches "shipped" after a space boundary inside the title', () => {
        // "Recently shipped" → shipped via the / shipped\b/ branch.
        expect(categorize('Recently shipped')).toBe('shipped');
    });

    it('matches "(shipped" parenthetical marker', () => {
        // Some real BACKLOG.md titles use "Foo (shipped 2026-03-28)".
        expect(categorize('Foo (shipped 2026-03-28)')).toBe('shipped');
    });

    it('is case-insensitive', () => {
        expect(categorize('SHIPPED')).toBe('shipped');
        expect(categorize('ACTIVE')).toBe('active');
    });
});

describe('parseBacklog', () => {
    it('returns zero epics for empty input', () => {
        const p = parseBacklog('');
        expect(p.total_epics).toBe(0);
        expect(p.total_items).toBe(0);
        expect(p.epics).toEqual([]);
    });

    it('parses a single epic with shipped + active + backlog sub-sections', () => {
        const md = [
            '# Project',
            '## My Epic',
            '### Shipped',
            '- one',
            '- two',
            '### Active',
            '- three',
            '### New Ideas',
            '- four',
            '- five',
            '- six',
        ].join('\n');
        const p = parseBacklog(md);
        expect(p.total_epics).toBe(1);
        const epic = p.epics[0];
        expect(epic.title).toBe('My Epic');
        expect(epic.total_items).toBe(6);
        expect(epic.shipped).toBe(2);
        expect(epic.active).toBe(1);
        expect(epic.backlog).toBe(3);
        expect(epic.progress_pct).toBe(33); // 2/6 = 33.33 → 33
        expect(epic.sub_sections).toHaveLength(3);
    });

    it('tolerates CRLF line endings (regression: gatherBacklog initially returned 0)', () => {
        // Same content as above but with Windows line endings.
        const md = [
            '# Project',
            '## My Epic',
            '### Shipped',
            '- one',
            '### New',
            '- two',
        ].join('\r\n');
        const p = parseBacklog(md);
        expect(p.total_epics).toBe(1);
        expect(p.epics[0].title).toBe('My Epic');
        expect(p.epics[0].shipped).toBe(1);
        expect(p.epics[0].backlog).toBe(1);
    });

    it('inherits H2 category for bullets that appear before any H3 (regression: code-reviewer #2)', () => {
        // An epic titled "Shipped Q1" with direct bullets and no H3 should
        // count those bullets as shipped — not silently as backlog.
        const md = [
            '## Shipped Q1',
            '- one',
            '- two',
        ].join('\n');
        const p = parseBacklog(md);
        expect(p.epics[0].shipped).toBe(2);
        expect(p.epics[0].backlog).toBe(0);
        expect(p.epics[0].progress_pct).toBe(100);
    });

    it('rolls untagged H2 bullets into backlog (default category)', () => {
        const md = [
            '## Random Epic',
            '- one',
            '- two',
        ].join('\n');
        const p = parseBacklog(md);
        expect(p.epics[0].shipped).toBe(0);
        expect(p.epics[0].backlog).toBe(2);
        expect(p.epics[0].progress_pct).toBe(0);
    });

    it('counts bullets with - and * markers identically', () => {
        const md = [
            '## Epic',
            '### Items',
            '- a',
            '* b',
            '- c',
        ].join('\n');
        const p = parseBacklog(md);
        expect(p.epics[0].total_items).toBe(3);
    });

    it('ignores bullets that appear before the first H2', () => {
        const md = [
            '- not in any epic',
            '## Real Epic',
            '### Active',
            '- counted',
        ].join('\n');
        const p = parseBacklog(md);
        expect(p.total_items).toBe(1);
        expect(p.epics[0].title).toBe('Real Epic');
    });

    it('aggregates overall_progress_pct across multiple epics', () => {
        const md = [
            '## E1',
            '### Shipped',
            '- a',
            '- b',
            '## E2',
            '### Backlog',
            '- c',
            '- d',
        ].join('\n');
        const p = parseBacklog(md);
        expect(p.total_items).toBe(4);
        expect(p.total_shipped).toBe(2);
        expect(p.overall_progress_pct).toBe(50);
    });

    it('preserves legacy top_sections shape for back-compat', () => {
        const md = '## E1\n### Active\n- a\n## E2\n### Shipped\n- b\n- c';
        const p = parseBacklog(md);
        expect(p.total_sections).toBe(2);
        expect(p.top_sections).toEqual([
            { title: 'E1', item_count: 1 },
            { title: 'E2', item_count: 2 },
        ]);
    });

    it('caps top_sections at 8 epics for the legacy view', () => {
        const epics = Array.from({ length: 12 }, (_, i) => `## Epic ${i}\n### Active\n- item`).join('\n');
        const p = parseBacklog(epics);
        expect(p.total_epics).toBe(12);
        expect(p.top_sections).toHaveLength(8);
    });
});

describe('extractDocTitle', () => {
    it('prefers the first H1 heading', () => {
        const md = '# The Real Title\n\nSome body text';
        expect(extractDocTitle(md)).toBe('The Real Title');
    });

    it('strips H1 marker whitespace', () => {
        expect(extractDocTitle('#    Spaced Title')).toBe('Spaced Title');
    });

    it('falls back to YAML frontmatter title: when no H1 in first 15 lines', () => {
        const md = '---\ntitle: Frontmatter Title\ndate: 2026-01-01\n---\n\nBody';
        expect(extractDocTitle(md)).toBe('Frontmatter Title');
    });

    it('strips quotes from frontmatter title', () => {
        expect(extractDocTitle('title: "Quoted Title"')).toBe('Quoted Title');
        expect(extractDocTitle("title: 'Single Quoted'")).toBe('Single Quoted');
    });

    it('returns null when neither H1 nor title: is present', () => {
        expect(extractDocTitle('Just some prose without a heading.')).toBeNull();
    });

    it('tolerates CRLF', () => {
        expect(extractDocTitle('# Heading\r\nBody')).toBe('Heading');
    });

    it('only scans the first 15 lines', () => {
        const md = Array.from({ length: 20 }, () => 'filler line').join('\n') + '\n# Late Heading';
        expect(extractDocTitle(md)).toBeNull();
    });
});

describe('binActivityByDay', () => {
    const fixedNow = new Date('2026-05-23T00:00:00Z');

    it('returns an entry for every day in the window', () => {
        const bins = binActivityByDay([], 7, fixedNow);
        expect(bins).toHaveLength(7);
        // First entry should be 6 days ago, last entry should be today.
        expect(bins[0].date).toBe('2026-05-17');
        expect(bins[bins.length - 1].date).toBe('2026-05-23');
    });

    it('zero-fills days with no activity', () => {
        const bins = binActivityByDay(['2026-05-22T10:00:00Z'], 7, fixedNow);
        const map = Object.fromEntries(bins.map((b) => [b.date, b.count]));
        expect(map['2026-05-22']).toBe(1);
        expect(map['2026-05-21']).toBe(0);
        expect(map['2026-05-20']).toBe(0);
    });

    it('counts multiple commits on the same day', () => {
        const ts = ['2026-05-23T01:00:00Z', '2026-05-23T05:00:00Z', '2026-05-23T23:59:59Z'];
        const bins = binActivityByDay(ts, 7, fixedNow);
        const today = bins.find((b) => b.date === '2026-05-23');
        expect(today?.count).toBe(3);
    });

    it('drops timestamps outside the window', () => {
        const ts = ['2025-01-01T00:00:00Z', '2026-05-23T12:00:00Z'];
        const bins = binActivityByDay(ts, 7, fixedNow);
        const total = bins.reduce((s, b) => s + b.count, 0);
        expect(total).toBe(1);
    });

    it('ignores empty / null timestamp strings safely', () => {
        const bins = binActivityByDay(['', '2026-05-23T00:00:00Z'], 7, fixedNow);
        const total = bins.reduce((s, b) => s + b.count, 0);
        expect(total).toBe(1);
    });
});

describe('projectLoopsGoals', () => {
    const now = new Date('2026-05-24T12:00:00Z');

    it('returns empty buckets for no lines', () => {
        const p = projectLoopsGoals([], now);
        expect(p.active_loops).toEqual([]);
        expect(p.active_goals).toEqual([]);
        expect(p.completed_recent).toEqual([]);
        expect(p.total_records).toBe(0);
    });

    it('skips blank lines + malformed JSON without throwing', () => {
        const lines = [
            '',
            '   ',
            '{not valid json',
            '{"id":"x"}',                              // missing kind+status — drop
            '{"id":"y","kind":"loop","status":"active","started_at":"2026-05-24T10:00:00Z","last_activity_at":"2026-05-24T10:00:00Z","session_id":"s1","prompt_or_condition":"keep","turn_count":0}',
        ];
        const p = projectLoopsGoals(lines, now);
        expect(p.active_loops.length).toBe(1);
        expect(p.active_loops[0].id).toBe('y');
    });

    it('latest-line-per-id wins (turn event supersedes start event)', () => {
        const start = '{"id":"a1","kind":"loop","status":"active","started_at":"2026-05-24T10:00:00Z","last_activity_at":"2026-05-24T10:00:00Z","session_id":"s1","prompt_or_condition":"test","turn_count":0}';
        const turn  = '{"id":"a1","kind":"loop","status":"active","started_at":"2026-05-24T10:00:00Z","last_activity_at":"2026-05-24T11:30:00Z","session_id":"s1","prompt_or_condition":"test","turn_count":3}';
        const p = projectLoopsGoals([start, turn], now);
        expect(p.active_loops.length).toBe(1);
        expect(p.active_loops[0].turn_count).toBe(3);
        expect(p.active_loops[0].last_activity_at).toBe('2026-05-24T11:30:00Z');
    });

    it('separates loops from goals', () => {
        const loop = '{"id":"L1","kind":"loop","status":"active","started_at":"2026-05-24T10:00:00Z","last_activity_at":"2026-05-24T11:00:00Z","session_id":"s","prompt_or_condition":"x","turn_count":1}';
        const goal = '{"id":"G1","kind":"goal","status":"active","started_at":"2026-05-24T10:00:00Z","last_activity_at":"2026-05-24T11:00:00Z","session_id":"s","prompt_or_condition":"y","turn_count":1}';
        const p = projectLoopsGoals([loop, goal], now);
        expect(p.active_loops.map((r) => r.id)).toEqual(['L1']);
        expect(p.active_goals.map((r) => r.id)).toEqual(['G1']);
    });

    it('drops archived records, surfaces completed ones < cutoff', () => {
        const archived = '{"id":"old","kind":"loop","status":"archived","started_at":"2026-05-01T00:00:00Z","last_activity_at":"2026-05-01T00:00:00Z","session_id":"s","prompt_or_condition":"old","turn_count":99}';
        const completedRecent = '{"id":"done","kind":"goal","status":"completed","started_at":"2026-05-23T00:00:00Z","last_activity_at":"2026-05-23T12:00:00Z","session_id":"s","prompt_or_condition":"finished","turn_count":5}';
        const completedStale  = '{"id":"stale","kind":"goal","status":"completed","started_at":"2026-05-10T00:00:00Z","last_activity_at":"2026-05-10T00:00:00Z","session_id":"s","prompt_or_condition":"forgotten","turn_count":12}';
        const p = projectLoopsGoals([archived, completedRecent, completedStale], now, 7);
        expect(p.active_loops).toEqual([]);
        expect(p.active_goals).toEqual([]);
        expect(p.completed_recent.map((r) => r.id)).toEqual(['done']);
    });

    it('decorates active rows with age + last_activity_min_ago', () => {
        const rec = '{"id":"d1","kind":"loop","status":"active","started_at":"2026-05-24T10:00:00Z","last_activity_at":"2026-05-24T11:45:00Z","session_id":"s","prompt_or_condition":"hi","turn_count":2}';
        const p = projectLoopsGoals([rec], now);
        expect(p.active_loops[0].age_min).toBe(120);
        expect(p.active_loops[0].last_activity_min_ago).toBe(15);
    });

    it('sorts active rows newest-activity-first', () => {
        const a = '{"id":"A","kind":"loop","status":"active","started_at":"2026-05-24T08:00:00Z","last_activity_at":"2026-05-24T08:30:00Z","session_id":"s","prompt_or_condition":"a","turn_count":0}';
        const b = '{"id":"B","kind":"loop","status":"active","started_at":"2026-05-24T09:00:00Z","last_activity_at":"2026-05-24T11:00:00Z","session_id":"s","prompt_or_condition":"b","turn_count":0}';
        const c = '{"id":"C","kind":"loop","status":"active","started_at":"2026-05-24T07:00:00Z","last_activity_at":"2026-05-24T10:00:00Z","session_id":"s","prompt_or_condition":"c","turn_count":0}';
        const p = projectLoopsGoals([a, b, c], now);
        expect(p.active_loops.map((r) => r.id)).toEqual(['B', 'C', 'A']);
    });

    it('paused records appear in the active bucket (operator action queued)', () => {
        const paused = '{"id":"p1","kind":"goal","status":"paused","started_at":"2026-05-24T10:00:00Z","last_activity_at":"2026-05-24T11:00:00Z","session_id":"s","prompt_or_condition":"hold","turn_count":4}';
        const p = projectLoopsGoals([paused], now);
        expect(p.active_goals.length).toBe(1);
        expect(p.active_goals[0].status).toBe('paused');
    });
});
