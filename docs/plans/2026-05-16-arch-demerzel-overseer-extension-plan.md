---
title: "arch: extend Demerzel as cross-repo AI-oversight control plane (halt + audit + trust)"
type: arch
status: draft-pending-signoff
version: 0.1
date: 2026-05-16
origin: 2026-05-16 session prompt — "we need a mechanism to oversee the entire development process that works from any repo to allow AI autonomous development"
related_plans:
  - docs/plans/2026-05-14-arch-cherny-adoption-and-tribunal-ci-plan-v2.md
  - docs/plans/2026-05-02-arch-qa-architect-tribunal-plan.md
  - docs/plans/2026-05-16-arch-nardien-distillation-plan.md
related_contracts:
  - docs/contracts/2026-05-02-qa-verdict.contract.md
  - docs/contracts/qa-verdict.schema.json
reversibility: |
  Phase 0 plan + schema additions = two-way.
  Phase 1 HALT-ALL marker + ACP endpoint = two-way (delete the file, redeploy without endpoint).
  Phase 2 audit-log JSONL writes = two-way at the write-call level but the log itself accumulates monotonically; new actions are reversible, recorded actions are append-only.
  Phase 3 trust-score derivation = two-way (compute is pure).
  Phase 4 per-repo Step 0 enforcement of HALT-ALL + audit appends = one-way at the "loops now depend on the overseer being reachable" level; needs an offline fallback so a Demerzel outage doesn't freeze every repo's autonomy.
revisit_trigger: end of Phase 1 — if HALT-ALL adoption across 4 repos slips past 1 week OR a single false-positive halt locks an operator out of a real fix, revisit centralization vs federation.
---

# Demerzel as cross-repo AI-oversight control plane

## Problem frame

We have **bounded autonomy** at the per-repo level today:

- GA: `.claude/skills/auto-optimize/` + `state/quality/<domain>/baseline.json` + per-domain killswitch
- Each repo: own `state/digests/latest.md`, own `Scripts/precompact-digest.ps1`, own `state/.loop-halted`
- Cross-repo: Demerzel ACP server on port 8200 already orchestrates the QA tribunal across GA/ix/tars; `docs/contracts/qa-verdict.schema.json` is the shipped verdict format

But there is **no ecosystem-level oversight**:

| Question | Today | Gap |
|---|---|---|
| "Is any loop running right now in any repo?" | Walk every repo's state/ dir manually | No central view |
| "Halt every loop across all repos immediately" | `touch state/.loop-halted` in each repo separately | Single command should fan out |
| "How much cloud credit did agents burn this week across all repos?" | Unknown — no ledger | No cost accounting |
| "Has this agent ever shipped a regression I missed?" | Memory + intuition | No track record |
| "Can I let this auto-loop merge its own PRs?" | No — Phase 2 spec says human-only | No trust signal to lift that gate later |

Today's session is the proximate motivation: a /auto-optimize cycle surfaced a load-bearing reliability bug that took hours to triage. If the loop had been running in 4 repos simultaneously, the cost would compound. The user's ask: **a control plane that travels with the AI, works from any repo, and provides halt + audit + trust primitives.**

## Operator picks (collected 2026-05-16)

| Dimension | Picked? | Why |
|---|---|---|
| Observability dashboard | Deferred (Phase 5+) | Read-only is nice-to-have; doesn't change agent behavior |
| **Global kill / pause / resume** | ✅ Phase 1 | Lowest-blast-radius control; high practical value (one-button halt across ecosystem) |
| **Cross-repo audit log + budget ledger** | ✅ Phase 2 | Foundation for everything else; append-only is hard to subvert |
| **Agent trust/rep system** | ✅ Phase 3 | Derived from Phase 2; gates the "auto-merge on multi-LLM green" Level 4 work |
| Host: extend Demerzel | ✅ | ACP + IXQL already exists; tribunal pattern proven |

## Decisions to make (drafted; signoff required)

