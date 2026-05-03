# Chatbot Phase 3 Scoping — IX integration + GA DSL leverage

**Status**: Scoping (drafted 2026-05-03). Not yet committed for implementation.
**Predecessor**: `2026-05-03-chatbot-skill-md-migration-completion.md` — that workstream finished. This doc proposes what comes next.
**Goal**: turn the chatbot from "catalog lookup with nice phrasing" into something that does real music-theoretic reasoning.

---

## Context

After the 2026-05-03 SKILL.md migration (13 PRs, all 10 `IOrchestratorSkill` impls have SKILL.md equivalents, 7 MCP tools under `ga_*` prefix), the chatbot can:

- Look up chord notes deterministically (e.g. `Cmaj7` → `C E G B`).
- Identify a key from a chord progression.
- Compute interval / scale / fret-span info.
- Find chord substitutions ranked by Grothendieck ICV distance.

What it **can't** do:

- Search forward in chord-space for the strongest 3-chord cadence to end a phrase.
- Detect modulation across a progression (today returns one key for the whole input).
- Find chord transitions with smooth voice leading (only static set similarity).
- Return style-conditioned suggestions ("end this in bossa nova style").
- Score playability empirically (`1 + span × 2` is hand-waving).
- Compose multi-step queries ("find voicings of this chord with low fret span and good voice leading from the previous chord").

The first five are **IX integration** opportunities. The last is a **GA DSL leverage** opportunity (and probably the one that compounds best — every future skill benefits).

---

## Workstream A — IX integration (5 proposals)

[`ix`](../../../../ix/) is a Rust workspace with 65 crates exposing 68 MCP tools to Claude Code. Stable subset relevant here: `ix-search` (A*, MCTS, minimax), `ix-graph` (HMM/Viterbi, Markov chains), `ix-ensemble` (gradient-boosted trees), `ix-supervised` (KNN, regression, trees), `ix-unsupervised` (KMeans, DBSCAN, PCA, t-SNE, GMM).

Each proposal below: new `ga_*` MCP tool in the chatbot's in-process server, calling into IX for the heavy lift. Same template as the migration we just did.

### A1. MCTS-driven progression completion (`ix-search`) — 🥇 most fun

**New tool**: `ga_progression_complete_mcts(query, depth=3, branchFactor=4)`

**Currently** `progression-completion` SKILL.md picks 2–3 cadences from the diatonic set via LLM. **Replace** with MCTS searching `depth` chords ahead, scored by:

| Reward term | Source |
|---|---|
| Voice-leading smoothness | `KeyIdentificationService` diatonic set + chord-tone overlap arithmetic |
| Cadential strength | Static table (V→I = 1.0, IV→I = 0.7, deceptive = 0.5, half = 0.3) |
| Diatonic preference | Penalty for chords outside `TopCandidates[0].DiatonicSet` |
| Length penalty | Longer continuations get diminishing returns |

**New capability**: *"Continue this 4-bar phrase — give me the strongest 3-chord ending"* gets a searched answer, not a catalog pick.

**Effort estimate**: 3–4 PRs. M (medium-complex). The reward function is the hard part — needs careful authoring with worked examples for "feels right" vs "technically optimal".

**Risk**: reward function authoring is subjective. Mitigation: ship behind a `?style=mcts` flag and A/B against the LLM-pick path on a held-out prompt set.

### A2. Modulation detection via HMM/Viterbi (`ix-graph`) — 🥇 highest impressiveness/effort ratio

**New tool**: `ga_key_modulation_detect(query)`

**Currently** `key-identification` SKILL.md picks **one key** for the whole progression. **Replace** with Viterbi over the chord sequence:

- States: 24 major/minor keys.
- Emissions: chord qualities (P(C major | C major key) > P(C major | F major key)).
- Transitions: cost matrix penalizing distant-key modulations, favoring closely-related keys (relative, parallel, dominant, mediant).

**Returns**: `{ Chord, MostLikelyKey, Confidence, IsModulation }[]` — a per-chord key annotation.

**New capability**: *"Where does this song change key?"* gets a chord-by-chord annotated output. Genuinely sophisticated theory the LLM cannot do reliably.

**Effort estimate**: 1–2 PRs. M (the model is small; emission and transition matrices fit in a few hundred lines). HMM/Viterbi from `ix-graph` is in IX's stable tier per its README.

**Risk**: parameter tuning. Mitigation: pin transition costs from music-theory literature (Lerdahl, Krumhansl key-distance), validate against canonical modulating examples (Beatles "Eight Days a Week", "Penny Lane").

### A3. Voice-leading-cost search for substitutions (`ix-search` A*) — 🥈 incremental

**New tool**: `ga_chord_substitutions_voiced(chordA, prevChord)`

**Currently** `chord-substitution` ranks by Grothendieck ICV (static set similarity). **Replace** the ranking with A* search where the heuristic is voice-leading cost between the previous chord and each candidate substitution:

