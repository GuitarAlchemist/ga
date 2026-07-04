# Guitar Alchemist Backlog

Ideas waiting to become features. One bullet per idea. When ready to build, run `/feature <idea>` — this launches brainstorm → plan → PR.

See `docs/plans/` for active plans.

---

## Guitarist Problems to Solve

These are real problems guitarists hit. They're the North Star for every feature.

### Ear Training & Recognition
- **"What key am I in?"** — a guitarist plays a progression by ear and wants GA to identify the key, suggest the scale, and show the diatonic chord set
- **"Why does this sound outside?"** — given a chord or note over a backing, explain which scale degrees are "outside" and why (tension vs. resolution)
- **"Is this a common substitution?"** — given two chords, tell the guitarist if there's a known substitution relationship (tritone sub, backdoor dominant, parallel minor, etc.)

### Chord & Voicing Discovery
- **"My hand hurts playing barre chords"** — suggest open-position or partial-barre voicings for any chord, ranked by fret-hand stretch
- **"I play in DADGAD / open D / open G"** — generate correct chord shapes for any alternate tuning, with voicing diagrams

### Improvisation & Scale Choice
- **"Which arpeggio fits this chord progression?"** — given Am F C G, suggest the arpeggios and scales that work over each chord with mode names
- **"How do I solo over a ii-V-I?"** — step-by-step: target notes, guide tones, chromatic approaches, bebop scale options
- **"Show me a lick in the style of [blues / jazz / country]"** — generate a 2-bar lick in ASCII tab + VexTab matching the stylistic vocabulary

### Practice & Learning
- **"I want to practice this scale in all positions"** — generate all 5 CAGED positions for any scale, with tab + fretboard diagram
- **"I keep forgetting chord tones"** — a drill: show a chord symbol → user names the intervals → GA verifies with GaChordIntervals
- **"How long until I can play this song?"** — technique gap analysis: compare required techniques to user's known skills, suggest a practice sequence

### Songwriting & Composition
- **"Help me finish this progression"** — given 2-3 chords, suggest 2-3 natural completions that cadence correctly, in the same key
- **"Make this progression more interesting"** — apply passing chords, secondary dominants, or borrowed chords to a plain I-IV-V
- **"What would this sound like in a minor key?"** — parallel minor / relative minor translation of an existing progression

### Technical / Gear
- **"How do I tune to drop-C?"** — fret-by-fret retuning guide with string tensions and chord shape adjustments
- **"Why does my tab look wrong?"** — parse ASCII tab and flag common notation errors (string order, timing symbols, missing barlines)

---

## Prime Radiant / Living Cosmos Ideas

