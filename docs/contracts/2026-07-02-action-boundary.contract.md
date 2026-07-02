# Action-boundary contract — v0.1.0 (DRAFT)

> **Status: v0.1.x DRAFT.** Per the repo contract convention, drafts freeze
> only at their plan's explicitly named milestone — here, when the Jarvis
> Track J3 epic (BACKLOG.md) reaches its "all consumers ported" state.
> Until then fields may move with ordinary review.

**Jarvis Track J3 tracer bullet** (BACKLOG.md § Jarvis Track): promote the
safe action boundary from scattered conventions to a single machine-readable
contract every autonomous actor pre-checks. This document + the schema +
the generated instance are that promotion. Demerzel remains the owner of
governance *semantics*; this contract is GA's projection of them.

## The problem

The guardrails hardened on 2026-07-01/02 exist but live as fragments:

| Concern | Canonical fragment |
|---|---|
| Risk thresholds, blocked paths, one-way-door paths | `agent-blackbox.policy.json` |
| Supervised-loop edit scope (`allow_edit` / `protected_paths`) | `ga.loop-policy.json` |
| Halt semantics (HALT-ALL marker, local killswitch, fail-closed) | `Scripts/Governance.psm1` |
| Cost doctrine (subscription-only lanes vs API fallback) | `docs/solutions/tooling/2026-07-02-afk-delegation-chain-failures.md` |
| One-way doors (OPTIC-K dims, schemas, public APIs, pricing) | `CLAUDE.md` Karpathy rule 6 |

Every autonomous consumer (supervised-loop preflight, `/auto-optimize` scope
check, AFK router) re-reads a different subset, in a different order, with its
own parsing. That is how boundary drift happens.

## The shape

- **Instance:** `state/governance/action-boundary.json` — schema
  `action-boundary-v0.1.0`, validated by
  [`action-boundary.schema.json`](action-boundary.schema.json).
- **Generated, never hand-edited:** `Scripts/action-boundary-aggregate.py`
  projects the fragments into the instance. The fragments stay canonical —
  there is no second authority to drift. If an instance and a fragment
  disagree, **the fragment wins** and the instance is stale.
- **Drift gates:**
  - `--check` mode regenerates and fails if the committed instance differs
    (used in CI; see `.github/workflows/karpathy-cherny-discipline.yml`).
  - The aggregator string-verifies the halt-marker paths against
    `Scripts/Governance.psm1` and fails loudly if governance semantics moved.

## Semantics (unchanged, just gathered)

- `capabilities.allow_edit` / `capabilities.protected_paths` — the supervised
  loop's edit scope, verbatim from `ga.loop-policy.json`.
- `capabilities.blocked_paths` / `capabilities.one_way_door_paths` — the
  risk-scoring view, verbatim from `agent-blackbox.policy.json`.
- `halt.*` — marker locations; the gate *implementation* stays
  `Scripts/Governance.psm1` (fail-closed). Consumers must not re-implement
  parsing — they call the module (pwsh) or treat marker presence as halt.
- `cost_lanes` — the 2026-07-02 doctrine: API-key fallback ONLY on
  human-initiated, bounded (mention-triggered) lanes; scheduled and per-PR
  lanes are subscription-only and skip green when the token is absent.
- `one_way_doors` — the human-sign-off list from CLAUDE.md rule 6.

## Consumers

| Consumer | Status |
|---|---|
| `supervised-loop` preflight (Step 2 of `.claude/skills/supervised-loop/SKILL.md`) | **ported** — reads the instance as the aggregated view, fragments as fallback |
| `Scripts/dev-process-overseer.ps1` | follow-up (pwsh change; port when a pwsh-capable session touches it) |
| `/auto-optimize` scope check | follow-up |
| AFK router lanes | follow-up |

Porting rule: a consumer port must be behavior-preserving — same decisions,
one source. New *enforcement* (e.g. the audit log in `audit.expectation`)
is a separate J3 slice with its own review.

## One-way door & cross-repo

The schema's **locked fields** (once frozen): `schema`, `capabilities.*` key
names, `halt.*` key names. Demerzel owns governance semantics; any change to
halt semantics must land in `Scripts/Governance.psm1` + Demerzel constitutions
first, then be re-projected here. Baseline shifts follow the
`links.supersedes` pattern from the optick-sae-artifact contract.

**Tribunal: REQUIRED** before freeze (governance + cross-repo, per the J3
epic). This draft shipping on a branch is the review artifact, not the
sign-off.
