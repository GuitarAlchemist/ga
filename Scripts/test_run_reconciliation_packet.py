"""Disposable-repository tests for the Cloud Factory packet runner."""

from __future__ import annotations

import json
import os
import subprocess
import tempfile
import unittest
from pathlib import Path

from Scripts.run_reconciliation_packet import (
    FinishPacketRequest,
    PacketRunner,
    StartPacketRequest,
    SubprocessAgentBlackboxToolkit,
    SubprocessGovernanceGate,
)


class FakeToolkit:
    def __init__(self) -> None:
        self.calls: list[tuple[str, dict]] = []
        self.verdict = {"contract": "afk-postflight-verdict-v0.2", "verdict": "pass", "failure_codes": []}

    def acquire(self, **kwargs: object) -> dict:
        self.calls.append(("acquire", kwargs))
        Path(kwargs["task_state_path"]).write_text(
            json.dumps(
                {
                    "schema_version": 2,
                    "contract": "afk-task-state-v0.2",
                    "task": {
                        "repository": kwargs["repository"],
                        "issue": kwargs["issue"],
                    },
                    "attempts": [],
                }
            )
            + "\n",
            encoding="utf-8",
        )
        return {
            "status": "leased",
            "lease": {"token": "0123456789abcdef0123456789abcdef", "generation": 1},
        }

    def start(self, **kwargs: object) -> dict:
        self.calls.append(("start", kwargs))
        return {
            "status": "running",
            "lease": {
                "token": kwargs["token"],
                "generation": kwargs["generation"],
            },
        }

    def postflight(self, **kwargs: object) -> dict:
        self.calls.append(("postflight", kwargs))
        Path(kwargs["verdict_path"]).write_text(json.dumps(self.verdict) + "\n", encoding="utf-8")
        return self.verdict

    def release(self, **kwargs: object) -> dict:
        self.calls.append(("release", kwargs))
        return {"status": kwargs["terminal_status"]}


class FakeGovernanceGate:
    def __init__(self) -> None:
        self.calls: list[dict] = []
        self.verdict = {"allowed": True, "source": "none", "reason": None}

    def check(self, **kwargs: object) -> dict:
        self.calls.append(kwargs)
        return self.verdict


