import {defineConfig, type UserConfig} from 'vite'
import react from '@vitejs/plugin-react'
import {visualizer} from 'rollup-plugin-visualizer'

// https://vitejs.dev/config/
export default defineConfig({
    plugins: [
        react(),
        visualizer({
            filename: 'dist/bundle-analysis.html',
            template: 'treemap',
            gzipSize: true,
            brotliSize: true,
        }),
    ],
    server: {
        allowedHosts: true,
        proxy: {
            '/api': {
                target: 'https://localhost:7001',
                changeOrigin: true,
                secure: false,
            },
            '/hubs': {
                target: 'https://localhost:7001',
                changeOrigin: true,
                secure: false,
                ws: true,
            },
        },
    },
    resolve: {
        // `three` added: ga-client lazy-imports across packages
        // (App.tsx → ReactComponents/ga-react-components/src/pages/...). Rollup
        // resolves bare specifiers from the importing file's directory upward,
        // so `import 'three'` from inside ReactComponents/ never reaches
        // Apps/ga-client/node_modules — CI failed with "Rollup failed to
        // resolve import 'three'". `dedupe` makes vite resolve the package
        // from the project root, then route all importers to that instance,
        // including sub-paths (`three/examples/...`, `three/webgpu`) via the
        // package's exports map.
        dedupe: ['react', 'react-dom', 'react-router-dom', '@mui/material', '@mui/icons-material', '@emotion/react', '@emotion/styled', 'three'],
    },
    build: {
        chunkSizeWarningLimit: 1500,
        rollupOptions: {
            output: {
                manualChunks(id) {
                    if (id.includes('node_modules')) {
                        if (id.includes('@mui')) {
                            return 'mui';
                        }
                        if (id.includes('react-markdown') || id.includes('remark-gfm') || id.includes('react-dom')) {
                            return 'react-vendor';
                        }
                    }
                    return undefined;
                },
            },
        },
    },
    test: {
        globals: true,
        environment: 'jsdom',
        setupFiles: './src/test/setup.ts',
        css: true,
        exclude: ['**/node_modules/**', '**/tests/e2e/**', '**/dist/**'],
    },
} as UserConfig)
