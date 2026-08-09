// qualityScorecard.test — integration guard for the published quality
// scorecard (`gatherQuality()` in vite.config.ts, served at /dev-data/quality
// and /dev-data/manifest).
//
// ix#244: the scorecard promoted every non-ok `oracle_status` straight into
// `regressions[]`. The maintain-gate snapshot self-declares `advisory: true`
// and its producer has been skipping since 2026-07-20, so a non-binding,
// long-dead verdict was published as a live regression alongside two real
// ones. This suite reads the REAL committed `state/quality/` tree — the same
// pattern maintainGate.test.ts uses — and pins the reclassification:
//   before: regressions = 3, no stale_or_advisory collection
//   after:  regressions = 2, stale_or_advisory = [maintain-gate]
// with set-equality between the two, so nothing is dropped, only relabelled.
//
// NOTE: asserting against real committed data is deliberate (it is what makes
// this a reproduction of the reported defect rather than a synthetic fixture),
// and it couples the expected counts to the tree. When a producer's committed
// verdict changes, the expectations below change with it.
//
// NOTE: `legacyRegressions()` models the `oracle_status` producer only — the
// one this change classifies. `gatherQuality()` has a second, unclassified
// producer of `regressions[]` (the prior-vs-latest `metric_value` comparison,
// vite.config.ts:127-137), which emits nothing on today's tree. So the
// set-equality guard below proves the CLASSIFIER is loss-free, not that the
// whole published `regressions[]` is set-preserved.

import { describe, it, expect } from 'vitest';
import { existsSync, readdirSync, readFileSync } from 'node:fs';
import path from 'node:path';
import { classifyQualitySnapshot } from './parsers';
import type { QualitySnapshotClassification } from './parsers';

const QUALITY_DIR = path.resolve(__dirname, '../../../../state/quality');
const VITE_CONFIG = path.resolve(__dirname, '../../vite.config.ts');

// Frozen clocks. The tree is real, so a live `new Date()` would make the
// reported ages drift and the assertions below flip on a calendar boundary
// instead of on a code change.
//
// AUDIT_NOW is the instant the ix#244 baseline (3 regressions) was measured.
const AUDIT_NOW = new Date('2026-08-09T12:00:00Z');
// FRESH_NOW is a clock at which the maintain-gate snapshot (emitted
// 2026-07-20T10:37Z) is 0.6 days old. It pins that the demotion comes from
// `advisory: true` alone and owes nothing to the snapshot being old.
const FRESH_NOW = new Date('2026-07-21T00:00:00Z');
// The exact instant readme-drift's committed snapshot (emitted
// 2026-08-03T11:21:18.864Z) turns 7 days old — the boundary at which a fixed
// 7-day staleness rule would have demoted a genuine red verdict.
const CADENCE_BOUNDARY_NOW = new Date('2026-08-10T11:21:19Z');

interface TreeEntry { domain: string; data: Record<string, unknown> }

/**
 * Mirrors the snapshot selection in `gatherQuality()` (vite.config.ts:106-142):
 * `last.json` when present, otherwise the newest `YYYY-MM-DD.json`. Only the
 * *selection* is duplicated here; the classification — the behaviour under
 * test — is imported from the shared seam.
 */
function readQualityTree(): TreeEntry[] {
    const out: TreeEntry[] = [];
    for (const entry of readdirSync(QUALITY_DIR, { withFileTypes: true })) {
        if (!entry.isDirectory()) continue;
        const subDir = path.join(QUALITY_DIR, entry.name);
        const lastJson = path.join(subDir, 'last.json');
        let raw: string | null = null;
        if (existsSync(lastJson)) {
            raw = readFileSync(lastJson, 'utf-8');
        } else {
            const dated = readdirSync(subDir).filter((f) => /^\d{4}-\d{2}-\d{2}\.json$/.test(f)).sort();
            const latest = dated[dated.length - 1];
            if (latest) raw = readFileSync(path.join(subDir, latest), 'utf-8');
        }
        if (raw === null) continue;
        try {
            out.push({ domain: entry.name, data: JSON.parse(raw) as Record<string, unknown> });
        } catch {
            out.push({ domain: entry.name, data: { error: 'parse_failed' } });
        }
    }
    return out;
}

/** The pre-change rule, verbatim from vite.config.ts:145-146 at 7aa1695. */
function legacyRegressions(entries: TreeEntry[]): string[] {
    const out: string[] = [];
    for (const { domain, data } of entries) {
        const status = data.oracle_status;
        if (status && status !== 'ok') out.push(`${domain}: oracle_status=${status}`);
    }
    return out;
}

/** The post-change split, exactly as gatherQuality() routes it. */
function classifyAll(entries: TreeEntry[], now: Date) {
    const regressions: string[] = [];
    const staleOrAdvisory: Array<{ domain: string } & QualitySnapshotClassification> = [];
    for (const { domain, data } of entries) {
        const verdict = classifyQualitySnapshot(domain, data, now);
        if (verdict.kind === 'regression') regressions.push(verdict.label);
        else if (verdict.kind !== 'ok') staleOrAdvisory.push({ domain, ...verdict });
    }
    return { regressions, staleOrAdvisory };
}

