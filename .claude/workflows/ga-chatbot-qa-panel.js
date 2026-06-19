export const meta = {
  name: 'ga-chatbot-qa-panel',
  description: 'Run the GA chatbot prompt corpus through a multi-lens semantic judge panel with hexavalent consensus',
  whenToUse:
    'Daily/local semantic QA of the GA chatbot. Complements (does NOT replace) the deterministic ' +
    'PromptCorpusTests.cs invariant gate: this layer judges correctness, grounding, and helpfulness ' +
    'that substring checks cannot see. Requires the live GaChatbot.Api backend (default :5252).',
  phases: [
    { title: 'Probe', detail: 'confirm backend live + discover chat endpoint/request shape; load corpus' },
    { title: 'Evaluate', detail: 'per prompt: query live chatbot, then judge the answer with N lenses + consensus' },
    { title: 'Synthesize', detail: 'aggregate hexavalent verdicts → write semantic snapshot JSON' },
  ],
}

// ── Config (override via Workflow args: { host, limit }) ──────────────────────
// host  : GaChatbot.Api base URL (default http://localhost:5252)
// limit : cap the number of (non-skipped) prompts evaluated — for smoke runs.
const HOST = (args && args.host) || 'http://localhost:5252'
const LIMIT = args && Number.isFinite(args.limit) ? args.limit : null
const CORPUS = 'Tests/Apps/GaChatbot.Api.Tests/Corpus/prompts.yaml'
const SNAPSHOT_DIR = 'state/quality/chatbot-qa-semantic'

// The judge panel: each lens is an INDEPENDENT reviewer with a distinct failure
// mode it is responsible for catching. Diversity > redundancy — three identical
// judges only catch what one catches.
const LENSES = [
  {
    key: 'musical-correctness',
    brief:
      'You are a music-theory expert. Judge ONLY whether the music theory in the answer is FACTUALLY CORRECT ' +
      '(intervals, scale/mode spellings, chord tones, key relationships, voicings). Ignore style and length. ' +
      'A confidently-stated wrong interval or misspelled mode is F. A correct but terse answer is still T.',
  },
  {
    key: 'grounding-hallucination',
    brief:
      'You are a hallucination auditor. Judge ONLY whether every SPECIFIC claim is grounded and non-fabricated: ' +
      'invented chord/voicing names, made-up fret numbers, fake citations, or "returned no matches"/"not yet ' +
      'implemented"/leaked SKILL.md preamble are F (or C if the answer also asserts the opposite elsewhere). ' +
      'A correctly-hedged "I don\'t have that" is U, not F.',
  },
  {
    key: 'completeness-helpfulness',
    brief:
      'You are a guitarist user. Judge ONLY whether the answer actually ANSWERS the asked question at useful ' +
      'depth. Off-topic, evasive, or stub answers are F. Correct but missing the obvious follow-the-question ' +
      'detail is P. Fully useful is T.',
  },
]

// ── Schemas (force structured agent output) ───────────────────────────────────
const PROBE_SCHEMA = {
  type: 'object',
  required: ['healthy', 'endpoint', 'requestTemplate', 'answerField'],
  properties: {
    healthy: { type: 'boolean', description: 'true only if the chat endpoint returned a real answer to a probe request' },
    endpoint: { type: 'string', description: 'full chat URL actually used, e.g. http://localhost:5252/api/chatbot/chat' },
    requestTemplate: { type: 'string', description: 'JSON request body with the literal token {{PROMPT}} where the user text goes' },
    answerField: { type: 'string', description: 'response JSON field holding the natural-language answer (e.g. naturalLanguageAnswer)' },
    note: { type: 'string' },
  },
}

const PROMPTS_SCHEMA = {
  type: 'object',
  required: ['prompts'],
  properties: {
    prompts: {
      type: 'array',
      items: {
        type: 'object',
        required: ['prompt', 'category'],
        properties: {
          prompt: { type: 'string' },
          category: { type: 'string' },
          contains: { type: 'array', items: { type: 'string' } },
          contains_any: { type: 'array', items: { type: 'string' } },
          not_contains: { type: 'array', items: { type: 'string' } },
          routes_to: { type: 'string' },
          min_length: { type: 'number' },
        },
      },
    },
  },
}

