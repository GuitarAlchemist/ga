import { describe, expect, it } from 'vitest';
import { JEV_MAX_PAYLOAD_BYTES, JEV_MODEL, buildAdvicePayload, readAdvice, usageCostUsd, type ProposalSummary } from './jevAdvice';

const summary: ProposalSummary = {
  title: 'Drop payload, add a stats probe',
  author: 'agent',
  based_on_current: true,
  added: [{ id: 'extra', tool: 'ix_stats', tier: 'tier1', effect: 'read_only' }],
  removed: [{ id: 'prime_radiant_payload', tool: 'ix_voicings_payload' }],
  changed: [],
  steps_before: 53,
  steps_after: 53,
};

const good = {
  model: JEV_MODEL,
  answers: {
    recommendation: { type: 'choice', choice: 'accept', confidence: 0.8, probabilities: { accept: 0.8, review: 0.15, reject: 0.05 } },
    risk: { type: 'score', score: 0.3, confidence: 0.7 },
    matches_title: { type: 'noul', noul: 0.9 },
  },
  usage: { input_tokens: 400, output_tokens: 30 },
};

describe('buildAdvicePayload', () => {
  it('sends a summary only, within the byte budget', () => {
    const body = JSON.stringify(buildAdvicePayload(summary));
    expect(body).not.toContain('arguments');
    expect(new TextEncoder().encode(body).length).toBeLessThan(JEV_MAX_PAYLOAD_BYTES);
  });

  it('caps long lists and says so', () => {
    const many = { ...summary, added: Array.from({ length: 60 }, (_, i) => ({ id: `s${i}`, tool: 'ix_stats' })) };
    const state = buildAdvicePayload(many).state as { added: unknown[]; truncated: boolean };
    expect(state.added).toHaveLength(20);
    expect(state.truncated).toBe(true);
    expect(new TextEncoder().encode(JSON.stringify(buildAdvicePayload(many))).length).toBeLessThan(JEV_MAX_PAYLOAD_BYTES);
  });
});

describe('readAdvice', () => {
  it('reads a well-formed answer', () => {
    expect(readAdvice(good)).toEqual({
      recommendation: 'accept', confidence: 0.8, risk: 0.3, matches_title: 0.9,
      usage: { input_tokens: 400, output_tokens: 30 },
    });
  });

  it('refuses anything off contract', () => {
    expect(() => readAdvice({ ...good, model: 'other' })).toThrow('pinned');
    expect(() => readAdvice({ ...good, answers: { ...good.answers, recommendation: { ...good.answers.recommendation, choice: 'merge' } } })).toThrow('choice');
    expect(() => readAdvice({ ...good, answers: { ...good.answers, risk: { type: 'score', score: 5 } } })).toThrow('score');
    expect(() => readAdvice({ ...good, usage: {} })).toThrow('usage');
  });

  it('prices usage with the lab proxy', () => {
    expect(usageCostUsd({ input_tokens: 1_000_000, output_tokens: 0 })).toBeCloseTo(0.042);
  });
});
