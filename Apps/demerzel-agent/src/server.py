"""Demerzel ACP Agent Server — exposes governance, pipeline, epistemic, and what's-next agents.

Run with:
  cd Apps/demerzel-agent
  DEMERZEL_API_KEY=your-secret-key uvicorn src.server:app --host 0.0.0.0 --port 8200

Security: All endpoints require Authorization: Bearer <DEMERZEL_API_KEY> header.
Agent discovery (GET /agents) is public. All run endpoints require auth.
"""

from __future__ import annotations

import os
from collections.abc import AsyncGenerator
from pathlib import Path

from acp_sdk.models import Message, Metadata, Capability
from acp_sdk.server import RunYield, RunYieldResume, agent, create_app
from dotenv import load_dotenv

from .agents import governance_agent, pipeline_agent, epistemic_agent, whats_next_agent

# Load .env from project root (Apps/demerzel-agent/.env). os.environ already-set
# values win — running with `DEMERZEL_API_KEY=... uvicorn ...` still overrides.
load_dotenv(Path(__file__).resolve().parent.parent / ".env")

# ---------------------------------------------------------------------------
# API Key — required for all mutation/run endpoints
# ---------------------------------------------------------------------------
DEMERZEL_API_KEY = os.environ.get("DEMERZEL_API_KEY", "")

# ---------------------------------------------------------------------------
# Agent 1: Governance
# ---------------------------------------------------------------------------

@agent(
    name="demerzel-governance",
    description=(
        "Demerzel governance agent — query beliefs (tetravalent T/F/U/C), "
        "policies (40+), constitutions (Asimov + Epistemic), strategies, "
        "and learning journal."
    ),
    metadata=Metadata(
        framework="Demerzel",
        capabilities=[
            Capability(name="Belief Query", description="Query tetravalent belief states with filtering"),
            Capability(name="Policy Lookup", description="List and inspect governance policies"),
            Capability(name="Constitution Access", description="Read Asimov and Epistemic constitutions"),
            Capability(name="Strategy Repertoire", description="View learning/teaching strategy catalog"),
        ],
        domains=["governance", "ai-safety", "epistemic"],
    ),
)
async def governance_handler(input: list[Message]) -> AsyncGenerator[RunYield, RunYieldResume]:
    async for msg in governance_agent.handle(input):
        yield msg


# ---------------------------------------------------------------------------
# Agent 2: Pipeline
# ---------------------------------------------------------------------------

@agent(
    name="demerzel-pipeline",
    description=(
        "Demerzel pipeline agent — runs the 5-stage development pipeline: "
        "brainstorm, plan, implement, review, compound. Uses Ollama LLM."
    ),
    metadata=Metadata(
        framework="Demerzel",
        capabilities=[
            Capability(name="Brainstorm", description="Generate ideas for a feature or fix"),
            Capability(name="Plan", description="Create implementation plan with phases"),
            Capability(name="Implement", description="Describe code changes needed"),
            Capability(name="Review", description="Check for security, performance, edge cases"),
            Capability(name="Compound", description="Document learnings from completed work"),
        ],
        domains=["development", "planning", "code-review"],
        recommended_models=["llama3.2"],
    ),
)
async def pipeline_handler(input: list[Message]) -> AsyncGenerator[RunYield, RunYieldResume]:
    async for msg in pipeline_agent.handle(input):
        yield msg


# ---------------------------------------------------------------------------
# Agent 3: Epistemic (Articles E-0 to E-9)
# ---------------------------------------------------------------------------

