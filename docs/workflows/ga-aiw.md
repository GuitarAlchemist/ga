# GA ↔ AIW workflow (product issues for AI workers)

> How product-facing work in `GuitarAlchemist/ga` is shaped, routed, implemented, and evidenced by AI workers (the **AI Workforce** — "AIW"). Implements the Epic 4 task of [`docs/roadmap.md`](../roadmap.md) (issue #482).
>
> AIW orchestration lives in **Demerzel** (GuitarAlchemist/Demerzel#473). GA is a **consumer**: it receives AIW-generated issues, implements the product ones, and writes evidence back. This doc is the GA-side contract so a worker can pick up an issue and finish it without guessing.

## 1. Lanes (who does what)

| Lane | Worker | Best at | Gate |
|---|---|---|---|
| **shape** | any model (local/cloud) | research, decomposition, issue authoring | human review of the resulting issues |
| **frontend** | build-capable **or** an npm-equipped sandbox | React/R3F/TS — locally verifiable (`npm run build`/`lint`/`tsc`) | CI |
| **backend (C#/F#)** | **build-capable worker only** (needs the .NET SDK) | services, MCP tools, DSL | CI + tribunal (if Agents/MCP/DSL) |
| **governance / CI** | human-in-the-merge-path | `.github/workflows/*`, schemas, contracts | **explicit human override** (blocked path) |

Route by capability, not by author. A sandbox without a .NET SDK should take **shape** or **frontend** lane work; C# implementation routes to a build-capable worker.

## 2. A "Matt-ready" product issue

An issue is ready for an AI worker (`ready-for-agent`) only if it states all of:

1. **Goal** — the user pain and what changes for them (one paragraph).
2. **Exact files in scope** — paths, not "the chat module".
3. **Non-goals** — what NOT to touch (prevents scope creep).
4. **Acceptance criteria** — verifiable, e.g. a test name, an endpoint contract, a metric threshold.
5. **Verification command** — how CI/a human confirms it (`dotnet test --filter …`, `npm run build`, a curl, a screenshot).
6. **Risk classification** — `pure-additive` / `refactor` / `api-change` / `one-way-door`. One-way doors (OPTIC-K dims, schemas, contracts, public API, pricing, `.github/workflows/*`) require human sign-off and may need `/council`.

If an issue can't state these, it's `ready-for-human` (needs shaping) — not agent work yet. Use the Matt Pocock skills (#481) or `/to-issues` to convert vague asks into Matt-ready slices.

## 3. Evidence requirements (definition of done)

Every AIW product PR carries evidence proportional to its surface:

| Surface | Required evidence |
|---|---|
| Any | passing CI (build + tests); `Fixes #N`; conventional commit |
| Backend logic | unit/integration test named in acceptance criteria; key test output quoted in the PR |
| User-visible UI | **before/after screenshot or short capture** of the rendered change |
| Perf claim (e.g. "+N FPS") | the measurement, or an explicit "needs human FPS verification" note if the worker can't run a browser |
| Metric-moving change | baseline + direction + guardrail (`state/quality/` snapshot), per the Karpathy rule |
| One-way door | `/council` verdict or named human override |

**Written-blind disclosure:** if a worker authored code it could not build/run locally (no SDK/browser), it must say so in the PR and name CI as the verification gate. Honesty about what was *not* verified is part of done.

## 4. Tracer-bullets, not horizontal layers

Per the ecosystem's aihero delta (see `CLAUDE.md`): a product slice is a **thin vertical** cutting through every layer (data → service → API → UI), tested end-to-end — never a layer built in isolation. AIW issues should describe one tracer-bullet with a single measurable success criterion.

## 5. Loop back to Demerzel AIW gates

- AIW-generated issues arrive from Demerzel; GA implements the **product** ones (UI/gameplay/audio/theory UX) and leaves governance/runtime ones to their lane.
- Autonomous-loop runs record evidence to `state/quality/loops/` and are scored by the ix `maintain-gate` ([#428](https://github.com/GuitarAlchemist/ga/issues/428)) — advisory, never auto-revert.
- The Galactic-Protocol / tribunal verdicts and contracts live in `../Demerzel/` and `docs/contracts/`; GA reads them, doesn't re-implement them.

## 6. Worker checklist (copy into the PR)

```text
[ ] Lane correct for my capability (didn't blind-write C# I couldn't verify)
[ ] Only the files in scope changed (non-goals respected)
[ ] Acceptance criteria met + verification command run (or CI named as the gate)
[ ] Evidence attached (test output / screenshot / metric / written-blind disclosure)
[ ] Risk class stated; one-way door → human/council sign-off
[ ] Fixes #N + conventional commit + roadmap table updated if an epic shifted
```
