# Calibration-gated autonomy — design (J2 × J3 bridge)

**Status: v0.1 DRAFT — design only, nothing enforced. Tribunal REQUIRED before any enforcement step (one-way door).**

Type: arch. Reversibility: two-way while shadow-only; **one-way at enforcement** (agents adapt to incentives — an earning rule, once lived under, cannot be cleanly un-taught). Revisit trigger: 30 shadow days or ≥50 resolved forecasts on the tracer actor, or the hari#20 calibration study landing.

---

## 1. Problem

The ecosystem's autonomy controls are **declared**, not **earned**:

- Demerzel confidence thresholds (`≥0.9 autonomous · ≥0.7 with note · ≥0.5 ask · ≥0.3 escalate · <0.3 do not act`) gate on **self-reported** confidence — exactly the quantity the J4 research showed is miscalibrated at the planning stage (arXiv 2605.23414: planning miscalibration survives correct execution; EPC-AW +9.75% from calibration-aware workflows).
- `issue_meta.afk.max_autonomy` (`none/draft/pr`) is hand-set per issue and never updated by outcomes.
- The Agent Blackbox gate + `agent-blackbox-reviewed` label is a **human** exception mechanism, not a learning one.

Meanwhile J2 (hari forecast ledger, `hari-forecast-record-v0.1.0`) now measures actual calibration: Brier-scored, per-belief-family, `void`-aware, append-only. Nothing consumes it for governance yet.

**Design goal:** an actor's *effective* autonomy on a *class of action* becomes a function of its *measured* calibration on forecasts attached to that class — never exceeding the human-declared ceiling. Trust earned by outcomes, not configured by role (the exact phrase of hari#14, generalized from sources to actors).

Who is in pain: the operator, who today must either babysit every delegation or grant static trust that outcomes never validate; and the delegates, whose good track records earn them nothing.

## 2. Prior art (in-house — all pieces exist)