- State: a chord (its pitch-class set + nearest voicing to `prevChord`).
- Cost: voicing-distance metric (sum of semitone moves between voices).
- Goal: the candidate set with lowest total voice-leading cost.

**New capability**: substitutions that actually sound smooth in context, not just contain similar pitch classes.

**Effort estimate**: 2–3 PRs. M-large. Needs voicing search (probably leverages existing OPTIC-K voicing index) before it can run.

**Risk**: if the voicing index doesn't already serve this query shape, becomes 4–5 PRs and crosses into the OPTIC-K workstream's territory.

### A4. Style-conditioned next-chord prediction (`ix-ensemble` GBT) — 🥉 needs data

**New tool**: `ga_progression_predict_next(query, style)`

Train a gradient-boosted-tree on a corpus of style-labeled chord progressions. Input: previous N chords + style tag. Output: probability distribution over the next chord.

**New capability**: *"Suggest a continuation in bossa nova style"* becomes a tool call to a trained classifier.

**Effort estimate**: 5–7 PRs. L (large). Blocked on:

1. Style-labeled corpus acquisition (likely needs licensing — public datasets like Hooktheory, McGill Billboard, JAAH are partial fits).
2. Training pipeline (one-off Python or Rust training, then export model weights to `ix-ensemble`).
3. Inference wrapper.
4. Style-set definition (which 5–10 styles are first-class?).

**Risk**: data licensing is the realistic blocker. Defer until corpus question is resolved.

### A5. Empirical fret-span scoring (`ix-unsupervised` KMeans) — 🥉 incremental

**New tool**: `ga_fret_span_empirical(diagram)`

**Currently** `playabilityScore = 1 + span × 2`. **Replace** with score from KMeans clusters fit on a corpus of human-rated chord diagrams.

**New capability**: same answer shape but grounded in data instead of arbitrary math.

**Effort estimate**: 3–4 PRs. M. Blocked on a small labeled dataset (~1000 diagrams with playability ratings); could bootstrap from existing voicing telemetry.

**Risk**: dataset labeling is the work. Probably defer until a more impactful proposal lands first.

---

## Workstream B — GA DSL leverage (4 options)

GA already has `Common/GA.Business.DSL` — an F# DSL layer for music theory and practice routines, with parsers, generators, grammars, and LSP support. It's already exposed as MCP tools via `GaMcpServer/Tools/GaDslTool.cs` (~80 `[McpServerTool]` methods including `GaParseChord`, `GaChordIntervals`, `GaDiatonicChords`, `GaSearchVoicings`, `GaInvokeClosure`, …).

The chatbot's in-process MCP tools (the 7 we just shipped) **don't talk to** `GaMcpServer`. They run inside the chatbot service via `InProcessMcpToolsProvider`. So there's clear **duplication and missed leverage** — `GaChordIntervals` (DSL) ≈ `ga_chord_info` (chatbot).

The user's question: should the chatbot be able to use the broader GA DSL surface?

### B1. Bridge to external GA DSL MCP server — 🥇 highest leverage / lowest code

`InProcessMcpToolsProvider` adds a delegating tool that forwards calls to `GaMcpServer` (running as a sibling process or the same one). Every `Ga*` tool in `GaMcpServer` becomes available to chatbot SkillMdDrivenSkill instances.

**New capability**: chatbot LLM can call `GaSearchVoicings`, `GaInvokeClosure`, `GaProgressionCompletion`, etc. without us re-implementing them.

**Effort estimate**: 1 PR. S (small). The MCP framework's standard client transport already supports this — it's what Claude Code does to talk to `GaMcpServer`. We just need to start the chatbot as a parallel client.

**Risk**: process-boundary trust model. `GaMcpServer` has tools that take side-effects (`io.*`, `agent.*`, `tab.*` closures — already gated by an allowlist per the source). Need to confirm the chatbot only sees the safe subset; ideally the chatbot opens the connection with a scoped allowlist.

**Why this is high-leverage**: every existing GA DSL operation immediately becomes a chatbot capability. No re-porting. No drift between two implementations of the same domain operation.

### B2. Generated wrappers for each GA DSL tool — 🥈 strong hygiene, more code

Code-gen a `[McpServerToolType]` wrapper in `Common/GA.Business.ML/Agents/Mcp/` for every GA DSL tool we want chatbot-accessible. Each wrapper applies the chatbot's input hardening (length guards, `McpEchoSanitizer`) before delegating.

**New capability**: same as B1 but with explicit per-tool Result types and our hardening layer.

**Effort estimate**: 6–8 PRs (small ones, mostly mechanical). M total. Need a code-gen step or template-based authoring.

**Risk**: maintenance burden — every GA DSL tool change forces a chatbot wrapper update. Drift risk over time. Probably not worth it unless we want very different surface shapes between the two consumers.

### B3. Eval-based integration — 🥇 most expressive

