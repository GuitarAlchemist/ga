---
id: <YYYY-MM-DD-kebab-slug>
date: <YYYY-MM-DD>
status: open            # open | active | concluded | superseded
domain: <music | code | embeddings | cross>
question: <one falsifiable sentence>
hypotheses:
  - claim: <what we expect>
    refuted_if: <the observation that would make us abandon it>
tools: []               # e.g. [ix_pca, ga_icv_neighbors, paper-search, tars]
artifacts: state/research/<slug>/
validators: []          # models/tools that independently checked the verdict, e.g. [fable-5, tars]
confidence: <low | medium | high>   # fill at conclusion
supersedes: null
superseded_by: null
---

# <Title — the question, phrased as a statement of intent>

**Date:** <YYYY-MM-DD>
**Type:** research (not a commitment to build)
**Question:** <restate the one-sentence question>

## TL;DR

<Two or three sentences a future reader gets even if they read nothing else.
State the verdict and the single most important caveat. Write this LAST, then
move it to the top.>

---

## 1. Question

Who is in pain not knowing this? What decision does the answer unblock? Why is
it worth an experiment rather than a guess?

## 2. Hypothesis

- **Claim:** <what we expect to find>
- **Refuted if:** <the concrete observation that kills the claim>
- **Prior art:** <what `/paper-search` / `/deep-research` already established —
  cite it. "Novel" only after you've checked.>

## 3. Method (reproducible)

The load-bearing section. Anyone must be able to re-run this from what's below.

```
# exact commands / tool calls, with args
# e.g.  ix_pca on <matrix>, k=<n>   →   state/research/<slug>/pca.json
#       ga_icv_neighbors chord="<x>" →  ...
```

- **Inputs:** <where the data comes from, exact path/snapshot>
- **Environment:** <ix binary, GA MCP, snapshot date — anything version-sensitive>

## 4. Evidence

What the experiment produced. Link committed artifacts under
`state/research/<slug>/`. Show the numbers, not a summary of the numbers.

## 5. Verdict

- **Answer:** <yes / no / partial — directly to the stage-1 question>
- **Confidence:** <low | medium | high> — and *why* that level.
- **Independent validation:** <which model/tool re-checked it; any dissent>
- **Belief recorded:** <hari observation id / Demerzel ref, if applicable>
- **One-way-door check:** <does the verdict justify a schema/dim/API change? If
  so it needs sign-off — link the follow-up plan/ADR, do NOT act inline.>

## 6. Next

What this opens up: a follow-up study, a plan, an ADR, or "closed — no action."
