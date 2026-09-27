import { describe, expect, it } from 'vitest';
import lifecyclePnml from './examples/petri-pipeline-lifecycle.pnml?raw';
import bufferPnml from './examples/petri-producer-consumer.pnml?raw';
import {
  analyzeArgs,
  canConnect,
  classifyDeadMarking,
  netProblems,
  parsePnml,
  petriLayout,
  readReport,
  type PetriNet,
} from './petriNet';

// `ix_petri_analyze` output captured from ix-mcp for the net
// p(1) -> t -> q: its one dead marking and witness.
const STUCK_REPORT = {
  bounded: { detail: { k: 1, per_place: [['p', 1], ['q', 1]] }, verdict: 'holds' },
  deadlock_count: 1,
  deadlock_free: { detail: [{ marking: 'q=1', state: 1, tokens: [['q', 1]], witness: ['t'] }], verdict: 'fails' },
  live: { detail: ['t'], verdict: 'fails' },
  quasi_live: { detail: [], verdict: 'holds' },
  reversible: { detail: null, verdict: 'fails' },
  states: 2,
  truncated: false,
};

describe('parsePnml', () => {
  it('reads the course bounded buffer (lesson 1)', () => {
    const net = parsePnml(bufferPnml);
    expect(net.places).toHaveLength(6);
    expect(net.transitions).toEqual(['produce', 'deposit', 'take', 'consume']);
    expect(net.arcs).toHaveLength(12);
    expect(net.places.find((p) => p.id === 'free')?.tokens).toBe(2);
    expect(net.arcs.every((a) => a.weight === 1)).toBe(true);
  });

  it('reads the pipeline lifecycle (lesson 14)', () => {
    const net = parsePnml(lifecyclePnml);
    expect(net.places.map((p) => p.id)).toContain('cancelled');
    expect(net.transitions).toContain('settle_success');
    expect(netProblems(net)).toEqual([]);
  });

  it('refuses what it cannot represent', () => {
    expect(() => parsePnml('<oops')).toThrow('well-formed');
    expect(() => parsePnml('<pnml><net id="n" type="x/coloured"/></pnml>')).toThrow('Place/Transition');
    const bad = '<pnml><net id="n" type="http://www.pnml.org/version-2009/grammar/ptnet"><page id="g">'
      + '<place id="a"/><place id="b"/><arc id="x" source="a" target="b"/></page></net></pnml>';
    expect(() => parsePnml(bad)).toThrow('must join a place and a transition');
  });
});

describe('net editing', () => {
  const net: PetriNet = {
    name: 'n',
    places: [{ id: 'p', tokens: 1 }, { id: 'q', tokens: 0 }],
    transitions: ['t'],
    arcs: [{ source: 'p', target: 't', weight: 1 }, { source: 't', target: 'q', weight: 1 }],
  };

  it('only joins a place and a transition', () => {
    expect(canConnect(net, 'p', 't')).toBe(true);
    expect(canConnect(net, 't', 'q')).toBe(true);
    expect(canConnect(net, 'p', 'q')).toBe(false);
    expect(canConnect(net, 't', 't')).toBe(false);
  });

  it('builds the analyzer arguments', () => {
    expect(analyzeArgs(net)).toEqual({
      name: 'n',
      places: [{ id: 'p', tokens: 1 }, { id: 'q', tokens: 0 }],
      transitions: ['t'],
      arcs: [{ source: 'p', target: 't', weight: 1 }, { source: 't', target: 'q', weight: 1 }],
    });
  });

  it('lays out from the marked places and survives cycles', () => {
    const pos = petriLayout(parsePnml(bufferPnml));
    expect(pos.ready.x).toBe(0);
    expect(pos.produce.x).toBeGreaterThan(pos.ready.x);
    expect(new Set(Object.values(pos).map((p) => `${p.x},${p.y}`)).size).toBe(10);
  });
});

describe('readReport', () => {
  it('reads verdicts and the dead marking with its witness', () => {
    const r = readReport(STUCK_REPORT);
    expect(r.states).toBe(2);
    expect(r.properties.find((p) => p.key === 'deadlock_free')?.verdict).toBe('fails');
    expect(r.properties.find((p) => p.key === 'bounded')?.detail).toBe('k = 1');
    expect(r.properties.find((p) => p.key === 'live')?.detail).toBe('not live: t');
    expect(r.deadMarkings).toEqual([{ tokens: { q: 1 }, witness: ['t'] }]);
  });

  it('treats a missing or odd verdict as unknown, never as holds', () => {
    expect(readReport({}).properties.every((p) => p.verdict === 'unknown')).toBe(true);
  });
});

describe('classifyDeadMarking', () => {
  it('names outcomes after their terminal places', () => {
    expect(classifyDeadMarking({ tokens: { succeeded: 1 }, witness: [] })).toBe('success');
    expect(classifyDeadMarking({ tokens: { failed: 1, producer_faulted: 1 }, witness: [] })).toBe('failure');
    expect(classifyDeadMarking({ tokens: { cancelled: 1 }, witness: [] })).toBe('cancelled');
    expect(classifyDeadMarking({ tokens: { q: 1 }, witness: [] })).toBe('deadlock');
  });
});
