export const meta = {
  name: 'deep-research-frugal',
  description: 'Cost-tiered deep research (v0.3): haiku search/fetch/extract, sonnet escalating verify, session-model synthesis; reachability-aware + anti-fabrication',
  whenToUse: 'Fact-checked research reports when cost matters. Pass the question as args (string or {question}). Targets ~10-20x cheaper than the bundled deep-research via hard fanout caps, model tiering, 1-vote verification with escalation, and an on-disk source cache. BACKLOG.md "In-house frugal deep-research workflow" is the design record.',
  phases: [
    { title: 'Scope', detail: 'decompose the question into at most 4 search angles', model: 'haiku' },
    { title: 'Search', detail: 'one web searcher per angle', model: 'haiku' },
    { title: 'Extract', detail: 'cached fetch + falsifiable-claim extraction', model: 'haiku' },
    { title: 'Verify', detail: '1 skeptic vote per claim; 3-vote panel only on contest or load-bearing claims', model: 'sonnet' },
    { title: 'Synthesize', detail: 'merge, rank, cite — session model, single agent' },
  ],
}

// ---------------------------------------------------------------------------
// Hard caps — the cost contract of this workflow. Raising these is a deliberate
// spend decision; announce the estimated cost to the user first (CLAUDE.md
// session-learned rule, 2026-07-02).
// ---------------------------------------------------------------------------
const MAX_ANGLES = 4
const MAX_RESULTS_PER_ANGLE = 6
const MAX_SOURCES = 10
const MAX_CLAIMS = 25
const MIN_BUDGET_FOR_STAGE = 30_000 // tokens; below this, degrade instead of fanning out

const QUESTION = typeof args === 'string' ? args : args && args.question
if (!QUESTION) throw new Error('Pass the research question as args (string or {question: "..."}).')

// Cache lives in the ga repo so re-runs and follow-up questions skip re-fetching.
const CACHE_NOTE = [
  'SOURCE CACHE PROTOCOL: the cache directory is state/research-cache/ under the ga repo root',
  '(if your cwd is the workspace parent, that is ga/state/research-cache/; create it if missing).',
  'Cache key: sha256 of the URL — bash: echo -n "<url>" | sha256sum | cut -d" " -f1.',
  'BEFORE fetching, Read <cache-dir>/<key>.md; if it exists, use it and do NOT fetch.',
  'AFTER a successful fetch, Write the extracted plain text (first line: the URL) to <cache-dir>/<key>.md.',
  'Cache files must contain cleaned SOURCE text only — never your own synthesis, search-result notes,',
  'or query-specific commentary (a poisoned cache silently corrupts every future run on that URL).',
  'If you could not fetch the actual page, do NOT write a cache file.',
].join(' ')

// Fetch discipline (2026-07-03 smoke test): hung fetches and huge pages were
// the top cost driver — 7 agents died on "Prompt is too long" retry storms.
const FETCH_DISCIPLINE =
  'FETCH DISCIPLINE: try each URL AT MOST ONCE (WebFetch); if it errors, hangs, or the page is ' +
  'huge, fall back to the cache file and the quoted excerpt and say so in your reasoning — do NOT ' +
  'retry, do NOT pull full page bodies into your context.'

const stats = { agents: { haiku: 0, sonnet: 0, session: 0 }, cacheHits: 0, escalations: 0, dropped: [] }
const spent = (tier) => { stats.agents[tier] += 1 }

const guard = (stage) => {
  if (budget.total && budget.remaining() < MIN_BUDGET_FOR_STAGE) {
    log(`budget: skipping ${stage} — ${Math.round(budget.remaining() / 1000)}k tokens remaining`)
    stats.dropped.push(`${stage} (budget)`)
    return false
  }
  return true
}

