---
title: Documentation maintenance system
scope: How GA keeps 400+ docs from rotting — the ephemeral/durable split, CI gates, and scheduled content audit.
status: authoritative
last_verified: 2026-06-01
parent: docs/architecture/README.md
---

# Documentation maintenance system

Docs rot because code moves and prose doesn't. A 2026-06-01 audit of all 424
docs found **142 broken internal links** and **50 docs whose content was
contradicted by the current code** (e.g. references to apps/classes/endpoints
that were never shipped or had been removed). The biggest single source was a
generation of `*_COMPLETE.md` / `*_SUMMARY.md` session docs that were written
to celebrate a moment, then frozen as if they were current reference.

This is a process problem, not a one-time cleanup. The system has four layers.

## 1. Ephemeral vs durable (the structural lever)

Split docs by lifecycle and treat them differently:

- **Durable** — `architecture/`, `guides/`, `methodology/`, `contracts/`,
  `runbooks/`, `API/`, root-level reference docs. These **must stay current**.
  They carry frontmatter (`status`, `last_verified`) and are subject to the
  gates below.
- **Ephemeral / point-in-time** — `plans/`, `reports/`, `solutions/`,
  `brainstorms/`, `archive/`, `history/`. These are **append-only history**:
  dated, never updated, never claimed to be "current". Their links/refs are
  allowed to decay — that's expected for a record.

**Do not mint new permanent `*_COMPLETE.md` / `*_SUMMARY.md` docs.** "What I did
this session" belongs in `state/` / the digest system, or in a clearly-dated
`reports/` file — never as a durable reference doc.

## 2. CI gates — stop new rot in minutes (`.github/workflows/docs-health.yml`)

Run on every PR touching `docs/`:

- **Link gate** (`Scripts/audit-doc-links.py HEAD --ci`) — fails if **any live
  doc** has a broken internal markdown link. Live docs are kept at zero, so any
  new break fails the PR.
- **Code-reference ratchet** (`Scripts/audit-doc-code-refs.py --files <changed>`)
  — fails if a doc the PR **touches** references a repo path (`Apps/...`,
  `Common/...`, `*.cs`, …) that doesn't exist. It only checks changed docs, so
  the pre-existing backlog doesn't block unrelated PRs, but you can't *add*
  drift to a doc you edit. This is the gate that would have caught the
  `GuitarAlchemistChatbot` app reference (the app doesn't exist) and the
  dead-class references found in the audit.

Frozen dirs are exempt in both.

## 3. Scheduled deep audit — bound the backlog

The mechanical gates can't judge "this paragraph describes a workflow that no
longer exists". For that, a **monthly** content audit (an LLM multi-agent sweep
— too expensive per-PR) reads each durable doc and verifies its claims against
the code, producing a dated report under `reports/` and filing issues for STALE
docs. This is the Cherny-loop pattern; it extends the existing `readme-drift`
quality signal rather than inventing a new one. (First run:
`docs/reports/2026-06-01-docs-staleness-audit.md`.)

## 4. Shrink + freeze the surface (ongoing)

Delete docs for never-shipped features; archive done-and-dusted ones; keep one
canonical index (`docs/README.md`), not several. Fewer, current docs are easier
to keep current.

## Tooling

| Script | Purpose |
|---|---|
| `Scripts/audit-doc-links.py` | broken internal-link scanner (`--ci` to gate live docs) |
| `Scripts/audit-doc-code-refs.py` | repo-path-reference checker (`--files` to ratchet changed docs; `--report` for a non-gating full scan) |
| `Scripts/patch-doc-links.py` | bulk fix: redirect or delinkify broken links (skips frozen dirs) |
| `Scripts/remediate-stale-docs.py` | applies a staleness-audit result (delete/archive/banner) |
| `Scripts/check-architecture-docs.ps1` | frontmatter + freshness check for `architecture/` docs |
