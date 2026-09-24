import { describe, expect, it } from 'vitest';
import { createLineSplitter, pickIxMcpBin } from './ixMcpFraming';

describe('createLineSplitter', () => {
  it('reassembles JSON-RPC lines split across stdout chunks and skips blanks', () => {
    const lines: string[] = [];
    const feed = createLineSplitter((l) => lines.push(l));
    feed('{"id":1,"res');
    feed('ult":{}}\r\n\n{"id":2}');
    expect(lines).toEqual(['{"id":1,"result":{}}']);
    feed('\n');
    expect(lines).toEqual(['{"id":1,"result":{}}', '{"id":2}']);
  });
});

describe('pickIxMcpBin', () => {
  const only = (...paths: string[]) => (p: string) => paths.includes(p);

  it('IX_MCP_BIN wins when it exists, and a missing one is refused rather than silently replaced', () => {
    expect(pickIxMcpBin('C:/r/ga', 'D:/x/ix-mcp.exe', true, only('D:/x/ix-mcp.exe'))).toBe('D:/x/ix-mcp.exe');
    expect(pickIxMcpBin('C:/r/ga', 'D:/x/ix-mcp.exe', true, only('C:/r/ga/../ix/target/release/ix-mcp.exe'))).toBeNull();
  });

  it('prefers the sibling release build, then debug', () => {
    const rel = 'C:/r/ga/../ix/target/release/ix-mcp.exe';
    const dbg = 'C:/r/ga/../ix/target/debug/ix-mcp.exe';
    expect(pickIxMcpBin('C:/r/ga/', undefined, true, only(rel, dbg))).toBe(rel);
    expect(pickIxMcpBin('C:/r/ga', undefined, true, only(dbg))).toBe(dbg);
    expect(pickIxMcpBin('/r/ga', undefined, false, only('/r/ga/../ix/target/debug/ix-mcp'))).toBe('/r/ga/../ix/target/debug/ix-mcp');
    expect(pickIxMcpBin('C:/r/ga', undefined, true, only())).toBeNull();
  });
});
