#!/usr/bin/env python3
"""Build a read-only snapshot of unfinished local Git work.

The snapshot is a GA adapter over Agent Blackbox's canonical path matcher and
AFK contract names. It discovers registered worktrees, then binds optional
GitHub PR, live-presence, and historical Claude Code provenance. It never runs
a mutating Git command and refuses to write its output inside a discovered
worktree.
"""

from __future__ import annotations

import argparse
import hashlib
import importlib.util
import json
import os
import subprocess
import sys
from datetime import datetime, timedelta, timezone
from pathlib import Path
from typing import Any, Protocol


SNAPSHOT_CONTRACT = "ga-local-reconciliation-snapshot-v0.1"
AFK_TASK_CONTRACT = "afk-task-state-v0.2"
AFK_EVIDENCE_CONTRACT = "afk-evidence-manifest-v0.2"


class Matcher(Protocol):
    @classmethod
    def matches_any(cls, path: str, patterns: list[str]) -> bool: ...


def utc_now() -> str:
    return datetime.now(timezone.utc).isoformat().replace("+00:00", "Z")


def canonical_path(value: str | Path) -> str:
    return str(Path(value).expanduser().resolve(strict=False))


def _git(repository: Path, *args: str, check: bool = True) -> subprocess.CompletedProcess[bytes]:
    environment = os.environ.copy()
    environment["GIT_OPTIONAL_LOCKS"] = "0"
    return subprocess.run(
        ["git", "-c", "core.quotepath=false", *args],
        cwd=repository,
        check=check,
        capture_output=True,
        env=environment,
    )


def _packet_id(repository_id: str, worktree: str) -> str:
    identity = f"{repository_id}\0{os.path.normcase(worktree)}".encode("utf-8")
    return f"local-{hashlib.sha256(identity).hexdigest()[:24]}"


def _parse_worktree_records(raw: bytes) -> list[dict[str, Any]]:
    records: list[dict[str, Any]] = []
    current: dict[str, Any] = {}
    for token_bytes in raw.split(b"\0"):
        if not token_bytes:
            if current:
                records.append(current)
                current = {}
            continue
        token = token_bytes.decode("utf-8", errors="surrogateescape")
        key, _, value = token.partition(" ")
        if key in {"detached", "bare", "locked", "prunable"} and not value:
            current[key] = True
        else:
            current[key] = value
    if current:
        records.append(current)
    return records


def _dirty_entries(worktree: Path, matcher: Matcher, path_classes: dict[str, list[str]]) -> list[dict[str, str]]:
    raw = _git(worktree, "status", "--porcelain=v1", "-z", "--untracked-files=all").stdout
    tokens = [item.decode("utf-8", errors="surrogateescape") for item in raw.split(b"\0") if item]
    entries: list[dict[str, str]] = []
    index = 0
    while index < len(tokens):
        token = tokens[index]
        index += 1
        if len(token) < 4:
            continue
        status = token[:2]
        paths = [token[3:]]
        if "R" in status or "C" in status:
            if index < len(tokens):
                paths.append(tokens[index])
                index += 1
        for raw_path in paths:
            path = raw_path.replace("\\", "/")
            classification = "unknown"
            for candidate in ("state", "generated", "source"):
                patterns = path_classes.get(candidate, [])
                if patterns and matcher.matches_any(path, patterns):
                    classification = candidate
                    break
            entries.append({"status": status, "path": path, "classification": classification})
    return sorted(entries, key=lambda item: (item["path"], item["status"]))


def _dirty_digest(entries: list[dict[str, str]]) -> str:
    canonical = json.dumps(entries, separators=(",", ":"), sort_keys=True).encode("utf-8")
    return hashlib.sha256(canonical).hexdigest()


def _upstream_state(worktree: Path) -> tuple[str | None, int | None, int | None]:
    upstream_result = _git(
        worktree,
        "rev-parse",
        "--abbrev-ref",
        "--symbolic-full-name",
        "@{upstream}",
        check=False,
    )
    if upstream_result.returncode:
        return None, None, None
    upstream = upstream_result.stdout.decode("utf-8").strip()
    counts = _git(worktree, "rev-list", "--left-right", "--count", f"HEAD...{upstream}")
    left, right = counts.stdout.decode("ascii").strip().split()
    return upstream, int(left), int(right)


