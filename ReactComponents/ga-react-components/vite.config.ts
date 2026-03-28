import {defineConfig} from 'vite'
import react from '@vitejs/plugin-react'
import dts from 'vite-plugin-dts'
import * as path from 'path'
import { createReadStream, existsSync, statSync } from 'fs'
import type { Plugin } from 'vite'

// Serve Godot HTML5 export files from /godot/ path during dev
function godotStaticPlugin(): Plugin {
    const godotDir = path.resolve(__dirname, '../../../ga-godot/export/web');
    return {
        name: 'godot-static-serve',
        configureServer(server) {
            server.middlewares.use('/godot', (req, res, next) => {
                const filePath = path.join(godotDir, req.url ?? '/index.html');
                if (existsSync(filePath) && statSync(filePath).isFile()) {
                    const ext = path.extname(filePath).toLowerCase();
                    const mimeTypes: Record<string, string> = {
                        '.html': 'text/html',
                        '.js': 'application/javascript',
                        '.wasm': 'application/wasm',
                        '.pck': 'application/octet-stream',
                        '.png': 'image/png',
                        '.svg': 'image/svg+xml',
                    };
                    res.setHeader('Content-Type', mimeTypes[ext] ?? 'application/octet-stream');
                    res.setHeader('Cross-Origin-Opener-Policy', 'same-origin');
                    res.setHeader('Cross-Origin-Embedder-Policy', 'require-corp');
                    createReadStream(filePath).pipe(res);
                } else {
                    next();
                }
            });
        },
    };
}

// ---------------------------------------------------------------------------
// Prime Radiant Control API — Claude ↔ React message bus
// POST /pr/command   — Claude sends a command (React polls & executes)
// GET  /pr/state     — Claude reads current PR state
// GET  /pr/events    — SSE stream for React to receive commands
// POST /pr/result    — React posts command execution results
// ---------------------------------------------------------------------------
interface PrCommand {
    id: string;
    action: string;
    params: Record<string, unknown>;
    timestamp: number;
}

interface PrResult {
    commandId: string;
    success: boolean;
    data?: unknown;
    error?: string;
    timestamp: number;
}

