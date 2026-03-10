---
title: "Windows git packed-refs silent commit failure — branch HEAD does not advance"
date: 2026-03-10
problem_type: "runtime-errors"
component: "git / Windows CI"
symptoms:
  - "git commit exits 0 but git log shows no new commit on the branch"
  - "HEAD did not advance after commit"
  - "Commit object exists in object store but no ref points to it (dangling)"
  - "Staged changes reappear clean but log is stale"
tags:
  - "git"
  - "windows"
  - "ci"
  - "packed-refs"
  - "branch-corruption"
severity: "high"
related_docs:
  - "docs/solutions/runtime-errors/fsharp-module-init-closure-registry.md"
---

# Windows git `packed-refs` silent commit failure

## Symptoms

- `git commit -m "..."` exits with code 0 and prints the normal commit summary, but `git log` does not show the new commit on the current branch
- `git log HEAD~1..HEAD` returns empty — HEAD did not advance
- `git fsck` reports the commit object as unreachable/dangling
- On Windows CI agents, a pipeline step that commits then reads back the SHA gets a stale value
- `git log --all` shows the commit on no branch

## Diagnosis

**Step 1 — Check if the branch is only in `packed-refs`:**
```bash
BRANCH=$(git branch --show-current)
grep "$BRANCH" .git/packed-refs && echo "Branch in packed-refs"
ls .git/refs/heads/"$BRANCH" 2>/dev/null || echo "No loose ref file"
```

If the branch appears in `packed-refs` AND there is no loose file at `.git/refs/heads/<branch>`, this bug is the likely cause.

**Step 2 — Verify HEAD didn't move:**
```bash
BEFORE=$(git rev-parse HEAD)
git commit -m "probe"
AFTER=$(git rev-parse HEAD)
[ "$BEFORE" = "$AFTER" ] && echo "Silent failure confirmed"
```

**Step 3 — Find the dangling commit:**
```bash
git fsck --unreachable 2>&1 | grep commit
```

## Root Cause

When a branch ref lives only in `.git/packed-refs` (i.e., no loose file at `.git/refs/heads/<branch>`), git's ref-update mechanism on Windows can silently fail to create the loose ref file after a commit. The porcelain `git commit` command calls the internal `update_ref()` function, which on Windows may hit NTFS file-locking or path-handling issues and return success without actually writing the ref.

This is most common when:
- `git pack-refs --all` was run (moves all loose refs into `packed-refs`)
- Automated tooling created the branch without writing a loose ref
- The repository was cloned in a way that bypassed normal loose ref creation

## Workaround

Use git plumbing commands directly to write the commit object and update the ref file, bypassing the porcelain path:

```bash
#!/usr/bin/env bash
# safe-commit.sh — bypasses the ref-update path that silently fails on Windows packed-refs
# Usage: bash safe-commit.sh "commit message"

set -euo pipefail

MESSAGE="${1:?Usage: safe-commit.sh <message>}"
BRANCH=$(git branch --show-current)

TREE=$(git write-tree)          # snapshot the index
PARENT=$(git rev-parse HEAD)    # current HEAD SHA
SHA=$(git commit-tree "$TREE" -p "$PARENT" -m "$MESSAGE")  # create commit object

# Write loose ref directly — bypasses the broken ref-update code path
mkdir -p .git/refs/heads
printf '%s\n' "$SHA" > .git/refs/heads/"$BRANCH"

echo "[safe-commit] $BRANCH -> $SHA"
```

**Verify:**
```bash
git log --oneline -3   # new commit at top
git status             # clean working tree
git fsck --no-dangling # no errors
```

## Prevention

### 1. Unpack refs before any scripted commit sequence on Windows

Add to the top of CI scripts that commit on Windows agents:
```bash
git for-each-ref --format='%(refname) %(objectname)' refs/heads | \
  while read refname sha; do
    loose=".git/${refname}"
    mkdir -p "$(dirname "$loose")"
    [ -f "$loose" ] || printf '%s\n' "$sha" > "$loose"
  done
```

### 2. Pre-commit hook guard

Add to `.git/hooks/pre-commit` (included in `pwsh Scripts/install-git-hooks.ps1`):
```bash
BRANCH=$(git branch --show-current)
if grep -q " refs/heads/$BRANCH$" .git/packed-refs 2>/dev/null \
   && [ ! -f ".git/refs/heads/$BRANCH" ]; then
  echo "Unpacking branch ref to avoid Windows packed-refs silent failure..."
  SHA=$(grep " refs/heads/$BRANCH$" .git/packed-refs | awk '{print $1}')
  mkdir -p .git/refs/heads
  printf '%s\n' "$SHA" > ".git/refs/heads/$BRANCH"
fi
```

### 3. Assert HEAD advanced in CI pipelines

```bash
BEFORE=$(git rev-parse HEAD)
git add <files>
bash scripts/safe-commit.sh "chore: update generated files"
AFTER=$(git rev-parse HEAD)
[ "$BEFORE" != "$AFTER" ] || { echo "ERROR: Commit silent failure"; exit 1; }
```

### 4. Windows git version

The bug is most prevalent in git-for-windows < 2.43. Pin the version in CI:
```yaml
# GitHub Actions
- uses: git-for-windows/setup-git@v1
  with:
    version: '2.47.0.windows.2'
```

### 5. Windows git config

```bash
git config --global core.fscache true       # reduce NTFS overhead
git config --global core.longpaths true     # avoid path-length failures
git config --global core.autocrlf false     # prevent CRLF from interfering with ref files
```

## CI/CD Validation Checklist

| Check | Command | Expected |
|---|---|---|
| git version | `git --version` | >= 2.43.0.windows.1 |
| Loose ref exists | `ls .git/refs/heads/<branch>` | File present before committing |
| HEAD advanced | Compare rev-parse before/after | Different SHAs |
| No dangling commits | `git fsck --no-dangling` | No output |

## Relation to GA Project

This bug surfaced when automated commit sequences (e.g., background subagents writing generated files) ran on the `feat/chatbot-orchestration-extraction` branch on a Windows development machine. The branch existed only in `packed-refs` because of a prior `git pack-refs` run, causing `git commit` to silently create dangling objects. The `safe-commit.sh` plumbing workaround was used to land all commits successfully.
