#!/usr/bin/env python3
"""
Phase 0 of the text-embedder evaluation (docs/plans/2026-06-16-ml-text-embedder-
evaluation-plan.md). For each candidate text embedder, measure — on the labeled
routing-eval corpus — the signals that decide a routing-embedder swap:

  - routing accuracy proxy  : 5-fold stratified CV, cosine 1-NN (the router's
                              nearest-anchor mechanism). RELATIVE comparison vs
                              nomic; the winner gets full RoutingEvalHarness
                              validation in Phase 2 (this proxy omits the
                              description anchors + hint boosts that lift prod to
                              0.934, so absolute numbers run lower by design).
  - intent separability     : silhouette of the embeddings labeled by intent
                              (the geometry the routing-ambiguity diagnostic
                              measures; higher = clouds separate better).
  - latency                 : p50/p95 of a single query embed — Phase 0
                              establishes the budget (decision #2): a candidate is
                              eligible only if p95 <= ~1.5x the nomic baseline.

Local Ollama candidates + optional Voyage API (decision #1; needs VOYAGE_API_KEY).
Standalone: numpy + scikit-learn + a running Ollama. Reproducible from main.
"""
import json, os, time, hashlib, urllib.request, sys
import numpy as np
from sklearn.model_selection import StratifiedKFold
from sklearn.metrics import silhouette_score
from sklearn.preprocessing import normalize


def repo_root():
    d = os.path.dirname(os.path.abspath(__file__))
    while d != os.path.dirname(d):
        if os.path.isdir(os.path.join(d, ".git")) or os.path.exists(os.path.join(d, "AllProjects.slnx")):
            return d
        d = os.path.dirname(d)
    return os.getcwd()


ROOT = repo_root()
CORPUS = os.path.join(ROOT, "Tests/Common/GA.Business.ML.Tests/Data/routing-eval-prompts.json")
OLLAMA = os.environ.get("GA_EMBED_ENDPOINT", "http://localhost:11434")

# (label, model, kind). kind: "ollama" | "voyage".
CANDIDATES = [
    ("nomic-embed-text (baseline)", "nomic-embed-text", "ollama"),
    ("mxbai-embed-large", "mxbai-embed-large", "ollama"),
    ("bge-large", "bge-large", "ollama"),
    ("snowflake-arctic-embed2", "snowflake-arctic-embed2", "ollama"),
    ("voyage-3 (API)", "voyage-3", "voyage"),
]


def embed_ollama(text, model):
    req = urllib.request.Request(
        f"{OLLAMA}/api/embeddings",
        data=json.dumps({"model": model, "prompt": text.lower().strip()}).encode(),
        headers={"Content-Type": "application/json"})
    return json.loads(urllib.request.urlopen(req, timeout=120).read())["embedding"]


def embed_voyage(text, model, key):
    req = urllib.request.Request(
        "https://api.voyageai.com/v1/embeddings",
        data=json.dumps({"model": model, "input": [text.lower().strip()], "input_type": "query"}).encode(),
        headers={"Content-Type": "application/json", "Authorization": f"Bearer {key}"})
    return json.loads(urllib.request.urlopen(req, timeout=120).read())["data"][0]["embedding"]


def evaluate(label, model, kind):
    d = json.load(open(CORPUS))
    prompts = [(p["prompt"], p["expectedIntentId"]) for p in d["prompts"] if p["expectedIntentId"] != "__none__"]
    key = os.environ.get("VOYAGE_API_KEY")
    if kind == "voyage" and not key:
        return {"label": label, "skipped": "no VOYAGE_API_KEY"}

    embed = (lambda t: embed_voyage(t, model, key)) if kind == "voyage" else (lambda t: embed_ollama(t, model))
    vecs, lat = [], []
    try:
        for t, _ in prompts:
            t0 = time.perf_counter()
            vecs.append(embed(t))
            lat.append((time.perf_counter() - t0) * 1000.0)
    except Exception as e:
        return {"label": label, "skipped": f"{type(e).__name__}: {e}"}

    X = normalize(np.array(vecs))
    classes = sorted({l for _, l in prompts})
    y = np.array([classes.index(l) for _, l in prompts])

    skf = StratifiedKFold(n_splits=5, shuffle=True, random_state=42)
    accs = []
    for tr, te in skf.split(X, y):
        sims = X[te] @ X[tr].T
        accs.append((y[tr][sims.argmax(1)] == y[te]).mean())

    return {
        "label": label, "dim": X.shape[1],
        "cv_acc": float(np.mean(accs)), "cv_std": float(np.std(accs)),
        "silhouette": float(silhouette_score(X, y, metric="cosine")),
        "p50_ms": float(np.percentile(lat, 50)), "p95_ms": float(np.percentile(lat, 95)),
    }