@agent(
    name="demerzel-epistemic",
    description=(
        "Demerzel epistemic agent — implements the Epistemic Constitution "
        "(Articles E-0 to E-9). SHOW beliefs/strategies/tensor, "
        "METHYLATE/DEMETHYLATE, AMNESIA, BROADCAST."
    ),
    metadata=Metadata(
        framework="Demerzel",
        capabilities=[
            Capability(name="Epistemic Query", description="Query beliefs by tensor state, viscosity, confidence"),
            Capability(name="Strategy Methylation", description="Suppress or restore learning strategies (Article E-8)"),
            Capability(name="Deliberate Amnesia", description="Schedule beliefs for deletion testing (Article E-5)"),
            Capability(name="Federated Review", description="Broadcast beliefs for peer assessment (Article E-9)"),
        ],
        domains=["epistemic", "meta-learning", "governance"],
    ),
)
async def epistemic_handler(input: list[Message]) -> AsyncGenerator[RunYield, RunYieldResume]:
    async for msg in epistemic_agent.handle(input):
        yield msg


# ---------------------------------------------------------------------------
# Agent 4: What's Next
# ---------------------------------------------------------------------------

@agent(
    name="demerzel-whats-next",
    description=(
        "Demerzel recommendation agent — scans GitHub issues (4 repos), "
        "CI/CD status, governance health, and epistemic state for "
        "prioritized recommendations."
    ),
    metadata=Metadata(
        framework="Demerzel",
        capabilities=[
            Capability(name="Issue Scanning", description="Fetches open issues from 4 GitHub repos"),
            Capability(name="CI/CD Analysis", description="Checks workflow run status for failures"),
            Capability(name="Priority Classification", description="Classifies into urgent/high/quick/strategic"),
            Capability(name="Governance Health", description="Checks epistemic state and learning journal"),
        ],
        domains=["project-management", "prioritization", "governance"],
    ),
)
async def whats_next_handler(input: list[Message]) -> AsyncGenerator[RunYield, RunYieldResume]:
    async for msg in whats_next_agent.handle(input):
        yield msg


# ---------------------------------------------------------------------------
# ASGI app (for uvicorn)
# ---------------------------------------------------------------------------

app = create_app(
    governance_handler,
    pipeline_handler,
    epistemic_handler,
    whats_next_handler,
)


# ---------------------------------------------------------------------------
# Auth middleware — protect run endpoints with API key
# ---------------------------------------------------------------------------

from starlette.middleware.base import BaseHTTPMiddleware
from starlette.requests import Request
from starlette.responses import JSONResponse


class ApiKeyMiddleware(BaseHTTPMiddleware):
    """Require Bearer token for all /runs endpoints. Discovery (/agents) is public."""

    async def dispatch(self, request: Request, call_next):  # type: ignore[override]
        # Allow agent discovery without auth (ACP standard)
        if request.url.path.startswith("/agents") or request.url.path in ("/", "/docs", "/openapi.json"):
            return await call_next(request)

        # Allow health checks
        if request.method == "GET" and request.url.path == "/health":
            return await call_next(request)

        # Require API key for everything else (runs, sessions)
        if not DEMERZEL_API_KEY:
            # No key configured — allow localhost only
            client_host = request.client.host if request.client else ""
            if client_host in ("127.0.0.1", "::1", "localhost"):
                return await call_next(request)
            return JSONResponse(
                status_code=401,
                content={"error": "DEMERZEL_API_KEY not configured and request is not from localhost"},
            )

        auth = request.headers.get("authorization", "")
        if auth == f"Bearer {DEMERZEL_API_KEY}":
            return await call_next(request)

        # Also accept X-API-Key header
        api_key = request.headers.get("x-api-key", "")
        if api_key == DEMERZEL_API_KEY:
            return await call_next(request)

        return JSONResponse(
            status_code=401,
            content={"error": "Invalid or missing API key. Use Authorization: Bearer <key> or X-API-Key: <key>"},
        )


app.add_middleware(ApiKeyMiddleware)


def main():
    import uvicorn

    if not DEMERZEL_API_KEY:
        import warnings
        warnings.warn(
            "DEMERZEL_API_KEY not set — server allows localhost access only. "
            "Set DEMERZEL_API_KEY env var for remote access.",
            stacklevel=1,
        )

    uvicorn.run("src.server:app", host="0.0.0.0", port=8200, reload=True)


if __name__ == "__main__":
    main()
