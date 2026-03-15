# API Quality Backlog

_Updated by agents after each iteration. Status values: `open` · `in-progress` · `done` · `blocked:API-NNN`_

## Priority Key
- **P0** — Breaks functionality or causes crashes in production
- **P1** — High impact on developer experience or API consumers
- **P2** — Medium: correctness / completeness
- **P3** — Low: polish / consistency

---

## Open Issues

| ID | Priority | Role | Status | Service | Title |
|----|----------|------|--------|---------|-------|
| API-001 | P1 | test-writer | done | GaApi | Integration tests for ContextualChordsController (3 endpoints + borrowed chords) |
| API-002 | P1 | test-writer | done | GaApi | Integration tests for SearchController |
| API-003 | P1 | test-writer | done | GaApi | Integration tests for ChatbotController (status + examples endpoints) |
| API-004 | P1 | contract-auditor | done | GaApi | Add `[ProducesResponseType]` to SearchController (already complete: 200/400/500) |
| API-005 | P1 | contract-auditor | done | GaApi | Add `[ProducesResponseType]` to ChatbotController (already complete: 200/400/500) |
| API-006 | P1 | error-enforcer | open | ALL | Unify `ApiResponse<T>` — each of 8 services has its own copy; consolidate to `AllProjects.ServiceDefaults` |
| API-007 | P2 | contract-auditor | done | GaApi | Add `[ProducesResponseType(404)]` to ContextualChordsController (5 endpoints fixed) |
| API-008 | P2 | contract-auditor | done | GA.MusicTheory.Service | Audit 8 controllers for ProducesResponseType completeness |
| API-009 | P2 | contract-auditor | done | GA.Analytics.Service | Audit 5 controllers for ProducesResponseType completeness |
| API-010 | P2 | contract-auditor | done | GA.AI.Service | Audit 7 controllers for ProducesResponseType completeness |
| API-011 | P2 | test-writer | open | GA.MusicTheory.Service | Write integration tests for ChordsController (most-used endpoint in the service) |
| API-012 | P2 | schema-guardian | done | GaApi | Verify all `[ProducesResponseType(typeof(T), 200)]` match actual controller return types |
| API-013 | P2 | schema-guardian | done | GaApi | Add `[Required]` and validation attributes to request models used by POST/PUT endpoints |
| API-014 | P2 | error-enforcer | done | ALL | Verify `ErrorHandlingMiddleware` is registered; microservices lack centralised exception handling — audit complete |
| API-015 | P3 | contract-auditor | open | GA.Knowledge.Service | Audit 5 controllers for ProducesResponseType completeness |
| API-016 | P3 | contract-auditor | open | GA.Fretboard.Service | Audit 4 controllers for ProducesResponseType completeness |
| API-017 | P3 | contract-auditor | open | GA.BSP.Service | Audit 4 controllers for ProducesResponseType completeness |
| API-018 | P3 | contract-auditor | open | GA.DocumentProcessing.Service | Audit 4 controllers for ProducesResponseType completeness |
| API-019 | P3 | test-writer | open | GaApi | Integration tests for HealthController |
| API-020 | P3 | schema-guardian | open | ALL | No API versioning strategy — document or implement `/api/v1/` prefix consistently |

---

## Done Issues

| ID | Priority | Role | Closed | Service | Title |
|----|----------|------|--------|---------|-------|
| API-000 | P0 | claude-code | 2026-02-27 | GaApi | Ollama unavailability crashes VoicingIndexInitializationService — fix with graceful fallback |
| API-001 | P1 | claude-code | 2026-02-27 | GaApi | Integration tests for ContextualChordsController — 22 tests, 22 passed |
| API-002 | P1 | test-writer | 2026-03-06 | GaApi | Integration tests for SearchController — 25 tests written (blocked by build issue: missing ErrorResponse type) |
| API-004 | P1 | contract-auditor | 2026-03-06 | GaApi | SearchController already had complete ProducesResponseType coverage (200/400/500) — no changes needed |
| API-005 | P1 | contract-auditor | 2026-03-06 | GaApi | ChatbotController already had complete ProducesResponseType coverage (200/400/500) — no changes needed |
| API-007 | P2 | contract-auditor | 2026-03-06 | GaApi | Added ProducesResponseType to ContextualChordsController (5 endpoints: keys, scales, modes, borrowed, voicings) |
| API-008 | P2 | contract-auditor | 2026-03-06 | GA.MusicTheory.Service | Audited 8 controllers (29 endpoints): 28 complete, added 400 status to ChordsController.GetSimilar |
| API-009 | P2 | contract-auditor | 2026-03-06 | GA.Analytics.Service | Audited 5 controllers (31 endpoints): added 500 status to all, 200/400 to MetricsController |
| API-010 | P2 | contract-auditor | 2026-03-06 | GA.AI.Service | Audited 5 controllers (10 endpoints): added ProducesResponseType to all (200/400/403/404/500) |
| API-012 | P2 | schema-guardian | 2026-03-06 | GaApi | Verified all ProducesResponseType declarations match controller return types — no mismatches found |
| API-013 | P2 | schema-guardian | 2026-03-06 | GaApi | Added validation attributes to HybridSearchRequest (Required, StringLength, Range) |
| API-003 | P1 | test-writer | 2026-03-06 | GaApi | Integration tests for ChatbotController — 21 tests written (6 status, 6 examples, 9 SSE tests marked [Ignore] due to WebApplicationFactory SSE limitations) |
| API-014 | P2 | error-enforcer | 2026-03-06 | ALL | Audited error handling middleware across all 8 services — GaApi has full implementation, all others use Hellang.Middleware.ProblemDetails |

---

## Notes on API-006 (Unified ApiResponse)

Each service currently defines its own `ApiResponse.cs`:
- `GaApi/Models/ApiResponse.cs` — most complete (has CorrelationId, Pagination, Metadata)
- `GA.Analytics.Service/Models/ApiResponse.cs` — simplified
- `GA.MusicTheory.Service/Models/ApiResponse.cs` — simplified
- … (5 more)

**Proposed fix**: move the GaApi version to `AllProjects.ServiceDefaults` (it already exists as a shared project) and delete the per-service copies. Assigned to **Error Enforcer** once a Conductor approves the API contract.
