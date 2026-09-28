import { describe, expect, it } from 'vitest';
import lifecyclePnml from './examples/petri-pipeline-lifecycle.pnml?raw';
import bufferPnml from './examples/petri-producer-consumer.pnml?raw';
import {
  analyzeArgs,
  canConnect,
  classifyDeadMarking,
  finalsWithout,
  netProblems,
  parsePnml,
  petriLayout,
  readReport,
  type Marking,
  type PetriNet,
} from './petriNet';

describe('finalsWithout', () => {
  it('drops a final that needs a token on a deleted place, and nothing else', () => {
    const finals: Marking[] = [{ done: 1, free: 1 }, { failed: 1, free: 1 }, { done: 1, spare: 0 }];
    expect(finalsWithout(finals, new Set(['failed']))).toEqual([{ done: 1, free: 1 }, { done: 1, spare: 0 }]);
    // A zero entry says nothing: it goes, and the final stays.
    expect(finalsWithout(finals, new Set(['spare', 't1']))).toEqual([{ done: 1, free: 1 }, { failed: 1, free: 1 }, { done: 1 }]);
    expect(finalsWithout(finals, new Set(['free']))).toEqual([{ done: 1, spare: 0 }]);
  });
});

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
  it('names failure and cancellation after their terminal places', () => {
    expect(classifyDeadMarking({ tokens: { failed: 1, producer_faulted: 1 }, witness: [] })).toBe('failure');
    expect(classifyDeadMarking({ tokens: { cancelled: 1 }, witness: [] })).toBe('cancelled');
    expect(classifyDeadMarking({ tokens: { q: 1 }, witness: [] })).toBe('deadlock');
  });

  it('never calls a marking a success from its place names alone', () => {
    expect(classifyDeadMarking({ tokens: { succeeded: 1 }, witness: [] })).toBe('unverified');
    expect(classifyDeadMarking({ tokens: { done1: 1, tested2: 1 }, witness: [] })).toBe('unverified');
  });

  // Two jobs share one capacity token (IX_PETRI_CHECKPOINT_PILOT, case B);
  // the model's one intended end is both jobs done and the capacity back.
  const finals = [{ cap: 1, done1: 1, done2: 1 }];
  const dead = (tokens: Record<string, number>) => classifyDeadMarking({ tokens, witness: [] }, finals);

  it('calls a success only a declared final marking', () => {
    expect(dead({ cap: 1, done1: 1, done2: 1 })).toBe('success');
    expect(dead({ done1: 1, done2: 1, cap: 1, overlap_seen: 0 })).toBe('success');
  });

  it('calls any other end a deadlock once finals are declared', () => {
    // Release keeps the capacity: job 2 tested, never admitted.
    expect(dead({ done1: 1, tested2: 1 })).toBe('deadlock');
    expect(dead({ done2: 1, tested1: 1 })).toBe('deadlock');
    // Codex review: job 2 never started, or the capacity is gone.
    expect(dead({ done1: 1, ready2: 1 })).toBe('deadlock');
    expect(dead({ done1: 1, done2: 1 })).toBe('deadlock');
    expect(dead({ cap: 2, done1: 1, done2: 1 })).toBe('deadlock');
  });
});

describe('final markings in PNML', () => {
  const pnml = (finals: string) => '<pnml><net id="n" type="http://www.pnml.org/version-2009/grammar/ptnet"><page id="g">'
    + '<place id="p"><initialMarking><text>1</text></initialMarking></place><place id="q"/>'
    + '<transition id="t"/><arc id="a1" source="p" target="t"/><arc id="a2" source="t" target="q"/></page>'
    + finals + '</net></pnml>';

  it('reads the final markings a net declares', () => {
    const net = parsePnml(pnml('<toolspecific tool="ga-pipeline-editor" version="1">'
      + '<finalMarking><token place="q" count="1"/></finalMarking></toolspecific>'));
    expect(net.places.map((p) => p.id)).toEqual(['p', 'q']);
    expect(net.finalMarkings).toEqual([{ q: 1 }]);
  });

  it('has none when the net declares none', () => {
    expect(parsePnml(pnml('')).finalMarkings).toEqual([]);
  });

  it('refuses a final marking over an unknown place or a bad count', () => {
    const final = (tok: string) => pnml(`<toolspecific tool="ga-pipeline-editor" version="1"><finalMarking>${tok}</finalMarking></toolspecific>`);
    expect(() => parsePnml(final('<token place="nope" count="1"/>'))).toThrow('unknown place "nope"');
    expect(() => parsePnml(final('<token place="q" count="-1"/>'))).toThrow('not a non-negative integer');
  });

  it('refuses an empty count or a place listed twice in a final marking', () => {
    const final = (tok: string) => pnml(`<toolspecific tool="ga-pipeline-editor" version="1"><finalMarking>${tok}</finalMarking></toolspecific>`);
    expect(() => parsePnml(final('<token place="q" count=""/>'))).toThrow('not a non-negative integer');
    expect(() => parsePnml(final('<token place="q" count=" "/>'))).toThrow('not a non-negative integer');
    expect(() => parsePnml(final('<token place="q" count="1"/><token place="q" count="2"/>')))
      .toThrow('place "q" is listed twice');
    expect(parsePnml(final('<token place="q"/>')).finalMarkings).toEqual([{ q: 1 }]);
  });

  it('refuses a final count too large to hold exactly', () => {
    const final = (tok: string) => pnml(`<toolspecific tool="ga-pipeline-editor" version="1"><finalMarking>${tok}</finalMarking></toolspecific>`);
    expect(() => parsePnml(final(`<token place="q" count="${'9'.repeat(400)}"/>`))).toThrow('not a non-negative integer');
    expect(() => parsePnml(final('<token place="q" count="9007199254740992"/>'))).toThrow('not a non-negative integer');
    expect(parsePnml(final('<token place="q" count="9007199254740991"/>')).finalMarkings).toEqual([{ q: 9007199254740991 }]);
  });

  it('declares the lesson 14 lifecycle ends', () => {
    const net = parsePnml(lifecyclePnml);
    expect(net.finalMarkings).toHaveLength(3);
  });
});