def _normalise_presence(presence: dict[str, Any] | None) -> list[dict[str, Any]]:
    if not presence or not isinstance(presence.get("sessions"), list):
        return []
    sessions: list[dict[str, Any]] = []
    for item in presence["sessions"]:
        if not isinstance(item, dict) or item.get("state") != "live" or not item.get("worktree"):
            continue
        sessions.append({**item, "worktree": canonical_path(item["worktree"])})
    return sessions


def _owner_for(
    worktree: str, branch: str | None, live_sessions: list[dict[str, Any]]
) -> dict[str, Any]:
    owners = [
        session
        for session in live_sessions
        if os.path.normcase(session["worktree"]) == os.path.normcase(worktree)
        and (not session.get("branch") or session.get("branch") == branch)
    ]
    if not owners:
        return {"state": "unowned"}
    if len(owners) > 1:
        return {
            "state": "contested",
            "sessions": [item.get("session_id") for item in owners],
        }
    owner = owners[0]
    return {
        "state": "live",
        "session_id": owner.get("session_id"),
        "actor": owner.get("actor", "unknown"),
    }


def _provenance_for(
    worktree: str, branch: str | None, historical_sessions: list[dict[str, Any]]
) -> list[dict[str, Any]]:
    matches = []
    for session in historical_sessions:
        if not session.get("worktree"):
            continue
        if os.path.normcase(canonical_path(session["worktree"])) != os.path.normcase(worktree):
            continue
        if session.get("branch") and branch and session["branch"] != branch:
            continue
        matches.append(session)
    return sorted(matches, key=lambda item: item.get("last_seen", ""), reverse=True)


def _pr_for(repository_id: str, branch: str | None, head: str | None, prs: dict[str, Any]) -> dict[str, Any] | None:
    if branch is None:
        return None
    candidates = prs.get(repository_id, []) if isinstance(prs, dict) else []
    for pr in candidates:
        if isinstance(pr, dict) and pr.get("headRefName") == branch:
            return {
                "number": pr.get("number"),
                "url": pr.get("url"),
                "is_draft": bool(pr.get("isDraft")),
                "head_sha": pr.get("headRefOid"),
                "head_matches": not pr.get("headRefOid") or pr.get("headRefOid") == head,
            }
    return None


def _classification(
    *,
    contract_status: str,
    missing: bool,
    git_unavailable: bool,
    detached: bool,
    owner: dict[str, Any],
    dirty: bool,
    ahead: int | None,
    behind: int | None,
    pr: dict[str, Any] | None,
) -> tuple[str, str]:
    if contract_status != "pinned":
        return "blocked", "repair the Agent Blackbox revision pin before acting"
    if missing:
        return "blocked", "inspect the missing worktree registration"
    if git_unavailable:
        return "blocked", "repair or prune the invalid worktree registration"
    if owner.get("state") == "contested":
        return "blocked", "resolve contested live ownership"
    if owner.get("state") == "live":
        return "active", "wait for or hand off from the live owner"
    if detached and dirty:
        return "quarantined", "bind detached dirty work to an issue before recovery"
    if dirty:
        return "active", "inspect dirty provenance and choose one bounded slice"
    if behind and behind > 0:
        return "blocked", "decide update strategy in a new isolated worktree"
    if ahead and ahead > 0:
        return "ready", "verify the local commits and publish or archive the slice"
    if pr:
        return "ready", "reconcile exact PR head and CI evidence"
    return "archive", "archive the clean unbound lane if no owner needs it"


def _task_binding(repository: dict[str, Any]) -> dict[str, Any]:
    issue = repository.get("issue")
    if not isinstance(issue, int) or issue < 1:
        return {"status": "unbound"}
    return {
        "status": "bound",
        "canonical": {
            "schema_version": 2,
            "contract": AFK_TASK_CONTRACT,
            "task": {"repository": repository["id"], "issue": issue},
            "attempts": [],
        },
    }


