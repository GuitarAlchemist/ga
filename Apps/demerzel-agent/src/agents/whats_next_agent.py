"""Demerzel What's Next Agent — prioritized recommendations from GitHub + governance."""

from __future__ import annotations

import json
from collections.abc import AsyncGenerator

from acp_sdk.models import Message, MessagePart
from acp_sdk.server import RunYield, RunYieldResume

from ..services import github, governance, ollama


async def handle(input: list[Message]) -> AsyncGenerator[RunYield, RunYieldResume]:
    """Analyze GitHub issues, CI/CD status, and governance state to recommend what to work on next.

    Input: optional focus query (e.g., "chatbot improvements", "governance debt")
    Output: prioritized recommendations in 4 tiers (urgent, high, quick, strategic)
    """
    query = ""
    for msg in input:
        for part in msg.parts:
            if hasattr(part, "content") and isinstance(part.content, str):
                query += part.content + " "
    query = query.strip()

    yield Message(parts=[MessagePart(content="Scanning 4 repos, CI/CD, governance state...")])

    # Gather context
    issues = await github.fetch_open_issues()
    ci_runs = await github.fetch_ci_status()
    beliefs = governance.list_beliefs()
    strategies = governance.load_strategies()
    learning = governance.list_learning_entries()

    recommendations: list[dict] = []

    # Urgent: failing CI
    failures = [r for r in ci_runs if r.get("conclusion") == "failure"]
    failed_repos = list({f["repo"] for f in failures})
    if failed_repos:
        recommendations.append({
            "priority": "urgent",
            "title": f"Fix CI failures in {', '.join(failed_repos)}",
            "rationale": f"{len(failures)} workflow run(s) failing. Green CI is prerequisite for everything else.",
            "source": "CI/CD",
        })

    # Urgent: bugs
    bugs = [i for i in issues if any(l.get("name") == "bug" for l in i.get("labels", []))]
    for bug in bugs[:2]:
        recommendations.append({
            "priority": "urgent",
            "title": bug["title"],
            "rationale": f"Bug in {bug['repo']}. Fix bugs before adding features.",
            "source": f"{bug['repo']}#{bug['number']}",
            "url": bug.get("html_url"),
        })

    # High value: features
    features = [i for i in issues if any(
        l.get("name") in ("enhancement", "feat") for l in i.get("labels", [])
    ) or i.get("title", "").lower().startswith("feat:")]
    for feat in features[:3]:
        recommendations.append({
            "priority": "high",
            "title": feat["title"],
            "rationale": f"Feature request in {feat['repo']}. Adds user-facing value.",
            "source": f"{feat['repo']}#{feat['number']}",
            "url": feat.get("html_url"),
        })

    # Quick wins: small issues
    seen = {i["number"] for i in bugs + features}
    quick = [i for i in issues if i["number"] not in seen and len(i.get("title", "")) < 60]
    for q in quick[:2]:
        recommendations.append({
            "priority": "quick",
            "title": q["title"],
            "rationale": f"Small scope item in {q['repo']}. Can ship quickly.",
            "source": f"{q['repo']}#{q['number']}",
            "url": q.get("html_url"),
        })

    # Strategic: epistemic items
    amnesia_entries = [e for e in learning if e.get("type") == "amnesia_scheduled" and not e.get("executed")]
    if amnesia_entries:
        recommendations.append({
            "priority": "strategic",
            "title": f"{len(amnesia_entries)} belief(s) scheduled for amnesia review",
            "rationale": "Article E-5: beliefs scheduled for deletion need re-derivation testing.",
            "source": "Epistemic Constitution",
        })

    recommendations.append({
        "priority": "strategic",
        "title": "Run epistemic tensor review",
        "rationale": "Article E-9: periodic federated peer review prevents epistemic isolation.",
        "source": "Epistemic Constitution",
    })

    # If user provided a query, ask Ollama to prioritize
    if query:
        context = (
            f"You are Demerzel. Given these recommendations:\n"
            f"{json.dumps(recommendations, indent=2)}\n\n"
            f"The user asks: {query}\n\n"
            f"Re-rank and filter recommendations relevant to the query. "
            f"Return a concise prioritized list."
        )
        llm_response = await ollama.generate(context)
        yield Message(parts=[MessagePart(content=llm_response)])
    else:
        yield Message(parts=[MessagePart(
            content=json.dumps({"recommendations": recommendations}, indent=2),
            content_type="application/json",
        )])

    yield Message(parts=[MessagePart(
        content=f"Analysis complete: {len(recommendations)} recommendations across "
                f"{len(issues)} open issues, {len(failures)} CI failures, "
                f"{len(beliefs)} beliefs, {len(strategies)} strategies.",
    )])