**New tool**: `ga_dsl_eval(script)` exposing `GaInvokeClosure` (or the underlying F# DSL eval).

The LLM can compose multi-step queries:

```
chord = ga_dsl_eval("parseChord(\"C7#9\")")
voicings = ga_dsl_eval("searchVoicings(chord) |> rankBy voiceLeading")
```

**New capability**: compositional queries the LLM authors on the fly. *"Find voicings of C7#9 with low fret span and good voice leading from Bbm7"* becomes a single eval call rather than a chain of tool calls the LLM has to compose by hand.

**Effort estimate**: 1 PR if the closure / eval surface already exists (per `GaInvokeClosure` / `GaListClosures`). M total.

**Risk**: HIGH — exposing a script-eval surface to LLM-generated input is a sandbox question. The DSL has io/agent/tab closures with side effects; even with the existing allowlist, an LLM emitting hostile script is a real attack surface.

**Mitigation**: only allow the read-only / pure-compute closure subset (`ga.*` excluding `io.*`, `agent.*`, `tab.*`). The allowlist already exists in `GaDslTool.cs`; we just have to ensure the chatbot's eval call passes it through.

### B4. Script-level skills — 🥉 architectural overhaul

Today SKILL.md is markdown + frontmatter. Allow SKILL files written in **GA DSL** instead — a SKILL.gas file is a script the LLM runs deterministically, no prompt at all.

**New capability**: certain skills become 100% deterministic — no LLM call. The "interval" skill, for example, becomes a 3-line GA DSL script.

**Effort estimate**: 8–10 PRs. L. Touches `SkillMdLoader`, `FileBasedSkillsProvider`, the SkillMdPlugin, the Agent Framework spike — every place that currently consumes SKILL.md.

**Risk**: BIG architectural change. Probably not worth it unless we have a critical mass of skills that fit the script-only shape.

---

## Recommended sequencing

### Phase 1 (next 1–2 sessions) — pick 1 high-leverage thing to ship

**Top recommendation**: **B1 (DSL bridge)** + **A2 (modulation detection)**, in that order.

**Why**:

- B1 unlocks the entire existing GA DSL surface (~80 tools) for chatbot use with one PR. Multiplies what every future skill can do.
- A2 lands a marquee new capability the chatbot literally couldn't do before. Demoable, defensible, finishes in 1–2 PRs against `ix-graph`'s stable surface.
- A1 (MCTS) is the most fun but the reward function is subjective and would benefit from B1 being in place first (so the reward function can call into the broader DSL for voice-leading metrics, etc.).

### Phase 2 (after Phase 1) — search-driven capabilities

**A1 (MCTS completion)** + **A3 (A* voice-leading substitutions)**. Both depend on B1 being in place so the reward / cost functions can call into the broader DSL surface.

### Phase 3 (data-blocked) — empirical / learned scoring

**A4 (style-conditioned GBT)** + **A5 (empirical fret-span)**. Both blocked on dataset acquisition. Consider deferring until the data question is independently resolved.

### Defer indefinitely

**B2 (per-tool wrappers)** — strictly worse than B1. **B3 (DSL eval surface)** — high security risk, hold until the chatbot has an authentication / scoping story. **B4 (script-level skills)** — speculative architectural overhaul.

---

## Open questions for the user

1. **Phase 1 priorities — confirm or change?** Is B1 + A2 the right "ship one impressive thing" for next session, or would you rather start with A1 (MCTS completion) for the user-facing "wow" demo?
2. **DSL bridge trust model**: which closure subset of `GaInvokeClosure` should the chatbot have access to? The existing `GaDslTool` allowlist is a good starting point — confirm or refine.
3. **Modulation-detection cost matrix source**: pin transition costs from Lerdahl 2001? Krumhansl 1990? A custom GA-tuned matrix? Affects "feel" of the answer.
4. **Demo target**: what's the canonical query that would let us know A2 works? *"Where does Eight Days a Week change key?"* would be a satisfying validator.

---

## Reversibility & risk

Every proposal in this doc is structurally a **two-way door**:

- New `ga_*` MCP tools add to `GaPlugin.McpToolTypes`; remove the registration to roll back.
- New `skills/<name>/SKILL.md` files are additive markdown; delete to roll back.
- `B1` (DSL bridge) is the only one with a non-trivial revert — once chatbot consumers depend on the bridged tools, removing the bridge breaks them. Mitigation: introduce behind a feature flag with a measured soak before declaring committed.

No one-way doors proposed in this scoping. Any specific implementation PR may introduce one (e.g. a new public Result type shape becomes a one-way door once external code consumes it) and should be flagged at PR time.

## Cross-references

- Migration completion report: `docs/plans/2026-05-03-chatbot-skill-md-migration-completion.md`
- Migration recommendation: `docs/plans/2026-05-03-chatbot-agent-framework-migration-recommendation.md` (Phase 4 A2A and Phase 5 workflow remain deferred — independent of this scoping)
- IX repo: `../../../../ix/README.md` for crate inventory
- GA DSL MCP exposure: `GaMcpServer/Tools/GaDslTool.cs`
- Memory entry: `memory/project_chatbot_skills_migration_2026_05_03.md`