const GEN_SCHEMA = {
  type: 'object',
  required: ['answer', 'elapsed_ms', 'reached_backend'],
  properties: {
    answer: { type: 'string' },
    elapsed_ms: { type: 'number' },
    reached_backend: { type: 'boolean', description: 'false if the request errored / 5xx / no answer field' },
    agentId: { type: 'string' },
    deterministic_pass: { type: 'boolean', description: 'do the declared contains/not_contains/routes_to invariants hold? (best-effort substring check)' },
    deterministic_notes: { type: 'string' },
  },
}

const JUDGE_SCHEMA = {
  type: 'object',
  required: ['truth', 'certainty', 'rationale'],
  properties: {
    // Hexavalent T/P/U/F/C per the ecosystem convention (a single judge never
    // emits D — D is the AGGREGATE state when judges split T/P vs F).
    truth: { type: 'string', enum: ['T', 'P', 'U', 'F', 'C'] },
    certainty: { type: 'number', description: '0..1, per Demerzel thresholds' },
    rationale: { type: 'string', description: 'one or two sentences, cite the specific claim that drove the verdict' },
  },
}

const SYNTH_SCHEMA = {
  type: 'object',
  required: ['written_path', 'summary'],
  properties: {
    written_path: { type: 'string' },
    summary: { type: 'string' },
    pass_pct: { type: ['number', 'null'] },
    evaluated: { type: 'number' },
  },
}

// ── Hexavalent consensus (deterministic, in-script for reproducibility) ───────
// Combine the panel's per-lens verdicts into one hexavalent verdict.
//   C (contradiction) is sticky: any high-certainty C dominates.
//   Disagreement between the {T,P} camp and the {F} camp → D (disputed).
//   Otherwise majority among {T,P,F,U}; U on a tie; P when T and P mix.
// pass = consensus ∈ {T, P}.
function consensus(verdicts) {
  const v = verdicts.filter(Boolean)
  if (v.length === 0) return { truth: 'U', certainty: 0, pass: false, reason: 'no judge returned a verdict' }

  const strongC = v.filter((x) => x.truth === 'C' && x.certainty >= 0.7)
  if (strongC.length > 0) {
    return { truth: 'C', certainty: mean(strongC.map((x) => x.certainty)), pass: false, reason: 'a judge found an internal/ground-truth contradiction' }
  }

  const good = v.filter((x) => (x.truth === 'T' || x.truth === 'P') && x.certainty >= 0.6)
  const bad = v.filter((x) => (x.truth === 'F' || x.truth === 'C') && x.certainty >= 0.6)
  if (good.length > 0 && bad.length > 0) {
    return { truth: 'D', certainty: mean(v.map((x) => x.certainty)), pass: false, reason: 'panel split: some judges T/P, others F/C' }
  }

  // Tally remaining (no strong cross-camp split).
  const tally = { T: 0, P: 0, U: 0, F: 0, C: 0 }
  for (const x of v) tally[x.truth] += 1
  const top = Object.keys(tally).sort((a, b) => tally[b] - tally[a])
  // Tie at the top → fall back to U (honest uncertainty, not a coin flip).
  let truth = top[0]
  if (tally[top[0]] === tally[top[1]]) truth = 'U'
  // If any T but also any P (and no F), the honest aggregate is P (a gap exists somewhere).
  if ((truth === 'T') && tally.P > 0) truth = 'P'
  const pass = truth === 'T' || truth === 'P'
  return { truth, certainty: mean(v.map((x) => x.certainty)), pass, reason: `majority ${truth} (${JSON.stringify(tally)})` }
}

function mean(xs) {
  if (!xs.length) return 0
  return Math.round((xs.reduce((a, b) => a + b, 0) / xs.length) * 1000) / 1000
}

// ════════════════════════════════════════════════════════════════════════════
// PHASE 1 — Probe backend + load corpus (in parallel; both are prerequisites)
// ════════════════════════════════════════════════════════════════════════════
phase('Probe')

