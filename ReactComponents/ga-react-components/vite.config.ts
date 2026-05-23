import {defineConfig, loadEnv} from 'vite'
import react from '@vitejs/plugin-react'
import dts from 'vite-plugin-dts'
import * as path from 'path'
import { createReadStream, existsSync, statSync, readFileSync, readdirSync } from 'fs'
import { execFileSync } from 'child_process'
import type { Plugin } from 'vite'
import { parseBacklog, extractDocTitle, binActivityByDay } from './src/dev-data/parsers'
import type { BacklogPayload } from './src/dev-data/parsers'

// Load ALL env vars (not just VITE_*) from .env.local for proxy auth injection
try {
  const envLocal = path.resolve(__dirname, '.env.local');
  if (existsSync(envLocal)) {
    const lines = readFileSync(envLocal, 'utf-8').split('\n');
    for (const line of lines) {
      const trimmed = line.trim();
      if (!trimmed || trimmed.startsWith('#')) continue;
      const eq = trimmed.indexOf('=');
      if (eq > 0) {
        const key = trimmed.slice(0, eq).trim();
        const val = trimmed.slice(eq + 1).trim();
        if (!process.env[key]) process.env[key] = val;
      }
    }
  }
} catch { /* ignore */ }

// Serve repo-root dev artifacts (BACKLOG.md, state/quality/*, git log,
// docs/architecture/) for the Development dashboard on /test, plus a single
// /dev-data/manifest aggregated JSON that both the UI and external AI tools
// (Claude, Junie, Codex) can hit to bootstrap project context.
//
// Every endpoint reads fresh per request so CI-pushed quality snapshots,
// edited BACKLOG.md, and new commits show up without a rebuild.
//
// SECURITY: this plugin is dev-only (configureServer; vite build strips it),
// BUT the dev server is exposed publicly via Cloudflare tunnel
// (demos.guitaralchemist.com). Each endpoint gates on isLocalOrigin() so
// external clients get 403. localhost / 127.0.0.1 / private LAN ranges
// (192.168/16, 10/8, 172.16/12) are allowed; everything else is rejected.
//
// Schema is intentionally stable — see /dev-data/manifest top-level keys.
// Add new fields rather than renaming existing ones.
// SECURITY: the Cloudflare tunnel proxies remote traffic to localhost:5176,
// so req.socket.remoteAddress is always 127.0.0.1 — useless as a trust
// signal. The real signal is the Host header: tunnel traffic carries the
// public hostname (demos.guitaralchemist.com); direct-local traffic carries
// localhost:5176 / 127.0.0.1 / LAN IP. We also reject anything bearing
// Cloudflare headers (CF-Ray, CF-Connecting-IP, etc.) as a belt-and-suspenders
// check. Used by /dev-data/* and /pr/* middlewares to keep them local-only.
function isLocalOrigin(req: import('http').IncomingMessage): boolean {
    const host = ((req.headers.host as string) || '').toLowerCase().split(':')[0];
    if (!host) return false;
    for (const k of Object.keys(req.headers)) {
        if (k.toLowerCase().startsWith('cf-')) return false;
    }
    const isPrivate = (h: string): boolean => {
        if (h === 'localhost' || h === '127.0.0.1' || h === '::1') return true;
        if (/^192\.168\./.test(h)) return true;
        if (/^10\./.test(h)) return true;
        if (/^172\.(1[6-9]|2[0-9]|3[0-1])\./.test(h)) return true;
        return false;
    };
    if (!isPrivate(host)) return false;
    const origin = (req.headers.origin as string) || '';
    const referer = (req.headers.referer as string) || '';
    const checkUrl = (raw: string): boolean => {
        if (!raw) return true;
        try { return isPrivate(new URL(raw).hostname); } catch { return false; }
    };
    if (!checkUrl(origin)) return false;
    if (!checkUrl(referer)) return false;
    return true;
}

