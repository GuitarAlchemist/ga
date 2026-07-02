#!/usr/bin/env python3
"""Presence snapshot — Jarvis Track J1 tracer bullet.

One honest answer to "is the butler awake, and which limbs are asleep?".
Aggregates signals that already exist — it never re-polls what another
workflow already measures:

  - GitHub Actions run health for the delegation lanes + scheduled sensors
    (via `gh api`, or an injected fixture for offline tests)
  - sibling-repo reachability from state/algedonic/poller-state.json
    (written by ecosystem-health.yml)
  - recent algedonic inbox severity from state/algedonic/inbox.jsonl

Writes:
  - state/fleet/presence.json   (schema: presence-snapshot-v0.1.0)

The file is only rewritten when a limb's STATUS changes or the existing
snapshot is older than 24h (daily liveness proof) — so the committing
workflow stays quiet while everything is green.

Usage:
  python Scripts/presence-snapshot.py --root .                # live (needs gh)
  python Scripts/presence-snapshot.py --root . --runs-json f  # offline fixture
  python Scripts/presence-snapshot.py --self-test             # rule smoke test

Tracer bullet only: nothing consumes this file yet. It just exists and is
honest. BACKLOG.md § Jarvis Track J1.
"""

from __future__ import annotations

import argparse
import json
import subprocess
import sys
from datetime import datetime, timedelta, timezone
from pathlib import Path
from typing import Any

SCHEMA = "presence-snapshot-v0.1.0"
REPO = "GuitarAlchemist/ga"

# Lanes and sensors watched via their latest workflow run.
# cadence_minutes: expected schedule; None = event-driven (failure check only,
# no staleness check — an event lane that hasn't fired recently is not asleep,
# it just has nothing to do).
LANES: list[dict[str, Any]] = [
    {"file": "jules-auto-delegate.yml", "id": "lane:jules-router", "kind": "delegation-lane", "cadence_minutes": 1560},
    {"file": "claude.yml", "id": "lane:claude-mention", "kind": "delegation-lane", "cadence_minutes": None},
    {"file": "claude-code-review.yml", "id": "lane:claude-pr-review", "kind": "review-lane", "cadence_minutes": None},
    {"file": "post-merge-smoke.yml", "id": "sensor:post-merge-smoke", "kind": "sensor", "cadence_minutes": None},
    # Scheduled for */15 but GitHub throttles busy-repo crons to ~2-3h in
    # practice (measured 2026-07-02) — threshold on observed cadence, not the
    # cron expression, or every snapshot is a false yellow.
    {"file": "ecosystem-health.yml", "id": "sensor:ecosystem-health", "kind": "sensor", "cadence_minutes": 240},
    {"file": "quality-snapshot.yml", "id": "sensor:quality-snapshot", "kind": "sensor", "cadence_minutes": 1560},
    {"file": "fleet-status.yml", "id": "sensor:fleet-status", "kind": "sensor", "cadence_minutes": 1560},
]

SEVERITY_RANK = {"green": 0, "unknown": 1, "yellow": 2, "red": 3}


def utc_now() -> datetime:
    return datetime.now(timezone.utc)


def parse_ts(value: str) -> datetime | None:
    try:
        return datetime.fromisoformat(value.replace("Z", "+00:00"))
    except (ValueError, AttributeError):
        return None


# ---------------------------------------------------------------------------
# Workflow-run limbs
# ---------------------------------------------------------------------------


def fetch_runs_live(workflow_file: str) -> list[dict[str, Any]]:
    """Latest runs for one workflow via `gh api`. Empty list on any failure."""
    try:
        result = subprocess.run(
            [
                "gh", "api",
                f"repos/{REPO}/actions/workflows/{workflow_file}/runs?per_page=5",
                "--jq", ".workflow_runs",
            ],
            capture_output=True, text=True, timeout=30,
        )
        if result.returncode != 0:
            return []
        return json.loads(result.stdout)
    except (subprocess.TimeoutExpired, json.JSONDecodeError, OSError):
        return []