function primeRadiantControlPlugin(): Plugin {
    // Command queue: Claude pushes, React pops via SSE
    const pendingCommands: PrCommand[] = [];
    const results = new Map<string, PrResult>();
    let currentState: Record<string, unknown> = {};
    const sseClients: Set<import('http').ServerResponse> = new Set();
    let cmdCounter = 0;

    return {
        name: 'prime-radiant-control',
        configureServer(server) {
            // ── POST /pr/command — Claude sends a command ──
            server.middlewares.use('/pr/command', (req, res, next) => {
                if (req.method !== 'POST') { next(); return; }
                let body = '';
                req.on('data', (chunk: Buffer) => { body += chunk.toString(); });
                req.on('end', () => {
                    try {
                        const { action, params } = JSON.parse(body);
                        const cmd: PrCommand = {
                            id: `cmd-${++cmdCounter}-${Date.now()}`,
                            action,
                            params: params ?? {},
                            timestamp: Date.now(),
                        };
                        pendingCommands.push(cmd);
                        // Push to SSE clients
                        const sseData = `data: ${JSON.stringify(cmd)}\n\n`;
                        for (const client of sseClients) {
                            client.write(sseData);
                        }
                        res.writeHead(200, { 'Content-Type': 'application/json' });
                        res.end(JSON.stringify({ ok: true, commandId: cmd.id }));
                    } catch {
                        res.writeHead(400, { 'Content-Type': 'application/json' });
                        res.end(JSON.stringify({ ok: false, error: 'Invalid JSON' }));
                    }
                });
            });

            // ── POST /pr/batch — Claude sends multiple commands at once ──
            server.middlewares.use('/pr/batch', (req, res, next) => {
                if (req.method !== 'POST') { next(); return; }
                let body = '';
                req.on('data', (chunk: Buffer) => { body += chunk.toString(); });
                req.on('end', () => {
                    try {
                        const { commands } = JSON.parse(body) as { commands: { action: string; params?: Record<string, unknown> }[] };
                        if (!Array.isArray(commands)) {
                            res.writeHead(400, { 'Content-Type': 'application/json' });
                            res.end(JSON.stringify({ ok: false, error: 'commands must be an array' }));
                            return;
                        }
                        const commandIds: string[] = [];
                        for (const { action, params } of commands) {
                            const cmd: PrCommand = {
                                id: `cmd-${++cmdCounter}-${Date.now()}`,
                                action,
                                params: params ?? {},
                                timestamp: Date.now(),
                            };
                            pendingCommands.push(cmd);
                            commandIds.push(cmd.id);
                            // Push each command to SSE clients as a separate event
                            const sseData = `data: ${JSON.stringify(cmd)}\n\n`;
                            for (const client of sseClients) {
                                client.write(sseData);
                            }
                        }
                        res.writeHead(200, { 'Content-Type': 'application/json' });
                        res.end(JSON.stringify({ ok: true, commandIds }));
                    } catch {
                        res.writeHead(400, { 'Content-Type': 'application/json' });
                        res.end(JSON.stringify({ ok: false, error: 'Invalid JSON' }));
                    }
                });
            });

            // ── GET /pr/state — Claude reads current state ──
            server.middlewares.use('/pr/state', (req, res, next) => {
                if (req.method !== 'GET') { next(); return; }
                res.writeHead(200, { 'Content-Type': 'application/json' });
                res.end(JSON.stringify(currentState));
            });

            // ── POST /pr/state — React updates state ──
            server.middlewares.use('/pr/state', (req, res, next) => {
                if (req.method !== 'POST') { next(); return; }
                let body = '';
                req.on('data', (chunk: Buffer) => { body += chunk.toString(); });
                req.on('end', () => {
                    try {
                        currentState = JSON.parse(body);
                        res.writeHead(200, { 'Content-Type': 'application/json' });
                        res.end(JSON.stringify({ ok: true }));
                    } catch {
                        res.writeHead(400, { 'Content-Type': 'application/json' });
                        res.end(JSON.stringify({ ok: false }));
                    }
                });
            });

            // ── GET /pr/events — SSE stream for React to receive commands ──
            server.middlewares.use('/pr/events', (req, res, next) => {
                if (req.method !== 'GET') { next(); return; }
                res.writeHead(200, {
                    'Content-Type': 'text/event-stream',
                    'Cache-Control': 'no-cache',
                    'Connection': 'keep-alive',
                    'Access-Control-Allow-Origin': '*',
                });
                // Send any pending commands
                for (const cmd of pendingCommands) {
                    res.write(`data: ${JSON.stringify(cmd)}\n\n`);
                }
                sseClients.add(res);
                req.on('close', () => { sseClients.delete(res); });
            });

            // ── POST /pr/result — React posts command results ──
            server.middlewares.use('/pr/result', (req, res, next) => {
                if (req.method !== 'POST') { next(); return; }
                let body = '';
                req.on('data', (chunk: Buffer) => { body += chunk.toString(); });
                req.on('end', () => {
                    try {
                        const result: PrResult = JSON.parse(body);
                        results.set(result.commandId, result);
                        // Remove from pending
                        const idx = pendingCommands.findIndex(c => c.id === result.commandId);
                        if (idx >= 0) pendingCommands.splice(idx, 1);
                        res.writeHead(200, { 'Content-Type': 'application/json' });
                        res.end(JSON.stringify({ ok: true }));
                    } catch {
                        res.writeHead(400, { 'Content-Type': 'application/json' });
                        res.end(JSON.stringify({ ok: false }));
                    }
                });
            });

            // ── GET /pr/result/:id — Claude checks result of a command ──
            server.middlewares.use('/pr/result', (req, res, next) => {
                if (req.method !== 'GET') { next(); return; }
                const url = new URL(req.url ?? '/', `http://${req.headers.host}`);
                const cmdId = url.searchParams.get('id');
                if (!cmdId) {
                    // Return all results
                    res.writeHead(200, { 'Content-Type': 'application/json' });
                    res.end(JSON.stringify(Object.fromEntries(results)));
                    return;
                }
                const result = results.get(cmdId);
                res.writeHead(result ? 200 : 404, { 'Content-Type': 'application/json' });
                res.end(JSON.stringify(result ?? { error: 'Not found' }));
            });
        },
    };
}

export default defineConfig({
    plugins: [react(), dts(), godotStaticPlugin(), primeRadiantControlPlugin()],
    server: {
        port: 5176,
        allowedHosts: true,
        hmr: {
            overlay: false,
        },
        proxy: {
            '/api': {
                target: 'http://localhost:5232',
                changeOrigin: true,
                secure: false,
            },
            '/hubs': {
                target: 'http://localhost:5232',
                changeOrigin: true,
                secure: false,
                ws: true,
            },
            // Godot MCP relay — browser connects here, Vite proxies to MCP server
            '/ws/godot-mcp': {
                target: 'ws://127.0.0.1:6505',
                ws: true,
                changeOrigin: true,
                rewrite: () => '/',
            },
        },
    },
    optimizeDeps: {
        include: ['prop-types'],
    },
    resolve: {
        alias: {
            'prop-types': 'prop-types/prop-types.js',
        },
    },
    build: {
        lib: {
            entry: path.resolve(__dirname, 'src/index.ts'),
            name: 'GaReactComponents',
            fileName: (format) => `ga-react-components.${format}.js`
        },
        rollupOptions: {
            external: ['react', 'react-dom'],
            output: {
                globals: {
                    react: 'React',
                    'react-dom': 'ReactDOM'
                }
            }
        }
    }
})