def _missing_packet(repository: dict[str, Any], contract_status: str) -> dict[str, Any]:
    worktree = canonical_path(repository["path"])
    classification, next_action = _classification(
        contract_status=contract_status,
        missing=True,
        git_unavailable=False,
        detached=False,
        owner={"state": "unowned"},
        dirty=False,
        ahead=None,
        behind=None,
        pr=None,
    )
    return {
        "packet_id": _packet_id(repository["id"], worktree),
        "repo": repository["id"],
        "worktree": worktree,
        "missing": True,
        "git_unavailable": False,
        "branch": None,
        "upstream": None,
        "head_sha": None,
        "detached": False,
        "ahead": None,
        "behind": None,
        "dirty_paths": [],
        "dirty_digest": _dirty_digest([]),
        "source_signature": "missing",
        "pr": None,
        "owner": {"state": "unowned"},
        "provenance": [],
        "task_binding": _task_binding(repository),
        "classification": classification,
        "next_action": next_action,
    }


def discover_repository(
    repository: dict[str, Any],
    *,
    matcher: Matcher,
    contract_status: str,
    path_classes: dict[str, list[str]],
    live_sessions: list[dict[str, Any]],
    prs: dict[str, Any],
    historical_sessions: list[dict[str, Any]],
) -> list[dict[str, Any]]:
    root = Path(repository["path"]).expanduser().resolve(strict=False)
    if not root.is_dir():
        return [_missing_packet(repository, contract_status)]
    result = _git(root, "worktree", "list", "--porcelain", "-z", check=False)
    if result.returncode:
        return [_missing_packet(repository, contract_status)]

    packets: list[dict[str, Any]] = []
    for record in _parse_worktree_records(result.stdout):
        worktree = canonical_path(record["worktree"])
        path = Path(worktree)
        missing = not path.is_dir()
        git_unavailable = False
        branch_ref = record.get("branch")
        branch = branch_ref.removeprefix("refs/heads/") if branch_ref else None
        detached = bool(record.get("detached")) or branch is None
        head = record.get("HEAD")
        dirty_paths: list[dict[str, str]] = []
        upstream: str | None = None
        ahead: int | None = None
        behind: int | None = None
        if not missing:
            head_result = _git(path, "rev-parse", "HEAD", check=False)
            if head_result.returncode:
                git_unavailable = True
            else:
                head = head_result.stdout.decode("ascii").strip()
                dirty_paths = _dirty_entries(path, matcher, path_classes)
                upstream, ahead, behind = _upstream_state(path)
        owner = _owner_for(worktree, branch, live_sessions)
        pr = _pr_for(repository["id"], branch, head, prs)
        classification, next_action = _classification(
            contract_status=contract_status,
            missing=missing,
            git_unavailable=git_unavailable,
            detached=detached,
            owner=owner,
            dirty=bool(dirty_paths),
            ahead=ahead,
            behind=behind,
            pr=pr,
        )
        dirty_digest = _dirty_digest(dirty_paths)
        packets.append(
            {
                "packet_id": _packet_id(repository["id"], worktree),
                "repo": repository["id"],
                "worktree": worktree,
                "missing": missing,
                "git_unavailable": git_unavailable,
                "branch": branch,
                "upstream": upstream,
                "head_sha": head,
                "detached": detached,
                "ahead": ahead,
                "behind": behind,
                "dirty_paths": dirty_paths,
                "dirty_digest": dirty_digest,
                "source_signature": (
                    f"unavailable:{head or 'unknown'}" if git_unavailable else f"{head or 'missing'}:{dirty_digest}"
                ),
                "pr": pr,
                "owner": owner,
                "provenance": _provenance_for(worktree, branch, historical_sessions),
                "task_binding": _task_binding(repository),
                "classification": classification,
                "next_action": next_action,
            }
        )
    return packets or [_missing_packet(repository, contract_status)]


