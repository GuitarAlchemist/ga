---
name: digest
description: Capture meaningful session state (current cursor, in-flight work, live hypotheses, open questions, do-NOT-carry-forward) to state/digests/latest.md so the next session — including one after auto-compaction — can re-enter without re-discovering context cold. Distinct from /learnings (which captures surprises).
allowed-tools: Bash, Read, Write
last_verified: 2026-05-14
---

# /digest

Captures the **meaningful state of the current session** to
`state/digests/latest.md`. The `Scripts/sessionstart-digest.ps1` hook reads
it on the next session start and emits it as `additionalContext` for the
model. Pairs with `Scripts/precompact-digest.ps1` which provides a
metadata-only fallback when /digest isn't invoked before auto-compaction.

## When to run

- **Before compaction is imminent** (context feels >60% full).
- **At a natural breakpoint** — after finishing a feature, before a risky
  operation, before handing off to another agent.
- **Before launching long-running background work** so the digest captures
  what you expect that work to do.

**Do NOT** invoke on every message or tool call. The digest is for
*meaningful state changes*, not transcript capture.

## What it captures

Git metadata is gravy. The value is in the **content sections**.

```yaml
---
schema_version: 1
session_id: <session_id, propagated from PreCompact stdin when known>
written_at: <RFC3339 UTC>
trigger: digest-skill | precompact-hook-fallback
branch: <git branch>
head_sha: <short SHA>
head_subject: <commit subject>
open_pr: <"#N" or null>
last_model_update: <RFC3339 UTC>
---

# Session digest — <branch> @ <sha>

## Next action

ONE imperative sentence describing the next concrete step. If multiple
things are queued, pick the one with highest blast radius. If nothing is
queued, write `Wait for user direction on <specific question>`.

## In-flight

Bulleted list of work currently mid-execution. For each item: the
file/feature, the current step number out of total, and the immediate
next sub-step.

## Live hypotheses

Bulleted list of working hypotheses the next session should inherit.
"X is the bottleneck because Y" / "If we change A, B should follow."
These are *unconfirmed*; do not promote to MEMORY.md until validated.

## Open questions

Numbered. Questions you would ask the user if they walked in right now.

## Do NOT carry forward

**Highest-leverage field.** Things the next session must NOT re-propose:
rejected designs, abandoned approaches, paths the user explicitly closed.
Without this, a fresh model context will silently re-derive bad ideas you
already filtered.
```

## How to run

1. **Read existing** `state/digests/latest.md` if present — preserve content
   sections still current; rewrite stale ones. Don't drop "Do NOT carry
   forward" entries from the prior digest unless they're definitively
   resolved.
2. **Capture git state** via Bash:
   ```bash
   git rev-parse --abbrev-ref HEAD
   git rev-parse --short HEAD
   git log -1 --format='%s'
   gh pr view --json number 2>$null
   ```
3. **Synthesize the content sections** from your current working context.
   The first line of "Next action" is the most important sentence in the
   digest — make it imperative and concrete.
4. **Write** the full digest to `state/digests/latest.md` (overwrite).
5. **Report** one line to the session:
   `Digest updated: <branch>@<sha> · next: <one-line>`.

## Anti-patterns

- **Transcript capture.** "We did X, then Y, then Z." Git log is the
  transcript. Write the cursor, not the path.
- **Empty digest.** "Next action: continue" with no In-flight is noise.
  If nothing is in flight, don't write — the prior digest still applies.
- **Over-eager invocation.** Re-writing 10 times per session dilutes
  signal.
- **Forgetting "Do NOT carry forward."** Always populate, or explicitly
  write "none". Empty = signal lost.
- **Confusing with /learnings.** /learnings captures *surprises* (non-
  obvious facts worth grep-finding in 3 months). /digest captures *state*
  (the cursor right now). They compose — both can fire in the same
  session.

## Related

- [[reference-cherny-learnings-ritual]] / `/learnings` — surprises, not state.
- `Scripts/precompact-digest.ps1` — automatic fallback on PreCompact event.
- `Scripts/sessionstart-digest.ps1` — reads `latest.md` back on session start.
- `state/digests/README.md` — directory layout + retention policy.
- `.claude/agent-memory/octo-personas-context-manager/project_session_digest_design.md` — the original design doc.
