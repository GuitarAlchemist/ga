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

- **Extended / altered / sus / slash chord support in `chord-info`** — pros work daily with `Cmaj9`, `Dm11`, `G13`, `C7b5`, `C7#9`, `Calt`, `Cm7b5` (half-diminished), `Csus2`, `Csus4`, `Cadd9`, `C/E`. Today the `ga_chord_info` tool returns Error for all of these, and the SKILL.md correctly defers ("for altered or extended chords I'd need different tooling"). Implementation: extend `ChordSymbolRegex` to parse extension/alteration tokens and add formula entries. Likely needs a separate `ga_chord_extended` tool to keep the basic tool's contract clean. Also enables: chord-substitution and progression-completion to operate on extended-chord progressions (currently major/minor-only diatonic-set match).

- **Alternate tuning chord shapes** — DADGAD, drop-D, double-drop-D, open G/D/E, DGCGCD, all-fourths. A pro asks "give me a Cadd9 in DADGAD" or "what's the easiest A minor in drop-D?" The chatbot currently has no tuning context. New tool: `ga_chord_voicings(chord, tuning)` returning playable shapes ranked by fret-span. Composes with `ga_fret_span` for difficulty filtering.

- **Capo and key-transpose** — pros routinely play "song in concert E with capo 3" (the played shape is C major). New tool: `ga_transpose_progression(progression, semitones)` and `ga_capo_render(progression, capoFret)` returning the played-shape progression. New skill: `transpose` / `capo` SKILL.md gathering both.

- **Voice leading between two chords** — pros pick voicings to minimise common-tone movement (a hallmark of chord-melody and comping). New tool: `ga_voice_leading(chordA, chordB)` returning the optimal voice-leading pair from the precomputed OPTIC-K voicing index, reporting moves per voice. Complements existing `ga_chord_substitutions` (which ranks by ICV cost, not voice-leading smoothness — already documented as out of scope in the SKILL.md).

- **Chord identification from a note set** — `chord-info` SKILL.md mentions this gap: *"what chord is C E G?"* should return "C major triad" and ranked alternatives (C/Em with E in bass, etc.). New tool: `ga_chord_identify(notes)`.

- **Half-diminished + diminished-7 are partially in chord-substitution but missing in chord-info** — `m7b5` and `dim7` are valid suffixes in `ga_chord_substitutions`/`ga_chord_compare` (per their suffix table) but rejected by `ga_chord_info`. Inconsistency. Cheap fix: add the two formulas to `GetFormula(quality)` in `ChordMcpTools`.

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
- [ ] **`router-quality`** [ready] — Improve embedding router quality (Phase 3 next bottleneck per memory). Today's keyword-fallback fires too often; semantic similarity threshold needs tuning + an evaluation harness. Paths: `Common/GA.Business.ML/Agents/SemanticRouter.cs`, `Common/GA.Business.ML/Agents/Intents/SemanticIntentRouter.cs`, `Common/GA.Business.ML/Agents/RoutingFeedback.cs`. **Tribunal: REQUIRED**. Was task #81.
- [ ] **`m7b5-dim7-chord-info`** [ready] — Add `m7b5` and `dim7` to `GetFormula(quality)` in `ChordMcpTools`. They're valid in `ga_chord_substitutions`/`ga_chord_compare` but rejected by `ga_chord_info`. Trivial; smallest possible chatbot PR. Paths: `Common/GA.Business.ML/Mcp/Tools/ChordMcpTools.cs`. **Tribunal: REQUIRED** (Mcp path) — small change still goes through the gate, that's the iron law.

### P1 — capability completeness

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

### Scheduled / orchestrated elsewhere — don't pick manually

- [ ] **`qa-tribunal-phase-1`** [scheduled: trig_01WdRGSqgxah5PD46wg8u4Qq fires 2026-05-18] — Phase 1 of the QA Architect Tribunal. Schema is v0.1 draft; chatbot-iterate consumes the verdict shape but does NOT implement it. See memory `project_qa_architect_tribunal`.
- [ ] **`optick-sae-phase-1`** [scheduled: trig_01QUrKEsYLPPW4KNLzKZRE2n fires 2026-05-19] — Phase 1 of OPTIC-K Sparse Autoencoder. Cross-repo (ix crate + GA consumer + Demerzel orchestration). After Phase 1 lands, a follow-up "consume SAE features in TheoryAgent reasoning" item will be promoted into this track. See memory `project_optick_sae`.

### Parked

- **`frontier-llm-provider`** — `frontier-remote` purpose for `IChatClientFactory`. Reference Cloudflare Workers AI. **Preconditions** (per Infrastructure Ideas): (a) Ollama stable, (b) concrete user-facing task that local 3B fails at, (c) IChatClientFactory fully wired. NOT useful for OPTIC-K embeddings.

---

## How to Start a Feature

```bash
/feature <idea from above>
```

The `/feature` skill will:
1. Brainstorm with GA MCP tools to verify the music theory
2. Produce a plan in `docs/plans/`
3. Guide implementation grounded in the GA domain model
