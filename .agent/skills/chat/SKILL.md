---
name: "GA Chat"
description: "Build, index data, and run the GA chatbot locally. Knows the full startup sequence: build solution → start Aspire services → seed MongoDB → probe the chat endpoint."
---

# GA Chat Skill

Use this skill when you need to **start the Guitar Alchemist chatbot locally**, **verify it is responding**, or **diagnose why it is not working**.

## When to Use

- User asks "is the chatbot working?"
- User says "start the chatbot" / "run the chatbot"
- Probing the chat endpoint before or after code changes
- CI is green but you want to confirm end-to-end locally
- Debugging why a chat request returns 404 or times out

---

## Step 1 — Prerequisites Check

Verify all required tools are installed:

```powershell
# .NET 10
dotnet --version   # must be 10.x

# Ollama (local LLM backend)
ollama --version
ollama list        # confirm a model is available (e.g. llama3.2)

# MongoDB (via Docker or local install)
docker ps | grep mongo
# OR
mongosh --eval "db.version()"
```

If Ollama has no models, pull one:

```powershell
ollama pull llama3.2
```

---

## Step 2 — Build the Solution

```powershell
cd C:\Users\spare\source\repos\ga
dotnet build AllProjects.slnx -c Debug
```

Expected: `Build succeeded. 0 Error(s)`

If warnings fire for files you touched, fix them (`IDE0305`, `IDE0022`, etc.) per the C# coding standards.

---

## Step 3 — Start All Services via Aspire

**Architecture contract (non-negotiable):**
- **Aspire** orchestrates: MongoDB (Docker), Redis, all microservices
- **GaApi** runs via `dotnet run` or via Aspire — never from a Docker image
- **Ollama** runs natively at `localhost:11434` (not Docker)
- Aspire auto-injects `ConnectionStrings__guitar-alchemist` into GaApi; no manual MongoDB URL needed

```powershell
pwsh Scripts/start-all.ps1 -NoBuild -Dashboard
```

This launches (via Aspire AppHost):
- **MongoDB** (Docker, dynamic port, data persists via `ga-mongodb-data` volume)
- **MongoExpress** UI at `http://localhost:8081`
- **GaApi** (REST + SignalR) — actual port shown in Aspire dashboard
- **GaChatbot** — internal Ollama-backed chatbot service
- **Aspire Dashboard** at `https://localhost:15001`

Wait ~30 seconds for all services to become healthy.

---

## Step 4 — Verify Services Are Up

```powershell
# Aspire health dashboard
Start-Process "https://localhost:15001"

# Or check individual service health via curl
# Find the GaApi port from the Aspire dashboard or launchSettings
$port = 5232   # default http port from launchSettings.json
curl -s "http://localhost:$port/api/chatbot/status"
```

Expected response:
```json
{"status":"Healthy","agentsLoaded":5,"routerReady":true}
```

If GaApi port differs, check the Aspire dashboard Resources tab — click the GaApi entry to see its actual URL.

---

## Step 5 — Seed / Index MongoDB Data

Run once after a fresh MongoDB start (data persists across restarts):

```powershell
# Option A: Via the GaApi data-sync endpoint (preferred)
$port = 5232
curl -sk -X POST "http://localhost:$port/api/data/sync" \
  -H "Content-Type: application/json"

# Option B: Via the import script (if GaApi is not yet running)
pwsh Scripts/import-to-mongodb.ps1

# Option C: GaDataCLI (full export + reimport)
dotnet run --project Apps/GaDataCLI/GaDataCLI.csproj -- --sync-all
```

Check seeding completed:
```bash
mongosh guitar-alchemist --eval "db.chords.countDocuments()"
# Expected: > 0 (thousands of chords)
```

---

## Step 6 — Probe the Chat Endpoint

### Non-streaming (JSON response):

```bash
PORT=5232
curl -s -X POST "http://localhost:$PORT/api/chatbot/chat" \
  -H "Content-Type: application/json" \
  -d '{"message":"What notes are in a Cmaj7 chord?","sessionId":"skill-test"}' | jq .
```

Expected response shape:
```json
{
  "response": "A Cmaj7 chord contains the notes C, E, G, and B...",
  "agentUsed": "TheoryAgent",
  "routingMethod": "semantic",
  "confidence": 0.91,
  "chords": ["Cmaj7"]
}
```

### Streaming (SSE):

```bash
curl -sN -X POST "http://localhost:$PORT/api/chatbot/chat/stream" \
  -H "Content-Type: application/json" \
  -H "Accept: text/event-stream" \
  -d '{"message":"Suggest a blues progression in E","sessionId":"skill-stream"}'
```

### Via SignalR Hub (advanced):

The chatbot also exposes a SignalR hub at `/hubs/chatbot`. Use this for real-time streaming in the React frontend.

---

## Step 7 — Check Routing Decisions

```bash
PORT=5232
curl -s "http://localhost:$PORT/api/chatbot/status" | jq .
```

To see which agent handled the last request and with what confidence, look at the response's `agentUsed`, `routingMethod`, and `confidence` fields.

---

## Common Failure Scenarios

| Symptom | Cause | Fix |
|---|---|---|
| `404 Not Found` on `/api/chatbot/*` | GaApi not running | Check Aspire dashboard; restart with `start-all.ps1` |
| `Connection refused` on port 5232 | Wrong port | Find actual port in Aspire dashboard |
| `503 Service Unavailable` | Ollama not running | `ollama serve` in a separate terminal |
| Chat returns empty/timeout | No Ollama model | `ollama pull llama3.2` |
| MongoDB collections empty | Data not seeded | Run Step 5 |
| Build fails with `CS0246` | Missing model type | `dotnet build AllProjects.slnx` — fix errors |
| `AllProjects.sln not found` | Old workflow/command | Use `AllProjects.slnx` (with x) |