| # | Decision | Default | Alternatives | Why |
|---|---|---|---|---|
| **D-marker-loc** | Where does HALT-ALL live? | **`$env:USERPROFILE/.demerzel/HALT-ALL`** (Windows) / `~/.demerzel/HALT-ALL` (Unix) | Repo-local mirror, S3 bucket, env var | User-home is operator-local, survives repo deletes, no network dependency, matches how `state/.loop-halted` works today |
| **D-audit-store** | Where does the audit log live? | **`$env:USERPROFILE/.demerzel/audit.jsonl`** (append-only, one line per event) | SQLite, central server, S3 bucket | JSONL is grep-able + tail-able + git-diffable + survives Demerzel-server outages. Operator can rotate manually. |
| **D-audit-schema** | What's an audit event? | **`{ts, repo, agent_id, action, target, cost_usd, outcome, verdict_ref}`** as JSONL | Protobuf, GHA-events-style | JSON is the lingua franca of the existing contracts; aligns with qa-verdict.schema.json style |
| **D-trust-formula** | How is trust computed? | **EWMA over last 30 days of `outcome ∈ {pass, regression, neutral}`** keyed by agent_id + action_type | Simple count, ELO-style pairwise, ML model | Exponentially-weighted moving average is robust to recent agent updates, decays old failures, simple to recompute |
| **D-halt-scope** | What does HALT-ALL stop? | **Every per-repo /auto-optimize Step 0 check + every loop-script invocation** | Also block manual /digest calls, also block IXQL pipelines | Conservative scope first; the loops are the autonomy surface. Manual /digest is operator-driven and shouldn't be blockable. |
| **D-offline-fallback** | What if Demerzel is unreachable? | **Per-repo `state/.loop-halted` REMAINS the authoritative local killswitch; HALT-ALL is checked OPPORTUNISTICALLY (skip if `~/.demerzel/` doesn't exist)** | Hard-fail when overseer unreachable; cache last-known-state | Fail-safe: if oversight is down, fall back to per-repo controls. Don't let the overseer outage freeze every loop. |
| **D-budget-enforcement** | When does the ledger BLOCK an action? | **Phase 2 = read-only (log everything, enforce nothing); Phase 3 = soft enforce (warning if >budget); Phase 4 = hard enforce (refuse if budget exceeded)** | Hard enforcement from Phase 2 | Three-phase rollout lets us calibrate budgets before turning on enforcement. |

All seven decisions are reversible per the table. The single one-way moment is the audit log itself — appended events are not retractable.

## Phases

### Phase 0 — Plan + contracts (this doc + 1 PR per related repo)

Deliverables (this repo only):
- `docs/plans/2026-05-16-arch-demerzel-overseer-extension-plan.md` (this file)
- `docs/contracts/2026-05-16-overseer-audit-event.contract.md` (new)
- `docs/contracts/overseer-audit-event.schema.json` (new)
- `docs/contracts/overseer-halt-marker.schema.json` (new, defines the HALT-ALL file format)

Deliverables (cross-repo, separate PRs):
- Demerzel: ACP endpoint stubs (no logic yet) `/halt`, `/audit/append`, `/trust/<agent>`
- GA / ix / tars: README mention of the new contract; no code changes

Reversibility: pure docs + schemas. Zero behavioral change.

### Phase 1 — HALT-ALL MVP

Deliverables:
- Demerzel: `~/.demerzel/HALT-ALL` write/read primitives + ACP endpoint `/halt POST {reason}`, `/halt DELETE`
- Demerzel: `demerzel-halt` MCP tool exposed to all agents (`demerzel:halt-all`, `demerzel:resume-all`, `demerzel:halt-status`)
- GA `.claude/skills/auto-optimize/SKILL.md` Step 0: add bullet "Check `$env:USERPROFILE/.demerzel/HALT-ALL`; exit 0 if present"
- ix, tars: equivalent Step 0 additions to their loop runners
- New `docs/runbooks/halt-resume.md` (operator one-pager: when to use, how to verify, how to lift)

Acceptance: operator runs `mcp__demerzel__demerzel_governance halt-all "testing"` from any repo's Claude Code session → all 4 repos' next auto-optimize invocation exits 0 with "halted by overseer" message → operator runs `demerzel:resume-all` → loops can run again.

Reversibility: delete the marker file. Two-way.

### Phase 2 — Audit log + cost ledger (read-only)

Deliverables:
- Demerzel: `~/.demerzel/audit.jsonl` append-only writer with rotation policy (rotate at 100MB, keep 12 rotations)
- Demerzel: ACP endpoint `/audit POST {event}`, `/audit GET ?since=<ts>&repo=<r>&agent=<a>`
- Demerzel: `demerzel-audit` MCP tool for query (read-only; writes happen via direct file append from loops to avoid round-trips)
- GA `.claude/skills/auto-optimize/SKILL.md`: every commit, every PR open, every cost-bearing tool call appends an audit event before proceeding
- ix, tars: equivalent additions
- Per-repo cost ledger: track LLM-bound spend per session in `state/cost-ledger.jsonl` (mirrors the central log; doubles as offline fallback)

Audit event shape (D-audit-schema):
```json
{
  "ts": "2026-05-16T15:42:00.123Z",
  "session_id": "digest-skill-2026-05-16T15-40Z",
  "repo": "ga",
  "agent_id": "auto-optimize/chatbot-qa",
  "action": "commit",
  "target": "Common/GA.Business.ML/Agents/Skills/ModeSkill.cs",
  "cost_usd": 0.0,
  "outcome": "pending",
  "verdict_ref": null,
  "schema_pin": { "qa_verdict": "v0.1", "audit_event": "v0.1" }
}
```

After verdict lands (roundtrip validator passes / fails), a SECOND event with the outcome closes the loop:
```json
{
  "ts": "2026-05-16T15:47:00.456Z",
  "session_id": "digest-skill-2026-05-16T15-40Z",
  "repo": "ga",
  "agent_id": "auto-optimize/chatbot-qa",
  "action": "verdict",
  "target": "<commit-sha>",
  "cost_usd": 0.02,
  "outcome": "pass",
  "verdict_ref": "state/quality/chatbot-qa/loop-history.jsonl#L42"
}
```

Acceptance: 1 week of dogfood across all 4 repos shows ≥95% of agent actions captured (sample by walking session digests and comparing to audit events).

Reversibility: removing the audit writer is one line per loop script. Removing the log itself loses history — that's the one-way door.

### Phase 3 — Trust/rep derivation + soft enforcement

Deliverables:
- Demerzel: nightly batch job that recomputes `~/.demerzel/trust/<agent_id>.json` from the audit log
- Trust score JSON shape:
  ```json
  {
    "agent_id": "auto-optimize/chatbot-qa",
    "score": 0.87,
    "score_formula": "ewma_30d",
    "actions_total": 142,
    "passes": 128,
    "regressions": 6,
    "neutrals": 8,
    "false_positives": 0,
    "last_updated": "2026-05-16T22:00:00Z",
    "schema_version": 1
  }
  ```
- Demerzel: `demerzel:trust-score <agent>` MCP tool
- Soft enforcement: when budget exceeds threshold, log warning to audit + Slack/console; do NOT block
- Trust gates documented in `.claude/skills/auto-optimize/SKILL.md` (e.g., "agent with score < 0.7 requires manual review on every commit")

Acceptance: trust scores stabilize within ±0.02 between two consecutive nightly recomputes for at least one agent with >20 actions logged.

Reversibility: trust scores are derivations of the audit log; deleting trust files just forces recomputation.

### Phase 4 — Cross-repo Step 0 enforcement + hard budget caps

Deliverables:
- Every `/auto-optimize` invocation in every repo checks (1) global HALT-ALL, (2) local `state/.loop-halted`, (3) trust score above threshold for the action being attempted, (4) cumulative session cost below `~/.demerzel/budget-caps.json[repo].monthly_usd`
- Operator-only override marker for trust/budget exceptions: `[allow-overseer-override: <reason>]` in commit subject (mirrors the existing `[allow-protected: <path>]` pattern)
- Per-action cost-budget enforcement: distillation Phase 1 burn is now ledger-tracked and refuses to start if month's budget exhausted

Acceptance: a deliberately-misconfigured loop that tries to spend >budget gets refused before the first cloud API call; operator can override with the marker.

Reversibility: drop the cap file → no enforcement. Trust thresholds are config-driven, not code-driven.

### Phase 5+ — Observability dashboard (deferred per operator pick)

Out of scope for v0.1. Once the audit log + trust derivations exist, a dashboard is a SELECT query — that's a separate UI project.

## One-way doors

| # | Door | Phase closes | Revisit trigger |
|---|---|---|---|
| **OWD-1** | Audit log records cannot be retracted | Phase 2 | Privacy / compliance ask — schema accepts a `redacted: true` marker but the record itself stays. Revisit if GDPR/legal asks for true deletion. |
| **OWD-2** | Trust score formula change requires full recompute (potentially weeks of nightly batch) | Phase 3 | Significant scoring change — pin schema_version and recompute everything from raw audit. |
| **OWD-3** | Per-repo Step 0 hard-checking Demerzel reachability would centralize a fault tolerance burden | Phase 4 | D-offline-fallback specifies opportunistic check — this should stay; revisit only if Demerzel SLA becomes load-bearing. |

## Success criteria

1. **Phase 1**: operator can halt all autonomous loops across 4 repos with a single MCP call; verified within 1 minute by trying to run /auto-optimize in each.
2. **Phase 2**: audit log captures ≥95% of agent commits/PR opens/cost-bearing tool calls across 1 week of dogfood.
3. **Phase 3**: trust score stabilizes for ≥1 agent with >20 actions; score visible via `demerzel:trust-score <agent>`.
4. **Phase 4**: a budget-violating action gets refused without an explicit operator override.
5. **Cumulative**: at least one real autonomous cycle runs end-to-end across 2+ repos with audit trail intact and trust score computed.

## Out of scope

- Centralized session-digest aggregation (each repo's `state/digests/latest.md` stays local; audit log captures the events, not the digest content)
- Cross-repo plan-of-plans / dependency graph (the related_plans frontmatter is the manual version; a tool comes later)
- Replacing per-repo `state/.loop-halted` (the global HALT-ALL is additive, not a replacement)
- Multi-tenant Demerzel (single-user assumption holds; multi-user is its own design)
- ML-driven trust score (Phase 3 default is EWMA, deliberately simple)

## Open questions

1. Should the audit log live in `~/.demerzel/` or in `~/.ai-overseer/` or `~/.config/demerzel/`? The home-relative convention is universal-ish but XDG-style `~/.local/share/demerzel/` is closer to the standard on Linux. Defer to first PR.
2. How does the overseer handle agents that DON'T use Claude Code (e.g., Cursor, Augment, Conductor)? Phase 1 punts; the marker file is universal but the audit-append calls are Claude-Code-skill-specific. Cross-tool support is Phase 5+.
3. Cost in `cost_usd` — does the agent self-report (trust the agent's tally) or does Demerzel intercept (impossible without a proxy)? Self-report for v0.1; with the marker patterns + multi-LLM review, fraud is detectable downstream.
4. The audit log will likely grow ~MB/day across 4 repos. Rotation policy (100MB → 12 keeps = 1.2GB) sustains ~1 year. Acceptable for v0.1.

## Related

- `feedback_multi_llm_review_pays_off` — the trust system formalizes what the multi-LLM-review pattern proves: external review is load-bearing for non-trivial PRs.
- `project_qa_architect_tribunal` — Demerzel already orchestrates verdicts; the overseer extension adds halt/audit/trust on top of the existing ACP.
- `project_acp_agents_2026_03_29` — Demerzel ACP architecture (port 8200) is the host for new endpoints.
- `docs/plans/2026-05-14-arch-cherny-adoption-and-tribunal-ci-plan-v2.md` §Phase 2 item 6 — "Ollama-vs-cloud signal measurement gate" is the precedent for Level-4 auto-merge gating that THIS trust system enables.
- This session's incidents:
  - `docs/solutions/tooling/2026-05-16-auto-optimize-oracle-silent-success-build-failure.md` — the silent-success bug that would have polluted the audit log with bogus passes
  - `docs/solutions/tooling/2026-05-10-octo-plugin-install-corruption-silent-gate-failure.md` — same false-green family; audit log gives provenance to detect retrospectively

The supervised /auto-optimize cycles this session (cycle 1 abort + cycle 2 surfacing the rel-003 nuance) are the empirical case for this overseer: every false-positive metric we caught manually would have leaked into a trust score if the audit had been running. The overseer doesn't replace the supervised pattern — it makes the next 100 loops auditable so we don't have to supervise each one in isolation.
