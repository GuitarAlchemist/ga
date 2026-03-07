---
status: complete
priority: p1
issue_id: "001"
tags: [code-review, chatbot, mcp]
dependencies: []
---

# ChatTool.cs not committed — MCP chatbot tool is missing

## Problem Statement

The PR commit message claims todo 013 is resolved ("Add GaMcpServer ChatTool + register named HttpClient('gaapi') in GaMcpServer"), but `GaMcpServer/Tools/ChatTool.cs` does not exist in the HEAD commit. The `HttpClient("gaapi")` registration was added to `GaMcpServer/Program.cs` but without a tool class to use it, the MCP server does not expose the chatbot to AI agents.

## Findings

- `GaMcpServer/Tools/` contains: `AtonalTool.cs`, `EchoTool.cs`, `FeedReaderToolWrapper.cs`, `InstrumentTool.cs`, `KeyTools.cs`, `ModeTool.cs`, `WebScrapingToolWrapper.cs`, `WebSearchToolWrapper.cs` — no `ChatTool.cs`
- `GaMcpServer/Program.cs` registers `AddHttpClient("gaapi")` pointing to `GaApi:BaseUrl` — the plumbing is there
- `[McpServerToolType]` discovery via `WithToolsFromAssembly()` in Program.cs will find no chatbot tool at runtime
- The file was described in the session summary but appears to have been lost during the stash/pop cycle

## Proposed Solutions

### Option A: Add ChatTool.cs now (Recommended)
Recreate the file that was described in the session summary:

```csharp
// GaMcpServer/Tools/ChatTool.cs
namespace GaMcpServer.Tools;

using System.Net.Http.Json;
using Microsoft.Extensions.Configuration;
using ModelContextProtocol.Server;

[McpServerToolType]
public class ChatTool(IHttpClientFactory httpClientFactory, IConfiguration configuration)
{
    [McpServerTool]
    [Description("Ask the Guitar Alchemist chatbot a music theory or guitar question. Returns a grounded answer with chord voicings and agent routing metadata.")]
    public async Task<string> AskChatbot(
        [Description("The music theory or guitar question to ask")] string question,
        CancellationToken cancellationToken = default)
    {
        var client = httpClientFactory.CreateClient("gaapi");
        var response = await client.PostAsJsonAsync(
            "/api/chatbot/chat",
            new { message = question },
            cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(cancellationToken);
    }
}
```

**Pros:** Completes the stated PR goal; small change
**Cons:** None
**Effort:** Small
**Risk:** Low

### Option B: Remove the HttpClient registration from Program.cs
Revert the `AddHttpClient("gaapi")` from Program.cs and document the tool as deferred.

**Pros:** Keeps the repo consistent — no orphaned registration
**Cons:** Loses the feature entirely
**Effort:** Small
**Risk:** Low

## Recommended Action

Option A — add the `ChatTool.cs` file.

## Technical Details

- **Affected files:** `GaMcpServer/Tools/ChatTool.cs` (missing), `GaMcpServer/Program.cs`
- **Component:** GaMcpServer
- **No DB changes**

## Acceptance Criteria

- [ ] `GaMcpServer/Tools/ChatTool.cs` exists and compiles
- [ ] `dotnet build GaMcpServer` succeeds
- [ ] MCP server starts and `AskChatbot` tool appears in tool list when `GaApi` base URL is configured

## Work Log

- 2026-03-06: Found during code review of PR #2
