#!/usr/bin/env python3
"""Start or finish one bounded Cloud Factory reconciliation packet.

The runner owns lifecycle, not implementation: it validates one read-only
snapshot packet, creates one isolated worktree, consumes Agent Blackbox's
canonical fenced lease and postflight interfaces, appends a transition, and
stops. It never runs an arbitrary worker command, pushes, merges, or cleans a
source checkout.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import os
import subprocess
import sys
import uuid
from dataclasses import dataclass
from datetime import datetime, timezone
from pathlib import Path
from typing import Any, Protocol

if __package__:
    from Scripts.reconcile_local_work import resolve_contract_source
else:
    from reconcile_local_work import resolve_contract_source


RUN_HANDLE_CONTRACT = "ga-packet-run-handle-v0.1"
RUN_EVENT_CONTRACT = "ga-packet-run-event-v0.1"


class AgentBlackboxToolkit(Protocol):
    def acquire(self, **kwargs: object) -> dict[str, Any]: ...

    def start(self, **kwargs: object) -> dict[str, Any]: ...

    def postflight(self, **kwargs: object) -> dict[str, Any]: ...

    def release(self, **kwargs: object) -> dict[str, Any]: ...


@dataclass
class StartPacketRequest:
    snapshot: dict[str, Any]
    packet_id: str
    target_worktree: Path
    branch: str
    handle_path: Path
    task_state_path: Path
    budget_path: Path
    worker: str
    provider: str
    attempt: int
    lease_seconds: int


@dataclass
class FinishPacketRequest:
    handle_path: Path
    task_state_path: Path
    policy_path: Path
    budget_path: Path
    evidence_path: Path
    verdict_path: Path


def _utc_now() -> str:
    return datetime.now(timezone.utc).isoformat().replace("+00:00", "Z")


def _sha256_bytes(value: bytes) -> str:
    return hashlib.sha256(value).hexdigest()


def _sha256_file(path: Path) -> str:
    return _sha256_bytes(path.read_bytes())


def _git(repository: Path, *args: str, check: bool = True) -> subprocess.CompletedProcess[str]:
    environment = os.environ.copy()
    environment["GIT_OPTIONAL_LOCKS"] = "0"
    return subprocess.run(
        ["git", "-c", "core.quotepath=false", *args],
        cwd=repository,
        check=check,
        capture_output=True,
        text=True,
        env=environment,
    )


def _atomic_json(path: Path, value: dict[str, Any]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    temporary = path.with_name(f".{path.name}.{uuid.uuid4().hex}.tmp")
    try:
        temporary.write_text(json.dumps(value, indent=2, sort_keys=True) + "\n", encoding="utf-8")
        os.replace(temporary, path)
    finally:
        temporary.unlink(missing_ok=True)


def _read_json(path: Path) -> dict[str, Any]:
    value = json.loads(path.read_text(encoding="utf-8"))
    if not isinstance(value, dict):
        raise ValueError(f"expected a JSON object: {path}")
    return value


class PacketRunner:
    """Deep lifecycle module over packet eligibility and AFK postflight."""

    def __init__(
        self,
        *,
        toolkit: AgentBlackboxToolkit,
        event_log: Path,
        toolkit_revision: str | None = None,
    ):
        self.toolkit = toolkit
        self.event_log = event_log
        self.toolkit_revision = toolkit_revision

    def start(self, request: StartPacketRequest) -> dict[str, Any]:
        packet = self._select_eligible_packet(request.snapshot, request.packet_id)
        snapshot_revision = request.snapshot.get("contract_source", {}).get("observed_revision")
        if self.toolkit_revision and snapshot_revision != self.toolkit_revision:
            raise RuntimeError("snapshot Agent Blackbox revision differs from the packet runner")
        source = Path(packet["worktree"]).resolve(strict=True)
        target = request.target_worktree.resolve(strict=False)
        handle_path = request.handle_path.resolve(strict=False)
        task_state_path = request.task_state_path.resolve(strict=False)
        budget_path = request.budget_path.resolve(strict=True)
        self._validate_control_paths(
            request.snapshot,
            target=target,
            outputs=[
                handle_path,
                task_state_path,
                budget_path,
                self.event_log.resolve(strict=False),
            ],
        )
        if target.exists():
            raise RuntimeError(f"isolated worktree already exists: {target}")
        if handle_path.exists():
            raise RuntimeError(f"run handle already exists: {handle_path}")
        if request.attempt < 1 or request.lease_seconds < 1:
            raise ValueError("attempt and lease_seconds must be positive")
        if not request.branch.startswith("codex/"):
            raise ValueError("isolated packet branches must use the codex/ prefix")

        observed_head = _git(source, "rev-parse", "HEAD").stdout.strip()
        observed_dirty = _git(source, "status", "--porcelain=v1", "--untracked-files=all").stdout
        if observed_head != packet["head_sha"] or observed_dirty:
            raise RuntimeError("snapshot is stale: selected source HEAD or dirty state changed")

        task = packet["task_binding"]["canonical"]["task"]
        budget = _read_json(budget_path)
        lease: dict[str, Any] | None = None
        isolated = False
        try:
            lease = self.toolkit.acquire(
                task_state_path=task_state_path,
                repository_path=source,
                repository=task["repository"],
                issue=task["issue"],
                attempt=request.attempt,
                worker=request.worker,
                provider=request.provider,
                base_sha=observed_head,
                branch=request.branch,
                lease_seconds=request.lease_seconds,
                budget_path=budget_path,
                budget=budget,
            )
            token, generation = self._lease_identity(lease)
            target.parent.mkdir(parents=True, exist_ok=True)
            _git(source, "worktree", "add", "-b", request.branch, str(target), observed_head)
            isolated = True
            if _git(target, "rev-parse", "HEAD").stdout.strip() != observed_head:
                raise RuntimeError("isolated worktree HEAD differs from the leased base SHA")
            running = self.toolkit.start(
                task_state_path=task_state_path,
                token=token,
                generation=generation,
            )
            if running.get("status") != "running":
                raise RuntimeError("Agent Blackbox did not transition the lease to running")

            run_id = f"{request.packet_id}:a{request.attempt}:g{generation}"
            handle = {
                "schema_version": 1,
                "contract": RUN_HANDLE_CONTRACT,
                "run_id": run_id,
                "packet_id": request.packet_id,
                "repository": task["repository"],
                "issue": task["issue"],
                "attempt": request.attempt,
                "base_sha": observed_head,
                "source_worktree": str(source),
                "isolated_worktree": str(target),
                "branch": request.branch,
                "worker": request.worker,
                "provider": request.provider,
                "agent_blackbox_revision": request.snapshot.get("contract_source", {}).get(
                    "observed_revision"
                ),
                "task_state_path": str(task_state_path),
                "budget_path": str(budget_path),
                "budget_sha256": _sha256_file(budget_path),
                "event_log_path": str(self.event_log.resolve(strict=False)),
                "snapshot_generated_at": request.snapshot.get("generated_at"),
                "packet_source_signature": packet.get("source_signature"),
                "lease": {"token": token, "generation": generation},
                "started_at": _utc_now(),
            }
            _atomic_json(handle_path, handle)
            event = self._event(
                handle,
                event="started",
                head_sha=observed_head,
                lease_token_sha256=_sha256_bytes(token.encode("utf-8")),
            )
            return self._append_event(event, idempotency_key=f"{run_id}:started")
        except Exception:
            if lease is not None:
                token, generation = self._lease_identity(lease)
                try:
                    self.toolkit.release(
                        task_state_path=task_state_path,
                        token=token,
                        generation=generation,
                        terminal_status="stopped",
                        failure_class=None,
                    )
                except Exception:
                    pass
            if isolated:
                # Preserve the isolated checkout for diagnosis; cleanup is an
                # explicit operator action, never an exception side effect.
                pass
            raise

    def finish(self, request: FinishPacketRequest) -> dict[str, Any]:
        handle = _read_json(request.handle_path.resolve(strict=True))
        if handle.get("contract") != RUN_HANDLE_CONTRACT or handle.get("schema_version") != 1:
            raise ValueError("run handle contract is invalid")
        if self.toolkit_revision and handle.get("agent_blackbox_revision") != self.toolkit_revision:
            raise RuntimeError("run handle Agent Blackbox revision differs from the packet runner")
        worktree = Path(handle["isolated_worktree"]).resolve(strict=True)
        task_state_path = request.task_state_path.resolve(strict=True)
        budget_path = request.budget_path.resolve(strict=True)
        event_log_path = self.event_log.resolve(strict=False)
        if task_state_path != Path(handle["task_state_path"]).resolve(strict=True):
            raise RuntimeError("finish task-state path differs from the started run")
        if budget_path != Path(handle["budget_path"]).resolve(strict=True):
            raise RuntimeError("finish budget path differs from the started run")
        if event_log_path != Path(handle["event_log_path"]).resolve(strict=False):
            raise RuntimeError("finish event log differs from the started run")
        if _sha256_file(budget_path) != handle["budget_sha256"]:
            raise RuntimeError("finish budget differs from the budget pinned at start")
        head_sha = _git(worktree, "rev-parse", "HEAD").stdout.strip()
        lease = handle["lease"]
        token = lease["token"]
        generation = lease["generation"]
        verdict_path = request.verdict_path.resolve(strict=False)
        evidence_path = request.evidence_path.resolve(strict=True)
        source_worktree = Path(handle["source_worktree"]).resolve(strict=True)
        for output in (verdict_path, evidence_path, task_state_path, budget_path, event_log_path):
            if self._inside(output, worktree) or self._inside(output, source_worktree):
                raise ValueError("run control files must stay outside source and isolated worktrees")

        verdict = self.toolkit.postflight(
            repository_path=worktree,
            repository=handle["repository"],
            issue=handle["issue"],
            attempt=handle["attempt"],
            base_sha=handle["base_sha"],
            head_sha=head_sha,
            policy_path=request.policy_path.resolve(strict=True),
            budget_path=budget_path,
            evidence_path=evidence_path,
            task_state_path=task_state_path,
            verdict_path=verdict_path,
        )
        if not verdict_path.is_file():
            raise RuntimeError("Agent Blackbox postflight returned without a verdict artifact")
        passed = verdict.get("verdict") == "pass"
        failure_class = None if passed else self._failure_class(verdict.get("failure_codes", []))
        self.toolkit.release(
            task_state_path=task_state_path,
            token=token,
            generation=generation,
            terminal_status="succeeded" if passed else "failed",
            failure_class=failure_class,
        )
        event = self._event(
            handle,
            event="postflight-passed" if passed else "postflight-failed",
            head_sha=head_sha,
            verdict_sha256=_sha256_file(verdict_path),
            verdict_path=str(verdict_path),
            failure_class=failure_class,
        )
        return self._append_event(
            event,
            idempotency_key=f"{handle['run_id']}:{event['event']}",
        )

    @staticmethod
    def _select_eligible_packet(snapshot: dict[str, Any], packet_id: str) -> dict[str, Any]:
        if snapshot.get("contract") != "ga-local-reconciliation-snapshot-v0.1":
            raise ValueError("snapshot contract is invalid")
        if snapshot.get("contract_source", {}).get("status") != "pinned":
            raise RuntimeError("snapshot does not have a pinned Agent Blackbox source")
        matches = [item for item in snapshot.get("packets", []) if item.get("packet_id") == packet_id]
        if len(matches) != 1:
            raise ValueError(f"packet id must select exactly one packet: {packet_id}")
        packet = matches[0]
        failures = []
        if packet.get("classification") != "ready":
            failures.append("classification is not ready")
        if packet.get("owner", {}).get("state") != "unowned":
            failures.append("ownership is not uncontested")
        if packet.get("dirty_paths"):
            failures.append("source worktree is dirty")
        if packet.get("behind") not in (None, 0):
            failures.append("source branch is behind")
        if packet.get("missing") or packet.get("git_unavailable") or packet.get("detached"):
            failures.append("source worktree is unavailable or detached")
        if packet.get("pr") and not packet["pr"].get("head_matches", False):
            failures.append("PR head does not match packet head")
        binding = packet.get("task_binding", {})
        canonical = binding.get("canonical", {})
        if (
            binding.get("status") != "bound"
            or canonical.get("contract") != "afk-task-state-v0.2"
            or canonical.get("schema_version") != 2
            or canonical.get("task", {}).get("repository") != packet.get("repo")
            or not isinstance(canonical.get("task", {}).get("issue"), int)
            or canonical["task"]["issue"] < 1
        ):
            failures.append("packet is not bound to a canonical AFK task")
        if not packet.get("head_sha"):
            failures.append("packet has no head SHA")
        if failures:
            raise RuntimeError("packet is ineligible: " + "; ".join(failures))
        return packet

    @staticmethod
    def _validate_control_paths(
        snapshot: dict[str, Any], *, target: Path, outputs: list[Path]
    ) -> None:
        worktrees = [
            Path(item["worktree"]).resolve(strict=False)
            for item in snapshot.get("packets", [])
            if item.get("worktree") and not item.get("missing")
        ]
        for worktree in worktrees:
            if PacketRunner._inside(target, worktree):
                raise ValueError("isolated target must be outside discovered worktrees")
            for output in outputs:
                if PacketRunner._inside(output, worktree):
                    raise ValueError("run state must be outside discovered worktrees")
        for output in outputs:
            if PacketRunner._inside(output, target):
                raise ValueError("run state must be outside the isolated worktree")

    @staticmethod
    def _inside(candidate: Path, root: Path) -> bool:
        return candidate == root or root in candidate.parents

    @staticmethod
    def _lease_identity(record: dict[str, Any]) -> tuple[str, int]:
        lease = record.get("lease", {})
        token = lease.get("token")
        generation = lease.get("generation")
        if not isinstance(token, str) or len(token) < 16 or not isinstance(generation, int):
            raise RuntimeError("Agent Blackbox returned an invalid lease identity")
        return token, generation

    @staticmethod
    def _failure_class(codes: object) -> str:
        joined = " ".join(str(item).lower() for item in codes) if isinstance(codes, list) else str(codes).lower()
        if "review" in joined:
            return "review"
        if "budget" in joined:
            return "budget"
        if any(term in joined for term in ("policy", "protected", "one-way", "path-")):
            return "policy"
        if "provider" in joined:
            return "provider"
        if "infrastructure" in joined:
            return "infrastructure"
        return "verification"

    @staticmethod
    def _event(handle: dict[str, Any], *, event: str, head_sha: str, **fields: Any) -> dict[str, Any]:
        value = {
            "schema_version": 1,
            "contract": RUN_EVENT_CONTRACT,
            "event_id": str(uuid.uuid4()),
            "recorded_at": _utc_now(),
            "event": event,
            "run_id": handle["run_id"],
            "packet_id": handle["packet_id"],
            "repository": handle["repository"],
            "issue": handle["issue"],
            "attempt": handle["attempt"],
            "base_sha": handle["base_sha"],
            "head_sha": head_sha,
            "worktree": handle["isolated_worktree"],
            "branch": handle["branch"],
            "lease_generation": handle["lease"]["generation"],
            "agent_blackbox_revision": handle.get("agent_blackbox_revision"),
            "packet_source_signature": handle.get("packet_source_signature"),
        }
        value.update({key: item for key, item in fields.items() if item is not None})
        return value

    def _append_event(self, event: dict[str, Any], *, idempotency_key: str) -> dict[str, Any]:
        self.event_log.parent.mkdir(parents=True, exist_ok=True)
        lock_path = self.event_log.with_name(f"{self.event_log.name}.lock")
        try:
            lock_fd = os.open(lock_path, os.O_CREAT | os.O_EXCL | os.O_WRONLY)
        except FileExistsError as error:
            raise RuntimeError(f"packet event log is locked: {lock_path}") from error
        try:
            if self.event_log.exists():
                for line in self.event_log.read_text(encoding="utf-8").splitlines():
                    existing = json.loads(line)
                    if existing.get("idempotency_key") == idempotency_key:
                        return existing
            persisted = {**event, "idempotency_key": idempotency_key}
            with self.event_log.open("a", encoding="utf-8", newline="\n") as stream:
                stream.write(json.dumps(persisted, separators=(",", ":"), sort_keys=True) + "\n")
                stream.flush()
                os.fsync(stream.fileno())
            return persisted
        finally:
            os.close(lock_fd)
            lock_path.unlink(missing_ok=True)


class SubprocessAgentBlackboxToolkit:
    """Production adapter over Agent Blackbox's reviewed CLI interface."""

    def __init__(self, root: Path):
        self.root = root.resolve(strict=True)

    def acquire(self, **kwargs: object) -> dict[str, Any]:
        return self._json_command(
            "afk-lease", "acquire",
            "--state", kwargs["task_state_path"],
            "--repo", kwargs["repository_path"],
            "--repository", kwargs["repository"],
            "--issue", kwargs["issue"],
            "--attempt", kwargs["attempt"],
            "--worker", kwargs["worker"],
            "--provider", kwargs["provider"],
            "--base-sha", kwargs["base_sha"],
            "--branch", kwargs["branch"],
            "--lease-seconds", kwargs["lease_seconds"],
            "--budget", kwargs["budget_path"],
        )

    def start(self, **kwargs: object) -> dict[str, Any]:
        return self._json_command(
            "afk-lease", "start",
            "--state", kwargs["task_state_path"],
            "--token", kwargs["token"],
            "--generation", kwargs["generation"],
        )

    def postflight(self, **kwargs: object) -> dict[str, Any]:
        verdict_path = Path(kwargs["verdict_path"])
        completed = self._command(
            "afk-postflight",
            "--repo", kwargs["repository_path"],
            "--repository-id", kwargs["repository"],
            "--issue", kwargs["issue"],
            "--attempt", kwargs["attempt"],
            "--base-sha", kwargs["base_sha"],
            "--head-sha", kwargs["head_sha"],
            "--policy", kwargs["policy_path"],
            "--budget", kwargs["budget_path"],
            "--evidence", kwargs["evidence_path"],
            "--task-state", kwargs["task_state_path"],
            "--out", verdict_path,
            allowed_codes={0, 1},
        )
        if not verdict_path.is_file():
            raise RuntimeError(f"Agent Blackbox postflight emitted no verdict: {completed.stderr}")
        return _read_json(verdict_path)

    def release(self, **kwargs: object) -> dict[str, Any]:
        arguments: list[object] = [
            "afk-lease", "release",
            "--state", kwargs["task_state_path"],
            "--token", kwargs["token"],
            "--generation", kwargs["generation"],
            "--terminal-status", kwargs["terminal_status"],
        ]
        if kwargs.get("failure_class"):
            arguments.extend(["--failure-class", kwargs["failure_class"]])
        return self._json_command(*arguments)

    def _json_command(self, *arguments: object) -> dict[str, Any]:
        completed = self._command(*arguments)
        try:
            value = json.loads(completed.stdout)
        except json.JSONDecodeError as error:
            raise RuntimeError(f"Agent Blackbox returned invalid JSON: {completed.stdout}") from error
        if not isinstance(value, dict):
            raise RuntimeError("Agent Blackbox returned a non-object result")
        return value

    def _command(
        self, *arguments: object, allowed_codes: set[int] | None = None
    ) -> subprocess.CompletedProcess[str]:
        completed = subprocess.run(
            [sys.executable, "-m", "cli.agent_blackbox", *[str(item) for item in arguments]],
            cwd=self.root,
            capture_output=True,
            text=True,
            check=False,
        )
        if completed.returncode not in (allowed_codes or {0}):
            raise RuntimeError(
                f"Agent Blackbox command failed ({completed.returncode}): {completed.stderr or completed.stdout}"
            )
        return completed


