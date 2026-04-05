# Active Boundaries

Date: 2026-04-05
Audience: Claude Code, Octopus, and human maintainers

## Purpose

This document states where active work is expected to live across the portfolio. It exists to reduce ambiguity and prevent active, historical, and experimental work from blending together.

## `ga`

Active work should live in:

- `Common/`
- `Apps/`
- `Tests/`
- `ReactComponents/`
- `GA.Data.*`
- `GA.Business.*`
- `AllProjects.*`
- `Scripts/` for durable workflow scripts

Historical or non-canonical material should be moved away from the root into:

- `docs/history/`
- `docs/reports/`
- `archive/`
- `scripts/repair/`

Policy:

- The root should not remain the default destination for fix scripts, logs, or milestone summaries.

## `ix`

Active work should live in:

- `crates/`
- `docs/`
- `examples/`
- `governance/` where repo-local governance artifacts genuinely belong

Policy:

- Keep the root minimal.
- Do not allow capability growth to produce root clutter.

## `tars`

Declared active surface:

- `v2/`

Everything outside `v2/` should be treated cautiously until explicitly classified as one of:

- active support infrastructure,
- experimental,
- legacy,
- archived.

Policy:

- If new feature work is for the main TARS direction, default to `v2/`.
- Non-`v2` content should not appear active by accident.

## `Demerzel`

Active work should live in:

- `constitutions/`
- `policies/`
- `personas/`
- `schemas/`
- `grammars/`
- `contracts/`
- `pipelines/`
- `tests/`
- `docs/`

Policy:

- `Demerzel` is canonical for governance artifacts.
- Downstream repos should consume, not redefine, governance primitives where possible.

## `demerzel-bot`

Active work should live in:

- `src/`
- `scripts/`

Policy:

- Bot-specific logic belongs here.
- Canonical governance logic does not.

## `ga-godot`

Current inferred active surface:

- `scenes/`
- `scripts/`
- `assets/`
- `shaders/`

Policy:

- This repo should be treated as `experimental` until explicitly upgraded.

## `guitaralchemist.github.io`

Active work should live in:

- `index.html`
- `css/`
- `js/`
- `demos/`
- `showcase/`

Policy:

- Static public presentation only.
- No migration of core product logic here.

## `devto-mcp`

Active work should live in:

- `server.py`
- project manifests
- README/setup docs

Policy:

- Treat as a standalone integration repo unless strategically elevated.

## `hari`

Current inferred active surface:

- `crates/`
- `docs/`
- `docker/`
- `scripts/`

Policy:

- Treat as incubation/research until a clearer product or platform boundary is declared.

## Portfolio-Wide Rule

New work should default to the smallest repo that can own it cleanly.

Do not place work in a repo simply because it is convenient or already open.

Decision order:

1. Is this product/domain work? Put it in `ga`.
2. Is this reusable algorithm/platform work? Put it in `ix`.
3. Is this reasoning/agent/platform work? Put it in `tars` with `v2/` as default.
4. Is this governance/policy/schema/persona work? Put it in `Demerzel`.
5. Is this a runtime integration around those systems? Put it in the integration repo.

## Enforcement Note

If a change cannot clearly justify its repo placement in one sentence, stop and re-evaluate before implementing.