---

## Full Local Chat Session (Copy-Paste)

```powershell
# From repo root — complete sequence
dotnet build AllProjects.slnx -c Debug
pwsh Scripts/start-all.ps1 -NoBuild -Dashboard

# In a new terminal — wait 30 sec then verify
Start-Sleep 30
$PORT = 5232
curl -s "http://localhost:$PORT/api/chatbot/status" | ConvertFrom-Json

# Ask a question
$body = @{ message = "What is a tritone substitution?"; sessionId = "probe" } | ConvertTo-Json
Invoke-RestMethod -Uri "http://localhost:$PORT/api/chatbot/chat" -Method POST -Body $body -ContentType "application/json"
```

---

## Ports Reference

| Service | Default Port | Notes |
|---|---|---|
| GaApi HTTPS | 7184 | launchSettings.json |
| GaApi HTTP | 5232 | launchSettings.json |
| Aspire Dashboard | 15001 | HTTPS |
| MongoDB | 27017 | Docker or local |
| MongoExpress | 8081 | Browser UI |
| Ollama | 11434 | LLM backend |
| Jaeger Tracing | 16686 | OpenTelemetry spans |
| React Frontend | 5173 | Vite dev server |

---

## Step 8 — OpenTelemetry Observability Sub-Agent

When you need to understand chatbot performance, trace a slow request, or identify bottlenecks, run the observability sub-agent below. It connects to Jaeger, reads spans, and provides actionable recommendations.

### Trigger Phrases
- "the chatbot is slow"
- "analyze chatbot performance"
- "trace the last request"
- "find bottlenecks in the pipeline"
- "why is routing taking so long?"

### What the Sub-Agent Does

Launch a general-purpose sub-agent with this prompt:

```
You are an OpenTelemetry observability analyst for the Guitar Alchemist chatbot pipeline.

The chatbot exposes spans via the "GA.Chatbot" ActivitySource, registered in ServiceDefaults.
Spans flow: orchestration.answer → routing.route → [routing.semantic | routing.llm | routing.keyword] → agent.process → agent.chat

Jaeger is available at http://localhost:16686.
GaApi is available at http://localhost:5232.

Steps:
1. Fetch recent traces from Jaeger: GET http://localhost:16686/api/traces?service=GaApi&limit=20
2. For each trace, extract span names, durations (microseconds), and tags.
3. Build a breakdown table: span name | avg duration | p95 duration | % of total
4. Identify bottlenecks: any span > 500ms average is a concern, > 2000ms is critical.
5. Look for these specific tags on routing spans:
   - routing.method (semantic | llm | keyword | none)
   - routing.confidence (should be >= 0.85 for semantic; < 0.85 triggers LLM fallback)
   - routing.scores (JSON list of all agent scores)
6. Check agent.chat spans for llm.response_ms > 3000ms (Ollama slow).
7. Check orchestration.answer spans for orchestration.branch to see which pipeline paths are used.
8. Report findings in this structure:
   ## Pipeline Health Summary
   - Total traces analyzed: N
   - Average end-to-end latency: Xms
   - P95 end-to-end latency: Xms

   ## Span Breakdown (sorted by avg duration desc)
   | Span | Avg (ms) | P95 (ms) | % of total |

   ## Bottlenecks Found
   - [severity: critical/warning/info] description

   ## Routing Analysis
   - Semantic hits: N%  (confidence >= 0.85)
   - LLM fallbacks: N%  (confidence < 0.85 — each costs ~1-2s extra)
   - Keyword fallbacks: N%

   ## Recommendations
   1. [ranked by impact]
```

### Running the Sub-Agent

```
Agent tool: general-purpose
Prompt: [the prompt above, with actual Jaeger data if available]
```

### Manual Jaeger Queries

```bash
# All recent GaApi traces
curl -s "http://localhost:16686/api/traces?service=GaApi&limit=10" | jq '.data[].spans[] | {op: .operationName, dur: .duration}'

# Only slow traces (> 3 seconds)
curl -s "http://localhost:16686/api/traces?service=GaApi&limit=50&minDuration=3000000" | jq .

# Check routing confidence distribution
curl -s "http://localhost:16686/api/traces?service=GaApi&operation=routing.route&limit=50" \
  | jq '[.data[].spans[].tags[] | select(.key == "routing.confidence") | .value | tonumber] | {min: min, max: max, avg: add/length}'
```

### Common Bottleneck Patterns

| Symptom | Likely Cause | Fix |
|---|---|---|
| `routing.semantic` > 500ms | Embedding generation slow | Preload embeddings at startup; check Ollama health |
| `routing.llm` spans present | Semantic confidence < 0.85 | Improve agent descriptions / add keyword synonyms |
| `agent.chat` > 3000ms | Ollama model too large or cold | Use `llama3.2:1b` or `qwen:0.5b` for lower latency |
| `orchestration.answer` > 5000ms | Multiple sequential LLM calls | Check if `ChatWithCritiqueAsync` is being triggered |
| Many `routing.keyword` fallbacks | Embedding init failing | Check `EmbeddingProvider` config; ensure `nomic-embed-text` is pulled |

### ChatJsonResponse Observability Fields

Every non-streaming `/api/chatbot/chat` response now includes:
```json
{
  "naturalLanguageAnswer": "...",
  "agentId": "theory",
  "confidence": 0.91,
  "routingMethod": "semantic",
  "elapsedMs": 1234,
  "traceId": "abc123def456..."
}
```
Use `traceId` to look up the exact trace in Jaeger: `http://localhost:16686/trace/{traceId}`
