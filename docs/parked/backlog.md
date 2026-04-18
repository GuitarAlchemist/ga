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

## Dead/unreachable config

- [ ] Port `7001` referenced in multiple places as "GaApi HTTPS" — actual is `5232` HTTP per `launchSettings.json`. Grep + clean up.
- [ ] Port `7184` referenced in `chatAtoms.ts` default and any docs — phantom port, never served. Grep + remove.
