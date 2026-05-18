#!/usr/bin/env python3
"""Fleet Status generator.

Reads:
  - GitHub PR data for 5 sibling repos (via `gh pr list`)
  - install-audit JSON output for each repo (caller passes a directory)
  - state/fleet-status-initiatives.json (hand-curated, sanitised)
  - state/fleet-status-blockers.json    (hand-curated)

Writes:
  - state/fleet-status.md                              (durable markdown mirror)
  - ReactComponents/ga-react-components/public/fleet-status.json (page data)

No services touched. Pure GitHub-API + filesystem reader.

Usage:
  python Scripts/fleet-status-generate.py \
      --repos GuitarAlchemist/agent-blackbox GuitarAlchemist/ga \
              GuitarAlchemist/ix GuitarAlchemist/tars GuitarAlchemist/Demerzel \
      --install-audits-dir state/fleet-status-audits \
      --root .

Inside CI, install-audits-dir contains <repo-shortname>.install-audit.json files
emitted by the workflow's install-audit step (one per repo).
"""

from __future__ import annotations

import argparse
import json
import subprocess
import sys
from datetime import datetime, timezone
from pathlib import Path
from typing import Any


REPOS_DEFAULT = [
    "GuitarAlchemist/agent-blackbox",
    "GuitarAlchemist/ga",
    "GuitarAlchemist/ix",
    "GuitarAlchemist/tars",
    "GuitarAlchemist/Demerzel",
]


# ---------------------------------------------------------------------------
# PR collection
# ---------------------------------------------------------------------------


def fetch_prs(repo: str) -> list[dict[str, Any]]:
    """Call `gh pr list --repo <repo>` and return a normalised list."""
    try:
        result = subprocess.run(
            [
                "gh",
                "pr",
                "list",
                "--repo",
                repo,
                "--json",
                "number,title,createdAt,mergeable,isDraft,headRefName,statusCheckRollup,url,author",
                "--limit",
                "50",
            ],
            capture_output=True,
            text=True,
            check=False,
        )
    except FileNotFoundError:
        print(f"::warning::gh CLI not installed; skipping PRs for {repo}", file=sys.stderr)
        return []

    if result.returncode != 0:
        print(
            f"::warning::gh pr list failed for {repo}: {result.stderr.strip()}",
            file=sys.stderr,
        )
        return []

    try:
        raw = json.loads(result.stdout or "[]")
    except json.JSONDecodeError as exc:
        print(f"::warning::could not parse gh output for {repo}: {exc}", file=sys.stderr)
        return []

    now = datetime.now(timezone.utc)
    out: list[dict[str, Any]] = []
    for pr in raw:
        created = pr.get("createdAt") or ""
        age_days: int | None = None
        if created:
            try:
                age_days = (now - datetime.fromisoformat(created.replace("Z", "+00:00"))).days
            except ValueError:
                age_days = None

        checks = pr.get("statusCheckRollup") or []
        failing = sum(
            1
            for c in checks
            if (c.get("conclusion") or "").upper() in {"FAILURE", "TIMED_OUT", "CANCELLED"}
        )

        out.append(
            {
                "number": pr.get("number"),
                "title": pr.get("title") or "",
                "createdAt": created,
                "ageDays": age_days,
                "mergeable": pr.get("mergeable") or "UNKNOWN",
                "isDraft": bool(pr.get("isDraft")),
                "headRef": pr.get("headRefName") or "",
                "failingChecks": failing,
                "url": pr.get("url") or "",
                "author": (pr.get("author") or {}).get("login") or "",
            }
        )
    return out


# ---------------------------------------------------------------------------
# Install-audit collection
# ---------------------------------------------------------------------------


def load_install_audit(audits_dir: Path, repo: str) -> dict[str, Any] | None:
    """Look up <audits_dir>/<short>.install-audit.json (where <short> = repo basename)."""
    short = repo.split("/", 1)[-1]
    candidate = audits_dir / f"{short}.install-audit.json"
    if not candidate.exists():
        return None
    try:
        return json.loads(candidate.read_text(encoding="utf-8"))
    except json.JSONDecodeError as exc:
        print(f"::warning::could not parse {candidate}: {exc}", file=sys.stderr)
        return None


