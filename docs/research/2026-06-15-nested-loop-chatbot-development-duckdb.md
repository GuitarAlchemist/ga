# Nested-loop development for the GA chatbot, with DuckDB as the state backbone

**Date:** 2026-06-15
**Type:** research + design synthesis (not a commitment to build)
**Question:** Can we structure the *development* of the GA chatbot as **nested loops** — in the spirit of Claude Code `/loop` and `/goal` — using **DuckDB as the durable backbone**?

## TL;DR

**Yes — and you've already built ~80% of it.** The pattern (fast inner loop + slow outer loop, each gated by a verifier, convergence tracked in a durable store) is well-established and now first-class in Claude Code. GA already has the oracles (chatbot-qa corpus, RoutingEvalHarness, the ix-duck flight recorder), the loop executor (`/auto-optimize`), the DuckDB analytics layer (`quality.duckdb`), and a loop/goal tracker. What's missing is **the connective tissue that makes "is the loop converging?" a SQL query**: a per-iteration loop ledger, plateau detection, and wiring the flight-recorder gate into CI.

**The one hard caveat:** the load-bearing part of any dev loop is the **oracle, not the loop**. GA's *mechanical* quality (routing correctness, canonical-trace stability, invariant pass-rate) is deterministically verifiable → safe to loop. GA's *fuzzy* quality (answer helpfulness, tone, NL correctness) is only LLM-judgeable → keep a human or the Demerzel tribunal on that gate. Don't let a fuzzy judge drive an autonomous inner loop; that's where loops produce "locally successful, globally wrong" output.

---

## 1. The model: two loops, two oracle classes

The consensus decomposition (Anthropic's "gather context → act → verify → repeat", OODA's nested short/long cycles, closed-loop control with the goal as setpoint):

| | **Inner loop** (tactical) | **Outer loop** (strategic) |
|---|---|---|
| Cycle | per change, seconds–minutes | per goal, hours–days |
| Drives | edit → build → test → eval-one | "is the capability good enough to ship?" |
| Oracle | **deterministic** (build, unit tests, routing match, canonical-trace diff, invariant pass) | **eval suite + human/tribunal** on fuzzy quality |
| Claude primitive | `/goal` (condition-based autonomy w/ independent checker) | `/loop` watchdog + human review gate |
| Exit | success condition verified, or stop on stagnation/ambiguity | metric ≥ target on the corpus AND tribunal pass |

**The ratchet is the highest-leverage guardrail:** every passing eval becomes a one-way door — CI auto-blocks any change that regresses it. GA already has the polarity-aware roundtrip validators to enforce this.

**Convergence is a query, not a vibe.** The inner loop's continue/stop/escalate decision should read the metric trajectory from DuckDB (improving → continue; plateau → stop & report; oscillating/regressing → halt & escalate), *not* the agent's self-assessment (agents are systematically optimistic about completion).

## 2. How the nesting maps onto GA

```
OUTER GOAL LOOP  (daily eval + human/tribunal, ratcheted in CI)
  goal: chatbot-qa pass_pct ≥ 0.94  AND  routing accuracy ≥ baseline  AND  no canonical-trace regressions
  oracle: state/quality/chatbot-qa/*.json (corpus) + routing-eval + ix-duck flight recorder
  │
  └── INNER TASK LOOP  (/auto-optimize, per-change, deterministic oracle)
        pick worst failing prompt → edit Agents/**/*.cs (scope-bounded) → run oracle →
        roundtrip-validate → commit if improved, revert if regressed → repeat to plateau
        │
        └── TACTICAL LOOP  (/goal or the edit-build-test inner loop on a single fix)
              build + targeted test until the one prompt's invariants pass
```

Existing components already playing each role:

