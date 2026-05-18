// vite.config.demos.ts — SPA build configuration for the demos site.
//
// The default `vite.config.ts` builds this package as a *library* (lib mode)
// for consumption by Apps/ga-client. Library mode does not produce an
// index.html + asset bundle the way GitHub Pages needs.
//
// This config is loaded by `npm run build:demos` (see package.json) and
// emits a standard SPA bundle into `dist-demos/`. It is invoked from the
// GitHub Actions `deploy-demos.yml` workflow on push-to-main.
//
// Base path notes:
// - We use `base: './'` so the generated bundle works at any sub-path
//   (e.g. https://guitaralchemist.github.io/ga/ or a future custom domain
//   served at the root). Relative URLs in index.html and bundled assets
//   are the safest choice when the eventual serve path may change.
// - For React Router we rely on `BrowserRouter` with `basename` injected
//   at runtime by `main.tsx` via `import.meta.env.BASE_URL`. The router
//   already lives in main.tsx; the env var is wired in deploy-demos.yml.
//
// What this config does NOT do:
// - It does not run on every CI build — only the deploy-demos workflow
//   calls it. Library publishes still use the default `vite.config.ts`.
// - It does not bundle the .NET API. `/api/*` fetches from inside test
//   pages will only succeed when an operator-controlled reverse proxy
//   (current Cloudflare Tunnel, or a future replacement) sits in front
//   of the static site and routes `/api/*` to a live GaApi instance.
import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';
import * as path from 'path';

// BASE_PATH lets the deploy workflow point at either the GitHub Pages
// project sub-path (`/ga/`) or a custom-domain root (`/`). Default to
// `./` so a developer running `npm run build:demos` locally gets a
// portable bundle without committing to a single serve path.
const basePath = process.env.DEMOS_BASE_PATH ?? './';

export default defineConfig({
    base: basePath,
    plugins: [react()],
    // Build the SPA, not the library. Entry is the standard index.html
    // at the package root, which already mounts /src/main.tsx — the same
    // file the Vite dev server uses today behind cloudflared.
    build: {
        outDir: 'dist-demos',
        emptyOutDir: true,
        // GitHub Pages caps individual files at 100 MB; warn at 5 MB so
        // we notice if a planet texture or splat sneaks past `public/`
        // size budget.
        chunkSizeWarningLimit: 5000,
        rollupOptions: {
            // No `external` here — unlike lib mode we WANT react/react-dom
            // bundled so the static site is self-contained.
            input: path.resolve(__dirname, 'index.html'),
        },
    },
    resolve: {
        alias: {
            // Match the runtime alias from the library config so dev and
            // demos bundles resolve `prop-types` identically.
            'prop-types': 'prop-types/prop-types.js',
        },
    },
});