def _toolkit_from_config(
    config_path: Path,
) -> tuple[SubprocessAgentBlackboxToolkit, dict[str, Any]]:
    config = _read_json(config_path.resolve(strict=True))
    _, source = resolve_contract_source(config)
    return SubprocessAgentBlackboxToolkit(Path(source["path"])), source


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--config", type=Path, required=True)
    parser.add_argument("--event-log", type=Path, required=True)
    subparsers = parser.add_subparsers(dest="action", required=True)

    start = subparsers.add_parser("start")
    start.add_argument("--snapshot", type=Path, required=True)
    start.add_argument("--packet-id", required=True)
    start.add_argument("--target-worktree", type=Path, required=True)
    start.add_argument("--branch", required=True)
    start.add_argument("--handle", type=Path, required=True)
    start.add_argument("--task-state", type=Path, required=True)
    start.add_argument("--budget", type=Path, required=True)
    start.add_argument("--worker", required=True)
    start.add_argument("--provider", required=True)
    start.add_argument("--attempt", type=int, default=1)
    start.add_argument("--lease-seconds", type=int, default=3600)

    finish = subparsers.add_parser("finish")
    finish.add_argument("--handle", type=Path, required=True)
    finish.add_argument("--task-state", type=Path, required=True)
    finish.add_argument("--policy", type=Path, required=True)
    finish.add_argument("--budget", type=Path, required=True)
    finish.add_argument("--evidence", type=Path, required=True)
    finish.add_argument("--verdict", type=Path, required=True)

    args = parser.parse_args(argv)
    toolkit, contract_source = _toolkit_from_config(args.config)
    runner = PacketRunner(
        toolkit=toolkit,
        event_log=args.event_log,
        toolkit_revision=contract_source["observed_revision"],
    )
    if args.action == "start":
        snapshot = _read_json(args.snapshot.resolve(strict=True))
        snapshot_revision = snapshot.get("contract_source", {}).get("observed_revision")
        if snapshot_revision != contract_source["observed_revision"]:
            raise RuntimeError(
                "snapshot Agent Blackbox revision differs from the runner configuration"
            )
        result = runner.start(
            StartPacketRequest(
                snapshot=snapshot,
                packet_id=args.packet_id,
                target_worktree=args.target_worktree,
                branch=args.branch,
                handle_path=args.handle,
                task_state_path=args.task_state,
                budget_path=args.budget,
                worker=args.worker,
                provider=args.provider,
                attempt=args.attempt,
                lease_seconds=args.lease_seconds,
            )
        )
    else:
        result = runner.finish(
            FinishPacketRequest(
                handle_path=args.handle,
                task_state_path=args.task_state,
                policy_path=args.policy,
                budget_path=args.budget,
                evidence_path=args.evidence,
                verdict_path=args.verdict,
            )
        )
    print(json.dumps(result, indent=2, sort_keys=True))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
