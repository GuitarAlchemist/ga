# Per-Session Same-Agent State (`.claude/local/state.md`)

Harness engineering plan item #3 — adopted from [pablomarin/claude-codex-forge](https://github.com/pablomarin/claude-codex-forge).

## What this solves

Today, if you open a second Claude Code window in parallel on this repo
(or come back after a long break), the new session has no idea you were
6 PRs deep into a dashboard refactor. The conversation buffer is gone,
and the [`/digest`](../skills/digest/SKILL.md) system captures
*session-end* state but not the mid-session "I'm in the middle of X,
blocked on Y, about to start Z" cursor.

`.claude/local/state.md` is that cursor. It survives auto-compaction
(disk, not conversation buffer), survives session end, and is read
manually at session start when you (or the next agent) want to
re-orient.

## How to use it

1. Create the (gitignored) local directory and copy the template into place:
   ```bash
   mkdir -p .claude/local
   cp .claude/local-template/state.md.template .claude/local/state.md
   ```
   The `mkdir -p` is required on a fresh checkout — `.claude/local/` is
   gitignored, so it does not exist until you create it. Without the
   `mkdir`, the `cp` fails with `No such file or directory`.
2. Edit it as you work. The four sections (Done / Now / Next /
   Blocked-by) are the minimum useful schema; Hypotheses + Notes are
   optional carry-forward.
3. At the start of a new session, open `.claude/local/state.md`
   (or paste its content into the conversation) to re-orient.

## Why gitignored

Per-developer, per-machine. Your "Now" is not someone else's "Now."
Cross-agent baton-passing is a different mechanism — that one is
`state/handoffs/` via `Scripts/antigravity-bridge.ps1` (see CLAUDE.md
"AI surfaces in this workspace"), and it IS tracked because the
hand-off is a shared artifact between agents.

| Pattern | File | Tracked? | Purpose |
|---|---|---|---|
| **Per-session state** | `.claude/local/state.md` | No | Same-agent cross-session continuity |
| **Cross-agent handoff** | `state/handoffs/<date>-*.md` | Yes | Shared baton between Claude / Codex / Antigravity |
| **Session digest** | `state/digests/latest.md` | Yes | Session-end snapshot, auto-injected on next start |

## Schema

See `state.md.template` in this directory. The frontmatter carries
`schema: claude-local-state-v1` + session start time + branch. Sections
are:

- **Done** — shipped work this session (append-only)
- **Now** — active workstream (1-2 items max)
- **Next** — queued / on-deck
- **Blocked by** — external waits (human-only unsticks)
- **Hypotheses** — working theories worth checking
- **Notes** — anything else worth carrying forward

## Future work (not in this PR)

- A `/state` slash command for read / append / clear
- A SessionStart hook that auto-injects `.claude/local/state.md` if
  present, alongside the digest auto-inject
- Schema validation via JSON Schema (the frontmatter is YAML)
- A "stale state" nudge if the file's `updated` timestamp is more than
  N hours old at session start
