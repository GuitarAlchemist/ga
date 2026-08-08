---
name: supervised-loop
description: Run one bounded supervised autonomous cycle on the Guitar Alchemist repo. Picks the smallest unchecked roadmap slice inside allow_edit, implements it, runs the oracle, emits cycle evidence, and stops. Refuses to run unless dev-process-overseer reports loop-eligible and the preflight passes. Use when asked to "run one supervised loop", "advance the roadmap autonomously", or "do one cycle and stop".
metadata:
  type: skill
  scope: ga
  version: 0.1.0
  emits: state/governance/supervised-loop-cycle.json
---

# Supervised Autonomous Loop (GA)

Operationalises the supervised-loop pattern from agent-blackbox on the
Guitar Alchemist repo. The cycle is intentionally short: one slice, one
verify, one evidence file, then stop.

## GA-specific scope

GA has a **5-layer strict bottom-up architecture** (per `CLAUDE.md`):

1. Core — `GA.Core`, `GA.Domain.Core`
2. Domain — `GA.Business.Core`, `GA.Business.Config`, `GA.BSP.Core`
3. Analysis — `GA.Business.Core.Harmony`, `GA.Business.Core.Fretboard`
4. AI/ML — `GA.Business.ML` (embeddings, OPTIC-K, RAG)
5. Orchestration — `GA.Business.Core.Orchestration`, `GA.Business.Assets`,
   `GA.Business.Intelligence`, Apps

The initial code rollout is narrower than "layer 4/5": it permits only
`Common/GA.Business.ML/Agents/Skills/**` and
`Common/GA.Business.ML/Agents/Mcp/**`, with matching
`*SkillTests.cs` / `*McpToolsTests.cs` files under the ML unit-test folder.
Frontend/docs/scripts/state paths remain available as listed in
`ga.loop-policy.json`. Layers 1-3, all other code/test paths, and the
OPTIC-K embedding surface remain unavailable by default. Widen only through
an operator-reviewed change to the protected policy file.

## When to use

- The user asked for one bounded autonomous improvement on this repo.
- You are running a scheduled `/loop` and need to make one safe slice of
  progress before yielding.
- You are dogfooding the supervised pattern that other repos will copy.

## Hard refusals (never bypass)

- `state/governance/dev-process-overseer.json` missing, stale (>24h), or
  `workflowMode != "loop-eligible"` -> abort with reason.
- `.STOP` file at repo root -> abort.
- HALT-ALL marker present at `$HOME/.demerzel/HALT-ALL` and active -> abort.
- `ga.loop-policy.json` fails `Test-GaLoopPolicy` (scope, L0-L3,
  postflight, promotion gates, exact-SHA evidence, or independent-review
  contract) -> abort. The risk-scoring policy remains separate in
  `agent-blackbox.policy.json`.
- Any diff touches a path in `protected_paths` -> abort, do not commit.
- **`pwsh Scripts/supervised-loop-preflight.ps1` emits `LOOP_READY=false`
  -> abort with the reason it printed; do not try to repair the gate.**
  The preflight enforces six gates (loop-policy parses, overseer fresh,
  overseer loop-eligible, no `.STOP`, no HALT-ALL, worktree clean). The
  worktree-clean gate forgives dirty files outside both `allow_edit` and
  `protected_paths` but still hard-fails on dirty files INSIDE either.
- Service restarts (`Restart-Service`, `taskkill` against GaApi /
  GaChatbot.Api / cloudflared / Ollama / Redis), secret rotation,
  cross-repo schema edits (`docs/contracts/**`, ix schemas, Demerzel
  governance schemas), OPTIC-K dimension changes -> abort, hand back to
  human.

## Out of scope (hand back to human)

- restarting services (`Restart-Service`, `taskkill`)
- editing `.github/workflows/**`
- editing `docs/contracts/**` (cross-repo schema contracts)
- editing `governance/demerzel/**` (submodule)
- editing `mcp-servers/**` (third-party)
- editing `Common/GA.Business.ML/Embeddings/**` (OPTIC-K one-way door)
- editing `Common/GA.Business.Core/**` (lowest-layer fanout)
- editing GA product code outside `Common/GA.Business.ML/Agents/Skills/**`
  and `Common/GA.Business.ML/Agents/Mcp/**`
