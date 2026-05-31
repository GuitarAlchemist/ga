# state/quality/sentrux-next-steps — Refactor Prescriptions

Append-only archive of **actionable refactor recommendations** produced by
the `/sentrux-next-steps` skill
(see `.claude/skills/sentrux-next-steps/SKILL.md`) and rendered on the
Sentrux tab at `/test#dev/sentrux` by `SentruxNextStepsCard`.

Each invocation writes one dated artifact and refreshes `latest.md`:

```
state/quality/sentrux-next-steps/
├── README.md                       ← this file
├── SCHEMA.json                     ← JSON Schema (sentrux-next-steps-v1) for the frontmatter
├── latest.md                       ← copy of the most recent dated artifact (what the card renders)
├── 2026-05-24T16-22-08Z.md         ← dated prescription (one per skill run)
└── 2026-05-25T09-00-00Z.md         ← next run (history retained)
```

Date stamps use the format `YYYY-MM-DDTHH-MM-SSZ` (colons replaced with
hyphens to stay path-safe on Windows). Files sort chronologically so
`ls -t | head -1` gives the latest prior to a new run.

## Why this exists

Closes the **"raw metrics, no prescription"** gap. The Sentrux tab
already surfaces `quality_signal`, cycle counts, and coverage — useful
data but not directive. Operators internally translate those numbers
into "I should refactor module X" but the translation is invisible and
not shared across teammates / sessions.

This skill makes the translation:

- **Visible** — the dashboard renders the prescription at the top of the
  Sentrux tab, above the raw-metrics cards.
- **Persistent** — every run is archived; the next run reads the prior
  one and acknowledges what changed (`carried over from <date>`).
- **Ranked** — leverage-weighted by structural impact × business value ×
  test risk, so the operator can pick the top item with confidence.

Pairs with `/test-plan` (forward-looking, per-PR) and `/grade-last-pr`
(backward-looking, per-merge) to form a triangle of quality skills:

| Skill | Scope | Direction | Cadence |
|---|---|---|---|
| `/test-plan` | one PR | forward (before merge) | per PR open |
| `/grade-last-pr` | one merge | backward (after merge) | per PR merge |
| `/sentrux-next-steps` | whole codebase | strategic (multi-PR) | on demand / daily |

## Schema (frontmatter: sentrux-next-steps-v1)

Each artifact is a single markdown file with YAML frontmatter. The
middleware (`/dev-data/sentrux/next-steps`) parses the frontmatter to
populate the inputs chip row and the timestamp, then renders the
body via `react-markdown`.

| Field | Type | Notes |
|---|---|---|
| `schema` | string | Literal `"sentrux-next-steps-v1"`. |
| `generated_at` | string | RFC3339 UTC. When the prescription was written. |
| `generator` | string | Model name (e.g. `claude-opus-4-7`). |
| `inputs.quality_signal` | number | Sentrux composite score (0-10000). |
| `inputs.cycles` | integer | Cycle count from DSM at generation time. |
| `inputs.coverage_pct` | number | Coverage percentage (0-100). |
| `inputs.bottleneck` | string | Sentrux-reported bottleneck dimension. |
| `inputs.value_annotations` | integer | Count of `@ai:business-value` annotations consulted. |
| `inputs.smell_annotations` | integer | Count of `@ai:smell` annotations consulted. |

Validate against `SCHEMA.json`:

```bash
npx -y ajv-cli@5 validate -s state/quality/sentrux-next-steps/SCHEMA.json \
  -d /tmp/frontmatter.json
```

(Extract the frontmatter to `/tmp/frontmatter.json` first — the JSON Schema
validates the parsed YAML head, not the markdown body.)

## Body layout

```
# Sentrux Next Steps — <date-Z>

> Quality signal: <N> / 10,000 · <N> cycles · <X>% coverage
> Bottleneck: <name> · Top root cause: <name> (<score>)

## 1. <Imperative title>

**Impact:** ...
**Effort:** S | M | L — ...
**Where:** `<paths>`
**Starter:** <3-5 sentences>
**Why now:** <value/risk tie-in>

## 2. ...
...

## Inputs
- sentrux health @ ...
- N rule violations
- N cycles in DSM
- ...
```

5-10 recommendations max. The template lives in
`.claude/skills/sentrux-next-steps/SKILL.md` §4.

## What goes here vs elsewhere

| Question | File |
|---|---|
| "What should I refactor next, across the whole codebase?" | `state/quality/sentrux-next-steps/latest.md` (this directory) |
| "What tests should this open PR add?" | `state/quality/test-plans/<head-sha>.md` |
| "Did the last merged PR deliver its stated intent?" | `state/quality/pr-grades/<merge-sha>.json` |
| "What are the current sentrux measurements?" | live from sentrux MCP via `/dev-data/sentrux/*` (not archived here) |

## Retention policy

Keep all dated artifacts indefinitely. Each is < 20 KB; even 1,000 runs
(daily for ~3 years) is < 20 MB. The history is useful for grading
whether prior recommendations were taken — operators / future skills
can `git log` the file pattern and see which prescriptions led to
`quality_signal` movements.

`latest.md` is overwritten on each run — that's intentional. It's the
"front door" the dashboard hits; archival lives in the dated files.

## Related

- `.claude/skills/sentrux-next-steps/SKILL.md` — the producer; full
  heuristic in §3.
- `ReactComponents/ga-react-components/src/components/Sentrux/SentruxNextStepsCard.tsx`
  — the consumer (dashboard surface).
- `ReactComponents/ga-react-components/vite.config.ts` — the middleware
  that serves `latest.md` to the card.
- `state/quality/test-plans/README.md` — sibling skill's directory
  contract (parallel layout).
- `../README.md` — parent `state/quality/` directory contract.
