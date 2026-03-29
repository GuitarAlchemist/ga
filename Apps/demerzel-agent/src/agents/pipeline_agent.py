"""Demerzel Pipeline Agent — run brainstorm/plan/build/review/compound stages."""

from __future__ import annotations

import json
from collections.abc import AsyncGenerator

from acp_sdk.models import Message, MessagePart
from acp_sdk.server import RunYield, RunYieldResume

from ..services import ollama

STAGE_PROMPTS = {
    "brainstorm": "Brainstorm 3-5 concrete ideas for: {title}. Consider feasibility, dependencies, and existing infrastructure. Be specific and actionable. Under 500 words.",
    "plan": "Create a concise implementation plan for: {title}. Structure as phases with deliverables, critical files, and testing approach. Under 500 words.",
    "implement": "Describe the key implementation steps for: {title}. List specific files to create/modify, code changes, and order of operations. Under 500 words.",
    "review": "Review the approach for: {title}. Check for security, performance, edge cases, governance compliance, and testing gaps. Under 300 words.",
    "compound": "Document what was learned from: {title}. Capture key decisions, patterns, what worked/didn't, and what to do differently. Under 300 words.",
}

ALL_STAGES = ["brainstorm", "plan", "implement", "review", "compound"]


async def handle(input: list[Message]) -> AsyncGenerator[RunYield, RunYieldResume]:
    """Run pipeline stages.

    Input format: JSON with { "title": "...", "stage": "brainstorm|plan|...|all", "source": "..." }
    Or plain text: "brainstorm: <title>" / "run all: <title>"
    """
    text = ""
    for msg in input:
        for part in msg.parts:
            if hasattr(part, "content") and isinstance(part.content, str):
                text += part.content + " "
    text = text.strip()

    # Try JSON input
    title = text
    stages = ALL_STAGES
    source = None
    try:
        data = json.loads(text)
        title = data.get("title", text)
        source = data.get("source")
        stage_input = data.get("stage", "all")
        if stage_input != "all":
            stages = [stage_input]
    except (json.JSONDecodeError, TypeError):
        # Plain text — check for "stage: title" format
        for s in ALL_STAGES:
            if text.lower().startswith(f"{s}:"):
                stages = [s]
                title = text[len(s) + 1:].strip()
                break
        if text.lower().startswith("run all:"):
            title = text[8:].strip()

    yield Message(parts=[MessagePart(
        content=f"Starting pipeline for: {title} (stages: {', '.join(stages)})",
    )])

    for stage in stages:
        prompt_template = STAGE_PROMPTS.get(stage)
        if not prompt_template:
            yield Message(parts=[MessagePart(content=f"Unknown stage: {stage}")])
            continue

        prompt = f"You are Demerzel, an AI governance agent. {prompt_template.format(title=title)}"
        if source:
            prompt += f"\nSource: {source}"

        yield Message(parts=[MessagePart(
            content=f"[{stage}] Running...",
        )])

        result = await ollama.generate(prompt)

        yield Message(parts=[MessagePart(
            content=f"[{stage}] {result}",
        )])

    yield Message(parts=[MessagePart(
        content=f"Pipeline complete for: {title} ({len(stages)} stage(s))",
    )])