| Piece | Where | Role here |
|---|---|---|
| Forecast ledger, Brier, calibration query, `void` | hari `forecast.rs` (J2, shipped) | The measurement instrument |
| Action-boundary contract + aggregator | ga `docs/contracts/*action-boundary*`, `Scripts/action-boundary-aggregate.py` (J3, shipped) | Where the effective level will be published. **Note:** the shipped aggregator projects capabilities/lanes/halt/one-way-door paths but has **no classification field yet** — deriving `action_class` from paths/capabilities is **new contract work** (a classification slice added to the boundary schema), not existing behavior (Codex review, ga#512) |
| Autonomy vocabulary | `issue_meta.afk.max_autonomy`, supervised-loop gates, blackbox blocked paths | The ladder's rungs already exist informally |
| Confidence thresholds + hexavalent logic | Demerzel constitution/policies | The governing semantics; `C` (contradictory) already triggers escalation |
| Simulate-before-acting slice | ga#508 (delegated) | Makes forecasting *mandatory* before acting — the anti-Goodhart keystone (§4.1) |

External: no published work compares "frontier + calibration ledger" against trained predictors on observable prediction (J4 report, open question #3) — this design doubles as the instrument for that study (hari#20).

## 3. Core design

### 3.1 Autonomy ladder (formalizing existing rungs)

```
L0 observe          — read-only
L1 forecast-only    — may emit forecasts (shadow); no side effects
L2 propose          — issues/comments (today: most delegates' floor)
L3 draft            — branches, no PR        (= issue_meta max_autonomy: draft)
L4 pr               — opens PRs, human merges (= max_autonomy: pr)
L5 merge-gated      — merges behind green CI + policy gates (label override stays human)
L6 autonomous-in-boundary — acts without review inside J3 boundary (today: nobody)
```

### 3.2 Calibration record

Key: `(actor, action_class, horizon_bucket)`. `action_class` is **derived by the J3 boundary contract from the action's target paths/capabilities — never self-declared** (§4.6). From the ledger over a rolling window `W` (default 90 d, echoing the only industrial retraining-cadence datum we have, Facebook's ~3-month window):

- `n_resolved`, `n_void`, `void_rate`
- `brier_mean`; once `n` justifies binning, its decomposition: `reliability` (calibration proper) and `resolution` (discrimination)
- `success_rate` of the *actions* the forecasts were attached to
- `last_resolved_at` (freshness)

### 3.3 Earning function

```
effective_level = min( declared_max,                  # human ceiling — NEVER exceeded
                       earned_level(record) )
```

`earned_level` is a Demerzel **policy artifact** (YAML, versioned, tribunal-gated), shaped as monotone requirements per rung, e.g.:

| Rung | n_resolved ≥ | brier_mean ≤ | resolution ≥ | void_rate ≤ | success_rate ≥ | freshness ≤ |
|---|---|---|---|---|---|---|
| L3 | 10 | 0.20 | — (n/a below binning volume) | 0.30 | 0.6 | 30 d |
| L4 | 20 | 0.15 | above class base-rate floor | 0.20 | 0.7 | 30 d |
| L5 | 50 | 0.10 | above class base-rate floor | 0.15 | 0.8 | 21 d |

(Numbers illustrative — the tribunal sets them; small-`n` guarded by Wilson lower bounds, not raw means.)

**Resolution gate (the §4.1 counter, made binding):** promotion to **L4 and above requires demonstrated `resolution`** — discrimination above what base-rate prediction yields for that class. While `resolution` is unavailable (`null`, below binning volume — §7.1), **promotion is capped at L3**: in low-entropy classes (docs-only CI, routine routing) an actor could otherwise meet every other threshold by predicting the base rate accurately while discriminating nothing, which is precisely the easy-forecast-farming case §4.1 exists to prevent (Codex review, ga#512).

**Demotion** is windowed and hysteretic: one rung per evaluation cycle on collapse (no flapping), except killswitch/halt conditions which floor to L0 immediately. **Promotion** is rate-limited: at most one rung per `N` newly resolved forecasts, each new rung starting with a shadow period (§4.7).

### 3.4 Ownership split (Galactic-Protocol-shaped)

- **hari** owns the math (ledger, Brier, decomposition) — it computes *records*.
- **Demerzel** owns the earning policy — it decides *what records mean*. Policies override personas; the Asimov constitution overrides everything; `declared_max` is a hard human ceiling.
- **ga (J3 aggregator)** computes and publishes `effective_level` into `state/governance/calibration-autonomy.json`, next to `action-boundary.json`, same drift-gate pattern.
- **Consumers** (supervised-loop preflight, AFK router, blackbox) read it exactly like they read the boundary artifact.

## 4. Adversarial analysis

This is where the design earns its keep. Each attack, then its structural counter — preferring *mechanical* counters over policed ones.

### 4.1 Goodhart: easy-forecast farming
*Attack:* build a shiny record by forecasting only gimmes ("the sun will rise", "CI will pass on a docs-only PR").
*Counter:* only forecasts **attached to actions of the class being earned** count (ga#508's simulate-before-acting makes emission mandatory at act time, so the difficulty distribution is the real work distribution, not chosen); plus the **resolution requirement** — perfectly predicting the base rate yields good reliability but ~zero resolution, and resolution is required for promotion. Free-floating forecasts remain welcome (they feed hari#20) but earn nothing.

### 4.2 Sandbagging: calibrated failure
*Attack:* predict your own failure, then fail. Perfect calibration, zero value — or worse, deliberate.
*Counter:* the `success_rate` floor makes calibrated failure non-promotable; the *pattern* (high resolution + low success) is cheap to detect mechanically and surfaces as a governance flag — deliberate failure is a constitutional violation, Demerzel's jurisdiction, not this mechanism's.

### 4.3 Void farming: kill the referee
*Attack:* let (or make) the resolving sensor die so forecasts void out, freezing a good record while acting continues. Today's live lesson: chatbot-qa dead since 06-19 while everything looked green.
*Counter:* `void_rate` ceiling gates promotion; **sustained high void-rate demotes to L1 (forecast-only)** — generalizing the stale-feed rule from ix#226: *no verification ⇒ no autonomy*. `void` never counts as success. A dead sensor freezes upward movement ecosystem-wide for the classes it resolves.

### 4.4 Distribution shift: stale credentials
*Attack:* calibration earned on last quarter's codebase, spent on this quarter's.
*Counter:* freshness window in every rung requirement; records age out (the ledger is append-only, the *window* moves); new `action_class`es cold-start at L2 regardless of the actor's record on sibling classes — no transfer by default (revisit with data, hari#20).

### 4.5 Contradiction
*Attack/accident:* conflicting outcome evidence (two sensors disagree on whether the action succeeded).
*Counter:* hexavalent `C` semantics already exist — contradiction on a record's inputs freezes the record and escalates to human. Never resolved by averaging.

### 4.6 Taxonomy gaming
*Attack:* label a risky action as belonging to a well-calibrated class.
*Counter:* `action_class` is **derived** by the J3 boundary from target paths/capabilities (blocked paths, cost lanes, one-way-door triggers) — the same machinery that already classifies actions for blocking. Self-declaration has no channel.

### 4.7 Feedback instability
*Attack/accident:* promotion increases action volume and changes the very distribution that produced the calibration; the record chases its own tail.
*Counter:* rate-limited promotion (one rung / N resolutions) + mandatory shadow period at each new rung (act at old level, log what the new level *would have* permitted) + windowed demotion. The loop is damped by construction.

### 4.8 Residual risks (owned, not solved)
- Thresholds themselves become a target once known — accepted; they are tribunal-set policy, changeable, and the ceiling is human.
- Collusion between actor and verifier (an agent resolving its own forecasts) — forbidden structurally: resolution is mechanical (pinned observables, `hari-forecast-record` contract) and producer/resolver separation is already contract law there.
- A genuinely new failure mode nobody forecast — this design narrows, never widens, the blast radius: `min(declared_max, …)` means the worst case equals today's status quo.

## 5. Contract sketch (v0.1, to be drafted as `docs/contracts/`)

```json
{
  "schema": "calibration-autonomy-v0.1.0",
  "actor": "jules",
  "action_class": "issue-routing",
  "ladder": { "declared_max": "L4", "earned": "L3", "effective": "L3" },
  "evidence": {
    "window_days": 90, "n_resolved": 23, "n_void": 4, "void_rate": 0.148,
    "brier_mean": 0.11, "reliability": null, "resolution": null,
    "success_rate": 0.83, "last_resolved_at": "2026-07-03T18:00:00Z"
  },
  "policy_version": "demerzel:autonomy-earning-v0.1",
  "boundary_version": "action-boundary-v0.1",
  "computed_at": "2026-07-03T20:00:00Z",
  "mode": "shadow"
}
```

Locked fields (once other repos consume): `schema`, ladder rung names, evidence field names. `links.supersedes` pattern for baseline shifts, as everywhere.

## 6. Tracer bullet (smallest end-to-end slice — shadow only)

One actor, one class, zero enforcement:

1. Actor = the **dead-letter re-router** (J5's chosen first decision); class = `issue-routing`; its forecasts already have a home (ga#508 emits them into the J2 ledger).
2. A small script (extension of `action-boundary-aggregate.py`) computes the calibration record + effective level daily and appends to `state/governance/calibration-autonomy.json` with `"mode": "shadow"`.
3. **Nothing reads it for decisions.** After 30 days: one honest report — what autonomy *would* have been granted vs what was declared, and whether the record moved sanely.
4. Success criterion (Karpathy R4): ≥1 promotion-or-demotion event justified end-to-end from ledger entries a human can audit, and zero behavior change anywhere.

Blocked on: ga#508 landing (forecast emission at act time), **and** the action-class classification slice in the J3 boundary schema (new contract work — §2 note; without it the tracer has no authoritative source for `action_class` and the §4.6 counter cannot hold). Tribunal: required before any consumer reads `effective` for enforcement — that is the one-way door, and it is explicitly NOT this slice.

## 7. Open questions

1. Brier decomposition needs binned outcomes — below ~50 resolutions per record, use raw Brier + success floor and say so in the record (`reliability: null`).
2. Cross-actor priors (does Claude's record inform a fresh Codex lane?) — default no; the hari#20 study is the evidence gate for revisiting.
3. Human overrides — the `agent-blackbox-reviewed` label pattern generalizes (a label = temporary +1 rung for one artifact, logged); needs its own small design if wanted.
4. Where the `horizon_bucket`s cut (hours/days/weeks) — decide from actual ledger data at tracer time, not a priori.
