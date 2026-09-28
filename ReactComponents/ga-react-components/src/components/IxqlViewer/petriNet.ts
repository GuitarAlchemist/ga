// Pure model for the Petri-net editor: a Place/Transition net, its PNML
// form, the `ix_petri_analyze` arguments and a readable report.
//
// The net is a specification oracle, not the execution: it enumerates the
// markings of a deliberately small lifecycle so a runtime test knows which
// terminal states to tell apart (learn, csharp-advanced lesson 17).

export interface PetriPlace {
  id: string;
  tokens: number;
}

export interface PetriArc {
  source: string;
  target: string;
  weight: number;
}

/** Tokens per place; a place left out holds none. */
export type Marking = Record<string, number>;

export interface PetriNet {
  name: string;
  places: PetriPlace[];
  transitions: string[];
  arcs: PetriArc[];
  /**
   * The ends the net is meant to reach, when it declares them (PNML
   * `<toolspecific tool="ga-pipeline-editor">` → `<finalMarking>` →
   * `<token place count>`). Only these are called a success.
   */
  finalMarkings?: Marking[];
}

/** Reads a PNML Place/Transition net (ISO/IEC 15909-2). Throws on anything else. */
export function parsePnml(xml: string): PetriNet {
  const doc = new DOMParser().parseFromString(xml, 'application/xml');
  if (doc.getElementsByTagName('parsererror').length > 0) throw new Error('not well-formed XML');
  const net = doc.getElementsByTagName('net')[0];
  if (!net) throw new Error('no <net> element');
  const type = net.getAttribute('type') ?? '';
  if (!type.endsWith('/ptnet')) throw new Error(`not a Place/Transition net (type "${type}")`);
  const text = (el: Element | undefined, tag: string) =>
    el?.getElementsByTagName(tag)[0]?.getElementsByTagName('text')[0]?.textContent?.trim();
  const count = (raw: string | undefined, what: string) => {
    if (raw === undefined || raw === '') return undefined;
    const n = Number(raw);
    if (!Number.isInteger(n) || n < 0) throw new Error(`${what}: "${raw}" is not a non-negative integer`);
    return n;
  };
  const places = [...net.getElementsByTagName('place')].map((p) => ({
    id: p.getAttribute('id') ?? '',
    tokens: count(text(p, 'initialMarking'), `place ${p.getAttribute('id')}`) ?? 0,
  }));
  const transitions = [...net.getElementsByTagName('transition')].map((t) => t.getAttribute('id') ?? '');
  const arcs = [...net.getElementsByTagName('arc')].map((a) => ({
    source: a.getAttribute('source') ?? '',
    target: a.getAttribute('target') ?? '',
    weight: count(text(a, 'inscription'), `arc ${a.getAttribute('id')}`) ?? 1,
  }));
  const placeIds = new Set(places.map((p) => p.id));
  const finalMarkings = [...net.getElementsByTagName('toolspecific')]
    .filter((ts) => ts.getAttribute('tool') === 'ga-pipeline-editor')
    .flatMap((ts) => [...ts.getElementsByTagName('finalMarking')])
    .map((fm) => {
      // A final is the only success a net can declare, so a malformed one is
      // refused rather than read as some other marking.
      const seen = new Set<string>();
      return Object.fromEntries([...fm.getElementsByTagName('token')].map((tok) => {
        const place = tok.getAttribute('place') ?? '';
        if (!placeIds.has(place)) throw new Error(`final marking: unknown place "${place}"`);
        if (seen.has(place)) throw new Error(`final marking: place "${place}" is listed twice`);
        seen.add(place);
        const raw = tok.getAttribute('count') ?? '1';
        // Digits alone can still overflow to Infinity or round: the count must
        // come back exactly.
        const n = Number(raw);
        if (!/^\d+$/.test(raw) || !Number.isSafeInteger(n)) {
          throw new Error(`final marking of ${place}: "${raw}" is not a non-negative integer up to ${Number.MAX_SAFE_INTEGER}`);
        }
        return [place, n];
      }));
    });
  const result = { name: text(net, 'name') ?? net.getAttribute('id') ?? 'net', places, transitions, arcs, finalMarkings };
  const problem = netProblems(result)[0];
  if (problem) throw new Error(problem);
  return result;
}

/** Structural problems the analyzer would refuse; empty when the net is well formed. */
export function netProblems(net: PetriNet): string[] {
  const out: string[] = [];
  const places = new Set(net.places.map((p) => p.id));
  const transitions = new Set(net.transitions);
  const ids = [...net.places.map((p) => p.id), ...net.transitions];
  if (ids.some((id) => !id)) out.push('every place and transition needs an id');
  const dup = ids.find((id, i) => ids.indexOf(id) !== i);
  if (dup) out.push(`duplicate id "${dup}"`);
  for (const a of net.arcs) {
    const pt = places.has(a.source) && transitions.has(a.target);
    const tp = transitions.has(a.source) && places.has(a.target);
    if (!pt && !tp) out.push(`arc ${a.source} → ${a.target} must join a place and a transition`);
    if (!Number.isInteger(a.weight) || a.weight < 1) out.push(`arc ${a.source} → ${a.target}: weight must be ≥ 1`);
  }
  return out;
}

/** Whether an arc from `source` to `target` is allowed: place ↔ transition only. */
export function canConnect(net: PetriNet, source: string, target: string): boolean {
  const isPlace = (id: string) => net.places.some((p) => p.id === id);
  const isTransition = (id: string) => net.transitions.includes(id);
  return (isPlace(source) && isTransition(target)) || (isTransition(source) && isPlace(target));
}

