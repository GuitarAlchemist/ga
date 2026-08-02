# GA Cloud Factory — local-work reconciliation tracer-bullet plan

**Date:** 2026-08-02

**Status:** in progress

**Owner:** operator-supervised Codex/Claude factory

**Scope:** GA ecosystem local work, GitHub PR queue, Gaia coordination, and BAML boundary experiments

**Canonical program:** [`GuitarAlchemist/.github#68`](https://github.com/GuitarAlchemist/.github/issues/68)

**Shared controls:** [`agent-blackbox#44`](https://github.com/GuitarAlchemist/agent-blackbox/issues/44), [`agent-blackbox#46`](https://github.com/GuitarAlchemist/agent-blackbox/issues/46)

**GA ownership split:** [`ga#629`](https://github.com/GuitarAlchemist/ga/issues/629) owns policy/readiness; [`ga#630`](https://github.com/GuitarAlchemist/ga/issues/630) owns the GA adapter and integration.

**Gaia repair:** [`Demerzel#948`](https://github.com/GuitarAlchemist/Demerzel/issues/948)

## Problem

Useful work is spread across dirty primary checkouts, isolated worktrees, local-only
commits, draft PRs, and ended Claude/Codex sessions. The operator cannot safely move
the factory forward without first answering four questions for every lane:

1. What exact Git state contains the work?
2. Is another live session still its owner?
3. Is the work current, superseded, or unsafe to resurrect?
4. What evidence and authority are required for its next mutation?

The existing supervised-loop kernel already provides preflight, allow/protect
boundaries, kill switches, bounded cycles, and evidence. Gaia adds automatic
same-worktree collision detection. The missing module is a reconciliation queue
that turns local state into reviewable work packets and binds postflight evidence
to the exact commit it certifies.

## Outcome

One operator command produces a read-only snapshot of unfinished ecosystem work.
Each candidate becomes a typed work packet with one next action. The packet uses
the canonical `afk-task-state-v0.1` and `afk-evidence-manifest-v0.1` contracts from
agent-blackbox #44; this adapter adds only local discovery provenance such as
worktree path, dirty-path digest, and classification. The factory may then advance
one packet in an isolated worktree, run its declared verifier, obtain an independent
review, and stop at the configured authority boundary.

The first end-to-end tracer bullet is the local Gaia lane discovered behind
Demerzel PR #940. PR #940 remains the design review. Its implementation must be
captured, corrected against the approved spec, reduced to a brokerless slice,
verified, reviewed on its exact SHA, and published as a separate draft PR without
merging. This exercises local discovery, ownership, isolation, verification,
independent review, and GitHub handoff in one slice.

## Non-goals

- No infinite autonomous prompt loop. Work is a bounded queue.
- No automatic merging until the operator explicitly grants that authority.
- No cleanup, reset, stash pop, rebase, or deletion of an existing local lane.
- No BAML fleet adoption or second schema authority.
- No replacement for GitHub, Galactic Protocol, the supervised-loop kernel, or
  Demerzel governance.
- No semantic/vector Gaia exchange until deterministic collision handling earns
  trust in production use.

## Work packet adapter

The canonical contracts remain authoritative for task state, budgets, authority,
and evidence. Every discovered lane supplies these reconciliation fields to those
contracts:

| Field | Meaning |
|---|---|
| `packet_id` | Stable local identifier mapped to the canonical task ID. |
| `repo` / `worktree` | Canonical repository and physical checkout. |
| `branch` / `upstream` | Current branch and configured remote tracking ref. |
| `base_sha` / `head_sha` | Exact comparison boundary. |
| `dirty_paths` | Uncommitted paths, classified as source, generated, state, or unknown. |
| `ahead` / `behind` | Divergence from the configured upstream. |
| `pr` | Bound GitHub PR, if one exists. |
| `owner` | Live session/claim owner or `unowned`; ended sessions are provenance only. |
| `authority` | Canonical authority grant; locally displayed as `inspect`, `edit`, `push`, `ready`, or `merge`. |
| `budget` | Canonical wall-clock, diff/files, model/tool-call, and monetary ceilings. |
| `verifiers` | Exact commands required for this lane. |
| `evidence` | SHA-bound command results and independent review verdicts. |
| `classification` | `active`, `ready`, `blocked`, `quarantined`, `superseded`, or `archive`. |
| `next_action` | One bounded, reversible action. |

Packets are append-only observations. They do not modify the source worktree.
Reclassification records evidence; it never erases prior state.

## Lifecycle

```text
discover -> bind owner/PR -> classify -> claim -> isolate -> implement one slice
        -> verify -> independent review -> SHA-bound postflight -> hand off/stop
```

Hard stops:

- a live owner or overlapping Gaia claim exists;
- the source worktree changes after snapshot capture;
- the head SHA changes after verification or review;
- protected paths, one-way doors, or the packet diff budget are crossed;
- the verifier cannot run or fails twice for different causes;
- monetary or wall-clock budget is exhausted;
- the next action requires authority above the packet's grant.

### Wayfinder is an escalation trigger, not a cadence

Periodic reconciliation runs ordinary issue/PR triage, evidence-freshness checks,
and packet discovery. It does **not** open a fresh Wayfinder map. Invoke Wayfinder
only for a materially new destination that is larger than one session, still too
foggy to spec, and has no existing canonical program/map. Continue that one map
until its decision frontier is empty, then return to spec/tickets/implementation.

The current Cloud Factory program already has a destination, plan, ownership
split, contracts, and tracer, so a second map would duplicate authority. See the
[primary-source cadence assessment](../research/2026-08-02-wayfinder-reconciliation-cadence.md).

## Existing modules to deepen, not duplicate

- `Scripts/supervised-loop-preflight.ps1`: deterministic readiness and protected
  path gate.
- `.claude/skills/supervised-loop/SKILL.md`: one-cycle/one-evidence discipline.
- `Scripts/loop-record.ps1` and `Scripts/loop-decide.ps1`: durable cycle trajectory
  and fail-closed oracle semantics.
- `Scripts/loop-killswitch.ps1`: operator stop boundary.
- Galactic Protocol: live presence, advisory repo/lane claims, and messages.
- Gaia: automatic same-physical-worktree path claims at mutation boundaries.
- GitHub Issues/PRs: durable queue and review surface.
- Agent Blackbox/Demerzel: independent risk and governance evidence.

The canonical ownership boundaries are:

- `.github#68`: organization-wide program and promotion gates;
- `agent-blackbox#44`: postflight, leases, exact-SHA evidence, and budgets;
- `agent-blackbox#46` and draft PR #50: one authoritative Git path matcher;
- `ga#629`: GA policy vocabulary, L0-L3 readiness, and independent-review rules;
- `ga#630`: GA integration adapter, consuming rather than copying the shared tools;
- `tars#223`: fleet postflight/lease alignment.

The Cloud Factory is orchestration over these modules. It should not become a new
general-purpose agent runtime.

## Upstream mechanism review

The primary-source comparison is recorded in
[`docs/research/2026-08-02-hermes-openclaw-cloud-factory-inspiration.md`](../research/2026-08-02-hermes-openclaw-cloud-factory-inspiration.md).
Hermes Agent and OpenClaw are mechanism catalogs, not runtimes to embed. Adopt:

- append-only packet events with actor, packet generation, reason, and idempotency
  key; a successful process exit without a terminal transition is a protocol error;
- exact-owner fenced SQLite leases over canonical physical worktree identity and
  overlapping paths; uncertain ownership stops or requeues rather than failing open;
- one isolated worktree per mutable packet, with unrelated repositories read-only
  or absent and network/secrets enabled only when the packet declares them;
- a capability snapshot pinned at claim time: skill and content digests, tools,
  command shapes, mounts, credentials, and BAML schema/client versions;
- one atomic hierarchical budget ledger from factory to packet to child, preserving
  charges when an external side effect is uncertain;
- SHA-, dirty-digest-, toolchain-, and packet-generation-bound evidence that any
  subsequent edit or capability change makes stale;
- runtime-derived completion, bounded retries by classified failure, and authorized
  cascade stop. Model prose and best-effort notifications are not completion records.

Do not copy fail-open lease timeouts, independent child budgets, host-local SQLite
presented as distributed coordination, broad shared writable workspaces, or skill
allowlists presented as security boundaries.

## Gaia lane — current review verdict

Local branch `docs/gaia-semantic-bus` is eight commits ahead of its remote PR #940
head. Its focused suites pass (89 tests, 3 Windows/platform skips) and the full
Demerzel Python suite passes (552 tests, 3 skips), but the implementation is not
push-ready:

- P0: re-editing a path after commit can retain an old `dirty` claim and reopen
  the check/claim race.
- P1: the approved `~/.agents/claims.jsonl` mirror is absent.
- P1: heartbeat-primary liveness was replaced by a fixed 24-hour claim expiry.
- P1: failed Git dirty probes collapse to clean and can delete a live claim.
- P1: the approved 100 ms PreToolUse ceiling became 750 ms.
- P1: only Claude hooks are wired; the promised MCP/generic harness surface is
  absent.
- P2: the base-rate tool observes but does not enforce the alarm-rate guard.
- P2: daemon/client/installer work landed although the approved slice explicitly
  deferred the daemon.

Decision: preserve the branch, do not push it yet, and repair the brokerless slice
under Demerzel #948 before deciding whether daemon work moves to a sibling service
or receives explicit owner adjudication. Keep PR #940 design-only.

## BAML lane — current reconciliation verdict

BAML remains a typed leaf-boundary experiment, not the Cloud Factory protocol:

- The isolated `demerzel-bot` route-explanation experiment is captured in local
  commit `1074d15`. Its verdict is **do not adopt**: BAML improved malformed-output
  parsing, but independent semantic validation still caught plausible wrong
  repairs. Preserve the experiment as evidence; do not put it on a production
  path.
- Demerzel's canonical BAML clients and subscription-backed typed grader are
  already represented by merged work on `origin/master`.
- The stale `feat/baml-adoption` worktree is two commits ahead, eighteen behind,
  and dirty. Its uncommitted self-issued provider receipt conflicts with the newer
  fail-closed budget design, which explicitly treats such receipts as forgeable.
  Classify this lane `quarantined/superseded`; never merge or rebase it wholesale.

Approved role for BAML: typed parsing at selected LLM leaves, followed by canonical
schema/domain validation. It must not own budgets, authorization, queue state,
cross-repo contracts, or merge decisions.

## 2026-08-02 local snapshot

This snapshot is evidence, not a cleanup list.

| Lane | Proven state | Classification | Next action |
|---|---|---|---|
| GA `feat/589-performance-intent-tracer` / PR #598 | 9 local commits ahead; 29 dirty paths; PR is overloaded and unstable | blocked/preserve | split coherent commits into clean topic branches; never build the factory in this checkout |
| GA PR #625 | draft, green CI, SHA-bound blocking review posted | blocked upstream | wait for corrected head; re-review exact SHA |
| GA PR #626 | draft, green CI, SHA-bound blocking review posted | blocked upstream | wait for corrected head; re-review exact SHA |
| Demerzel Gaia / PR #940 | 8 clean local commits ahead; tests pass; spec review found P0/P1 defects | active/blocked | TDD repair of atomic claim seam, then split brokerless slice from daemon work |
| Demerzel `feat/baml-adoption` | ahead 2, behind 18, 6 dirty paths; unsafe receipt approach superseded | quarantined | produce patch-level salvage report only; no wholesale rebase/merge |
| `demerzel-bot` BAML experiment | local commit `1074d15`; explicit do-not-adopt verdict | evidence/archive | retain as benchmark; optionally publish as a closed experiment PR |
| Demerzel primary / PR #930 | ahead 1; source/state edits plus untracked visual captures | active/owned unknown | bind each path to its originating session/PR before any edit |
| GA catalog-wrapper worktree | 28 modified `.agent/skills/**` files | blocked/unknown | determine whether this is a generated sync or intentional skill migration |
| GA dispersion worktree | 663 behind, 4 dirty paths | stale/preserve | snapshot diff and test value before choosing salvage or archive |
| ix main worktree | clean branch tip but 3 dirty paths including Demerzel submodule | active/unknown | bind to ix session/issue; no submodule mutation from GA factory |
| tars PR #207 worktree | 2 dirty local-only artifacts | active/low-risk | classify settings change vs crash dump; preserve branch |
| hari `main` | 1 local prototype commit ahead, no PR | ready for triage | review the prototype and either publish a narrow PR or archive evidence |
| agent-blackbox worktree | 1 ahead plus untracked state; content appears represented on local `main` | likely superseded | prove patch equivalence before archival |
| AFK harness | no remote; 5 dirty setup/package paths | active/local-only | capture provenance and decide canonical home before integration |

Clean worktrees already bound to open PRs remain in the GitHub queue; they are not
duplicated as local recovery work unless they also have local-only state.

## GitHub queue reconciliation

GitHub is the durable queue; local packets add physical-worktree and provenance
evidence only. The 2026-08-02 triage produced these decisions:

- `.github#68` is the canonical AFK/factory program. Duplicate `.github#66` was
  closed and child references were repointed.
- GA PRs #625 and #626 remain draft and blocked by exact-SHA review findings even
  though CI is green. Green checks are necessary evidence, not readiness.
- GA PRs #598, #608, #615, and the older non-draft queue (#559, #576, #579, #588)
  are unstable and cannot enter an autonomous-ready pool.
- GA PR #618 needs a stop/go review against the JEPA triage plan; its green checks
  do not establish that the research spike is informative.
- GA PR #617 is a label-triggered research spike and needs human scope review.
- Demerzel PR #930 is dirty with failing checks; PR #940 remains the clean design
  boundary for Gaia, not a target for pushing the local implementation wholesale.
- Demerzel #789 became the first live reconciliation packet. Its scheduled Jules
  lane was impossible to approve safely: reruns retain the old checkout SHA while
  fresh runs derive a new run-bound approval hash, and #937 documents that any
  allowed run also leaks its reservation. The packet was rerouted to an isolated
  local Codex worktree with no metered invocation; draft PR #950 at SHA `9f7f352`
  implements the requested generator and passed 486 Python tests, the Demerzel
  verifier, and its initial GitHub checks. The Jules trigger labels were removed.
- Tars PRs #207 and #219 are unstable; #221 is clean but still pending. Agent
  Blackbox draft PR #50 is the canonical path-matcher implementation candidate.

No PR was marked ready or merged during reconciliation.

## Learned-transition models and V-JEPA 2 boundary

The factory should record deterministic action-to-observation trajectories now:
input state and exact SHA, authorized action, tool/model identity, resulting state,
verifier and reviewer verdicts, elapsed time, and cost. This produces the dataset
needed for later prioritization and risk estimation without placing a learned model
in a safety-critical path.

For now the executable harness is the symbolic world model. Establish persistence
and simple calibrated baselines first. A JEPA-style action-conditioned latent
predictor may later advise queue priority, expected verifier failure, or likely
cost only after the promotion gates in `.github#63` are met. It must never authorize
an edit, invalidate a claim, override a verifier, or grant ready/merge authority.

## Tracer-bullet stages

### Stage 1 — reconciliation artifact

Produce a read-only local-work snapshot with stable packet IDs and explicit
classification. Test against temporary Git repositories and fixtures; never use a
test that mutates the operator's real worktrees.

Dependency: consume the canonical task/evidence contracts and path matcher from
agent-blackbox #44/#46. Do not reimplement them in GA.

Success:

- discovers dirty, ahead/behind, detached, missing, and PR-bound lanes;
- distinguishes active process/session ownership from historical provenance;
- reports stale snapshot if any source head or dirty-path digest changes;
- performs zero writes to discovered repositories.

### Stage 2 — one Gaia packet end to end

Use TDD at `Store.check_and_claim` and the PreToolUse hook boundary. Correct the
P0 race and fail-closed Git/liveness semantics in the brokerless slice. Keep daemon
work out of this packet.

Success:

- a concurrent second-edit-cycle regression is red before the fix and green after;
- all Gaia focused tests and `pwsh scripts/verify.ps1` pass;
- an independent reviewer certifies the exact head SHA;
- the corrected brokerless slice is pushed as a new draft implementation PR linked
  to #940 and #948, but is not marked ready or merged.

### Stage 3 — queue runner

Advance exactly one eligible packet in a new worktree, emit postflight evidence,
and stop. Reuse supervised-loop policies and kill switches.

Success:

- no mutation occurs without an uncontested claim and explicit authority;
- verifier and review evidence name the same head SHA;
- changing HEAD invalidates readiness;
- budget exhaustion and `STOP`/HALT produce a durable stopped packet.

Persist the authoritative packet transition and immutable evidence reference before
deriving Markdown/JSON views or notifications. Release only the exact lease token
and generation held by the packet.

### Stage 4 — optional throughput

Only after multiple clean packet runs, allow 3–5 independent packets concurrently.
Concurrency is across isolated worktrees; Gaia protects accidental shared-tree
editing. Merge authority remains a separate operator decision.

## Reversibility and one-way doors

The snapshot, packet queue, and postflight artifacts are additive and removable.
Gaia remains advisory during the tracer bullet. Hard blocking, a frozen packet
schema, cross-host coordination, production deployment, and BAML as a schema
authority are one-way doors and require separate explicit approval.

Revisit the design if packet bookkeeping costs more operator attention than it
saves over five completed packets, or if Gaia's measured collision fire rate makes
warnings noisy enough to be ignored.

## Open operator decision

The current authority boundary is **push corrected branches and stop before
ready/merge**. Granting `ready` or `merge` authority is deliberately separate.
