// maintainGate.test — covers the pure parser + freshness derivation the
// maintain-gate tile (ga#446) relies on. These are the green-but-dead guards:
// a missing/old/unparseable snapshot must NEVER read as fresh-green.

import { describe, it, expect } from 'vitest';
import { readFileSync } from 'node:fs';
import path from 'node:path';
import { parseMaintainGate, maintainAgeHours, isMaintainStale } from './parsers';

// The real federated snapshot committed to the repo — proves the parser
// matches the production envelope shape, not a synthetic input.
const LIVE = readFileSync(
  path.resolve(__dirname, '../../../../state/quality/maintain-gate/last.json'),
  'utf-8',
);

describe('parseMaintainGate', () => {
  it('parses the real federated last.json snapshot', () => {
    const snap = parseMaintainGate(LIVE);
    expect(snap).not.toBeNull();
    expect(snap!.domain).toBe('maintain-gate');
    expect(snap!.status).toBe('U');
    expect(snap!.decision).toBe('escalate');
    expect(snap!.advisory).toBe(true);
    expect(snap!.oracle_status).toBe('warn');
    expect(snap!.signals.map((s) => s.lens)).toEqual(['metric', 'guardrail', 'convergence', 'drift']);
    expect(snap!.signals.every((s) => s.status === 'unknown')).toBe(true);
  });

  it('returns null on malformed JSON (no throw)', () => {
    expect(parseMaintainGate('{not json')).toBeNull();
  });

  it('returns null when the load-bearing contract fields are absent', () => {
    expect(parseMaintainGate(JSON.stringify({ foo: 1 }))).toBeNull();
  });

  it('defaults advisory to true (fail-safe to non-binding) when omitted', () => {
    const snap = parseMaintainGate(JSON.stringify({
      status: 'T', emitted_at: '2026-06-21T00:00:00Z', oracle_status: 'ok',
    }));
    expect(snap!.advisory).toBe(true);
  });

  it('honors advisory:false when the producer marks it gating', () => {
    const snap = parseMaintainGate(JSON.stringify({
      status: 'T', emitted_at: '2026-06-21T00:00:00Z', oracle_status: 'ok', advisory: false,
    }));
    expect(snap!.advisory).toBe(false);
  });
});

describe('maintainAgeHours', () => {
  it('returns null for missing or unparseable timestamps', () => {
    expect(maintainAgeHours(null)).toBeNull();
    expect(maintainAgeHours('not-a-date')).toBeNull();
  });

  it('computes hours elapsed', () => {
    const now = new Date('2026-06-21T12:00:00Z');
    expect(maintainAgeHours('2026-06-21T10:00:00Z', now)).toBeCloseTo(2, 5);
  });
});

describe('isMaintainStale', () => {
  it('treats unknown freshness (null) as STALE — never green', () => {
    expect(isMaintainStale(null)).toBe(true);
  });

  it('fresh under the threshold, stale over it', () => {
    expect(isMaintainStale(2, 36)).toBe(false);
    expect(isMaintainStale(48, 36)).toBe(true);
  });
});
