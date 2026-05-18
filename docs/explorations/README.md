# docs/explorations/

Durable findings from multi-session explorations. Promoted from
`state/digests/latest.md` when the session uncovered something worth
keeping past the next compaction.

## When to write one

A session's digest is enough for the next session to re-enter context.
Explorations are for the slower-moving questions:

- Multi-LLM debates that produced substantive corrections
- Failed approaches worth remembering so we don't re-attempt
- Architectural what-ifs that survived a tribunal but didn't ship yet
- Cross-repo investigations spanning ga + ix + Demerzel + tars

If the finding belongs to a single commit/PR, it lives in
`docs/solutions/` (decision record) instead. Explorations are for the
diffuse pre-decision phase.

## Naming

`YYYY-MM-DD-<short-topic-kebab>.md` — e.g. `2026-05-17-agent-blackbox-multi-llm-debate.md`.

## Template

```markdown
---
date: YYYY-MM-DD
topic: <short>
agents_involved: <count>
duration: <hours>
outcome: <kept | abandoned | partial>
---
# Exploration: <topic>

## What I was trying to figure out
## What I tried
## What worked
## What didn't work
## What I'd carry forward
## What NOT to re-attempt (and why)
```

The last section is load-bearing. The whole point of writing this down
is to keep future sessions from re-running the same dead ends.

## Lifecycle

- **Write** at the end of a session when a finding survives `/digest`.
- **Promote** to `docs/solutions/` if a follow-up decision crystallizes
  (add YAML frontmatter per `docs/solutions/SCHEMA.md`).
- **Archive** to `docs/archive/explorations/` if the topic is fully
  obsolete — but only after confirming nothing references it.

## Related surfaces

- `state/digests/latest.md` — single-session continuity (auto-compacted).
- `docs/solutions/` — decision records tied to commits/PRs.
- `BACKLOG.md` → `docs/plans/` — forward-looking work.
- `docs/methodology/` — durable how-we-work patterns.

Explorations sit between digest (ephemeral) and solutions (committed
decision). They are the "we thought hard about this" surface.
