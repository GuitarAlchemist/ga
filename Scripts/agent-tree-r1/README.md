# agent-tree-r1 — bounded recursive supervision (GA-AGENT-TREE-R1, issue #855)

First slice only. Extends (does not replace) the one-cycle `supervised-loop`. All existing
preflight / stop / exact-SHA / promotion gates are untouched and still apply.

## Gap measured (`Measure-LoopGap`, grep over Scripts/ + supervised-loop SKILL.md, excluding this dir)
Before: 6 of 8 capabilities missing — durable parent_id nodes, parent verdict on child, cycle rejection,
dispatch receipt↔run correlation, independent-QA-pending, uncertain-dispatch-no-replay.
After (including this dir): 0 of 8. **Caveat:** this is a keyword presence check, not a behavioural proof;
behaviour is covered by `AgentTree.test.ps1`.

## Model
- Node: node_id, parent_id, scope, objective, baseline/after, acceptance, base/head SHA, budget, permissions,
  mode (read|write), status, evidence, execution_identity, parent_verdict, ancestry, events.
- Statuses: pending → dispatched | uncertain → reported (leaf self-report) → accepted | failed | blocked | escalated.
  `success/failure/blocked/unknown` are distinct report results; only the **immediate parent** can accept.
- Limits (cumulative, tree-wide): 3 live executions, 7 nodes, depth 3, 2 attempts/leaf, 2 concurrent children
  per parent, 30 min wall clock. Children inherit the intersection of parent permissions and min budget.
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