function gateLocal(req: import('http').IncomingMessage, res: import('http').ServerResponse, label = 'dev-data'): boolean {
    if (isLocalOrigin(req)) return true;
    res.writeHead(403, { 'Content-Type': 'application/json' });
    res.end(JSON.stringify({ error: 'forbidden', reason: `${label} endpoints are local-only` }));
    return false;
}

function devDataPlugin(): Plugin {
    const repoRoot = path.resolve(__dirname, '../..');

    interface QualityEntry { source: string; data: unknown }
    function gatherQuality(): { domains: Record<string, QualityEntry>; regressions: string[] } {
        const qualityDir = path.join(repoRoot, 'state', 'quality');
        const domains: Record<string, QualityEntry> = {};
        const regressions: string[] = [];
        if (!existsSync(qualityDir)) return { domains, regressions };

        for (const entry of readdirSync(qualityDir, { withFileTypes: true })) {
            if (!entry.isDirectory()) continue;
            const subDir = path.join(qualityDir, entry.name);
            const lastJson = path.join(subDir, 'last.json');
            let chosen: QualityEntry | null = null;
            if (existsSync(lastJson)) {
                try {
                    chosen = { source: 'last.json', data: JSON.parse(readFileSync(lastJson, 'utf-8')) };
                } catch (e) {
                    chosen = { source: 'last.json', data: { error: 'parse_failed', message: String(e) } };
                }
            } else {
                const dated = readdirSync(subDir)
                    .filter((f) => /^\d{4}-\d{2}-\d{2}\.json$/.test(f))
                    .sort();
                const latest = dated[dated.length - 1];
                if (latest) {
                    try {
                        const data = JSON.parse(readFileSync(path.join(subDir, latest), 'utf-8'));
                        chosen = { source: latest, data };
                        // Compare against prior dated snapshot for regression detection
                        const prior = dated[dated.length - 2];
                        if (prior) {
                            try {
                                const priorData = JSON.parse(readFileSync(path.join(subDir, prior), 'utf-8'));
                                const curVal = (data as Record<string, unknown>).metric_value;
                                const priorVal = (priorData as Record<string, unknown>).metric_value;
                                if (typeof curVal === 'number' && typeof priorVal === 'number' && curVal < priorVal) {
                                    regressions.push(`${entry.name}: ${priorVal} → ${curVal}`);
                                }
                            } catch { /* ignore comparison failures */ }
                        }
                    } catch (e) {
                        chosen = { source: latest, data: { error: 'parse_failed', message: String(e) } };
                    }
                }
            }
            if (chosen) {
                domains[entry.name] = chosen;
                const status = (chosen.data as Record<string, unknown>).oracle_status;
                if (status && status !== 'ok') regressions.push(`${entry.name}: oracle_status=${status}`);
            }
        }
        return { domains, regressions };
    }

    interface ActivityEntry { sha: string; short_sha: string; author: string; date: string; subject: string }
    function gatherActivity(limit = 10): ActivityEntry[] | { error: string } {
        // SECURITY: argv-form execFileSync — no shell, no interpolation, no
        // injection surface even if limit ever escapes its numeric clamp.
        try {
            const out = execFileSync(
                'git',
                ['log', '-n', String(Math.max(1, Math.floor(limit))), '--pretty=format:%H%x09%an%x09%aI%x09%s'],
                { cwd: repoRoot, encoding: 'utf-8', timeout: 5000 },
            );
            return out.trim().split('\n').filter(Boolean).map((line) => {
                const [sha, author, date, ...rest] = line.split('\t');
                return { sha, short_sha: sha.slice(0, 8), author, date, subject: rest.join('\t') };
            });
        } catch (e) {
            return { error: String((e as Error).message ?? e) };
        }
    }

    interface ActivityByDay { date: string; count: number }
    function gatherActivityByDay(days = 30): ActivityByDay[] | { error: string } {
        try {
            const out = execFileSync(
                'git',
                ['log', `--since=${Math.max(1, Math.floor(days))} days ago`, '--pretty=format:%aI'],
                { cwd: repoRoot, encoding: 'utf-8', timeout: 5000 },
            );
            const bins: Record<string, number> = {};
            // Pre-seed every day in the window with 0 so the chart always has full extent
            const now = new Date();
            for (let i = days - 1; i >= 0; i -= 1) {
                const d = new Date(now);
                d.setDate(d.getDate() - i);
                const key = d.toISOString().slice(0, 10);
                bins[key] = 0;
            }
            for (const line of out.split('\n')) {
                if (!line) continue;
                const day = line.slice(0, 10);
                if (day in bins) bins[day] += 1;
            }
            return Object.entries(bins).map(([date, count]) => ({ date, count }));
        } catch (e) {
            return { error: String((e as Error).message ?? e) };
        }
    }

    interface ArchDoc { file: string; title: string; modified_at: string; size: number }
    function gatherArchitecture(): ArchDoc[] {
        const archDir = path.join(repoRoot, 'docs', 'architecture');
        if (!existsSync(archDir)) return [];
        return readdirSync(archDir)
            .filter((f) => f.endsWith('.md'))
            .map((f) => {
                const full = path.join(archDir, f);
                const stat = statSync(full);
                let title = f.replace(/\.md$/, '').replace(/-/g, ' ');
                try {
                    const head = readFileSync(full, 'utf-8').split('\n').slice(0, 15);
                    const h1 = head.find((l) => /^#\s/.test(l));
                    if (h1) title = h1.replace(/^#\s+/, '').trim();
                    else {
                        // Fall back to frontmatter title:
                        const fm = head.find((l) => /^title:\s/.test(l));
                        if (fm) title = fm.replace(/^title:\s+/, '').trim().replace(/^["']|["']$/g, '');
                    }
                } catch { /* ignore */ }
                return { file: f, title, modified_at: stat.mtime.toISOString(), size: stat.size };
            })
            .sort((a, b) => b.modified_at.localeCompare(a.modified_at));
    }

    function gatherBacklog(): BacklogPayload | null {
        const p = path.join(repoRoot, 'BACKLOG.md');
        if (!existsSync(p)) return null;
        return parseBacklog(readFileSync(p, 'utf-8'));
    }

    interface AgentFileEntry {
        path: string;
        exists: boolean;
        size: number | null;
        modified_at: string | null;
        is_directory: boolean;
        description: string;
    }
    function gatherAgentFiles(): AgentFileEntry[] {
        // Files and directories that govern how AI agents (Claude, Antigravity v2, codex,
        // Gemini CLI) behave in this repo. Order roughly matches "general → specific".
        const entries: { rel: string; description: string }[] = [
            { rel: 'CLAUDE.md', description: 'Canonical agent rules — Claude reads this; AGENTS.md is auto-synced from it.' },
            { rel: 'AGENTS.md', description: 'codex/OpenAI convention. Auto-generated from CLAUDE.md via Scripts/sync-agents-md.ps1.' },
            { rel: 'GEMINI.md', description: 'Gemini CLI / Antigravity native AI configuration entry point.' },
            { rel: '.mcp.json', description: 'Project-level MCP server registrations. Antigravity v2 and Claude Code both read this.' },
            { rel: '.claude', description: 'Claude Code project settings, skills, hooks, slash commands.' },
            { rel: '.gemini', description: 'Gemini CLI / Antigravity settings, commands, skills.' },
            { rel: '.codex', description: 'Codex CLI workspace config (rules, slash commands).' },
            { rel: '.agent/skills', description: 'Shared agent skills (language standards, ROP, etc.) auto-discovered by Claude Code.' },
            { rel: 'Scripts/sync-agents-md.ps1', description: 'Keeps AGENTS.md in sync with CLAUDE.md.' },
            { rel: '.githooks/pre-commit', description: 'Pre-commit hook that runs sync-agents-md, dotnet format, build, ROP check.' },
        ];
        return entries.map(({ rel, description }) => {
            const full = path.join(repoRoot, rel);
            const exists = existsSync(full);
            if (!exists) {
                return { path: rel, exists: false, size: null, modified_at: null, is_directory: false, description };
            }
            const stat = statSync(full);
            return {
                path: rel,
                exists: true,
                size: stat.isFile() ? stat.size : null,
                modified_at: stat.mtime.toISOString(),
                is_directory: stat.isDirectory(),
                description,
            };
        });
    }

    function gatherMcpServers(): { count: number; names: string[]; types: Record<string, string> } {
        // SECURITY: never serve raw .mcp.json — it may contain env-var references
        // (${DEMERZEL_API_KEY} etc.) that would be auto-exfiltrated if anyone
        // ever inlines a resolved secret for debugging. Names + transport type
        // only; consumers can look at their own .mcp.json for full config.
        const mcpPath = path.join(repoRoot, '.mcp.json');
        if (!existsSync(mcpPath)) return { count: 0, names: [], types: {} };
        try {
            const raw = JSON.parse(readFileSync(mcpPath, 'utf-8'));
            const servers = raw?.mcpServers ?? {};
            const names = Object.keys(servers);
            const types: Record<string, string> = {};
            for (const name of names) {
                const s = servers[name];
                types[name] = (s?.type as string) ?? (s?.url ? 'http' : 'stdio');
            }
            return { count: names.length, names, types };
        } catch {
            return { count: 0, names: [], types: {} };
        }
    }

    interface ServiceTopology { name: string; port: number; public_path: string; expected: string }
    const serviceTopology: ServiceTopology[] = [
        { name: 'ga-react-components (Vite SPA)', port: 5176, public_path: '/', expected: 'serves React SPA + dev-data middleware' },
        { name: 'GaApi', port: 5232, public_path: '/api/*, /hubs/*', expected: '/health → "Healthy"' },
        { name: 'GaChatbot.Api', port: 5252, public_path: '/chatbot/*, /api/chatbot/*', expected: '/api/chatbot/status → JSON' },
        { name: 'cloudflared (ga-demos)', port: 0, public_path: 'demos.guitaralchemist.com', expected: 'reverse tunnel to local services' },
    ];

    return {
        name: 'dev-data',
        configureServer(server) {
            server.middlewares.use('/dev-data/backlog', (req, res, next) => {
                if (req.method !== 'GET') { next(); return; }
                if (!gateLocal(req, res)) return;
                const p = path.join(repoRoot, 'BACKLOG.md');
                if (!existsSync(p)) {
                    res.writeHead(404, { 'Content-Type': 'text/plain' });
                    res.end('BACKLOG.md not found');
                    return;
                }
                res.writeHead(200, { 'Content-Type': 'text/markdown; charset=utf-8', 'Cache-Control': 'no-store' });
                res.end(readFileSync(p, 'utf-8'));
            });

            server.middlewares.use('/dev-data/quality', (req, res, next) => {
                if (req.method !== 'GET') { next(); return; }
                if (!gateLocal(req, res)) return;
                res.writeHead(200, { 'Content-Type': 'application/json', 'Cache-Control': 'no-store' });
                res.end(JSON.stringify({ generated_at: new Date().toISOString(), ...gatherQuality() }));
            });

            server.middlewares.use('/dev-data/activity', (req, res, next) => {
                if (req.method !== 'GET') { next(); return; }
                if (!gateLocal(req, res)) return;
                const url = new URL(req.url ?? '/', `http://${req.headers.host}`);
                const limit = Math.max(1, Math.min(50, Number(url.searchParams.get('limit') ?? 10)));
                const days = Math.max(1, Math.min(180, Number(url.searchParams.get('days') ?? 30)));
                res.writeHead(200, { 'Content-Type': 'application/json', 'Cache-Control': 'no-store' });
                res.end(JSON.stringify({
                    generated_at: new Date().toISOString(),
                    commits: gatherActivity(limit),
                    by_day: gatherActivityByDay(days),
                }));
            });

            server.middlewares.use('/dev-data/architecture', (req, res, next) => {
                if (req.method !== 'GET') { next(); return; }
                if (!gateLocal(req, res)) return;
                res.writeHead(200, { 'Content-Type': 'application/json', 'Cache-Control': 'no-store' });
                res.end(JSON.stringify({ generated_at: new Date().toISOString(), docs: gatherArchitecture() }));
            });

            server.middlewares.use('/dev-data/agents', (req, res, next) => {
                if (req.method !== 'GET') { next(); return; }
                if (!gateLocal(req, res)) return;
                res.writeHead(200, { 'Content-Type': 'application/json', 'Cache-Control': 'no-store' });
                res.end(JSON.stringify({
                    generated_at: new Date().toISOString(),
                    agent_files: gatherAgentFiles(),
                    mcp_servers: gatherMcpServers(),
                }));
            });

            server.middlewares.use('/dev-data/manifest', (req, res, next) => {
                if (req.method !== 'GET') { next(); return; }
                if (!gateLocal(req, res)) return;
                const manifest = {
                    schema_version: '1.0.0',
                    generated_at: new Date().toISOString(),
                    repo: 'GuitarAlchemist/ga',
                    public_url: 'https://demos.guitaralchemist.com',
                    endpoints: {
                        backlog: '/dev-data/backlog',
                        quality: '/dev-data/quality',
                        activity: '/dev-data/activity',
                        architecture: '/dev-data/architecture',
                        agents: '/dev-data/agents',
                        manifest: '/dev-data/manifest',
                    },
                    services: serviceTopology,
                    backlog: gatherBacklog(),
                    quality: gatherQuality(),
                    activity: gatherActivity(10),
                    activity_by_day: gatherActivityByDay(30),
                    architecture: gatherArchitecture(),
                    agent_files: gatherAgentFiles(),
                    mcp_servers: gatherMcpServers(),
                };
                res.writeHead(200, { 'Content-Type': 'application/json', 'Cache-Control': 'no-store' });
                res.end(JSON.stringify(manifest, null, 2));
            });
        },
    };
}

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

// Cap the unbounded pendingCommands array so the SSE command bus can't be
// flooded into OOM (sec-sentinel #9). At 1000 commands the bus is already
// pathological; older entries fall off the front.
const PR_PENDING_COMMANDS_CAP = 1000;

function primeRadiantControlPlugin(): Plugin {
    // Command queue: Claude pushes, React pops via SSE
    const pendingCommands: PrCommand[] = [];
    const results = new Map<string, PrResult>();
    let currentState: Record<string, unknown> = {};
    // Per-client state: keyed by clientId from POST /pr/state body
    const clientStates = new Map<string, Record<string, unknown>>();
    const sseClients: Set<import('http').ServerResponse> = new Set();
    let cmdCounter = 0;
    function pushBounded(cmd: PrCommand) {
        pendingCommands.push(cmd);
        while (pendingCommands.length > PR_PENDING_COMMANDS_CAP) {
            pendingCommands.shift();
        }
    }

    // ── Backend Observer — watches UI state and sends corrective commands ──
    let observerTimer: ReturnType<typeof setInterval> | null = null;
    const observerLog: string[] = [];

    function pushCommand(action: string, params: Record<string, unknown> = {}) {
        const cmd: PrCommand = {
            id: `obs-${++cmdCounter}-${Date.now()}`,
            action,
            params,
            timestamp: Date.now(),
        };
        pushBounded(cmd);
        const sseData = `data: ${JSON.stringify(cmd)}\n\n`;
        for (const client of sseClients) client.write(sseData);
    }

    function observeAndAdjust() {
        // Analyze all client states
        for (const [clientId, state] of clientStates) {
            const s = state as Record<string, unknown>;
            const render = s.render as Record<string, unknown> | undefined;
            const device = s.device as Record<string, unknown> | undefined;
            const errors = s.errors as string[] | undefined;
            if (!render) continue;

            const fps = render.fps as number ?? 60;
            const quality = render.qualityLevel as string ?? 'high';
            const formFactor = device?.formFactor as string ?? 'unknown';

            // Rule 1: FPS critically low — broadcast warning
            if (fps < 15 && fps > 0) {
                const msg = `[Observer] ${formFactor} at ${fps} FPS — performance critical`;
                if (!observerLog.includes(msg)) {
                    observerLog.push(msg);
                    if (observerLog.length > 50) observerLog.shift();
                    pushCommand('broadcast:message', { text: msg, durationMs: 5000 });
                }
            }

            // Rule 2: Errors detected — log them
            if (errors && errors.length > 0) {
                for (const err of errors) {
                    const logEntry = `[${formFactor}] ${err}`;
                    if (!observerLog.includes(logEntry)) {
                        observerLog.push(logEntry);
                        if (observerLog.length > 50) observerLog.shift();
                    }
                }
            }
        }
    }

    return {
        name: 'prime-radiant-control',
        configureServer(server) {
            // Start observer loop
            observerTimer = setInterval(observeAndAdjust, 5000);

            // ── GET /pr/observer — read observer log ──
            server.middlewares.use('/pr/observer', (req, res, next) => {
                if (req.method !== 'GET') { next(); return; }
                if (!gateLocal(req, res, 'prime-radiant')) return;
                res.writeHead(200, { 'Content-Type': 'application/json' });
                res.end(JSON.stringify({ log: observerLog, clientCount: clientStates.size }));
            });
            // ── GET /proxy/acp/agents — Mock ACP agent discovery ──
            // Serves agent cards directly instead of proxying to non-existent port 8200
            server.middlewares.use('/proxy/acp/agents', (req, res, next) => {
                if (req.method !== 'GET') { next(); return; }
                const agents = [
                    { name: 'demerzel', version: '1.1.0', status: 'online', skills: ['validate-governance-artifacts', 'execute-reconnaissance', 'evaluate-agent-compliance', 'invoke-zeroth-law'] },
                    { name: 'seldon', version: '2.0.0', status: 'online', skills: ['create-departments', 'teach-governance-knowledge', 'package-learnings', 'assess-comprehension'] },
                    { name: 'ix', version: '0.1.0', status: 'degraded', skills: ['build-pipeline', 'train-model', 'vector-search'] },
                    { name: 'tars', version: '0.1.0', status: 'degraded', skills: ['reason', 'manage-beliefs'] },
                    { name: 'ga', version: '0.1.0', status: 'online', skills: ['chatbot', 'music-theory', 'fretboard'] },
                ];
                res.writeHead(200, { 'Content-Type': 'application/json', 'Access-Control-Allow-Origin': '*' });
                res.end(JSON.stringify(agents));
            });

            // ── POST /pr/command — Claude sends a command ──
            server.middlewares.use('/pr/command', (req, res, next) => {
                if (req.method !== 'POST') { next(); return; }
                if (!gateLocal(req, res, 'prime-radiant')) return;
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
                        pushBounded(cmd);
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
                if (!gateLocal(req, res, 'prime-radiant')) return;
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
                            pushBounded(cmd);
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

            // ── GET /pr/state — Claude reads state ──
            // ?client=<id> returns specific client, otherwise returns all clients
            server.middlewares.use('/pr/state', (req, res, next) => {
                if (req.method !== 'GET') { next(); return; }
                if (!gateLocal(req, res, 'prime-radiant')) return;
                const url = new URL(req.url ?? '/', `http://${req.headers.host}`);
                const clientId = url.searchParams.get('client');
                res.writeHead(200, { 'Content-Type': 'application/json' });
                if (clientId && clientStates.has(clientId)) {
                    res.end(JSON.stringify(clientStates.get(clientId)));
                } else if (clientStates.size > 0) {
                    // Return all clients + legacy single-state for backward compat
                    const allClients = Object.fromEntries(clientStates);
                    res.end(JSON.stringify({ ...currentState, clients: allClients, clientCount: clientStates.size }));
                } else {
                    res.end(JSON.stringify(currentState));
                }
            });

            // ── POST /pr/state — React updates state (per-client) ──
            server.middlewares.use('/pr/state', (req, res, next) => {
                if (req.method !== 'POST') { next(); return; }
                if (!gateLocal(req, res, 'prime-radiant')) return;
                let body = '';
                req.on('data', (chunk: Buffer) => { body += chunk.toString(); });
                req.on('end', () => {
                    try {
                        const state = JSON.parse(body);
                        currentState = state; // backward compat
                        // Store per-client if device info is present
                        const dev = state.device;
                        if (dev) {
                            const clientId = `${dev.formFactor ?? 'unknown'}-${dev.width}x${dev.height}`;
                            clientStates.set(clientId, { ...state, clientId, lastSeen: Date.now() });
                            // Auto-cleanup stale clients (not seen in 30s)
                            const now = Date.now();
                            for (const [id, s] of clientStates) {
                                if (now - ((s as Record<string, unknown>).lastSeen as number) > 30000) {
                                    clientStates.delete(id);
                                }
                            }
                        }
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
                if (!gateLocal(req, res, 'prime-radiant')) return;
                res.writeHead(200, {
                    'Content-Type': 'text/event-stream',
                    'Cache-Control': 'no-cache',
                    'Connection': 'keep-alive',
                    // SSE event stream is gated to local origin already; no need
                    // to advertise it cross-origin.
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
                if (!gateLocal(req, res, 'prime-radiant')) return;
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
                if (!gateLocal(req, res, 'prime-radiant')) return;
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
    // 3d-force-graph WebGPU bug fixed via patch-package (see patches/3d-force-graph+1.79.1.patch)
    plugins: [react(), dts(), godotStaticPlugin(), primeRadiantControlPlugin(), devDataPlugin()],
    server: {
        port: 5176,
        host: true,
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
            // GraphQL endpoint — proxied so musicHierarchyApi.ts and any
            // other GraphQL consumer can use the relative `/graphql` URL
            // and avoid the stale hardcoded `https://localhost:7001` default
            // that broke the Music Hierarchy Navigator (2026-05-16).
            '/graphql': {
                target: 'http://localhost:5232',
                changeOrigin: true,
                secure: false,
            },
            // Ollama proxy — avoids CORS when checking local Ollama.
            // SECURITY: the dev server binds to all interfaces (host:true) so
            // tablet/phone can reach Prime Radiant on the LAN. Without an
            // Origin check, any webpage visited on any device on the LAN
            // could POST to http://<dev-host>:5176/proxy/ollama/api/chat and
            // drive local inference / cause VRAM DoS. We reject requests
            // whose Origin/Referer isn't one of our own addresses.
            '/proxy/ollama': {
                target: 'http://localhost:11434',
                changeOrigin: true,
                rewrite: (p: string) => p.replace(/^\/proxy\/ollama/, ''),
                configure: (proxy) => {
                    proxy.on('proxyReq', (proxyReq, req, res) => {
                        const origin = req.headers.origin || '';
                        const referer = req.headers.referer || '';
                        const host = req.headers.host || '';
                        // Allow: same-host origin/referer, or empty (curl/server-side).
                        // Reject: explicit origin that doesn't match our host.
                        const hostOnly = host.split(':')[0];
                        const ok = !origin || origin.includes(hostOnly) || referer.includes(hostOnly);
                        if (!ok) {
                            res.writeHead(403, { 'Content-Type': 'application/json' });
                            res.end(JSON.stringify({ error: 'origin not allowed', origin }));
                            proxyReq.destroy();
                        }
                    });
                },
            },
            // Docker Model Runner proxy — avoids CORS. Same origin guard.
            '/proxy/docker-models': {
                target: 'http://localhost:12434',
                changeOrigin: true,
                rewrite: (p: string) => p.replace(/^\/proxy\/docker-models/, ''),
                configure: (proxy) => {
                    proxy.on('proxyReq', (proxyReq, req, res) => {
                        const origin = req.headers.origin || '';
                        const referer = req.headers.referer || '';
                        const host = req.headers.host || '';
                        const hostOnly = host.split(':')[0];
                        const ok = !origin || origin.includes(hostOnly) || referer.includes(hostOnly);
                        if (!ok) {
                            res.writeHead(403, { 'Content-Type': 'application/json' });
                            res.end(JSON.stringify({ error: 'origin not allowed', origin }));
                            proxyReq.destroy();
                        }
                    });
                },
            },
            // ACP /proxy/acp/agents served by primeRadiantControlPlugin middleware
            // (no proxy to port 8200 needed — mock agents served directly)
            // Voxtral TTS proxy — injects MISTRAL_API_KEY server-side
            '/proxy/voxtral': {
                target: 'https://api.mistral.ai',
                changeOrigin: true,
                rewrite: (p: string) => p.replace(/^\/proxy\/voxtral/, ''),
                configure: (proxy) => {
                    proxy.on('proxyReq', (proxyReq) => {
                        const key = process.env.MISTRAL_API_KEY;
                        if (key) proxyReq.setHeader('Authorization', `Bearer ${key}`);
                    });
                },
            },
            // Codestral proxy — injects CODESTRAL_API_KEY server-side
            '/proxy/codestral': {
                target: 'https://codestral.mistral.ai',
                changeOrigin: true,
                rewrite: (p: string) => p.replace(/^\/proxy\/codestral/, ''),
                configure: (proxy) => {
                    proxy.on('proxyReq', (proxyReq) => {
                        const key = process.env.CODESTRAL_API_KEY;
                        if (key) proxyReq.setHeader('Authorization', `Bearer ${key}`);
                    });
                },
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
        // Pre-bundle heavy deps at dev-server startup so first-request
        // doesn't hit a multi-second esbuild transform — that pause
        // exceeds Cloudflare Tunnel's per-request timeout for our
        // demos.guitaralchemist.com origin and causes 504 cascades on
        // Prime Radiant routes (3d-force-graph, three postprocessing,
        // signalr, ag-grid all timed out + dynamic-import-fail).
        // Adding them here makes Vite pre-bundle on `vite` boot, so
        // the tunnel sees fast cache hits instead of cold transforms.
        include: [
            'prop-types',
            '3d-force-graph',
            'three',
            'three/examples/jsm/postprocessing/UnrealBloomPass.js',
            'three/examples/jsm/postprocessing/ShaderPass.js',
            'three/examples/jsm/loaders/STLLoader.js',
            '@microsoft/signalr',
            'ag-grid-community',
            'ag-grid-react',
            'react-dom',
            // @mui/x-tree-view ships many small ESM hook modules. On the
            // tunneled origin (demos.guitaralchemist.com → Cloudflare →
            // local Vite), each cold on-demand transform exceeds the
            // per-request timeout and the browser sees net::ERR_FAILED
            // on hooks like useApplyPropagationToSelectedItemsOnMount,
            // useTreeViewApiRef, useRichTreeViewApiRef. Pre-bundling at
            // boot avoids the cascade. NavigationPanel in
            // EcosystemRoadmap is the current consumer.
            '@mui/x-tree-view',
        ],
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
