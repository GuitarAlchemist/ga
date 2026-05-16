---
title: Embeddings snapshot pipeline — filename + cadence
status: planned
date: 2026-05-16
reversibility: two-way door
revisit_trigger: "next session implementing #182 (auto-optimize for embeddings) or whenever the quality dashboard shows the embedding panel as stale > 30 days"
related:
  - state/quality/embeddings/baseline.json
  - .github/workflows/chatbot-qa-snapshot.yml (template)
  - task #192
  - task #182 (auto-optimize loop blocked on this)
---

# Embeddings snapshot pipeline — filename + cadence

## The two gaps

**Filename strictness.** `ix-quality-trend`'s loader silently skips any snapshot whose stem isn't `YYYY-MM-DD`. The existing `state/quality/embeddings/2026-04-17-postrefactor.json` is invisible to the trend dashboard even though it's the more recent / better measurement (full_classifier_accuracy 0.7522 vs the pre-refactor 0.7473 in `2026-04-17.json`).

**Cadence.** The most recent embeddings snapshot is `2026-04-17`. No producer runs on a schedule. Compare with `chatbot-qa` which has a daily CI workflow (`.github/workflows/chatbot-qa-snapshot.yml`) and shipped fine. Without a cadence, the auto-optimize loop for this domain (#182) cannot baseline against anything fresh.

## Why now is enough

#192 has been pending since the trend gap surfaced 2026-05-16. The first deliverable (baseline.json — landed in this PR) unblocks the auto-optimize skill from refusing on missing-domain-contract. The producer workflow + filename fix remain.

## Proposed approach

### Step 1: producer CI workflow

Mirror `chatbot-qa-snapshot.yml` but cross-repo. The producer is the Rust binary `ix-embedding-diagnostics 0.1.0` in the ix sibling. Two options:

**Option A — checkout ix in the workflow, build it, run it.** Cleanest, no artifact storage. Adds ~3 min to the run for the Rust build.

```yaml
name: Embeddings Snapshot
on:
  schedule:
    - cron: '0 7 * * *'  # 07:00 UTC daily, after chatbot-qa
  workflow_dispatch:
permissions:
  contents: write
jobs:
  snapshot:
    runs-on: ubuntu-latest
    timeout-minutes: 30
    steps:
      - uses: actions/checkout@v4
        with: { token: ${{ secrets.PAT_TOKEN || secrets.GITHUB_TOKEN }} }
      - name: Checkout ix
        uses: actions/checkout@v4
        with:
          repository: spareilleux/ix
          path: ix
          token: ${{ secrets.PAT_TOKEN || secrets.GITHUB_TOKEN }}
      - name: Setup Rust
        uses: dtolnay/rust-toolchain@stable
      - name: Build ix-embedding-diagnostics
        run: cargo build --release -p ix-embedding-diagnostics
        working-directory: ix
      - name: Run + emit snapshot
        run: |
          DATE=$(date -u +%Y-%m-%d)
          ix/target/release/ix-embedding-diagnostics \
            --corpus state/voicings/optick.index \
            --out "state/quality/embeddings/${DATE}.json"
      - name: Commit if produced
        env: { GH_TOKEN: ${{ secrets.PAT_TOKEN || secrets.GITHUB_TOKEN }} }
        run: |
          DATE=$(date -u +%Y-%m-%d)
          SNAP="state/quality/embeddings/${DATE}.json"
          if [ ! -f "$SNAP" ]; then exit 1; fi
          git config user.name  "github-actions[bot]"
          git config user.email "github-actions[bot]@users.noreply.github.com"
          git add "$SNAP"
          if git diff --cached --quiet; then exit 0; fi
          git commit -m "chore(quality): embeddings snapshot ${DATE} [skip ci]"
          git push
```

**Option B — schedule on the ix repo CI, push to ga via cross-repo PAT.** Adds the build dependency where it belongs (ix owns its tool). Requires a PAT scoped to write to ga; more secret surface.

Recommend Option A. Build cost amortized across daily runs is fine.

### Step 2: filename normalization

The producer always writes `state/quality/embeddings/${DATE}.json`. If two runs in one day are valuable (e.g. before/after a refactor PR), use a SUFFIXED archive path (`state/quality/embeddings/archive/${DATE}-${tag}.json`) so the canonical YYYY-MM-DD slot is unambiguous.

No retroactive rename of `2026-04-17-postrefactor.json` — it's a historical artifact, leave it in place.

### Step 3: roundtrip validator skill

Before #182's auto-optimize loop can run unattended on the embeddings domain, it needs a `chatbot-qa-roundtrip-validate`-style skill that confirms a proposed change didn't regress the metric. Without it, baseline.json's `_harness.rollback_metadata.roundtrip_validator` stays `null` and the loop refuses to commit.

Out of scope for #192; lives under #182.

## Success criteria

- A daily CI workflow exists at `.github/workflows/embeddings-snapshot.yml`
- It produces `state/quality/embeddings/YYYY-MM-DD.json` on schedule
- The snapshot is loadable by `ix-quality-trend` (filename matches the strict pattern)
- The quality dashboard's embedding panel updates within 24h of merge

## Out of scope

- Auto-optimize loop for embeddings domain (that's #182; baseline.json prepares for it)
- Re-baselining the 0.7522 leak-detection threshold (only do after the producer is running and we have a few weeks of data)
- Loader permissiveness fix in `ix-quality-trend` — the producer-writes-canonical-name path is cleaner than making the loader smarter
