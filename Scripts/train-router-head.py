#!/usr/bin/env python3
"""
Train an L2-regularized softmax router head from labeled query->intent data and
emit `learned-head.json` in the EXACT format `LearnedHeadShadow` (the Hermes
Spike-A shadow evaluator) consumes.

Standalone: only needs a live Ollama embedder (nomic-embed-text, the same model
SemanticIntentRouter uses) + scikit-learn. Does NOT touch GA's build.

Apply contract replicated by the head (must match LearnedHeadShadow.Evaluate):
  - input = embedding of the lowercase+trimmed query,
  - the head L2-normalizes it,  logits[c] = b[c] + sum_f x[f]*W[f][c],
  - softmax -> argmax; decline if the winner's probability < tau.
So we train LogisticRegression on L2-NORMALIZED embeddings and write
  weights[feature][class] = coef_[class][feature],  bias[class] = intercept_[class].
PCA is intentionally NOT used: the current shadow evaluator disables PCA heads.

Usage:
  python Scripts/train-router-head.py                       # corpus only (reproducible)
  python Scripts/train-router-head.py --anchors dump.json   # + intent-anchor dump (more data)
"""
import argparse, json, hashlib, os, sys, urllib.request
import numpy as np
from sklearn.linear_model import LogisticRegression
from sklearn.preprocessing import normalize


def repo_root() -> str:
    d = os.path.dirname(os.path.abspath(__file__))
    while d != os.path.dirname(d):
        if os.path.isdir(os.path.join(d, ".git")) or os.path.exists(os.path.join(d, "AllProjects.slnx")):
            return d
        d = os.path.dirname(d)
    return os.getcwd()


def embed(text, model, endpoint, cache):
    key = hashlib.md5(text.lower().strip().encode()).hexdigest()
    if key in cache:
        return cache[key]
    req = urllib.request.Request(
        f"{endpoint}/api/embeddings",
        data=json.dumps({"model": model, "prompt": text.lower().strip()}).encode(),
        headers={"Content-Type": "application/json"},
    )
    cache[key] = json.loads(urllib.request.urlopen(req, timeout=120).read())["embedding"]
    return cache[key]


def main():
    root = repo_root()
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--corpus", default=os.path.join(root, "Tests/Common/GA.Business.ML.Tests/Data/routing-eval-prompts.json"))
    ap.add_argument("--anchors", default=None, help="optional routing-anchors-*.json dump (intent example anchors) for more training data")
    ap.add_argument("--out", default=os.path.join(root, "state/quality/router-head/learned-head.json"))
    ap.add_argument("--model", default=os.environ.get("GA_EMBED_MODEL", "nomic-embed-text"))
    ap.add_argument("--endpoint", default=os.environ.get("GA_EMBED_ENDPOINT", "http://localhost:11434"))
    ap.add_argument("--C", type=float, default=1.0, help="inverse L2 strength")
    ap.add_argument("--tau", type=float, default=None, help="decline threshold; auto-tuned from OOS prompts if omitted")
    ap.add_argument("--cache", default=os.path.join(root, "state/quality/router-head/.emb-cache.json"))
    args = ap.parse_args()

    cache = json.load(open(args.cache)) if os.path.exists(args.cache) else {}

    # ── Training data ────────────────────────────────────────────────────────
    corpus = json.load(open(args.corpus))
    in_scope = [(p["prompt"], p["expectedIntentId"]) for p in corpus["prompts"] if p["expectedIntentId"] != "__none__"]
    oos = [p["prompt"] for p in corpus["prompts"] if p["expectedIntentId"] == "__none__"]

    if args.anchors:
        a = json.load(open(args.anchors))
        in_scope += [(x["Text"], x["IntentId"]) for x in a["anchors"] if x["Kind"] == "example"]

    labels = sorted({l for _, l in in_scope})
    ci = {c: i for i, c in enumerate(labels)}
    print(f"training: {len(in_scope)} labeled prompts across {len(labels)} intents; {len(oos)} OOS for tau tuning")

    try:
        X = normalize(np.array([embed(t, args.model, args.endpoint, cache) for t, _ in in_scope]))
    except Exception as e:
        sys.exit(f"FAILED: embedding via {args.endpoint} ({args.model}) — is Ollama up? {type(e).__name__}: {e}")
    y = np.array([ci[l] for _, l in in_scope])
    dim = X.shape[1]

    # ── Train L2 softmax ─────────────────────────────────────────────────────
    clf = LogisticRegression(C=args.C, max_iter=4000, class_weight="balanced").fit(X, y)
    W = clf.coef_.T.astype(float)          # [feature][class]
    b = clf.intercept_.astype(float)       # [class]

    def head_probs(vecs):
        v = normalize(np.asarray(vecs))
        logits = v @ W + b
        logits -= logits.max(1, keepdims=True)
        e = np.exp(logits)
        return e / e.sum(1, keepdims=True)

    # ── tau: separate OOS (should decline) from in-scope (should keep) ───────
    in_maxp = head_probs(X).max(1)
    if args.tau is not None:
        tau = args.tau
    elif oos:
        oos_maxp = head_probs([embed(t, args.model, args.endpoint, cache) for t in oos]).max(1)
        # sweep tau; maximize (OOS correctly declined) + (in-scope kept)
        cands = np.unique(np.round(np.concatenate([in_maxp, oos_maxp]), 3))
        best, tau = -1.0, 0.4
        for c in cands:
            score = (oos_maxp < c).mean() + (in_maxp >= c).mean()
            if score > best:
                best, tau = score, float(c)
        print(f"  tau auto-tuned = {tau:.3f}  (OOS decline {(oos_maxp < tau).mean():.0%}, in-scope kept {(in_maxp >= tau).mean():.0%})")
    else:
        tau = 0.4

    # ── Self-check: head formula must match sklearn argmax ───────────────────
    head_pred = head_probs(X).argmax(1)
    assert (head_pred == clf.predict(X)).all(), "emitted-head formula disagrees with sklearn — format bug"

    head = {
        "dim": dim,
        "labels": labels,
        "weights": [[round(float(w), 6) for w in row] for row in W],
        "bias": [round(float(x), 6) for x in b],
        "tau": round(float(tau), 4),
        "pca": None,
        "_meta": {
            "trainer": "train-router-head.py",
            "embedder": args.model,
            "C": args.C,
            "n_train": len(in_scope),
            "n_intents": len(labels),
            "note": "Bootstrap head for shadow evaluation; retrain as RoutingShadowLog accumulates real labeled traffic.",
        },
    }
    os.makedirs(os.path.dirname(args.out), exist_ok=True)
    json.dump(head, open(args.out, "w"))
    json.dump(cache, open(args.cache, "w"))
    print(f"wrote {args.out}  (dim={dim}, {len(labels)} intents, tau={tau:.3f}, train-fit {(head_pred==y).mean():.0%})")
    print("Self-check passed: head formula matches sklearn argmax.")


if __name__ == "__main__":
    main()
