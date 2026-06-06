# Docs Staleness Audit — 2026-06-01

Systematic content audit of all `docs/**/*.md` on `main` (424 docs): a multi-agent
sweep read each doc and verified its concrete claims (referenced files, classes, apps,
API routes) against the current code. Read-only; this report records the findings and
the remediation applied in the same PR. Re-runnable via `Scripts/audit-doc-links.py`
(links) and the `docs-staleness-audit` workflow (content).

## Verdicts

| Verdict | Count | Meaning |
|---|---|---|
| CURRENT | 221 | claims verified against code |
| DATED_RECORD | 135 | point-in-time history (plans/reports/archive/brainstorms) — not staleness |
| STALE | 50 | a central claim is contradicted by current code |
| SUPERSEDED | 3 | a newer doc owns the topic |
| UNKNOWN | 15 | could not verify (fail-closed; NOT confirmed stale) |

## Remediation applied in this PR

- **Deleted (11)** — described components/APIs/classes that were never shipped or were removed.
- **Archived (4)** — done-and-dusted; moved under `docs/archive/`.
- **Superseded/stale banners (26)** — flagged in place with the specific evidence; pending update.
- **Hand-fixed (2)** — `AGENTS.md` (corrected project map → points to authoritative `architecture/` docs; `GuitarAlchemistChatbot`→`GaChatbot.Api`) and `runbooks/non-admin-service-install.md` (`Apps/ga-react-components`→`ReactComponents/ga-react-components`).
- Broken links created by the deletions were delinkified; **0 broken links remain in live docs**.

## STALE / SUPERSEDED — evidence (drives the banners + future fixes)