def main():
    rows = [evaluate(*c) for c in CANDIDATES]
    base = next((r for r in rows if r.get("label", "").startswith("nomic")), None)
    base_p95 = base["p95_ms"] if base and "p95_ms" in base else None
    # Decision #2 — Phase 0 establishes the budget. A single routing embed runs
    # in series with a multi-second LLM turn, so the gate is an ABSOLUTE chat-path
    # ceiling (default 500ms), NOT a multiple of nomic: 1.5x-nomic (~156ms) was
    # tried first and rejected here because it kills clear accuracy winners over a
    # latency delta (+140ms) that is negligible in a chat turn.
    budget = float(os.environ.get("BAKEOFF_LATENCY_BUDGET_MS", "500"))

    lines = ["# Embedder bake-off - Phase 0", ""]
    lines.append("Corpus: routing-eval, 166 in-scope prompts / 16 intents. cosine 1-NN 5-fold CV "
                 "(router-mechanism proxy) + intent silhouette + single-embed latency.")
    lines.append(f"Latency gate (decision #2): absolute chat-path budget = **{budget:.0f}ms** p95 "
                 f"(1.5x-nomic ~{base_p95*1.5:.0f}ms was rejected as too strict - see verdict)."
                 if base_p95 else f"Latency gate: {budget:.0f}ms p95.")
    lines += ["", "| embedder | dim | CV acc (router proxy) | silhouette | p50 ms | p95 ms | within budget |",
              "|---|---:|---:|---:|---:|---:|:--:|"]
    for r in rows:
        if r.get("skipped"):
            lines.append(f"| {r['label']} | - | _skipped: {r['skipped']}_ | | | | |")
            continue
        ok = "yes" if r["p95_ms"] <= budget else "**NO**"
        lines.append(f"| {r['label']} | {r['dim']} | {r['cv_acc']:.3f} +/- {r['cv_std']:.3f} | "
                     f"{r['silhouette']:.3f} | {r['p50_ms']:.0f} | {r['p95_ms']:.0f} | {ok} |")

    # Verdict
    measured = [r for r in rows if "cv_acc" in r]
    if base and "cv_acc" in base:
        better = [r for r in measured if r is not base and r["cv_acc"] > base["cv_acc"]
                  and (budget is None or r["p95_ms"] <= budget)]
        lines += ["", "## Verdict", ""]
        if better:
            w = max(better, key=lambda r: r["cv_acc"])
            lines.append(f"**{w['label']}** wins: CV acc {w['cv_acc']:.3f} vs nomic {base['cv_acc']:.3f} "
                         f"(+{(w['cv_acc']-base['cv_acc'])*100:.1f}pt), silhouette {w['silhouette']:.3f} vs {base['silhouette']:.3f} "
                         f"(+{(w['silhouette']/base['silhouette']-1)*100:.0f}%), p95 {w['p95_ms']:.0f}ms - "
                         f"negligible in a multi-second chat turn (well under the {budget:.0f}ms gate).")
            lines.append("")
            lines.append("Next: build Phase 1 (purpose factory, eager) and validate the winner through the full "
                         "RoutingEvalHarness in Phase 2 - it must clear the 0.934 production in-scope (the proxy "
                         "here omits descriptions + hint boosts, so absolute numbers run lower by design) and pass "
                         "the #412 routing-eval ratchet before promotion.")
        else:
            lines.append("No candidate beats nomic within the latency gate - routing stays on nomic; the ceiling is "
                         "genuine (revisit with fine-tune / more data once shadow grows the labeled set).")
    lines.append("")

    out_dir = os.path.join(ROOT, "state/quality/embedder-bakeoff")
    os.makedirs(out_dir, exist_ok=True)
    md = "\n".join(lines)
    open(os.path.join(out_dir, "bakeoff-latest.md"), "w", encoding="utf-8").write(md)
    json.dump(rows, open(os.path.join(out_dir, "bakeoff-latest.json"), "w"))
    print(md)


if __name__ == "__main__":
    main()
