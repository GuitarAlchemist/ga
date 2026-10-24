// Pure helpers for the dev-server IX MCP bridge (dev-server/ixMcpBridge.ts).
// Kept free of Node imports, like parsers.ts, so they run under the src/
// vitest suite that CI executes.

/** Splits a stdout stream into complete lines; keeps the partial tail. */
export function createLineSplitter(onLine: (line: string) => void): (chunk: string) => void {
  let buf = '';
  return (chunk: string) => {
    buf += chunk;
    let nl: number;
    while ((nl = buf.indexOf('\n')) >= 0) {
      const line = buf.slice(0, nl).trim();
      buf = buf.slice(nl + 1);
      if (line) onLine(line);
    }
  };
}

/**
 * Which ix-mcp binary to run: IX_MCP_BIN if it exists, else the sibling ix
 * checkout's release build, then its debug build. null when none exists.
 */
export function pickIxMcpBin(
  repoRoot: string,
  envBin: string | undefined,
  isWindows: boolean,
  exists: (p: string) => boolean,
): string | null {
  if (envBin) return exists(envBin) ? envBin : null;
  const exe = isWindows ? 'ix-mcp.exe' : 'ix-mcp';
  const root = repoRoot.replace(/[\\/]+$/, '');
  for (const profile of ['release', 'debug']) {
    const candidate = `${root}/../ix/target/${profile}/${exe}`;
    if (exists(candidate)) return candidate;
  }
  return null;
}
