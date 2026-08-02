# Local-work reconciliation snapshot

`Scripts/reconcile_local_work.py` is the read-only GA adapter for the Cloud
Factory tracer bullet in GA #630. One invocation inventories registered Git
worktrees across the configured repositories and binds the evidence needed to
decide what may happen next.

The command discovers dirty paths, ahead/behind state, detached and invalid
registrations, open PRs, explicit live ownership, and historical Claude Code
provenance. It emits observations only. It never checks out, cleans, resets,
stashes, rebases, or writes into a discovered repository.

## Canonical dependency

Path semantics and AFK contract names remain owned by Agent Blackbox #44/#46.
The configuration names an Agent Blackbox checkout and its full expected commit
SHA. The command verifies `HEAD` exactly, then loads the matcher from that
checkout. A missing or different revision fails before discovery; GA does not
vendor the matcher or schemas.

The current draft integration revision is:

```text
GuitarAlchemist/agent-blackbox@b90839e457998089d30e2e7f4c73ea49511cdddc
```

That revision belongs to stacked draft PR #51 and is not a released contract.
Update the pin only after reviewing the new exact SHA.

## Configuration

Use an operator-local JSON file outside the repositories being inspected:

```json
{
  "schema_version": 1,
  "contract_source": {
    "repository": "GuitarAlchemist/agent-blackbox",
    "path": "C:/path/to/agent-blackbox-contract-checkout",
    "expected_revision": "b90839e457998089d30e2e7f4c73ea49511cdddc"
  },
  "repositories": [
    {
      "id": "GuitarAlchemist/ga",
      "path": "C:/path/to/ga",
      "issue": 630
    }
  ],
  "path_classes": {
    "state": ["state/**", "**/state/**"],
    "generated": ["**/bin/**", "**/obj/**", "**/*.generated.*"],
    "source": ["**/*.cs", "**/*.fs", "**/*.py", "**/*.rs", "**/*.ts"]
  }
}
```

An `issue` creates a canonical empty `afk-task-state-v0.2` binding for that
repository. The snapshot also advertises `afk-evidence-manifest-v0.2`; the
v0.2 contracts bind task identity, policy, budget, task state, lease, and exact
Git revisions. A lane without an issue is explicitly `unbound`; the adapter
does not invent issue ownership.

## Run and recheck

Write the snapshot to an operator-owned path outside every discovered worktree:

```powershell
python Scripts/reconcile_local_work.py `
  --config C:\operator\reconciliation-config.json `
  --presence-json C:\operator\live-presence.json `
  --out C:\operator\reconciliation-snapshot.json
```

If `--prs-json` is omitted, the command reads open PR metadata through `gh`.
`--presence-json` is a normalized snapshot with a `sessions` array; only entries
whose `state` is `live` may own a lane. Ended sessions are provenance, never
owners.

By default, recent files below `~/.claude/projects` are scanned for only
`sessionId`, `cwd`, `gitBranch`, and `timestamp`. Message content, tool calls,
thinking, and attachments are never copied. Use `--no-claude` to disable this
metadata scan or `--claude-root` for a fixture/location override.

Before acting on a packet, re-run discovery in check mode:

```powershell
python Scripts/reconcile_local_work.py `
  --config C:\operator\reconciliation-config.json `
  --presence-json C:\operator\live-presence.json `
  --check C:\operator\reconciliation-snapshot.json
```

Exit code `1` means at least one packet appeared, disappeared, changed `HEAD`,
or changed its dirty-path digest. The prior snapshot is then stale and must not
authorize a mutation.

## Packet semantics

Stable packet IDs hash repository identity plus canonical physical worktree.
Source signatures bind `HEAD` and the sorted dirty-path digest. Classification
is conservative:

- `active`: a live owner or uncommitted work exists;
- `ready`: clean local commits or a PR are available for bounded review;
- `blocked`: missing/invalid registration, stale contract pin, behind branch, or
  contested ownership;
- `quarantined`: detached dirty state has no safe issue binding;
- `archive`: clean, unbound, and no unfinished evidence.

Classification is advice, not authority. Mutation still requires an uncontested
Gaia/Galactic claim, an isolated worktree, an Agent Blackbox lease/budget, and
exact-SHA postflight evidence.

## Advance one packet

`Scripts/run_reconciliation_packet.py` is the bounded lifecycle module layered
over the read-only snapshot. It exposes two operations and deliberately does not
accept a worker command:

