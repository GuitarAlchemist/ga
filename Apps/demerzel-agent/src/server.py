"""Demerzel ACP Agent Server — exposes governance, pipeline, epistemic, and what's-next agents.

Run with:
  cd Apps/demerzel-agent
  uvicorn src.server:app --host 0.0.0.0 --port 8200
"""

from __future__ import annotations

from collections.abc import AsyncGenerator

from acp_sdk.models import Message, Metadata, Capability
from acp_sdk.server import RunYield, RunYieldResume, agent, create_app

from .agents import governance_agent, pipeline_agent, epistemic_agent, whats_next_agent

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


def main():
    import uvicorn
    uvicorn.run("src.server:app", host="0.0.0.0", port=8200, reload=True)


if __name__ == "__main__":
    main()
