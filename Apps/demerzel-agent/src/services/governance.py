"""Governance state reader — loads beliefs, policies, strategies from the Demerzel submodule."""

from __future__ import annotations

import json
import os
from pathlib import Path
from typing import Any

import yaml


def find_governance_root() -> Path:
    """Walk up from this file to find governance/demerzel/."""
    candidate = Path(__file__).resolve()
    for _ in range(10):
        candidate = candidate.parent
        gov = candidate / "governance" / "demerzel"
        if gov.is_dir():
            return gov
    # Fallback: environment variable
    env = os.environ.get("DEMERZEL_ROOT")
    if env:
        return Path(env)
    raise FileNotFoundError("Could not locate governance/demerzel/ directory")


GOV_ROOT = find_governance_root()


def load_json(path: Path) -> Any:
    with open(path, encoding="utf-8") as f:
        return json.load(f)


def load_yaml(path: Path) -> Any:
    with open(path, encoding="utf-8") as f:
        return yaml.safe_load(f)


# ---------------------------------------------------------------------------
# Beliefs
# ---------------------------------------------------------------------------

def list_beliefs() -> list[dict[str, Any]]:
    """Load all .belief.json files from state/beliefs/."""
    beliefs_dir = GOV_ROOT / "state" / "beliefs"
    if not beliefs_dir.exists():
        return []
    results = []
    for f in sorted(beliefs_dir.glob("*.belief.json")):
        try:
            results.append(load_json(f))
        except Exception:
            continue
    return results


def get_belief(name: str) -> dict[str, Any] | None:
    """Load a specific belief by filename stem."""
    path = GOV_ROOT / "state" / "beliefs" / f"{name}.belief.json"
    if path.exists():
        return load_json(path)
    return None


# ---------------------------------------------------------------------------
# Policies
# ---------------------------------------------------------------------------

def list_policies() -> list[dict[str, Any]]:
    """Load all policy YAML files."""
    policies_dir = GOV_ROOT / "policies"
    if not policies_dir.exists():
        return []
    results = []
    for f in sorted(policies_dir.glob("*.yaml")):
        try:
            data = load_yaml(f)
            if isinstance(data, dict):
                data["_filename"] = f.name
                results.append(data)
        except Exception:
            continue
    return results


# ---------------------------------------------------------------------------
# Strategies
# ---------------------------------------------------------------------------

def load_strategies() -> list[dict[str, Any]]:
    """Load the strategy repertoire."""
    path = GOV_ROOT / "state" / "strategies" / "initial-repertoire.json"
    if not path.exists():
        return []
    data = load_json(path)
    if isinstance(data, list):
        return data
    if isinstance(data, dict) and "strategies" in data:
        return data["strategies"]
    return []


# ---------------------------------------------------------------------------
# Constitutions
# ---------------------------------------------------------------------------

def load_constitution(name: str = "epistemic") -> str:
    """Load a constitution as markdown text."""
    path = GOV_ROOT / "constitutions" / f"{name}.constitution.md"
    if path.exists():
        return path.read_text(encoding="utf-8")
    return f"Constitution '{name}' not found."


# ---------------------------------------------------------------------------
# Learning journal
# ---------------------------------------------------------------------------

def list_learning_entries() -> list[dict[str, Any]]:
    """Load learning journal entries."""
    learning_dir = GOV_ROOT / "state" / "learning"
    if not learning_dir.exists():
        return []
    results = []
    for f in sorted(learning_dir.glob("*.learning.json")):
        try:
            results.append(load_json(f))
        except Exception:
            continue
    return results


def save_learning_entry(entry: dict[str, Any]) -> Path:
    """Save a learning journal entry."""
    from datetime import datetime, timezone
    learning_dir = GOV_ROOT / "state" / "learning"
    learning_dir.mkdir(parents=True, exist_ok=True)
    slug = entry.get("id", datetime.now(timezone.utc).strftime("%Y%m%d-%H%M%S"))
    path = learning_dir / f"{slug}.learning.json"
    with open(path, "w", encoding="utf-8") as f:
        json.dump(entry, f, indent=2)
    return path
