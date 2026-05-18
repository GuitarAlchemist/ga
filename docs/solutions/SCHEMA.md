# docs/solutions/ — YAML frontmatter convention

Decision-record fields for new entries under `docs/solutions/`. The
existing `module / tags / problem_type` fields stay; this schema
**extends** them with four optional decision-record fields. Existing
files are NOT being retroactively edited.

## Schema

```yaml
---
# --- Existing fields (still required) ---
module: <subsystem>                 # e.g. GA.Business.ML, GaChatbot.Api
tags: [<tag1>, <tag2>]              # free-form, searchable
problem_type: <decision | bug-fix | learning | architectural>

# --- New decision-record fields (opt-in, additive) ---
decision: <what was chosen>         # one-line summary of the chosen path
rejected: [<alt1>, <alt2>]          # alternatives we considered and dropped
reason: <why this choice>           # the load-bearing "because" sentence
date_decided: YYYY-MM-DD            # when the choice locked in
---
```

## When to add the new fields

Required when `problem_type: decision` or `problem_type: architectural`.
Optional but encouraged on `bug-fix` and `learning` entries that
contain a "we picked X over Y" moment.

## Why this exists

Bug-fix-only records answer "what broke and how we fixed it." Decision
records answer "what did we choose and what did we close off." The
second question is the load-bearing one for future agents — without
`rejected` + `reason`, a future session re-discovers the rejected
alternative and may re-attempt it.

Per the Karpathy + Cherny instrumentation rule: one-way doors (OPTIC-K
dims, schema changes, public APIs, pricing, positioning) require
explicit sign-off. `decision` + `rejected` + `reason` is the minimum
artifact that makes a one-way door legible after the fact.

## Example

```yaml
---
module: GA.Business.ML.Embeddings
tags: [optic-k, embeddings, schema]
problem_type: decision
decision: Add ROOT partition to OPTIC-K (124 → 240 dims via OPTK compact form)
rejected: [keep 124-dim, separate root-embedding model, learned positional encoding]
reason: ROOT partition gives the agent direct pitch-class anchoring without breaking the 124-dim inference path; OPTK compact form preserves backward compat.
date_decided: 2026-04-19
---
```

## Relationship to other surfaces

- `docs/explorations/` — pre-decision exploration; promotes to a
  decision record here once the choice locks in.
- `docs/plans/` — forward-looking proposals; the resulting decision
  lands here when the plan ships.
- `CLAUDE.md` "Session-learned rules" — operator-level corrections;
  not architectural decisions.
