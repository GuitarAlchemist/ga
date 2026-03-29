"""Demerzel Governance Agent — query beliefs, policies, constitutions, strategies."""

from __future__ import annotations

import json
from collections.abc import AsyncGenerator

from acp_sdk.models import Message, MessagePart
from acp_sdk.server import RunYield, RunYieldResume

from ..services import governance


async def handle(input: list[Message]) -> AsyncGenerator[RunYield, RunYieldResume]:
    """Process governance queries.

    Supported commands (in message text):
      - "list beliefs" — return all belief states
      - "get belief <name>" — return a specific belief
      - "list policies" — return all policies
      - "list strategies" — return strategy repertoire
      - "constitution [name]" — return constitution text
      - "list learning" — return learning journal entries
      - Any other text — treated as a natural language governance query
    """
    text = ""
    for msg in input:
        for part in msg.parts:
            if hasattr(part, "content") and isinstance(part.content, str):
                text += part.content + " "
    text = text.strip().lower()

    if text.startswith("list beliefs"):
        beliefs = governance.list_beliefs()
        yield Message(
            parts=[MessagePart(
                content=json.dumps(beliefs, indent=2, default=str),
                content_type="application/json",
            )]
        )

    elif text.startswith("get belief"):
        name = text.replace("get belief", "").strip()
        belief = governance.get_belief(name)
        if belief:
            yield Message(parts=[MessagePart(
                content=json.dumps(belief, indent=2, default=str),
                content_type="application/json",
            )])
        else:
            yield Message(parts=[MessagePart(content=f"Belief '{name}' not found.")])

    elif text.startswith("list policies"):
        policies = governance.list_policies()
        summary = [{"name": p.get("name", "?"), "version": p.get("version", "?")} for p in policies]
        yield Message(parts=[MessagePart(
            content=json.dumps(summary, indent=2),
            content_type="application/json",
        )])

    elif text.startswith("list strategies"):
        strategies = governance.load_strategies()
        yield Message(parts=[MessagePart(
            content=json.dumps(strategies, indent=2, default=str),
            content_type="application/json",
        )])

    elif text.startswith("constitution"):
        name = text.replace("constitution", "").strip() or "epistemic"
        content = governance.load_constitution(name)
        yield Message(parts=[MessagePart(content=content, content_type="text/markdown")])

    elif text.startswith("list learning"):
        entries = governance.list_learning_entries()
        yield Message(parts=[MessagePart(
            content=json.dumps(entries, indent=2, default=str),
            content_type="application/json",
        )])

    else:
        # Natural language query — summarize governance state
        from ..services import ollama
        beliefs = governance.list_beliefs()
        policies = governance.list_policies()
        context = (
            f"You are Demerzel, an AI governance agent. "
            f"You have {len(beliefs)} beliefs and {len(policies)} policies. "
            f"Answer the following governance query:\n\n{text}"
        )
        response = await ollama.generate(context)
        yield Message(parts=[MessagePart(content=response)])
