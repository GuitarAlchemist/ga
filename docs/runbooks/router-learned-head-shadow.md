# Router learned-head — assessment + shadow runbook

How to train the Hermes Spike-A learned router head, run it in **shadow** (logs
its decision alongside production's, never changes routing), and — the part that
matters — **when it's actually worth promoting**. Short answer today: not yet;
run it in shadow to collect data.

## Assessment (2026-06-16): a learned head does NOT earn promotion at current scale

The `SemanticIntentRouter` routes by max cosine over each intent's example
anchors (+ description + hint boosts). The question: would a learned softmax head
on the same nomic embeddings beat it? Measured faithfully — train on the 219
intent example anchors, predict the 166 held-out eval-corpus prompts:

| method | held-out accuracy |
|---|---|
| cosine 1-NN over anchors (≈ the router's bare mechanism) | 0.898 |
| cosine centroid (nearest class mean) | 0.892 |
| **softmax 768-d** (deployable in shadow — no PCA) | **0.904** |
| PCA64 + softmax (NOT shadow-deployable; evaluator disables PCA heads) | 0.916 |
| *production router, in-scope (cosine + descriptions + hints + threshold)* | *0.934* |

**Conclusion:** the deployable (no-PCA) head (0.904) ties bare cosine and lands
**below** the production router's 0.934 — promoting it would *regress* routing.
The best head (PCA64, 0.916) still trails production and isn't even runnable in
the current evaluator. The cause is data scale: 166 prompts / 16 classes / 768
dims — a parameter-free nearest-anchor router is well-suited to tiny data; a
~12k-parameter softmax head can match it but can't decisively beat a router that
*also* has curated descriptions and hint rules. **The embedder, not the head, is
the ceiling here.**

**Recommendation:**
1. Do not promote a learned head to production routing now.
2. Run the head in **shadow** to accumulate head-vs-prod disagreements on real
   traffic — that telemetry is also free labeled data.
3. Revisit promotion when the labeled set is ~5–10× larger (shadow log +
   `RoutingTelemetryLog`). At that scale a PCA+softmax head has a real shot — but
   it will need PCA support added to `LearnedHeadShadow.Evaluate` (currently
   disabled).

## Train a head

```bash
# Needs Ollama up with nomic-embed-text. Reproducible from main: corpus only.
python Scripts/train-router-head.py
# More training data: merge an intent-anchor dump (from the routing-ambiguity diagnostic).
python Scripts/train-router-head.py --anchors state/quality/routing-diagnostic/routing-anchors-<date>.json
```

Emits `state/quality/router-head/learned-head.json` in the exact `HeadDto` format
`LearnedHeadShadow` consumes: `{dim, labels, weights[feature][class], bias[class],
tau, pca:null}`. The trainer trains LogisticRegression on **L2-normalized**
embeddings (the head normalizes at apply time), writes `weights[f][c] =
coef_[c][f]`, auto-tunes `tau` to decline OOS while keeping in-scope, and
**self-checks** that its emitted formula matches sklearn's argmax — so the C#
shadow behaves identically. A committed bootstrap head (corpus-trained, 16
intents, tau 0.124) ships alongside.

## Run it in shadow

Shadow is OFF by default and **never changes the routing result** (any load/eval
failure no-ops). Activate (e.g. on the `feat/router-learned-head-shadow` branch
that carries `LearnedHeadShadow`/`RoutingShadowLog`):

```powershell
$env:GA_ROUTER_SHADOW = "1"
$env:GA_LEARNED_HEAD_PATH = "C:\...\ga\state\quality\router-head\learned-head.json"
# run the chatbot / routing-eval as usual
```

`SemanticIntentRouter` calls `LearnedHeadShadow.LogShadow` per route, appending a
per-day JSONL record (`query, prodChosen, headChosen, headConfidence,
headDeclined, agree`) via `RoutingShadowLog`. Mine those for: where the head and
the router disagree (candidate routing bugs or genuinely ambiguous intents — see
the routing-ambiguity diagnostic), and a growing labeled set to retrain on.

## Promote later (checklist, not now)

- [ ] Labeled set ≥ ~1–2k real queries (shadow log + telemetry).
- [ ] Add PCA support to `LearnedHeadShadow.Evaluate` (apply the stored
      projection before the linear layer); re-enable PCA heads.
- [ ] Head beats the production router's in-scope accuracy on a held-out set by a
      margin that clears the routing-eval ratchet (#412).
- [ ] Shadow-agree rate and disagreement review show no systematic regressions.
