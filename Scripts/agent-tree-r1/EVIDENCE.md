# agent-tree-r1: evidence of the first real cycle (2026-11-14)

Parent: a local Claude Code session acting for the owner. Draft PR: https://github.com/GuitarAlchemist/ga/pull/858. Raw ledger: `evidence/tree.json`.

Labels: **cloud-executed** = a real GitHub Claude run answered a real comment; **locally verified** = run by the parent on Windows; **simulated** = the injected-sender unit tests.

| node | parent | agent / run | status | gap before -> after | parent verdict |
|---|---|---|---|---|---|
| root | - | local session | running | - | - |
| fix-gap-measure (write) | root | cloud-executed: run 43649154259, receipt comment 6885307238, response 6885312495, commit 1c7bb15 | reported, correlated | headline measure not behavioural (1) -> behavioural, 0 failing assertions (0) | accepted after parent re-ran the tests on 1c7bb15 (49 ok, 0 FAIL) |
| qa-1 (read-only) | root | cloud-executed: run 43650162072, receipt 6885445236, response 6885451742 | reported, correlated | - | task accepted; its code verdict on 1c7bb15 is **fail** |

Budget (#855 reading: the invoking run 43566102411 counts): 3 of 3 executions, 3 of 7 nodes, depth 2 of 3, 1 attempt per leaf, at most 1 child in flight, started 22:38 UTC (30 min cap).

## Independent QA found a real defect

QA (a separate run that shares no context with the author run, same GitHub integration identity) showed that a `-Simulated` report released the concurrency slot of a node that already had a live receipt, so a third live child could be launched. The parent reproduced it as a red test (3 failures), then fixed `Submit-ChildReport` (refuse `simulated_report_on_live_node`): 51 assertions pass. **That fix was not re-checked by an independent QA run: the execution budget was spent.** QA also listed README claims without a test and two weak spots of `Measure-BehaviouralGap` (a script that exits 0 without running any assertion is reported as gap 0); these are open.

## Deviations, stated plainly

- The child pushed to `gaia/agent-tree-r1` (fast-forward, no force) because its only push helper targets the session branch. Sole writer, so no overlap, but the "child writes its own branch" intent was not met.
- QA ran under the same GitHub integration as the author run. The contexts are distinct; the identity is not. Treat the QA as independent in context only.
- Never run: L3 (`dotnet build/test AllProjects.slnx`), Blackbox, independent human review. Nothing here is claimed ready.
- `cloud-validate.ps1` does not work on this machine (hardcoded Git Bash path); `bash Scripts/cloud-validate.sh` was used (158/158).

## Follow-up AGENTIC-TREE-858-FIX-R1 (locally verified, simulated tests, no new dispatch)

- Item 3 of the QA report fixed: `Measure-BehaviouralGap` no longer reports gap 0 when no assertion ran (`no_assertions_ran`) and strips ANSI colour codes before counting `FAIL:`. Red first: 4 failures without the fix; 60 assertions pass with it.
- Item 4 partly covered by new regression tests (resume from saved state keeps uncertain/no-replay and both slots, stop switch blocks live dispatch, uncorrelated live node cannot be reported, correlated report without evidence cannot be accepted). These pin existing behaviour and cannot go red by themselves; permission-intersection on reparent and min-budget inheritance remain untested.
- **No new real dispatch.** The cumulative limits of #855 are exhausted: 3 of 3 executions (invoking run 43566102411 counted) and the 30-minute window opened 22:38 UTC expired at 23:08 UTC. The fixes since `1c7bb15` (`dc2023cd1` and this one) have therefore had no independent QA run.
- The comments posted on #858 that trigger agents are the loop's dispatches only where the ledger lists them (`evidence/tree.json`: nodes `fix-gap-measure`, `qa-1`); the parent that posted them is a local session, so this proves delivery and correlation, not that a loop with no human session can dispatch.
