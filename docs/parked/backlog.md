# Parked work — rework later

Items discovered mid-session that are broken or sloppy but **not on the current critical path**. Park → keep moving → rework in a dedicated session.

Format: `- [ ] YYYY-MM-DD path:line — description — found while <task> — reason parked`.

## 2026-04-17

- [ ] `ga/Apps/ga-client/` vs `ga/ReactComponents/ga-react-components/` — **two React SPAs overlap**, only ga-react-components (port 5176) is actually tunneled. ga-client has nicer routes (`/chatbot`, `/demos`, `/demos/all`, `/harmonic-studio`) + my session's dense-table rewrites, but isn't served. Decide: deprecate ga-client OR swap tunnel to ga-client (5173). Tracked in `reference_ga_two_react_apps.md`.
- [ ] `ga/ReactComponents/ga-react-components/src/main.tsx:420` — `/test/fluffy-grass` renders `FluffyGrassTest` which uses `FluffyGrassDemo` — user says it's "the old one". There's a newer inline version inside `BSP/BSPDoomExplorer.tsx:2000 createFluffyGrass`. Consolidate to one implementation, route correctly.
- [ ] `ga/Apps/ga-client/src/pages/{DemosIndex,AllDemosTable}.tsx` — two disjoint demo catalogs (~17 vs ~29 entries). Merge into a single source-of-truth `demosCatalog.ts`. See prior scope proposal in session transcript.
- [ ] `ga/Apps/ga-client/src/pages/AllDemosTable.tsx` — table doesn't scroll inside the Layout wrapper because main area uses `overflow: 'hidden'`. Change to `overflow: 'auto'` when wrapping long content.
- [ ] `ga/Apps/ga-client/src/store/chatAtoms.ts:32` — was hardcoded `https://localhost:7184` (wrong port). Changed to `''` this session + bumped storage key to `ga-chat-config-v2`. Verify no other stale endpoints referenced.
- [ ] `ga/Apps/ga-client/vite.config.ts` — proxy rules for GaApi updated to `http://localhost:5232` this session, but ga-client isn't served. If ga-client becomes canonical later, re-verify.
- [ ] `GaApi.Services.CacheWarmingService` / `HybridCache[6]` — `fail:` log line on every startup. Non-fatal but noisy. Investigate root cause.
- [ ] Chatbot through tunnel → 500 Internal Server Error on `/api/chatbot/status` despite proxy routing correctly. Not a tunnel/proxy bug — API itself returns 500 when called via the ga-react-components proxy path. Likely missing route, CORS, or unhandled exception in the chatbot controller. Diagnose when wiring IX embeddings → chatbot.
- [ ] Invariant-coverage report: #29 (C# SchemaHashV4 == Rust SchemaHashV4) not wired. Part of the Tier 2 instrumentation pass. On the IX-embeddings critical path? Maybe, since embedding schema parity is load-bearing.

## Naming-and-UX drift

- [ ] "AI Copilot" → "Chatbot" rename committed in ga-client (not served). Also in docs, nav descriptions, any marketing copy — grep + sweep when the canonical SPA is decided.
- [ ] `/ai-copilot` route: currently redirects to `/chatbot` in ga-client. If ga-client is deprecated, move redirect logic to ga-react-components (or remove if not needed there).

## Voicing search init — 2026-04-18

- [ ] `GaApi.Services.VoicingIndexInitializationService.ShouldInitializeIndex()` short-circuits when `VoicingSearch:LazyLoading=true`, but there is no lazy-init code path anywhere that actually initializes on first search. Consequence: `EnhancedVoicingSearchService.SearchAsync` always throws `InvalidOperationException: Service not initialized. Call InitializeEmbeddingsAsync first.`, which `SemanticKnowledgeSource` silently swallows, so every chatbot retrieval returns empty. Workaround applied 2026-04-18: flipped `appsettings.Development.json` to `LazyLoading=false`. Real fix: implement lazy init — either (a) first search triggers eager init as a one-shot async task guarded by `SemaphoreSlim`, or (b) drop the lazy knob and always init on startup.

- [x] ~~**(critical for chatbot grounding)** `GpuVoicingSearchStrategy` init pipeline does not transfer embeddings into the search structure.~~ **Root cause found + fixed 2026-04-18**: `RagDocumentBase.Embedding` defaults to `= []` (empty float array, not null). The null-check in `EnhancedVoicingSearchService.InitializeEmbeddingsAsync` only rejected null, so empty arrays slipped through → first voicing had `Embedding = []` → `CopyEmbeddingsToGpu` read `voicings[0].Embedding.Length = 0` → `_embeddingDimensions = 0` → every query threw `Expected 0, got 768`. Fix: treat empty-and-null the same, and fall back to the 768-dim text embedding (which IS loaded from cache) as the primary search embedding when the musical one is empty. Commit pending as part of the follow-up session.

- [ ] **(perf follow-up to the above fix)** With the fix applied, the search structure holds 667k × 768-dim double = ~3.9 GB of host memory. The `MemoryUsageMb` stat overflows to `-187 MB` (int rather than long in the division). CPU cosine scan of 667k 768-dim vectors per query hangs well beyond a 60s curl timeout — either the ILGPU kernel isn't being reached, or it is and the transfer is the bottleneck. Next session: (a) confirm which code path `SemanticSearchAsync` actually hits (CPU vs GPU strategy), (b) fix `MemoryUsageMb` overflow, (c) either get ILGPU kernel working for 768-dim at 667k voicings, or shrink the index to a top-K candidate set before cosine, or use approximate-NN (HNSW) instead of exhaustive scan.

## Test infrastructure — 2026-04-17

- [ ] `Tests/Apps/GaApi.Tests/GaApi.Tests.csproj` — test discovery is completely broken. `dotnet test --list-tests` returns "No test is available" for the whole assembly, not just new tests. The csproj has correct NUnit + test-sdk package refs; the `bin/Debug/net10.0/GaApi.Tests.dll` builds fine. Likely a test-adapter version / net10.0 target compat issue. Blocks unit-testing any new GaApi controller. Workaround: smoke-test live against running GaApi.

## Dead/unreachable config

- [ ] Port `7001` referenced in multiple places as "GaApi HTTPS" — actual is `5232` HTTP per `launchSettings.json`. Grep + clean up.
- [ ] Port `7184` referenced in `chatAtoms.ts` default and any docs — phantom port, never served. Grep + remove.