/** The `ix_petri_analyze` arguments for a net (inline form). */
export function analyzeArgs(net: PetriNet): Record<string, unknown> {
  return {
    name: net.name,
    places: net.places.map((p) => ({ id: p.id, tokens: p.tokens })),
    transitions: net.transitions,
    arcs: net.arcs.map((a) => ({ source: a.source, target: a.target, weight: a.weight })),
  };
}

/**
 * Left-to-right layout: a node's column is its breadth-first distance from
 * the initially marked places, so the story reads from the starting tokens.
 * Unreachable nodes go in a last column; cycles cannot loop the walk.
 */
export function petriLayout(net: PetriNet, { dx = 170, dy = 110 } = {}): Record<string, { x: number; y: number }> {
  const next = new Map<string, string[]>();
  for (const a of net.arcs) next.set(a.source, [...(next.get(a.source) ?? []), a.target]);
  const all = [...net.places.map((p) => p.id), ...net.transitions];
  const roots = net.places.filter((p) => p.tokens > 0).map((p) => p.id);
  const col = new Map<string, number>(roots.map((id) => [id, 0]));
  const queue = [...roots];
  while (queue.length > 0) {
    const id = queue.shift()!;
    for (const n of next.get(id) ?? []) {
      if (!col.has(n)) {
        col.set(n, col.get(id)! + 1);
        queue.push(n);
      }
    }
  }
  const last = Math.max(0, ...col.values()) + 1;
  const rows = new Map<number, number>();
  const out: Record<string, { x: number; y: number }> = {};
  for (const id of all) {
    const c = col.get(id) ?? last;
    const r = rows.get(c) ?? 0;
    rows.set(c, r + 1);
    out[id] = { x: c * dx, y: r * dy };
  }
  return out;
}

export type Verdict = 'holds' | 'fails' | 'unknown';

export interface DeadMarking {
  /** Tokens per place in this terminal marking. */
  tokens: Record<string, number>;
  /** Shortest firing sequence from the initial marking. */
  witness: string[];
}

export interface PetriReport {
  states: number;
  truncated: boolean;
  properties: { key: string; label: string; verdict: Verdict; detail?: string }[];
  deadMarkings: DeadMarking[];
}

const PROPERTY_LABELS: [string, string][] = [
  ['bounded', 'bounded'],
  ['deadlock_free', 'deadlock-free'],
  ['live', 'live'],
  ['quasi_live', 'quasi-live (no dead transition)'],
  ['reversible', 'reversible'],
];

/** Reads the `ix_petri_analyze` result into what the editor shows. */
export function readReport(raw: unknown): PetriReport {
  const r = (raw ?? {}) as Record<string, unknown>;
  const prop = (key: string) => (r[key] ?? {}) as { verdict?: string; detail?: unknown };
  const properties = PROPERTY_LABELS.map(([key, label]) => {
    const p = prop(key);
    const verdict: Verdict = p.verdict === 'holds' || p.verdict === 'fails' ? p.verdict : 'unknown';
    let detail: string | undefined;
    if (key === 'bounded' && p.detail && typeof p.detail === 'object' && 'k' in p.detail) {
      detail = `k = ${(p.detail as { k: unknown }).k}`;
    } else if ((key === 'live' || key === 'quasi_live') && Array.isArray(p.detail) && p.detail.length > 0) {
      detail = `${key === 'live' ? 'not live' : 'dead'}: ${p.detail.join(', ')}`;
    }
    return { key, label, verdict, detail };
  });
  const dead = prop('deadlock_free').detail;
  const deadMarkings: DeadMarking[] = Array.isArray(dead)
    ? dead.map((d) => {
        const m = d as { tokens?: [string, number][]; witness?: string[] };
        return { tokens: Object.fromEntries(m.tokens ?? []), witness: m.witness ?? [] };
      })
    : [];
  return { states: Number(r.states ?? 0), truncated: r.truncated === true, properties, deadMarkings };
}

/**
 * Names a terminal marking after the terminal places it marks, as lesson 17
 * asks (success, failure, cancellation): a marked place whose name reads as
 * an outcome. Anything else is an unexplained deadlock.
 *
 * A success is never read from names: a marked `done1` says nothing of the
 * tokens stuck or missing elsewhere. Only a marking equal to one of the
 * net's declared `finals` is a success, and once the net declares any, every
 * other end is a deadlock. Without declared finals, an end that reads as a
 * success is `unverified`.
 */
export function classifyDeadMarking(
  m: DeadMarking,
  finals: Marking[] = [],
): 'success' | 'failure' | 'cancelled' | 'deadlock' | 'unverified' {
  const marked = Object.keys(m.tokens).filter((p) => m.tokens[p] > 0).map((p) => p.toLowerCase());
  if (marked.some((p) => /cancel/.test(p))) return 'cancelled';
  if (marked.some((p) => /fail|fault|error/.test(p))) return 'failure';
  if (finals.length > 0) return finals.some((f) => sameMarking(m.tokens, f)) ? 'success' : 'deadlock';
  if (marked.some((p) => /succe|done|complete/.test(p))) return 'unverified';
  return 'deadlock';
}

/** Whether two markings put the same tokens on every place. */
export function sameMarking(a: Marking, b: Marking): boolean {
  const places = new Set([...Object.keys(a), ...Object.keys(b)]);
  return [...places].every((p) => (a[p] ?? 0) === (b[p] ?? 0));
}