- `start` rechecks one `ready`, unowned, clean, non-behind, issue-bound packet;
  creates one isolated `codex/` worktree at the snapshot SHA; acquires and starts
  the canonical Agent Blackbox fenced lease; writes an operator-controlled run
  handle; appends
  a `started` transition; and stops.
- `finish` reads that handle after the worker has committed one bounded slice;
  invokes canonical Agent Blackbox postflight against independently produced
  evidence; releases only the exact lease token and generation; appends the
  terminal transition with the verdict hash; and stops.

The run handle, task state, budget, evidence, event log, and verdict must live
outside every discovered and isolated worktree. The GA event log is an
append-only audit projection; canonical Agent Blackbox task state and verdicts
remain authoritative. The projection hashes the lease token rather than
exposing it. The handle pins the budget digest, task-state path, base SHA, packet
generation, and reviewed Agent Blackbox revision. `finish` fails before
postflight if the budget or task-state path differs from `start`.

Start one packet:

```powershell
python Scripts/run_reconciliation_packet.py `
  --config C:\operator\reconciliation-config.json `
  --event-log C:\operator\runs\events.jsonl `
  start `
  --snapshot C:\operator\reconciliation-snapshot.json `
  --packet-id local-0123456789abcdef01234567 `
  --target-worktree C:\operator\worktrees\ga-630-attempt-1 `
  --branch codex/factory-ga-630-attempt-1 `
  --handle C:\operator\runs\ga-630-attempt-1.json `
  --task-state C:\operator\runs\ga-630-task-state.json `
  --budget C:\operator\runs\ga-630-budget.json `
  --stop-marker state/quality/chatbot-qa/.STOP `
  --worker codex --provider openai
```

The repo-root `.STOP` marker is checked by default. Repeat `--stop-marker` for
packet/domain sentinels such as `state/quality/<domain>/.STOP`; declared paths
must stay inside the source worktree. The canonical governance gate also checks
the repo-local `state/.loop-halted` switch and the operator-local
`~/.demerzel/HALT-ALL` contract before a lease is acquired and again before
postflight.

After the worker commits the slice and an independent reviewer produces an
`afk-evidence-manifest-v0.2` bound to that exact head, finish it:

```powershell
python Scripts/run_reconciliation_packet.py `
  --config C:\operator\reconciliation-config.json `
  --event-log C:\operator\runs\events.jsonl `
  finish `
  --handle C:\operator\runs\ga-630-attempt-1.json `
  --task-state C:\operator\runs\ga-630-task-state.json `
  --policy C:\src\ga\ga.loop-policy.json `
  --budget C:\operator\runs\ga-630-budget.json `
  --evidence C:\operator\runs\ga-630-evidence.json `
  --verdict C:\operator\runs\ga-630-postflight.json
```

A stop signal present before `start` refuses the packet without acquiring a
lease. A signal that appears during the run makes `finish` release the exact
token and generation as `stopped`, append the source and reason durably, skip
postflight, and preserve the isolated worktree for diagnosis. This stop path
takes precedence over budget drift so the kill switch cannot be blocked by a
changed control file.

A failed postflight is terminal and classified as review, budget, policy,
provider, infrastructure, or verification. The runner does not retry functional
work, remove diagnostic worktrees, push, mark a PR ready, or merge.

## Transcript boundary

This command reconciles provenance, not conversations. Importing a ChatGPT
Classic export or rendering a Claude transcript is a separate, explicit action
because it copies conversation content. Claude rendering remains available via
`Scripts/export_session_to_md.py`; a ChatGPT export should receive its own
consentful adapter rather than being silently folded into the worktree scan.

## Verification

The test suite creates disposable Git repositories and never points a mutating
test at an operator checkout:

```powershell
python -m unittest Scripts.test_reconcile_local_work Scripts.test_run_reconciliation_packet -v
```

It covers dirty/ahead/behind/detached/missing/invalid lanes, PR and live-owner
binding, stable IDs, stale detection, exact dependency pins, and the guarantee
that Claude conversation content is excluded.

The packet-runner suite additionally covers stale snapshot rejection, eligibility
gates, isolated-worktree creation, exact lease generation, token redaction,
budget/path pinning, canonical STOP/HALT handling, successful postflight, and
terminal failure classification.
Set `AGENT_BLACKBOX_CHECKOUT` to the exact reviewed checkout to exercise the real
lease and postflight CLI end to end; otherwise that one integration test skips.
