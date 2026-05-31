# state/quality/council — Council verdict archive

Produced by the `/council` skill (`.claude/skills/council/SKILL.md`). One
JSON file per convened council, named by the PR head SHA at convocation
time. Format is `council-verdict-v1` — see `SCHEMA.json` in this directory.

## When a file gets written

Only when `/council` is invoked **and** the PR touches a one-way-door path
enumerated in the skill (OPTIC-K dim, contract schema, public controller,
SPA route, installer script, pricing). PRs that don't touch a door produce
no artifact and exit clean.

This is the **opt-in escalated gate**, not the daily review gate. Routine
review verdicts live elsewhere (see `state/chatbot-reviews/` for tribunal
verdicts, and PR comments for `/octo:review` output).

## File layout

```
state/quality/council/
├── README.md                      # this file
├── SCHEMA.json                    # JSON Schema for council-verdict-v1
└── <head-sha>.json                # one verdict per convened council
```

`<head-sha>` is the 40-char Git SHA the council reviewed. A new push to
the PR produces a new SHA and a new artifact; the old one stays for
history. This is intentional — the verdict pins to the commit it judged.

## Retention

Verdicts are append-only history. Keep them indefinitely:

- They are tiny (each file is a few KB).
- They form the audit trail for one-way-door decisions, which is the whole
  point of the gate. Deleting them defeats the purpose.
- If the directory ever grows past ~1k files we'll revisit (unlikely —
  one-way-door PRs are rare by construction).

## Aggregation rule

Mirrored in the skill and in `SCHEMA.json`'s `final_verdict` enum:

- Any advisor returns `block` → final = `block`.
- Else any returns `request_changes` → final = `request_changes`.
- Else → final = `approve`.

The chair's `chair_synthesis` field is prose, not a vote. It explains the
reasoning behind the deterministic aggregation.

## Related

- `.claude/skills/council/SKILL.md` — the producer.
- `docs/plans/2026-05-23-arch-harness-engineering-adoption-plan.md` — the
  source plan (item M6).
- `state/quality/README.md` — overall quality-snapshot directory layout.