const [probe, corpus] = await parallel([
  () =>
    agent(
      `Confirm the GA chatbot backend is live and discover its exact request/response shape.\n` +
      `1. Read ${CORPUS.replace('prompts.yaml', 'PromptCorpusTests.cs')} around the POST call (~line 185) to find the exact ` +
      `chat route, the request JSON body shape, and the response field that holds the natural-language answer.\n` +
      `2. The known facts: host base is ${HOST}, route is /api/chatbot/chat, answer field is "naturalLanguageAnswer", ` +
      `agent id is "agentId". Confirm these against the source.\n` +
      `3. Send ONE probe request (curl or Invoke-RestMethod) with a trivial prompt like "What is a major triad". ` +
      `Set healthy=true ONLY if you get back a non-empty answer in the answer field. A connection refused / 5xx / ` +
      `empty body means healthy=false — do NOT fabricate.\n` +
      `Return requestTemplate as the JSON body with the literal token {{PROMPT}} where the user text belongs.`,
      { label: 'probe:backend', phase: 'Probe', schema: PROBE_SCHEMA },
    ),
  () =>
    agent(
      `Read the GA chatbot corpus at ${CORPUS}. It is YAML under a top-level "prompts:" key. ` +
      `Return every entry whose "skip" is not true, preserving prompt, category, contains, contains_any, ` +
      `not_contains, routes_to, and min_length. Do not invent entries — only what the file contains.`,
      { label: 'load:corpus', phase: 'Probe', schema: PROMPTS_SCHEMA },
    ),
])

// Never fabricate a pass rate when the backend never answered (feedback_green_but_dead).
if (!probe || !probe.healthy) {
  log(`Backend NOT healthy at ${HOST} — writing a degraded snapshot (pass_pct=null), not a fabricated score.`)
  phase('Synthesize')
  const degraded = await agent(
    `Write a DEGRADED semantic-QA snapshot. Run \`date -u +%Y-%m-%dT%H:%M:%SZ\` for the timestamp and ` +
    `\`date -u +%Y-%m-%d\` for the filename. Create the file ${SNAPSHOT_DIR}/<date>.json (mkdir -p the dir) with:\n` +
    `{ "timestamp": <ts>, "layer": "semantic-panel", "degraded": true, ` +
    `"degraded_reason": "backend_unavailable", "pass_pct": null, "total_prompts": ${(corpus && corpus.prompts ? corpus.prompts.length : 0)}, ` +
    `"evaluated": 0, "note": ${JSON.stringify('Probe could not reach ' + HOST + ': ' + (probe ? probe.note : 'no probe result'))} }\n` +
    `Return the written path and a one-line summary.`,
    { label: 'synth:degraded', phase: 'Synthesize', schema: SYNTH_SCHEMA },
  )
  return { degraded: true, host: HOST, snapshot: degraded }
}

let prompts = (corpus && corpus.prompts) || []
if (LIMIT != null) prompts = prompts.slice(0, LIMIT)
log(`Backend healthy. Evaluating ${prompts.length} prompt(s) through a ${LENSES.length}-lens panel.`)

// ════════════════════════════════════════════════════════════════════════════
// PHASE 2 — Per prompt: generate (live) → judge panel → consensus
// Pipeline (no barrier): a prompt can be in judging while another is still
// generating. Wall-clock = slowest single chain, not sum of stage maxima.
// ════════════════════════════════════════════════════════════════════════════
phase('Evaluate')

const records = await pipeline(
  prompts,
  // Stage 1 — query the SAME host the corpus targets, faithful to PromptCorpusTests.
  (p) =>
    agent(
      `Query the live GA chatbot and report its answer verbatim.\n` +
      `Endpoint: ${probe.endpoint}\n` +
      `Request body (substitute the prompt for {{PROMPT}}, JSON-escaped): ${probe.requestTemplate}\n` +
      `Answer field in the response: ${probe.answerField}; routed agent id field: agentId.\n` +
      `User prompt: ${JSON.stringify(p.prompt)}\n` +
      `Measure elapsed_ms around the request. If the call errors / 5xx / empty answer, set reached_backend=false ` +
      `and leave answer "".\n` +
      `Also do a best-effort deterministic check: does the answer satisfy these declared invariants? ` +
      `contains(all, case-insensitive)=${JSON.stringify(p.contains || [])}, ` +
      `contains_any=${JSON.stringify(p.contains_any || [])}, not_contains=${JSON.stringify(p.not_contains || [])}, ` +
      `min_length=${p.min_length || 0}, routes_to=${JSON.stringify(p.routes_to || null)} (compare to agentId). ` +
      `Set deterministic_pass accordingly and note any miss.`,
      { label: `gen:${p.category}`, phase: 'Evaluate', schema: GEN_SCHEMA },
    ).then((g) => ({ ...g, prompt: p.prompt, category: p.category })),

  // Stage 2 — N independent lenses judge the answer, then deterministic consensus.
  async (g) => {
    if (!g || !g.reached_backend || !g.answer) {
      return { ...g, consensus: { truth: 'U', certainty: 0, pass: false, reason: 'no answer to judge' }, judges: [] }
    }
    const judges = await parallel(
      LENSES.map((lens) => () =>
        agent(
          `${lens.brief}\n\n` +
          `Question the user asked: ${JSON.stringify(g.prompt)}\n` +
          `Category: ${g.category}\n` +
          `Chatbot's answer:\n"""\n${g.answer}\n"""\n\n` +
          `Return a hexavalent verdict (T/P/U/F/C) for YOUR lens only, with certainty 0..1 and a one-sentence ` +
          `rationale citing the specific claim that drove it. Default to U if you genuinely cannot tell.`,
          { label: `judge:${lens.key}`, phase: 'Evaluate', schema: JUDGE_SCHEMA },
        ).then((v) => (v ? { ...v, lens: lens.key } : null)),
      ),
    )
    return { ...g, judges: judges.filter(Boolean), consensus: consensus(judges) }
  },
)

