# Portfolio Map

Date: 2026-04-05
Audience: Claude Code, Octopus, and human maintainers

## Purpose

This document is the canonical portfolio map for the local repository ecosystem under `C:\Users\spare\source\repos`.

Use it to answer five questions quickly:

1. What is each repo for?
2. What category does it belong to?
3. Is it active, incubating, peripheral, or archival?
4. What are its primary dependencies?
5. Where should new work go?

## Repository Table

| Repo | Category | Status | Primary Purpose | Depends On | Consumers | Notes |
|------|----------|--------|-----------------|------------|-----------|-------|
| `ga` | `product` | `Active` | Guitar Alchemist domain product: music theory, apps, frontend, API, orchestration | `ix`, `tars`, `Demerzel` | End users, demos, downstream integrations | Flagship product repo. Highest cleanup priority. |
| `ix` | `platform` | `Active` | Rust algorithm forge, MCP tools, ML/math/search/signal capabilities | `Demerzel` governance concepts, own Rust workspace | `ga`, `tars`, Claude/agents | Cleanest engineering surface. Needs stability tiering. |
| `tars` | `platform` | `Active` with legacy surface | Reasoning, agents, grammars, self-improvement, MCP server | `ix`, `Demerzel`, own F#/CLI stack | `ga`, agents, cross-repo workflows | Active boundary should be treated as `v2/`. |
| `Demerzel` | `governance` | `Active` | Constitutions, personas, policies, schemas, grammars, contracts, behavioral governance tests | None conceptually; canonical source | `ga`, `tars`, `ix`, `demerzel-bot` | Governance control plane. |
| `demerzel-bot` | `integration` | `Active` | Discord runtime surface for Demerzel/Seldon personas | `Demerzel` | Discord users | Keep thin; avoid policy duplication. |
| `ga-godot` | `incubation` | `Experimental` | 3D/immersive exploration surface for Guitar Alchemist | likely `ga` conceptually | future GA experiences | Needs README and status declaration. |
| `guitaralchemist.github.io` | `showcase` | `Active` | Public demo/showcase site | `ga` content and assets | Public visitors | Keep static and thin. |
| `devto-mcp` | `integration` | `Peripheral Active` | Dev.to MCP server | Dev.to API | external assistants/tools | Useful, but not central to core thesis. |
| `hari` | `incubation` | `Experimental` | AGI research / cognitive architecture exploration | own Rust/Docker stack | none yet | Under-explained; treat as incubation until clarified. |

## Dependency Direction

Desired conceptual direction:

1. `Demerzel` defines governance and policy.
2. `ix` and `tars` consume governance where appropriate.
3. `ga` consumes both platform capabilities and governance.
4. `demerzel-bot` consumes `Demerzel` directly.
5. `guitaralchemist.github.io` presents curated outputs from `ga`.
6. `ga-godot` may consume concepts/assets from `ga`, but should not become a second product root.

## Where New Work Should Go

### Put work in `ga` when

- it directly changes the user-facing Guitar Alchemist product,
- it belongs to music theory domain logic,
- it is frontend behavior for the main app,
- it is product-facing API work,
- it is orchestration specifically in service of GA.

### Put work in `ix` when

- it is a reusable algorithm, math primitive, ML component, optimization system, or MCP-exposed computational utility,
- it should plausibly be reused outside GA,
- it belongs naturally in a Rust crate.

### Put work in `tars` when

- it is agent reasoning infrastructure,
- grammar evolution, WoT, promotion pipelines, or self-improvement logic,
- MCP/CLI capability belongs to the reasoning platform rather than the product.

### Put work in `Demerzel` when

- it is constitutional logic,
- policy, persona, schema, governance grammar, or behavioral governance validation,
- it is intended to shape multiple repos rather than one runtime surface.

### Put work in `demerzel-bot` when

- it is Discord-specific runtime behavior,
- it is persona routing or bot UX,
- it is integration glue, not canonical governance.

### Put work in `ga-godot` when

- it is clearly an immersive or exploratory 3D surface not yet ready for the main GA product,
- it is explicitly marked as experimental or incubation work.

### Put work in `guitaralchemist.github.io` when

- it is static showcase content,
- it is public presentation,
- it does not belong in the main product runtime.

## Ownership Model

Recommended ownership lanes:

- Product / Domain: `ga`
- Platform / Algorithms: `ix`
- Platform / Reasoning: `tars`
- Governance / Policy: `Demerzel`
- Runtime Integration: `demerzel-bot`, `devto-mcp`
- Incubation / R&D: `ga-godot`, `hari`
- Public Showcase: `guitaralchemist.github.io`

## Required Follow-Up

Each repo should eventually expose, in its own README:

- category,
- status,
- active surface,
- owner/steward,
- dependencies,
- and maturity level.

Until then, use this file as the source-of-truth portfolio summary.
