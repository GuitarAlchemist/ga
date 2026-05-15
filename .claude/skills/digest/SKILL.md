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

Git metadata is gravy. The value is in the **content sections** + the
**success_criteria** field (Karpathy R4: "task complete != goal achieved").

```yaml
---
schema_version: 1
session_id: <session_id, propagated from PreCompact stdin when known>
written_at: <RFC3339 UTC>
trigger: digest-skill
branch: <git branch>
head_sha: <short SHA>
head_subject: <commit subject>
open_pr: <"#N" or null>
last_model_update: <RFC3339 UTC>
success_criteria:
  - criterion: "<testable assertion for the Next action>"
    status: pending | in-progress | achieved | abandoned
    evidence: "<file:line | PR# | metric path | null>"
---

# Session digest — <branch> @ <sha>

## Next action

ONE imperative sentence describing the next concrete step. If multiple
things are queued, pick the one with highest blast radius. If nothing is
queued, write `Wait for user direction on <specific question>`.

The next action must map 1:1 to entries in the `success_criteria`
frontmatter array — each criterion testable, not vibes.

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

## Prior success criteria status (Karpathy R4)

When a prior digest exists, this section reports the status delta:

```
- ✅ achieved: <prior criterion> — evidence: <file:line | commit | PR>
- ⏳ in-progress: <prior criterion> — last touched: <where>
- ⛔ abandoned: <prior criterion> — reason: <one sentence>
```

Carrying forward only the still-pending or in-progress criteria into the
new `success_criteria` frontmatter prevents drift.
```

## How to run

1. **Read existing** `state/digests/latest.md` if present — preserve content
   sections still current; rewrite stale ones. Don't drop "Do NOT carry
   forward" entries from the prior digest unless they're definitively
   resolved.
2. **Karpathy R4 — review prior success criteria** if the prior digest had
   them. For each: did the work since complete it? Mark `achieved` with
   evidence, `in-progress` with where it's parked, or `abandoned` with a
   one-sentence reason. Build the "Prior success criteria status" section.
3. **Capture git state** via Bash:
   ```bash
   git rev-parse --abbrev-ref HEAD
   git rev-parse --short HEAD
   git log -1 --format='%s'
   gh pr view --json number 2>$null
   ```
4. **Synthesize the content sections** from your current working context.
   The first line of "Next action" is the most important sentence in the
   digest — make it imperative and concrete. Then derive 1–3 testable
   `success_criteria` entries from it (Karpathy R4: every Next action
   declares verifiable criteria, not "task complete").
5. **Write** the full digest to `state/digests/latest.md` (overwrite).
   Set `trigger: digest-skill` and `last_model_update` to current RFC3339 UTC.
6. **Reset the activity counter** so the staleness nudge starts fresh:
   ```bash
   rm -f state/digests/.activity-counter
   ```
7. **Validate** the written digest against the schema (Karpathy R11:
   runtime rejects schema mismatches):
   ```bash
   pwsh -NoProfile -File Scripts/digest-validate.ps1
   ```
   Non-zero exit = the digest you just wrote is malformed; fix and rewrite.
8. **Report** one line to the session:
   `Digest updated: <branch>@<sha> · next: <one-line> · criteria: <N>`.

## Driving criteria autonomously with `/goal`

After writing the digest, **consider `/goal <condition>` (native Claude
Code v2.1.139+) for substantial autonomous work**. `/goal` mechanizes
Karpathy R4: a small fast model evaluates after every turn whether the
condition holds and either fires another turn or clears the goal.

Use `/goal` when the Next action has:

- A verifiable end state (build green, tests pass, file count under
  budget, an empty queue)
- 5+ minutes of expected autonomous work
- An evaluator-checkable result (the check happens against what Claude
  surfaces in the transcript — no tool calls)

Skip `/goal` for short tasks (overhead isn't worth it), visual/UX
judgement calls, or anything that needs human taste to call "done."

When `/goal` lands a "yes," the achievement appears in the transcript
with the condition + duration. The next `/digest` invocation should
read this and mark the corresponding `success_criteria` entry as
`achieved` with the `/goal` evidence.

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
