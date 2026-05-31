# ga-react-components Context

> Fresh-session orientation for `ReactComponents/ga-react-components/`. Read this BEFORE touching anything in this subsystem.

## What this subsystem is

The React 18 + Vite SPA that serves Guitar Alchemist's UI: the AG-UI chat panel (`/chatbot`), Streamlit-embeddable panels (`/panels/diatonic`), and ~50 demo / test pages under `/test/*` (3D fretboards, Three.js + R3F + WebGPU explorations, BSP Doom explorer, Prime Radiant, Grothendieck DSL, ecosystem roadmap, …). Built as a library (`vite build` produces `ga-react-components.js`) AND served as a dev SPA on **port 5176**. Dev server proxies `/api`, `/hubs`, `/graphql` to `localhost:5232` (GaApi) and acts as a server-side auth shim for Voxtral TTS, Codestral, Ollama, and Docker Model Runner.

## Key invariants (DO NOT VIOLATE)

- **`.env` MUST NOT hardcode `VITE_API_BASE_URL`.** Vite proxy targets `localhost:5232`; a hardcoded base URL bypasses the proxy and breaks every dev + tunnel scenario. Memory rule: `feedback_api_params`.
- **Vite dev server binds `host: true` (all interfaces).** That means tablet/phone on the LAN can reach Prime Radiant. The Ollama / Docker Model Runner proxies have an explicit Origin/Referer guard for this reason — do not relax it.
- **Click-test every advertised path against the live backend before claiming done.** Memory rule `feedback_ui_click_through_before_done`: PR #210 shipped 8 of 15 broken showcase prompts past green CI. Shape-only tests are decorative; exercise the full path via Chrome (NOT Edge).
- **Three.js: no state in `useFrame`.** Layer convention from `docs/architecture/layers.md` — every R3F page MUST follow it; profiling regressions are silent otherwise.
- **MUI v5 `sx` prop only.** No `styled` components, no `makeStyles`, no `className` for layout. Visual consistency depends on it.
- **TypeScript strict; no `any`.** The eslint config will fail you. Use proper types from `src/types/` or extend them.
- **`patch-package` is load-bearing.** `postinstall` applies `patches/3d-force-graph+1.79.1.patch` — a WebGPU compatibility patch. Don't bump `3d-force-graph` past 1.79.1 without re-evaluating the patch.
- **`optimizeDeps.include` is load-bearing for the tunnel.** `demos.guitaralchemist.com` → Cloudflare → local Vite has a per-request timeout. Cold esbuild transforms exceed it → `net::ERR_FAILED`. Adding a heavy ESM dep (Three.js example modules, `@mui/x-tree-view` hooks) means adding it to `optimizeDeps.include` in `vite.config.ts`.
- **API keys live ONLY in `.env.local`, never in `.env`.** `vite.config.ts` loads `.env.local` at boot and injects `MISTRAL_API_KEY` / `CODESTRAL_API_KEY` server-side via the `/proxy/voxtral` and `/proxy/codestral` proxies. Browser code never sees the key.
- **`/test/fleet` is the latest test surface (PR #269, merged 2026-05-16).** When adding a test page, mirror it: route in `src/main.tsx`, page component in `src/pages/<Name>Test.tsx`, link from `TestIndex.tsx`.
- **CORS lives in GaChatbot.Api appsettings, not here.** If you add a dev port (e.g. 5178), update `Apps/GaChatbot.Api/appsettings.json` `Cors:AllowedOrigins` too.
- **Chatbot routes (`/chatbot`, `/ai-copilot` redirect) point at `${VITE_GA_API_URL ?? 'https://localhost:7001'}`.** That fallback is the HTTPS dev profile of GaApi, not GaChatbot.Api. Don't "fix" it to `http://localhost:5252` without testing both modes.

## The 5-10 files that matter

- `vite.config.ts` — 500+ lines: dev server config, Prime Radiant control-plane plugin, Godot static serving, all proxies, `optimizeDeps`. The most behavior-laden file in the package.
- `src/main.tsx` — route table. Adding a new page = adding a `<Route>` here.
- `src/App.tsx` — outer shell (`<App>` wrapper used by every route).
- `package.json` — `dev`, `build`, `lint`, `test` (Playwright), `test:unit` (Vitest), `generate:api` (NSwag from GaApi swagger).
- `eslint.config.js` — strict TS + React Hooks rules; CI fails on warnings.
- `playwright.config.ts` + `run-playwright-tests.ps1` — E2E surface against the dev server.
- `nswag.json` — generates `src/api/` clients from GaApi swagger. Run `npm run generate:api` after GaApi controllers change.
- `src/pages/TestIndex.tsx` — the test landing page; add new test routes here.
- `src/components/GAChatPanel/` — the AG-UI chat panel served at `/chatbot` and `/test/ga-chat`.
- `patches/` — patch-package patches; verify each on dep upgrades.

## How to add a new demo / test page

1. **Create `src/pages/<Name>Test.tsx`** wrapped in `<App>`. Use MUI `Box`/`Stack`/`Typography` for layout (`sx` prop).
2. **Add an import + `<Route path="/test/<kebab-name>" element={<App><YourTest /></App>} />`** to `src/main.tsx`. Mirror the cluster the page belongs to (3D, DSL, hand-pose, ecosystem, …).
3. **Add an entry to `src/pages/TestIndex.tsx`** so it's discoverable from `/test`.
4. **If the page calls a NEW API endpoint**, regenerate types: `npm run generate:api` (requires GaApi running on 5232 with current swagger).
5. **If the page imports a heavy ESM dep** (>200KB or many submodules), add it to `optimizeDeps.include` in `vite.config.ts`. Otherwise tunnel users see `net::ERR_FAILED` on first load.
6. **Manual click-test in Chrome at `http://localhost:5176/test/<kebab-name>`** against a real GaApi (port 5232) and GaChatbot.Api (port 5252) — both running. Then again through the tunnel at `https://demos.guitaralchemist.com/test/<kebab-name>`.
7. **Add a Playwright spec** under `src/test/` for the happy path. Shape-only assertions are not enough; assert against rendered content the user would see.

## What NOT to do here

- Don't add `VITE_API_BASE_URL` to `.env`. (Already shipped twice as a "convenience" and reverted.)
- Don't replace MUI v5 with v6 / v7 / a different system without an explicit migration plan. Many pages assume `sx` semantics.
- Don't upgrade `@react-three/fiber` past v8 without checking R3F-best-practices skill. v9 changes `useFrame` ordering.
- Don't add a build-time env var read for secrets — all auth happens via the Vite proxy injection pattern. Browser code never holds an API key.
- Don't replace `3d-force-graph` without removing the patch first; the patch references a specific internal export.
- Don't run `npm install` at the package root unless deps are explicitly NOT hoisted; the workspace hoists most deps and `postinstall` will `echo skipped` correctly.
- Don't use Edge for verification (project rule). Chrome only.
- Don't disable `hmr.overlay` cosmetically; it's already off because R3F errors spam.
- Don't add a non-allow-listed dev origin without updating `Cors:AllowedOrigins` in `Apps/GaChatbot.Api/appsettings.json` — chat calls will silently fail.
- Don't proxy a new external API without an Origin guard. The pattern is documented at the `/proxy/ollama` proxy in `vite.config.ts`.

## Where to look for related context

- Parent: [/CLAUDE.md](../../CLAUDE.md) — build/test commands, frontend conventions, Karpathy rules.
- Sibling: [`../../Apps/GaChatbot.Api/CONTEXT.md`](../../Apps/GaChatbot.Api/CONTEXT.md) — where the chat backend (and its CORS allow-list) lives.
- Sibling: [`../../Common/GA.Business.ML/Agents/CONTEXT.md`](../../Common/GA.Business.ML/Agents/CONTEXT.md) — the skill surface the chat panel renders.
- Skills (auto-discovered by Claude Code): `.agent/skills/react-frontend-engineering/SKILL.md`, `.claude/skills/r3f-best-practices/`, `.claude/skills/three-best-practices/`.
- Solutions: `docs/solutions/best-practices/showcase-demo-end-to-end-qa-2026-05-12.md`, `docs/solutions/integration-issues/playwright-tests-against-ghost-ui-2026-05-06.md`, `docs/solutions/integration-issues/2026-03-10-ag-ui-scale-event-sse-streaming-frontend-bridge.md`.
- Memory references: `feedback_api_params`, `feedback_ui_click_through_before_done`, `reference_dev_stack_three_services`, `reference_mistral_voxtral`.
