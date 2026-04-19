import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';
import path from 'node:path';

// Deployed at https://guitaralchemist.github.io/ga/ so base must match
// the repo path. Override with VITE_BANNER_BASE for custom roots
// (e.g. '/' for Cloudflare Pages or a different hosting target).
const base = process.env.VITE_BANNER_BASE ?? '/ga/';

export default defineConfig({
  base,
  plugins: [react()],
  resolve: {
    alias: {
      // Import Ocean component straight from the workspace library. We
      // keep a single source of truth — no copy-paste drift.
      '@ocean': path.resolve(
        __dirname,
        '../ReactComponents/ga-react-components/src/components/Ocean',
      ),
    },
  },
  build: {
    outDir: 'dist',
    assetsInlineLimit: 0,
    // Warship model is ~63 MB; don't let Vite try to warn us about it.
    chunkSizeWarningLimit: 80_000,
  },
  server: {
    port: 5177,
  },
});