// --------------------------------------------------------------------- Scope
phase('Scope')
const ANGLES_SCHEMA = {
  type: 'object',
  required: ['angles'],
  properties: {
    angles: {
      type: 'array',
      minItems: 2,
      maxItems: MAX_ANGLES,
      items: {
        type: 'object',
        required: ['key', 'query'],
        properties: {
          key: { type: 'string' },
          query: { type: 'string' },
          rationale: { type: 'string' },
        },
      },
    },
  },
}
spent('haiku')
const scope = await agent(
  `Decompose this research question into at most ${MAX_ANGLES} DISTINCT web-search angles ` +
    `(different sub-questions or source types, not rephrasings). Prefer angles whose answers are ` +
    `verifiable facts. Question:\n\n${QUESTION}`,
  { label: 'scope', phase: 'Scope', schema: ANGLES_SCHEMA, model: 'haiku', effort: 'low' }
)
const angles = (scope && scope.angles ? scope.angles : []).slice(0, MAX_ANGLES)
if (!angles.length) throw new Error('Scoping produced no angles.')
log(`Scope: ${angles.length} angles — ${angles.map((a) => a.key).join(', ')}`)

// -------------------------------------------------------------------- Search
// Barrier justified: source selection dedupes across ALL angles (cross-angle
// hits are the strongest quality signal) before the capped Extract fanout.
phase('Search')
const SEARCH_SCHEMA = {
  type: 'object',
  required: ['results'],
  properties: {
    results: {
      type: 'array',
      maxItems: MAX_RESULTS_PER_ANGLE,
      items: {
        type: 'object',
        required: ['url', 'title'],
        properties: {
          url: { type: 'string' },
          title: { type: 'string' },
          why: { type: 'string' },
          quality: { type: 'string', enum: ['primary', 'secondary', 'blog', 'unreliable'] },
        },
      },
    },
  },
}
const searches = (
  await parallel(
    angles.map((a) => () => {
      spent('haiku')
      return agent(
        `Web-search this angle and return the ${MAX_RESULTS_PER_ANGLE} most load-bearing sources ` +
          `(prefer primary: official announcements, first-party repos/docs, papers; mark quality).\n\n` +
          `REACHABILITY (v0.3, this sandbox's network policy 403s most academic publishers — ` +
          `arxiv.org, springer, journals, NIH/NCBI, even Wikipedia — at the proxy): strongly prefer ` +
          `sources whose evidence is FETCHABLE here — GitHub repos (LICENSE, README, code), product ` +
          `docs, official blogs, changelogs, GitHub-hosted datasets. Treat a paywalled/publisher URL ` +
          `as a POINTER whose claims must be corroborated from a reachable source or from the search ` +
          `snippet itself — never as a fetchable primary. Angle "${a.key}": ${a.query}\n\n` +
          `Overall question for context: ${QUESTION}`,
        { label: `search:${a.key}`, phase: 'Search', schema: SEARCH_SCHEMA, model: 'haiku', effort: 'low' }
      )
    })
  )
).filter(Boolean)

