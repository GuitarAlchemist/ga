# agent-tree-r1 — bounded recursive supervision (GA-AGENT-TREE-R1, issue #855)

First slice only. Extends (does not replace) the one-cycle `supervised-loop`. All existing
preflight / stop / exact-SHA / promotion gates are untouched and still apply.

## Gap measured
**Headline (behavioural):** `Measure-BehaviouralGap` runs `AgentTree.test.ps1` in a child `pwsh` process and
reports the number of failing assertions (`FAIL:` lines). A non-zero exit with no counted failure (crash, parse
error) still reports gap >= 1, never 0. `Get-LoopGapReport` returns this as `headline_gap`.

**Secondary (presence only):** `Measure-LoopGap` greps Scripts/ + supervised-loop SKILL.md (excluding this dir
unless `-IncludeR1`) for 8 capability keywords. Before: 6 of 8 missing; after (including this dir): 0 of 8.
**Caveat:** this figure is keyword presence only, not a behavioural proof; it is reported as `presence_only`
and must not be used as the headline. Behaviour is covered by the test run behind the headline.

## Model
- Node: node_id, parent_id, scope, objective, baseline/after, acceptance, base/head SHA, budget, permissions,
  mode (read|write), status, evidence, execution_identity, parent_verdict, ancestry, events.
- Statuses: pending → dispatched | uncertain → reported (leaf self-report) → accepted | failed | blocked | escalated.
  `success/failure/blocked/unknown` are distinct report results; only the **immediate parent** can accept.
- Limits (cumulative, tree-wide): 3 live executions, 7 nodes, depth 3, 2 attempts/leaf, 2 concurrent in-flight children
  anywhere in the tree (enforced at live dispatch; per-parent only bounds planning), 30 min wall clock. Children inherit the intersection of parent permissions and min budget.
- Reparenting: root only, cycle/depth checked, can only shrink permissions; ancestry history preserved.
- Escalation records a proposal; it grants no privileges.
- Dispatch: dry-run by default (a draft is never a launch). Live requires an injected `-Sender` returning a
  receipt URL; failure ⇒ `uncertain` (spends budget, never replayed). Run/response correlation is a separate
  step (`Confirm-AgentRun`); an uncorrelated child cannot report or be accepted (unless explicitly `-Simulated`).
- Inbound requests: allow-listed sender, well-formed, single-use nonce. Stop switch halts all mutation.
- QA: `Request-IndependentQA` stays `pending` unless a distinct, non-producer, non-root identity starts.

## Run
`pwsh Scripts/agent-tree-r1/AgentTree.test.ps1` — deterministic; every dispatch in it is **simulated**.

## Not done in this slice
No real GitHub dispatch sender is wired (no network code ships here). Nothing was launched live.
