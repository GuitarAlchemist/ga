# Demerzel ACP Agent

Demerzel as a standalone [Agent Communication Protocol (ACP)](https://agentcommunicationprotocol.dev) server. Exposes governance, pipeline, epistemic, and recommendation agents that any ACP-compatible client can discover and communicate with.

## Quick Start

```bash
cd Apps/demerzel-agent
pip install -e .
uvicorn src.server:app --host 0.0.0.0 --port 8200
```

## Agents

| Agent | Endpoint | What it does |
|-------|----------|--------------|
| `demerzel-governance` | `/agents/demerzel-governance` | Query beliefs, policies, constitutions, strategies |
| `demerzel-pipeline` | `/agents/demerzel-pipeline` | Run brainstorm/plan/build/review/compound |
| `demerzel-epistemic` | `/agents/demerzel-epistemic` | SHOW/METHYLATE/AMNESIA/BROADCAST (Epistemic Constitution) |
| `demerzel-whats-next` | `/agents/demerzel-whats-next` | Prioritized recommendations from GitHub + governance |

## Discovery

```bash
# List all agents
curl http://localhost:8200/agents

# Get agent manifest
curl http://localhost:8200/agents/demerzel-governance
```

## Usage (Python ACP Client)

```python
from acp_sdk.client import Client
from acp_sdk.models import Message, MessagePart

async with Client(base_url="http://localhost:8200") as client:
    # Query beliefs
    run = await client.run_sync(
        agent="demerzel-governance",
        input=[Message(parts=[MessagePart(content="list beliefs")])]
    )
    print(run.output[0].parts[0].content)

    # Run pipeline
    run = await client.run_sync(
        agent="demerzel-pipeline",
        input=[Message(parts=[MessagePart(content='{"title": "Fix CI failures", "stage": "brainstorm"}')])]
    )

    # What's next?
    run = await client.run_sync(
        agent="demerzel-whats-next",
        input=[Message(parts=[MessagePart(content="What should I work on?")])]
    )
```

## Architecture

```
ACP Server (port 8200)
  ├── demerzel-governance → reads governance/demerzel/state/
  ├── demerzel-pipeline   → runs Ollama prompts per stage
  ├── demerzel-epistemic  → implements Epistemic Constitution E-0 to E-9
  └── demerzel-whats-next → scans GitHub + CI/CD + governance
```

## Environment Variables

| Variable | Default | Description |
|----------|---------|-------------|
| `OLLAMA_BASE_URL` | `http://localhost:11434` | Ollama API endpoint |
| `OLLAMA_MODEL` | `llama3.2` | Default model for agent reasoning |
| `GITHUB_TOKEN` | (none) | GitHub PAT for higher rate limits |
| `DEMERZEL_ROOT` | (auto-detected) | Path to governance/demerzel/ |
