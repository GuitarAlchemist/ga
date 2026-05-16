---
title: Overseer HALT-ALL marker contract
date: 2026-05-16
status: draft-v0.1
schema: docs/contracts/overseer-halt-marker.schema.json
related_plans:
  - docs/plans/2026-05-16-arch-demerzel-overseer-extension-plan.md
  - docs/plans/2026-05-16-feat-development-process-overseer-plan.md
producers:
  - Demerzel ACP `/halt POST` endpoint (Phase 1)
  - MCP tool `demerzel:halt-all` (Phase 1)
  - Operator hand-edit (always)
consumers:
  - .claude/skills/auto-optimize/SKILL.md Step 0 (GA, Phase 1)
  - Scripts/dev-process-overseer.ps1 (GA, deferred to follow-up PR)
  - ix loop runner Step 0 (Phase 1, separate PR in ix sibling)
  - tars loop runner Step 0 (Phase 1, separate PR in tars sibling)
---

# Overseer HALT-ALL marker contract

A per-user file that pauses every `/auto-optimize` loop across every repo in the GuitarAlchemist ecosystem. The contract: **presence of the file == halted**; absence == not halted.

## Location

| Platform | Path |
|---|---|
| Windows | `$env:USERPROFILE\.demerzel\HALT-ALL` |
| macOS / Linux | `$HOME/.demerzel/HALT-ALL` |

Per `D-marker-loc` in the strategic plan: user-home is operator-local, survives repo deletes, has no network dependency, and matches how per-repo `state/.loop-halted` already works.

## File format

JSON conforming to `docs/contracts/overseer-halt-marker.schema.json`. Required fields:

- `schema_version` (integer, MUST equal 1 for v0.1)
- `halted_at` (RFC3339 UTC)
- `reason` (1–500 chars, human-readable)

Optional fields: `halted_by`, `scope`, `expires_at`, `exempt_agents`, `links`. See the schema for full descriptions.

Example:

```json
{
  "schema_version": 1,
  "halted_at": "2026-05-16T16:30:00Z",
  "halted_by": "operator:spareilleux",
  "reason": "Investigating cost burn on Mistral teacher rollouts",
  "scope": "loops-only",
  "expires_at": null,
  "exempt_agents": [],
  "links": { "issue_ref": "ga#999" }
}
```

## Consumer obligations

Every consumer (per-repo loop runner, dev-process-overseer, etc.) MUST:

1. **Opportunistic check** — read the file before each cycle. If the file does not exist OR the directory is unreadable OR the file is unparseable, fall through to per-repo `state/.loop-halted` (the offline fallback per D-offline-fallback).
2. **Honor `expires_at`** — if `expires_at` is set and in the past, treat the marker as absent.
3. **Honor `exempt_agents`** — if the consumer's agent ID is in the list, ignore the marker for this cycle (but still log the bypass to audit).
4. **Honor `scope`** — v1 only acts on `"loops-only"`. Anything else is reserved.
5. **Reject unknown `schema_version`** — fail-closed: an unknown version means a newer producer than this consumer; treat as halted.
6. **Surface the reason** — print `halted_by` and `reason` so the operator who hits the gate knows why.

## Producer obligations

Every producer (ACP endpoint, MCP tool, operator) MUST:

1. **Atomic write** — write to a temp file in the same directory, then rename to `HALT-ALL`. Avoid half-written markers.
2. **Set RFC3339 UTC timestamps** — never localize.
3. **Validate against the schema** before writing — see `schema_version` mismatch above.
4. **Append an audit event** (once Phase 2 audit log lands) recording who halted and why.

## Removal semantics

Removing the file is the unhalt signal. Producers SHOULD archive the removed marker to `~/.demerzel/halts/<timestamp>-<reason-slug>.json` so the audit log can reconstruct the timeline.

## Versioning

`schema_version: 1` is the v0.1 contract. Breaking changes (renaming `reason`, dropping `halted_at`, etc.) bump to 2. Additive changes (new optional fields) do NOT bump.

## Out of scope for v0.1

- Multi-user halt semantics (this is user-local; multi-user is a future contract)
- Per-repo halt scope (per-repo `state/.loop-halted` is unchanged; this contract is for ecosystem-wide halt only)
- Audit-log integration (lands in Phase 2 of the strategic plan)
- Trust-score interaction (lands in Phase 3)