- editing tests outside the two filename families explicitly listed in
  `allow_edit`
- editing `Tests/Apps/GaChatbot.Api.Tests/Corpus/prompts.yaml` (golden corpus)
- editing `AllProjects.slnx`, `global.json`, `Directory.Build.*`,
  `Directory.Packages.props` (build-critical)
- rotating secrets, touching billing/legal/customer commitments
- cross-repo schema bumps
- merging the PR (a separate human-gated step)

## Steps

### Step 1: Verify loop eligibility

Read `state/governance/dev-process-overseer.json`. Abort if:

- file missing
- `emittedAt` older than 24h
- `workflowMode != "loop-eligible"`
- `haltAll.active == true`
- any `domains[].oracleStatus != "ok"`

If the file is stale, refresh it first:

```powershell
pwsh Scripts/supervised-loop-harness-oracle.ps1
pwsh Scripts/dev-process-overseer.ps1 -Domain ga-harness
```

The harness oracle emits `state/quality/ga-harness/last.json` with the
contract shape the overseer expects. Scoping the overseer to
`-Domain ga-harness` avoids pre-existing `missing-oracle-output`
warnings from other domains (chatbot-qa, embeddings, etc.) that may not
have a fresh `last.json`. See `docs/loop-onboarding.md` for why.

### Step 2: Pin policy into working memory

Read `state/governance/action-boundary.json` (schema
`action-boundary-v0.1.0` — the aggregated single source, generated by
`Scripts/action-boundary-aggregate.py` per
`docs/contracts/2026-07-02-action-boundary.contract.md`). Capture
`capabilities.allow_edit` and `capabilities.protected_paths` verbatim.
These are the **only** edit boundaries you may operate within for this
cycle. If either array is missing, abort.

Before trusting it, run
`python Scripts/action-boundary-aggregate.py --root . --check` — if it
reports drift, the instance is stale against its source fragments:
regenerate (same command without `--check`) and use the fresh values.
If the file is absent entirely, fall back to the fragments directly
(`ga.loop-policy.json` for the edit scope, `agent-blackbox.policy.json`
for the risk-scoring view — blocked paths, one-way doors): same
semantics, the fragments are canonical.

The loop-policy-derived scope is the active edit boundary; the
risk-policy-derived view informs the post-cycle risk report.

Also run `pwsh Scripts/supervised-loop-preflight.ps1`. It must print
`LOOP_READY=true` on its last line and exit 0. If not, abort with its
reason. This is verification **L0**. L0-L3 here mean verification depth,
not GA autonomy level; their commands are canonical in
`ga.loop-policy.json.verification_levels`.

### Step 3: Pick one backlog slice

Open `BACKLOG.md` and `state/governance/ga-loop-status.json`. Choose the
smallest unchecked item that lives entirely inside `allow_edit` and
touches nothing in `protected_paths`. Existing GA loops that this skill
is designed to supervise:

