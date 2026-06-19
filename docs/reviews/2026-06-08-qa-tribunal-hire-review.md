---
title: "QA Architect Tribunal — AI Hire Review"
date: 2026-06-08
author: claude-code (automated analysis)
status: insufficient-data
verdict: no-hires-recommended
dataset_size: 0
phase1_pr: 286
phase1_pr_state: open
phase1_pr_ci: failing
---

# QA Architect Tribunal — AI Team Hire Review (2026-06-08)

## TL;DR

**Zero verdicts exist. Phase 1 has not merged. No hiring analysis is possible.**

The right next action is to fix the 2 failing backend tests on PR #286 and merge it. Revisit
this review once ≥10 verdicts have accumulated (estimated: 2 weeks post-merge).

---

## 1. Verdict Volume

**Count: 0.**

`state/quality/verdicts/` does not exist. No verdict has ever been persisted to the
canonical path.

Root cause — two compounding issues:

**Issue A — Phase 1 PR #286 never merged.**
`feat(qa): QA Architect Tribunal Phase 1 — real evidence producers` was opened 2026-05-18
and is still open as of 2026-06-08 (~3 weeks). CI shows 2 failing backend tests out of
2,679 total (`Backend Tests: 2 ❌, 2,664 ✅`). No subsequent pushes or fixes have been
made to the branch. The PR has been stalled since its creation date.

**Issue B — Phase 0 `qa_emit_verdict` writes to `tmp/`, not `state/quality/verdicts/`.**
Even if the Phase 0 skeleton had been invoked, verdicts would have landed in
`Path.GetTempPath() + "ga-qa-verdicts/"` (see `Apps/GaQaMcp/Tools/QaTools.cs:287`), not
the contract-specified path. Phase 1 corrects this, but Phase 1 has not merged.

**Current production state (main branch, 2026-06-08):**

| Component | State |
|---|---|
| `QAArchitectAgent.cs` | Phase 0 skeleton — hardcoded `P3/informational` verdict, `EstimatedBlastScore: 0.0` |
| `QaTools.cs` — all tools | Phase 0 stubs (empty arrays / `n/a` outcomes), except `qa_score_quality_drift` which has partial optick-sae drift logic |
| `qa_emit_verdict` storage | `tmp/` (not `state/quality/verdicts/`) |
| `state/quality/verdicts/` | Directory does not exist |
| `qa-architect.yml` GitHub workflow | Not found — Phase 3 deliverable, not yet started |
| Daily 06:00 UTC sweep | Not running — Phase 4 deliverable |

---

## 2–5. Role Coverage / Followup Patterns / Escape Analysis / Capacity Utilization

All four sections require verdict data. With 0 verdicts, none of these analyses can be
performed without speculation, which this review declines to produce per the task constraint
("if the verdict feed is empty or thin, say so plainly and don't pad with speculation").

---

## 6. Phase 1 PR #286 — What It Would Deliver When Merged

For context on what Phase 1 adds (from the PR description, verified against the branch):

- **`qa_verify_invariants`**: OPTIC-K dim check (reads `EmbeddingSchema.TotalDimension=240`,
  not hardcoded), 5-layer bottom-up rule (csproj reference graph walk), contract-locked
  field check (`QaVerdict.SchemaVersion == 1`).
- **`qa_assess_blast_radius`**: git-diff-based static analysis, maps file paths to 9 layers,
  detects one-way-door crossings (EmbeddingSchema.cs, qa-verdict.schema.json, QaTools.cs).
- **`qa_score_quality_drift`**: new `voicing-analysis` and `embeddings` branches alongside
  existing `optick-sae`.
- **`CriticAgent` as `semantic_judge`**: wired into `QAArchitectAgent.ProcessAsync`; score
  propagates to `reviewer_chain[].score`.
- **`qa_emit_verdict` storage**: corrected from `tmp/` to contract §4 layout
  (`state/quality/verdicts/<repo>/<pr>/<verdict_id>.json`).
- **30+ new tests** across 5 test files.

**Known CI blocker on #286**: 2 failing backend tests (run logged at
`https://github.com/GuitarAlchemist/ga/runs/76498803181`). The agent-blackbox-risk-report
also flagged `diff.very_large_line_count` (1,474 lines) as high risk, and
`"concern"` outcome not in schema enum as contract concern C1.

---

## 7. Recommendation

**Hires: 0.**

This is not a tribunal quality problem; it is a Phase 1 delivery problem. The tribunal
cannot be evaluated for gaps or missing roles until it is actually producing verdicts.

**Immediate next action (blocking everything else):**

1. Diagnose the 2 failing backend tests on PR #286
   (`https://github.com/GuitarAlchemist/ga/runs/76498803181`).
2. Fix and push; re-request CI.
3. Resolve contract concern C1 (`"concern"` outcome enum) — Option A (add to schema) is
   recommended per the PR discussion; it's an additive v0.1 change.
4. Merge #286.

**Post-merge gate for re-running this review:**

- Let the tribunal run on ≥10 real PRs (estimated 2 weeks of normal dev cadence).
- Re-run this review script once `state/quality/verdicts/` has ≥10 JSON files.
- Only then does role-coverage, followup-pattern, and escape analysis have a real dataset
  to work from.

**Why not hire speculatively?**
The plan's reviewer taxonomy (`blast_radius`, `semantic_judge`, `regression_replay`,
`gap_analysis`, `contract_audit`, `accessibility`, `performance`, `security`,
`architecture`, `human`) already covers the intended surface. Phase 1 wires
`blast_radius` and `semantic_judge`. Phase 2 (not yet started) wires `gap_analysis` and
`regression_replay`. The remaining roles — `performance`, `security`, `accessibility`,
`contract_audit` — map directly to existing compound-engineering personas
(`ce-security-reviewer`, `ce-performance-reviewer`, `ce-data-integrity-guardian`) and
Demerzel pipelines (`weakness-probe.ixql`, `governance-shake-test.ixql`). There is no
evidence-based gap requiring a new agent because there is no evidence at all.

---

## Appendix — Evidence Trail

| Fact | Source |
|---|---|
| `state/quality/verdicts/` does not exist | `ls /home/user/ga/state/quality/verdicts/` → DIRECTORY_MISSING |
| Phase 0 stubs confirmed | `Apps/GaQaMcp/Tools/QaTools.cs` — every tool except optick-sae drift returns empty/hardcoded |
| `qa_emit_verdict` writes to `tmp/` | `QaTools.cs:287`: `Path.Combine(Path.GetTempPath(), "ga-qa-verdicts")` |
| `QAArchitectAgent` is Phase 0 | `QAArchitectAgent.cs:44`: "Phase 0 skeleton — hardcoded verdict" |
| PR #286 state | GitHub: open, `merged=null`, created 2026-05-18, last updated 2026-05-18 |
| PR #286 CI | Backend Tests: 2 ❌ out of 2,679; `CI Summary: failure` |
| No `qa-architect.yml` workflow | Not present in `.github/workflows/`; Phase 3 deliverable |
| Plan Phase 1 target date | "week 2" from 2026-05-02 = approximately 2026-05-18 (matched PR open date) |
