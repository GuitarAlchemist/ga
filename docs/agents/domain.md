# Domain Docs

How the engineering skills should consume this repo's domain documentation. **ga is single-context** for skill purposes: one `CONTEXT.md` + `docs/adr/` at the repo root (the codebase itself is layered — see the five-layer model in `CLAUDE.md` / `docs/architecture/layers.md`).

## Before exploring, read these

- **`CONTEXT.md`** at the repo root — the domain glossary (music theory + voicings + RAG).
- **`docs/adr/`** — ADRs touching the area you're about to work in.
- **`docs/architecture/`** — the layer map and existing architecture docs.

If `CONTEXT.md`/`docs/adr/` don't exist yet, **proceed silently** — `/grill-with-docs` creates and grows them lazily as terms/decisions get resolved.

## Use the glossary's vocabulary

When your output names a domain concept (issue title, refactor proposal, test name), use the term as defined in `CONTEXT.md` (and the canonical music-theory names — e.g. `DisplayName` vs `CanonicalName` for voicings). Don't drift to synonyms the glossary avoids.

## Respect the five-layer model

ga enforces strict bottom-up layering (Core → Domain → Analysis → AI/ML → Orchestration; AI code at layer 4, never lower). Any refactor proposal must not introduce an upward dependency. See `docs/architecture/layers.md`.

## Flag ADR conflicts

If your output contradicts an existing ADR or the layer model, surface it explicitly rather than silently overriding.