### Shipped (2026-03-28 mega session)
- ~~IXQL data pipeline (DataFetcher + DynamicPanel)~~ → Phase 2
- ~~Declarative health bindings (HealthBindingEngine)~~ → Phase 3
- ~~Reactive triggers (ON...THEN + ReactiveEngine)~~ → Phase 5 MVP
- ~~Demo tour button (yellow lightning, 6-step walkthrough)~~
- ~~Edge highlighting (SELECT edges SET color/width)~~
- ~~Triage Drop Zone (drag/paste for AI classification + dispatch)~~
- ~~Algedonic → triage wiring (📥 on recommended actions)~~
- ~~JPP comics inline PDF reader (16 comics, Public Domain)~~
- ~~Seldon beliefs + Markov predictions populated~~
- ~~/devfix skill + session-start health check~~
- ~~[SHIPPED] Admin-only access (#30)~~
- ~~[SHIPPED] Rich hover popovers (#32)~~
- ~~[SHIPPED] LLM Status panel real checks (#33)~~
- ~~[SHIPPED] Backlog beliefs with AI assessment (#34)~~
- ~~[SHIPPED] Brainstorm button / What's Next (#36)~~
- ~~[SHIPPED] Godot integration plan (#37)~~
- ~~[SHIPPED] JPP Library panel (#38)~~
- ~~[SHIPPED] Health→graph visualization (#39)~~
- ~~[SHIPPED] Epistemic Constitution + CognitionModel~~
- ~~[SHIPPED] IXQL epistemic commands~~
- ~~[SHIPPED] Meshy AI MCP server~~
- ~~[SHIPPED] Blue-green build MSBuild hook~~
- ~~[SHIPPED] Mobile phone layout fixes~~
- ~~[SHIPPED] GIS preset fixes~~

### Shipped (2026-03-29 multi-model orchestration session)
- ~~Godot Bridge Phase 1 (bridge protocol, useGodotBridge, GodotScene, A2A agent presence)~~
- ~~Grouped IconRail with LED status indicators (5 groups, 22 panels)~~
- ~~LLM Provider categories with LEDs (8 providers: Cloud AI / Local / Tools)~~
- ~~Mistral guitar-alchemist agent (cross-model theory validator)~~
- ~~Multi-Model Fan-Out (parallel provider query service)~~
- ~~Theory Tribunal (multi-model music theory consensus panel)~~
- ~~Demerzel Voice (VoxtralTTS pipeline with Godot lip sync)~~
- ~~Seldon Faculty (LLM providers as university department heads)~~
- ~~Code Lab (multi-model code generation with diff view)~~
- ~~Signal Creation Form (create algedonic signals from UI)~~
- ~~Mobile Overflow Menu (bottom sheet drawer for 22 panels)~~
- ~~Admin Inbox (triaged items with approve/reject/defer)~~
- ~~Weak Signal Interaction Graph (canvas force graph)~~
- ~~Screenshot Capture (camera button + preview overlay)~~
- ~~Active Teams accordion (Claude Code agent teams in ActivityPanel)~~
- ~~FE screenshot capture via Demerzel~~
- ~~Claude teams accordion~~
- ~~Voxtral/Codestral/ACP Vite proxy with auth headers~~

### Active Ideas
- **3D animated IX pipeline flow** — click IXql node → expands into animated pipeline with particles flowing through stages. Uses IxqlParser.
- **Commit detail hover** — hover a commit in ActivityPanel shows diff stats, files changed, author
- **GitHub token for API rate limits** — authenticated requests (5,000/hr vs 60/hr) for the multi-repo ActivityPanel
- **Progressive pipeline zoom** — far = IXql node, medium = orbiting sub-nodes, close = full animated flow diagram
- **Algedonic channels** — Demerzel builds real-time pleasure/pain signal channels from governance health, updates belief states live via SignalR
- **Octopus PR review queue** — industrial-scale PR review using /octo:security + /octo:debate across multiple LLMs, results surfaced in ActivityPanel
- **Auto-spawn Claude Code teams** — Demerzel detects workload (open issues, failing tests, stale nodes) and auto-spawns Claude Code agent teams to address them
- **Activities from real sources** — wire Activities accordion to actual task tracking (GitHub Projects, Linear, or internal PDCA cycles from governance state)
- **Terminal node filaments** — leaf/terminal nodes emit thin glowing filaments with lit dots at the end

### New Ideas (from 2026-03-28 session)
- **IXQL Phase 6: FALLBACK + BACKOFF** — chaos resilience: `CREATE PANEL ... FALLBACK source2 BACKOFF exponential 5s..300s TOLERANCE 3`
- **IXQL meta-combination** — `panel://` cross-references so panels can read from other panels' data
- **Grammar telemetry dashboard** — visualize IXQL variant adoption rates from localStorage telemetry
- **Triage → IXQL auto-generation** — when an item is dispatched, generate the IXQL command to create it (e.g. task → CREATE PANEL with the task data)
- **Godot MCP bridge** — DataFetcher already has `godot://` protocol, wire it to 3D scene management
- **Octopus plugin reinstall** — `/octo:*` skills not loading, needs `claude plugin install octo@nyldn-plugins`

## Infrastructure Ideas

- **Agentic-engineering harness research — INFRA-BLOCKED, no external epic (2026-07-04)** — the frugal run wf_7c628b55 returned zero external evidence: the proxy network policy 403s all academic domains (arxiv.org, springer, journals — confirmed live via `$HTTPS_PROXY/__agentproxy/status` recentRelayFailures), so SWE-bench multi-agent-vs-mono data, agent-eval methods, memory/context patterns, and delegated-agent security incidents could not be sourced. WebSearch snippets survive; primary-PDF fetches don't. **Do NOT dimension harness epics on this run** — its four substantive claims were refuted 0-3 with hallucinated attribution (all citing one unreachable arXiv URL). Two options to actually get this evidence: (a) re-run from an environment whose network policy allows academic domains, or (b) feed specific papers in manually. The run *did* surface internally-checkable security hypotheses worth a **narrow self-audit** (not external facts): is `/dev-data/manifest` access-controlled? is the `/pr` SSE bus public? can `.mcp.json` env leak? — verify against our own code in a security-review pass, independent of any citation.
- **Frugal deep-research v0.3 hardening (2026-07-04)** — two live runs (spectral wf_7b53e48d, agentic wf_7c628b55) exposed the same class of defect the adversarial panel caught: agents (1) **fabricate citations** — reuse a reachable/である URL as the source for unrelated claims, and (2) **poison the cache** — write synthesis memos or FETCH-FAILED stubs as if they were cleaned source text, despite the v0.2 CACHE_NOTE forbidding it. Harden: make the extract/verify prompts emit an explicit `SOURCE_UNREACHABLE` sentinel instead of hallucinating a citation when a fetch 403s; add a post-run cache linter that rejects any cache file whose body doesn't start with its URL + real prose; consider dropping MAX_SOURCES cap dynamically when many primaries are unreachable. `state/research-cache/` is gitignored until this lands.
- **Gap-driven homemade skills — promotion rule (captured 2026-07-02)** — do NOT sweep for "missing skills" speculatively (procedures-over-abilities doctrine: every loaded skill leaks context; authoring machinery already exists via skill-author/skill-graduator agents + demerzel-metabuild). Promotion trigger: a procedure executed manually ≥2-3 times, or a Session-learned rule that requires multi-step discipline, becomes a skill. Evidence-based candidates from 2026-07-02: (a) `/babysit-pr` — subscribe → pre-apply `agent-blackbox-reviewed` with human approval → scan Codex P0/P1 before merge → read check conclusions not sticky text → squash-merge when green (executed by hand 4× today; encodes 3 learned rules); (b) `/workflow-recover` — hung-workflow diagnosis (journal freshness, started-without-result agents, TaskStop, resume **with original args**) — docs/solutions/ first, promote if it recurs; (c) `/forecast` — GA-side bridge to hari's forecast CLI, to create **when** ga#508 starts, not before. — the bundled `deep-research` workflow runs every agent on the session model; the two 2026-07-02 runs (~110 agents each, ~6M tokens) triggered a pay-per-use recharge (see the /correct rule in CLAUDE.md). Shipped as [`.claude/workflows/deep-research-frugal.js`](.claude/workflows/deep-research-frugal.js) (invoke: `Workflow({name: "deep-research-frugal", args: "<question>"})`). **Smoke test 2026-07-03** (pricing question, run wf_83249f24-00b): pipeline + accuracy PASS (answer 100% correct incl. intro-pricing expiry; verification caught fabricated quotes; disk cache works — 6 files, 28K; caps respected), cost FAIL (8.4M tokens — worse than the bundled run on a trivial question). Three causes fixed in v0.2 same day: (1) escalation over-triggered (13/15 claims → 41 sonnet vs 15 haiku agents; now escalates on contest/indecision only, not importance); (2) 7 extract agents died in "Prompt is too long" retry storms on huge/403 pages (now: one fetch attempt max, cleaned+capped cache text, fall back to cache); (3) resume cache-cascade re-ran ~40 agents (inherent to prompt-chaining — documented, not fixed). Revalidation = the ga#509 cloud acceptance run on v0.2. **Cloud lane shipped same day**: [`.github/workflows/deep-research.yml`](.github/workflows/deep-research.yml) (workflow_dispatch: question/slug/budget_k) — subscription-only per cost doctrine (skips green without `CLAUDE_CODE_OAUTH_TOKEN`), main loop pinned to `claude-sonnet-5`, harness-enforced `+<n>k` token ceiling, report + source cache committed to `state/research{,-cache}/`. Cost levers implemented:
  - **Model tiering**: `opts.model: 'haiku'` + `opts.effort: 'low'` for search/fetch/claim-extraction agents; `'sonnet'` for verification; the session model only for the final synthesis (1 agent out of ~50).
  - **Escalating verification**: 1 verdict vote per claim by default, escalate to 3 votes only when the first vote is contested or the claim is load-bearing — instead of a flat 3-vote panel on all claims.
  - **Budget guard**: `while (budget.remaining() > N)` loops + hard fanout caps (4 angles, top-10 sources); `log()` what was dropped so truncation is never silent.
  - **Source cache**: persist fetched pages under `state/research-cache/<url-hash>.md` so re-runs and follow-up questions don't re-fetch (or re-tokenize) the same sources.
  - **Retrieval without LLM tokens**: plain HTTP fetch + text extraction in the harness where possible; LLM only for claims and verdicts.
  - Acceptance: re-run one past question (e.g. the compounding report), compare findings overlap and total token spend vs the bundled workflow. — the mattpocock skills were installed with `npx skills@latest add --copy` and do **not** track upstream; the "externally maintained" compounding assumed in the harness doctrine only holds with deliberate re-sync. Add a quarterly check-in (backlog-groom prompt line or a light workflow that diffs vendored `.claude/skills/` against upstream and files a `needs-triage` issue on drift). Applies to ga, tars, ix, Demerzel, hari. Source: [docs/research/2026-07-02-deep-research-compounding-strategique.md](docs/research/2026-07-02-deep-research-compounding-strategique.md) rec #3.
- **Measure the /correct loop against state/quality/ history (deep-research 2026-07-02)** — the external attribution claim for CLAUDE.md self-correction was refuted (0-3), and the internal mechanism has no measured baseline. Small internal study: for each Session-learned rule, grep later sessions/PRs for recurrence of the corrected pattern; report recurrence rate before/after. Cheap, falsifiable, and closes open question #3 of the report.
- **Lane deletion criteria — treat orchestration lanes as consumables (deep-research 2026-07-02)** — the verified scaffolding-trap finding: harnesses built around a model's limitations become the bottleneck one to two model generations later. Each scheduled/orchestration lane (auto-optimize loops, roundtrip validators, watchdog sweeps, tribunal gates) should carry an explicit retirement trigger in its header comment: "delete when the model does X unaided" or a metric/date. The churn-gated architecture-refinement lanes (2026-07-02) are the pattern to generalize. Absorption speed is unquantified (all strong-form claims were refuted) — hence criteria + observation, not preemptive deletion.
- **DuckDB + IX lens layer (ADR-0001)** — DuckDB + `ix.duckdb_extension` is GA's offline ML/AI **lens layer** (read-only analytics over `state/quality/` artifacts); live retrieval stays on `EmbeddingSchema.WeightedPartitionCosine` (decision: `docs/adr/0001-duckdb-ix-lens-layer-only.md`). Shipped lenses: routing-ambiguity, optick-retrieval-quality, ix-ga-settheory, domain-invariants, loop-convergence, **sae-feature-coverage** (`Scripts/sae-feature-coverage.ps1`), **query-drift** (`Scripts/query-drift-lens.ps1`). Next candidates: SAE feature ↔ partition attribution; per-query drift over a rolling window as a regression alarm; wire both new lenses into the daily `state/quality/` snapshot cadence. Do **not** add a standalone "embedder bake-off geometry" lens — parameterise `routing-ambiguity-diagnostic` by embedder instead (ADR-0001 consequences).
- **install-audit observability +10 (workflow-step strings)** — the remaining install-audit deduction (`observability` 5/15) requires the literal strings `harness-audit` and `response-quality` in `.github/workflows/agent-blackbox.yml`. The `feat/install-audit-closure-ga` branch closed the review-independence deduction via docs only (96 → 100) but cannot reach 110 without an operator workflow edit, which `agent-blackbox.policy.json` `blocked_paths` and the supervised-loop hard gates both prohibit. Follow-up: either (a) add a `harness-audit` + `response-quality` step to the workflow under explicit human approval, or (b) relax the audit rule in agent-blackbox to credit `docs/harness-observability.md` + on-disk artifact evidence.
- **Development process overseer** — deterministic repo-local scanner that watches Claude Code loops/goals, oracle health, dirty scope, protected paths, and kill switches; recommends whether to pause, use `/goal`, schedule `/loop`, or add Stop-hook enforcement. MVP: `Scripts/dev-process-overseer.ps1`; plan: `docs/plans/2026-05-16-feat-development-process-overseer-plan.md`.
- **Live fretboard overlay** — show scale degrees on the React 3D fretboard in real time as the chatbot explains a concept
- **Chatbot chord diagram rendering** — when TheoryAgent mentions a chord, auto-generate a VexTab diagram inline in the chat response
- **BSP room chord assignment** — assign a diatonic chord function (I, ii, V…) to each BSP room and visualise harmonic flow through rooms
- **OPTIC-K similarity search UI** — let guitarists search by playing a rhythm pattern, find songs with similar harmonic structure
- **Remote frontier-LLM provider for hard composition tasks** (parked 2026-05-05) — add a `frontier-remote` purpose to `IChatClientFactory` for tasks where local 3B genuinely fails (long-form composition, deep style imitation). Reference: Cloudflare Workers AI / Infire ([blog](https://blog.cloudflare.com/high-performance-llms/)) supports Kimi K2.5 / Llama 4 Scout via PD-disaggregation + EAGLE-3 spec decoding. **Preconditions before revisiting**: (a) Ollama is stable, (b) we have a concrete user-facing task that 3B fails at, (c) `IChatClientFactory` from migration Phase 1 is fully wired. Don't adopt to dodge an Ollama installer bug. NOT useful for OPTIC-K embeddings (custom 240-dim schema).
- **WebMCP imperative API for chord / fretboard / transpose actions** — Phase 1 of [WebMCP prep plan](docs/plans/2026-05-16-feat-webmcp-prep-plan.md). Register tools via `navigator.modelContext.registerTool()` for multi-step actions that don't map to a single form (play-chord-on-fretboard, transpose-progression, render-vextab). Phase 0 declarative-API annotations are already on the chat form. Defer until W3C promotes WebMCP from CG Draft to REC, OR Chrome 146 ships GA, OR a second WebMCP-aware browser ships. Tracker: revisit when any of those three trigger.

## Pro-Guitarist Usability Gaps (audit 2026-05-05)

`/octo:loop` audit of all 12 chatbot skill bodies found that the existing skill content is theoretically correct and honestly scoped, but the chatbot is **not yet usable by a working professional guitarist** because of capability gaps. The audit shipped two SKILL.md improvements (PR #137 trigger tightening, PR #138 modes catalog enrichment) and surfaced the following dealbreakers as new-tool work — each requires either a new MCP tool or an extension to an existing one.

Ordered by user impact (highest first):

- **Extended / altered / sus / slash chord support in `chord-info`** — pros work daily with `Cmaj9`, `Dm11`, `G13`, `C7b5`, `C7#9`, `Calt`, `Cm7b5` (half-diminished), `Csus2`, `Csus4`, `Cadd9`, `C/E`. As of 2026-05-30 `ga_chord_info` handles `Cm7b5`/`dim7` (see resolved item below), but still returns Error for the rest (`Cmaj9`, `Dm11`, `G13`, `C7b5`, `C7#9`, `Calt`, `Csus2`, `Csus4`, `Cadd9`, `C/E`); the SKILL.md correctly defers ("for altered or extended chords I'd need different tooling"). NOTE: `GetChordInfo_OutOfScopeSymbols_ReturnError` (`ChordMcpToolsTests`) is a *deliberate* contract test asserting these decline cleanly — so this is a contract change, not just a formula add. Implementation: extend `ChordSymbolRegex` to parse extension/alteration tokens and add formula entries, **likely as a separate `ga_chord_extended` tool** to keep the basic tool's contract clean. The compositional `MusicalQueryEncoder.TryComputeChordOffsets` (PR #376) already parses these to pitch classes and could be reused for the offsets (it lacks enharmonic spelling, which `ga_chord_info` needs). Also enables: chord-substitution and progression-completion on extended-chord progressions (currently major/minor-only diatonic-set match). Tracked as `extended-chord-support` below.

- **Alternate tuning chord shapes** — DADGAD, drop-D, double-drop-D, open G/D/E, DGCGCD, all-fourths. A pro asks "give me a Cadd9 in DADGAD" or "what's the easiest A minor in drop-D?" The chatbot currently has no tuning context. New tool: `ga_chord_voicings(chord, tuning)` returning playable shapes ranked by fret-span. Composes with `ga_fret_span` for difficulty filtering.

- **Capo and key-transpose** — pros routinely play "song in concert E with capo 3" (the played shape is C major). New tool: `ga_transpose_progression(progression, semitones)` and `ga_capo_render(progression, capoFret)` returning the played-shape progression. New skill: `transpose` / `capo` SKILL.md gathering both.

- **Voice leading between two chords** — pros pick voicings to minimise common-tone movement (a hallmark of chord-melody and comping). New tool: `ga_voice_leading(chordA, chordB)` returning the optimal voice-leading pair from the precomputed OPTIC-K voicing index, reporting moves per voice. Complements existing `ga_chord_substitutions` (which ranks by ICV cost, not voice-leading smoothness — already documented as out of scope in the SKILL.md).

- **Chord identification from a note set** — `chord-info` SKILL.md mentions this gap: *"what chord is C E G?"* should return "C major triad" and ranked alternatives (C/Em with E in bass, etc.). New tool: `ga_chord_identify(notes)`.

- ~~**Half-diminished + diminished-7 are partially in chord-substitution but missing in chord-info**~~ — ✅ **RESOLVED** (2026-05-30): `m7b5` and `dim7` now parse in `ga_chord_info` (`GetFormula` + `ChordSymbolRegex`, tested in `ChordMcpToolsTests`). The inconsistency vs `ga_chord_substitutions` is gone. See `m7b5-dim7-chord-info` in the Chatbot Track below.

- **Modal analysis of a progression** — `key-identification` only ranks major/minor keys. Pros sometimes write modal vamps ("Am-G-Am-F" is A Aeolian, not A minor with borrowed F). New mode-aware classifier: `ga_key_identify_modal` returning a parent-scale candidate ranked alongside major/minor, with the relative mode named.

- **Modulation detection within one progression** — `key-identification` returns one key (or a relative pair) for the whole input. A pro chord chart with *"C G | F | Db Ab Bb | C"* modulates to bII briefly. Detect modulations and report each segment's local key. Algorithm: sliding-window key-identify with a confidence delta heuristic.

- **Style-specific progression generation** — `progression-completion` is style-neutral by design. Pros want jazz turnarounds (`I-vi-ii-V`, `iii-vi-ii-V`), bluegrass cadences, country I-IV-V variations. New tool: `ga_generate_progression(key, style, length)` with a curated style → cadence-pattern map.

- **Lead-sheet / chord-melody arrangement** — render a melody + chord progression as a chord-melody arrangement playable on a single guitar. Bigger lift; needs a melody-input format and a fretboard-aware arranger. Likely depends on most of the above (voice leading, voicings-by-tuning, transpose).

These are the dealbreakers for moving the chatbot from "novice/intermediate" to "really usable by a pro". Each is a real workstream (≥1 PR, often more) and most depend on each other (e.g. `ga_chord_voicings` is the substrate for several others). The audit produced no SKILL.md text-edit can substitute for any of them.

---

## Chatbot Track (curated 2026-05-10)

Read by `/chatbot-iterate` (Step 1 — pick next item). Priority is user impact first; dependencies break ties (a P0 with an unmet dep is effectively P1). Path hints feed the tribunal-gate classifier in `Scripts/check-chatbot-tribunal-gate.ps1`. **Don't add items here without a path hint** — the gate plan needs it.

Status legend: **ready** = unblocked, can pick today. **blocked** = waiting on listed prerequisite. **scheduled** = work has a fire-trigger from another initiative; don't start manually.

### P0 — load-bearing for chatbot trust

- [ ] **`memory-session-scope`** [ready] — Make `MemoryStore` session-scoped so retrieval can be re-enabled without cross-conversation leak. Memory hook already gated by `Memory:EnrichOnRetrieve` flag (PR #151), but the underlying isolation gap remains. Paths: `Common/GA.Business.ML/Agents/Memory/MemoryStore.cs`, `Common/GA.Business.ML/Agents/Hooks/MemoryHook.cs`, `Common/GA.Business.ML/Agents/Memory/MemoryMcpTools.cs`. **Tribunal: REQUIRED** (Agents path). Was task #82.
- [ ] **`router-quality`** [ready] — Improve embedding router quality (Phase 3 next bottleneck per memory). **Eval harness SHIPPED** — `Tests/Common/GA.Business.ML.Tests/Eval/RoutingEvalHarness.cs` (reflect-loads production intents, runs labeled corpus through the real router) with a fresh baseline at the production 0.55 threshold in `state/quality/routing-eval-2026-05-30.json`: **in-scope 89.2%, OOS-decline 87.5%, mean top1−top2 margin 0.170** (PR #378 added the OOS dimension + margin and fixed a 0.65/0.55 threshold-drift bug). **REMAINING (the actual `router-quality` work): tune** example prompts / `DefaultRoutingHintProvider` rules for the weak intents (`commontones` F1 0.71, `scaleinfo` 0.73, `progressioncompletion` recall 0.60) against that baseline, and decide an escalate-on-ambiguity rule using the margin signal. Paths: `Common/GA.Business.ML/Agents/Intents/SemanticIntentRouter.cs`, `Common/GA.Business.ML/Agents/Intents/DefaultRoutingHintProvider.cs`, eval data `Tests/Common/GA.Business.ML.Tests/Data/routing-eval-prompts.json`. **Tribunal: REQUIRED**. Was task #81.
- [x] **`m7b5-dim7-chord-info`** ✅ **DONE** (verified shipped 2026-05-30 — backlog had drifted) — `m7b5` and `dim7` parse in `ga_chord_info`: present in `GetFormula`, `NormalizeQuality`, and `ChordSymbolRegex` in `Common/GA.Business.ML/Agents/Mcp/ChordMcpTools.cs` (note: actual path is `Agents/Mcp/`, not the `Mcp/Tools/` originally listed), covered by `ChordMcpToolsTests` (`Cdim7`→C Eb Gb Bbb, `Bm7b5`→B D F A, half-dim + dim7 test groups). The inconsistency vs `ga_chord_substitutions` is resolved.

### P1 — capability completeness

- [ ] **`text-embedder-evaluation`** [ready] — Evaluate a stronger text embedder for semantic routing. The `router-quality` example/hint tuning has hit the **embedder ceiling**: anchor silhouette is 0.036 (clouds barely separate) and a learned head can't beat the 0.934 cosine router (PR #419) — both point at `nomic-embed-text` (768-d) as the limit, not the algorithm. A stronger embedder is the one lever that improves routing **without more labeled data**. Plan: [docs/plans/2026-06-16-ml-text-embedder-evaluation-plan.md](docs/plans/2026-06-16-ml-text-embedder-evaluation-plan.md). Phase 0 = bake-off harness (reuse `RoutingEvalHarness` + `routing-ambiguity-diagnostic` + `train-router-head.py`, all `GA_EMBED_MODEL`-parameterized); Phase 1 = decouple routing embedder via a purpose factory (routing has no persistence, so the swap is reversible and doesn't touch `MemoryStore`/MongoDB/GPU-768 hardcodes). OPTIC-K musical embedding explicitly out of scope. Paths: `Common/GA.Business.ML/Providers/OllamaProvider.cs`, `Common/GA.Business.ML/Extensions/AiServiceExtensions.cs`, `Common/GA.Business.ML/Agents/Intents/SemanticIntentRouter.cs`, `Apps/appsettings.Shared.json`. **Tribunal: REQUIRED** (Agents/Intents path).
- [ ] **`extended-chord-support`** [ready] — Extend `ChordSymbolRegex` for `Cmaj9`, `Dm11`, `G13`, `C7b5`, `C7#9`, `Calt`, `Csus2`, `Cadd9`, `C/E`. May warrant separate `ga_chord_extended` tool to preserve `ga_chord_info` contract. Substrate for chord-substitution + progression-completion on extended chords. Paths: `Common/GA.Business.Core/Notes/Chord*.cs` (parser), `Common/GA.Business.ML/Mcp/Tools/ChordMcpTools.cs` (formula table + tool). **Tribunal: REQUIRED** (Mcp path).
- [ ] **`chord-identify`** [ready] — `ga_chord_identify(notes)` → "C major triad" + ranked alternatives (C/Em with E in bass). Already mentioned in chord-info SKILL.md as a known gap. Paths: `Common/GA.Business.ML/Mcp/Tools/`. **Tribunal: REQUIRED**.
- [ ] **`alternate-tuning-voicings`** [blocked: voicing-index expansion] — `ga_chord_voicings(chord, tuning)` for DADGAD, drop-D, double-drop-D, open G/D/E, DGCGCD, all-fourths. Returns shapes ranked by fret-span. Composes with `ga_fret_span`. Substrate for several P2 items. Paths: `Common/GA.Business.ML/Mcp/Tools/`, `Common/GA.Business.Core/Fretboard/Voicings/`. **Tribunal: REQUIRED**. Prereq: voicing index needs alternate-tuning entries (status unknown — verify before unblocking).
- [ ] **`transpose-capo`** [ready] — `ga_transpose_progression(progression, semitones)` + `ga_capo_render(progression, capoFret)` returning played-shape progression. New `transpose`/`capo` SKILL.md gathers both. Paths: `Common/GA.Business.ML/Mcp/Tools/`, `.claude/skills/transpose/SKILL.md`. **Tribunal: REQUIRED**.
- [ ] **`voice-leading-pair`** [ready] — `ga_voice_leading(chordA, chordB)` from precomputed OPTIC-K voicing index, reports moves per voice. Complements `ga_chord_substitutions` (which ranks by ICV cost — already documented out-of-scope in SKILL.md). Paths: `Common/GA.Business.ML/Mcp/Tools/`. **Tribunal: REQUIRED**.

### P2 — advanced / dependent

- [ ] **`modal-key-identify`** [ready] — `ga_key_identify_modal` returning parent-scale candidate ranked alongside major/minor with relative mode named. Pros write modal vamps (Am-G-Am-F is A Aeolian, not A minor with borrowed F). Paths: `Common/GA.Business.ML/Mcp/Tools/`, `Common/GA.Business.Core/Harmony/Modal/`. **Tribunal: REQUIRED**.
- [ ] **`modulation-detection`** [ready] — Sliding-window key-identify with confidence-delta heuristic. Detect bII/bVI etc. modulations within a single progression. Paths: `Common/GA.Business.ML/Mcp/Tools/`. **Tribunal: REQUIRED**.
- [ ] **`style-progression-gen`** [ready] — `ga_generate_progression(key, style, length)` with style → cadence-pattern map (jazz turnarounds, bluegrass, country I-IV-V variations). Paths: `Common/GA.Business.ML/Mcp/Tools/`. **Tribunal: REQUIRED**.
- [ ] **`fretboard-overlay-live`** [ready] — Show scale degrees on the React 3D fretboard in real time as the chatbot explains. Frontend-heavy; only the SignalR push from the agent touches chatbot core. Paths: `ReactComponents/ga-react-components/**`, `Apps/ga-server/GaApi/Hubs/ChatbotHub*`. **Tribunal: NOT required** (no Agents/MCP/DSL paths). octo:review still recommended.
- [ ] **`chord-diagram-inline`** [ready] — When TheoryAgent mentions a chord, auto-generate a VexTab diagram inline in the chat response. Paths: `Common/GA.Business.ML/Agents/TheoryAgent*` + `ReactComponents/ga-react-components/**`. **Tribunal: REQUIRED** (Agents path).
- [ ] **`lead-sheet-arranger`** [blocked: extended-chord-support, alternate-tuning-voicings, voice-leading-pair, transpose-capo] — Render melody + progression as chord-melody arrangement. Bigger lift; needs melody-input format + fretboard-aware arranger. Don't start until the four prereqs ship.
- [ ] **`optk-index-filter-enrichment`** ([#448](https://github.com/GuitarAlchemist/ga/issues/448)) [blocked: demand — gated on `dropped`-filter telemetry] — Enrich the OPTK mmap index so `OptickSearchStrategy` (the production-default voicing search) can honor the rich `VoicingSearchFilters` it currently drops (Difficulty, Tags, ModeName, StackingType, SetClassId, Phase 3/4 fields — see `OptickSearchStrategy.ComputeUnsupportedFilters`). **One-way door**: a corpus reindex (OPTIC-K coordination + sign-off, cross-repo with ix's `ix-optick`). **Demand-gated, evidence-driven** — don't reindex speculatively: the `dropped` telemetry (`state/telemetry/voicing-search/*.jsonl`, added 2026-06-20) records which filters callers actually request but the index can't serve; reindex only the high-frequency fields. See [docs/adr/0002-voicing-filter-parity-cpu-gpu-only.md](docs/adr/0002-voicing-filter-parity-cpu-gpu-only.md) (option 3). Paths: `Common/GA.Business.ML/Search/OptickSearchStrategy.cs`, OPTK producer in `../ix/` (`ix-optick`), `docs/contracts/`. **Tribunal: REQUIRED** (Search + cross-repo one-way door).
- [ ] **`ga-claim-consistency-checker`** (was `tars-consistency-oracle`) [ready — design captured] — A GA-side **longitudinal self-consistency checker**: extract the chatbot's typed music-theory claims, **detect** same-`key`-different-`asserted_value` contradictions in GA (trivial dict compare), and emit to `state/quality/consistency/` — catching the chatbot asserting *X* one session and *¬X* another, a bug class invisible to single-response QA. **Keystone-corrected 2026-06-20:** TARS's contradiction tools only *query pre-asserted edges* — they don't detect from content (ADR-0003 § Correction) — so detection is GA's; **TARS is an optional downstream ledger**, not load-bearing (and its MCP output is currently broken). **Consistency, not truth** (ADR-0003): never consults GA ground truth, so non-circular. **Tracer-bullet v1 = `pitch_classes` only**, validated by replaying buggy-era #414 traces. Two contradiction kinds: intra-response (prose vs co-emitted tool fact) + cross-session (prose drift). Offline (piggybacks chatbot-qa; no live TARS dependency). Output = quality-snapshot envelope under `state/quality/consistency/`; a source the `qa-verdict` may cite, not a new tribunal. Contract: [`docs/contracts/2026-06-20-ga-tars-claim.contract.md`](docs/contracts/2026-06-20-ga-tars-claim.contract.md) + schema; decision: [`docs/adr/0003-tars-validates-consistency-not-truth.md`](docs/adr/0003-tars-validates-consistency-not-truth.md). Paths: `Common/GA.Business.ML/Agents/*` (claim extraction), `../tars/` (ingest), `docs/contracts/`. **Tribunal: REQUIRED** (Agents + cross-repo).

### Scheduled / orchestrated elsewhere — don't pick manually

- [ ] **`qa-tribunal-phase-1`** [scheduled: trig_01WdRGSqgxah5PD46wg8u4Qq fires 2026-05-18] — Phase 1 of the QA Architect Tribunal. Schema is v0.1 draft; chatbot-iterate consumes the verdict shape but does NOT implement it. See memory `project_qa_architect_tribunal`.
- [ ] **`optick-sae-phase-1`** [scheduled: trig_01QUrKEsYLPPW4KNLzKZRE2n fires 2026-05-19] — Phase 1 of OPTIC-K Sparse Autoencoder. Cross-repo (ix crate + GA consumer + Demerzel orchestration). After Phase 1 lands, a follow-up "consume SAE features in TheoryAgent reasoning" item will be promoted into this track. See memory `project_optick_sae`.

### Parked

- **`frontier-llm-provider`** — `frontier-remote` purpose for `IChatClientFactory`. Reference Cloudflare Workers AI. **Preconditions** (per Infrastructure Ideas): (a) Ollama stable, (b) concrete user-facing task that local 3B fails at, (c) IChatClientFactory fully wired. NOT useful for OPTIC-K embeddings.
- **`gachatbot-managed-agents`** (assessed 2026-07-02) — Anthropic **Managed Agents** (platform.claude.com: server-hosted agent loop + per-session containers, memory stores, vaults, versioned agent configs via `ant` CLI YAML) as a GaChatbot **product** backend: hosted session per end user, per-user persistent memory, outcome tracking. **Verdict: not for the dev/governance harness** — it duplicates the repaired `claude-code-action` lane at strictly higher cost (always API-metered pay-per-use vs $0/call on subscription OAuth), cron needs are covered by GitHub Actions, and the SDK-container gap is covered by the merged devcontainers; fails "abstractions must be earned". **Revisit triggers (product):** (a) a concrete GaChatbot need for per-user persistent memory across sessions, or (b) two occurrences of >30-min mounted-repo custom-toolchain scheduled jobs that Actions/Codespaces can't serve. **One-way door** (pricing + user data leave our infra) → tribunal sign-off before any implementation. Paths: `Apps/GaChatbot/**` (would be a provider behind the existing chat surface, not a new one — see `docs/architecture/chat-surfaces.md`). **Tribunal: REQUIRED** (one-way door).

---

## Jarvis Track — the auditable butler (epics captured 2026-07-02)

A Jarvis is three things, and two and a half of them already exist in this ecosystem:

1. **Continuous presence** — the AFK harness + router + watchdog sweeps (`jules-auto-delegate.yml`, `claude.yml`, `fleet-status.yml`, `post-merge-smoke.yml`, dead-letter sweeps from the 2026-07-02 delegation-chain hardening) are an embryo of an always-on daemon.
2. **A world model** — **hari** (`../hari/`, Rust, `hari-mcp` in `.mcp.json`) is literally that: a persistent belief state, contradictions preserved, updated by observations. It is missing exactly one half: **prediction**.
3. **A safe action boundary** — **Demerzel** + the guardrails hardened on 2026-07-01/02 (cost doctrine, killswitches, `agent-blackbox.policy.json` blocked paths, tribunal gates). See `docs/solutions/tooling/2026-07-02-afk-delegation-chain-failures.md`.

The missing research piece is the **learned** world model — Dreamer/JEPA-class: simulate consequences *before* acting instead of reacting to events. The day **ix** runs a predictive model whose epistemic memory is hari and whose superego is Demerzel, this stops being a metaphor. The plumbing shipped so far (delegate without watching every move) is the existence condition for a Jarvis that isn't a HAL: auditable, bounded, interruptible.

Epics below follow tracer-bullet discipline — each starts with the smallest end-to-end slice, never a layer in isolation. J1–J3 are engineering; J4 is research; J5 is the integration slice and only starts once J1–J4 tracer bullets exist.

### J1 — Continuous presence: from event-reactive workflows to a supervised daemon

Today's presence is a pile of independent event-triggered workflows plus watchdog sweeps bolted on after each silent failure (four found in one evening — see the 2026-07-02 solutions doc). Epic: consolidate into one *deliberate* presence layer.

- **Unify liveness**: one heartbeat surface answering "is the butler awake, and which limbs are asleep?" — router, delegation lanes, tunnels (530-class infra-down detection), quality-snapshot cadence, MCP federation peers. Builds on `fleet-status.yml` + `ecosystem-health.yml` + `Scripts/dev-process-overseer.ps1` rather than replacing them.
- **Dead-letter sweeps as a norm**: every event-triggered lane gets the daily re-route sweep pattern (the fix that unstranded ga#328 after 5+ weeks), not just the Jules lane.
- **Resource idempotency rule mechanized**: automations create their own labels/resources idempotently (rule from the delegation-chain fixes) — lint for it in CI.
- **Tracer bullet**: a single `presence` snapshot under `state/fleet/` (or folded into the dev-data manifest) that aggregates lane health + last-heartbeat per limb, red/green, generated on schedule. Nothing acts on it yet; it just exists and is honest. **✅ shipped 2026-07-02**: `Scripts/presence-snapshot.py` + `.github/workflows/presence-snapshot.yml` → `state/fleet/presence.json` (12 limbs: 7 lanes/sensors via workflow-run health, 4 siblings via poller-state, 1 algedonic inbox; quiet-write = commits only on status change or daily liveness proof). **Expansion shipped same day**: `/dev-data/presence` endpoint + `presence` manifest key (vite.config.ts, `gatherPresence()` with age/stale annotation).
- **New lanes register in presence**: any new scheduled/delegation workflow gets added to `Scripts/presence-snapshot.py` `LANES` **once it has run history** (a never-run lane reads as `unknown` and would yellow the fleet dishonestly). Queue: `architecture-refinement.yml` (ga + tars, shipped 2026-07-02 — need-driven (daily churn-gate check-in, review fires only when a layer clears MIN_COMMITS/MAX_AGE) propose-only headless run of `/improve-codebase-architecture`; subscription-only per cost doctrine).
- Paths: `.github/workflows/*.yml`, `Scripts/dev-process-overseer.ps1`, `state/fleet/`. **Tribunal: not required** (observability only) until any lane gains new write permissions — then required.

### J2 — Epistemic world model: give hari the prediction half

hari holds beliefs, preserves contradictions, updates on observations — memory without foresight. Epic: close the perception–prediction loop so beliefs generate testable expectations.

- **Forecast records**: each belief can emit an expected-observation (probability + horizon); outcomes are matched back and scored (Brier / calibration curve). Prediction error becomes the belief-update signal instead of raw observation alone.
- **Contradiction-aware forecasting**: when contradictory beliefs coexist, both forecast; resolution is driven by which one keeps winning — that's the epistemically honest version of belief decay.
- **Tracer bullet**: hari predicts exactly one narrow, cheap, frequent thing — e.g. "will tonight's `quality-snapshot.yml` run green?" or "will routing-eval in-scope stay ≥ 89%?" — and records forecast vs outcome to its state dir. One belief type, one scorer, end to end. **✅ shipped 2026-07-02** (hari#18): `crates/hari-core/src/forecast.rs` + CLI `forecast emit|resolve|calibration`, append-only JSONL ledger under `state/forecasts/`, canonical-UTC timestamps enforced at the write boundary, 12 tests; 2 live forecasts (quality-snapshot pipeline, chatbot Sonnet 5) resolving 07-03/04.
- **External validation (2026-07-02)**: the compounding deep-research ([docs/research/2026-07-02-deep-research-compounding-strategique.md](docs/research/2026-07-02-deep-research-compounding-strategique.md)) confirms the direction — epistemic miscalibration at the planning stage escapes execution-level verification, and a calibration-aware workflow gains +9.75% task success (arXiv 2605.23414). The calibration ledger is a 5-15 year compounding asset.
- Cross-repo: hari schema additions are a **contract change** (`docs/contracts/` pattern, `links.supersedes` for baseline shifts). **One-way door**: the forecast-record schema, once other repos consume it. **📝 contract drafted 2026-07-02** (v0.1 DRAFT, hari-side review + tribunal pending): `docs/contracts/2026-07-02-hari-forecast-record.contract.md` + schema — pins observables at emission (mechanical scoring), contradiction-aware (each belief forecasts), `void` outcome first-class, first observable = J1's `sensor:quality-snapshot` presence limb.
- Paths: `../hari/` (owner), GA consumes via `hari` MCP; contract lands in `docs/contracts/`. **Tribunal: REQUIRED** (cross-repo contract).

### J3 — Safe action boundary as a first-class contract (the Demerzel superego)

The guardrails exist but live as scattered conventions: cost doctrine in a solutions doc, blocked paths in `agent-blackbox.policy.json`, killswitches checked by individual skills, one-way-door sign-off in CLAUDE.md prose. Epic: promote the boundary to a single machine-readable contract every autonomous actor pre-checks.

- **Action-boundary schema**: capabilities (what an agent may touch), cost lanes (subscription-only vs API-metered, per the 2026-07-02 cost doctrine), one-way-door triggers (OPTIC-K dims, schemas, public APIs, pricing), killswitch + governance-halt semantics — one document, one schema, versioned like `qa-verdict`.
- **Pre-action check**: `supervised-loop` preflight, `/auto-optimize` scope check, and the AFK router all consume the same contract instead of re-implementing fragments.
- **Audit trail**: every autonomous action logs (actor, capability invoked, boundary version, verdict) — the property that makes delegation-without-surveillance defensible.
- **Tracer bullet**: write `docs/contracts/<date>-action-boundary.contract.md` + schema; port exactly one existing consumer (the supervised-loop preflight) to read it. No new enforcement yet — same behavior, single source of truth. **✅ shipped 2026-07-02** (contract stays v0.1 DRAFT until tribunal): contract + schema + `Scripts/action-boundary-aggregate.py` → `state/governance/action-boundary.json` **generated from the fragments** (no duplicate to drift; halt markers string-verified against `Governance.psm1`); supervised-loop SKILL.md Step 2 ported (fragments remain fallback); drift gate wired into `karpathy-cherny-discipline.yml` (`--check` + jsonschema). Next consumers: `dev-process-overseer.ps1` (needs a pwsh session), `/auto-optimize` scope check, AFK router.
- Paths: `docs/contracts/`, `Scripts/dev-process-overseer.ps1`, `../Demerzel/` (owner of governance semantics). **Tribunal: REQUIRED** (governance + cross-repo).

### J4 — Learned predictive world model in ix (Dreamer/JEPA-class) [research]

The research-grade missing piece: a model that *simulates consequences before acting* instead of reacting to events. ix already has the MCTS/skill engine — what's missing is a **learned transition model** to roll out against, instead of hand-coded simulators.

- **Shape**: ix runs the predictive model (Rust, owns the compute); **hari is the epistemic memory** (beliefs in → priors; rollout outcomes out → belief updates via J2's forecast records); **Demerzel is the superego** (candidate actions filtered through J3's boundary before *and after* simulation).
- **Domain discipline**: train on a world we fully own and observe — this repo's own process telemetry (`state/quality/` time series, routing-eval trends, loop-convergence lenses via the ADR-0001 DuckDB layer), NOT music theory and NOT the open internet. The world model predicts *our system*, which is exactly what a butler needs.
- **Tracer bullet**: one learned transition model over one `state/quality/` metric time series (e.g. predict next chatbot-qa score given a proposed change class), evaluated against the trivial baseline (persistence / last-value). If it can't beat persistence, the epic pauses honestly.
- **Explicitly parked until**: J2 tracer bullet ships (hari can score forecasts — otherwise the model has no epistemic memory to write to — **✅ shipped 2026-07-02**) and ix has capacity. No speculative scaffolding in GA before then (Karpathy rule 2).
- **Research verdict (2026-07-02)** — [docs/research/2026-07-02-deep-research-j4-world-models.md](docs/research/2026-07-02-deep-research-j4-world-models.md) (104 agents, 22 confirmed / 3 killed claims): **prepare-the-data now, train nothing explicit now.** (1) Invest now: frontier-as-implicit-world-model (WebDreamer pattern, +34-42% relative with zero training) plugged into J2's Brier ledger, and classic GBDT predictors for CI observables (Facebook production proof). (2) Prepare data: log every typed action→observation transition (action type, diff features, before-state baselines, observed outcome, forecast+resolution) — target order 10⁶ transitions (Dreamer-7B: 3.1M), successes included (~99:1 imbalance). (3) Wait on any latent JEPA/Dreamer-class model: no SE-domain backbone exists, and a 397B trained world model beats frontier-as-simulator by only 0.46 pt (58.71 vs 58.25 on AgentWorldBench). Small-model path plausibly practicable at 2-5 yrs if open weights + logged corpus exist; SE-native latent world model 5-15 yrs or never needed. The tracer bullet below stands, with GBDT-vs-persistence as the reference technique (not a neural net). Not to be confused with [docs/research/world-models-diffusion-ga-eval.md](docs/research/world-models-diffusion-ga-eval.md) (music-product surfaces, DEFER — different scope).
- **J4-data (the real near-term investment)**: instrument the existing harness to log the transition schema above — natural extension of J2's forecast ledger + J1's presence snapshot. Own tracer bullet: one workflow (e.g. post-merge smoke) emitting one typed transition record per run to `state/transitions/`, schema versioned in `docs/contracts/`.
- Paths: `../ix/` (owner), `state/quality/` (training data), contracts in `docs/contracts/`. **Tribunal: REQUIRED** (cross-repo, new ML surface).

### J5 — The Jarvis loop: observe → believe → predict → gate → act → learn [integration]

The vertical slice that makes the other four a system instead of four organs in jars. One real, low-stakes, recurring decision taken end-to-end:

- Presence layer (J1) observes an event → hari (J2) updates beliefs and forecasts outcomes of candidate responses → ix (J4) simulates the top candidates → Demerzel (J3) gates the chosen action against the boundary → harness executes → outcome feeds back into hari's calibration.
- **Candidate first decision**: "should the dead-letter sweep re-route this stranded issue to Jules, Codex, or a human?" — already automated, already bounded, already observable, and a wrong answer costs almost nothing.
- **Success criterion** (Karpathy rule 4): one fully audited loop iteration exists in the log — every step attributable, every prediction scored, every action inside the boundary — before any scope expansion.
- **Blocked on**: J1–J4 tracer bullets. Do not start this epic first; it is the *last* slice, not the vision statement.

---

## Spectral Music Intelligence Track (epics captured 2026-07-04)

Product-facing counterpart to the Jarvis Track. Groundwork + capability map + gap analysis: [docs/plans/2026-07-04-feat-spectral-music-track-groundwork-plan.md](docs/plans/2026-07-04-feat-spectral-music-track-groundwork-plan.md). Research basis: the frugal deep-research run wf_7b53e48d-cbc (3 confirmed / 12 refuted claims — **read the confidence markers below, several angles came back uninstructed and must be re-sourced before an epic freezes**). Organizing insight: **OPTIC-K is already the frozen encoder the JEPA recipe wants** (docs/research/2026-07-04-…-j4-world-models.md). Same tracer-bullet discipline as Jarvis. Gap verdict (Explore inventory): **no domain overhaul; one architectural ML item** (per-candidate scorer seam in the mmap scan) — the rest is composition of existing pieces.

### M1 — Chord⊂scale inclusion lattice [domain, low risk]

Exhaustive, *provable* over the 4096 pitch-class sets (finite-universe sweep doctrine, docs/solutions/architecture/2026-06-19). **Verified basis**: maximal-evenness theory (Clough & Douthett 1991; Tymoczko) is textbook-solid — the major scale is the canonical maximally-even 7-set, pentatonic the n=5 case (confirmed 3-0, though the primary PDF 403'd; re-cite Clough & Douthett directly). **Reuse**: `PitchClassSet.IsSubsetOf` + the full `PitchClassSet.Items` universe + `Scale.Items` (rich tonic-aware catalog). The pattern already exists as `PitchClassSet.GetCompatibleKeys()` scoped to 24 keys — generalize it.

- **Tracer bullet**: one `ContainingScales(PitchClassSet)` op + a cached lattice built by the existing sweep tool, plus a self-validation that **re-derives the maximally-even set counts per cardinality by enumeration** (the research flagged the n=6 count as `gcd(6,12)=6` configs, not "1 or 2" — the sweep settles it internally). Empty-diff invariant sweep = law holds.
- Paths: `Common/GA.Domain.Core/Theory/Atonal/PitchClassSet.cs`, `…/Tonal/Scales/`, `Tools/GaDomainInvariants`. **Tribunal: not required** (pure domain, no schema/embedding change).

### M2 — Chord→scale matching by Fourier phase distance [analysis]

Rank the M1 candidates by the ga#513 phase-aligned operator `S` between a voicing and each scale. Magnitude k=5 = diatonicity; its phase locates the key on the circle of fifths; modes = same set + ROOT partition. **HONESTY FLAG**: the deep-research **failed to independently validate** the Quinn/Amiot/Tymoczko phase-matching foundation (all such claims refuted 0-3, but *for citation fabrication, not for being disproven* — the Tymoczko PDF 403'd and the run had a systematic fake-citation problem). ga#513 verified its own theorems numerically and cites Lewin/Quinn/Amiot correctly; **before this epic ships, re-source Quinn 2006-07, Amiot 2016 (phase chapters), and Yust (DFT phase spaces / key-finding) from primaries** — open question left by the run.

- **Tracer bullet**: `S`-rank the diatonic scales for a handful of test chords, eyeball against theory (Cmaj7 → Ionian/Lydian/Mixolydian ordering), before any UI.
- **Blocked on**: M1 (the candidate set) + the M-arch scorer seam. Paths: `Common/GA.Business.ML/Retrieval/` (near `ModulationAnalyzer`, which already un-quantizes phases). **Tribunal: not required** (query-time, no re-index).

### M-arch — Per-candidate scorer seam in the OPTK scan [ML, the one architectural item]

`OptickSearchStrategy.SearchInternal` hardcodes `TensorPrimitives.Dot`; `IVoicingSearchStrategy` is pluggable at strategy level only. Introduce a clean `ICandidateScorer` (or a re-ranking pass) so every future operator — phase-aligned `S`, the TnI flag, Quinn-weighted variants — drops in without editing the parallel scan. **This is the only piece that needs deliberate design**; M2/M4 both depend on it.

- **Tracer bullet**: port the existing `WeightedPartitionCosine` to the seam with **byte-identical** results (pure refactor, provable by diffing top-K on the live index), then add `S` as the second scorer.
- Paths: `Common/GA.Business.ML/Search/OptickSearchStrategy.cs`, `Embeddings/EmbeddingSchema.cs`. **Tribunal: not required** unless it touches the index format (it must not).

### M3 — Arpeggio path generation on the fretboard [analysis, product]

Arpeggios = ordered sequences + fingerings, not sets. **Verified differentiation**: no mainstream tool combines spectral fretboard visualization with arpeggio ergonomics (research confidence low — the claim was refuted for citation fabrication, so treat differentiation as *plausible, not established*; the concurrent audio/product run wf_9dade0b0 will firm this up). v1 is **deterministic composition of existing pieces** — no ML. **Untouched by research**: fingering-cost prior art (Sayegh, Hori, Radicioni) was never searched — do that before building the cost function.

- **Reuse**: `ShapeGraphBuilder.GenerateShapes` (all fingerings of a PC-set) × `ShapeTransition.ComputePhysicalCost` / `PhysicalCostService.CalculateTransitionCost` × the existing `TabSequenceSolver`/`AdvancedTabSolver`. The gap is a multi-node **path** type chaining shapes (confirmed absent).
- **Tracer bullet**: generate one arpeggio path (e.g. Amaj7 across 3 positions) ranked by ergonomic cost, `t*` from ga#513 giving the transposed fingering. Paths: `Common/GA.Domain.Services/Fretboard/`, `Common/GA.Business.ML/Tabs/`. **Tribunal: not required**.

### M4 — Progression-JEPA: next-chord prediction in OPTIC-K latent space [ML + ix, research]

**Verified originality**: JEPA/SSL is applied to musical *audio* (Stem-JEPA ISMIR 2024, Audio-JEPA — notably data-efficient, <1/5 the data of wav2vec 2.0; MERT) but **no published JEPA for symbolic chord-progression prediction** (confirmed; absence is medium-confidence). **Verified data availability**: McGill Billboard (742 songs, Burgoyne et al. 2011), Hooktheory research set (18,843 sections), and **CHORDONOMICON (666k songs, arXiv 2410.22046, Oct 2024 — the largest symbolic progression corpus)** — re-cite from primaries, and **licenses/access terms of each are NOT yet verified** (open question). Data doctrine: synthesize training volume from GA's harmony grammars (owned, key-normalized, matches the equivariant loss), fine-tune/eval on these corpora; scarce faithful tabs = eval only.

- **Tracer bullet**: tiny predictor (per-partition MLP or ix's GBDT, ix#221) predicting the next voicing's OPTIC-K embedding from context, vs **honest baselines** (persistence, Markov-on-chord-symbols). Pause rule (Karpathy R4): if it can't beat Markov, the epic pauses and says so.
- **Blocked on**: M-arch (scorer seam), a `Progression`→embedding-sequence adapter (the DSL/GrothendieckService produces ICV deltas, not training sequences — new adapter needed), corpus license verification, and ix capacity. **Tribunal: REQUIRED** (cross-repo, new ML surface). Not to be confused with the parked J4 (process telemetry) — this is the *musical* application of the same frozen-encoder recipe.

### M5 — "Listen to me play": on-device audio input [ML, product — research-backed]

**Verified feasibility** (frugal run wf_9dade0b0, 17 confirmed / 4 refuted — this run worked because its evidence is on GitHub, not paywalled journals): **Basic Pitch (Spotify, Apache 2.0)** is the only permissively-licensed polyphonic option — exports ONNX/TFLite/CoreML (→ onnxruntime-web path), 22.05 kHz, 2 s windows, 86.13 frames/s — but it outputs **notes, not chord labels** (three heads: contour/note-presence/onset), so a chord-inference layer is GA's to write (chroma → template matching — the domain already has this). **CREPE (MIT)** is monophonic only; `tiny` runs ~13× faster than realtime on desktop CPU → fine for a tuner/single-note fallback, not chords. **Essentia is AGPL-3.0 → EXCLUDE** from a proprietary product. Onsets-and-Frames is archived (→ MT3, Apache 2.0, but T5X transformer — too heavy for browser). **Honest limits**: zero accuracy numbers survived verification (CREPE/Basic-Pitch benchmark figures live in ICASSP PDFs that 403'd; Basic Pitch has a known TODO that a contour conv layer was "unintentionally skipped" in the paper's training) — do NOT promise chord accuracy until an internal GuitarSet-like bench exists (barre chords, distortion, phone-mic AGC).

- **Tracer bullet**: Basic Pitch ONNX + onnxruntime-web in the browser → note stream → GA's own chord-inference (reuse the domain's chroma/template code) on ONE clean-signal test clip; measure real latency (the 2 s window is a structural floor — overlap-streaming required). No accuracy promise yet.
- **Blocked on**: verifying the published ONNX file actually loads in onnxruntime-web (panel split 1-0 vs 1-2 — check the repo directly). Paths: `ReactComponents/`, `Common/GA.Business.ML/`, domain chroma code. **Tribunal: not required** for the tracer (no schema); required if it becomes a scored product surface.
- **Open (needs a reachable-source re-run)**: guitar 6-string chord-recognition SOTA (GuitarSet, BTC, CRNN), real browser latencies, competitor tech/patents (esp. Yousician real-time-scoring patents) — all uninstructed; the audio landscape beyond model licenses needs question (2)–(4) re-run.

### Research reachability pattern (2026-07-04, load-bearing for all future research)

Five frugal runs this session split cleanly by **source reachability**, not topic difficulty: **spectral** (M1-M4) and **audio** (M5) produced real epics because their evidence lives on **GitHub / recent arXiv abstracts surfaced in search / product docs**; **agentic-engineering**, **motor-practice-science**, and (expected) **K-theory** returned near-zero because their evidence lives in **academic journals/PDFs the env proxy 403s at CONNECT** (arxiv.org, springer, NIH/NCBI, Nature, even Wikipedia — verified via `$HTTPS_PROXY/__agentproxy/status`; only GitHub-raw + package registries pass). **Implications**: (1) research questions here must be framed to target GitHub/product/doc evidence, or reframed to their reachable slice (e.g. FSRS/Anki are open-source repos; competitor apps have public docs — the *tooling* half of the motor-practice question is answerable even though the *science* half isn't); (2) genuinely academic questions need either an open-network environment or manually-fed papers; (3) the frugal workflow is now **v0.3** — reachability-aware source selection + a hard anti-fabrication rule (SOURCE_UNREACHABLE sentinel, no citing unread URLs, no stand-in sources) so blocked runs fail *honestly and cheaply* instead of hallucinating citations.

### Track hygiene note (research-process finding, 2026-07-04)

The wf_7b53e48d run refuted 12/15 claims — but the transverse finding is that **10 of those 12 refusals were citation fabrication** (two real URLs reused as sources for unrelated claims) plus a **poisoned-cache incident** (a synthesis memo stored as source text). Purged; `state/research-cache/` is now gitignored until the frugal workflow's write discipline is hardened (v0.3: reject non-source cache writes, block fetch-fail stubs). Several refuted claims (corpus licenses, product/legal facts, phase-theory primaries) are *plausible but must be re-sourced*, not treated as false. **Do not freeze any epic above on a refuted-for-citation claim without a clean primary source.**

---

## How to Start a Feature

```bash
/feature <idea from above>
```

The `/feature` skill will:
1. Brainstorm with GA MCP tools to verify the music theory
2. Produce a plan in `docs/plans/`
3. Guide implementation grounded in the GA domain model
