---
module: GA (repo-wide)
date: 2026-03-06
problem_type: tooling
component: cclsp MCP server / csharp-ls LSP integration
symptoms:
  - "LSP request timeout: textDocument/documentSymbol (30000ms)"
  - "find_references, find_definition, find_workspace_symbols all fail with timeout"
  - "get_diagnostics works reliably at file level without indexing"
  - "Scoping rootDir to a subdirectory does not resolve the timeout"
root_cause: timeout
resolution_type: workaround
severity: medium
tags: [cclsp, csharp-ls, lsp, dotnet, mcp, slnx]
---

# Tooling: cclsp LSP Cross-Reference Timeout on Large .NET Solutions

## Problem

Setting up the `cclsp` MCP server (github.com/ktnyt/cclsp) for Claude Code on this repo (50+ projects, `.slnx` format, .NET 10) gives mixed results: `get_diagnostics` works perfectly, but every cross-reference tool (`find_references`, `find_definition`, `find_workspace_symbols`, `rename_symbol`) times out after exactly 30 seconds. The timeout is hardcoded in cclsp and not configurable.

## Environment

- Module: Repo-wide tooling
- .NET Version: .NET 10 / C# 14
- Solution format: `AllProjects.slnx` (new XML format — OmniSharp incompatible)
- LSP server: `csharp-ls` 0.22.0
- MCP bridge: `cclsp@latest` via `npx`
- OS: Windows 11 Pro

## Symptoms

- `find_references`, `find_definition`, `find_workspace_symbols` all return: `LSP request timeout: textDocument/documentSymbol (30000ms)`
- Each failing call hangs for almost exactly 30 seconds before erroring
- `get_diagnostics` on the same file succeeds immediately
- Narrowing `rootDir` in `cclsp.json` to a sub-project does not fix the timeout

## What Didn't Work

**Scoping rootDir to a single project:**
```json
{ "extensions": ["cs"], "command": ["csharp-ls"], "rootDir": "Common/GA.Domain.Services" }
```
- **Why it failed:** csharp-ls still scans transitive dependencies when loading a project, so the warm-up time doesn't shrink proportionally with the root scope.

**Restarting the server and retrying:**
- `mcp__cclsp__restart_server` successfully restarts csharp-ls, but the new process hits the same timeout on the next `documentSymbol` request because it must re-index from scratch.

**Trying OmniSharp:**
- Not installed. Would not support `.slnx` format anyway.

## Solution

**Direct solution:** Use `get_diagnostics` as the primary LSP tool. Accept that cross-reference navigation is unavailable via cclsp on this codebase until cclsp adds a configurable timeout.

### What Was Installed

```bash
dotnet tool install -g csharp-ls        # v0.22.0 — C# language server
dotnet tool install -g fsautocomplete   # v0.83.0 — F# language server
```

### Configuration

**`cclsp.json`** (repo root):

```json
{
  "servers": [
    {
      "extensions": ["cs"],
      "command": ["csharp-ls"],
      "rootDir": "Common/GA.Domain.Services",
      "restartInterval": 60
    },
    {
      "extensions": ["fs", "fsi", "fsx"],
      "command": ["fsautocomplete", "--use-stdin"],
      "rootDir": "Common/GA.Business.Config",
      "restartInterval": 60
    }
  ]
}
```

**`.mcp.json`** entry (Windows requires `cmd /c` wrapper):

```json
{
  "cclsp": {
    "command": "cmd",
    "args": ["/c", "npx", "cclsp@latest"],
    "env": {
      "CCLSP_CONFIG_PATH": "C:\\Users\\spare\\source\\repos\\ga\\cclsp.json"
    }
  }
}
```

> **Windows note:** `command: "cmd"` with `args: ["/c", "npx", ...]` is required. `npx` alone fails because Claude Code spawns the process without a shell. `CCLSP_CONFIG_PATH` must be an absolute path with backslash-escaped separators.

### Tool Behaviour Summary

| Tool | Works? | Why |
|---|---|---|
| `get_diagnostics` | **Yes** | Calls `textDocument/diagnostic` directly — no symbol index needed |
| `find_definition` | No | Calls `textDocument/documentSymbol` first → times out |
| `find_references` | No | Same root cause |
| `find_workspace_symbols` | No | Requires full workspace index |
| `rename_symbol` | No | Same root cause |
| `get_hover` | Likely yes | Single-file request, no symbol scan |

## Why This Works (Root Cause)

`cclsp` resolves symbol names by calling `textDocument/documentSymbol` on the file before dispatching the actual LSP request. `csharp-ls` must fully parse and load the project graph to answer this query. On a 50+ project .NET solution, that warm-up takes 60–120 seconds — well beyond cclsp's hardcoded 30s timeout.

`get_diagnostics` bypasses this entirely: it uses `textDocument/diagnostic`, a pull-based diagnostic protocol that operates at the file level without requiring a complete workspace index.

## Prevention

### Detect this problem early
- Solution has 50+ projects → assume cross-reference tools will timeout
- First `get_diagnostics` takes > 10s → csharp-ls still loading; symbol queries will fail
- Calls hang for exactly 30s then error → hardcoded timeout, not a transient issue

### When `get_diagnostics` is sufficient
- Verifying a change compiles (type errors, missing members, ambiguous overloads)
- Zero-warnings compliance sweep (`IDE0022`, `CA1822`, `CS8602`, etc.)
- Checking an interface is implemented correctly in a file you have open
- Verifying a refactor didn't break a specific file

### When you truly need cross-reference data
Use these fallbacks in order:

**1. Grep (most reliable):**
```bash
# All references to a type
grep -r "\bMyClass\b" Common/ Apps/ --include="*.cs"

# All implementations of an interface
grep -r ": IMyInterface" . --include="*.cs"
```

**2. Glob + naming conventions:**
```bash
# All controllers, agents, services — naming is consistent in this repo
Glob "**/*Controller.cs"
Glob "**/*Agent.cs"
```

**3. Five-layer dependency model as navigation map:**
Symbols in `GA.Business.ML` (Layer 4) can only be referenced from Layer 5 and App projects. Use the architecture to prune the search space before grepping.

**4. dotnet build as reference finder:**
```powershell
dotnet build AllProjects.slnx -c Debug 2>&1 | grep "error CS"
```
Build errors after a rename name every broken callsite.

### Effective use of `get_diagnostics`

```
1. Edit file in Layer N
2. get_diagnostics on the edited file
3. get_diagnostics on the consuming project's entry point
4. dotnet build via Bash as the final gate
```

Error codes to know:
- `CS0246` — type not found (missing using or project reference)
- `CS1061` — member does not exist on type (renamed/removed)
- `CS8602` — possible null dereference
- `IDE0022`/`CA1822` — style warnings (zero-warnings sweep)

### Configuration tweaks worth trying

- **Check for a timeout field** in future cclsp versions — raise to 120000ms if it appears
- **Pre-warm via build:** `dotnet build AllProjects.slnx` before starting a session gives csharp-ls binary artifacts to index against
- **Create a `.slnf` solution filter** with only 5–10 relevant projects; point csharp-ls at it for focused sessions
- **Update csharp-ls:** `dotnet tool update -g csharp-ls` — newer versions have better large-solution cold-start performance

## Related Issues

- See also: [docs/solutions/refactoring/dotnet-solution-structure-cleanup.md](../refactoring/dotnet-solution-structure-cleanup.md) — .NET project structure patterns that affect LSP indexing surface