def install_audit_summary(report: dict[str, Any] | None) -> dict[str, Any]:
    """Reduce install-audit report to fleet-status fields."""
    if not report:
        return {
            "available": False,
            "score": None,
            "maxScore": None,
            "verdict": None,
            "readiness": None,
            "checkCount": 0,
            "failingChecks": [],
        }
    # build_install_audit can return either fleet-level (with .repos[]) or single
    repos = report.get("repos") or []
    if repos:
        repo = repos[0]
    else:
        repo = report
    checks = repo.get("checks") or []
    failing = [c["id"] for c in checks if (c.get("status") or "") != "pass"]
    return {
        "available": True,
        "score": repo.get("score"),
        "maxScore": repo.get("maxScore"),
        "verdict": repo.get("verdict"),
        "readiness": repo.get("readiness"),
        "checkCount": len(checks),
        "failingChecks": failing,
    }


# ---------------------------------------------------------------------------
# Manual data
# ---------------------------------------------------------------------------


def load_json(path: Path, default: Any) -> Any:
    if not path.exists():
        return default
    try:
        return json.loads(path.read_text(encoding="utf-8"))
    except json.JSONDecodeError as exc:
        print(f"::warning::could not parse {path}: {exc}", file=sys.stderr)
        return default


# ---------------------------------------------------------------------------
# Markdown renderer
# ---------------------------------------------------------------------------


def render_markdown(data: dict[str, Any]) -> str:
    lines: list[str] = []
    lines.append("# Fleet Status")
    lines.append("")
    lines.append(
        "Snapshot of work across the 5 sibling repos: "
        "`agent-blackbox`, `ga`, `ix`, `tars`, `Demerzel`."
    )
    lines.append("")
    lines.append(
        "Generated by `.github/workflows/fleet-status.yml` "
        "(see also `/test/fleet` on the demos site)."
    )
    lines.append("")
    lines.append("---")
    lines.append("")

    # Section 1: Active PRs
    lines.append("## Active PRs")
    lines.append("")
    repos = data.get("repos") or []
    total_prs = sum(len(r.get("prs") or []) for r in repos)
    lines.append(f"_{total_prs} open PR(s) across {len(repos)} repos._")
    lines.append("")
    for r in repos:
        prs = r.get("prs") or []
        if not prs:
            lines.append(f"### {r['name']}")
            lines.append("")
            lines.append("_No open PRs._")
            lines.append("")
            continue
        lines.append(f"### {r['name']} ({len(prs)} open)")
        lines.append("")
        lines.append("| # | Title | Age | Mergeable | Failing checks | Author |")
        lines.append("|---|---|---:|---|---:|---|")
        for pr in prs:
            age = f"{pr['ageDays']}d" if pr.get("ageDays") is not None else "?"
            title = (pr.get("title") or "").replace("|", "\\|")[:70]
            mergeable = pr.get("mergeable") or "UNKNOWN"
            failing = pr.get("failingChecks") or 0
            num = pr.get("number")
            url = pr.get("url") or ""
            num_link = f"[#{num}]({url})" if url else f"#{num}"
            draft = " (draft)" if pr.get("isDraft") else ""
            lines.append(
                f"| {num_link} | {title}{draft} | {age} | "
                f"{mergeable} | {failing} | {pr.get('author','')} |"
            )
        lines.append("")

    # Section 2: Install-audit fleet
    lines.append("## Install-audit fleet score")
    lines.append("")
    lines.append("| Repo | Verdict | Score | Readiness | Failing checks |")
    lines.append("|---|---|---:|---|---|")
    for r in repos:
        a = r.get("installAudit") or {}
        if not a.get("available"):
            lines.append(f"| {r['name']} | — | — | — | _audit not available_ |")
            continue
        score = a.get("score")
        max_score = a.get("maxScore")
        score_str = f"{score}/{max_score}" if score is not None else "?"
        failing = ", ".join(a.get("failingChecks") or []) or "—"
        lines.append(
            f"| {r['name']} | {a.get('verdict','?')} | {score_str} | "
            f"{a.get('readiness','?')} | {failing} |"
        )
    lines.append("")

    # Section 3: Active initiatives
    lines.append("## Active initiatives")
    lines.append("")
    inits = data.get("initiatives") or []
    if not inits:
        lines.append("_No active initiatives recorded. Seed `state/fleet-status-initiatives.json`._")
    else:
        for init in inits:
            name = init.get("name") or "(unnamed)"
            phase = init.get("phase") or ""
            desc = init.get("description") or ""
            line = f"- **{name}**"
            if phase:
                line += f" — _{phase}_"
            if desc:
                line += f": {desc}"
            lines.append(line)
    lines.append("")

    # Section 4: Blockers per surface
    lines.append("## Blockers per surface")
    lines.append("")
    blockers = data.get("blockers") or []
    if not blockers:
        lines.append("_No blockers recorded. Seed `state/fleet-status-blockers.json`._")
    else:
        lines.append("| Surface | Hard blocker | Soft blocker |")
        lines.append("|---|---|---|")
        for b in blockers:
            surface = b.get("surface") or "?"
            hard = b.get("hard") or "—"
            soft = b.get("soft") or "—"
            lines.append(f"| {surface} | {hard} | {soft} |")
    lines.append("")

    # Footer
    lines.append("---")
    lines.append("")
    meta = data.get("meta") or {}
    lines.append(f"_Generated: {meta.get('generatedAt','?')} UTC_")
    sha = meta.get("commitSha")
    if sha:
        lines.append("")
        lines.append(f"_Commit: `{sha[:12]}`_")
    lines.append("")
    return "\n".join(lines)


