---
title: Skill Stewards — permanent agent team for the SKILL.md iteration loop
date: 2026-05-05
reversibility: two-way-door (agent definitions are markdown, easily revisable); one-way-door (the dual-watch architecture in FileBasedSkillsProvider — switching back to single-watch would break all in-flight drafts and is a behavioural break for any consumer that grew to expect overlay semantics)
revisit_trigger: chatbot transcripts produce daily skill-archaeologist signal AND ix Phase 3 action layer ships, OR more than 50 SKILL.md files accumulate (current count is 13 canonical + 0 drafts)
status: design — `skill-author` and `skill-graduator` shipped this PR. `skill-iterator`, `skill-maintainer`, `skill-archaeologist` await IX Phase 3 + an eval harness.
---

# Skill Stewards — permanent agent team

## Problem

The SKILL.md ecosystem is now load-bearing on the GA chatbot's dispatch
surface (5 PRs of it shipped 2026-05-05: #110 #111 #113 #114 #115).
Authoring, iterating, and retiring skills is currently ad-hoc:

- New skills are hand-written; format mistakes (PascalCase vs camelCase,
  missing triggers, body-too-long) only surface on parser test runs.
- Iteration loop is open: edits to a SKILL.md require chatbot restart
  unless the author knows about live-reload (and points at the right
  directory).
- Graduation is implicit: when does a draft become canonical? No
  explicit threshold, no audit trail.
- Drift goes unnoticed: a skill whose triggers haven't fired in 60 days
  might be redundant, broken, or shadowed — nobody checks.
- Failure-pattern mining is unused: chatbot transcripts contain queries
  that DON'T match any skill, but no process turns those into proposed
  SKILL.md drafts.

This plan defines a permanent five-role agent team to close all five
gaps. The first two roles ship in this PR; the remaining three are
gated on the IX session's Phase 3 action layer (the cron-driven
"actor" that turns evals into PRs).

## The team

### 1. `skill-author` (on-demand) — SHIPPED

Drafts a new SKILL.md in `skills-dev/<name>/` from a description. Runs
the parser pre-flight to confirm the stub validates. Refuses to
shadow a canonical or existing draft of the same name. See
[`.claude/agents/skill-author.md`](../../.claude/agents/skill-author.md).

### 2. `skill-graduator` (on-demand) — SHIPPED

Promotes a stable draft from `skills-dev/` to `skills/`, validates it,
commits on a branch, opens a PR. Refuses to graduate parser-invalid
files OR silently replace canonical skills. Does NOT modify content
during graduation — relocation + validation only. See
[`.claude/agents/skill-graduator.md`](../../.claude/agents/skill-graduator.md).

### 3. `skill-iterator` (on edit, debounced) — DESIGN

Triggered by a `FileSystemWatcher` event on `skills-dev/<name>/SKILL.md`.
After a 2-second debounce (longer than `FileBasedSkillsProvider`'s
200 ms reload debounce — author may save several times in a row), runs
a configured eval fixture against the skill and posts a diff vs the
prior pass.

Eval fixture format (proposed): `skills-dev/<name>/eval.yaml` with a
list of `prompt: expected_substring_OR_metadata_check` pairs. The
iterator runs each prompt against the chatbot endpoint
(`http://localhost:5252/api/chatbot/chat` if running locally), captures
the response, and asserts that the configured substring is present
AND the routing metadata names this skill.

Blockers:
- Requires a stable chatbot running locally (or a CI runner that
  starts one). Today this is gated on the Ollama wedge being
  resolved — chat models need to actually load.
- Eval fixture format isn't defined; it cross-cuts the existing
  `state/quality/chatbot-baseline-*.json` and would benefit from a
  shared schema with the IX action layer's planned drift snapshots.

Build trigger: when Ollama unwedges OR when the IX Phase 3 daily
snapshot lands and we have a hosted chatbot the iterator can hit.

### 4. `skill-maintainer` (daily cron via Demerzel) — DESIGN

Runs daily at 09:00 UTC. Scans canonical `skills/` and produces a
maintenance report:

- **Dead triggers**: skills whose triggers haven't fired in the last
  N days (configurable; default 60). Surfaces via the chatbot's
  routing telemetry (which exists: `AgentRoutingMetadata.AgentId` is
  recorded per request).
- **Drift candidates**: skills whose body references an
  `IOrchestratorSkill` class that no longer exists (renamed,
  retired) — requires a static analysis pass over `Common/GA.Business.ML/Agents/Skills/`.
