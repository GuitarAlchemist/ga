import { defineConfig } from 'vite'

export default defineConfig({
  root: '.',
  publicDir: 'public',
  build: {
    outDir: 'dist',
    emptyOutDir: true,
    sourcemap: true,
    target: 'es2022'
  },
  server: {
    port: 3000,
    open: true
  },
  optimizeDeps: {
    include: ['three', 'three-mesh-bvh']
  }
})
