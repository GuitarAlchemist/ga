# GaChatbot.Api

Thin public-safe chatbot host for Guitar Alchemist.

What it includes:

- `POST /api/chatbot/chat/stream`
- `POST /api/chatbot/chat`
- `GET /api/chatbot/status`
- `GET /api/chatbot/examples`

What it does not include:

- Redis
- SignalR
- GraphQL
- OAuth
- reverse proxy config
- deployment manifests
- committed credentials

## Local run

```powershell
dotnet run --project Apps/GaChatbot.Api
```

Helper scripts:

```powershell
pwsh Scripts/start-chatbot-api.ps1
pwsh Scripts/start-chatbot-dev.ps1
pwsh Scripts/chatbot-status.ps1
```

## Configuration

Use environment variables or local user-specific config outside git.

Common variables:

```text
Chatbot__Mode=full
AI__ChatProvider=ollama
AI__EmbeddingProvider=ollama
Ollama__BaseUrl=http://localhost:11434
Ollama__ChatModel=llama3.2:3b
Ollama__EmbeddingModel=nomic-embed-text
GITHUB_TOKEN=...
```

Notes:

- `Chatbot__Mode=full` is now the default development mode. It wires the full orchestration stack, including routing and OPTIC-K-aware services.
- Use `Chatbot__Mode=direct` for the cheapest public-safe fallback. It only registers the direct chat path and skips the full orchestration graph.
- `Chatbot__Mode=routed` uses a lightweight keyword router plus specialized prompt profiles, still without the full orchestration graph.
- Use `Chatbot__Mode=full` or `Chatbot__Mode=orchestrated` only when you want the richer orchestration stack.
- `GITHUB_TOKEN` is only needed when `AI__ChatProvider=github` or `AI__EmbeddingProvider=github`.
- The algebra route now supports an external IX process over a simple JSON stdin/stdout contract. Configure it with:

```text
IX__External__Enabled=true
IX__External__ExecutablePath=C:\path\to\ga-chatbot.exe
IX__External__Arguments__0=algebra
IX__External__WorkingDirectory=C:\path\to\ix
IX__External__TimeoutSeconds=10
```

- With the current IX repo, the intended executable is the `ga-chatbot` release binary:

```powershell
cargo build -p ga-chatbot --release
```

- Then point `IX__External__ExecutablePath` at the resulting binary, for example:

```text
IX__External__ExecutablePath=C:\Users\<you>\source\repos\ix\target\release\ga-chatbot.exe
```

- The external process should read `{"query":"..."}` from stdin and emit one JSON object to stdout:

```json
{
  "naturalLanguageAnswer": "0146 and 0137 are Z-related.",
  "queryType": "z-relation",
  "facts": {
    "left": "[0,1,4,6]",
    "right": "[0,1,3,7]"
  },
  "source": "ix",
  "revision": "7b02a56"
}
```

- If no external IX executable is configured, or the process fails, `GaChatbot.Api` falls back to the internal GA algebra compatibility path and marks grounding as `ix-compatible`.
- Do not commit `.env`, `appsettings.Production.json`, `railway.toml`, `vercel.json`, or provider-specific deployment files.
- Keep any public hostname, tunnel name, or provider project ID outside this repository.
- For local frontend work, set `VITE_GA_API_URL=http://localhost:5252` or use `Scripts/start-chatbot-dev.ps1`, which sets it for the Vite session.
