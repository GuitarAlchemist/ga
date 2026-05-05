# state/handoffs/

Drop-zone for asynchronous notes between AI surfaces working in this repo
(Antigravity native AI ↔ Claude Code extension/CLI ↔ remote agents).

## Why this exists

Two AIs working in the same workspace can't read each other's chat history.
The file system is the cheapest coordination point — write a small markdown
note here, the other surface reads it on next ask.

This is intentionally low-tech: no daemon, no event bus, no schema beyond
the YAML frontmatter the bridge emits. If you need stronger guarantees
(ordering, ack), use a real PR or a tracked artifact under `state/quality/`.

## How to write a handoff

```powershell
pwsh Scripts/antigravity-bridge.ps1 -Handoff "Phase 2 drift wiring landed in PR #112. Tests at QaToolsScoreQualityDriftTests." -From claude-code
```

The `-From` field is one of:

- `claude-code` — Claude Code extension or CLI session
- `antigravity-native` — Antigravity's native AI panel
- `agent` — a remote scheduled CCR / cloud agent
- `human` — you, dropping a note for the AIs

The bridge auto-captures the current branch, the last commit, and a
working-tree-dirty line count so the receiving surface gets context, not
just prose.

## How to read handoffs

Sort by filename — names are timestamp-prefixed (`yyyy-MM-ddTHH-mm-ssZ-from.md`)
so newest is last. Each note is small (≤ 1 KB typical); a single `cat` or
`Read` is enough.

When the work referenced is fully landed (PR merged, snapshot promoted),
delete the note. This directory is a queue, not a log.

## Retention policy

- Notes are gitignored — they're working memory, not project artifacts.
- This README is tracked.
- If a handoff becomes load-bearing (someone needs it weeks later), promote
  it to `docs/learnings/` or `docs/plans/` with a real filename.
