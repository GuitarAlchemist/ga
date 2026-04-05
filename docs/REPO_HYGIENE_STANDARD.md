# Repository Hygiene Standard

Date: 2026-04-05
Audience: Claude Code, Octopus, and human maintainers

## Purpose

This standard exists to stop repository surfaces from drifting away from the architecture they are supposed to communicate.

The goal is not aesthetic tidiness. The goal is operational clarity.

## Core Rules

### Rule 1: Every repo must declare category and status

At minimum, each `README.md` must say:

- Category: `product`, `platform`, `governance`, `integration`, `showcase`, `incubation`, or `archive`
- Status: `Active`, `Experimental`, `Incubating`, `Maintenance`, or `Archived`

### Rule 2: Every repo must declare the active surface

Examples:

- “All active development lives in `v2/`.”
- “The runtime app lives in `Apps/ga-client` and `Apps/ga-server`.”
- “This repo is static showcase content only.”

If a reviewer cannot tell where active work lives within 30 seconds, the repo is failing this rule.

### Rule 3: Root-level clutter is a defect

These do not belong at the repo root unless they are canonical:

- ad hoc logs,
- temporary output files,
- generated reports,
- one-off repair scripts,
- archive snapshots,
- milestone status docs,
- scratch notebooks.

They must be moved into dedicated directories such as:

- `docs/reports/`
- `docs/history/`
- `scripts/repair/`
- `artifacts/`
- `archive/`

### Rule 4: Archive semantics must be explicit

Historical work is allowed. Historical work that looks active is not.

If content is not active, it must be marked by location or naming:

- `archive/`
- `legacy/`
- `historical/`
- `experimental/`

### Rule 5: Claims should be machine-verifiable where possible

Claims like these should increasingly come from scripts or CI:

- test counts,
- benchmark totals,
- tool counts,
- crate counts,
- policy/test inventory,
- build status.

Narrated claims are acceptable temporarily. Generated claims are preferred.

## README Minimum Contract

Every repo README should contain:

1. One-sentence purpose.
2. Category.
3. Status.
4. Active surface.
5. Setup or entrypoint.
6. Relationship to the rest of the ecosystem.

Optional but recommended:

7. Owner/steward.
8. Maturity level.
9. “What does not belong here” section.

## Directory Standards

Recommended root-level directories:

- `src/` or language-appropriate source roots
- `docs/`
- `tests/`
- `scripts/`
- `public/` or `assets/` where relevant
- `archive/` only when necessary

Avoid using the root as a general inbox.

## Documentation Standards

### Canonical docs

Canonical docs stay near the top of `docs/` and are linked from `README.md`.

Examples:

- architecture,
- quick start,
- active boundaries,
- portfolio map,
- testing guide.

### Non-canonical docs

These should be grouped away from the top level:

- session summaries,
- implementation diaries,
- final reports,
- one-time migration summaries,
- experiment logs.

Recommended homes:

- `docs/reports/`
- `docs/history/`
- `docs/archive/`

## Script Standards

### Durable scripts

Durable scripts should have:

- a stable home under `scripts/` or `tools/`,
- a descriptive name,
- a short usage comment or README link.

### One-off repair scripts

One-off repair scripts should either:

- be deleted after use,
- or moved into `scripts/repair/` with enough context to justify keeping them.

If a repo accumulates dozens of root-level repair scripts, that is a maintenance smell and should trigger cleanup.

## Generated Artifact Standards

Generated artifacts should not live indefinitely at the root.

Examples:

- logs,
- build outputs,
- screenshots,
- test result folders,
- generated JSON snapshots,
- benchmark outputs.

They belong in:

- ignored directories,
- `artifacts/`,
- CI outputs,
- or archival folders when historically important.

## Maturity Labels

Recommended maturity labels:

- `Concept`
- `Prototype`
- `Alpha`
- `Beta`
- `Stable`
- `Maintenance`
- `Archived`

Every repo should eventually expose a maturity label. Large platform repos may expose maturity per module rather than one repo-wide label.

## Enforcement Checklist

When reviewing a repo, check:

1. Is category declared?
2. Is status declared?
3. Is the active surface obvious?
4. Is the root low-noise?
5. Are archives visibly archived?
6. Are high-value claims verifiable?
7. Could a new contributor navigate this repo without insider knowledge?

If the answer to 4 or more is “no”, the repo needs hygiene work before more feature expansion.
