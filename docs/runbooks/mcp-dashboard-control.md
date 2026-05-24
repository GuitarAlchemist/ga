# Runbook: MCP control of `/test#dev/*` dashboard (Phase 1)

**Status:** Phase 1 (read-only) shipped.
**Audience:** any agent (or operator) that needs to verify UI state on the
dev dashboard without writing a Playwright spec or waiting for deploy lag.

## Why this matters

Before this pattern existed, an agent that shipped a new dev-dashboard
tab had to:

1. Push the code.
2. Wait for the deploy to roll to `demos.guitaralchemist.com`.
3. Write a Playwright spec to verify the rendered output.
4. Run the spec against the deployed URL.

That loop is 3–10 minutes per check, and the Playwright spec — usually
written defensively with `crossover-skip` — only catches gross failures.

With MCP control, the agent can:

1. Open `/test#dev/*` in a local browser tab (or use the one the operator
   already has open).
2. Call `ga_dashboard_navigate({subTab: "sentrux"})`.
3. Call `ga_dashboard_screenshot()` and inspect the returned PNG.
4. Call `ga_dashboard_state()` to read structured state (visible
   components, algedonic-unack count, in-flight PR count).

That loop is sub-second per check, and works against any environment
where GaApi can be reached (localhost, tunneled prod, CI VM).

This replaces (and is strictly better than) a chunk of the existing
Playwright `crossover-skip` tests — see "Retired tests" below.

## Architecture (mirrors Prime Radiant)

```
┌─ Claude / agent ──┐
│ MCP client        │
└─────────┬─────────┘
          │ stdio
┌─────────▼─────────┐
│ GaMcpServer       │   DashboardControlTool.cs (4 tools)
│ (dotnet)          │
└─────────┬─────────┘
          │ HTTP (GAAPI_BASE_URL)
┌─────────▼─────────┐
│ GaApi             │   DashboardController.cs (4 endpoints)
│ (ASP.NET Core)    │
└─────────┬─────────┘
          │ SignalR (group "dev-dashboard")
┌─────────▼─────────┐
│ React SPA         │   McpControlProvider.tsx
│ /test#dev/*       │   (mounted only inside DevelopmentSection)
└───────────────────┘
```

Same pattern as the existing Prime Radiant `SceneControlTool` →
`GovernanceHub` → `ForceRadiant.tsx` chain — see
`GaMcpServer/Tools/SceneControlTool.cs` and
`Apps/ga-server/GaApi/Hubs/GovernanceHub.cs` for the prior art.

## The 4 tools

All tools live in `GaMcpServer/Tools/DashboardControlTool.cs` and are
registered automatically via `[McpServerToolType]`. Sub-tab names are
case-insensitive and validated server-side.

### `ga_dashboard_navigate(subTab)`

Switch the dashboard to a sub-tab on every connected client.

```jsonc
// Args
{ "subTab": "sentrux" }      // one of: summary | architecture | product |
                             // project | qa | sentrux | harness | annotations

// Returns
{ "ok": true, "tab": "sentrux", "clients_notified": 1 }
```

Returns `400` with a list of valid tabs on a typo. Returns `200` with
`clients_notified: 0` when no SPA is open — that's not an error, just
a hint that the broadcast went nowhere.

### `ga_dashboard_state()`

Ask the connected SPA for a structured snapshot of what's on screen.

```jsonc
// No args

// Returns (200)
{
  "current_tab": "sentrux",
  "visible_components": ["Sentrux", "Rule violations", "Test gaps", ...],
  "algedonic_unacked_count": 0,
  "in_flight_pr_count": 3,
  "scroll_position": { "x": 0, "y": 412 },
  "viewport_size": { "w": 1920, "h": 1080 },
  "captured_at": "2026-05-24T12:34:56.789Z"
}

// Returns 404 with hint if no SPA is connected.
// Returns 504 if the SPA didn't reply within 5s.
```

### `ga_dashboard_screenshot({subTab?, fullPage?})`

Capture a viewport (or full-page) screenshot of the dashboard.

```jsonc
// Args (both optional)
{ "subTab": "qa", "fullPage": false }

// Returns
{
  "base64_png": "iVBORw0KGgo...",       // raw base64 (no data URL prefix)
  "captured_at": "2026-05-24T12:34:56.789Z",
  "format": "image/png",
  "sub_tab": "qa"
}
```

If `subTab` is provided, the controller navigates first, sleeps 700ms
for the SPA to render, then captures. Screenshot timeout is 8s; the
SPA uses `html2canvas` to render the DOM into a canvas.

### `ga_dashboard_refresh({endpoint?})`

Tell the dashboard to invalidate fetcher data so the next state/screenshot
sees fresh values. Fire-and-forget — no waiting for a reply.

```jsonc
// Args
{ "endpoint": "/dev-data/sentrux/health" }  // or null to refresh everything

// Returns
{ "refreshed": ["/dev-data/sentrux/health"], "clients_notified": 1 }
```

Fetcher hooks subscribe to the `mcp:dashboard:refresh` window event:

```ts
useEffect(() => {
  const handler = (e: CustomEvent<DashboardRefreshDetail>) => {
    if (e.detail.endpoint === null || e.detail.endpoint === '/my/endpoint') {
      refetch();
    }
  };
  window.addEventListener('mcp:dashboard:refresh', handler as EventListener);
  return () => window.removeEventListener('mcp:dashboard:refresh', handler as EventListener);
}, [refetch]);
```

