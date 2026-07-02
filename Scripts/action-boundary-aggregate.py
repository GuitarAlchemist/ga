#!/usr/bin/env python3
"""Action-boundary aggregator — Jarvis Track J3 tracer bullet.

Generates state/governance/action-boundary.json — the single machine-readable
statement of what an autonomous actor in this repo may touch, what halts it,
and which cost lane it runs on — from the fragments that already govern those
things. GENERATED, never hand-edited: the fragments stay canonical, so there
is no duplicate to drift.

Sources aggregated:
  - agent-blackbox.policy.json      (risk thresholds, blocked / one-way-door paths)
  - ga.loop-policy.json             (supervised-loop allow_edit / protected_paths)
  - Scripts/Governance.psm1         (halt marker paths — string-verified, see below)
  - cost doctrine                   (docs/solutions/tooling/2026-07-02-afk-delegation-chain-failures.md)
  - CLAUDE.md one-way-door list     (encoded here; Karpathy rule 6)

Drift guard: the halt-marker paths are asserted to still appear verbatim in
Governance.psm1 — if governance semantics move, this script fails loudly
instead of emitting a stale boundary.

Usage:
  python Scripts/action-boundary-aggregate.py --root .           # (re)generate
  python Scripts/action-boundary-aggregate.py --root . --check   # CI drift gate:
      exit 1 if the committed instance differs from a fresh regeneration
      (ignoring generated_at) or fails structural validation.

Contract: docs/contracts/2026-07-02-action-boundary.contract.md (v0.1 DRAFT).
Consumers: supervised-loop preflight (Step 2). Enforcement semantics are
unchanged — this file is a projection of the fragments, not a new authority.
"""

from __future__ import annotations

import argparse
import json
import sys
from datetime import datetime, timezone
from pathlib import Path
from typing import Any

SCHEMA = "action-boundary-v0.1.0"
OUT_REL = "state/governance/action-boundary.json"

# Halt semantics: one definition in Scripts/Governance.psm1 (fail-closed).
# These strings must appear verbatim there; see drift guard below.
HALT_MARKERS = {
    "halt_all_marker": "$HOME/.demerzel/HALT-ALL",
    "halt_all_source_string": ".demerzel/HALT-ALL",
    "local_killswitch": "state/.loop-halted",
    "stop_marker": ".STOP",
}


def load_json(path: Path) -> dict[str, Any]:
    return json.loads(path.read_text(encoding="utf-8"))


def build(root: Path) -> dict[str, Any]:
    risk_policy = load_json(root / "agent-blackbox.policy.json")
    loop_policy = load_json(root / "ga.loop-policy.json")

    governance = (root / "Scripts" / "Governance.psm1").read_text(encoding="utf-8")
    for key in ("halt_all_source_string", "local_killswitch"):
        if HALT_MARKERS[key] not in governance:
            raise SystemExit(
                f"drift: '{HALT_MARKERS[key]}' no longer appears in Scripts/Governance.psm1 — "
                "halt semantics moved; update HALT_MARKERS and the contract together."
            )

    return {
        "schema": SCHEMA,
        "generated_at": datetime.now(timezone.utc).isoformat().replace("+00:00", "Z"),
        "generated_by": "Scripts/action-boundary-aggregate.py",
        "repo": "GuitarAlchemist/ga",
        "sources": {
            "risk_policy": "agent-blackbox.policy.json",
            "loop_policy": "ga.loop-policy.json",
            "halt_semantics": "Scripts/Governance.psm1",
            "cost_doctrine": "docs/solutions/tooling/2026-07-02-afk-delegation-chain-failures.md",
        },
        "capabilities": {
            "allow_edit": loop_policy["allow_edit"],
            "protected_paths": loop_policy["protected_paths"],
            "blocked_paths": risk_policy["blocked_paths"],
            "one_way_door_paths": risk_policy["one_way_door_paths"],
        },
        "risk_thresholds": risk_policy["risk_thresholds"],
        "required_evidence": risk_policy["required_evidence"],
        "halt": {
            "halt_all_marker": HALT_MARKERS["halt_all_marker"],
            "local_killswitch": HALT_MARKERS["local_killswitch"],
            "stop_marker": HALT_MARKERS["stop_marker"],
            "semantics": "fail-closed: an unreadable or unknown-schema marker means halted (Scripts/Governance.psm1 is the single gate implementation)",
        },
        "cost_lanes": {
            "subscription_only": ["scheduled", "per-pr-review"],
            "api_fallback_allowed": ["mention-triggered"],
            "rule": "API-key fallback ONLY on human-initiated, bounded lanes; never wire pay-per-use into anything that fires per-PR or on a schedule",
        },
        "one_way_doors": [
            "OPTIC-K embedding dimensions (coordinated re-index required)",
            "docs/contracts/** schema locked fields (cross-repo coordination required)",
            "public APIs",
            "pricing / anything metered",
        ],
        "audit": {
            "expectation": "every autonomous action is attributable: actor, capability invoked, boundary version, verdict",
            "status": "aspirational-v0.1 — logging surface not yet unified",
        },
    }