def lane_limb(lane: dict[str, Any], runs: list[dict[str, Any]], now: datetime) -> dict[str, Any]:
    """Classify one lane from its recent runs (newest first)."""
    limb = {
        "id": lane["id"],
        "kind": lane["kind"],
        "workflow": lane["file"],
        "status": "unknown",
        "last_heartbeat": None,
        "evidence": f"https://github.com/{REPO}/actions/workflows/{lane['file']}",
        "detail": "no runs found (workflow never fired, or API unreachable)",
    }
    if not runs:
        return limb

    newest = runs[0]
    limb["last_heartbeat"] = newest.get("created_at")
    limb["evidence"] = newest.get("html_url") or limb["evidence"]

    completed = next((r for r in runs if r.get("status") == "completed"), None)
    conclusion = (completed or {}).get("conclusion")

    if conclusion in ("failure", "timed_out", "startup_failure"):
        limb["status"] = "red"
        limb["detail"] = f"latest completed run: {conclusion}"
        limb["evidence"] = completed.get("html_url") or limb["evidence"]
        return limb

    cadence = lane["cadence_minutes"]
    heartbeat = parse_ts(newest.get("created_at", ""))
    if cadence is not None and heartbeat is not None:
        if now - heartbeat > timedelta(minutes=2 * cadence):
            limb["status"] = "yellow"
            limb["detail"] = (
                f"stale: last run {newest.get('created_at')}, "
                f"expected cadence ~{cadence} min"
            )
            return limb

    if conclusion == "cancelled":
        limb["status"] = "yellow"
        limb["detail"] = "latest completed run was cancelled"
    elif conclusion in ("success", "skipped", "neutral"):
        limb["status"] = "green"
        limb["detail"] = f"latest completed run: {conclusion}"
    elif conclusion is None and newest.get("status") in ("in_progress", "queued"):
        limb["status"] = "green"
        limb["detail"] = f"run currently {newest.get('status')}"
    else:
        limb["detail"] = f"unrecognized conclusion: {conclusion}"
    return limb


# ---------------------------------------------------------------------------
# Local-state limbs (reuse ecosystem-health's output, never re-poll)
# ---------------------------------------------------------------------------


def sibling_limbs(root: Path) -> list[dict[str, Any]]:
    state_file = root / "state" / "algedonic" / "poller-state.json"
    limbs: list[dict[str, Any]] = []
    try:
        state = json.loads(state_file.read_text(encoding="utf-8"))
    except (OSError, json.JSONDecodeError):
        return [{
            "id": "sibling:*", "kind": "sibling-repo", "status": "unknown",
            "last_heartbeat": None, "evidence": str(state_file),
            "detail": "poller-state.json missing or unreadable",
        }]
    for name, entry in sorted(state.items()):
        failures = int(entry.get("consecutive_failures", 0))
        status = "red" if failures >= 2 else "yellow" if failures == 1 else "green"
        limbs.append({
            "id": f"sibling:{name}",
            "kind": "sibling-repo",
            "status": status,
            "last_heartbeat": None,  # poller-state carries no timestamp; see sensor:ecosystem-health
            "evidence": f"https://github.com/GuitarAlchemist/{name}",
            "detail": (
                f"consecutive_failures={failures}, "
                f"blocked_prs={entry.get('blocked_prs', 'n/a')} "
                "(as of last ecosystem-health poll)"
            ),
        })
    return limbs


def algedonic_limb(root: Path, now: datetime) -> dict[str, Any]:
    inbox = root / "state" / "algedonic" / "inbox.jsonl"
    limb = {
        "id": "sensor:algedonic-inbox", "kind": "sensor", "status": "unknown",
        "last_heartbeat": None, "evidence": "state/algedonic/inbox.jsonl",
        "detail": "inbox missing or unreadable",
    }
    try:
        lines = inbox.read_text(encoding="utf-8").splitlines()
    except OSError:
        return limb

    window = now - timedelta(hours=24)
    counts = {"critical": 0, "fail": 0, "warn": 0}
    newest: datetime | None = None
    for line in lines:
        try:
            signal = json.loads(line)
        except json.JSONDecodeError:
            continue
        ts = parse_ts(signal.get("emitted_at", ""))
        if ts is None:
            continue
        if newest is None or ts > newest:
            newest = ts
        if ts >= window and signal.get("severity") in counts:
            counts[signal["severity"]] += 1

    limb["last_heartbeat"] = newest.isoformat().replace("+00:00", "Z") if newest else None
    if counts["critical"]:
        limb["status"] = "red"
    elif counts["fail"]:
        limb["status"] = "yellow"
    else:
        limb["status"] = "green"
    limb["detail"] = (
        f"last 24h: critical={counts['critical']} fail={counts['fail']} "
        f"warn={counts['warn']} (warn does not degrade presence)"
    )
    return limb


# ---------------------------------------------------------------------------
# Snapshot assembly + quiet-write
# ---------------------------------------------------------------------------


