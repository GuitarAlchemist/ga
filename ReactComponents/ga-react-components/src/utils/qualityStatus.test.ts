// qualityStatus.test — covers the trip-wire matrix every QA tile rendering
// path relies on. If the chatbot-qa tile ever silently returns to red-on-no-
// data, one of these expectations breaks first.

import { describe, it, expect } from 'vitest';
import { deriveTileStatus } from './qualityStatus';

describe('deriveTileStatus', () => {
    it('returns unknown/default when snapshot is null', () => {
        expect(deriveTileStatus(null)).toEqual({
            color: 'default',
            label: 'unknown',
            headline: 'no data',
        });
    });

    it('respects explicit oracle_status="ok" (envelope contract)', () => {
        const t = deriveTileStatus({ oracle_status: 'ok' });
        expect(t.color).toBe('success');
        expect(t.label).toBe('ok');
    });

    it('respects explicit oracle_status="warn"', () => {
        const t = deriveTileStatus({ oracle_status: 'warn' });
        expect(t.color).toBe('warning');
        expect(t.label).toBe('warn');
    });

    it('treats any other explicit oracle_status as error', () => {
        const t = deriveTileStatus({ oracle_status: 'fail' });
        expect(t.color).toBe('error');
        expect(t.label).toBe('error');
        expect(t.headline).toContain('fail');
    });

    it('amber/degraded when degraded:true with a last-known-good (0..100 percent input)', () => {
        // last_known_good_pass_pct is emitted as 0..100 by run-prompt-corpus.ps1
        // (it multiplies baseline.primary_baseline by 100 at the producer).
        const t = deriveTileStatus({
            degraded: true,
            degraded_reason: 'backend_unavailable',
            last_known_good_pass_pct: 94.0,
        });
        expect(t.color).toBe('warning');
        expect(t.label).toBe('degraded');
        expect(t.headline).toContain('94%');
        expect(t.headline).toContain('backend_unavailable');
        // Guard against the prior bug (TestPlansCard pre-fix) that multiplied
        // the already-percent LKG by 100 → "9400%".
        expect(t.headline).not.toContain('9400');
    });

    it('amber/degraded when degraded:true without a last-known-good', () => {
        const t = deriveTileStatus({ degraded: true });
        expect(t.color).toBe('warning');
        expect(t.label).toBe('degraded');
        expect(t.headline).toContain('no last-known-good');
    });

    it('honest unknown (gray) when pass_pct is null and not degraded — NOT red', () => {
        const t = deriveTileStatus({ pass_pct: null });
        expect(t.color).toBe('default');
        expect(t.label).toBe('unknown');
        // Regression test for the original triage finding: null pass_pct must
        // not be coerced into a red tile.
        expect(t.color).not.toBe('error');
    });

    it('red/error when pass_pct < threshold (0..1 fraction)', () => {
        const t = deriveTileStatus({ pass_pct: 0.5 });
        expect(t.color).toBe('error');
        expect(t.label).toBe('error');
        expect(t.headline).toContain('50%');
    });

    it('green/ok when pass_pct >= threshold (0..1 fraction)', () => {
        const t = deriveTileStatus({ pass_pct: 0.94 });
        expect(t.color).toBe('success');
        expect(t.label).toBe('ok');
        expect(t.headline).toContain('94%');
    });

    it('older snapshots without degraded/last_known_good still parse (backward compat)', () => {
        // Pre-#327 snapshot: just pass_pct + oracle_status, no degraded field.
        const t = deriveTileStatus({ oracle_status: 'ok', pass_pct: 0.91 });
        expect(t.label).toBe('ok');
    });

    it('oracle_status wins over degraded — explicit envelope contract', () => {
        const t = deriveTileStatus({
            oracle_status: 'ok',
            degraded: true,
            last_known_good_pass_pct: 50,
        });
        expect(t.label).toBe('ok');
        expect(t.color).toBe('success');
    });
});