def build_snapshot(
    config: dict[str, Any],
    *,
    matcher: Matcher,
    contract_source: dict[str, Any],
    presence: dict[str, Any] | None = None,
    prs: dict[str, Any] | None = None,
    historical_sessions: list[dict[str, Any]] | None = None,
) -> dict[str, Any]:
    if config.get("schema_version") != 1 or not isinstance(config.get("repositories"), list):
        raise ValueError("config must have schema_version 1 and a repositories array")
    live_sessions = _normalise_presence(presence)
    path_classes = config.get("path_classes") or {}
    packets: list[dict[str, Any]] = []
    for repository in config["repositories"]:
        if not isinstance(repository, dict) or not repository.get("id") or not repository.get("path"):
            raise ValueError("each repository needs id and path")
        packets.extend(
            discover_repository(
                repository,
                matcher=matcher,
                contract_status=contract_source.get("status", "unavailable"),
                path_classes=path_classes,
                live_sessions=live_sessions,
                prs=prs or {},
                historical_sessions=historical_sessions or [],
            )
        )
    packets.sort(key=lambda packet: (packet["repo"].lower(), packet["worktree"].lower()))
    return {
        "schema_version": 1,
        "contract": SNAPSHOT_CONTRACT,
        "generated_at": utc_now(),
        "contract_source": contract_source,
        "canonical_contracts": {
            "task_state": AFK_TASK_CONTRACT,
            "evidence": AFK_EVIDENCE_CONTRACT,
        },
        "packet_count": len(packets),
        "packets": packets,
    }


def snapshot_is_stale(previous: dict[str, Any], current: dict[str, Any]) -> dict[str, Any]:
    def signatures(snapshot: dict[str, Any]) -> dict[str, str]:
        return {
            packet["packet_id"]: packet["source_signature"]
            for packet in snapshot.get("packets", [])
            if isinstance(packet, dict) and packet.get("packet_id")
        }

    before = signatures(previous)
    after = signatures(current)
    changed = sorted(
        packet_id for packet_id in set(before) | set(after) if before.get(packet_id) != after.get(packet_id)
    )
    return {"stale": bool(changed), "changed_packet_ids": changed}


def read_claude_provenance(root: Path, days: int = 30) -> list[dict[str, Any]]:
    if not root.is_dir():
        return []
    cutoff = datetime.now(timezone.utc) - timedelta(days=days)
    sessions: list[dict[str, Any]] = []
    for path in root.rglob("*.jsonl"):
        modified = datetime.fromtimestamp(path.stat().st_mtime, tz=timezone.utc)
        if modified < cutoff:
            continue
        latest: dict[str, Any] | None = None
        try:
            with path.open("r", encoding="utf-8") as stream:
                for line in stream:
                    try:
                        event = json.loads(line)
                    except json.JSONDecodeError:
                        continue
                    if event.get("isSidechain") or not event.get("cwd"):
                        continue
                    latest = {
                        "session_id": event.get("sessionId") or path.stem,
                        "actor": "claude-code",
                        "state": "historical",
                        "worktree": canonical_path(event["cwd"]),
                        "branch": event.get("gitBranch"),
                        "last_seen": event.get("timestamp") or modified.isoformat(),
                        "source": str(path),
                    }
        except OSError:
            continue
        if latest:
            sessions.append(latest)
    return sessions


def resolve_contract_source(config: dict[str, Any]) -> tuple[Matcher, dict[str, Any]]:
    source = config.get("contract_source")
    if not isinstance(source, dict) or not source.get("path") or not source.get("expected_revision"):
        raise ValueError("config.contract_source needs path and expected_revision")
    root = Path(source["path"]).expanduser().resolve(strict=False)
    expected = source["expected_revision"]
    observed = None
    status = "unavailable"
    module_path = root / "cli" / "git_path_matcher.py"
    if root.is_dir():
        result = _git(root, "rev-parse", "HEAD", check=False)
        if result.returncode == 0:
            observed = result.stdout.decode("ascii").strip()
            status = "pinned" if observed == expected else "mismatch"
            if status == "pinned" and _git(
                root, "status", "--porcelain=v1", "--untracked-files=all"
            ).stdout:
                status = "dirty"
    resolved_module = module_path.resolve(strict=False)
    if root not in resolved_module.parents:
        status = "invalid-path"
    if status != "pinned" or not resolved_module.is_file():
        raise RuntimeError(
            f"Agent Blackbox contract source is {status}: expected {expected}, observed {observed}"
        )
    spec = importlib.util.spec_from_file_location("agent_blackbox_git_path_matcher", resolved_module)
    if spec is None or spec.loader is None:
        raise RuntimeError(f"cannot load matcher from {module_path}")
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module.GitPathMatcher, {
        "repository": source.get("repository", "GuitarAlchemist/agent-blackbox"),
        "path": str(root),
        "expected_revision": expected,
        "observed_revision": observed,
        "status": status,
    }


