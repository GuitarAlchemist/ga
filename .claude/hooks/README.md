# `.claude/hooks/` — project-local Claude Code hooks

Every hook wired into `.claude/settings.json` is a subcommand of one Go
program, `ga-hooks` (`go/claudehooks/cmd/ga-hooks`, standard library only).
No hook runs a shell, PowerShell, Python or Node (the user's rule,
2026-11-05: "jamais de shell dans les hooks"); CI fails a hook command that
does (`.github/workflows/karpathy-cherny-discipline.yml`). The same layout
is used by ix (`go/claudehooks`, ix PR #512) and tars (tars PR #419).

- **SessionStart** runs `go -C "$CLAUDE_PROJECT_DIR/go/claudehooks" run
  ./cmd/ga-hooks session-start`. Go builds (and caches) the program from
  the checked-out source; `session-start` copies it to
  `.claude/hooks/bin/ga-hooks[.exe]` (gitignored), prints the session
  banner and, after a compaction or a resume, injects
  `state/digests/latest.md`.
- **Every other hook** calls `"$CLAUDE_PROJECT_DIR/.claude/hooks/bin/ga-hooks"
  <subcommand>` directly: no build, no interpreter start-up.

| Event | Matcher | Subcommand | Was |
|---|---|---|---|
| PostToolUse | `Edit\|Write` | `dsl-changed` | inline `pwsh -Command` (GA.Business.DSL notice) |
| PostToolUse | `Edit\|Write\|Bash` | `digest-activity-tracker` | `Scripts/digest-activity-tracker.ps1` |
| PostToolUse | `Edit\|Write` | `ai-annotation-scan` | `Scripts/ai-annotation-scan.ps1` |
| PreToolUse | `Bash` | `pr-rationale-capture` | `Scripts/pr-rationale-capture.ps1` |
| PreToolUse | `Bash` | `ai-annotation-merge-gate` (exit 2 blocks `gh pr merge`) | `Scripts/ai-annotation-merge-gate.ps1` |
| UserPromptSubmit | — | `digest-staleness-nudge` | `Scripts/digest-staleness-nudge.ps1` |
| UserPromptSubmit | — | `correction-detector` | `Scripts/correction-detector.ps1` |
| UserPromptSubmit | — | `loops-goals-tracker prompt` | `.claude/hooks/loops-goals-tracker.ps1 prompt` |
| PreCompact | — | `precompact-digest` | `Scripts/precompact-digest.ps1` |
| SessionStart | — | `session-start` (banner; digest on `compact\|resume`) | inline `pwsh -Command` banner + `Scripts/sessionstart-digest.ps1` |
| Stop | — | `session-pr-check` | `Scripts/session-pr-check.ps1` |
| Stop | — | `digest-stop-finalize` | `Scripts/digest-stop-finalize.ps1` |
| Stop | — | `loops-goals-tracker stop` | `.claude/hooks/loops-goals-tracker.ps1 stop` |

`ga-hooks digest-validate [path]` checks a digest's frontmatter like
`Scripts/digest-validate.ps1` (kept: the `/digest` skill runs it).

To work on a hook: edit `go/claudehooks/cmd/ga-hooks/*.go`, then
`cd go/claudehooks && GOPROXY=off go vet ./... && GOPROXY=off go test ./...`.
Tests fake git, gh and HTTP; they never start a real program or reach the
network. The next session start installs the new build.

---

## `loops-goals-tracker` — runtime visibility for `/loop` and `/goal`

### Problem

Claude Code's native `/loop` and `/goal` slash commands are session-scoped
with zero disk telemetry. If you have a second Claude window open in
parallel, you can't see "what is the first one looping on right now?" or
"how many turns has the goal been working?". This tracker closes that gap.

### What it does

Triggered as a second hook in two existing chains:

| Hook event         | Mode arg | Behavior |
|--------------------|----------|----------|
| `UserPromptSubmit` | `prompt` | Regex-match the user prompt for `^/loop` or `^/goal`; on match, append a `start` event to the JSONL. |
| `Stop`             | `stop`   | For every active record belonging to the current session, append a `turn` event with `turn_count += 1` and a fresh `last_activity_at`. |

The script is silent for non-matching prompts (most prompts). It writes
nothing to stdout (no `additionalContext` injection — pure side effect).

### State files

Two files are written on every event, both append-only JSONL:

1. **Canonical (per-user, per-project, durable)**:
   ```
   ~/.claude/projects/<encoded-repo-path>/state/runtime-loops-goals.jsonl
   ```
   `<encoded-repo-path>` follows Claude Code's existing convention of
   replacing path separators and colons with dashes (e.g.
   `C--Users-spare-source-repos-ga`).

2. **Repo-local mirror (readable by the Vite dev server)**:
   ```
   <repo>/state/.runtime-loops-goals.jsonl
   ```
   Gitignored. This is the file the dashboard reads via
   `/dev-data/runtime-loops-goals`.

Both are written for redundancy — the canonical copy survives if the
repo-local mirror is wiped (e.g. `git clean -fdx`), and the repo-local
mirror gives the dashboard a path it can resolve without knowing the
encoded path convention.

### Record schema

Every line is a JSON object:

```json
{
  "id": "5bc686790f284cd3",
  "kind": "loop",
  "started_at": "2026-05-24T03:24:51Z",
  "session_id": "claude-session-uuid",
  "prompt_or_condition": "5m check the deploy",
  "turn_count": 3,
  "last_activity_at": "2026-05-24T03:39:18Z",
  "status": "active",
  "event": "start",
  "branch": "feat/runtime-loops-goals-tracker"
}
```

| Field                  | Type                              | Notes |
|------------------------|-----------------------------------|-------|
| `id`                   | string (16-char hex)              | Unique per `/loop` or `/goal` invocation. Stable across events. |
| `kind`                 | `"loop"` \| `"goal"`              | Which slash command produced this record. |
| `started_at`           | ISO-8601 UTC                      | When the `start` event fired. Preserved across `turn` events. |
| `session_id`           | string                            | Provided by the Claude Code hook payload. Used to scope Stop bumps to the right session. |
| `prompt_or_condition`  | string (truncated to 280 chars)   | What followed `/loop` or `/goal` on the prompt line. Sanitized — `\r\n\t` → space. |
| `turn_count`           | int                               | Bumped by the Stop hook. Starts at 0. |
| `last_activity_at`     | ISO-8601 UTC                      | Refreshed on every `turn` event. |
| `status`               | `"active"` \| `"paused"` \| `"completed"` \| `"archived"` | Lifecycle (see below). |
| `event`                | `"start"` \| `"turn"` \| `"status_change"` | What produced this line. |
| `branch`               | string                            | git branch at the time the record was first written. |

### Lifecycle

```
active ──(manual pause via dashboard)──> paused
   │
   └─(manual stop via dashboard, /complete, or shutdown)──> completed
                                                              │
                                                              └──(background sweep after 7d)──> archived
```

- **`active`** — the default. Both freshly-started records and ones being
  bumped by the Stop hook stay `active`.
- **`paused`** — operator-driven via the dashboard's pause button (not
  implemented in PR 1; reserved for follow-up). Still appears under
  "active" buckets so it isn't forgotten.
