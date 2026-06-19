import {defineConfig, loadEnv} from 'vite'
import react from '@vitejs/plugin-react'
import dts from 'vite-plugin-dts'
import * as path from 'path'
import { createReadStream, existsSync, statSync, readFileSync, readdirSync, appendFileSync } from 'fs'
import { execFileSync, spawn } from 'child_process'
import type { Plugin } from 'vite'
import { parseBacklog, extractDocTitle, binActivityByDay, projectLoopsGoals, parseValueCatalog } from './src/dev-data/parsers'
import type { BacklogPayload, LoopsGoalsProjection } from './src/dev-data/parsers'

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
// SECURITY MODEL — public by design:
// All data sources here are already public on github.com/GuitarAlchemist/ga:
// BACKLOG.md, docs/architecture/, state/quality/, git log, .mcp.json,
// CLAUDE.md / AGENTS.md / GEMINI.md, etc. Serving them here is a UX
// convenience for the dashboard, not a privileged surface. **Do not** add
// secrets, private state, or user data behind any /dev-data/* endpoint —
// they are reachable from the public Cloudflare tunnel.
//
// One redaction is kept defensively: gatherMcpServers() returns
// names + transport types only, never the full .mcp.json body. That
// protects against the foot-gun of a contributor inlining a resolved
// ${ENV_VAR} secret into .mcp.json for local debugging and then
// committing it.
//
// /pr/* (Prime Radiant control bus) IS gated — it's a live SSE command
// bus that accepts arbitrary actions. Public access there is a real
// disruption vector, not a data leak.
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

    // ── AI agent activity ────────────────────────────────────────────────
    // Surface which agents are actively contributing — from state/handoffs/
    // YAML frontmatter (durable cross-agent notes) plus Co-Authored-By
    // signature counts from recent git log. Token-quota visibility is
    // intentionally NOT here: it requires per-provider auth (Anthropic,
    // OpenAI, Google) and would leak credentials. Surfaced as an
    // Operational TODO instead.

    interface AgentActivity {
        agent: string;
        display_name: string;
        last_seen_at: string | null;
        handoff_count: number;
        coauthored_commits_30d: number;
        recent_handoffs: { at: string; branch: string | null; head: string | null; path: string }[];
    }

    interface RecentHandoffEntry {
        from: string;
        at: string;
        branch: string | null;
        head: string | null;
        path: string;
    }

    interface AgentActivityPayload {
        generated_at: string;
        agents: AgentActivity[];
        recent_handoffs: RecentHandoffEntry[];
    }

    function canonicalAgent(raw: string): { id: string; display: string } {
        const norm = raw.toLowerCase().trim().replace(/^["']|["']$/g, '');
        if (norm.includes('antigravity')) return { id: 'antigravity', display: 'Antigravity' };
        if (norm.includes('codex') || norm.includes('chatgpt')) return { id: 'codex', display: 'Codex (OpenAI)' };
        if (norm.includes('claude')) return { id: 'claude', display: 'Claude Code' };
        if (norm.includes('gemini')) return { id: 'gemini', display: 'Gemini' };
        if (norm.includes('junie')) return { id: 'junie', display: 'Junie (JetBrains)' };
        if (norm.includes('demerzel')) return { id: 'demerzel', display: 'Demerzel' };
        if (norm.includes('human') || norm.includes('operator')) return { id: 'human', display: 'Human operator' };
        // Generic 'agent' or unknown
        return { id: norm || 'unknown', display: norm ? norm[0].toUpperCase() + norm.slice(1) : 'Unknown' };
    }

    function parseHandoffFrontmatter(text: string): { from?: string; at?: string; branch?: string; head?: string } {
        // Frontmatter: --- ... --- at the top. Tolerate both
        //   from: codex                  (unquoted)
        //   from: "codex"                (quoted)
        // and ISO-8601 timestamps under `at:` or `generated_at:`.
        const m = text.match(/^---\s*\r?\n([\s\S]*?)\r?\n---/);
        if (!m) return {};
        const out: Record<string, string> = {};
        for (const line of m[1].split(/\r?\n/)) {
            const kv = line.match(/^(\w+):\s*(.+?)\s*$/);
            if (!kv) continue;
            out[kv[1]] = kv[2].replace(/^["']|["']$/g, '');
        }
        return {
            from: out.from,
            at: out.at || out.generated_at,
            branch: out.branch,
            head: out.head,
        };
    }

    function gatherAgentActivity(): AgentActivityPayload {
        const generated_at = new Date().toISOString();
        const stats = new Map<string, AgentActivity>();
        const recent: RecentHandoffEntry[] = [];

        // 1. Scan state/handoffs/*.md
        const handoffDir = path.join(repoRoot, 'state', 'handoffs');
        if (existsSync(handoffDir)) {
            for (const file of readdirSync(handoffDir)) {
                if (!file.endsWith('.md') || file === 'README.md') continue;
                const full = path.join(handoffDir, file);
                try {
                    const fm = parseHandoffFrontmatter(readFileSync(full, 'utf-8'));
                    if (!fm.from || !fm.at) continue;
                    const { id, display } = canonicalAgent(fm.from);
                    const entry: RecentHandoffEntry = {
                        from: id,
                        at: fm.at,
                        branch: fm.branch ?? null,
                        head: fm.head ?? null,
                        path: `state/handoffs/${file}`,
                    };
                    recent.push(entry);
                    let row = stats.get(id);
                    if (!row) {
                        row = { agent: id, display_name: display, last_seen_at: null, handoff_count: 0, coauthored_commits_30d: 0, recent_handoffs: [] };
                        stats.set(id, row);
                    }
                    row.handoff_count += 1;
                    if (!row.last_seen_at || fm.at > row.last_seen_at) row.last_seen_at = fm.at;
                    row.recent_handoffs.push({ at: fm.at, branch: entry.branch, head: entry.head, path: entry.path });
                } catch { /* skip malformed */ }
            }
        }

        // 2. Count Co-Authored-By signatures in last 30d of git log.
        // %b is the full commit message body — includes trailers.
        try {
            const body = execFileSync(
                'git',
                ['log', '--since=30 days ago', '--pretty=format:%H%n%b%n---END-COMMIT---'],
                { cwd: repoRoot, encoding: 'utf-8', timeout: 5000 },
            );
            const seenCommitsByAgent = new Map<string, Set<string>>();
            let currentSha: string | null = null;
            for (const line of body.split(/\r?\n/)) {
                if (line === '---END-COMMIT---') { currentSha = null; continue; }
                if (currentSha === null && /^[0-9a-f]{40}$/.test(line)) { currentSha = line; continue; }
                const ca = line.match(/^Co-Authored-By:\s*(.+?)\s*<.*>$/i);
                if (ca && currentSha) {
                    const { id } = canonicalAgent(ca[1]);
                    if (!seenCommitsByAgent.has(id)) seenCommitsByAgent.set(id, new Set());
                    seenCommitsByAgent.get(id)!.add(currentSha);
                }
            }
            for (const [id, shas] of seenCommitsByAgent) {
                let row = stats.get(id);
                if (!row) {
                    const { display } = canonicalAgent(id);
                    row = { agent: id, display_name: display, last_seen_at: null, handoff_count: 0, coauthored_commits_30d: 0, recent_handoffs: [] };
                    stats.set(id, row);
                }
                row.coauthored_commits_30d = shas.size;
            }
        } catch { /* git unavailable — leave counts at 0 */ }

        // Sort handoffs newest-first; trim per-agent recent list to 3
        recent.sort((a, b) => b.at.localeCompare(a.at));
        for (const row of stats.values()) {
            row.recent_handoffs.sort((a, b) => b.at.localeCompare(a.at));
            row.recent_handoffs = row.recent_handoffs.slice(0, 3);
        }

        // Sort agents by last_seen desc, then commits desc
        const agents = Array.from(stats.values()).sort((a, b) => {
            const aSeen = a.last_seen_at ?? '';
            const bSeen = b.last_seen_at ?? '';
            if (aSeen !== bSeen) return bSeen.localeCompare(aSeen);
            return b.coauthored_commits_30d - a.coauthored_commits_30d;
        });

        return { generated_at, agents, recent_handoffs: recent.slice(0, 10) };
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

    // Harness engineering adoption progress, sourced from state/harness/items.json.
    // Manually authored — edit that file when an item ships or a PR opens.
    // Schema documented inline in items.json. Returns null if the file is missing
    // so the dashboard renders an empty-state hint instead of throwing.
    function gatherHarness(): unknown | null {
        const p = path.join(repoRoot, 'state/harness/items.json');
        if (!existsSync(p)) return null;
        try {
            return JSON.parse(readFileSync(p, 'utf-8'));
        } catch {
            return null;
        }
    }

    // ── Test plans — proposals from the /test-plan skill ──────────────────
    // Aggregates state/quality/test-plans/*.md + *.meta.json into a single
    // structured payload for the TestPlansCard on /test#dev/qa. The .md is
    // the human-readable plan; the .meta.json sidecar (test-plan-v1 schema)
    // carries the structured fields the card surfaces as chips.
    //
    // Parses lightweight YAML-ish frontmatter from the .md to surface a
    // title / target / status when no sidecar exists yet — keeps the card
    // usable for hand-authored seed plans as well as workflow-generated ones.
    //
    // Also fetches state/quality/chatbot-qa/last.json (or the latest dated
    // snapshot) so the card footer can answer "is the chatbot loop healthy?".

    interface TestPlanSummary {
        id: string;
        path: string;
        title: string;
        target: string;
        generated_at: string | null;
        step_count: number;
        status: 'draft' | 'reviewed' | 'executed' | 'unknown';
        markdown: string;
    }

    interface ChatbotQaSummary {
        pass_pct: number | null;
        fail_count: number | null;
        total_prompts: number | null;
        last_run_at: string | null;
        degraded?: boolean;
        degraded_reason?: string | null;
        last_known_good_pass_pct?: number | null;
    }

    interface TestPlansPayload {
        generated_at: string;
        total: number;
        plans: TestPlanSummary[];
        chatbot_qa: ChatbotQaSummary | null;
    }

    function parsePlanFrontmatter(text: string): Record<string, string> {
        // Tolerate both `---` YAML blocks and the markdown title (#) for
        // plans that pre-date the convention. Returns lowercase keys.
        const out: Record<string, string> = {};
        const fm = text.match(/^---\s*\r?\n([\s\S]*?)\r?\n---/);
        if (fm) {
            for (const line of fm[1].split(/\r?\n/)) {
                const kv = line.match(/^(\w+):\s*(.+?)\s*$/);
                if (!kv) continue;
                out[kv[1].toLowerCase()] = kv[2].replace(/^["']|["']$/g, '');
            }
        }
        if (!out.title) {
            const h1 = text.split(/\r?\n/).find((l) => /^#\s+\S/.test(l));
            if (h1) out.title = h1.replace(/^#\s+/, '').trim();
        }
        return out;
    }

    function countPlanSteps(text: string): number {
        // Count proposal items across the four sections (Unit / Integration
        // / E2E / Chatbot). Each item starts with `- [ ]` or `- [x]` on its
        // own line (the skill template). Fall back to plain `- ` bullets
        // for hand-authored plans.
        const checkboxes = (text.match(/^\s*-\s+\[[ xX]\]\s+/gm) ?? []).length;
        if (checkboxes > 0) return checkboxes;
        return (text.match(/^\s*-\s+\S/gm) ?? []).length;
    }

    function deriveStatus(meta: Record<string, unknown> | null, fm: Record<string, string>): TestPlanSummary['status'] {
        const raw = (fm.status ?? (meta?.status as string | undefined) ?? '').toLowerCase().trim();
        if (raw === 'draft' || raw === 'reviewed' || raw === 'executed') return raw;
        return 'unknown';
    }

    function gatherTestPlans(): TestPlanSummary[] {
        const plansDir = path.join(repoRoot, 'state', 'quality', 'test-plans');
        if (!existsSync(plansDir)) return [];
        const out: TestPlanSummary[] = [];
        for (const file of readdirSync(plansDir)) {
            if (!file.endsWith('.md')) continue;
            // Skip README / SCHEMA-style docs at the root of the dir.
            if (file === 'README.md') continue;
            const full = path.join(plansDir, file);
            try {
                const markdown = readFileSync(full, 'utf-8');
                const fm = parsePlanFrontmatter(markdown);
                const id = file.replace(/\.md$/, '');
                // Look for a sidecar — accept both <id>.meta.json (skill convention)
                // and <id>.json (looser, hand-authored).
                let meta: Record<string, unknown> | null = null;
                for (const ext of ['.meta.json', '.json']) {
                    const sidecar = path.join(plansDir, `${id}${ext}`);
                    if (existsSync(sidecar)) {
                        try { meta = JSON.parse(readFileSync(sidecar, 'utf-8')) as Record<string, unknown>; }
                        catch { /* leave meta null */ }
                        break;
                    }
                }
                const stat = statSync(full);
                const generated_at = (meta?.generated_at as string | undefined)
                    ?? fm.generated_at
                    ?? stat.mtime.toISOString();
                const title = fm.title
                    ?? (meta?.title as string | undefined)
                    ?? id.replace(/[-_]/g, ' ');
                const target = fm.target
                    ?? (meta?.target as string | undefined)
                    ?? ((meta?.layers_touched as string[] | undefined)?.join(', '))
                    ?? 'unspecified';
                out.push({
                    id,
                    path: `state/quality/test-plans/${file}`,
                    title,
                    target,
                    generated_at,
                    step_count: countPlanSteps(markdown),
                    status: deriveStatus(meta, fm),
                    markdown,
                });
            } catch { /* skip malformed plan */ }
        }
        // Newest first.
        out.sort((a, b) => (b.generated_at ?? '').localeCompare(a.generated_at ?? ''));
        return out;
    }

    function gatherChatbotQa(): ChatbotQaSummary | null {
        const cbDir = path.join(repoRoot, 'state', 'quality', 'chatbot-qa');
        if (!existsSync(cbDir)) return null;
        // Prefer last.json; fall back to the most recent dated snapshot.
        let raw: string | null = null;
        const lastJson = path.join(cbDir, 'last.json');
        if (existsSync(lastJson)) {
            try { raw = readFileSync(lastJson, 'utf-8'); } catch { /* ignore */ }
        }
        if (!raw) {
            const dated = readdirSync(cbDir)
                .filter((f) => /^\d{4}-\d{2}-\d{2}\.json$/.test(f))
                .sort();
            const latest = dated[dated.length - 1];
            if (latest) {
                try { raw = readFileSync(path.join(cbDir, latest), 'utf-8'); } catch { /* ignore */ }
            }
        }
        if (!raw) return null;
        try {
            const parsed = JSON.parse(raw) as Record<string, unknown>;
            const total = typeof parsed.total_prompts === 'number' ? parsed.total_prompts : null;
            // pass_pct in the snapshots is a fraction (0..1) when present.
            const passPctRaw = parsed.pass_pct;
            const pass_pct = typeof passPctRaw === 'number' ? passPctRaw : null;
            const fail_count = (pass_pct != null && total != null) ? Math.round(total * (1 - pass_pct)) : null;
            return {
                pass_pct,
                fail_count,
                total_prompts: total,
                last_run_at: (parsed.timestamp as string | undefined) ?? null,
                degraded: parsed.degraded === true ? true : undefined,
                degraded_reason: typeof parsed.degraded_reason === 'string' ? parsed.degraded_reason : null,
                last_known_good_pass_pct: typeof parsed.last_known_good_pass_pct === 'number' ? parsed.last_known_good_pass_pct : null,
            };
        } catch {
            return null;
        }
    }

    // ── AI annotations (phase 2 of the ai-annotations campaign) ───────
    //
    // Reads two ix-produced files:
    //   state/quality/ai-annotations.jsonl         (extractor output)
    //   state/quality/ai-annotations-reconciliation.json (reconciler output)
    //
    // The reconciliation report supersedes the raw JSONL when present
    // (richer schema, includes bucket counts). When neither exists we
    // return an "empty" payload — the dashboard renders an onboarding
    // hint instead of an error. See docs/contracts/2026-05-24-ai-annotation.contract.md
    // (in the ix repo) for the field shapes.
    function gatherAiAnnotations(): Record<string, unknown> {
        const reconPath = path.join(repoRoot, 'state/quality/ai-annotations-reconciliation.json');
        const jsonlPath = path.join(repoRoot, 'state/quality/ai-annotations.jsonl');
        const now = new Date().toISOString();

        if (existsSync(reconPath)) {
            try {
                const recon = JSON.parse(readFileSync(reconPath, 'utf-8')) as Record<string, unknown>;
                return {
                    generated_at: recon.generated_at ?? now,
                    total: recon.total_annotations ?? 0,
                    by_truth_value: recon.by_truth_value ?? {},
                    by_certainty: recon.by_certainty ?? {},
                    by_kind: recon.by_kind ?? {},
                    verified_by_test: recon.verified_by_test ?? 0,
                    stale: recon.stale ?? 0,
                    contradictory: recon.contradictory ?? 0,
                    annotations: recon.annotations ?? [],
                };
            } catch {
                // fall through to JSONL
            }
        }

        if (existsSync(jsonlPath)) {
            try {
                const lines = readFileSync(jsonlPath, 'utf-8')
                    .split('\n')
                    .map((l) => l.trim())
                    .filter((l) => l.length > 0);
                const annotations: Record<string, unknown>[] = [];
                const byTv: Record<string, number> = {};
                const byCert: Record<string, number> = {};
                const byKind: Record<string, number> = {};
                let stale = 0;
                for (const line of lines) {
                    try {
                        const a = JSON.parse(line) as Record<string, unknown>;
                        annotations.push(a);
                        const tv = (a.truth_value as string) ?? 'U';
                        byTv[tv] = (byTv[tv] ?? 0) + 1;
                        const cert = (a.certainty as string) ?? 'uncertain';
                        byCert[cert] = (byCert[cert] ?? 0) + 1;
                        const k = (a.kind as string) ?? 'hint';
                        byKind[k] = (byKind[k] ?? 0) + 1;
                        if (a.stale) stale += 1;
                    } catch {
                        // skip malformed line
                    }
                }
                return {
                    generated_at: now,
                    total: annotations.length,
                    by_truth_value: byTv,
                    by_certainty: byCert,
                    by_kind: byKind,
                    verified_by_test: 0,
                    stale,
                    contradictory: byTv['C'] ?? 0,
                    annotations,
                };
            } catch {
                // fall through
            }
        }

        return {
            generated_at: now,
            total: 0,
            by_truth_value: {},
            by_certainty: {},
            by_kind: {},
            verified_by_test: 0,
            stale: 0,
            contradictory: 0,
            annotations: [],
            empty: true,
        };
    }

    // /loop and /goal runtime tracker — reads the append-only JSONL written by
    // .claude/hooks/loops-goals-tracker.ps1 (repo-local mirror lives at
    // state/.runtime-loops-goals.jsonl; the user-level canonical copy at
    // ~/.claude/projects/<encoded-repo>/state/runtime-loops-goals.jsonl is
    // intentionally NOT served — Vite has no way to know the encoded path
    // without path-traversal risk). Empty projection on missing file so the
    // dashboard renders an "invoke /loop or /goal to populate" hint.
    function gatherLoopsGoals(): LoopsGoalsProjection {
        const p = path.join(repoRoot, 'state', '.runtime-loops-goals.jsonl');
        const now = new Date();
        if (!existsSync(p)) {
            return {
                fetched_at: now.toISOString(),
                active_loops: [],
                active_goals: [],
                completed_recent: [],
                total_records: 0,
            };
        }
        try {
            const raw = readFileSync(p, 'utf-8');
            return projectLoopsGoals(raw.split('\n'), now);
        } catch {
            return {
                fetched_at: now.toISOString(),
                active_loops: [],
                active_goals: [],
                completed_recent: [],
                total_records: 0,
            };
        }
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

    // ── Algedonic channel — VSM alarm path ────────────────────────────────
    // Reads state/algedonic/inbox.jsonl (append-only JSONL per the contract
    // at docs/contracts/2026-05-24-algedonic-channel.contract.md), projects
    // the latest-line-per-id, drops superseded records, and groups by severity.
    //
    // Writes (acks) are append-only too — never rewrite existing lines.
    // Local-only via gateLocal because writes mutate state.

    interface AlgedonicAck {
        acked: boolean;
        acked_by: string | null;
        acked_at: string | null;
        resolution: string | null;
    }
    interface AlgedonicSignal {
        id: string;
        schema: string;
        emitted_at: string;
        repo: string;
        source: string;
        severity: 'info' | 'warn' | 'fail' | 'critical';
        summary: string;
        details: string;
        evidence_url: string | null;
        affected_artifacts: string[];
        ttl_hours: number;
        escalation: { on_unack_after_hours: number | null; route_to: string };
        ack: AlgedonicAck;
        supersedes: string[];
    }
    interface AlgedonicProjection {
        generated_at: string;
        total: number;
        by_severity: { info: number; warn: number; fail: number; critical: number };
        unacked: AlgedonicSignal[];
        top3: AlgedonicSignal[];
        has_critical: boolean;
    }
    const SEVERITY_RANK: Record<string, number> = { critical: 4, fail: 3, warn: 2, info: 1 };

    function projectAlgedonic(): AlgedonicProjection {
        const inboxPath = path.join(repoRoot, 'state', 'algedonic', 'inbox.jsonl');
        const empty: AlgedonicProjection = {
            generated_at: new Date().toISOString(),
            total: 0,
            by_severity: { info: 0, warn: 0, fail: 0, critical: 0 },
            unacked: [],
            top3: [],
            has_critical: false,
        };
        if (!existsSync(inboxPath)) return empty;

        // Group lines by id; latest-line-per-id wins.
        const byId = new Map<string, AlgedonicSignal>();
        const superseded = new Set<string>();
        try {
            const raw = readFileSync(inboxPath, 'utf-8');
            for (const line of raw.split('\n')) {
                const trim = line.trim();
                if (!trim) continue;
                try {
                    const sig = JSON.parse(trim) as AlgedonicSignal;
                    if (!sig.id || !sig.severity) continue;
                    const existing = byId.get(sig.id);
                    if (!existing || sig.emitted_at >= existing.emitted_at) {
                        byId.set(sig.id, sig);
                    }
                    if (Array.isArray(sig.supersedes)) {
                        for (const s of sig.supersedes) superseded.add(s);
                    }
                } catch { /* skip malformed line */ }
            }
        } catch { return empty; }

        // Build the unacked list — drop acked, drop superseded.
        const unacked = Array.from(byId.values())
            .filter((s) => !superseded.has(s.id))
            .filter((s) => !s.ack?.acked)
            .sort((a, b) => {
                const sevDelta = (SEVERITY_RANK[b.severity] ?? 0) - (SEVERITY_RANK[a.severity] ?? 0);
                if (sevDelta !== 0) return sevDelta;
                return b.emitted_at.localeCompare(a.emitted_at);
            });

        const by_severity = { info: 0, warn: 0, fail: 0, critical: 0 };
        for (const s of unacked) by_severity[s.severity] = (by_severity[s.severity] ?? 0) + 1;

        return {
            generated_at: new Date().toISOString(),
            total: byId.size,
            by_severity,
            unacked,
            top3: unacked.slice(0, 3),
            has_critical: by_severity.critical > 0,
        };
    }

    // Append an ack line for an existing signal. Returns the synthesized ack
    // line on success, or null if the id is unknown.
    function appendAck(id: string, ackedBy: string, resolution: string | null): AlgedonicSignal | null {
        const inboxPath = path.join(repoRoot, 'state', 'algedonic', 'inbox.jsonl');
        if (!existsSync(inboxPath)) return null;

        // Find the latest line for this id so we can preserve required fields.
        let source: AlgedonicSignal | null = null;
        const raw = readFileSync(inboxPath, 'utf-8');
        for (const line of raw.split('\n')) {
            const trim = line.trim();
            if (!trim) continue;
            try {
                const sig = JSON.parse(trim) as AlgedonicSignal;
                if (sig.id === id && (!source || sig.emitted_at >= source.emitted_at)) {
                    source = sig;
                }
            } catch { /* skip */ }
        }
        if (!source) return null;

        const now = new Date().toISOString().replace(/\.\d+Z$/, 'Z');
        const ackLine: AlgedonicSignal = {
            id: source.id,
            schema: source.schema,
            emitted_at: now,
            repo: source.repo,
            source: source.source,
            severity: source.severity,
            summary: source.summary,
            details: source.details,
            evidence_url: source.evidence_url,
            affected_artifacts: source.affected_artifacts ?? [],
            ttl_hours: source.ttl_hours,
            escalation: source.escalation,
            ack: { acked: true, acked_by: ackedBy, acked_at: now, resolution: resolution },
            supersedes: [],
        };
        appendFileSync(inboxPath, JSON.stringify(ackLine) + '\n', 'utf-8');
        return ackLine;
    }

    // ── /dev-data/in-flight — "what's being worked on right now" ─────────
    // Aggregates open PRs, recent merges, active loops/goals, and recent
    // algedonic activity into one snapshot. Drives the "In Flight" tile
    // on the Summary tab. Read-only; safe to expose without gateLocal.
    //
    // Data sources:
    //   - Open PRs:        gh pr list --state open --json ...
    //   - Recent merges:   gh pr list --state merged --limit 20 --json ...
    //   - Author-is-agent: parse commit messages for Co-Authored-By:
    //                      (Claude|Codex|Mercury|Gemini|...)
    //   - ETA:             state/quality/pr-eta-baseline.json (computed
    //                      weekly by Scripts/compute-pr-eta-baseline.ps1).
    //                      Falls back to 30-minute default when missing.
    //   - Loops/goals:     state/.runtime-loops-goals.jsonl (PR #330,
    //                      may be absent until that lands — return []).
    //   - Algedonic:       reuse projectAlgedonic() above, filter to
    //                      last 24h, return severity counts + top
    //                      unacked summary.
    //
    // Cached per-process for 10s to keep gh shell-outs predictable when
    // the operator pins the dashboard tab. Cache is intentionally short:
    // an open PR's check status moves in real-time and operators are
    // watching for the merge-able moment.

    interface InFlightPrCheck { name: string; status: string; conclusion: string | null; workflow: string }
    interface InFlightPr {
        number: number;
        title: string;
        url: string;
        head_branch: string;
        author: string;
        author_is_agent: boolean;
        author_agent_name: string | null;
        opened_at: string;
        updated_at: string;
        age_minutes: number;
        draft: boolean;
        labels: string[];
        checks: {
            total: number;
            passed: number;
            failed: number;
            pending: number;
            details: InFlightPrCheck[];
        };
        mergeable: string;
        eta_minutes: number | null;
        eta_basis: string;
        pr_type: string;
    }
    interface InFlightRecentMerge { number: number; title: string; url: string; merged_at: string; ago_minutes: number; author: string }
    interface InFlightLoopOrGoal { id: string; label: string; status: string; updated_at: string }
    interface InFlightAlgedonicSummary {
        info: number;
        warn: number;
        fail: number;
        critical: number;
        top_unacked: { id: string; severity: string; summary: string; emitted_at: string; repo: string }[];
    }
    interface InFlightPayload {
        fetched_at: string;
        open_prs: InFlightPr[];
        recent_merges: InFlightRecentMerge[];
        active_loops: InFlightLoopOrGoal[];
        active_goals: InFlightLoopOrGoal[];
        algedonic_recent: InFlightAlgedonicSummary;
        eta_baseline: { source: string; window_days: number | null; computed_at: string | null };
        warnings: string[];
    }

    // PR-type classifier — mirrors Scripts/compute-pr-eta-baseline.ps1.
    // Keep these two in sync if either side changes the bucket names.
    function prTypeFromTitle(title: string): string {
        if (!title) return 'other';
        const m = title.match(/^(?<type>[a-z]+)(\([^)]*\))?\s*:/i);
        if (!m || !m.groups) return 'other';
        const t = m.groups.type.toLowerCase();
        if (t === 'chore') return 'chore';
        if (t === 'feat' || t === 'feature') return 'feat';
        if (t === 'fix' || t === 'bugfix') return 'fix';
        if (t === 'docs' || t === 'doc') return 'docs';
        return 'other';
    }

    // Known agent names that show up as Co-Authored-By identities. Matched
    // case-insensitively against the trailer line; first hit wins.
    const AGENT_PATTERNS: { pattern: RegExp; name: string }[] = [
        { pattern: /claude/i, name: 'Claude' },
        { pattern: /codex|chatgpt|openai/i, name: 'Codex' },
        { pattern: /mercury|inception/i, name: 'Mercury' },
        { pattern: /gemini|antigravity/i, name: 'Gemini' },
        { pattern: /junie/i, name: 'Junie' },
        { pattern: /copilot/i, name: 'Copilot' },
    ];

    // Per-process cache. Keyed by PR number; entries live for one /dev-data/in-flight cycle
    // (the cache is wiped by the 10s payload cache below, so this is mostly belt-and-suspenders
    // for when multiple PRs share the same commits sampling cost).
    const prAgentCache = new Map<number, { isAgent: boolean; agentName: string | null }>();

    function detectAgentForPr(prNumber: number): { isAgent: boolean; agentName: string | null } {
        const cached = prAgentCache.get(prNumber);
        if (cached) return cached;
        try {
            const out = execFileSync(
                'gh',
                ['pr', 'view', String(prNumber), '--json', 'commits'],
                { cwd: repoRoot, encoding: 'utf-8', timeout: 8000 },
            );
            const parsed = JSON.parse(out) as { commits?: { messageBody?: string; authors?: { name?: string }[] }[] };
            const commits = parsed.commits ?? [];
            for (const c of commits) {
                const body = c.messageBody ?? '';
                for (const line of body.split(/\r?\n/)) {
                    const ca = line.match(/^Co-Authored-By:\s*(.+?)\s*<.*>$/i);
                    if (!ca) continue;
                    for (const a of AGENT_PATTERNS) {
                        if (a.pattern.test(ca[1])) {
                            const v = { isAgent: true, agentName: a.name };
                            prAgentCache.set(prNumber, v);
                            return v;
                        }
                    }
                }
                // Also check the authors[] array — co-authors are returned as separate entries
                for (const a of (c.authors ?? [])) {
                    const n = a.name ?? '';
                    for (const ap of AGENT_PATTERNS) {
                        if (ap.pattern.test(n)) {
                            const v = { isAgent: true, agentName: ap.name };
                            prAgentCache.set(prNumber, v);
                            return v;
                        }
                    }
                }
            }
        } catch { /* gh unreachable, return false */ }
        const v = { isAgent: false, agentName: null };
        prAgentCache.set(prNumber, v);
        return v;
    }

    interface EtaBaseline {
        '_schema'?: string;
        computed_at?: string;
        window_days?: number;
        by_type?: Record<string, { sample_n: number; median_minutes: number; p90_minutes: number }>;
    }
    let etaBaselineCache: { baseline: EtaBaseline | null; loaded_at: number } | null = null;
    const ETA_BASELINE_TTL_MS = 5 * 60 * 1000; // 5 minutes; file changes weekly
    function loadEtaBaseline(): { baseline: EtaBaseline | null; source: string } {
        const now = Date.now();
        if (etaBaselineCache && (now - etaBaselineCache.loaded_at) < ETA_BASELINE_TTL_MS) {
            return { baseline: etaBaselineCache.baseline, source: 'cache' };
        }
        const p = path.join(repoRoot, 'state', 'quality', 'pr-eta-baseline.json');
        if (!existsSync(p)) {
            etaBaselineCache = { baseline: null, loaded_at: now };
            return { baseline: null, source: 'missing' };
        }
        try {
            const data = JSON.parse(readFileSync(p, 'utf-8')) as EtaBaseline;
            etaBaselineCache = { baseline: data, loaded_at: now };
            return { baseline: data, source: 'file' };
        } catch {
            etaBaselineCache = { baseline: null, loaded_at: now };
            return { baseline: null, source: 'parse_error' };
        }
    }

    function computeEta(prType: string, ageMinutes: number): { etaMinutes: number | null; basis: string } {
        const { baseline, source } = loadEtaBaseline();
        if (!baseline || !baseline.by_type) {
            const remaining = Math.max(30 - ageMinutes, 0);
            return { etaMinutes: remaining, basis: `fallback 30-min default (baseline source: ${source})` };
        }
        const bucket = baseline.by_type[prType] ?? baseline.by_type['other'];
        if (!bucket || bucket.median_minutes <= 0) {
            // Sample too small or all merges were ~instant — fall back to default
            const remaining = Math.max(30 - ageMinutes, 0);
            return { etaMinutes: remaining, basis: `fallback 30-min default (${prType} median was 0)` };
        }
        const remaining = Math.max(bucket.median_minutes - ageMinutes, 0);
        const window = baseline.window_days ?? 30;
        return {
            etaMinutes: remaining,
            basis: `median time-to-merge for ${prType} PRs in last ${window}d = ${bucket.median_minutes} min (n=${bucket.sample_n})`,
        };
    }

    interface GhPrAuthor { login?: string; name?: string }
    interface GhPrCheckRollup {
        __typename?: string;
        name?: string;
        status?: string;
        conclusion?: string;
        workflowName?: string;
        state?: string;
    }
    interface GhOpenPr {
        number: number;
        title: string;
        headRefName: string;
        author: GhPrAuthor;
        createdAt: string;
        updatedAt: string;
        isDraft: boolean;
        labels: { name: string }[];
        statusCheckRollup: GhPrCheckRollup[];
        mergeStateStatus: string;
    }

    function fetchOpenPrs(): { prs: InFlightPr[]; warning: string | null } {
        try {
            const out = execFileSync(
                'gh',
                ['pr', 'list', '--state', 'open', '--limit', '30',
                    '--json', 'number,title,headRefName,author,createdAt,updatedAt,isDraft,labels,statusCheckRollup,mergeStateStatus'],
                { cwd: repoRoot, encoding: 'utf-8', timeout: 10000 },
            );
            const raw = JSON.parse(out) as GhOpenPr[];
            const now = Date.now();
            const prs: InFlightPr[] = raw.map((p) => {
                const ageMin = Math.max(0, Math.floor((now - new Date(p.createdAt).getTime()) / 60000));
                const prType = prTypeFromTitle(p.title);
                const { etaMinutes, basis } = computeEta(prType, ageMin);
                const checks = (p.statusCheckRollup ?? []).map((c) => ({
                    name: c.name ?? '(unnamed)',
                    status: (c.status ?? c.state ?? 'UNKNOWN').toUpperCase(),
                    conclusion: c.conclusion ? String(c.conclusion).toUpperCase() : null,
                    workflow: c.workflowName ?? '',
                }));
                // SKIPPED counts as neither passed nor failed nor pending — it's a no-op
                const passed = checks.filter((c) => c.conclusion === 'SUCCESS').length;
                const failed = checks.filter((c) => c.conclusion === 'FAILURE' || c.conclusion === 'CANCELLED' || c.conclusion === 'TIMED_OUT').length;
                const pending = checks.filter((c) => c.status === 'IN_PROGRESS' || c.status === 'QUEUED' || c.status === 'PENDING').length;
                const author = p.author?.login ?? p.author?.name ?? 'unknown';
                const { isAgent, agentName } = detectAgentForPr(p.number);
                return {
                    number: p.number,
                    title: p.title,
                    url: `https://github.com/GuitarAlchemist/ga/pull/${p.number}`,
                    head_branch: p.headRefName,
                    author,
                    author_is_agent: isAgent,
                    author_agent_name: agentName,
                    opened_at: p.createdAt,
                    updated_at: p.updatedAt,
                    age_minutes: ageMin,
                    draft: !!p.isDraft,
                    labels: (p.labels ?? []).map((l) => l.name),
                    checks: { total: checks.length, passed, failed, pending, details: checks },
                    mergeable: p.mergeStateStatus ?? 'UNKNOWN',
                    eta_minutes: etaMinutes,
                    eta_basis: basis,
                    pr_type: prType,
                };
            });
            // Sort by age desc (oldest first), then by PR number for stability
            prs.sort((a, b) => b.age_minutes - a.age_minutes || a.number - b.number);
            return { prs, warning: null };
        } catch (e) {
            return { prs: [], warning: `gh pr list (open) failed: ${String((e as Error).message ?? e)}` };
        }
    }

    interface GhMergedPr { number: number; title: string; mergedAt: string; author: GhPrAuthor }
    function fetchRecentMerges(): { merges: InFlightRecentMerge[]; warning: string | null } {
        try {
            const out = execFileSync(
                'gh',
                ['pr', 'list', '--state', 'merged', '--limit', '20', '--json', 'number,title,mergedAt,author'],
                { cwd: repoRoot, encoding: 'utf-8', timeout: 10000 },
            );
            const raw = JSON.parse(out) as GhMergedPr[];
            const now = Date.now();
            const merges: InFlightRecentMerge[] = raw
                .filter((p) => p.mergedAt)
                .map((p) => ({
                    number: p.number,
                    title: p.title,
                    url: `https://github.com/GuitarAlchemist/ga/pull/${p.number}`,
                    merged_at: p.mergedAt,
                    ago_minutes: Math.max(0, Math.floor((now - new Date(p.mergedAt).getTime()) / 60000)),
                    author: p.author?.login ?? p.author?.name ?? 'unknown',
                }))
                .sort((a, b) => a.ago_minutes - b.ago_minutes);
            return { merges, warning: null };
        } catch (e) {
            return { merges: [], warning: `gh pr list (merged) failed: ${String((e as Error).message ?? e)}` };
        }
    }

    // Loops/goals tracker — PR #330 will populate state/.runtime-loops-goals.jsonl.
    // Until then, return empty arrays so the tile gracefully hides those sub-sections.
    interface LoopGoalLine { kind?: 'loop' | 'goal'; id?: string; label?: string; status?: string; updated_at?: string }
    function fetchActiveLoopsGoals(): { loops: InFlightLoopOrGoal[]; goals: InFlightLoopOrGoal[] } {
        const p = path.join(repoRoot, 'state', '.runtime-loops-goals.jsonl');
        if (!existsSync(p)) return { loops: [], goals: [] };
        const latestById = new Map<string, LoopGoalLine>();
        try {
            const raw = readFileSync(p, 'utf-8');
            for (const line of raw.split('\n')) {
                const t = line.trim();
                if (!t) continue;
                try {
                    const parsed = JSON.parse(t) as LoopGoalLine;
                    if (!parsed.id) continue;
                    const existing = latestById.get(parsed.id);
                    if (!existing || (parsed.updated_at ?? '') >= (existing.updated_at ?? '')) {
                        latestById.set(parsed.id, parsed);
                    }
                } catch { /* skip malformed */ }
            }
        } catch { return { loops: [], goals: [] }; }
        const loops: InFlightLoopOrGoal[] = [];
        const goals: InFlightLoopOrGoal[] = [];
        for (const entry of latestById.values()) {
            // Treat "done"/"completed"/"cancelled" as no-longer-active
            const status = (entry.status ?? 'unknown').toLowerCase();
            if (status === 'done' || status === 'completed' || status === 'cancelled') continue;
            const row: InFlightLoopOrGoal = {
                id: entry.id ?? '',
                label: entry.label ?? entry.id ?? '(unlabeled)',
                status: entry.status ?? 'unknown',
                updated_at: entry.updated_at ?? '',
            };
            if (entry.kind === 'goal') goals.push(row);
            else loops.push(row);
        }
        // Newest first
        loops.sort((a, b) => b.updated_at.localeCompare(a.updated_at));
        goals.sort((a, b) => b.updated_at.localeCompare(a.updated_at));
        return { loops, goals };
    }

    function summarizeAlgedonicRecent(): InFlightAlgedonicSummary {
        const projection = projectAlgedonic();
        const cutoff = Date.now() - 24 * 60 * 60 * 1000;
        const recent = projection.unacked.filter((s) => new Date(s.emitted_at).getTime() >= cutoff);
        const out: InFlightAlgedonicSummary = {
            info: 0, warn: 0, fail: 0, critical: 0,
            top_unacked: [],
        };
        for (const s of recent) out[s.severity] = (out[s.severity] ?? 0) + 1;
        out.top_unacked = recent.slice(0, 3).map((s) => ({
            id: s.id,
            severity: s.severity,
            summary: s.summary,
            emitted_at: s.emitted_at,
            repo: s.repo,
        }));
        return out;
    }

    // Cache the assembled payload for 10s so dashboard auto-refresh (every
    // 20s from the UI) only hits gh once per cycle.
    let inFlightCache: { payload: InFlightPayload; built_at: number } | null = null;
    const IN_FLIGHT_TTL_MS = 10_000;

    // ── /dev-data/recent-events — last-6h fleet activity ────────────────────
    // Feeds the MissionControl "JUST HAPPENED" quadrant. Pure aggregator:
    // reads (a) recent merges from the same gh source as buildInFlight,
    // (b) algedonic signals from inbox.jsonl within the window, (c) recent
    // commits via `git log --since`. Heuristic — not a contract — so we
    // can tweak the shape without coordinating across repos.
    interface RecentEvent {
        kind: 'merge' | 'algedonic' | 'commit' | 'ack';
        at: string;
        ago_minutes: number;
        summary: string;
        url?: string;
        severity?: string;
        author?: string;
        pr_number?: number;
    }
    interface RecentEventsPayload {
        fetched_at: string;
        window_hours: number;
        events: RecentEvent[];
    }

    let recentEventsCache: { payload: RecentEventsPayload; built_at: number } | null = null;
    const RECENT_EVENTS_TTL_MS = 10_000;

    function buildRecentEvents(windowHours = 6): RecentEventsPayload {
        const now = Date.now();
        if (recentEventsCache && (now - recentEventsCache.built_at) < RECENT_EVENTS_TTL_MS) {
            return recentEventsCache.payload;
        }
        const cutoff = now - windowHours * 60 * 60 * 1000;
        const events: RecentEvent[] = [];

        // (a) Recent merges — reuse the same gh call path as fetchRecentMerges.
        // Failure mode: just drop the bucket so the quadrant degrades to
        // showing commits + signals only.
        const { merges } = fetchRecentMerges();
        for (const m of merges) {
            const t = Date.parse(m.merged_at);
            if (Number.isNaN(t) || t < cutoff) continue;
            events.push({
                kind: 'merge',
                at: m.merged_at,
                ago_minutes: Math.max(0, Math.floor((now - t) / 60000)),
                summary: `#${m.number} ${m.title}`,
                url: m.url,
                author: m.author,
                pr_number: m.number,
            });
        }

        // (b) Algedonic signals — emitted within window. Both acks and signals.
        const projection = projectAlgedonic();
        // projection.unacked is the live signal list; we also want acks
        // emitted in the window, so re-read inbox.jsonl to grab ack lines.
        for (const s of projection.unacked) {
            const t = Date.parse(s.emitted_at);
            if (Number.isNaN(t) || t < cutoff) continue;
            events.push({
                kind: 'algedonic',
                at: s.emitted_at,
                ago_minutes: Math.max(0, Math.floor((now - t) / 60000)),
                summary: `[${s.repo}] ${s.summary}`,
                severity: s.severity,
                url: s.evidence_url ?? undefined,
            });
        }
        // Ack events from the raw inbox.jsonl
        const inboxPath = path.join(repoRoot, 'state', 'algedonic', 'inbox.jsonl');
        if (existsSync(inboxPath)) {
            try {
                const raw = readFileSync(inboxPath, 'utf-8');
                for (const line of raw.split('\n')) {
                    const t = line.trim();
                    if (!t) continue;
                    try {
                        const rec = JSON.parse(t) as { ack?: { acked?: boolean; acked_at?: string; acked_by?: string }; summary?: string; repo?: string };
                        if (!rec.ack?.acked || !rec.ack.acked_at) continue;
                        const ms = Date.parse(rec.ack.acked_at);
                        if (Number.isNaN(ms) || ms < cutoff) continue;
                        events.push({
                            kind: 'ack',
                            at: rec.ack.acked_at,
                            ago_minutes: Math.max(0, Math.floor((now - ms) / 60000)),
                            summary: `[${rec.repo ?? '?'}] ${rec.summary ?? ''}`.slice(0, 120),
                            author: rec.ack.acked_by ?? undefined,
                        });
                    } catch { /* skip malformed */ }
                }
            } catch { /* unreadable inbox → just skip the ack bucket */ }
        }

        // (c) Recent commits — `git log --since=Nh`. Limited to 10 for the
        // quadrant's needs; older entries are uninteresting and would
        // crowd merges out of the top-5.
        try {
            const out = execFileSync(
                'git',
                ['log', `--since=${windowHours} hours ago`, '-n', '10', '--pretty=format:%H%x09%an%x09%aI%x09%s'],
                { cwd: repoRoot, encoding: 'utf-8', timeout: 5000 },
            );
            for (const line of out.split('\n')) {
                if (!line) continue;
                const [sha, author, date, ...rest] = line.split('\t');
                if (!sha || !date) continue;
                const t = Date.parse(date);
                if (Number.isNaN(t)) continue;
                events.push({
                    kind: 'commit',
                    at: date,
                    ago_minutes: Math.max(0, Math.floor((now - t) / 60000)),
                    summary: `${sha.slice(0, 8)} ${rest.join('\t')}`,
                    author,
                });
            }
        } catch { /* git failed → fine, drop commits bucket */ }

        // Newest first
        events.sort((a, b) => a.ago_minutes - b.ago_minutes);

        const payload: RecentEventsPayload = {
            fetched_at: new Date().toISOString(),
            window_hours: windowHours,
            events: events.slice(0, 20),
        };
        recentEventsCache = { payload, built_at: now };
        return payload;
    }

    function buildInFlight(): InFlightPayload {
        const now = Date.now();
        if (inFlightCache && (now - inFlightCache.built_at) < IN_FLIGHT_TTL_MS) {
            return inFlightCache.payload;
        }
        const warnings: string[] = [];
        prAgentCache.clear(); // refresh agent detection each cycle (small cost)
        const { prs: openPrs, warning: w1 } = fetchOpenPrs();
        const { merges: recentMerges, warning: w2 } = fetchRecentMerges();
        if (w1) warnings.push(w1);
        if (w2) warnings.push(w2);
        const { loops, goals } = fetchActiveLoopsGoals();
        const { baseline, source } = loadEtaBaseline();
        const payload: InFlightPayload = {
            fetched_at: new Date().toISOString(),
            open_prs: openPrs,
            recent_merges: recentMerges.slice(0, 10),
            active_loops: loops,
            active_goals: goals,
            algedonic_recent: summarizeAlgedonicRecent(),
            eta_baseline: {
                source,
                window_days: baseline?.window_days ?? null,
                computed_at: baseline?.computed_at ?? null,
            },
            warnings,
        };
        inFlightCache = { payload, built_at: now };
        return payload;
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
                res.writeHead(200, { 'Content-Type': 'application/json', 'Cache-Control': 'no-store' });
                res.end(JSON.stringify({ generated_at: new Date().toISOString(), ...gatherQuality() }));
            });

            // ── /dev-data/quality-gates — unified quality-gate ledger ──────
            // Reads state/quality/gate-ledger.jsonl line-by-line. v1 entries
            // (schema_version==1) parse into the dashboard payload; legacy v0
            // rows (no schema_version) are surfaced under `legacy_rows` so
            // the chatbot PR ledger doesn't disappear during the transition.
            //
            // Query params (all optional):
            //   source=ix-quality-trend|sentrux|chatbot-qa|ga-retrieval|...
            //   domain=structural|chatbot|tests|...
            //   decision=pass|fail|warn|skip
            //   since=2026-05-20T00:00:00Z   (RFC3339)
            //   limit=50                     (default 100, max 500)
            //
            // Schema mirror: ix/docs/contracts/2026-05-24-quality-gate-ledger.contract.md
            server.middlewares.use('/dev-data/quality-gates', (req, res, next) => {
                if (req.method !== 'GET') { next(); return; }
                if (!gateLocal(req, res, 'quality-gates')) { return; }
                try {
                    const url = new URL(req.url ?? '/', `http://${req.headers.host}`);
                    const sourceFilter = url.searchParams.get('source') ?? undefined;
                    const domainFilter = url.searchParams.get('domain') ?? undefined;
                    const decisionFilter = url.searchParams.get('decision') ?? undefined;
                    const sinceFilter = url.searchParams.get('since') ?? undefined;
                    const limit = Math.max(1, Math.min(500, Number(url.searchParams.get('limit') ?? 100)));

                    const ledgerPath = path.join(repoRoot, 'state/quality/gate-ledger.jsonl');
                    if (!existsSync(ledgerPath)) {
                        res.writeHead(200, { 'Content-Type': 'application/json', 'Cache-Control': 'no-store' });
                        res.end(JSON.stringify({
                            generated_at: new Date().toISOString(),
                            ledger_path: 'state/quality/gate-ledger.jsonl',
                            v1_entries: [],
                            legacy_rows: [],
                            counts_by_source: {},
                            counts_by_decision: {},
                            note: 'ledger not yet present (no producer has emitted a row)',
                        }));
                        return;
                    }

                    interface V1Entry {
                        schema_version: 1;
                        schema: string;
                        id: string;
                        run_at: string;
                        source: string;
                        domain: string;
                        decision: 'pass' | 'fail' | 'warn' | 'skip';
                        metric: { name: string; value: number; threshold?: number; trend?: string };
                        evidence?: { kind: string; ref: string };
                        supersedes?: string[];
                        operator_ack?: unknown;
                        extra?: Record<string, unknown>;
                    }

                    const raw = readFileSync(ledgerPath, 'utf-8');
                    const v1: V1Entry[] = [];
                    const legacy: unknown[] = [];
                    for (const line of raw.split(/\r?\n/)) {
                        const t = line.trim();
                        if (!t) continue;
                        let obj: unknown;
                        try { obj = JSON.parse(t); } catch { continue; }
                        if (obj && typeof obj === 'object' && (obj as { schema_version?: unknown }).schema_version === 1) {
                            v1.push(obj as V1Entry);
                        } else {
                            legacy.push(obj);
                        }
                    }

                    // Filter v1
                    let filtered = v1;
                    if (sourceFilter)   filtered = filtered.filter(e => e.source === sourceFilter);
                    if (domainFilter)   filtered = filtered.filter(e => e.domain === domainFilter);
                    if (decisionFilter) filtered = filtered.filter(e => e.decision === decisionFilter);
                    if (sinceFilter) {
                        const since = Date.parse(sinceFilter);
                        if (!Number.isNaN(since)) {
                            filtered = filtered.filter(e => Date.parse(e.run_at) >= since);
                        }
                    }
                    // Newest-first, then limit. Sort by parsed epoch (not
                    // localeCompare on the raw string) so RFC3339 timestamps
                    // with differing offsets (Z vs +02:00) order chronologically
                    // rather than lexically — otherwise older rows can surface
                    // as "newest" and hide recent ones before the slice.
                    filtered.sort((a, b) => (Date.parse(b.run_at) || 0) - (Date.parse(a.run_at) || 0));
                    const top = filtered.slice(0, limit);

                    // Aggregate counts (over filtered, pre-limit so the
                    // tile numbers don't shift with pagination).
                    const countsBySource: Record<string, number> = {};
                    const countsByDecision: Record<string, number> = { pass: 0, fail: 0, warn: 0, skip: 0 };
                    for (const e of filtered) {
                        countsBySource[e.source] = (countsBySource[e.source] ?? 0) + 1;
                        countsByDecision[e.decision] = (countsByDecision[e.decision] ?? 0) + 1;
                    }

                    res.writeHead(200, { 'Content-Type': 'application/json', 'Cache-Control': 'no-store' });
                    res.end(JSON.stringify({
                        generated_at: new Date().toISOString(),
                        ledger_path: 'state/quality/gate-ledger.jsonl',
                        filters: {
                            source: sourceFilter ?? null,
                            domain: domainFilter ?? null,
                            decision: decisionFilter ?? null,
                            since: sinceFilter ?? null,
                            limit,
                        },
                        v1_total: v1.length,
                        v1_after_filter: filtered.length,
                        v1_entries: top,
                        legacy_rows_count: legacy.length,
                        counts_by_source: countsBySource,
                        counts_by_decision: countsByDecision,
                    }));
                } catch (err) {
                    res.writeHead(500, { 'Content-Type': 'application/json' });
                    res.end(JSON.stringify({ error: 'quality-gates middleware failed', detail: String(err) }));
                }
            });

            // Renders docs/quality/README.md (auto-generated by ix-quality-trend)
            // verbatim. The QA tab embeds it via react-markdown so the same
            // headline tables + sparklines + per-domain detail show inline,
            // not just on GitHub.
            server.middlewares.use('/dev-data/quality-readme', (req, res, next) => {
                if (req.method !== 'GET') { next(); return; }
                const p = path.join(repoRoot, 'docs/quality/README.md');
                if (!existsSync(p)) {
                    res.writeHead(404, { 'Content-Type': 'application/json' });
                    res.end(JSON.stringify({ error: 'docs/quality/README.md not found' }));
                    return;
                }
                const stat = statSync(p);
                res.writeHead(200, { 'Content-Type': 'application/json', 'Cache-Control': 'no-store' });
                res.end(JSON.stringify({
                    markdown: readFileSync(p, 'utf-8'),
                    source_path: 'docs/quality/README.md',
                    source_mtime: stat.mtime.toISOString(),
                    fetched_at: new Date().toISOString(),
                }));
            });

            server.middlewares.use('/dev-data/activity', (req, res, next) => {
                if (req.method !== 'GET') { next(); return; }
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
                res.writeHead(200, { 'Content-Type': 'application/json', 'Cache-Control': 'no-store' });
                res.end(JSON.stringify({ generated_at: new Date().toISOString(), docs: gatherArchitecture() }));
            });

            // ── /dev-data/value — business-value scorecard ──────────────────
            // Reads the federated RICE→stars catalog emitted by the sibling ix
            // repo's `ix-value` generator (JSON-on-disk contract). Returns the
            // parsed records plus a precomputed split (demo leaderboard sorted
            // by score, repo rollups) so the card stays dumb. Never 500s on a
            // corrupt line — parseValueCatalog drops bad lines.
            server.middlewares.use('/dev-data/value', (req, res, next) => {
                if (req.method !== 'GET') { next(); return; }
                const ixRoot = process.env.IX_ROOT || path.resolve(repoRoot, '../ix')
                const p = path.join(ixRoot, 'state/value/catalog.jsonl')
                if (!existsSync(p)) {
                    res.writeHead(404, { 'Content-Type': 'application/json' })
                    res.end(JSON.stringify({ error: `value catalog not found at ${p} — run: cargo run -p ix-value -- catalog` }))
                    return
                }
                try {
                    const records = parseValueCatalog(readFileSync(p, 'utf-8'))
                    const demos = records.filter((r) => r.kind === 'demo').sort((a, b) => b.score01 - a.score01)
                    const repos = records.filter((r) => r.kind === 'repo').sort((a, b) => b.score01 - a.score01)
                    res.writeHead(200, { 'Content-Type': 'application/json', 'Cache-Control': 'no-store' })
                    res.end(JSON.stringify({ generated_at: new Date().toISOString(), records, demos, repos }))
                } catch (err) {
                    res.writeHead(500, { 'Content-Type': 'application/json' })
                    res.end(JSON.stringify({ error: String(err) }))
                }
            });

            server.middlewares.use('/dev-data/agents', (req, res, next) => {
                if (req.method !== 'GET') { next(); return; }
                res.writeHead(200, { 'Content-Type': 'application/json', 'Cache-Control': 'no-store' });
                res.end(JSON.stringify({
                    generated_at: new Date().toISOString(),
                    agent_files: gatherAgentFiles(),
                    mcp_servers: gatherMcpServers(),
                }));
            });

            server.middlewares.use('/dev-data/agent-activity', (req, res, next) => {
                if (req.method !== 'GET') { next(); return; }
                res.writeHead(200, { 'Content-Type': 'application/json', 'Cache-Control': 'no-store' });
                res.end(JSON.stringify(gatherAgentActivity()));
            });

            // ── POST harness/skill/<name> — queue a skill invocation ──
            // The harness tab exposes "Invoke" buttons for skills like
            // /grade-last-pr, /council, /backlog-groom, /test-plan. Clicking
            // a button writes a line to state/harness/skill-invocations.jsonl
            // (gitignored — per-developer-machine). An agent session can read
            // the queue and dispatch; we don't run skills server-side here.
            //
            // Body: { source?: string, context?: string, item_number?: number }
            // Allowed skill names are restricted to /^[a-z0-9][a-z0-9-]{0,48}$/.
            //
            // Two route prefixes, identical behaviour:
            //   /actions/harness/skill/<name>     — canonical; gated by
            //                                       Cloudflare Access at the
            //                                       edge (path policy on
            //                                       /actions/*). Both local
            //                                       and tunnel traffic land
            //                                       here; CF Access proves
            //                                       operator identity before
            //                                       traffic reaches Vite.
            //   /dev-data/harness/skill/<name>    — deprecated mirror, still
            //                                       gated locally via
            //                                       gateLocal. Emits a
            //                                       Deprecation response
            //                                       header so callers can
            //                                       migrate. Remove after one
            //                                       release.
            const handleSkillInvoke = (
                req: import('http').IncomingMessage,
                res: import('http').ServerResponse,
                stripPrefix: string,
            ) => {
                const url = new URL(req.url ?? '/', `http://${req.headers.host}`);
                // Vite's `use(prefix, fn)` already strips the prefix from
                // req.url, so url.pathname here is just '/<name>'. The
                // stripPrefix arg is informational (used for error labels).
                const name = url.pathname.replace(/^\/+/, '');
                if (!/^[a-z0-9][a-z0-9-]{0,48}$/.test(name)) {
                    res.writeHead(400, { 'Content-Type': 'application/json' });
                    res.end(JSON.stringify({ error: 'bad skill name', route: stripPrefix }));
                    return;
                }

                let body = '';
                req.on('data', (chunk: Buffer) => { body += chunk.toString(); });
                req.on('end', () => {
                    let source: string | null = null;
                    let context: string | null = null;
                    let itemNumber: number | null = null;
                    if (body.trim()) {
                        try {
                            const parsed = JSON.parse(body) as { source?: string; context?: string; item_number?: number };
                            if (parsed.source) source = String(parsed.source).slice(0, 64);
                            if (parsed.context) context = String(parsed.context).slice(0, 280);
                            if (typeof parsed.item_number === 'number') itemNumber = parsed.item_number;
                        } catch { /* fall through with defaults */ }
                    }
                    const queuePath = path.join(repoRoot, 'state/harness/skill-invocations.jsonl');
                    const line = {
                        id: `inv-${Date.now()}-${Math.random().toString(36).slice(2, 8)}`,
                        skill: name,
                        queued_at: new Date().toISOString(),
                        source: source ?? 'harness-tab',
                        context,
                        item_number: itemNumber,
                    };
                    try {
                        appendFileSync(queuePath, JSON.stringify(line) + '\n', 'utf-8');
                    } catch (e) {
                        res.writeHead(500, { 'Content-Type': 'application/json' });
                        res.end(JSON.stringify({ error: 'queue write failed', detail: String(e) }));
                        return;
                    }
                    res.writeHead(200, { 'Content-Type': 'application/json' });
                    res.end(JSON.stringify({
                        ok: true,
                        queued: line,
                        message: `Invocation queued — agents typically pick this up from state/handoffs/ within 5 minutes. Or run locally: /${name}`,
                    }));
                });
            };

            // Canonical: /actions/harness/skill/<name> — auth happens at CF
            // Access (path policy). We do NOT call gateLocal here, so the
            // tunnel can carry signed-in operator traffic through.
            server.middlewares.use('/actions/harness/skill', (req, res, next) => {
                if (req.method !== 'POST') { next(); return; }
                handleSkillInvoke(req, res, '/actions/harness/skill');
            });

            // Deprecated mirror — still local-only. Emits Deprecation header
            // so old clients can migrate before removal.
            server.middlewares.use('/dev-data/harness/skill', (req, res, next) => {
                if (req.method !== 'POST') { next(); return; }
                if (!gateLocal(req, res, 'harness/skill')) return;
                res.setHeader('Deprecation', 'true');
                res.setHeader('Sunset', 'use /actions/harness/skill/<name> instead');
                res.setHeader('Link', '</actions/harness/skill>; rel="successor-version"');
                handleSkillInvoke(req, res, '/dev-data/harness/skill');
            });

            server.middlewares.use('/dev-data/harness', (req, res, next) => {
                if (req.method !== 'GET') { next(); return; }
                const payload = gatherHarness();
                if (payload == null) {
                    res.writeHead(404, { 'Content-Type': 'application/json' });
                    res.end(JSON.stringify({ error: 'state/harness/items.json not found' }));
                    return;
                }
                res.writeHead(200, { 'Content-Type': 'application/json', 'Cache-Control': 'no-store' });
                res.end(JSON.stringify({ generated_at: new Date().toISOString(), ...(payload as Record<string, unknown>) }));
            });

            // ── GET /dev-data/test-plans — list /test-plan proposals ──
            // Aggregates state/quality/test-plans/*.md + sidecars into a
            // payload the TestPlansCard on /test#dev/qa consumes. Returns
            // an empty list (not 404) when the directory is missing so the
            // card can render the empty state hint without an extra fetch.
            //
            // The /actions/test-plans alias below mirrors this handler so a
            // future CF Access-gated public surface can hit the same logic
            // without duplicating the gather code.
            const testPlansHandler = (
                req: import('http').IncomingMessage,
                res: import('http').ServerResponse,
                next: () => void,
            ) => {
                if (req.method !== 'GET') { next(); return; }
                const plans = gatherTestPlans();
                const payload: TestPlansPayload = {
                    generated_at: new Date().toISOString(),
                    total: plans.length,
                    plans,
                    chatbot_qa: gatherChatbotQa(),
                };
                res.writeHead(200, { 'Content-Type': 'application/json', 'Cache-Control': 'no-store' });
                res.end(JSON.stringify(payload));
            };
            server.middlewares.use('/dev-data/test-plans', testPlansHandler);
            server.middlewares.use('/actions/test-plans', testPlansHandler);

            // ── /dev-data/ai-annotations — merged extractor + reconciler ──
            // Always 200 (returns {empty:true} when no data exists, so the
            // dashboard renders an onboarding hint instead of throwing).
            server.middlewares.use('/dev-data/ai-annotations', (req, res, next) => {
                if (req.method !== 'GET') { next(); return; }
                res.writeHead(200, { 'Content-Type': 'application/json', 'Cache-Control': 'no-store' });
                res.end(JSON.stringify(gatherAiAnnotations()));
            });

            // ── /dev-data/runtime-loops-goals — read latest projection ──
            // Returns active /loop + /goal invocations across all Claude
            // sessions on this machine, with age + last-activity decoration.
            // Always 200 with the empty shape when no data exists (the
            // dashboard renders an empty-state hint instead of error).
            server.middlewares.use('/dev-data/runtime-loops-goals', (req, res, next) => {
                if (req.method !== 'GET') { next(); return; }
                const url = new URL(req.url ?? '/', `http://${req.headers.host}`);
                // POST routes (e.g. /stop/<id>) are handled by the sibling
                // middleware registered below; this GET handler ignores them.
                if (url.pathname.startsWith('/stop/')) { next(); return; }
                res.writeHead(200, { 'Content-Type': 'application/json', 'Cache-Control': 'no-store' });
                res.end(JSON.stringify(gatherLoopsGoals()));
            });

            // ── POST /dev-data/runtime-loops-goals/stop/<id> — mark completed ──
            // Appends a synthetic record with status=completed for the dashboard
            // Stop button. Local-only (writes mutate state). Append-only:
            // never rewrites existing lines. Matches the algedonic /ack pattern.
            server.middlewares.use('/dev-data/runtime-loops-goals/stop', (req, res, next) => {
                if (req.method !== 'POST') { next(); return; }
                if (!gateLocal(req, res, 'runtime-loops-goals')) return;

                const url = new URL(req.url ?? '/', `http://${req.headers.host}`);
                // Middleware strips '/dev-data/runtime-loops-goals/stop' so
                // pathname is e.g. '/<id>'.
                const id = url.pathname.replace(/^\/+/, '');
                if (!id || !/^[A-Za-z0-9_-]+$/.test(id)) {
                    res.writeHead(400, { 'Content-Type': 'application/json' });
                    res.end(JSON.stringify({ error: 'bad id' }));
                    return;
                }
                const inboxPath = path.join(repoRoot, 'state', '.runtime-loops-goals.jsonl');
                if (!existsSync(inboxPath)) {
                    res.writeHead(404, { 'Content-Type': 'application/json' });
                    res.end(JSON.stringify({ error: 'no tracker state on disk' }));
                    return;
                }
                // Find the latest record for this id so we can preserve schema
                // fields when synthesizing the completion event.
                let latest: Record<string, unknown> | null = null;
                try {
                    const raw = readFileSync(inboxPath, 'utf-8');
                    for (const line of raw.split('\n')) {
                        const trim = line.trim();
                        if (!trim) continue;
                        try {
                            const rec = JSON.parse(trim) as Record<string, unknown>;
                            if (rec.id === id) {
                                const recAct = String(rec.last_activity_at ?? rec.started_at ?? '');
                                const latestAct = String(latest?.last_activity_at ?? latest?.started_at ?? '');
                                if (!latest || recAct >= latestAct) latest = rec;
                            }
                        } catch { /* skip malformed */ }
                    }
                } catch {
                    res.writeHead(500, { 'Content-Type': 'application/json' });
                    res.end(JSON.stringify({ error: 'failed to read tracker state' }));
                    return;
                }
                if (!latest) {
                    res.writeHead(404, { 'Content-Type': 'application/json' });
                    res.end(JSON.stringify({ error: 'id not found in tracker state' }));
                    return;
                }
                const now = new Date().toISOString();
                const completion = {
                    id: String(latest.id),
                    kind: String(latest.kind),
                    started_at: String(latest.started_at ?? ''),
                    session_id: String(latest.session_id ?? 'unknown'),
                    prompt_or_condition: String(latest.prompt_or_condition ?? ''),
                    turn_count: Number(latest.turn_count ?? 0),
                    last_activity_at: now,
                    status: 'completed',
                    event: 'status_change',
                    branch: latest.branch ?? null,
                    completed_by: 'dashboard',
                };
                try {
                    appendFileSync(inboxPath, JSON.stringify(completion) + '\n', 'utf-8');
                } catch {
                    res.writeHead(500, { 'Content-Type': 'application/json' });
                    res.end(JSON.stringify({ error: 'append failed' }));
                    return;
                }
                res.writeHead(200, { 'Content-Type': 'application/json' });
                res.end(JSON.stringify({ ok: true, completion }));
            });

            // ── /dev-data/algedonic — read latest projection of inbox.jsonl ──
            // Returns the unacked signal list grouped by severity. Used by the
            // Heartbeat algedonic tile (OverviewSection.tsx). Refreshed every
            // request — the inbox is small and reading is cheap.
            server.middlewares.use('/dev-data/algedonic', (req, res, next) => {
                if (req.method !== 'GET') { next(); return; }
                const url = new URL(req.url ?? '/', `http://${req.headers.host}`);
                if (url.pathname.startsWith('/ack/')) { next(); return; } // POST handler below
                res.writeHead(200, { 'Content-Type': 'application/json', 'Cache-Control': 'no-store' });
                res.end(JSON.stringify(projectAlgedonic()));
            });

            // ── POST algedonic/ack/<id> — ack a signal ─────────────────────
            // Body: optional { acked_by, resolution }. Writes a new line to
            // inbox.jsonl with ack.acked = true.
            //
            // Two route prefixes, identical behaviour (same auth split as
            // harness/skill above):
            //   /actions/algedonic/ack/<id>      — canonical; gated by CF
            //                                       Access path policy.
            //   /dev-data/algedonic/ack/<id>     — deprecated mirror;
            //                                       local-only + Deprecation
            //                                       header.
            const handleAlgedonicAck = (
                req: import('http').IncomingMessage,
                res: import('http').ServerResponse,
                stripPrefix: string,
            ) => {
                const url = new URL(req.url ?? '/', `http://${req.headers.host}`);
                // Vite's `use(prefix, fn)` strips the prefix already.
                const id = url.pathname.replace(/^\/+/, '');
                if (!id || !/^[A-Za-z0-9_-]+$/.test(id)) {
                    res.writeHead(400, { 'Content-Type': 'application/json' });
                    res.end(JSON.stringify({ error: 'bad id', route: stripPrefix }));
                    return;
                }
                let body = '';
                req.on('data', (chunk: Buffer) => { body += chunk.toString(); });
                req.on('end', () => {
                    let ackedBy = 'dashboard-user';
                    let resolution: string | null = null;
                    if (body.trim()) {
                        try {
                            const parsed = JSON.parse(body) as { acked_by?: string; resolution?: string };
                            if (parsed.acked_by) ackedBy = String(parsed.acked_by).slice(0, 64);
                            if (parsed.resolution) resolution = String(parsed.resolution).slice(0, 280);
                        } catch { /* fall through with defaults */ }
                    }
                    const ackLine = appendAck(id, ackedBy, resolution);
                    if (!ackLine) {
                        res.writeHead(404, { 'Content-Type': 'application/json' });
                        res.end(JSON.stringify({ error: 'signal id not found in inbox' }));
                        return;
                    }
                    res.writeHead(200, { 'Content-Type': 'application/json' });
                    res.end(JSON.stringify({ ok: true, ack: ackLine }));
                });
            };

            server.middlewares.use('/actions/algedonic/ack', (req, res, next) => {
                if (req.method !== 'POST') { next(); return; }
                handleAlgedonicAck(req, res, '/actions/algedonic/ack');
            });

            server.middlewares.use('/dev-data/algedonic/ack', (req, res, next) => {
                if (req.method !== 'POST') { next(); return; }
                if (!gateLocal(req, res, 'algedonic')) return;
                res.setHeader('Deprecation', 'true');
                res.setHeader('Sunset', 'use /actions/algedonic/ack/<id> instead');
                res.setHeader('Link', '</actions/algedonic/ack>; rel="successor-version"');
                handleAlgedonicAck(req, res, '/dev-data/algedonic/ack');
            });

            // ── /cdn-cgi/access/get-identity — local dev stub ──────────────
            // Cloudflare Access serves this path in production with the
            // signed-in user's identity ({ email, name, ... }) or 401 if not
            // authenticated. In local dev there's no CF in front, so we stub
            // it with a fixed dev identity. This keeps the auth-aware UI
            // (useCfIdentity hook + AuthChip) working when running
            // `npm run dev` directly.
            //
            // Production traffic for this path is handled by CF before it
            // reaches Vite — Cloudflare intercepts /cdn-cgi/* and responds
            // itself. The middleware here only fires for local-origin
            // requests; if a tunnel request somehow reached it, gateLocal
            // would reject it so we still don't impersonate identities.
            server.middlewares.use('/cdn-cgi/access/get-identity', (req, res, next) => {
                if (req.method !== 'GET') { next(); return; }
                if (!isLocalOrigin(req)) {
                    // Tunnel / external — let CF handle this in prod; in
                    // dev-without-CF the request would have been blocked
                    // earlier. Return 401 to mirror CF's "not signed in".
                    res.writeHead(401, { 'Content-Type': 'application/json' });
                    res.end(JSON.stringify({ error: 'not authenticated' }));
                    return;
                }
                res.writeHead(200, { 'Content-Type': 'application/json', 'Cache-Control': 'no-store' });
                res.end(JSON.stringify({
                    email: 'dev@localhost',
                    name: 'Local Dev',
                    id: 'local-dev-stub',
                    type: 'local-dev-stub',
                }));
            });

            // ── /dev-data/in-flight ───────────────────────────────────────
            // Single endpoint feeding the Summary-tab "In Flight" tile.
            // Returns open PRs + checks + ETAs, recent merges, active
            // loops/goals, and last-24h algedonic counts. Read-only.
            server.middlewares.use('/dev-data/in-flight', (req, res, next) => {
                if (req.method !== 'GET') { next(); return; }
                res.writeHead(200, { 'Content-Type': 'application/json', 'Cache-Control': 'no-store' });
                res.end(JSON.stringify(buildInFlight(), null, 2));
            });

            // ── /dev-data/recent-events — last-6h fleet activity ──────────
            // Aggregates merges + algedonic signals + commits within a
            // sliding window. Feeds MissionControl's JUST HAPPENED quadrant.
            // The default 6h window is operator-friendly: "what changed
            // since I went to lunch?". Override via ?hours=N for debug.
            server.middlewares.use('/dev-data/recent-events', (req, res, next) => {
                if (req.method !== 'GET') { next(); return; }
                const url = new URL(req.url ?? '/', `http://${req.headers.host}`);
                const raw = url.searchParams.get('hours');
                let windowHours = 6;
                if (raw) {
                    const parsed = Number.parseInt(raw, 10);
                    if (Number.isFinite(parsed) && parsed > 0 && parsed <= 168) {
                        windowHours = parsed;
                    }
                }
                res.writeHead(200, { 'Content-Type': 'application/json', 'Cache-Control': 'no-store' });
                res.end(JSON.stringify(buildRecentEvents(windowHours), null, 2));
            });

            server.middlewares.use('/dev-data/manifest', (req, res, next) => {
                if (req.method !== 'GET') { next(); return; }
                const manifest = {
                    schema_version: '1.0.0',
                    generated_at: new Date().toISOString(),
                    repo: 'GuitarAlchemist/ga',
                    public_url: 'https://demos.guitaralchemist.com',
                    endpoints: {
                        backlog: '/dev-data/backlog',
                        quality: '/dev-data/quality',
                        quality_readme: '/dev-data/quality-readme',
                        activity: '/dev-data/activity',
                        architecture: '/dev-data/architecture',
                        agents: '/dev-data/agents',
                        agent_activity: '/dev-data/agent-activity',
                        harness: '/dev-data/harness',
                        algedonic: '/dev-data/algedonic',
                        test_plans: '/dev-data/test-plans',
                        in_flight: '/dev-data/in-flight',
                        recent_events: '/dev-data/recent-events',
                        runtime_loops_goals: '/dev-data/runtime-loops-goals',
                        sentrux_health: '/dev-data/sentrux/health',
                        sentrux_rules: '/dev-data/sentrux/rules',
                        sentrux_test_gaps: '/dev-data/sentrux/test-gaps',
                        sentrux_dsm: '/dev-data/sentrux/dsm',
                        sentrux_next_steps: '/dev-data/sentrux/next-steps',
                        ai_annotations: '/dev-data/ai-annotations',
                        manifest: '/dev-data/manifest',
                        // Action endpoints — CF-Access-gated in production
                        // (path policy on /actions/*). Read endpoints above
                        // stay public; only state-mutating POSTs require
                        // operator identity.
                        actions: {
                            invoke_skill: '/actions/harness/skill/<name>',
                            ack_algedonic: '/actions/algedonic/ack/<id>',
                            identity: '/cdn-cgi/access/get-identity',
                        },
                    },
                    services: serviceTopology,
                    backlog: gatherBacklog(),
                    quality: gatherQuality(),
                    activity: gatherActivity(10),
                    activity_by_day: gatherActivityByDay(30),
                    architecture: gatherArchitecture(),
                    agent_files: gatherAgentFiles(),
                    mcp_servers: gatherMcpServers(),
                    agent_activity: gatherAgentActivity(),
                    harness: gatherHarness(),
                    algedonic: projectAlgedonic(),
                    in_flight: buildInFlight(),
                    runtime_loops_goals: gatherLoopsGoals(),
                    ai_annotations: gatherAiAnnotations(),
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

// ---------------------------------------------------------------------------
// Sentrux MCP bridge — surfaces realtime structural-quality signals from
// the optional `sentrux.exe` peer (see .mcp.json). Each endpoint spawns
// `sentrux.exe mcp` as a one-shot stdio child, runs an
// initialize → scan → <tool> JSON-RPC chain, and unwraps the inner tool
// payload before returning it to the React dashboard.
//
// Sentrux's CLI subcommands (`check`, `gate`, `scan`) do NOT expose a `--json`
// output, but the `mcp` subcommand speaks standard MCP stdio. We use that
// rather than parsing the GUI/CLI output.
//
// Graceful degradation: if sentrux.exe is missing OR errors, the endpoints
// return `{ ok: false, error, hint }` (HTTP 200) and the cards render an
// empty state. The dashboard never crashes on a missing optional peer.
//
// Caching: scan is the slow part (~5-10s on the ga repo). We cache the
// tool result per-endpoint for a tool-specific window so the React cards
// can refresh on a 30s / 60s / 5min cadence without re-scanning every time.
// ---------------------------------------------------------------------------
function sentruxPlugin(): Plugin {
    const repoRoot = path.resolve(__dirname, '../..');
    const sentruxExe = process.env.SENTRUX_EXE ?? 'C:/Users/spare/bin/sentrux.exe';

    // Per-tool TTL cache. Health / rules / test_gaps refresh on the
    // frontend at 30s / 60s / 5min, so we keep cache slightly shorter to
    // ensure each frontend tick gets fresh data without thrashing sentrux.
    interface CacheEntry { value: unknown; expires_at: number }
    const cache = new Map<string, CacheEntry>();
    const cacheTtlMs: Record<string, number> = {
        health: 20_000,
        check_rules: 45_000,
        test_gaps: 4 * 60_000,
        dsm: 5 * 60_000,
    };

    // Track an MCP child for the lifetime of one tools/call. We spawn fresh
    // each time because the MCP server is small and a long-lived child
    // would need supervision (PID file, restart-on-crash) we don't want
    // in the Vite dev plugin.
    type McpResult = { ok: true; result: unknown } | { ok: false; error: string; hint?: string };

    function callSentruxTool(toolName: string, args: Record<string, unknown> = {}): Promise<McpResult> {
        return new Promise((resolve) => {
            if (!existsSync(sentruxExe)) {
                resolve({
                    ok: false,
                    error: `sentrux.exe not found at ${sentruxExe}`,
                    hint: 'Install sentrux (https://github.com/sentrux/sentrux) or set SENTRUX_EXE to its path.',
                });
                return;
            }

            let child: ReturnType<typeof spawn>;
            try {
                child = spawn(sentruxExe, ['mcp'], {
                    cwd: repoRoot,
                    stdio: ['pipe', 'pipe', 'pipe'],
                    windowsHide: true,
                });
            } catch (e) {
                resolve({ ok: false, error: `failed to spawn sentrux: ${(e as Error)?.message ?? e}` });
                return;
            }

            const stdoutChunks: string[] = [];
            let stderr = '';
            let done = false;
            const finish = (r: McpResult) => {
                if (done) return;
                done = true;
                try { child.kill(); } catch { /* ignore */ }
                resolve(r);
            };

            // Most tools complete within 12s; first scan on a cold repo can
            // take ~10s, so a 60s ceiling is safe headroom. dsm + test_gaps
            // can be slower on huge repos — bump if necessary.
            const timeoutMs = toolName === 'dsm' ? 90_000 : 60_000;
            const timer = setTimeout(() => {
                finish({ ok: false, error: `sentrux ${toolName} timed out after ${timeoutMs}ms` });
            }, timeoutMs);

            child.stdout?.on('data', (buf: Buffer) => {
                stdoutChunks.push(buf.toString());
                // Sentrux emits one JSON-RPC response per line. The last
                // response we care about has id = 99 (the tool call).
                const text = stdoutChunks.join('');
                for (const line of text.split('\n')) {
                    const trimmed = line.trim();
                    if (!trimmed.startsWith('{')) continue;
                    let parsed: unknown;
                    try { parsed = JSON.parse(trimmed); } catch { continue; }
                    const obj = parsed as { id?: number; result?: unknown; error?: { message?: string } };
                    if (obj.id !== 99) continue;
                    clearTimeout(timer);
                    if (obj.error) {
                        finish({ ok: false, error: obj.error.message ?? 'sentrux returned an error' });
                        return;
                    }
                    finish({ ok: true, result: obj.result });
                    return;
                }
            });

            child.stderr?.on('data', (buf: Buffer) => { stderr += buf.toString(); });

            child.on('error', (e) => {
                clearTimeout(timer);
                finish({ ok: false, error: `sentrux spawn error: ${e.message}` });
            });
            child.on('exit', (code) => {
                clearTimeout(timer);
                if (!done) {
                    finish({
                        ok: false,
                        error: `sentrux exited (code ${code ?? 'null'}) before responding`,
                        hint: stderr.slice(-500) || undefined,
                    });
                }
            });

            // Send the JSON-RPC handshake → scan → tool call pipeline. We always
            // run `scan` first because sentrux requires it before any other
            // tool (see tools/list description). For tools that need their
            // own arguments, merge them into the call.
            const lines: string[] = [
                JSON.stringify({ jsonrpc: '2.0', id: 1, method: 'initialize', params: {
                    protocolVersion: '2024-11-05',
                    capabilities: {},
                    clientInfo: { name: 'ga-dashboard-sentrux-tab', version: '1' },
                } }),
                JSON.stringify({ jsonrpc: '2.0', method: 'notifications/initialized' }),
            ];

            if (toolName !== 'scan') {
                // Run scan implicitly so the requested tool has data to chew on.
                // The scan response (id=2) will arrive but be ignored; we only
                // resolve on id=99 below.
                lines.push(JSON.stringify({
                    jsonrpc: '2.0', id: 2, method: 'tools/call',
                    params: { name: 'scan', arguments: { path: repoRoot } },
                }));
            }
            lines.push(JSON.stringify({
                jsonrpc: '2.0', id: 99, method: 'tools/call',
                params: { name: toolName, arguments: toolName === 'scan' ? { path: repoRoot, ...args } : args },
            }));

            try {
                child.stdin?.write(lines.join('\n') + '\n');
                child.stdin?.end();
            } catch (e) {
                clearTimeout(timer);
                finish({ ok: false, error: `sentrux stdin write failed: ${(e as Error)?.message}` });
            }
        });
    }

    // Unwrap the MCP tool envelope { content: [{ text: '...' }] } → parsed JSON.
    function unwrap(result: unknown): unknown {
        const r = result as { content?: Array<{ text?: string }> } | undefined;
        const text = r?.content?.[0]?.text;
        if (typeof text !== 'string') return null;
        try {
            return JSON.parse(text);
        } catch {
            // Some sentrux tools return human-readable text; pass it through
            // verbatim under a `text` key so the cards can render it.
            return { text };
        }
    }

    async function cached(tool: string, args: Record<string, unknown> = {}): Promise<{
        ok: boolean; data?: unknown; error?: string; hint?: string; duration_ms: number;
    }> {
        const key = `${tool}:${JSON.stringify(args)}`;
        const now = Date.now();
        const hit = cache.get(key);
        if (hit && hit.expires_at > now) {
            return { ok: true, data: hit.value, duration_ms: 0 };
        }
        const t0 = Date.now();
        const res = await callSentruxTool(tool, args);
        const duration = Date.now() - t0;
        if (!res.ok) {
            return { ok: false, error: res.error, hint: (res as { hint?: string }).hint, duration_ms: duration };
        }
        const data = unwrap(res.result);
        cache.set(key, { value: data, expires_at: now + (cacheTtlMs[tool] ?? 30_000) });
        return { ok: true, data, duration_ms: duration };
    }

    function writeJson(res: import('http').ServerResponse, payload: unknown): void {
        res.writeHead(200, { 'Content-Type': 'application/json', 'Cache-Control': 'no-store' });
        res.end(JSON.stringify({ generated_at: new Date().toISOString(), ...(payload as Record<string, unknown>) }));
    }

    return {
        name: 'sentrux-bridge',
        configureServer(server) {
            server.middlewares.use('/dev-data/sentrux/health', (req, res, next) => {
                if (req.method !== 'GET') { next(); return; }
                void cached('health').then((r) => writeJson(res, r));
            });

            server.middlewares.use('/dev-data/sentrux/rules', (req, res, next) => {
                if (req.method !== 'GET') { next(); return; }
                void cached('check_rules').then((r) => writeJson(res, r));
            });

            server.middlewares.use('/dev-data/sentrux/test-gaps', (req, res, next) => {
                if (req.method !== 'GET') { next(); return; }
                void cached('test_gaps', { limit: 20 }).then((r) => writeJson(res, r));
            });

            server.middlewares.use('/dev-data/sentrux/dsm', (req, res, next) => {
                if (req.method !== 'GET') { next(); return; }
                void cached('dsm', { format: 'stats' }).then((r) => writeJson(res, r));
            });

            // GET /dev-data/sentrux/next-steps — actionable refactor
            // prescriptions written by the /sentrux-next-steps skill.
            // Reads state/quality/sentrux-next-steps/latest.md and parses
            // its YAML frontmatter so the card can render headline
            // chips (quality_signal, cycles, coverage_pct) alongside the
            // markdown body. Always returns 200 — an absent file yields
            // { empty: true, hint: "..." } so the card renders an
            // onboarding state instead of throwing.
            server.middlewares.use('/dev-data/sentrux/next-steps', (req, res, next) => {
                if (req.method !== 'GET') { next(); return; }
                const sourcePath = 'state/quality/sentrux-next-steps/latest.md';
                const fullPath = path.join(repoRoot, sourcePath);
                res.writeHead(200, { 'Content-Type': 'application/json', 'Cache-Control': 'no-store' });
                if (!existsSync(fullPath)) {
                    res.end(JSON.stringify({
                        empty: true,
                        hint: "Run /sentrux-next-steps to generate recommendations.",
                        source_path: sourcePath,
                        fetched_at: new Date().toISOString(),
                    }));
                    return;
                }
                try {
                    const raw = readFileSync(fullPath, 'utf-8');
                    const stat = statSync(fullPath);
                    // Cheap YAML-frontmatter parser tuned for the
                    // sentrux-next-steps-v1 shape — depth-1 keys + a
                    // nested `inputs:` object. We don't pull in a full
                    // YAML lib; the schema is fixed.
                    const fmMatch = raw.match(/^---\r?\n([\s\S]*?)\r?\n---\r?\n?/);
                    let generatedAt: string | null = null;
                    let generator: string | null = null;
                    let schema: string | null = null;
                    const inputs: Record<string, number | string> = {};
                    let body = raw;
                    if (fmMatch) {
                        body = raw.slice(fmMatch[0].length);
                        const lines = fmMatch[1].split(/\r?\n/);
                        let inInputs = false;
                        for (const line of lines) {
                            if (!line.trim()) continue;
                            const inputsHead = /^inputs:\s*$/.exec(line);
                            if (inputsHead) { inInputs = true; continue; }
                            const topKv = /^([A-Za-z_][A-Za-z0-9_]*):\s*(.*)$/.exec(line);
                            const nestedKv = /^\s{2,}([A-Za-z_][A-Za-z0-9_]*):\s*(.*)$/.exec(line);
                            if (inInputs && nestedKv) {
                                const k = nestedKv[1];
                                const vStr = nestedKv[2].trim().replace(/^["']|["']$/g, '');
                                const vNum = Number(vStr);
                                inputs[k] = Number.isFinite(vNum) && vStr !== '' ? vNum : vStr;
                                continue;
                            }
                            if (topKv) {
                                inInputs = false;
                                const k = topKv[1];
                                const v = topKv[2].trim().replace(/^["']|["']$/g, '');
                                if (k === 'generated_at') generatedAt = v;
                                else if (k === 'generator') generator = v;
                                else if (k === 'schema') schema = v;
                            }
                        }
                    }
                    res.end(JSON.stringify({
                        empty: false,
                        schema,
                        generated_at: generatedAt,
                        generator,
                        inputs,
                        markdown: body,
                        source_path: sourcePath,
                        source_mtime: stat.mtime.toISOString(),
                        fetched_at: new Date().toISOString(),
                    }));
                } catch (e) {
                    res.end(JSON.stringify({
                        empty: true,
                        error: String((e as Error)?.message ?? e),
                        hint: "Failed to read latest.md. Re-run /sentrux-next-steps or check file permissions.",
                        source_path: sourcePath,
                        fetched_at: new Date().toISOString(),
                    }));
                }
            });

            // POST /actions/sentrux/rescan — local-only, drops the cache and
            // forces a fresh scan on the next health probe.
            server.middlewares.use('/actions/sentrux/rescan', (req, res, next) => {
                if (req.method !== 'POST') { next(); return; }
                if (!gateLocal(req, res, 'sentrux/rescan')) return;
                cache.clear();
                const scanId = `scan-${Date.now()}-${Math.random().toString(36).slice(2, 8)}`;
                // Fire-and-forget — the next GET /health will trigger the
                // actual scan and surface results. Returning here keeps the
                // POST fast and lets the UI poll for the new health number.
                void callSentruxTool('rescan').catch(() => { /* ignored */ });
                res.writeHead(200, { 'Content-Type': 'application/json' });
                res.end(JSON.stringify({ queued: true, scan_id: scanId }));
            });
        },
    };
}

export default defineConfig({
    // 3d-force-graph WebGPU bug fixed via patch-package (see patches/3d-force-graph+1.79.1.patch)
    plugins: [react(), dts(), godotStaticPlugin(), primeRadiantControlPlugin(), devDataPlugin(), sentruxPlugin()],
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
