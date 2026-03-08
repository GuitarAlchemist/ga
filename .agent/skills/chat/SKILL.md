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

```powershell
pwsh Scripts/start-all.ps1 -NoBuild -Dashboard
```

This launches (via Aspire AppHost):
- **MongoDB** on `localhost:27017`
- **MongoExpress** UI at `http://localhost:8081`
- **GaApi** (REST + SignalR) — check Aspire dashboard for actual port
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
| React Frontend | 5173 | Vite dev server |
