---
status: complete
priority: p1
issue_id: "010"
tags: [architecture, dead-code, layer-violations, code-review]
dependencies: []
---

# 010 — `GaChatbot` Contains Stale Types That Conflict with Orchestration Layer

## Problem Statement
Three files in `Apps/GaChatbot/` still compile into `GaChatbot.dll` and shadow or conflict with the canonical types introduced in `GA.Business.Core.Orchestration`. This creates layer violations (the application layer re-defining types that belong to the orchestration layer), potential runtime ambiguity, and ongoing maintenance confusion about which definition is authoritative.

## Findings
1. `Apps/GaChatbot/Models/ChatModels.cs:29` — `CandidateVoicing` still embeds the raw `VoicingExplanation` type from the ML layer. `VoicingExplanationDto` was introduced in the orchestration layer specifically to replace this.
2. `Apps/GaChatbot/Abstractions/IGroundedNarrator.cs:12` — duplicate interface using `List<T>` (mutable) while the canonical version in `GA.Business.Core.Orchestration` uses `IReadOnlyList<T>` (immutable). The mutable version leaks implementation details and breaks encapsulation.
3. `Apps/GaChatbot/Abstractions/IHarmonicChatOrchestrator.cs` — also stale; the canonical interface lives in `GA.Business.Core.Orchestration`.

All three canonical versions already exist in `GA.Business.Core.Orchestration`. The stale files should be deleted and all usages within `GaChatbot` updated to reference the orchestration types.

## Proposed Solutions
### Option A — Delete stale files and fix usages (recommended)
Delete all three files. Update any `using` directives and type references inside `Apps/GaChatbot/` to point to `GA.Business.Core.Orchestration`. Confirm the project compiles and tests pass.

**Pros:** Eliminates the layer violation permanently; single source of truth; no risk of the wrong type being resolved.
**Cons:** Requires a sweep of `GaChatbot` usages to update namespaces.
**Effort:** Small
**Risk:** Low

### Option B — Mark as `[Obsolete]` and redirect
Add `[Obsolete]` attributes pointing to the canonical types and keep the stale files temporarily while consumers migrate.

**Pros:** Non-breaking in the short term.
**Cons:** Extends the window in which the layer violation exists; `[Obsolete]` warnings will accumulate and conflict with the zero-warnings policy.
**Effort:** Small
**Risk:** Low (but deferred)

## Recommended Action
(Leave blank — to be filled during triage)

## Technical Details
- **Affected files:**
  - `Apps/GaChatbot/Models/ChatModels.cs:29` (stale `CandidateVoicing`)
  - `Apps/GaChatbot/Abstractions/IGroundedNarrator.cs:12` (mutable `List<T>` duplicate)
  - `Apps/GaChatbot/Abstractions/IHarmonicChatOrchestrator.cs` (stale interface)
- **Canonical location:** `GA.Business.Core.Orchestration`
- **Components:** `GaChatbot` application, `GA.Business.Core.Orchestration`

## Acceptance Criteria
- [ ] `Apps/GaChatbot/Models/ChatModels.cs`, `IGroundedNarrator.cs`, and `IHarmonicChatOrchestrator.cs` are deleted (or empty redirects removed).
- [ ] All `GaChatbot` code compiles against the orchestration-layer types only.
- [ ] Zero new warnings introduced.
- [ ] Full test suite passes after the change.

## Work Log
- 2026-03-07 — Identified in /ce:review of PR #2

## Resources
- PR: https://github.com/GuitarAlchemist/ga/pull/2
