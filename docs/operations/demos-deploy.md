# Demos deploy — `demos.guitaralchemist.com`

Operator-facing reference for the demos serving path. Read this first
when `demos.guitaralchemist.com` is showing stale or 404-ing routes.

## Current state (after this PR)

Two serve paths exist in parallel. **The PR does NOT change which one
the public domain points at.** That flip is an operator action,
documented below.

| Path | URL | Source | Trigger |
| --- | --- | --- | --- |
| **Legacy (live)** | `https://demos.guitaralchemist.com` | Local Vite dev server on `localhost:5176` via Cloudflare Tunnel `ga-demos` | Operator runs `pwsh Scripts/start-dev.ps1` on their workstation |
| **New (auto-deploy)** | `https://guitaralchemist.github.io/ga/` | GitHub Pages, built by `.github/workflows/deploy-demos.yml` | Push to `main` that touches `ReactComponents/ga-react-components/**` |

The legacy path is what's broken — every new route in
`ReactComponents/ga-react-components/src/main.tsx` silently 404s
until the local Vite process is restarted. The new path is
auto-rebuilt and auto-deployed on every relevant push to main.

## What gets deployed

`vite.config.demos.ts` builds the contents of
`ReactComponents/ga-react-components/index.html` and `src/main.tsx`
as a standard SPA (not a library). All test routes
(`/test/fleet`, `/test/modal-meadow`, etc.) ship as a single static
bundle plus `404.html` fallback so React Router's `BrowserRouter`
handles deep links.

The router uses `import.meta.env.BASE_URL` so the same `main.tsx`
serves both:

- The lib-mode dev server at `/` (current cloudflared origin)
- The GH Pages build at `/ga/` (new deploy path)

`public/fleet-status.json` and similar static assets are served
relative to `BASE_URL` (see `FleetStatus.tsx`).

## Verifying the deploy

After a push to `main` touching the demos surface:

```bash
gh run list --repo GuitarAlchemist/ga \
    --workflow deploy-demos --limit 5
# Pick the latest run — it should say `completed` and `success`.

# Then hit the live URL:
curl -sI https://guitaralchemist.github.io/ga/ | head -5
# Expect HTTP/2 200.

# And a deep-linked test route:
curl -sI https://guitaralchemist.github.io/ga/test/fleet | head -5
# Expect HTTP/2 200 (404.html fallback serves index.html).
```

If the workflow is red, check the build logs — the most common
breakage is a new dependency that doesn't survive `npm install`
without `patch-package`.

## Operator flip — point `demos.guitaralchemist.com` at GH Pages

**This step is owned by the operator.** Pick one of two options.

### Option 1 — Update the Cloudflare Tunnel config (recommended)

Edit `~/.cloudflared/config.yml` and change the **catch-all** ingress
rule from `service: http://localhost:5176` to a GH Pages origin via
`service: https://guitaralchemist.github.io` plus `originRequest`
host header override:

```yaml
# Keep these three rules at the top — backend stays local:
- hostname: demos.guitaralchemist.com
  path: ^/chatbot(/.*)?$
  service: http://localhost:5252
- hostname: demos.guitaralchemist.com
  path: ^/api/.*$
  service: http://localhost:5232
- hostname: demos.guitaralchemist.com
  path: ^/hubs/.*$
  service: http://localhost:5232

# CHANGED — was http://localhost:5176, now GH Pages:
- hostname: demos.guitaralchemist.com
  service: https://guitaralchemist.github.io
  originRequest:
    httpHostHeader: guitaralchemist.github.io
    # GH Pages serves the site at /ga/, so rewrite the path:
    originServerName: guitaralchemist.github.io
- service: http_status:404
```

Then `Restart-Service cloudflared`. After that:

- `demos.guitaralchemist.com/test/fleet` is served by GH Pages
- `demos.guitaralchemist.com/api/*` still routes to the local GaApi
- `demos.guitaralchemist.com/chatbot/*` still routes to GaChatbot.Api

Caveat: cloudflared's URL-rewriting to inject the `/ga/` base path is
fiddly. The cleanest variant is **Option 2** below.

### Option 2 — Custom domain on GitHub Pages (cleaner, requires DNS edit)

This retires the Cloudflare Tunnel for the demos host entirely.

1. In `https://github.com/GuitarAlchemist/ga/settings/pages`, set
   the **Custom domain** to `demos.guitaralchemist.com`. GitHub will
   add a `CNAME` file to the repo (or fail with a DNS warning).
2. In Cloudflare DNS for `guitaralchemist.com`, change the `demos`
   record:
   - **From** CNAME → `6a7697f8-9178-4c0e-91f4-b0b56e550813.cfargotunnel.com`
   - **To**   CNAME → `guitaralchemist.github.io`
   - Disable Cloudflare's orange-cloud proxy (gray cloud) for this
     record. GH Pages provisions its own TLS cert and the
     Cloudflare proxy interferes with that handshake.
3. In `vite.config.demos.ts` (or via the workflow env), change
   `DEMOS_BASE_PATH` from `/ga/` to `/`. Next push to main triggers
   a fresh deploy serving from the domain root.
4. After GH Pages reports the cert as issued (≈30 min), the
   Cloudflare Tunnel for this host can be retired.

DNS change is a one-way door — revert is a DNS edit back to the
tunnel CNAME, but propagation can take up to 5 min for Cloudflare
TTL defaults.

## Rolling back

If the new deploy breaks something:

1. **Don't change DNS.** The legacy path
   (`localhost:5176` via cloudflared) is still live and serving — the
   GH Pages deploy is additive.
2. Revert the offending commit on `main`, or `workflow_dispatch` an
   earlier good commit via the Actions UI:
   `gh workflow run deploy-demos.yml --ref <good-sha>`.
3. To stop further deploys while diagnosing:
   `gh workflow disable deploy-demos.yml`.

## What this fix does NOT cover

- **The .NET API.** `/api/*` and `/hubs/*` still come from the
  operator's local GaApi instance through Cloudflare Tunnel. Test
  pages that fetch from `/api/*` only work behind a proxy that
  routes those paths to a live GaApi.
- **The chatbot service.** `GaChatbot.Api` on `:5252` is still
  local-tunnel only.
- **Custom domain.** `demos.guitaralchemist.com` continues to point
  at the tunnel until the operator does Option 1 or Option 2 above.