- **Missing triggers**: canonical skills with zero surviving
  triggers (parser drops triggers below `MinTriggerLength`; if a
  skill ends up with none, it's silently un-dispatchable).
- **Format drift**: skills authored in PascalCase that should be
  migrated (the parser still accepts them but the convention is
  camelCase).

Output: a markdown report posted as an issue on the GA repo, OR (in
autopilot mode) a PR draft per maintenance category.

Blockers:
- Daily routing telemetry is recorded ad-hoc in
  `state/telemetry/voicing-search/` but not aggregated for skill-level
  hit counts. Needs the IX action layer's daily snapshot pattern.
- Demerzel cron infrastructure exists (the QA Architect Tribunal
  trigger `trig_01WdRGSqgxah5PD46wg8u4Qq` proves the pattern) but no
  GA-side cron has been wired yet.

Build trigger: when IX Phase 3 daily snapshots ship AND the GA cron
inbound is wired (~2.5 days of GA work per the IX session's plan).

### 5. `skill-archaeologist` (weekly cron via Demerzel) — DESIGN

Runs weekly. Mines chatbot transcripts (the queries users sent the
public chatbot at `https://demos.guitaralchemist.com/chatbot/`) for
patterns that DIDN'T match any canonical skill — i.e. prompts that
fell through to the LLM-only path.

Clusters those queries (semantically, via embedding similarity), and
for each large enough cluster (>= 5 distinct queries in a week)
proposes a new SKILL.md draft via the `skill-author` agent. Output is
a PR with the draft pre-populated.

This is the closest match to Karpathy's "LLM Knowledge Base" thesis:
AI maintains the skill library based on real interactions, not on the
human author's a priori imagination of what users would ask.

Blockers:
- Chatbot transcripts aren't currently persisted in queryable form.
  `ConversationHistoryStore` is in-memory; the SignalR / SSE traffic
  is logged to `state/telemetry/` ad-hoc.
- Embedding similarity over user queries is a separate pipeline;
  could reuse OPTIC-K embeddings but the encoder's musical
  specialisation may not generalise to "what is a chord" plain
  English.

Build trigger: when chatbot transcript persistence + embedding lookup
ship. Sized at ~1 week of GA work; sequenced AFTER `skill-maintainer`.

## Architectural integration

### Today (this PR)

```
skills-dev/<name>/SKILL.md  ←  skill-author (on-demand)
       │
       │  (live-reload via FileBasedSkillsProvider multi-watch)
       ▼
GA chatbot dispatches against the draft
       │
       │  user iterates
       ▼
skills/<name>/SKILL.md  ←  skill-graduator (on-demand, signoff-driven)
```

### Once IX Phase 3 ships

```
                   skill-archaeologist (weekly cron)
                            │
                            │ proposes drafts from transcripts
                            ▼
skills-dev/<name>/SKILL.md  ←  skill-author (on-demand)
       │                  ←  skill-iterator (on edit, eval diffs)
       ▼
GA chatbot dispatches against the draft
       │
       ▼
skills/<name>/SKILL.md  ←  skill-graduator (on-demand)
       │
       │  daily scan
       ▼
skill-maintainer report → triage issues → retire / migrate / fix
```

## Why this doesn't bloat the agent inventory

The five roles are all `Skill` or `Agent` definitions — markdown files
under `.claude/agents/`. They're loaded by Claude Code on session
start (zero runtime cost when not invoked) and by Demerzel cron jobs
on schedule. No new daemon, no new database, no new infrastructure
beyond what already exists for QA Architect Tribunal.

The on-demand roles (`skill-author`, `skill-graduator`) are pure
markdown agent definitions — same shape as a SKILL.md, which is the
elegance: this team's definitions ARE skills under the parity contract
that PR #113 established.

## Open questions for the user

1. Do we want a **`.gitignore` exception for `skills-dev/`** (drafts
   committed, tracked alongside main code) OR is `skills-dev/` ephemeral
   per-author? Current default: tracked, short-lived branches.
2. Should `skill-iterator` post eval diffs to **GitHub PR comments** OR
   **a Slack channel** OR **the Demerzel governance UI**? Defaulting
   to PR comments for now.
3. Should `skill-archaeologist`'s **embedding clustering** reuse
   OPTIC-K (musical-specific) OR pull in a general-purpose text
   encoder (sentence-transformers, nomic-embed-text via Ollama)?
   Sentence-level encoder is probably the right call for plain
   English queries; OPTIC-K excels at musical content but generalises
   poorly.

## Blast radius / one-way doors

- **Two-way doors** (revisable easily): all five agent definitions,
  the eval fixture format, the cron schedule, the report destinations.
- **One-way door**: the dual-watch architecture in
  `FileBasedSkillsProvider`. Once consumers depend on overlay
  semantics (drafts shadow canonical), reverting to single-watch
  silently breaks every in-flight draft. Pinned by 5 new tests in
  `FileBasedSkillsProviderReloadTests`.

## Pinned by tests

- `FileBasedSkillsProviderReloadTests.MultiDir_*` — 5 cases: shadow,
  non-overlap, missing-dir tolerance, graduation flow, constructor
  validation.
- `SkillMdParserTests` — 23 cases (parity + casing + bounds + mixed-
  casing data-loss prevention).
- The `skill-author` and `skill-graduator` agents themselves run
  `dotnet test --filter "FullyQualifiedName~SkillMdParserTests"` as
  pre-flight.