def build_snapshot(root: Path, runs_by_file: dict[str, list[dict[str, Any]]], now: datetime) -> dict[str, Any]:
    limbs = [lane_limb(lane, runs_by_file.get(lane["file"], []), now) for lane in LANES]
    limbs += sibling_limbs(root)
    limbs.append(algedonic_limb(root, now))

    worst = max(limbs, key=lambda l: SEVERITY_RANK[l["status"]])
    overall = worst["status"] if worst["status"] != "unknown" else "yellow"
    return {
        "schema": SCHEMA,
        "generated_at": now.isoformat().replace("+00:00", "Z"),
        "repo": REPO,
        "overall": overall,
        "limbs": limbs,
    }


def signature(snapshot: dict[str, Any]) -> list[list[str]]:
    return sorted([l["id"], l["status"]] for l in snapshot["limbs"])


def write_if_meaningful(snapshot: dict[str, Any], out_path: Path, now: datetime) -> bool:
    """Write only on status change or >24h-old snapshot. Returns True if written."""
    if out_path.exists():
        try:
            previous = json.loads(out_path.read_text(encoding="utf-8"))
            prev_ts = parse_ts(previous.get("generated_at", ""))
            if (
                signature(previous) == signature(snapshot)
                and prev_ts is not None
                and now - prev_ts < timedelta(hours=24)
            ):
                return False
        except (OSError, json.JSONDecodeError):
            pass  # unreadable previous snapshot -> rewrite
    out_path.parent.mkdir(parents=True, exist_ok=True)
    out_path.write_text(json.dumps(snapshot, indent=2) + "\n", encoding="utf-8")
    return True


# ---------------------------------------------------------------------------
# Self-test (offline, no filesystem/network)
# ---------------------------------------------------------------------------


def self_test() -> int:
    now = parse_ts("2026-07-02T12:00:00Z")
    assert now is not None
    lane = {"file": "x.yml", "id": "lane:x", "kind": "sensor", "cadence_minutes": 60}

    ok = lane_limb(lane, [{"status": "completed", "conclusion": "success",
                           "created_at": "2026-07-02T11:30:00Z", "html_url": "u"}], now)
    assert ok["status"] == "green", ok

    failed = lane_limb(lane, [{"status": "completed", "conclusion": "failure",
                               "created_at": "2026-07-02T11:30:00Z", "html_url": "u"}], now)
    assert failed["status"] == "red", failed

    stale = lane_limb(lane, [{"status": "completed", "conclusion": "success",
                              "created_at": "2026-07-01T09:00:00Z", "html_url": "u"}], now)
    assert stale["status"] == "yellow", stale

    event_lane = {"file": "x.yml", "id": "lane:x", "kind": "sensor", "cadence_minutes": None}
    quiet = lane_limb(event_lane, [{"status": "completed", "conclusion": "success",
                                    "created_at": "2026-06-01T00:00:00Z", "html_url": "u"}], now)
    assert quiet["status"] == "green", quiet  # event-driven lanes never go stale

    empty = lane_limb(lane, [], now)
    assert empty["status"] == "unknown", empty

    snap = {"limbs": [{"id": "a", "status": "green"}, {"id": "b", "status": "unknown"}]}
    worst = max(snap["limbs"], key=lambda l: SEVERITY_RANK[l["status"]])
    assert worst["status"] == "unknown" and SEVERITY_RANK["red"] > SEVERITY_RANK["yellow"]

    print("self-test: OK")
    return 0


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--root", default=".", help="repo root")
    parser.add_argument("--runs-json", help="fixture: JSON object {workflow_file: [runs...]} instead of gh api")
    parser.add_argument("--out", default=None, help="output path (default: <root>/state/fleet/presence.json)")
    parser.add_argument("--self-test", action="store_true")
    args = parser.parse_args()

    if args.self_test:
        return self_test()

    root = Path(args.root).resolve()
    out_path = Path(args.out) if args.out else root / "state" / "fleet" / "presence.json"
    now = utc_now()

    if args.runs_json:
        runs_by_file = json.loads(Path(args.runs_json).read_text(encoding="utf-8"))
    else:
        runs_by_file = {lane["file"]: fetch_runs_live(lane["file"]) for lane in LANES}

    snapshot = build_snapshot(root, runs_by_file, now)
    written = write_if_meaningful(snapshot, out_path, now)

    print(f"overall={snapshot['overall']} changed={str(written).lower()}")
    for limb in snapshot["limbs"]:
        print(f"  {limb['status']:>7}  {limb['id']}: {limb['detail']}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
