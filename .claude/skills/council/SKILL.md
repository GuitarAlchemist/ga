---
name: council
description: Opt-in pre-merge gate that convenes a virtual council of specialist sub-agents for one-way-door PRs (schema changes, public APIs, OPTIC-K dim changes, pricing). Adopts the claude-codex-forge council pattern. Use sparingly — only for high-stakes decisions the team would lose sleep about reverting. Invoked as `/council <PR#>` or `/council` for the current branch's open PR.
allowed-tools: Read, Write, Bash, Grep, Glob, Agent
last_verified: 2026-05-23
harness_item: 6
karpathy_rule: R-instrument-and-log-one-way-doors
---

# /council

A **virtual council** of specialist sub-agents convened to review a PR that
touches a known one-way door. Inspired by [claude-codex-forge](https://github.com/pablomarin/claude-codex-forge)'s
`/council` pattern — five voices, one chair, one synthesized verdict, written
to `state/quality/council/<head-sha>.json` and posted as a PR comment.

This is **opt-in by design**. It does not auto-fire on every PR. The operator
invokes it on PRs they would lose sleep about reverting. For routine review,
use `/octo:review` or the existing `chatbot-iterate` tribunal gate.

## When to run

Invoke `/council` (or `/council <PR#>`) when the PR touches one of:

- **`Common/GA.Business.ML/Embeddings/EmbeddingSchema.cs`** — OPTIC-K
  dimension changes. Locked field; ripples through every cached voicing
  index and every cross-repo consumer.
- **`docs/contracts/*.contract.md`** / **`docs/contracts/*.schema.json`** —
  cross-repo contracts (qa-verdict, optick-sae-artifact, optick-weights-config,
  ga-dsl-eval, ga-loop-driver, overseer-halt-marker, digest-schema). Once
  shipped, both producer and consumer crates pin to the hash.
- **`Apps/ga-server/GaApi/Controllers/*.cs`** — public HTTP API surface.
  Renaming routes or changing response shapes breaks third-party callers
  + the React SPAs.
- **`ReactComponents/ga-react-components/src/main.tsx`** — route
  definitions. URL changes break bookmarks, links, and embed iframes.
- **`Scripts/install-ga-service.ps1`** (and other `install-*` /
  `uninstall-*` scripts) — Windows-service installer changes that affect
  already-deployed boxes.
- Pricing / billing code — reserved pattern; nothing matches today.

If no one-way-door path is touched, the skill exits early with
`no council required`. The chair never convenes for ordinary code.

## How to run

### Step 1 — Resolve the PR

```bash
# If invoked as /council <num>, use that.
# Otherwise, resolve from current branch.
PR_NUM="${1:-$(gh pr view --json number --jq .number)}"
if [ -z "$PR_NUM" ]; then
  echo "No PR found for current branch and no PR number passed. Aborting."
  exit 1
fi

gh pr view "$PR_NUM" --json title,body,baseRefName,headRefName,headRefOid,files,additions,deletions,url \
  > /tmp/council-pr-$PR_NUM.json
```

### Step 2 — Detect one-way-door touches

Read the `files[].path` array from the PR JSON and test each against the
glob patterns below. Build the `one_way_door_paths` list.

```text
Common/GA.Business.ML/Embeddings/EmbeddingSchema.cs
docs/contracts/*.contract.md
docs/contracts/*.schema.json
Apps/ga-server/GaApi/Controllers/*.cs
ReactComponents/ga-react-components/src/main.tsx
Scripts/install-*.ps1
Scripts/uninstall-*.ps1
```

If the list is empty:

```text
No one-way-door paths touched on PR #<N>. Council not required.
Routine review path: /octo:review or chatbot-iterate.
```

…and **exit clean**. Do not write an artifact for skipped PRs.

### Step 3 — Convene the council (only on door touches)

Spawn five sub-agents in parallel via the `Agent` tool. Each receives the
same brief plus a focus area.

```text
Brief sent to every advisor:

  PR: #<N> — <title>
  URL: <url>
  Base → Head: <baseRefName> → <headRefName> @ <headRefOid>
  Files: <N>, +<additions> / -<deletions>
  One-way-door paths touched:
    - <path1>
    - <path2>

  Body:
  <pr body>

  Diff (use `gh pr diff <N>` if you need the full patch):
  <inline if short, or instruction to fetch>

  Your focus: <per-role focus area>

  Return JSON only:
    {"verdict": "approve|request_changes|block", "reasoning": "<2-5 sentences>"}
```

Recommended slate (all live under `octo:personas:`):

| Role | subagent_type | Focus area |
|------|---------------|------------|
| **Chair / synthesizer** | `octo:personas:backend-architect` | Architectural soundness; reconciles the other four into a final verdict |
| Security | `octo:personas:security-auditor` | Authn/z, trust boundaries, secrets, supply chain implications of the one-way door |
| Data | `octo:personas:database-architect` | Schema/index/migration implications; OPTIC-K dim ripple analysis when relevant |
| Code quality | `octo:personas:code-reviewer` | Readability, naming, test coverage, error-path correctness |
| UX | `octo:personas:ux-researcher` | User-visible impact: route changes, error messages, breaking API responses |

Dispatch all five in a **single Agent tool call batch** (parallel) so the
council convenes in one round-trip.

#### Future: chair-by-PR-type

The chair is `backend-architect` by default. A future revision can swap
based on the dominant one-way-door type:

- Public UX / route changes → chair = `octo:personas:ux-researcher`
- Pure data schema (OPTIC-K, SAE artifact) → chair = `octo:personas:database-architect`
- Pricing / billing → chair = `octo:personas:finance-analyst`

We don't have a Codex CLI persona, so we can't mirror claude-codex-forge's
"Codex as chairman" exactly. `backend-architect` is the closest analog.

### Step 4 — Synthesize

The chair (you, in chair mode) reads the four advisor JSONs and writes the
**chair_synthesis**: 3–6 sentences naming each dissent, calling out the
decisive concern, and stating the final verdict.

**Aggregation rule** (deterministic, not a vibes call):

- Any `block` from any advisor → `final_verdict = block`.
- Else if any `request_changes` → `final_verdict = request_changes`.
- Else → `final_verdict = approve`.

The synthesis text explains *why*, not just *what*.

### Step 5 — Persist the verdict

Write to `state/quality/council/<head_sha>.json`. Schema is
`state/quality/council/SCHEMA.json`. Required fields:

```json
{
  "schema": "council-verdict-v1",
  "pr_number": 123,
  "head_sha": "abc1234567890...",
  "one_way_door_paths": ["Common/GA.Business.ML/Embeddings/EmbeddingSchema.cs"],
  "convened_at": "2026-05-23T15:04:05Z",
  "advisors": [
    {"name": "octo:personas:security-auditor", "role": "security",
     "verdict": "approve", "reasoning": "..."},
    {"name": "octo:personas:database-architect", "role": "data",
     "verdict": "request_changes", "reasoning": "..."},
    {"name": "octo:personas:code-reviewer", "role": "code-quality",
     "verdict": "approve", "reasoning": "..."},
    {"name": "octo:personas:ux-researcher", "role": "ux",
     "verdict": "approve", "reasoning": "..."}
  ],
  "chair": "octo:personas:backend-architect",
  "chair_synthesis": "...",
  "final_verdict": "request_changes",
  "graded_by": "claude-opus-4-7"
}
```

### Step 6 — Post the synthesis as a PR comment

```bash
gh pr comment "$PR_NUM" --body "$(cat <<EOF
## Council verdict — $FINAL_VERDICT

**One-way doors touched:** $(echo "$DOORS" | sed 's/^/- /')

$CHAIR_SYNTHESIS

<details>
<summary>Advisor breakdown</summary>

- security-auditor: $SEC_VERDICT
- database-architect: $DB_VERDICT
- code-reviewer: $CR_VERDICT
- ux-researcher: $UX_VERDICT

</details>

Full verdict JSON: \`state/quality/council/$HEAD_SHA.json\`
EOF
)"
```

### Step 7 — Report

One line back to the session:

```text
Council #<PR>: <final_verdict> · doors: <count> · advisors: 4 → 1 chair
artifact: state/quality/council/<sha>.json
```

## Anti-patterns

- **Auto-firing on every PR.** This skill is opt-in. If you find yourself
  invoking it for routine changes, you're using the wrong gate — try
  `/octo:review` or the chatbot-iterate tribunal instead.
- **Skipping the door check.** If you convene the council on a PR that
  doesn't touch a one-way door, you've spent four sub-agent budgets for
  nothing. Always run Step 2 first.
- **Ignoring a `block` verdict.** The aggregation rule is one-way-door
  blast radius mirrored at the gate: a single advisor can halt the merge.
  If you disagree, fix the concern or argue in PR comments and re-run
  `/council` after the next commit (new `head_sha` → new artifact).
- **Re-running on the same SHA.** Once the artifact for `<head_sha>.json`
  exists, the council has spoken for that commit. Push a new commit to
  re-convene.
- **Treating advisor JSON as gospel.** Each advisor returns 2–5 sentences;
  the chair must read them, not just count verdicts.

## Why this exists

Per the harness adoption plan (`docs/plans/2026-05-23-arch-harness-engineering-adoption-plan.md`,
item M6): one-way-door PRs need a richer review than a single LLM pass.
`/octo:review` is the standard daily gate. `/council` is the **escalated**
gate — used when the cost of a wrong call is high and asymmetric (reverting
an OPTIC-K dim change means re-indexing the corpus and coordinating with
ix + every consumer crate).

The forge pattern (multiple specialists + one chair + one verdict + persisted
artifact) is the cheapest known protocol that reliably catches mistakes a
single reviewer misses. We adopt it sparingly.

## Related

- `/octo:review` — daily review gate; runs on every PR.
- `/chatbot-iterate` — chatbot-scoped tribunal gate; load-bearing on
  `Common/GA.Business.ML/Agents/**` paths.
- `docs/plans/2026-05-23-arch-harness-engineering-adoption-plan.md` —
  source plan; this skill is item M6.
- `state/quality/council/README.md` — artifact format + retention.
- `state/quality/council/SCHEMA.json` — verdict JSON Schema.
- [claude-codex-forge](https://github.com/pablomarin/claude-codex-forge) —
  upstream pattern this skill adopts.
