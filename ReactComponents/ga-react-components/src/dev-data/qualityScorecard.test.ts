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

import { describe, it, expect } from 'vitest';
import { existsSync, readdirSync, readFileSync } from 'node:fs';
import path from 'node:path';
import { classifyQualitySnapshot } from './parsers';
import type { QualitySnapshotClassification } from './parsers';

const QUALITY_DIR = path.resolve(__dirname, '../../../../state/quality');

// Frozen clocks. The tree is real, so a live `new Date()` would silently
// re-classify domains as their snapshots age past the staleness threshold and
// this guard would flip on a calendar boundary instead of on a code change.
//
// AUDIT_NOW is the instant the ix#244 baseline (3 regressions) was measured.
const AUDIT_NOW = new Date('2026-08-09T12:00:00Z');
// FRESH_NOW is a clock at which the maintain-gate snapshot (emitted
// 2026-07-20T10:37Z) is 0.6 days old, i.e. well inside the staleness window.
// It isolates rule A (advisory) from rule B (stale): only A can fire here.
const FRESH_NOW = new Date('2026-07-21T00:00:00Z');

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

    it('does not report an advisory, stale snapshot as a regression', () => {
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
        // At FRESH_NOW rule B cannot fire, so this pins rule A as independently
        // sufficient — it must not rot into a no-op hidden behind staleness.
        const { regressions, staleOrAdvisory } = classifyAll(entries, FRESH_NOW);
        const gate = staleOrAdvisory.find((e) => e.domain === 'maintain-gate');
        expect(gate).toBeDefined();
        expect(gate!.ageDays!).toBeLessThan(7);
        expect(gate!.kind).toBe('advisory');
        expect(regressions).not.toContain('maintain-gate: oracle_status=warn');
    });
});