def fetch_prs(config: dict[str, Any]) -> dict[str, Any]:
    result: dict[str, Any] = {}
    for repository in config.get("repositories", []):
        repository_id = repository.get("id")
        if not repository_id:
            continue
        try:
            completed = subprocess.run(
                [
                    "gh",
                    "pr",
                    "list",
                    "--repo",
                    repository_id,
                    "--state",
                    "open",
                    "--limit",
                    "100",
                    "--json",
                    "number,url,isDraft,headRefName,headRefOid",
                ],
                check=False,
                capture_output=True,
                text=True,
                timeout=30,
            )
            result[repository_id] = json.loads(completed.stdout) if completed.returncode == 0 else []
        except (OSError, subprocess.TimeoutExpired, json.JSONDecodeError):
            result[repository_id] = []
    return result


def _load_json(path: Path) -> Any:
    return json.loads(path.read_text(encoding="utf-8"))


def _assert_output_outside_worktrees(output: Path, snapshot: dict[str, Any]) -> None:
    resolved = output.expanduser().resolve(strict=False)
    for packet in snapshot["packets"]:
        if packet.get("missing"):
            continue
        worktree = Path(packet["worktree"])
        if resolved == worktree or worktree in resolved.parents:
            raise ValueError(f"output must be outside discovered worktrees: {resolved}")


def _write_json_atomic(path: Path, value: dict[str, Any]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    temporary = path.with_name(f".{path.name}.tmp")
    temporary.write_text(json.dumps(value, indent=2, sort_keys=True) + "\n", encoding="utf-8")
    os.replace(temporary, path)


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--config", type=Path, required=True)
    parser.add_argument("--out", type=Path)
    parser.add_argument("--check", type=Path, help="compare with a prior snapshot; writes nothing")
    parser.add_argument("--presence-json", type=Path, help="normalized live-session presence snapshot")
    parser.add_argument("--prs-json", type=Path, help="offline PR fixture; live gh lookup when omitted")
    parser.add_argument(
        "--claude-root",
        type=Path,
        default=Path.home() / ".claude" / "projects",
        help="Claude Code JSONL root; only cwd/branch/session/timestamp metadata is read",
    )
    parser.add_argument("--no-claude", action="store_true")
    args = parser.parse_args(argv)
    if not args.out and not args.check:
        parser.error("one of --out or --check is required")

    config = _load_json(args.config)
    matcher, contract_source = resolve_contract_source(config)
    presence = _load_json(args.presence_json) if args.presence_json else None
    prs = _load_json(args.prs_json) if args.prs_json else fetch_prs(config)
    historical = [] if args.no_claude else read_claude_provenance(args.claude_root)
    snapshot = build_snapshot(
        config,
        matcher=matcher,
        contract_source=contract_source,
        presence=presence,
        prs=prs,
        historical_sessions=historical,
    )
    if args.check:
        result = snapshot_is_stale(_load_json(args.check), snapshot)
        print(json.dumps(result, indent=2, sort_keys=True))
        return 1 if result["stale"] else 0

    assert args.out is not None
    _assert_output_outside_worktrees(args.out, snapshot)
    _write_json_atomic(args.out, snapshot)
    print(f"wrote {args.out}: {snapshot['packet_count']} packet(s)")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
