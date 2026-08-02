"""Temporary-repository tests for the read-only reconciliation snapshot."""

from __future__ import annotations

import hashlib
import json
import subprocess
import tempfile
import unittest
from pathlib import Path

from Scripts.reconcile_local_work import (
    build_snapshot,
    read_claude_provenance,
    resolve_contract_source,
    snapshot_is_stale,
)


class FakeMatcher:
    @classmethod
    def matches_any(cls, path: str, patterns: list[str]) -> bool:
        from fnmatch import fnmatch

        normalized = path.replace("\\", "/")
        return any(fnmatch(normalized, pattern.replace("**", "*")) for pattern in patterns)


class ReconcileLocalWorkTests(unittest.TestCase):
    def setUp(self) -> None:
        self.temp = tempfile.TemporaryDirectory()
        self.root = Path(self.temp.name)
        self.repo = self.root / "repo"
        self.repo.mkdir()
        self._git(self.repo, "init", "-b", "main")
        self._git(self.repo, "config", "user.email", "test@example.com")
        self._git(self.repo, "config", "user.name", "Test User")
        (self.repo / "src").mkdir()
        (self.repo / "src" / "app.py").write_text("base\n", encoding="utf-8")
        self._git(self.repo, "add", ".")
        self._git(self.repo, "commit", "-m", "base")
        base = self._git(self.repo, "rev-parse", "HEAD").stdout.strip()
        self._git(self.repo, "branch", "upstream", base)
        self._git(self.repo, "branch", "--set-upstream-to=upstream", "main")
        (self.repo / "src" / "app.py").write_text("head\n", encoding="utf-8")
        self._git(self.repo, "add", ".")
        self._git(self.repo, "commit", "-m", "ahead")

        tree = self._git(self.repo, "rev-parse", f"{base}^{{tree}}").stdout.strip()
        upstream = subprocess.run(
            ["git", "commit-tree", tree, "-p", base],
            cwd=self.repo,
            input="upstream\n",
            capture_output=True,
            text=True,
            check=True,
        ).stdout.strip()
        self._git(self.repo, "update-ref", "refs/heads/upstream", upstream)
        (self.repo / "src" / "app.py").write_text("dirty\n", encoding="utf-8")

        self.detached = self.root / "detached"
        self._git(self.repo, "worktree", "add", "--detach", str(self.detached), "HEAD")

        self.config = {
            "schema_version": 1,
            "repositories": [
                {"id": "GuitarAlchemist/test", "path": str(self.repo)},
                {"id": "GuitarAlchemist/missing", "path": str(self.root / "missing")},
            ],
            "path_classes": {
                "state": ["state/**"],
                "generated": ["**/*.generated.*"],
                "source": ["**/*.py"],
            },
        }
        self.contract = {
            "repository": "GuitarAlchemist/agent-blackbox",
            "expected_revision": "a" * 40,
            "observed_revision": "a" * 40,
            "status": "pinned",
        }
        self.presence = {
            "sessions": [
                {
                    "session_id": "live-1",
                    "actor": "claude-code",
                    "state": "live",
                    "worktree": str(self.repo),
                    "branch": "main",
                }
            ]
        }
        self.prs = {
            "GuitarAlchemist/test": [
                {
                    "number": 7,
                    "headRefName": "main",
                    "headRefOid": self._git(self.repo, "rev-parse", "HEAD").stdout.strip(),
                    "isDraft": True,
                    "url": "https://example.test/pr/7",
                }
            ]
        }
        self.claude = [
            {
                "session_id": "ended-1",
                "actor": "claude-code",
                "state": "historical",
                "worktree": str(self.repo),
                "branch": "main",
                "last_seen": "2026-08-01T12:00:00Z",
                "source": "session.jsonl",
            }
        ]

    def tearDown(self) -> None:
        self.temp.cleanup()

    @staticmethod
    def _git(repo: Path, *args: str) -> subprocess.CompletedProcess[str]:
        return subprocess.run(
            ["git", *args], cwd=repo, check=True, capture_output=True, text=True
        )

    @staticmethod
    def _content_digest(root: Path) -> str:
        digest = hashlib.sha256()
        for path in sorted(item for item in root.rglob("*") if item.is_file() and ".git" not in item.parts):
            digest.update(str(path.relative_to(root)).encode())
            digest.update(path.read_bytes())
        return digest.hexdigest()

    def _snapshot(self) -> dict:
        return build_snapshot(
            self.config,
            matcher=FakeMatcher,
            contract_source=self.contract,
            presence=self.presence,
            prs=self.prs,
            historical_sessions=self.claude,
        )

    def test_discovers_git_states_and_binds_ownership_pr_and_provenance(self) -> None:
        before = self._content_digest(self.repo)
        index_before = (self.repo / ".git" / "index").read_bytes()
        snapshot = self._snapshot()
        after = self._content_digest(self.repo)
        self.assertEqual(before, after)
        self.assertEqual(index_before, (self.repo / ".git" / "index").read_bytes())

        lanes = {packet["worktree"]: packet for packet in snapshot["packets"]}
        main = lanes[str(self.repo.resolve())]
        self.assertEqual(main["branch"], "main")
        self.assertEqual((main["ahead"], main["behind"]), (1, 1))
        self.assertEqual(main["owner"]["session_id"], "live-1")
        self.assertEqual(main["pr"]["number"], 7)
        self.assertEqual(main["provenance"][0]["session_id"], "ended-1")
        self.assertEqual(main["dirty_paths"][0]["classification"], "source")
        self.assertEqual(main["classification"], "active")

        detached = lanes[str(self.detached.resolve())]
        self.assertTrue(detached["detached"])
        self.assertIsNone(detached["branch"])

        missing = next(packet for packet in snapshot["packets"] if packet["repo"] == "GuitarAlchemist/missing")
        self.assertTrue(missing["missing"])
        self.assertEqual(missing["classification"], "blocked")

    def test_packet_ids_are_stable_and_dirty_changes_make_snapshot_stale(self) -> None:
        first = self._snapshot()
        second = self._snapshot()
        self.assertEqual(
            [packet["packet_id"] for packet in first["packets"]],
            [packet["packet_id"] for packet in second["packets"]],
        )
        self.assertFalse(snapshot_is_stale(first, second)["stale"])

        (self.repo / "new.txt").write_text("new dirty path\n", encoding="utf-8")
        third = self._snapshot()
        stale = snapshot_is_stale(first, third)
        self.assertTrue(stale["stale"])
        self.assertTrue(stale["changed_packet_ids"])

    def test_contract_revision_mismatch_blocks_all_packets(self) -> None:
        contract = dict(self.contract, observed_revision="b" * 40, status="mismatch")
        snapshot = build_snapshot(
            self.config,
            matcher=FakeMatcher,
            contract_source=contract,
            presence=self.presence,
            prs=self.prs,
            historical_sessions=self.claude,
        )
        self.assertEqual(snapshot["contract_source"]["status"], "mismatch")
        self.assertTrue(all(packet["classification"] == "blocked" for packet in snapshot["packets"]))

    def test_registered_directory_that_is_not_a_worktree_is_blocked(self) -> None:
        (self.detached / ".git").unlink()
        snapshot = self._snapshot()
        packet = next(
            item for item in snapshot["packets"] if item["worktree"] == str(self.detached.resolve())
        )
        self.assertTrue(packet["git_unavailable"])
        self.assertEqual(packet["classification"], "blocked")

    def test_claude_provenance_reads_metadata_not_conversation_content(self) -> None:
        claude_root = self.root / "claude"
        claude_root.mkdir()
        log = claude_root / "session.jsonl"
        log.write_text(
            json.dumps(
                {
                    "type": "assistant",
                    "sessionId": "session-1",
                    "cwd": str(self.repo),
                    "gitBranch": "main",
                    "timestamp": "2026-08-02T12:00:00Z",
                    "message": {"content": "TOP-SECRET-CONVERSATION-CONTENT"},
                }
            )
            + "\n",
            encoding="utf-8",
        )
        sessions = read_claude_provenance(claude_root)
        self.assertEqual(sessions[0]["session_id"], "session-1")
        self.assertNotIn("TOP-SECRET", json.dumps(sessions))

    def test_contract_matcher_loads_only_at_exact_git_revision(self) -> None:
        dependency = self.root / "agent-blackbox"
        (dependency / "cli").mkdir(parents=True)
        self._git(dependency, "init", "-b", "main")
        self._git(dependency, "config", "user.email", "test@example.com")
        self._git(dependency, "config", "user.name", "Test User")
        (dependency / "cli" / "git_path_matcher.py").write_text(
            "class GitPathMatcher:\n"
            "    @classmethod\n"
            "    def matches_any(cls, path, patterns):\n"
            "        return path in patterns\n",
            encoding="utf-8",
        )
        self._git(dependency, "add", ".")
        self._git(dependency, "commit", "-m", "matcher")
        revision = self._git(dependency, "rev-parse", "HEAD").stdout.strip()
        config = {
            "contract_source": {
                "path": str(dependency),
                "expected_revision": revision,
            }
        }
        matcher, source = resolve_contract_source(config)
        self.assertTrue(matcher.matches_any("x", ["x"]))
        self.assertEqual(source["status"], "pinned")

        matcher_path = dependency / "cli" / "git_path_matcher.py"
        matcher_path.write_text(matcher_path.read_text(encoding="utf-8") + "# dirty\n", encoding="utf-8")
        with self.assertRaises(RuntimeError):
            resolve_contract_source(config)
        self._git(dependency, "add", ".")
        self._git(dependency, "commit", "-m", "advance matcher")

        (dependency / "README.md").write_text("new head\n", encoding="utf-8")
        self._git(dependency, "add", ".")
        self._git(dependency, "commit", "-m", "advance")
        with self.assertRaises(RuntimeError):
            resolve_contract_source(config)


if __name__ == "__main__":
    unittest.main()
