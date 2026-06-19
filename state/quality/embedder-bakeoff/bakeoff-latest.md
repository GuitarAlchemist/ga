# Embedder bake-off - Phase 0

Corpus: routing-eval, 166 in-scope prompts / 16 intents. cosine 1-NN 5-fold CV (router-mechanism proxy) + intent silhouette + single-embed latency.
Latency gate (decision #2): absolute chat-path budget = **500ms** p95 (1.5x-nomic ~152ms was rejected as too strict - see verdict).

| embedder | dim | CV acc (router proxy) | silhouette | p50 ms | p95 ms | within budget |
|---|---:|---:|---:|---:|---:|:--:|
| nomic-embed-text (baseline) | 768 | 0.644 +/- 0.061 | 0.084 | 73 | 101 | yes |
| mxbai-embed-large | 1024 | 0.681 +/- 0.042 | 0.108 | 165 | 242 | yes |
| bge-large | 1024 | 0.705 +/- 0.037 | 0.115 | 163 | 238 | yes |
| snowflake-arctic-embed2 | 1024 | 0.650 +/- 0.079 | 0.095 | 249 | 310 | yes |
| voyage-3 (API) | - | _skipped: no VOYAGE_API_KEY_ | | | | |

## Verdict

**bge-large** wins: CV acc 0.705 vs nomic 0.644 (+6.1pt), silhouette 0.115 vs 0.084 (+37%), p95 238ms - negligible in a multi-second chat turn (well under the 500ms gate).

Next: build Phase 1 (purpose factory, eager) and validate the winner through the full RoutingEvalHarness in Phase 2 - it must clear the 0.934 production in-scope (the proxy here omits descriptions + hint boosts, so absolute numbers run lower by design) and pass the #412 routing-eval ratchet before promotion.