const evaluated = records.filter(Boolean)

// ════════════════════════════════════════════════════════════════════════════
// PHASE 3 — Aggregate + write the semantic snapshot (sibling to the deterministic
// one; we never overwrite the locked ix-quality-trend schema).
// ════════════════════════════════════════════════════════════════════════════
phase('Synthesize')

// Aggregate in-script so the numbers are reproducible and don't depend on an LLM.
const passes = evaluated.filter((r) => r.consensus.pass).length
const passPct = evaluated.length ? Math.round((passes / evaluated.length) * 1000) / 10 : null
const byCategory = {}
const byTruth = { T: 0, P: 0, U: 0, D: 0, F: 0, C: 0 }
const latencies = []
for (const r of evaluated) {
  byTruth[r.consensus.truth] += 1
  if (Number.isFinite(r.elapsed_ms)) latencies.push(r.elapsed_ms)
  const c = (byCategory[r.category] = byCategory[r.category] || { n: 0, pass: 0 })
  c.n += 1
  if (r.consensus.pass) c.pass += 1
}
for (const k of Object.keys(byCategory)) {
  const c = byCategory[k]
  c.pass_pct = c.n ? Math.round((c.pass / c.n) * 1000) / 10 : null
}
const avgMs = latencies.length ? Math.round(latencies.reduce((a, b) => a + b, 0) / latencies.length) : null

const snapshot = {
  layer: 'semantic-panel',
  total_prompts: prompts.length,
  evaluated: evaluated.length,
  pass_pct: passPct,
  avg_response_ms: avgMs,
  panel: { size: LENSES.length, lenses: LENSES.map((l) => l.key) },
  by_category: byCategory,
  by_truth_value: byTruth,
  prompts: evaluated.map((r) => ({
    prompt: r.prompt,
    category: r.category,
    consensus: r.consensus.truth,
    pass: r.consensus.pass,
    certainty: r.consensus.certainty,
    reason: r.consensus.reason,
    deterministic_pass: r.deterministic_pass ?? null,
    agentId: r.agentId ?? null,
    elapsed_ms: r.elapsed_ms ?? null,
    judges: r.judges.map((j) => ({ lens: j.lens, truth: j.truth, certainty: j.certainty, rationale: j.rationale })),
  })),
}

const written = await agent(
  `Write this semantic-QA snapshot to disk.\n` +
  `1. Run \`date -u +%Y-%m-%dT%H:%M:%SZ\` (timestamp) and \`date -u +%Y-%m-%d\` (filename date).\n` +
  `2. mkdir -p ${SNAPSHOT_DIR}\n` +
  `3. Write ${SNAPSHOT_DIR}/<date>.json containing this object with a "timestamp" field prepended:\n` +
  `${JSON.stringify(snapshot)}\n` +
  `Write it as valid pretty-printed JSON. Return the written path, the pass_pct (${passPct}), evaluated count ` +
  `(${evaluated.length}), and a one-line summary of the by_truth_value distribution.`,
  { label: 'synth:write', phase: 'Synthesize', schema: SYNTH_SCHEMA },
)

log(`Semantic panel: ${passes}/${evaluated.length} pass (${passPct ?? 'n/a'}%). Truth dist: ${JSON.stringify(byTruth)}`)
return { host: HOST, pass_pct: passPct, evaluated: evaluated.length, by_truth_value: byTruth, snapshot: written }
