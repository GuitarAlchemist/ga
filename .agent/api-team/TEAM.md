# Guitar Alchemist — API Quality Agent Team

## Mission
Continuously audit, test, and improve the quality of all 43+ REST endpoints across 8 microservices through structured, incremental iteration.

## The Team

| Handle | Role | Primary Skill | Typical Tool |
|--------|------|---------------|--------------|
| **Conductor** | Orchestrates the iteration; triages BACKLOG.md; unblocks others | — | All |
| **Contract Auditor** | Scans controllers for missing `[ProducesResponseType]`, bad status codes, naming violations | `api-contract-auditor` | Glob, Grep, Read |
| **Test Writer** | Writes `WebApplicationFactory` integration tests for untested controllers | `api-test-writer` | Read, Write, Bash |
| **Schema Guardian** | Verifies response models match declared types; validates request validation attributes | `api-schema-guardian` | Read, Grep |
| **Error Enforcer** | Standardises error handling and `ApiResponse<T>` usage across all 8 services | `api-error-enforcer` | Grep, Edit |

Any AI agent (Claude Code, Gemini CLI, Codex CLI, AntiGravity, etc.) can fulfil any role in a given session by loading the matching skill file.

---

## Iteration Protocol

```
AUDIT → BACKLOG → FIX → VERIFY → repeat
```

### Step-by-step

1. **Conductor** opens `BACKLOG.md`, assigns the top-priority `open` items by setting `Assigned` to the relevant role.
2. **Contract Auditor** scans controllers with `Grep`/`Glob`, adds newly found issues as `open` rows.
3. **Test Writer / Schema Guardian / Error Enforcer** each pick up `open` items tagged for their role, implement the fix, then set status → `done`.
4. **All agents** run `pwsh Scripts/api-quality-check.ps1` before marking any item `done`. The script must exit 0.
5. **Conductor** reviews closed items, promotes the next `open` batch, and closes the iteration.

### Escalation rule
If an item blocks another (e.g., a model change is needed before a test can be written), the blocker row is marked `blocked:API-NNN` in the Notes column.

---

## File Conventions

| Path | Purpose |
|------|---------|
| `.agent/api-team/BACKLOG.md` | Canonical issue tracker — every agent reads and writes here |
| `.agent/api-team/TEAM.md` | This file |
| `.agent/skills/api-contract-auditor/SKILL.md` | Skill for Contract Auditor role |
| `.agent/skills/api-test-writer/SKILL.md` | Skill for Test Writer role |
| `.agent/skills/api-schema-guardian/SKILL.md` | Skill for Schema Guardian role |
| `.agent/skills/api-error-enforcer/SKILL.md` | Skill for Error Enforcer role |
| `Scripts/api-quality-check.ps1` | Automated quality gate run by every agent before closing an item |
| `Tests/Apps/GaApi.Tests/Controllers/` | Target directory for new integration tests |

---

## API Surface Summary (as of 2026-02-27)

| Service | Controllers | Tested |
|---------|-------------|--------|
| GA.MusicTheory.Service | 8 | 0 |
| GA.AI.Service | 7 | 0 |
| GaApi (gateway) | 6 | 2 |
| GA.Analytics.Service | 5 | 0 |
| GA.Knowledge.Service | 5 | 0 |
| GA.Fretboard.Service | 4 | 0 |
| GA.BSP.Service | 4 | 0 |
| GA.DocumentProcessing.Service | 4 | 0 |
| **Total** | **43** | **2** |

---

## How to Join Mid-Stream

1. Read `BACKLOG.md` — find the highest-priority `open` item matching your role.
2. Load your role's SKILL.md from `.agent/skills/<role>/SKILL.md`.
3. Implement the fix.
4. Run `pwsh Scripts/api-quality-check.ps1` — confirm exit 0.
5. Update the BACKLOG row: `done`.