def validate(instance: dict[str, Any]) -> list[str]:
    """Structural validation mirroring docs/contracts/action-boundary.schema.json."""
    errors: list[str] = []

    def need(path: str, value: Any, kind: type) -> None:
        if not isinstance(value, kind):
            errors.append(f"{path}: expected {kind.__name__}, got {type(value).__name__}")

    need("schema", instance.get("schema"), str)
    if instance.get("schema") != SCHEMA:
        errors.append(f"schema: expected {SCHEMA}")
    need("generated_at", instance.get("generated_at"), str)
    need("sources", instance.get("sources"), dict)

    caps = instance.get("capabilities")
    need("capabilities", caps, dict)
    if isinstance(caps, dict):
        for key in ("allow_edit", "protected_paths", "blocked_paths", "one_way_door_paths"):
            value = caps.get(key)
            need(f"capabilities.{key}", value, list)
            if isinstance(value, list) and not all(isinstance(x, str) and x for x in value):
                errors.append(f"capabilities.{key}: entries must be non-empty strings")

    halt = instance.get("halt")
    need("halt", halt, dict)
    if isinstance(halt, dict):
        for key in ("halt_all_marker", "local_killswitch", "stop_marker", "semantics"):
            need(f"halt.{key}", halt.get(key), str)

    lanes = instance.get("cost_lanes")
    need("cost_lanes", lanes, dict)
    if isinstance(lanes, dict):
        for key in ("subscription_only", "api_fallback_allowed"):
            need(f"cost_lanes.{key}", lanes.get(key), list)
        need("cost_lanes.rule", lanes.get("rule"), str)

    need("one_way_doors", instance.get("one_way_doors"), list)
    return errors


def normalized(instance: dict[str, Any]) -> dict[str, Any]:
    return {k: v for k, v in instance.items() if k != "generated_at"}


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--root", default=".")
    parser.add_argument("--check", action="store_true", help="fail on drift or invalid committed instance")
    args = parser.parse_args()

    root = Path(args.root).resolve()
    out_path = root / OUT_REL

    fresh = build(root)
    problems = validate(fresh)
    if problems:
        for p in problems:
            print(f"invalid: {p}", file=sys.stderr)
        return 1

    if args.check:
        if not out_path.exists():
            print(f"check: {OUT_REL} missing — run without --check to generate.", file=sys.stderr)
            return 1
        committed = load_json(out_path)
        committed_problems = validate(committed)
        if committed_problems:
            for p in committed_problems:
                print(f"committed instance invalid: {p}", file=sys.stderr)
            return 1
        if normalized(committed) != normalized(fresh):
            print(
                "check: drift — the committed action-boundary no longer matches its sources.\n"
                "Regenerate: python Scripts/action-boundary-aggregate.py --root .",
                file=sys.stderr,
            )
            return 1
        print("check: OK (boundary matches sources)")
        return 0

    out_path.parent.mkdir(parents=True, exist_ok=True)
    out_path.write_text(json.dumps(fresh, indent=2) + "\n", encoding="utf-8")
    print(f"wrote {OUT_REL} ({SCHEMA})")
    return 0


if __name__ == "__main__":
    sys.exit(main())