- `chatbot-qa` (driven by `/auto-optimize` + `Scripts/run-prompt-corpus.ps1`)
- `embeddings` (state/quality/embeddings/*.json)
- `voicing-analysis` (state/quality/voicing-analysis/*.json)
- `dsl-eval` (state/quality/dsl-eval/prompts.json)
- `readme-drift` (state/quality/readme-drift/)
- bounded agent-skill or in-process MCP-tool changes in the two allowed
  layer-4 source seams, with matching allowed unit tests

Do **not** pick anything that touches `Common/GA.Business.Core/**`,
`Common/GA.Business.ML/Embeddings/**` (OPTIC-K), `docs/contracts/**`,
`governance/demerzel/**`, `mcp-servers/**`, `.github/workflows/**`,
`AllProjects.slnx`, `Directory.Build.*`, `global.json`, or
`Tests/Apps/GaChatbot.Api.Tests/Corpus/prompts.yaml`.

### Step 4: Plan the slice

Write a plain text plan inline with:

1. Goal (one sentence).
2. Files you will create or edit (must all match `allow_edit`).
3. Verifier commands from `ga.loop-policy.json.verification_levels`:
   L1 is the focused project test, L2 is `bash Scripts/cloud-validate.sh`
   (`pwsh Scripts/cloud-validate.ps1` on Windows), and L3 is the full
   solution build/test merge gate. Frontend slices also run the L3
   conditional frontend commands in the declared working directory.
4. Stop condition (matches a hard refusal above or repeated verify failure).

### Step 5: Implement one slice

- Edit only inside `allow_edit`.
- Keep the diff under ~200 lines and one logical concern.
- For a code change, add or update a test matching the corresponding
  allowed filename family. A test elsewhere under `Tests/**` remains out
  of scope.
- Run L1 and L2. If either fails, attempt one targeted fix. If it still
  fails, **stop** and emit the cycle evidence with
  `exit_reason: verify_failed`.
- The pre-commit hook (`Scripts/install-git-hooks.ps1`) runs `dotnet
  format` + build. Honour it; do not bypass.

A draft PR may be opened or updated only after L0, L1, and L2 pass for the
submitted head. Do not mark it ready: ready-for-merge additionally requires
L3, the Agent Blackbox postflight supplied through GA #630, and the required
`Independent Review Verdict` check from a separate reviewer identity/context.
An author comment is not review evidence. This skill never merges.

### Step 6: Emit cycle evidence

Write `state/governance/supervised-loop-cycle.json` with shape:

```json
{
  "schemaVersion": "0.2",
  "cycle_id": "<uuid or YYYYMMDD-HHMMSS-slug>",
  "started_at": "<ISO 8601 UTC>",
  "finished_at": "<ISO 8601 UTC>",
  "branch": "<current branch>",
  "base_sha": "<exact 40-character base SHA>",
  "head_sha": "<exact 40-character submitted head SHA>",
  "goal": "<one-line description of the slice>",
  "roadmap_item": "<exact bullet text from BACKLOG.md or ga-loop-status.json>",
  "files_changed": ["..."],
  "allow_edit_violation": false,
  "protected_path_touched": false,
  "verification_results": {
    "L0": "pass | fail | skipped",
    "L1": "pass | fail | skipped",
    "L2": "pass | fail | skipped",
    "L3": "pass | fail | skipped"
  },
  "postflight_status": "pass | fail | pending_external_dependency",
  "independent_review_status": "pass | fail | pending",
  "exit_reason": "completed | verify_failed | preflight_failed | protected_path | overseer_blocked | stop_marker | halt_all | manual"
}
```

Always emit this file, even when stopping early — that is the whole point
of "supervised". A skipped cycle with `exit_reason: overseer_blocked` is
still useful evidence.

### Step 7: Stop conditions

After Step 6, **always stop the session loop**. Do not start a second
slice in the same invocation. The skill is one-cycle-one-evidence by
design.

Hard stop triggers (no matter what step you are in):

- verify failed twice for different root causes
- a `protected_paths` glob matched any staged file
- `.STOP` file appeared
- HALT-ALL active
- any service-restart-needed condition surfaced
- user typed STOP / abort / kill

## How to dispatch

Invoke this skill from any GA Claude Code session with:
*"run one supervised loop cycle on this repo"*.

## Related

- `docs/loop-onboarding.md` — operator-facing rollout principle + daily recipe.
- `Scripts/dev-process-overseer.ps1` — producer of
  `state/governance/dev-process-overseer.json`.
- `Scripts/supervised-loop-preflight.ps1` — deterministic readiness gate.
- `ga.loop-policy.json` — edit-scope policy enforced by this skill.
- `agent-blackbox.policy.json` — risk-scoring policy (sibling).
- `.claude/skills/auto-optimize/` — domain-specific optimization loop for
  chatbot-qa (the existing precedent this kit generalises).
