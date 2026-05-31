# state/digests/

Session-digest artifacts. Captures the **meaningful state of a session**
(current cursor, in-flight work, live hypotheses, open questions,
do-NOT-carry-forward) so the next session — including one started after
auto-compaction — can re-enter without re-discovering context cold.

## Structure

```
state/digests/
├── README.md           — this file (tracked)
├── latest.md           — current digest (gitignored, single-slot)
└── archive/            — historical digests (gitignored)
    └── <ts>-<sessionId>.md
```

## Who writes

| Writer | Trigger | Content |
|---|---|---|
| `/digest` skill | Model-invoked at breakpoints | Rich — all sections populated |
| `Scripts/precompact-digest.ps1` (PreCompact hook) | Auto on compaction | Metadata only (fallback if /digest didn't run) |

## Who reads

- **`Scripts/sessionstart-digest.ps1`** (SessionStart hook) — emits
  `latest.md` to stdout. Claude Code captures SessionStart stdout and
  injects it as `additionalContext` for the model on session start.

## Distinctions from adjacent artifacts

| Artifact | Captures | Lifetime |
|---|---|---|
| `state/digests/latest.md` | **state** — current cursor, in-flight, hypotheses | per session, gitignored |
| `docs/solutions/<cat>/...` | **surprises** — non-obvious learnings | permanent, tracked |
| `state/handoffs/` | cross-surface notes between AI surfaces | until consumed, gitignored |
| `MEMORY.md` (user-global) | cross-session behavioural guidance | permanent, user-global |

See `.claude/skills/digest/SKILL.md` for the schema + invocation rules.

## Retention policy

- `latest.md` overwrites on each /digest invocation or PreCompact fallback.
- `archive/` accumulates per-compaction snapshots indefinitely. Periodic
  cleanup is the operator's responsibility — typical: keep last 30 days.
