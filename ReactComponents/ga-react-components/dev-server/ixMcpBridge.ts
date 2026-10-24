// Dev-server bridge to the IX MCP server (ix-mcp.exe, stdio JSON-RPC).
// Used only by vite.config.ts for the /ix-pipeline/* routes behind the
// pipeline editor. One process per call: IX tools are stateless here and
// the catalog is cached by the caller, so a long-lived child is not worth
// its restart/cleanup logic yet.
import { spawn } from 'child_process';
import { existsSync } from 'fs';
import { createLineSplitter, pickIxMcpBin } from '../src/dev-data/ixMcpFraming';

export interface IxToolResult {
  isError: boolean;
  text: string;
}

/** IX_MCP_BIN wins; otherwise the sibling ix checkout's release, then debug, build. */
export function resolveIxMcpBin(repoRoot: string, env: NodeJS.ProcessEnv = process.env): string | null {
  return pickIxMcpBin(repoRoot, env.IX_MCP_BIN, process.platform === 'win32', existsSync);
}

export function callIxTool(bin: string, name: string, args: unknown, timeoutMs = 60_000): Promise<IxToolResult> {
  return new Promise((resolve, reject) => {
    const child = spawn(bin, [], { stdio: ['pipe', 'pipe', 'pipe'], windowsHide: true });
    let stderr = '';
    let settled = false;
    const finish = (fn: () => void) => {
      if (settled) return;
      settled = true;
      clearTimeout(timer);
      child.kill();
      fn();
    };
    const timer = setTimeout(
      () => finish(() => reject(new Error(`ix-mcp timed out after ${timeoutMs} ms: ${stderr.slice(-300)}`))),
      timeoutMs,
    );
    const send = (msg: unknown) => child.stdin.write(JSON.stringify(msg) + '\n');

    child.on('error', (e) => finish(() => reject(e)));
    child.on('exit', (code) =>
      finish(() => reject(new Error(`ix-mcp exited (${code}) before answering: ${stderr.slice(-300)}`))),
    );
    child.stderr.on('data', (d) => { stderr += String(d); });
    child.stdout.on('data', createLineSplitter((line) => {
      let msg: { id?: number; result?: { content?: { text?: string }[]; isError?: boolean }; error?: { message?: string } };
      try { msg = JSON.parse(line); } catch { return; }
      if (msg.id === 1) {
        send({ jsonrpc: '2.0', method: 'notifications/initialized' });
        send({ jsonrpc: '2.0', id: 2, method: 'tools/call', params: { name, arguments: args } });
      } else if (msg.id === 2) {
        if (msg.error) {
          finish(() => reject(new Error(msg.error?.message ?? 'ix-mcp returned a JSON-RPC error')));
          return;
        }
        const text = (msg.result?.content ?? []).map((c) => c.text ?? '').join('\n');
        finish(() => resolve({ isError: msg.result?.isError === true, text }));
      }
    }));

    send({
      jsonrpc: '2.0',
      id: 1,
      method: 'initialize',
      params: { protocolVersion: '2025-06-18', capabilities: {}, clientInfo: { name: 'ga-pipeline-editor', version: '0' } },
    });
  });
}