## Auth model

**Phase 1 (this PR) is read-only.** Navigate, state, screenshot, refresh —
none of these mutate any data. The hub connection is fire-and-forget; no
Cloudflare Access JWT is required.

The SignalR origin check is left at defaults: the hub accepts connections
from anywhere the GaApi CORS policy already accepts (localhost,
demos.guitaralchemist.com, the tunneled origin). Since we never write,
the worst an attacker who connects could do is observe which tab is
selected and what's on screen — and they could already do that by
loading the public dashboard URL.

**Phase 2 (future, deferred):** writes (rescan, dismiss algedonic,
run-action) would require CF Access auth. That's a separate PR; see
`docs/runbooks/cf-access-dashboard.md` for the existing auth chip pattern.

## How to extend (adding a Phase 1 tool)

1. Add the broadcast method to `Apps/ga-server/GaApi/Hubs/DevDashboardHub.cs`.
2. Add the HTTP endpoint to `Apps/ga-server/GaApi/Controllers/DashboardController.cs`.
3. Add the MCP tool to `GaMcpServer/Tools/DashboardControlTool.cs`.
4. Add the client listener to `ReactComponents/ga-react-components/src/providers/McpControlProvider.tsx`.
5. Add an integration test to `Tests/Apps/GaApi.Tests/Controllers/DashboardControllerTests.cs`.

The Phase 2 expansion (writes) should:
1. Gate the controller endpoints on a CF Access policy.
2. Add a `[McpServerTool]` precondition: agent must supply a JWT.
3. Add an audit trail to algedonic emitter (any write = potential signal).

## Common patterns

### "Agent ships a new tab; verify it without Playwright"

```
1. ga_dashboard_navigate({subTab: "harness"})
2. ga_dashboard_screenshot()                       → visual confirmation
3. ga_dashboard_state()                            → assert components rendered
```

### "Agent runs a backend action, watch dashboard update"

```
1. ga_dashboard_navigate({subTab: "sentrux"})
2. (backend: run sentrux rescan)
3. ga_dashboard_refresh({endpoint: "/dev-data/sentrux/health"})
4. ga_dashboard_state()                            → assert new quality_signal
5. ga_dashboard_screenshot({subTab: "sentrux"})    → keep PNG for the PR body
```

### "Smoke test before merging"

```
for tab in [summary, architecture, product, project, qa, sentrux, harness, annotations]:
    ga_dashboard_navigate({subTab: tab})
    state = ga_dashboard_state()
    assert state.visible_components contains at_least(1)
    save ga_dashboard_screenshot() to evidence/
```

## Retired tests

The following Playwright `crossover-skip` specs in
`ReactComponents/ga-react-components/tests/dashboard/` can be retired or
simplified as agents adopt MCP control:

| Spec | Retire / keep |
|---|---|
| `dashboard-loads.spec.ts` | **keep** — outside-in 500/blank-page check needs to stay |
| `sentrux-tab.spec.ts` | **simplify** — tab visibility check can become `ga_dashboard_state` assertion |
| `harness-tab.spec.ts` | **simplify** — same |
| `ai-annotations-tab.spec.ts` | **simplify** — same |
| `epic-drilldown.spec.ts` | **simplify** — same |
| `manifest-page.spec.ts` | **simplify** — same |
| `mission-control.spec.ts` | **simplify** — same |
| `test-plans-card.spec.ts` | **simplify** — same |
| `heartbeat-banner.spec.ts` | **keep** — checks visual state on a route not under `#dev/*` |
| `auth-gate.spec.ts` | **keep** — auth flow is not in scope for Phase 1 |

Net: roughly 6 of the 11 dashboard specs become candidates for retirement
once their assertions move to MCP `ga_dashboard_state` calls inside the
agent that introduced the feature. The retained ones stay because they
cover routes / behaviours that read-only MCP control can't observe (full
auth flows, off-`#dev` pages).

## Troubleshooting

| Symptom | Likely cause | Fix |
|---|---|---|
| `ga_dashboard_navigate` returns 200 with `clients_notified: 0` | No browser tab open on `/test#dev/*` | Open one. |
| `ga_dashboard_state` returns 404 with hint | Same | Same. |
| `ga_dashboard_screenshot` returns 504 | SPA is rendering a slow tab (e.g. Sentrux fetching a slow rescan) | Retry in a few seconds, or call `ga_dashboard_refresh` first. |
| Could not reach GaApi server | GaApi is not running | `pwsh Scripts/start-all.ps1` |
| GAAPI_BASE_URL env var overrides | The MCP tool resolves `GAAPI_BASE_URL` before falling back to `https://localhost:7001` | Set it explicitly if your GaApi is on a non-default port. |

## See also

- `Apps/ga-server/GaApi/Hubs/DevDashboardHub.cs` — hub definition
- `Apps/ga-server/GaApi/Controllers/DashboardController.cs` — HTTP layer
- `GaMcpServer/Tools/DashboardControlTool.cs` — MCP tool registrations
- `ReactComponents/ga-react-components/src/providers/McpControlProvider.tsx` — SPA client
- `Tests/Apps/GaApi.Tests/Controllers/DashboardControllerTests.cs` — integration tests
- `ReactComponents/ga-react-components/tests/dashboard/mcp-control.spec.ts` — Playwright crossover-skip test
- Prior art: `GaMcpServer/Tools/SceneControlTool.cs` (Prime Radiant pattern)