| Doc | Conf | Evidence (first finding) |
|---|---|---|
| `docs/archive/conductor/tech-stack.md` | 0.98 | Claims .NET 9 — AllProjects.AppHost.csproj and all microservices target net10.0 (confirmed in file) |
| `docs/plans/2026-05-14-arch-cherny-adoption-and-tribunal-ci-plan.md` | 0.98 | Frontmatter status=superseded, superseded_by=docs/plans/2026-05-14-arch-cherny-adoption-and-tribunal-ci-plan-v2.md — explicit pointer to replacement. |
| `docs/runbooks/non-admin-service-install.md` | 0.95 | Line 47, 66: Refs 'Apps/ga-react-components' — actual location is ReactComponents/ga-react-components (confirmed via find command) |
| `docs/Performance/GPU_ACCELERATION_IMPLEMENTATION_COMPLETE.md` | 0.92 | Claims implementation date '2025-11-01' but current repo timestamp is 2026-05-29, indicating 6+ month staleness |
| `docs/References/DSL_QUICK_START.md` | 0.92 | Refs 'GA.MusicTheory.DSL' project - confirmed present at Common/GA.Business.DSL (glob found matches, DLL in bin/) |
| `docs/AGENTS.md` | 0.90 | Claims 'GuitarAlchemistChatbot' as runtime app in Apps/ — file search and ls -la show only 'GaChatbot', 'GaChatbot.Api', 'GaChatbotCli' exist; 'GuitarAlchemistC |
| `docs/Configuration/PROJECT_REORGANIZATION_SUMMARY.md` | 0.90 | Doc claims renames COMPLETED: 'GA.Business.Core.AI → GA.Business.AI' PARTIALLY TRUE (GA.Business.AI exists, but GA.Business.Core.* still exist) |
| `docs/Features/TAB_CONVERTER_REACT_DEMO_COMPLETE.md` | 0.90 | Claims 'React demo page complete' at 'ReactComponents/ga-react-components/src/components/TabConverter.tsx' — file NOT FOUND; directory exists but component miss |
| `docs/Guides/INTELLIGENT_AI_COMPLETE_GUIDE.md` | 0.90 | References 'IntelligentBSPVisualizer' React component and 'AdaptiveAIDashboard' component — COMPONENTS NOT FOUND in codebase |
| `docs/Guides/INTELLIGENT_BSP_AND_AI_GUIDE.md` | 0.90 | References 'IntelligentBSPGenerator.cs' at 'Common/GA.Business.Core/BSP/' — FILE NOT FOUND |
| `docs/RAG_VOICING_ANALYSIS_REVIEW.md` | 0.90 | Dated '2025-01-11' - document is 5+ months old |
| `docs/archive/conductor/tracks/core-maintenance/spec.md` | 0.90 | Claims OPTIC-K (v1.3.1) uses 109-dimensional embedding space — EmbeddingSchema.cs shows v1.8 with TotalDimension = 240 |
| `docs/Performance/MEMORY_OPTIMIZATION_COMPARISON.md` | 0.88 | Document compares before/after for IntelligentBSPGenerator, AdaptiveDifficultySystem, StyleLearningSystem, PatternRecognitionSystem |
| `docs/References/MUSIC_THEORY_DSL_PROPOSAL.md` | 0.88 | Line 176-179: References 'TARS fractal grammar system' at C:/Users/spare/source/repos/tars - repo exists at stated path (verified) |
| `docs/archive/Roadmap/QUARTERLY_ROADMAP_2026.md` | 0.88 | Plans Q1-Q4 2026 with milestones dated (Week 2 for Vision Agents, Week 4 for WebRTC) |
| `docs/archive/conductor/tracks/core-maintenance/plan.md` | 0.88 | Phase 1 claims .NET 10 compilation check for GA.Business.ML — but tech-stack.md says .NET 9; actual codebase uses net10.0 |
| `docs/superpowers/specs/2026-03-27-weather-clouds-design.md` | 0.88 | Refs CloudTextureProvider.tsx component at 'src/components/PrimeRadiant/CloudTextureProvider.tsx' — component does NOT exist; Grep search for 'CloudTextureProvi |
| `docs/API_DOCUMENTATION.md` | 0.85 | References endpoints like 'GET /api/chords', 'GET /api/scales', 'GET /api/fretboard/positions' — generic API contracts not verified as current |
| `docs/Configuration/DATA_LAYER_UNIFICATION_STRATEGY.md` | 0.85 | Refs 'GA.Data.MongoDB' project — does NOT exist; only GA.Data.EntityFramework exists |
| `docs/Features/GUITAR_TAB_CONVERSION_ROADMAP.md` | 0.85 | Doc references 'GA.TabConversion.Api project' and 'REST API endpoints' (POST /api/TabConversion/convert) — NOT FOUND in current codebase; only test project exis |
| `docs/GRAPHITI_RETROACTION_LOOP.md` | 0.85 | References 'KnowledgeGapAnalyzer.cs', 'YouTubeSearchService.cs', 'AutonomousCurationOrchestrator.cs' at Services/ — NONE FOUND in codebase |
| `docs/Implementation/AI_CODE_REORGANIZATION_PLAN.md` | 0.85 | Refs GA.Business.Core.AI.Fretboard.SemanticIndexing — but current code shows GA.Business.AI exists with different structure (HandPose, HuggingFace subdirs, not  |
| `docs/Integration/MCP_SERVER_ASPIRE_INTEGRATION.md` | 0.85 | References GaMcpServer project integration with Aspire. Searched codebase: GaMcpServer exists at /GaMcpServer/GaMcpServer.csproj (confirmed), but NO reference t |
| `docs/Integration/TARS_MCP_GPU_INTEGRATION_COMPLETE.md` | 0.85 | Claims TarsMcpClient.cs created at 'Common/GA.Business.Core/Diagnostics/TarsMcpClient.cs'. Search across entire codebase found ZERO instances of TarsMcpClient c |
| `docs/RAG_VOICING_IMPLEMENTATION_SUMMARY.md` | 0.85 | Dated November 2025 but current repo is May 2026 (6 months old) |
| `docs/SESSION_CONTEXT_IMPLEMENTATION.md` | 0.85 | Line 2: 'Status: Production Ready' - claims operational state but no verification of working code |
| `docs/archive/Roadmap/READY-TO-USE.md` | 0.85 | Title claims 'Vector Search - READY TO USE!' with embedded implementation status |
| `docs/archive/conductor/tracks/spectral-rag-chatbot/index.md` | 0.85 | Refs `Common/GA.Business.ML/_docs/Chatbot_Technical_Roadmap.md` — NOT FOUND at that path; actual location is docs/chatbot/Chatbot_Technical_Roadmap.md |
| `docs/archive/conductor/tracks/spectral-rag-chatbot/plan.md` | 0.85 | Implementation plan lists checkboxes for phases 1-22; many marked complete |
| `docs/methodology/invariants-catalog.md` | 0.85 | Status: Active, dated 2026-04-17 — BUT references 'Phase-A/D fix' (line 44) for invariant #21 (triad template) as if it's SHIPPED |
| `docs/reports/MUSIC_ROOM_MICROSERVICE_ARCHITECTURE.md` | 0.85 | Doc proposes MusicRoomService refactoring with Redis, MusicDataController endpoints, distributed caching (§Phase 1-4) |
| `docs/reports/MUSIC_THEORY_SELECTOR_IMPLEMENTATION.md` | 0.85 | Claims MusicTheoryController.cs created at 'Apps/ga-server/GaApi/Controllers/' (line 133) with 4 endpoints |
| `docs/reports/QUICK_WIN_1_COMPLETE.md` | 0.85 | Claims ChordProgressionTemplates.cs created at 'Apps/GuitarAlchemistChatbot/Services/' (line 14) |
| `docs/reports/QUICK_WIN_2_COMPLETE.md` | 0.85 | Claims ChordDiagram.razor component created at 'Apps/GuitarAlchemistChatbot/Components/Shared/' (line 14) |
| `docs/reports/QUICK_WIN_3_COMPLETE.md` | 0.85 | Claims ThemeToggle.razor component created at 'Apps/GuitarAlchemistChatbot/Components/Shared/' (line 14) |
| `docs/reports/THREEJS_BSP_SYSTEM_COMPLETE.md` | 0.85 | Describes 'complete implementation' of BSP-style scene loading with server at 'http://localhost:5190' and client at 'http://localhost:3000' |
| `docs/SESSION_CONTEXT_SUMMARY.md` | 0.84 | Line 4: 'Status: ✅ COMPLETE & PRODUCTION READY' - contradicted by line 310 in SESSION_CONTEXT_IMPLEMENTATION.md: 'Build Status: In Progress' |
| `docs/SESSION_CONTEXT_REGRESSION_TESTING.md` | 0.83 | Lines 28-49: References test file 'GA.Business.Core.Tests/Session/SessionContextTests.cs' - no evidence file exists from glob searches |
| `docs/reports/PLAYWRIGHT_TEST_FIXES_SUMMARY.md` | 0.82 | Details FloorManager test fixes (27 tests) at 'Tests/FloorManager.Tests.Playwright' (line 6) |
| `docs/reports/QUICK_WIN_IMPLEMENTATION_PLAN.md` | 0.82 | Comprehensive implementation plan for 5 quick wins referencing 'Apps/GuitarAlchemistChatbot' throughout (lines 18, 99, 232, 316) |
| `docs/Implementation/DSL_FIX_STRATEGY.md` | 0.80 | Describes steps to 'Remove ParseResult.fs' file — currently GA.Business.DSL still has Interpreters, LSP, Generators subdirs (no ParseResult file confirmed) |
| `docs/Integration/TARS_MCP_INTEGRATION_PLAN.md` | 0.80 | Proposes integration of TARS MCP diagnostics into GA. References GpuGrothendieckService.cs with incorrect path (Common/GA.Business.Core vs. actual Common/GA.Dom |
| `docs/archive/architecture-pre-2026-04/implementation_plan_chatbot_rewrite.md` | 0.80 | Implementation plan for 'GaChatbot' rewrite with 4 phases, all marked unchecked ([ ]) |
| `docs/archive/conductor/tracks/spectral-rag-chatbot/specs.md` | 0.80 | Lines 100-103 ref `../../Common/GA.Business.ML/_docs/Chatbot_*` — three paths do not exist at that location |
| `docs/automation/chatbot-loop.md` | 0.80 | Lines 61-67 (Iron Law): refs `Apps/ga-server/GaApi/Mcp/**` — directory does NOT exist |
| `docs/reports/MUSIC_THEORY_INTEGRATION_COMPLETE.md` | 0.80 | Claims 'InstrumentShowcase component' integration with MusicTheorySelector at ReactComponents/ga-react-components (line 13) |
| `docs/reports/PLAYWRIGHT_TESTS_COMPLETE.md` | 0.80 | Claims 46 tests created in 'Tests/GuitarAlchemistChatbot.Tests.Playwright' (line 59, 298) with three test suites |
| `docs/reports/REALISTIC_FRETBOARD_IMPLEMENTATION.md` | 0.80 | Claims RealisticFretboard.tsx component created at 'ReactComponents/ga-react-components/src/components/' using Pixi.js v8 (lines 11-13) |
| `docs/Implementation/AI_READY_API_IMPLEMENTATION.md` | 0.75 | References 'GuitarAlchemistChatbot' app — current code uses 'GaChatbot' and 'GaChatbot.Api' (not 'GuitarAlchemistChatbot') |
| `docs/Integration/REDIS_AI_INTEGRATION.md` | 0.75 | Proposes Redis for AI with vector search, caching, semantic search. Assumes Redis Stack is planned/available. No evidence in codebase that Redis Stack upgrade o |
| `docs/Integration/REDIS_AI_QUICK_START.md` | 0.75 | Quick-start guide assumes Redis Stack is available and configured. No current docker-compose.yml verified with redis/redis-stack image. |
| `docs/Performance/ADVANCED_OPTIMIZATION_OPPORTUNITIES.md` | 0.75 | Optimization analysis dated 2025-11-01 with 23 opportunities. Claims GpuGrothendieckService exists at 'Apps/ga-server/GaApi/Services/' (WRONG PATH — should be C |
| `docs/archive/conductor/workflows/mongodb-voicing-reindex.md` | 0.75 | Line 310: refs `Apps/ga-server/GaApi/Services/MongoVectorSearchIndexes.cs` — NOT FOUND in codebase |

## UNKNOWN — need human verification (not auto-touched)

- `docs/Features/STREAMING_API_QUICK_REFERENCE.md`
- `docs/Guides/ADVANCED_TECHNIQUES_GUIDE.md`
- `docs/Guides/BUILD_FIX_GUIDE.md`
- `docs/SESSION_CONTEXT_TESTING.md`
- `docs/VOICING_SEARCH_IMPROVEMENTS.md`
- `docs/architecture/chatbot-claude-handoff.md`
- `docs/architecture/chatbot-overview.md`
- `docs/architecture/cherny-loops-cross-repo.md`
- `docs/brainstorms/2026-03-25-living-cosmos-requirements.md`
- `docs/brainstorms/2026-03-26-algedonic-visual-signals-requirements.md`
- `docs/brainstorms/2026-03-26-prime-radiant-responsive-declutter-requirements.md`
- `docs/brainstorms/2026-03-27-icicle-drawer-requirements.md`
- `docs/brainstorms/2026-03-28-ixql-ui-grammar-requirements.md`
- `docs/plans/2026-05-12-chatbot-cleanup-tracker.md`
- `docs/plans/2026-05-13-mercury-subagent-evaluation-plan.md`