- **Outer oracle:** `Tests/Apps/GaChatbot.Api.Tests/Corpus/prompts.yaml` (50-prompt invariant suite) via `Scripts/run-prompt-corpus.ps1 -Snapshot`; `RoutingEvalHarness.cs`; `ix/crates/ix-duck/src/chatbot.rs` flight recorder (hard signal = routed `agent_id` vs `_signature.json` canonical).
- **Outer contract / setpoint:** `state/quality/chatbot-qa/baseline.json` (metric=`pass_pct`, primary_baseline=0.94, scope_boundary, plateau_window=5, plateau_threshold=0.005, kill switches).
- **Inner executor:** `.claude/skills/auto-optimize/SKILL.md` (lock → iterate → oracle → revert/commit → plateau exit) + `docs/runbooks/chatbot-improvement-loop.md` patterns A/B/C.
- **Loop/goal runtime state:** `.claude/hooks/loops-goals-tracker.ps1` → `state/.runtime-loops-goals.jsonl` → `/dev-data/runtime-loops-goals` → `LoopsGoalsCard`.
- **Escalation channel:** `state/algedonic/inbox.jsonl` (severity/ack/supersedes) — the loop's "pain signal".

## 3. DuckDB as the backbone

**Why DuckDB specifically:** convergence and regression questions are *analytical aggregations over many runs* — columnar, fast, reads JSONL/Parquet directly. The right split (per the durable-execution literature): **JSONL/SQLite = per-run append-only journal (resumability), DuckDB = cross-run trajectory analytics (convergence)**. GA's `quality.duckdb` + `GaDuckLens`/`QualityLens` already *is* the analytics half; the daily `chatbot_qa` table is the **outer** loop's trajectory. What's missing is the **inner** loop's per-iteration trajectory.

Proposed additions to `state/quality/analytics/build-views.sql` (materialized from a new `loop-history.jsonl` the executor writes):

```sql
-- One row per inner-loop iteration (written by /auto-optimize).
CREATE OR REPLACE TABLE loop_iteration AS
SELECT day, domain, loop_id, iteration, oracle_name,
       metric_name, TRY_CAST(metric_value AS DOUBLE) AS metric_value,
       TRY_CAST(metric_delta AS DOUBLE) AS metric_delta,
       verdict,                       -- improved | regressed | plateau | error | couldnt_run
       artifact_edited, commit_sha, started_at
FROM read_json_auto('chatbot-qa/loop-history.jsonl', union_by_name = true);

-- Convergence view: is each loop run improving, plateaued, or diverging?
CREATE OR REPLACE VIEW loop_convergence AS
SELECT loop_id, domain,
       count(*)                                  AS iterations,
       max(metric_value) - min(metric_value)     AS total_gain,
       avg(metric_delta)                         AS mean_delta,
       sum(CASE WHEN abs(metric_delta) < 0.005 THEN 1 ELSE 0 END) AS plateau_iters,
       sum(CASE WHEN verdict='regressed' THEN 1 ELSE 0 END)       AS regressions,
       sum(CASE WHEN verdict='couldnt_run' THEN 1 ELSE 0 END)     AS oracle_misfires
FROM loop_iteration GROUP BY loop_id, domain;
```

The executor then makes its stop decision by **querying its own ledger** (`plateau_iters >= plateau_window` → stop; `oracle_misfires > 0` → halt, never count a misfire as "passed" — the documented auto-optimize paranoia rule). `ix.duckdb_extension`'s `ix_pca_project`/`ix_silhouette` can additionally cluster failure embeddings to pick which failure family to attack next.

## 4. What exists vs. the gaps

