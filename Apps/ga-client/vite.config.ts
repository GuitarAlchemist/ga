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
        dedupe: ['react', 'react-dom', '@mui/material', '@mui/icons-material', '@emotion/react', '@emotion/styled'],
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
