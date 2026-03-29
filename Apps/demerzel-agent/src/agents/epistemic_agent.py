"""Demerzel Epistemic Agent — SHOW, METHYLATE, DEMETHYLATE, AMNESIA, BROADCAST."""

from __future__ import annotations

import json
from collections.abc import AsyncGenerator
from datetime import datetime, timezone, timedelta

from acp_sdk.models import Message, MessagePart
from acp_sdk.server import RunYield, RunYieldResume

from ..services import governance


async def handle(input: list[Message]) -> AsyncGenerator[RunYield, RunYieldResume]:
    """Process epistemic commands (Articles E-0 through E-9).

    Commands:
      - "show beliefs [where field op value]"
      - "show strategies"
      - "show tensor"
      - "methylate <strategy_id> [reason ...]"
      - "demethylate <strategy_id>"
      - "amnesia <belief_name> [in N days]"
      - "broadcast beliefs [where ...]"
    """
    text = ""
    for msg in input:
        for part in msg.parts:
            if hasattr(part, "content") and isinstance(part.content, str):
                text += part.content + " "
    text = text.strip()
    cmd = text.lower()

    # SHOW BELIEFS
    if cmd.startswith("show beliefs"):
        beliefs = governance.list_beliefs()
        # Simple WHERE filtering
        where_part = cmd.replace("show beliefs", "").strip()
        if where_part.startswith("where "):
            where_part = where_part[6:].strip()
            # Parse "field op value" — simple single predicate
            for op in [">=", "<=", "!=", ">", "<", "="]:
                if op in where_part:
                    field, value = where_part.split(op, 1)
                    field = field.strip()
                    value = value.strip().strip("'\"")
                    filtered = []
                    for b in beliefs:
                        actual = b.get(field)
                        if actual is None:
                            continue
                        try:
                            if op == "=" and str(actual) == value:
                                filtered.append(b)
                            elif op == ">" and float(actual) > float(value):
                                filtered.append(b)
                            elif op == "<" and float(actual) < float(value):
                                filtered.append(b)
                            elif op == ">=" and float(actual) >= float(value):
                                filtered.append(b)
                            elif op == "<=" and float(actual) <= float(value):
                                filtered.append(b)
                            elif op == "!=" and str(actual) != value:
                                filtered.append(b)
                        except (ValueError, TypeError):
                            continue
                    beliefs = filtered
                    break

        yield Message(parts=[MessagePart(
            content=json.dumps(beliefs, indent=2, default=str),
            content_type="application/json",
        )])

    # SHOW STRATEGIES
    elif cmd.startswith("show strategies"):
        strategies = governance.load_strategies()
        yield Message(parts=[MessagePart(
            content=json.dumps(strategies, indent=2, default=str),
            content_type="application/json",
        )])

    # SHOW TENSOR
    elif cmd.startswith("show tensor"):
        beliefs = governance.list_beliefs()
        tensor_summary = {}
        for b in beliefs:
            config = b.get("tensorConfig", "U_U")
            tensor_summary[config] = tensor_summary.get(config, 0) + 1
        yield Message(parts=[MessagePart(
            content=json.dumps({
                "total_beliefs": len(beliefs),
                "tensor_distribution": tensor_summary,
                "wisdom_count": tensor_summary.get("C_T", 0),
                "hunch_count": tensor_summary.get("T_C", 0),
                "blindspot_count": tensor_summary.get("U_F", 0),
            }, indent=2),
            content_type="application/json",
        )])

    # METHYLATE
    elif cmd.startswith("methylate "):
        parts_list = text[len("methylate "):].strip().split(" ", 1)
        strategy_id = parts_list[0]
        reason = parts_list[1] if len(parts_list) > 1 else None

        entry = {
            "id": f"methylation-{strategy_id}-{datetime.now(timezone.utc).strftime('%Y%m%d')}",
            "type": "methylation",
            "strategy_id": strategy_id,
            "reason": reason,
            "timestamp": datetime.now(timezone.utc).isoformat(),
            "article": "E-8",
        }
        governance.save_learning_entry(entry)
        yield Message(parts=[MessagePart(
            content=f"Strategy '{strategy_id}' methylated (Article E-8). Reason: {reason or 'not specified'}",
        )])

    # DEMETHYLATE
    elif cmd.startswith("demethylate "):
        strategy_id = text[len("demethylate "):].strip()
        entry = {
            "id": f"demethylation-{strategy_id}-{datetime.now(timezone.utc).strftime('%Y%m%d')}",
            "type": "demethylation",
            "strategy_id": strategy_id,
            "timestamp": datetime.now(timezone.utc).isoformat(),
            "article": "E-8",
        }
        governance.save_learning_entry(entry)
        yield Message(parts=[MessagePart(
            content=f"Strategy '{strategy_id}' demethylated — restored to full activation.",
        )])

    # AMNESIA
    elif cmd.startswith("amnesia "):
        remainder = text[len("amnesia "):].strip()
        days = 7
        belief_name = remainder
        if " in " in remainder.lower():
            idx = remainder.lower().index(" in ")
            belief_name = remainder[:idx].strip()
            days_str = remainder[idx + 4:].strip().split()[0]
            try:
                days = int(days_str)
            except ValueError:
                pass

        scheduled_for = (datetime.now(timezone.utc) + timedelta(days=days)).isoformat()
        entry = {
            "id": f"amnesia-{belief_name}-{datetime.now(timezone.utc).strftime('%Y%m%d')}",
            "type": "amnesia_scheduled",
            "belief_id": belief_name,
            "scheduled_for": scheduled_for,
            "days": days,
            "timestamp": datetime.now(timezone.utc).isoformat(),
            "article": "E-5",
        }
        governance.save_learning_entry(entry)
        yield Message(parts=[MessagePart(
            content=f"Belief '{belief_name}' scheduled for amnesia in {days} days (Article E-5). Re-derivation test on {scheduled_for}.",
        )])

    # BROADCAST
    elif cmd.startswith("broadcast"):
        beliefs = governance.list_beliefs()
        yield Message(parts=[MessagePart(
            content=f"Broadcasting {len(beliefs)} belief(s) for federated peer review (Article E-9). "
                    f"In production, this sends to connected agents via Galactic Protocol.",
        )])
        yield Message(parts=[MessagePart(
            content=json.dumps(beliefs[:10], indent=2, default=str),
            content_type="application/json",
        )])

    else:
        yield Message(parts=[MessagePart(
            content=(
                "Epistemic commands (Articles E-0 to E-9):\n"
                "  show beliefs [where field op value]\n"
                "  show strategies\n"
                "  show tensor\n"
                "  methylate <strategy_id> [reason ...]\n"
                "  demethylate <strategy_id>\n"
                "  amnesia <belief_name> [in N days]\n"
                "  broadcast beliefs"
            ),
        )])
