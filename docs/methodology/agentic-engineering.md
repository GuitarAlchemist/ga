# Agentic Engineering — the harness is the work

> A read-on-demand reference, **not** an always-loaded instruction block. Distilled from Matt
> Pocock's "Agentic Engineering Workflow" (aihero.dev) + Ousterhout's *A Philosophy of Software
> Design*, and mapped to this ecosystem's existing machinery. Read it when you're deciding *how* to
> direct AI on a non-trivial change — not on every turn.

## The one idea

**Optimise the harness, not the model.** The model is the engine; the *harness* — prompts, skills,
the codebase itself, the environment the agent runs in — is roughly half the system and the half you
fully control. The load-bearing consequence:

> *"How do you optimise token spend? Have a codebase that's easier to make changes in."*

A deeper, lower-duplication, better-documented codebase lets a **cheaper model** do the same work with
fewer tokens, because the guardrails are tighter and there's less head-banging. Hamstring the codebase
and you'll need an expensive model just to cope.

## Strategic over tactical

AI ate **tactical** programming (writing syntax, chasing bugs, making commits) — it's cheaper and
faster than you at it. Your leverage is **strategic** programming (Ousterhout):

- **Design the hard parts up front.** Decide the consequential things before delegating.
- **Scope tasks tightly.** A well-scoped task is one an AFK agent can finish with no further context.
- **Own the interfaces / seams between modules.** This is where bugs and rework concentrate.
- **Keep just-enough docs that point agents to the right place** — not exhaustive, navigational.

"Your skills are the ceiling on what AI can do." Delegate the tactical; keep the strategic mindset.

## DX ≈ AX

Agent experience ≈ developer experience. What makes a codebase pleasant for a senior human makes it
tractable for an agent: **deep modules** (a lot of behaviour behind a small interface), **low
duplication**, **clear seams**, **guardrails** (types, tests, invariants). Improving the codebase
*is* improving the harness — the most overlooked lever. In this repo that's the
[`/improve-codebase-architecture`](../../.claude/skills/improve-codebase-architecture/SKILL.md)
vocabulary (module / interface / depth / seam / deletion test); recent worked examples:
`EmbeddingSchema` layout ops, the shared `ChordVocabulary`/`KeyNaming`/`IntervalNaming` parser seams,
and `PitchClassSetId` as the canonicalization authority.

## Procedures vs abilities (and context hygiene)

- **Procedure** — a skill *you* invoke to stay in the driver's seat (`/grill-me`, `/to-prd`,
  `/to-issues`, `/improve-codebase-architecture`). Prefer these; keep the thinking in the human.
- **Ability** — a skill the *model* self-invokes (coding standards it pulls in mid-task). Every ability
  leaks its description into the context window. Too many = bloat.

Matt's blank-slate test: periodically strip skills / MCP / CLAUDE.md back toward nothing, watch what
the agent does unaided, then **layer back only the procedures you deliberately choose**. Mark
procedures `disable-model-invocation: true` so they don't leak into context. Treat a long CLAUDE.md as
a smell — push detail into read-on-demand docs (like this one) and keep the always-loaded surface lean.

## Queues, not loops

The unit of AFK work is a **queue** of well-scoped tasks, not an infinite prompt loop (that just burns
tokens). Tasks flow **triage → explore → implement → review → merge**, pulled off by labelled agents.
This repo already speaks this: GitHub Issues + the canonical triage labels
(`needs-triage`/`needs-info`/`ready-for-agent`/`ready-for-human`/`wontfix`, see
[docs/agents/triage-labels.md](../agents/triage-labels.md)), and the `/auto-optimize` /
`supervised-loop` machinery. Keep **human-in-the-loop checkpoints**, but push them as far toward the
final output as the work safely allows.

## Build self-improving systems

When a model finds a deep bug, the lesson is **not** "the model is great" — it's *"I should have a
system that catches this."* Prefer a cheap, scheduled review (an Action/cron that sweeps a rotating
slice of the repo for bugs/security) over waiting for a smarter model. *"If someone keeps stealing
your bike, buy a lock."* You're reviewing **the system that produces the code**, not just the code.
In this repo: the `state/quality/` baselines, the daily quality snapshots, and the roundtrip
validators are exactly this — extend them rather than one-shotting fixes.

## Make review seamless

The bottleneck is human review, so spend the harness on making review *fast*: rich PR context, AI-
assisted review passes (multi-LLM review has repeatedly caught real bugs here — see
`feedback_multi_llm_review_pays_off`), and structured diffs over raw GitHub. You stay the gate on
security and on "did the system do a good job," but you make that gate one click, not a debugging
session.

## Which agent, and what makes a config reliable + cheap

The corollary of "optimise the harness, not the model": **reliability comes from the harness, not the
vendor**, and the cheapest *reliable* config is almost never "use the free external agent." Grounded
in this ecosystem's own evidence (the 2026-07-04 delegation session):

- **Subscription Claude Code + background sub-agents delivered 100 %** — every module, fix, exposure,
  and research pass — with **zero external-secret fragility**. The Jules lane, the same day, **broke
  on a missing pay-per-use `GEMINI_API_KEY`** in the `gemini_dispatch` triage job, is async/slow
  (hours), and had earlier shipped a real regression that needed a hand-fix. Jules didn't fail as an
  *agent* — it failed on *metered infra*, the exact anti-pattern the cost doctrine guards against.

The reliable-and-cheap config, in order:

1. **Spine = Claude Code on a flat subscription** (main loop + sub-agents). No pay-per-use fallback
   (cost doctrine). Flat rate, no per-token surprise — the CA$13.64 recharge that *created* the
   doctrine was pay-per-use API.
2. **Fan-out = background sub-agents** for parallel independent work — same subscription, ~$0 at the
   margin, no external secret to expire. This is what actually ships.
3. **Model tiering is the real cost lever, not the vendor choice** — Haiku for scout/mechanical,
   Sonnet for verify, Opus/Fable for synthesis/hard reasoning (the frugal-workflow pattern).
4. **External async agents (Jules / Codex) = opportunistic AFK bulk only, never on the critical
   path.** Their compute is free — treat that as *bonus throughput*, not the backbone. A broken
   external lane must never block delivery. Feed hari's G2 reliability model (`hari-core reliability`)
   from PR grades so "which agent, for which task class" becomes measured, not assumed.
5. **The reliability multiplier is verifiers, not pricier agents.** Exhaustive sweeps, the Brier
   ledger, the tribunal, CI gates catch bad output for a fraction of the cost of a smarter model —
   the discovery-engine insight (generator + mechanical evaluator) applied to the dev process itself.

The reframe: the cheapest config is not the free vendor, because its hidden costs — latency, the
regressions you hand-fix, review friction, the secret that expires — exceed the token savings. The
cheapest **reliable** config is **flat subscription + model tiering + strong mechanical verifiers**.

## You own the product

AI is weak at original ideas and at deciding *what* to build. Choose the features; ask "what can I
**remove**, how do I make this **simpler**." Talk to real users. The classic product-design
fundamentals still hold — AI just implements them faster.

## The two action steps Matt actually recommends

1. **Strip to a blank slate, then layer deliberately.** Remove the bloat; re-add only procedures you
   choose and can customise.
2. **Move work AFK.** Scope a task tightly, hand it to a sandboxed agent, review the result. Two of
   you, then three, then five — then you review.

---

*Pointer, not gospel: this doc is read when you're deciding how to direct a non-trivial change. It is
deliberately not wired into the always-loaded instruction set — that would contradict its own
context-hygiene advice.*
