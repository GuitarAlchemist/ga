"""GitHub API client for issue and CI/CD status fetching."""

from __future__ import annotations

import os

import httpx

GITHUB_OWNER = "GuitarAlchemist"
GITHUB_REPOS = ["ga", "Demerzel", "ix", "tars"]
GITHUB_TOKEN = os.environ.get("GITHUB_TOKEN")
GITHUB_HEADERS = {
    "Accept": "application/vnd.github.v3+json",
    **({"Authorization": f"Bearer {GITHUB_TOKEN}"} if GITHUB_TOKEN else {}),
}


async def fetch_open_issues(repos: list[str] | None = None) -> list[dict]:
    """Fetch open issues from org repos."""
    issues = []
    async with httpx.AsyncClient(timeout=15.0) as client:
        for repo in repos or GITHUB_REPOS:
            try:
                resp = await client.get(
                    f"https://api.github.com/repos/{GITHUB_OWNER}/{repo}/issues",
                    params={"state": "open", "per_page": 10},
                    headers=GITHUB_HEADERS,
                )
                if resp.status_code == 200:
                    for i in resp.json():
                        i["repo"] = repo
                        issues.append(i)
            except Exception:
                continue
    return issues


async def fetch_ci_status(repos: list[str] | None = None) -> list[dict]:
    """Fetch recent CI workflow runs."""
    runs = []
    async with httpx.AsyncClient(timeout=15.0) as client:
        for repo in repos or GITHUB_REPOS:
            try:
                resp = await client.get(
                    f"https://api.github.com/repos/{GITHUB_OWNER}/{repo}/actions/runs",
                    params={"per_page": 5, "status": "completed"},
                    headers=GITHUB_HEADERS,
                )
                if resp.status_code == 200:
                    for r in resp.json().get("workflow_runs", []):
                        r["repo"] = repo
                        runs.append(r)
            except Exception:
                continue
    return runs
