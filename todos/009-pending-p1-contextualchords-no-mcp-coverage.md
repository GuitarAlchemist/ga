---
status: pending
priority: p1
issue_id: "009"
tags: [agent-native, mcp, contextual-chords, code-review]
dependencies: []
---

# 009 — `ContextualChordsController` Has No MCP Tool Coverage

## Problem Statement
Five REST endpoints introduced in `ContextualChordsController` (GaApi) have no corresponding MCP tools. Agents cannot query contextual chords, borrowed chords, or voicings — capabilities that the React UI already exposes. This creates an asymmetry between what human users and AI agents can access.

## Findings
Endpoints with no MCP coverage:
- `GET /api/contextual-chords/keys/{keyName}`
- `GET /api/contextual-chords/scales/{scaleName}/{rootName}`
- `GET /api/contextual-chords/modes/{modeName}/{rootName}`
- `GET /api/contextual-chords/borrowed/{keyName}`
- `GET /api/contextual-chords/voicings/{chordName}`

- `Apps/ga-server/GaApi/Controllers/ContextualChordsController.cs`: all five actions are implemented.
- `GaMcpServer/Tools/`: no `ContextualChordsTool.cs` or equivalent closure registrations exist.

## Proposed Solutions
### Option A — Add `ContextualChordsTool.cs` to `GaMcpServer/Tools/` (recommended)
Create a new MCP tool file mirroring all five endpoints. Each tool method calls the corresponding GaApi endpoint via the configured `HttpClient` and returns the JSON response.

**Pros:** Direct, explicit mapping; consistent with the pattern used by other MCP tools in the project; easy to discover and test individually.
**Cons:** Requires maintaining parity when the controller changes.
**Effort:** Medium
**Risk:** Low

### Option B — Expose via existing `GaInvokeClosure`
Register five domain closures (e.g., `chords.contextualByKey`, `chords.borrowed`, `chords.voicings`, …) in the DSL closure registry. These closures call the controller logic directly (or via the service layer) and are then reachable via `GaInvokeClosure`.

**Pros:** No new MCP tool file; reuses the existing closure dispatch infrastructure.
**Cons:** Closures bypass the HTTP layer, making integration testing harder; increases coupling between DSL and GaApi service layer; closure discovery is less explicit than named MCP tools.
**Effort:** Large
**Risk:** Medium

## Recommended Action
(Leave blank — to be filled during triage)

## Technical Details
- **Affected files:**
  - `Apps/ga-server/GaApi/Controllers/ContextualChordsController.cs`
  - `GaMcpServer/Tools/` (new file to be created)
- **Components:** MCP server, `ContextualChordsController`, GaApi HTTP client

## Acceptance Criteria
- [ ] All five contextual-chords endpoints are reachable by an MCP agent.
- [ ] Each tool has a descriptive schema (parameters, return shape) visible in the MCP manifest.
- [ ] At least one integration test per tool verifying a non-empty response for a known key/scale/mode.
- [ ] MCP manifest is updated to reflect the new tools.

## Work Log
- 2026-03-07 — Identified in /ce:review of PR #2

## Resources
- PR: https://github.com/GuitarAlchemist/ga/pull/2