class PacketRunnerTests(unittest.TestCase):
    def setUp(self) -> None:
        self.temp = tempfile.TemporaryDirectory()
        self.root = Path(self.temp.name)
        self.source = self.root / "source"
        self.source.mkdir()
        self._git(self.source, "init", "-b", "main")
        self._git(self.source, "config", "user.email", "test@example.com")
        self._git(self.source, "config", "user.name", "Test User")
        (self.source / "src").mkdir()
        (self.source / "src" / "app.py").write_text("print('base')\n", encoding="utf-8")
        self._git(self.source, "add", ".")
        self._git(self.source, "commit", "-m", "base")
        self.head = self._git(self.source, "rev-parse", "HEAD").stdout.strip()

        self.runtime = self.root / "runtime"
        self.runtime.mkdir()
        self.target = self.root / "isolated"
        self.handle = self.runtime / "run-handle.json"
        self.events = self.runtime / "events.jsonl"
        self.task_state = self.runtime / "task-state.json"
        self.budget = self.runtime / "budget.json"
        self.policy = self.runtime / "policy.json"
        self.evidence = self.runtime / "evidence.json"
        self.verdict = self.runtime / "verdict.json"
        self.budget.write_text(json.dumps({"max_attempts": 2}) + "\n", encoding="utf-8")
        self.policy.write_text(json.dumps({"allow_edit": ["src/**"]}) + "\n", encoding="utf-8")
        self.evidence.write_text("{}\n", encoding="utf-8")

        self.packet = {
            "packet_id": "local-test-packet",
            "repo": "GuitarAlchemist/test",
            "worktree": str(self.source.resolve()),
            "missing": False,
            "git_unavailable": False,
            "branch": "main",
            "head_sha": self.head,
            "detached": False,
            "behind": 0,
            "dirty_paths": [],
            "source_signature": f"{self.head}:empty",
            "pr": None,
            "owner": {"state": "unowned"},
            "task_binding": {
                "status": "bound",
                "canonical": {
                    "schema_version": 2,
                    "contract": "afk-task-state-v0.2",
                    "task": {"repository": "GuitarAlchemist/test", "issue": 630},
                },
            },
            "classification": "ready",
        }
        self.snapshot = {
            "contract": "ga-local-reconciliation-snapshot-v0.1",
            "contract_source": {"status": "pinned"},
            "packets": [self.packet],
        }
        self.toolkit = FakeToolkit()
        self.governance = FakeGovernanceGate()
        self.runner = PacketRunner(
            toolkit=self.toolkit,
            governance=self.governance,
            event_log=self.events,
        )

    def tearDown(self) -> None:
        self.temp.cleanup()

    @staticmethod
    def _git(repo: Path, *args: str) -> subprocess.CompletedProcess[str]:
        return subprocess.run(["git", *args], cwd=repo, check=True, capture_output=True, text=True)

    def _start_request(self) -> StartPacketRequest:
        return StartPacketRequest(
            snapshot=self.snapshot,
            packet_id="local-test-packet",
            target_worktree=self.target,
            branch="codex/factory-local-test-packet-1",
            handle_path=self.handle,
            task_state_path=self.task_state,
            budget_path=self.budget,
            worker="codex",
            provider="openai",
            attempt=1,
            lease_seconds=600,
        )

    def test_start_creates_one_isolated_worktree_and_running_fenced_lease(self) -> None:
        event = self.runner.start(self._start_request())

        self.assertEqual(self._git(self.target, "rev-parse", "HEAD").stdout.strip(), self.head)
        self.assertEqual(self._git(self.target, "branch", "--show-current").stdout.strip(), "codex/factory-local-test-packet-1")
        self.assertEqual([name for name, _ in self.toolkit.calls], ["acquire", "start"])
        self.assertEqual(event["event"], "started")
        self.assertEqual(event["base_sha"], self.head)
        self.assertEqual(event["lease_generation"], 1)
        self.assertNotIn("0123456789abcdef", self.events.read_text(encoding="utf-8"))

        handle = json.loads(self.handle.read_text(encoding="utf-8"))
        self.assertEqual(handle["lease"]["token"], "0123456789abcdef0123456789abcdef")
        self.assertEqual(handle["packet_id"], "local-test-packet")

    def test_start_fails_closed_when_source_head_changed_after_snapshot(self) -> None:
        (self.source / "src" / "other.py").write_text("print('new')\n", encoding="utf-8")
        self._git(self.source, "add", ".")
        self._git(self.source, "commit", "-m", "advance")

        with self.assertRaisesRegex(RuntimeError, "stale"):
            self.runner.start(self._start_request())

        self.assertFalse(self.target.exists())
        self.assertFalse(self.events.exists())
        self.assertEqual(self.toolkit.calls, [])

    def test_start_refuses_a_stop_signal_before_acquiring_a_lease(self) -> None:
        self.governance.verdict = {
            "allowed": False,
            "source": "stop-marker",
            "reason": "operator pause",
        }

        with self.assertRaisesRegex(RuntimeError, "operator pause"):
            self.runner.start(self._start_request())

        self.assertFalse(self.target.exists())
        self.assertFalse(self.events.exists())
        self.assertEqual(self.toolkit.calls, [])

    def test_start_rejects_owned_dirty_behind_or_unbound_packets(self) -> None:
        cases = [
            ("owner", {"owner": {"state": "live"}}),
            ("dirty", {"dirty_paths": [{"path": "src/app.py"}]}),
            ("behind", {"behind": 1}),
            ("task", {"task_binding": {"status": "unbound"}}),
        ]
        for label, updates in cases:
            with self.subTest(label=label):
                packet = {**self.packet, **updates}
                request = self._start_request()
                request.snapshot = {**self.snapshot, "packets": [packet]}
                with self.assertRaises(RuntimeError):
                    self.runner.start(request)
                self.assertFalse(self.target.exists())
                self.assertEqual(self.toolkit.calls, [])

    def test_finish_runs_exact_sha_postflight_then_releases_same_lease(self) -> None:
        self.runner.start(self._start_request())
        (self.target / "src" / "app.py").write_text("print('head')\n", encoding="utf-8")
        self._git(self.target, "add", ".")
        self._git(self.target, "commit", "-m", "head")
        head_sha = self._git(self.target, "rev-parse", "HEAD").stdout.strip()

        event = self.runner.finish(
            FinishPacketRequest(
                handle_path=self.handle,
                task_state_path=self.task_state,
                policy_path=self.policy,
                budget_path=self.budget,
                evidence_path=self.evidence,
                verdict_path=self.verdict,
            )
        )

        self.assertEqual([name for name, _ in self.toolkit.calls], ["acquire", "start", "postflight", "release"])
        postflight = self.toolkit.calls[2][1]
        release = self.toolkit.calls[3][1]
        self.assertEqual(postflight["head_sha"], head_sha)
        self.assertEqual(release["token"], "0123456789abcdef0123456789abcdef")
        self.assertEqual(release["generation"], 1)
        self.assertEqual(release["terminal_status"], "succeeded")
        self.assertEqual(event["event"], "postflight-passed")
        self.assertEqual(event["head_sha"], head_sha)
        self.assertEqual(event["verdict_sha256"], self._sha256(self.verdict))

    def test_finish_failure_is_terminal_and_classified(self) -> None:
        self.runner.start(self._start_request())
        self.toolkit.verdict = {
            "contract": "afk-postflight-verdict-v0.2",
            "verdict": "fail",
            "failure_codes": ["independent-review-pending"],
        }

        event = self.runner.finish(
            FinishPacketRequest(
                handle_path=self.handle,
                task_state_path=self.task_state,
                policy_path=self.policy,
                budget_path=self.budget,
                evidence_path=self.evidence,
                verdict_path=self.verdict,
            )
        )

        release = self.toolkit.calls[-1][1]
        self.assertEqual(release["terminal_status"], "failed")
        self.assertEqual(release["failure_class"], "review")
        self.assertEqual(event["event"], "postflight-failed")
        self.assertEqual(event["failure_class"], "review")

    def test_finish_halt_releases_the_exact_lease_as_durably_stopped(self) -> None:
        self.runner.start(self._start_request())
        self.governance.verdict = {
            "allowed": False,
            "source": "halt-all",
            "reason": "cost spike",
        }
        self.budget.write_text(json.dumps({"max_attempts": 99}) + "\n", encoding="utf-8")

        event = self.runner.finish(
            FinishPacketRequest(
                handle_path=self.handle,
                task_state_path=self.task_state,
                policy_path=self.policy,
                budget_path=self.budget,
                evidence_path=self.evidence,
                verdict_path=self.verdict,
            )
        )

        self.assertEqual([name for name, _ in self.toolkit.calls], ["acquire", "start", "release"])
        release = self.toolkit.calls[-1][1]
        self.assertEqual(release["terminal_status"], "stopped")
        self.assertEqual(release["token"], "0123456789abcdef0123456789abcdef")
        self.assertEqual(release["generation"], 1)
        self.assertEqual(event["event"], "stopped")
        self.assertEqual(event["stop_source"], "halt-all")
        self.assertEqual(event["stop_reason"], "cost spike")
        self.assertFalse(self.verdict.exists())

    def test_finish_rejects_a_changed_budget_before_postflight(self) -> None:
        self.runner.start(self._start_request())
        self.budget.write_text(json.dumps({"max_attempts": 99}) + "\n", encoding="utf-8")

        with self.assertRaisesRegex(RuntimeError, "budget differs"):
            self.runner.finish(
                FinishPacketRequest(
                    handle_path=self.handle,
                    task_state_path=self.task_state,
                    policy_path=self.policy,
                    budget_path=self.budget,
                    evidence_path=self.evidence,
                    verdict_path=self.verdict,
                )
            )

        self.assertEqual([name for name, _ in self.toolkit.calls], ["acquire", "start"])

    def test_start_and_finish_reject_worker_controlled_run_files(self) -> None:
        request = self._start_request()
        request.budget_path = self.source / "budget.json"
        request.budget_path.write_text("{}\n", encoding="utf-8")

        with self.assertRaisesRegex(ValueError, "run state"):
            self.runner.start(request)

        self.assertEqual(self.toolkit.calls, [])
        request.budget_path.unlink()
        request = self._start_request()
        self.runner.start(request)
        evidence = self.target / "evidence.json"
        evidence.write_text("{}\n", encoding="utf-8")

        with self.assertRaisesRegex(ValueError, "control files"):
            self.runner.finish(
                FinishPacketRequest(
                    handle_path=self.handle,
                    task_state_path=self.task_state,
                    policy_path=self.policy,
                    budget_path=self.budget,
                    evidence_path=evidence,
                    verdict_path=self.verdict,
                )
            )

        self.assertEqual([name for name, _ in self.toolkit.calls], ["acquire", "start"])

    @unittest.skipUnless(os.environ.get("AGENT_BLACKBOX_CHECKOUT"), "exact Agent Blackbox checkout not provided")
    def test_subprocess_adapter_completes_canonical_postflight_lifecycle(self) -> None:
        self.budget.write_text(
            json.dumps(
                {
                    "schema_version": 1,
                    "contract": "afk-budget-v0.1",
                    "max_autonomy": "edit",
                    "max_files": 2,
                    "max_changed_lines": 20,
                    "max_attempts": 1,
                    "max_runtime_seconds": 300,
                    "allow_provider_fallback": False,
                    "allow_merge": False,
                }
            )
            + "\n",
            encoding="utf-8",
        )
        self.policy.write_text(
            json.dumps(
                {
                    "allow_edit": ["src/**"],
                    "protected_paths": [],
                    "one_way_door_paths": [],
                    "required_artifacts": ["risk-report", "harness-audit", "test-output"],
                }
            )
            + "\n",
            encoding="utf-8",
        )
        toolkit = SubprocessAgentBlackboxToolkit(Path(os.environ["AGENT_BLACKBOX_CHECKOUT"]))
        runner = PacketRunner(
            toolkit=toolkit,
            governance=self.governance,
            event_log=self.events,
        )
        runner.start(self._start_request())
        (self.target / "src" / "app.py").write_text("print('head')\n", encoding="utf-8")
        self._git(self.target, "add", ".")
        self._git(self.target, "commit", "-m", "head")
        head_sha = self._git(self.target, "rev-parse", "HEAD").stdout.strip()
        handle = json.loads(self.handle.read_text(encoding="utf-8"))

        risk = self.runtime / "risk-report.json"
        harness = self.runtime / "harness-audit.json"
        tests = self.runtime / "test-output.txt"
        risk.write_text(json.dumps({"verdict": "pass", "head_sha": head_sha}) + "\n", encoding="utf-8")
        harness.write_text(json.dumps({"readiness": "loop-ready"}) + "\n", encoding="utf-8")
        tests.write_text("1 passed\n", encoding="utf-8")
        self.evidence.write_text(
            json.dumps(
                {
                    "schema_version": 2,
                    "contract": "afk-evidence-manifest-v0.2",
                    "task": {
                        "repository": "GuitarAlchemist/test",
                        "issue": 630,
                        "attempt": 1,
                        "lease_token": handle["lease"]["token"],
                        "lease_generation": handle["lease"]["generation"],
                    },
                    "base_sha": self.head,
                    "head_sha": head_sha,
                    "policy_sha256": self._sha256(self.policy),
                    "budget_sha256": self._sha256(self.budget),
                    "task_state_sha256": self._sha256(self.task_state),
                    "changed_files": ["src/app.py"],
                    "verification": [
                        {"tier": "L0", "status": "pass", "head_sha": head_sha},
                        {
                            "tier": "L1",
                            "status": "pass",
                            "head_sha": head_sha,
                            "command": "python -m unittest tests.test_app",
                            "covers": ["src/app.py"],
                            "artifact_kind": "test-output",
                        },
                        {"tier": "L2", "status": "skipped", "head_sha": head_sha},
                        {"tier": "L3", "status": "skipped", "head_sha": head_sha},
                    ],
                    "independent_review": {
                        "verdict": "pass",
                        "producer": "codex-integration-test",
                        "reviewer": "fixture-human",
                        "producer_context": "producer-fixture",
                        "reviewer_context": "reviewer-fixture",
                        "producer_provider": "openai",
                        "reviewer_provider": "human",
                        "independence": "human",
                        "head_sha": head_sha,
                    },
                    "artifacts": [
                        {"kind": "risk-report", "path": str(risk), "sha256": self._sha256(risk), "head_sha": head_sha},
                        {"kind": "harness-audit", "path": str(harness), "sha256": self._sha256(harness), "head_sha": head_sha},
                        {"kind": "test-output", "path": str(tests), "sha256": self._sha256(tests), "head_sha": head_sha},
                    ],
                    "execution": {
                        "autonomy": "edit",
                        "provider_fallback_used": False,
                        "merge_requested": False,
                    },
                    "runtime": {"elapsed_seconds": 1, "estimated_cost_usd": 0},
                }
            )
            + "\n",
            encoding="utf-8",
        )
        event = runner.finish(
            FinishPacketRequest(
                handle_path=self.handle,
                task_state_path=self.task_state,
                policy_path=self.policy,
                budget_path=self.budget,
                evidence_path=self.evidence,
                verdict_path=self.verdict,
            )
        )

        self.assertEqual(event["event"], "postflight-passed")
        self.assertEqual(json.loads(self.verdict.read_text(encoding="utf-8"))["verdict"], "pass")

    def test_subprocess_governance_adapter_detects_a_declared_stop_marker(self) -> None:
        stop_marker = self.source / "state" / "quality" / "test" / ".STOP"
        stop_marker.parent.mkdir(parents=True)
        stop_marker.write_text("operator pause\n", encoding="utf-8")
        adapter = SubprocessGovernanceGate(Path("Scripts/Governance.psm1"))

        verdict = adapter.check(
            repository_path=self.source,
            agent_id="codex-test",
            stop_marker_paths=[stop_marker],
        )

        self.assertFalse(verdict["allowed"])
        self.assertEqual(verdict["source"], "stop-marker")
        self.assertEqual(verdict["reason"], "operator pause")

    @staticmethod
    def _sha256(path: Path) -> str:
        import hashlib

        return hashlib.sha256(path.read_bytes()).hexdigest()


if __name__ == "__main__":
    unittest.main()