describe('published quality scorecard', () => {
    const entries = readQualityTree();

    it('reads the real committed state/quality tree', () => {
        const domains = entries.map((e) => e.domain);
        expect(domains).toContain('maintain-gate');
        expect(domains).toContain('embeddings');
        expect(domains).toContain('readme-drift');
    });

    it('the pre-change rule reports 3 regressions (the ix#244 baseline)', () => {
        expect(legacyRegressions(entries)).toEqual([
            'embeddings: oracle_status=warn',
            'maintain-gate: oracle_status=warn',
            'readme-drift: oracle_status=error',
        ]);
    });

    it('does not report the advisory snapshot as a regression', () => {
        const { regressions, staleOrAdvisory } = classifyAll(entries, AUDIT_NOW);
        expect(regressions).not.toContain('maintain-gate: oracle_status=warn');
        expect(staleOrAdvisory.map((e) => e.domain)).toEqual(['maintain-gate']);
        expect(regressions).toHaveLength(2);
    });

    it('keeps the two live regressions untouched', () => {
        const { regressions } = classifyAll(entries, AUDIT_NOW);
        expect(regressions).toEqual([
            'embeddings: oracle_status=warn',
            'readme-drift: oracle_status=error',
        ]);
    });

    it('records the demoted entry as advisory, with its real age and label', () => {
        const { staleOrAdvisory } = classifyAll(entries, AUDIT_NOW);
        expect(staleOrAdvisory).toHaveLength(1);
        expect(staleOrAdvisory[0].kind).toBe('advisory');
        expect(staleOrAdvisory[0].label).toBe('maintain-gate: oracle_status=warn');
        expect(staleOrAdvisory[0].ageDays).not.toBeNull();
        expect(staleOrAdvisory[0].ageDays!).toBeGreaterThan(20);
    });

    // The §5.2 guardrail: /dev-data/quality is a PUBLISHED surface with four
    // rendering consumers. Reclassify, never silently drop.
    it('loses nothing — regressions ∪ stale_or_advisory equals the pre-change set', () => {
        const { regressions, staleOrAdvisory } = classifyAll(entries, AUDIT_NOW);
        const after = [...new Set([...regressions, ...staleOrAdvisory.map((e) => e.label)])].sort();
        const before = [...new Set(legacyRegressions(entries))].sort();
        expect(after).toEqual(before);
    });

    it('advisory alone demotes it, even when the snapshot is fresh', () => {
        // The demotion must come from `advisory: true`, not from the snapshot
        // happening to be old. At FRESH_NOW it is 0.6 days old and still moves.
        const { regressions, staleOrAdvisory } = classifyAll(entries, FRESH_NOW);
        const gate = staleOrAdvisory.find((e) => e.domain === 'maintain-gate');
        expect(gate).toBeDefined();
        expect(gate!.ageDays!).toBeLessThan(1);
        expect(gate!.kind).toBe('advisory');
        expect(regressions).not.toContain('maintain-gate: oracle_status=warn');
    });

    // ── Time stability ──────────────────────────────────────────────────
    // readme-drift emits weekly (.github/workflows/readme-drift-sensor.yml,
    // Mondays 08:00 UTC) and its newest committed snapshot is a genuine
    // `oracle_status=error`. A fixed 7-day staleness rule would demote it —
    // out of the only array any consumer renders — the moment it turned 7
    // days old, and every week thereafter that the producer ran late or was
    // skipped. This is the guard for that: classification depends on the
    // snapshot's fields, never on the wall clock.
    it('keeps readme-drift a live regression at the 7-day cadence boundary', () => {
        const { regressions, staleOrAdvisory } = classifyAll(entries, CADENCE_BOUNDARY_NOW);
        const drift = entries.find((e) => e.domain === 'readme-drift')!;
        // Confirm the clock really is past the boundary, so this is not a
        // vacuous assertion if the committed snapshot is ever refreshed.
        expect(classifyQualitySnapshot('readme-drift', drift.data, CADENCE_BOUNDARY_NOW).ageDays!).toBeGreaterThan(7);
        expect(regressions).toContain('readme-drift: oracle_status=error');
        expect(staleOrAdvisory.map((e) => e.domain)).toEqual(['maintain-gate']);
    });

    it('publishes the same split however far the clock advances', () => {
        const baseline = classifyAll(entries, AUDIT_NOW);
        for (const later of ['2026-08-10T11:21:19Z', '2026-09-15T12:00:00Z', '2027-06-01T12:00:00Z']) {
            const { regressions, staleOrAdvisory } = classifyAll(entries, new Date(later));
            expect(regressions).toEqual(baseline.regressions);
            expect(staleOrAdvisory.map((e) => `${e.domain}/${e.kind}`))
                .toEqual(baseline.staleOrAdvisory.map((e) => `${e.domain}/${e.kind}`));
        }
    });

    // ── Published key naming ────────────────────────────────────────────
    // `/dev-data/quality` and `/dev-data/manifest` are published surfaces that
    // agent readers fetch; every multi-word key they emit is snake_case
    // (`generated_at`, `eta_minutes`, `age_hours` on the sibling maintain-gate
    // route). The internal classification field is camelCase `ageDays`, so the
    // payload boundary in gatherQuality() must rename it. No test imports
    // vite.config.ts (GA #643), so this guards the real file as source text.
    it('publishes the age field as age_days, not ageDays, on both surfaces', () => {
        const src = readFileSync(VITE_CONFIG, 'utf-8');
        const gatherQuality = src.slice(src.indexOf('function gatherQuality('), src.indexOf('interface ActivityEntry'));
        expect(gatherQuality).toContain('age_days: verdict.ageDays');
        expect(gatherQuality).not.toMatch(/\bageDays\s*:/);
        // Both published surfaces embed the whole gatherQuality() return
        // object, so neither can omit or rename the key independently.
        expect(src).toContain('...gatherQuality()');
        expect(src).toContain('quality: gatherQuality(),');
    });
});