- **`completed`** — operator-driven via the dashboard's Stop button (POST
  to `/dev-data/runtime-loops-goals/stop/<id>`). Falls into the
  `completed_recent` bucket on the dashboard.
- **`archived`** — completed records older than 7 days. Hidden from the
  dashboard. Lines are still on disk for forensics.

The "latest-line-per-id wins" rule (same pattern as
`state/algedonic/inbox.jsonl`) means every projection picks the most
recent record per `id` and uses its `status` as the current state.

### Dashboard

`/test` → Development tab → Harness sub-tab → "Active loops & goals" card.

- Auto-refreshes every 15 seconds.
- Truncates the prompt/condition to 60 chars with a tooltip for the full
  string.
- "Stop" button hits the gated POST endpoint that appends a synthetic
  `status_change` line with `status: "completed"`.
- Empty state explicitly tells the operator to invoke `/loop` or `/goal`
  in any Claude session to populate the view.

### Testing the wiring manually

```powershell
# Simulate a /loop invocation
'{"prompt":"/loop 5m check deploys","session_id":"manual-test"}' | `
  .\.claude\hooks\bin\ga-hooks.exe loops-goals-tracker prompt

# Simulate a Stop event (bumps turn_count for every active record in the session)
'{"session_id":"manual-test"}' | `
  .\.claude\hooks\bin\ga-hooks.exe loops-goals-tracker stop

# Inspect the repo-local mirror
Get-Content .\state\.runtime-loops-goals.jsonl
```

The cases are pinned by `TestLoopsGoalsTracker` in
`go/claudehooks/cmd/ga-hooks/ga_test.go`.

### Limitations (intentional, not gold-plating)

- The native `CronCreate` event that fires on each `/loop` iteration is
  **not** observable from hooks (Claude Code v2.1.139+ has no
  `/loop`-specific hook events). The Stop hook is the closest proxy
  available: it fires when the model finishes responding, which for a
  loop is roughly once per iteration.
- The user-level canonical JSONL is **not** served by the Vite
  middleware. Resolving the encoded path requires path-traversal logic
  that's risky to ship publicly. The repo-local mirror is the dashboard's
  source of truth; the canonical file is a durability backup.
- No background sweep job for `archived` exists yet — the projection
  drops `archived` rows from the view, but the JSONL grows unbounded
  until someone trims it. With ~5 events per loop per minute, this is
  tens of kilobytes per day per developer — fine for a long while.

### Related files

- `go/claudehooks/cmd/ga-hooks/loopsgoals.go` — the tracker.
- `.claude/settings.json` — wires `prompt` and `stop` invocations into
  the corresponding hook chains.
- `ReactComponents/ga-react-components/vite.config.ts` — serves
  `/dev-data/runtime-loops-goals` (GET projection + POST stop).
- `ReactComponents/ga-react-components/src/dev-data/parsers.ts` —
  `projectLoopsGoals()` is the pure projection function used by both
  the middleware and the unit tests.
- `ReactComponents/ga-react-components/src/dev-data/parsers.test.ts` —
  8 unit tests covering empty / malformed / latest-line-wins /
  archived-drop / decoration / sorting / paused-handling.
- `ReactComponents/ga-react-components/src/components/Harness/LoopsGoalsCard.tsx` —
  the React tile.
