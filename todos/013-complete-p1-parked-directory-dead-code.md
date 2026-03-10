---
status: complete
priority: p1
issue_id: "013"
tags: [dead-code, quality, code-review]
dependencies: []
---

# 013 — `_Parked/` Directory Contains ~400 LOC of Broken Dead Code

## Problem Statement
`Apps/ga-server/GaApi/_Parked/` contains 6 files (~400 LOC) that are excluded from the `.csproj` build. None of the files compile as-is: they reference missing types, contain a `Cache<>` syntax error, simulate latency with `Task.Delay(10)`, and hardcode the OPTIC-K embedding dimension as 1536 when the canonical value is 216. One file (`MonadicHealthCheckService`) contains sync-over-async `.GetAwaiter().GetResult()` that would deadlock on ASP.NET Core's synchronization context if re-enabled. No file is referenced from any active code path.

## Findings
- `Apps/ga-server/GaApi/_Parked/`: 6 files, excluded from build via `.csproj` glob or explicit exclusion.
- Hardcoded OPTIC-K dimension `1536` conflicts with the canonical 216-dimension schema (`Common/GA.Business.ML/Embeddings/EmbeddingSchema.cs`). Re-enabling any file would silently corrupt the embedding pipeline.
- `Cache<>` syntax error would prevent compilation even if files were re-included.
- `MonadicHealthCheckService`: `.GetAwaiter().GetResult()` inside an `async` context is a deadlock risk on ASP.NET Core.
- No `git grep` hits for any symbol defined in `_Parked/` in the active build tree.
- Git history preserves all content; deletion is safe and reversible.

## Proposed Solutions
### Option A — `git rm -r _Parked/` (recommended)
Remove the entire directory from the repository. Git history preserves the content for future reference if any logic needs to be salvaged.

**Pros:** Zero maintenance burden; eliminates confusion for contributors; removes risk of accidental re-inclusion; cleans up the project tree.
**Cons:** Content is no longer in the working tree (but remains in git history).
**Effort:** Small
**Risk:** Low

### Option B — Move to `docs/archive/` as text snapshots
Copy the files as `.txt` (non-compiling) into `docs/archive/parked-gaapi/` for documentation purposes, then delete the originals.

**Pros:** Makes archived intent more explicit than a git log search.
**Cons:** Adds noise to the `docs/` tree; the zero-compile problem is deferred to documentation; git history already provides this.
**Effort:** Small
**Risk:** Low (but unnecessary)

## Recommended Action
(Leave blank — to be filled during triage)

## Technical Details
- **Affected files:**
  - `Apps/ga-server/GaApi/_Parked/` (all 6 files, ~400 LOC)
- **Key defects in parked code:**
  - OPTIC-K dimension hardcoded as 1536 (canonical: 216)
  - `Cache<>` syntax error
  - `Task.Delay(10)` placeholder latency simulation
  - Sync-over-async `.GetAwaiter().GetResult()` in `MonadicHealthCheckService`
- **Components:** GaApi project structure

## Acceptance Criteria
- [ ] `Apps/ga-server/GaApi/_Parked/` does not exist in the working tree.
- [ ] `dotnet build AllProjects.slnx` still passes with zero warnings after deletion.
- [ ] `dotnet test AllProjects.slnx` still passes after deletion.
- [ ] No active code references any symbol that was defined only in `_Parked/`.

## Work Log
- 2026-03-07 — Identified in /ce:review of PR #2

## Resources
- PR: https://github.com/GuitarAlchemist/ga/pull/2