const byUrl = new Map()
for (const s of searches) {
  for (const r of s.results || []) {
    const key = (r.url || '').replace(/[#?].*$/, '').replace(/\/+$/, '').toLowerCase()
    if (!key) continue
    const prev = byUrl.get(key)
    if (prev) prev.hits += 1
    else byUrl.set(key, { ...r, hits: 1 })
  }
}
const qualityRank = { primary: 0, secondary: 1, blog: 2, unreliable: 3 }
const sources = [...byUrl.values()]
  .sort((x, y) => y.hits - x.hits || (qualityRank[x.quality] ?? 2) - (qualityRank[y.quality] ?? 2))
  .slice(0, MAX_SOURCES)
if (byUrl.size > MAX_SOURCES) {
  stats.dropped.push(`${byUrl.size - MAX_SOURCES} sources beyond MAX_SOURCES=${MAX_SOURCES}`)
  log(`Search: ${byUrl.size} unique sources, keeping top ${MAX_SOURCES}`)
}

// ------------------------------------------------------------------- Extract
phase('Extract')
const CLAIMS_SCHEMA = {
  type: 'object',
  required: ['claims'],
  properties: {
    cacheHit: { type: 'boolean' },
    claims: {
      type: 'array',
      maxItems: 5,
      items: {
        type: 'object',
        required: ['claim', 'importance'],
        properties: {
          claim: { type: 'string' },
          quote: { type: 'string' },
          importance: { type: 'string', enum: ['load-bearing', 'supporting', 'minor'] },
        },
      },
    },
  },
}
let claims = []
if (guard('Extract')) {
  const extractions = await pipeline(sources, (src, _item, i) => {
    spent('haiku')
    return agent(
      `${CACHE_NOTE}\n\n${FETCH_DISCIPLINE}\n\nFetch (cache-first) this source and extract up to 5 ` +
        `FALSIFIABLE claims relevant to the research question — concrete numbers, dates, named facts; ` +
        `skip opinions and marketing. Cache the CLEANED text only (strip nav/boilerplate; cap ~2000 ` +
        `words). Quote the supporting sentence verbatim. Set cacheHit=true if the cache file already ` +
        `existed.\n\nANTI-FABRICATION (v0.3, HARD RULE): every claim's source MUST be content you ` +
        `actually retrieved in THIS task. If the fetch 403s/fails and you have no WebSearch snippet ` +
        `for it, return ZERO claims for this source — do NOT attach this URL to a claim you inferred ` +
        `from prior knowledge, and do NOT reuse another reachable URL as a stand-in source. If you ` +
        `only have a search snippet (not the full page), you may extract from it but set ` +
        `importance:"minor" and quote the snippet verbatim. Never write a cache file for a source you ` +
        `could not fetch.\n\nSource: ${src.url} (${src.title})\nQuestion: ${QUESTION}`,
      { label: `extract:${i}`, phase: 'Extract', schema: CLAIMS_SCHEMA, model: 'haiku', effort: 'low' }
    ).then((r) => {
      if (r && r.cacheHit) stats.cacheHits += 1
      return (r && r.claims ? r.claims : []).map((c) => ({ ...c, url: src.url, quality: src.quality }))
    })
  })
  claims = extractions.filter(Boolean).flat()
  // Crude cross-source dedupe; the synthesis agent does the semantic merge.
  const seen = new Set()
  claims = claims.filter((c) => {
    const k = c.claim.toLowerCase().replace(/\W+/g, ' ').trim().slice(0, 90)
    if (seen.has(k)) return false
    seen.add(k)
    return true
  })
  const rank = { 'load-bearing': 0, supporting: 1, minor: 2 }
  claims.sort((a, b) => (rank[a.importance] ?? 1) - (rank[b.importance] ?? 1))
  if (claims.length > MAX_CLAIMS) {
    stats.dropped.push(`${claims.length - MAX_CLAIMS} claims beyond MAX_CLAIMS=${MAX_CLAIMS}`)
    claims = claims.slice(0, MAX_CLAIMS)
  }
  log(`Extract: ${claims.length} claims kept (${stats.cacheHits} cache hits)`)
}

// -------------------------------------------------------------------- Verify
// Escalating verification: one skeptic vote per claim; only contested or
// load-bearing claims get the full 3-vote panel (majority refutes -> killed).
phase('Verify')
const VOTE_SCHEMA = {
  type: 'object',
  required: ['refuted', 'confident'],
  properties: {
    refuted: { type: 'boolean' },
    confident: { type: 'boolean', description: 'true if the evidence found was decisive either way' },
    reasoning: { type: 'string' },
  },
}
const refutePrompt = (c) =>
  `Adversarially verify this claim: try to REFUTE it (check the source and at least one independent ` +
  `check; the ${CACHE_NOTE} ${FETCH_DISCIPLINE}). Default to refuted=true if the evidence does not ` +
  `support it as stated; set confident=false if you could not reach decisive evidence.\n\n` +
  `v0.3 SOURCE-UNREACHABLE RULE: if the cited source 403s/is unreachable AND you cannot corroborate ` +
  `the claim from any fetchable source, vote refuted=false, confident=false and say ` +
  `"SOURCE_UNREACHABLE" in reasoning — this marks the claim UNVERIFIED (it will be dropped from ` +
  `confirmed findings), NOT confirmed. A claim citing a URL nobody in this run could open is a ` +
  `fabrication signal: refute it unless a DIFFERENT reachable source supports it.\n\n` +
  `Claim: ${c.claim}\nQuoted support: ${c.quote || '(none)'}\nSource: ${c.url}`

let verified = []
if (claims.length && guard('Verify')) {
  verified = (
    await pipeline(
      claims,
      (c, _i, idx) => {
        spent('sonnet')
        return agent(refutePrompt(c), {
          label: `verify:${idx}`,
          phase: 'Verify',
          schema: VOTE_SCHEMA,
          model: 'sonnet',
          effort: 'low',
        })
      },
      async (v1, c, idx) => {
        if (!v1) return null
        // Escalate on contest or indecision only — NOT on importance alone.
        // (2026-07-03 smoke test: "load-bearing ⇒ panel" escalated 13/15 claims
        // on a pricing question where everything is load-bearing, inverting the
        // tiering: 41 sonnet vs 15 haiku agents. A cleanly-confirmed claim
        // rarely flips on re-vote; a contested or shaky one does.)
        const contested = v1.refuted || v1.confident === false
        if (!contested) return { ...c, verdict: 'confirmed', votes: '1-0' }
        if (!guard(`Verify-escalation:${idx}`)) {
          return { ...c, verdict: v1.refuted ? 'refuted' : 'confirmed', votes: v1.refuted ? '0-1' : '1-0' }
        }
        stats.escalations += 1
        const extra = (
          await parallel(
            ['independent evidence', 'source reliability'].map((lens) => () => {
              spent('sonnet')
              return agent(`${refutePrompt(c)}\n\nLens for this vote: ${lens}.`, {
                label: `verify:${idx}:${lens.split(' ')[0]}`,
                phase: 'Verify',
                schema: VOTE_SCHEMA,
                model: 'sonnet',
                effort: 'low',
              })
            })
          )
        ).filter(Boolean)
        const votes = [v1, ...extra]
        const refutes = votes.filter((v) => v.refuted).length
        return {
          ...c,
          verdict: refutes >= 2 ? 'refuted' : 'confirmed',
          votes: `${votes.length - refutes}-${refutes}`,
          reasoning: votes.map((v) => v.reasoning).filter(Boolean).join(' | '),
        }
      }
    )
  ).filter(Boolean)
} else if (claims.length) {
  verified = claims.map((c) => ({ ...c, verdict: 'unverified', votes: 'n/a' }))
}
const confirmed = verified.filter((c) => c.verdict === 'confirmed')
const refuted = verified.filter((c) => c.verdict === 'refuted')
log(`Verify: ${confirmed.length} confirmed, ${refuted.length} refuted, ${stats.escalations} escalations`)

// ---------------------------------------------------------------- Synthesize
phase('Synthesize')
const REPORT_SCHEMA = {
  type: 'object',
  required: ['summary', 'findings'],
  properties: {
    summary: { type: 'string' },
    findings: {
      type: 'array',
      items: {
        type: 'object',
        required: ['claim', 'confidence', 'sources'],
        properties: {
          claim: { type: 'string' },
          confidence: { type: 'string', enum: ['high', 'medium', 'low'] },
          evidence: { type: 'string' },
          sources: { type: 'array', items: { type: 'string' } },
        },
      },
    },
    caveats: { type: 'string' },
    openQuestions: { type: 'array', items: { type: 'string' } },
  },
}
spent('session')
const report = await agent(
  `Synthesize a research report. Merge semantically duplicate claims, rank by confidence ` +
    `(confirmed by escalated panel + primary source = high), keep every citation, and be explicit ` +
    `about coverage gaps (dropped items, unverified claims, angles that produced nothing).\n\n` +
    `Question: ${QUESTION}\n\nCONFIRMED CLAIMS:\n${JSON.stringify(confirmed, null, 1)}\n\n` +
    `REFUTED (do not use except to warn):\n${JSON.stringify(refuted.map(({ claim, votes, url }) => ({ claim, votes, url })), null, 1)}\n\n` +
    `DROPPED/CAPS: ${stats.dropped.join('; ') || 'none'}`,
  { label: 'synthesize', phase: 'Synthesize', schema: REPORT_SCHEMA }
)

return {
  question: QUESTION,
  ...report,
  refuted: refuted.map(({ claim, votes, url }) => ({ claim, votes, url })),
  sources: sources.map(({ url, quality, hits }) => ({ url, quality, hits })),
  stats: {
    angles: angles.length,
    sources: sources.length,
    claimsExtracted: claims.length,
    confirmed: confirmed.length,
    refuted: refuted.length,
    escalations: stats.escalations,
    cacheHits: stats.cacheHits,
    agents: stats.agents,
    dropped: stats.dropped,
  },
}