# ---------------------------------------------------------------------------
# Orchestrator
# ---------------------------------------------------------------------------


def build(args: argparse.Namespace) -> dict[str, Any]:
    root = args.root.resolve()
    audits_dir = (args.install_audits_dir or root / "state/fleet-status-audits").resolve()

    initiatives_path = root / "state" / "fleet-status-initiatives.json"
    blockers_path = root / "state" / "fleet-status-blockers.json"

    initiatives = load_json(initiatives_path, [])
    blockers = load_json(blockers_path, [])

    repos_out: list[dict[str, Any]] = []
    for repo in args.repos:
        short = repo.split("/", 1)[-1]
        prs = fetch_prs(repo) if not args.skip_prs else []
        audit = load_install_audit(audits_dir, repo)
        repos_out.append(
            {
                "name": short,
                "fullName": repo,
                "prs": prs,
                "prCount": len(prs),
                "installAudit": install_audit_summary(audit),
            }
        )

    commit_sha = ""
    try:
        result = subprocess.run(
            ["git", "rev-parse", "HEAD"],
            capture_output=True,
            text=True,
            check=False,
            cwd=str(root),
        )
        if result.returncode == 0:
            commit_sha = result.stdout.strip()
    except FileNotFoundError:
        pass

    data = {
        "schemaVersion": "0.1",
        "meta": {
            "generatedAt": datetime.now(timezone.utc).isoformat(),
            "commitSha": commit_sha,
            "repoCount": len(repos_out),
            "totalPRs": sum(r["prCount"] for r in repos_out),
        },
        "repos": repos_out,
        "initiatives": initiatives,
        "blockers": blockers,
    }
    return data


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(prog="fleet-status-generate")
    parser.add_argument(
        "--repos",
        nargs="+",
        default=REPOS_DEFAULT,
        help="owner/name slugs to include in the fleet snapshot.",
    )
    parser.add_argument(
        "--install-audits-dir",
        type=Path,
        default=None,
        help="directory containing <repo-short>.install-audit.json files.",
    )
    parser.add_argument(
        "--root",
        type=Path,
        default=Path("."),
        help="GA repo root (defaults to current directory).",
    )
    parser.add_argument(
        "--skip-prs",
        action="store_true",
        help="skip the gh PR fetch (useful for local dry runs without network).",
    )
    parser.add_argument(
        "--out-json",
        type=Path,
        default=None,
        help="output JSON path. Defaults to public/fleet-status.json under the React app.",
    )
    parser.add_argument(
        "--out-md",
        type=Path,
        default=None,
        help="output markdown path. Defaults to state/fleet-status.md.",
    )
    args = parser.parse_args(argv)

    root = args.root.resolve()
    out_json = (
        args.out_json
        or root / "ReactComponents" / "ga-react-components" / "public" / "fleet-status.json"
    )
    out_md = args.out_md or root / "state" / "fleet-status.md"

    data = build(args)

    out_json.parent.mkdir(parents=True, exist_ok=True)
    out_json.write_text(json.dumps(data, indent=2, sort_keys=True) + "\n", encoding="utf-8")
    print(f"wrote {out_json}")

    out_md.parent.mkdir(parents=True, exist_ok=True)
    out_md.write_text(render_markdown(data), encoding="utf-8")
    print(f"wrote {out_md}")

    return 0


if __name__ == "__main__":
    sys.exit(main())