| Need | Status | Where |
|---|---|---|
| Outer oracle (corpus, routing, traces) | ✅ built | `prompts.yaml`, `RoutingEvalHarness.cs`, `ix-duck/src/chatbot.rs` |
| Setpoint contract (baseline + scope + plateau params) | ✅ built | `state/quality/chatbot-qa/baseline.json` |
| Inner loop executor | ✅ built | `.claude/skills/auto-optimize/SKILL.md` |
| DuckDB analytics + read lenses | ✅ built | `quality.duckdb`, `build-views.sql`, `QualityLens`, `GaDuckLens` |
| Loop/goal runtime state | ✅ built | `loops-goals-tracker.ps1` → JSONL → dashboard |
| Vector ops in DuckDB (cluster failures) | ✅ built | `ix.duckdb_extension` (merged) |
| **Per-iteration loop ledger** (`loop-history.jsonl` writer) | ❌ gap | `/auto-optimize` declares `history_file` but doesn't write it |
| **Plateau detection from trajectory** | ❌ gap | contract has params; executor doesn't compute delta-window |
| **Flight-recorder gate in CI** | ❌ gap | `chatbot.rs` done; no `--features duck` CI step |
| **Live eval backend in CI** (real metric, not `degraded`) | 🟡 in flight | chatbot-qa CI thread; PR #409 (CF-Access Ollama) open |
| **Convergence-as-a-query view** | ❌ gap | proposed §3 |

## 5. Failure modes & guardrails (and GA's existing answers)

| Failure mode | GA mitigation (existing or to-add) |
|---|---|
| Non-termination | hard `max_iterations` + `max_wall_clock_minutes` in baseline contract ✅ |
| Oscillation / regression | revert-on-regress + ratchet; add oscillation halt from `loop_iteration` ❌ |
| Reward hacking (edits tests, hardcodes) | scope_boundary protects corpus/oracle/baseline ✅; diff-scoped edits ✅ |
| Silent oracle failure ("passed" but couldn't run) | gate verdict on process exit code; `verdict='couldnt_run'` ❌ (documented paranoia rule, not yet enforced in ledger) |
| Cost runaway | iteration/commit caps ✅; add token budget |
| Fuzzy-quality drift | keep human/Demerzel tribunal on the outer gate ✅; never let LLM-judge drive the inner loop |
| Kill switch | `state/.loop-halted`, `state/quality/<domain>/.STOP`, `~/.demerzel/HALT-ALL` ✅ |

## 6. Feasibility verdict & phased recommendation

**Feasible and low-risk to start, because the oracle and the store already exist.** This is *closing a loop*, not building one. Suggested phasing (each independently shippable, reversible):

- **Phase 1 — Make the loop observable.** `/auto-optimize` writes `loop-history.jsonl` per iteration; add `loop_iteration` + `loop_convergence` to `build-views.sql`; surface on the dashboard. *No behavior change — just instrumentation.* This alone answers "are our improvement loops actually converging or spinning?".
- **Phase 2 — Make the loop self-terminating.** ✅ **DONE 2026-06-15.** `Scripts/loop-decide.ps1` reads the per-cycle ledger for the current run and returns `continue / stop-plateau / halt-oscillating / halt-misfire` (precedence: misfire > oscillating > plateau > continue). `/auto-optimize` Step 3.9 calls it instead of a fixed count, and escalates via `algedonic-emit.ps1` on a self-halt. `couldnt_run ≠ passed` is enforced (misfire checked first). Verified: `state/quality/_fixtures/verify-loop-decide.ps1` — all four decisions correct.
- **Phase 3 — Ratchet in CI.** Wire the ix-duck flight-recorder gate (`--features duck`) as a required check; make routing-eval a gate once the live backend lands (PR #409). Now the outer loop's ratchet is mechanical.
- **Phase 4 — Compound the process.** After each loop run, summarize learnings into `docs/solutions/` (the compound-engineering meta-loop) so the *next* run starts smarter.

**Hold the line at:** the outer fuzzy-quality judgment stays human/tribunal-gated; autonomous cycles stay branch-only/never-merge (the existing afk-harness rule); OPTIC-K/schema/pricing remain one-way doors needing sign-off.

## Sources

External synthesis (full source list in the research thread): Anthropic *Building effective agents* / *Building agents with the Claude Agent SDK* / *Effective harnesses for long-running agents*; eval-driven development (Braintrust, evaldriven.org, Red Hat); compound engineering (Every.to); OODA & control-loop framings; "SQLite is becoming the agent black box"; the Ralph Wiggum loop failure catalog; Claude Code `/goal` `/loop` `/batch` `/background` writeups. Internal inventory: see the component paths cited inline.
