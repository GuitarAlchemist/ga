---
id: 2026-08-09-mechanical-tensors-agentic-systems
date: 2026-08-09
status: concluded
domain: cross
question: Can mechanics-derived stress, strain, shear, compression, wrench, buckling, and fatigue models improve the prediction and control of Gaia agent coordination beyond graph-flow and queueing baselines?
hypotheses:
  - claim: Discrete balance, graph-gradient, queueing, and stability models are operationally useful now; continuum tensors are useful only after an identifiable geometric coarse-graining.
    refuted_if: A mechanics-derived tensor without a stable geometry, units, and covariance law outperforms preregistered scalar and graph baselines on held-out failures and remains invariant under harmless basis and unit changes.
tools: [duckdb-1.5.3, ix_graph, ix_eigen, jbcontext, primary-source-review]
artifacts: state/research/mechanical-tensors-agentic-systems/
validators: [independent-primary-source-review, coordinator-reproduction]
confidence: medium
supersedes: null
superseded_by: null
---

# Mechanical tensors for agentic systems: a discrete-first research verdict

**Date:** 2026-08-09  
**Type:** research (not a commitment to build)  
**Question:** Can mechanics-derived concepts predict or control Gaia/IX agent coordination better than simpler graph-flow, queueing, and control models?

## TL;DR

Partially. The transferable core is not the vocabulary of mechanical stress; it is the mathematical discipline of **balance laws, boundary flux, invariance, calibrated response laws, stability, and cumulative damage**. Gaia should adopt discrete graph flow, incidence/Laplacian operators, queue backpressure, Petri-net invariants, and controlled stability/fatigue experiments now.

A Cauchy stress tensor is admissible only after Gaia has a stable task/capability geometry, commensurate units, local directions, and evidence that traction is approximately linear in direction. Until then, call the observable a **load matrix** or **edge-flow field**. A mechanical torseur/wrench is rejected for the current product because Gaia has no natural SE(3) frame, reference-point moment, or twist-power dual—the defining structures in the [robotics formulation of wrenches](https://modernrobotics.northwestern.edu/nu-gm-book-resource/3-4-wrenches/).

## Résumé exécutif en français

Le transfert est possible, mais à condition de ne pas confondre une métaphore avec une structure mathématique.

- **À adopter maintenant :** lois de bilan sur le graphe, pression de file, backpressure, gradients d’interface, Laplacien, détection des cycles, stabilité, fiabilité et fatigue expérimentale.
- **À adapter prudemment :** compression comme demande/capacité, cisaillement comme différentiel d’état entre interfaces, flambage comme perte brusque de stabilité sous charge, fatigue comme dégradation cumulative après handoffs/retries.
- **À différer :** tenseur de contrainte et matrice de stress au sens fort, jusqu’à ce qu’un espace de tâche géométrique stable et testable existe.
- **À rejeter pour Gaia v1 :** un « torseur agentique » et toute contrainte équivalente de type von Mises combinant arbitrairement tokens, secondes, dollars et erreurs.

Le premier tracer bullet doit rester read-only : dériver des vues DuckDB depuis `events.jsonl`, mesurer backlog, débit, latence p95, résidus de bilan, énergie de désaccord et churn cyclique, puis vérifier si ces variables prédisent un stall ou un handoff mieux que trois null models simples. Aucun routage automatique ne doit dépendre de cette couche avant une validation hors échantillon et un load/fault experiment contrôlé.

---

## Research protocol

### Question

Can mechanics-derived observables improve prediction or control of an agent network, and which mechanical concepts remain mathematically valid on Gaia's discrete event graph?

### Hypothesis

- **Claim:** discrete balance, graph-gradient, queueing, and stability models are useful now; a continuum stress tensor is useful only after a stable geometric coarse-graining is identifiable.
- **Refuted if:** a tensor model without that geometry remains covariant under harmless basis/unit changes and outperforms queue-pressure and graph baselines on held-out failures.

### Method

1. Derive the defining mathematical obligations from primary mechanics, robotics, graph, queueing, control, reliability, and causal-inference sources.
2. Inspect the implemented GA, Gaia Interagent, IX, DuckDB, ix-duck, ix-pipeline, and IXQL seams rather than assuming planned capabilities exist.
3. Reproduce event counts, backlog, and acknowledgement latency from fixed Gaia JSONL artifacts with DuckDB.
4. Run bounded IX graph and eigensystem checks on the observed live-smoke topology.
5. State null models, invariance tests, causal interventions, and rejection conditions before recommending a tracer.

### Evidence standard

A conceptual resemblance is not evidence. Adoption requires a defined observable, units, invariants, a reproducible derivation, and predictive or control value beyond simpler baselines. Local smoke artifacts establish feasibility of measurement, not production validity.

## 1. The category error to avoid

An `actor × resource × time` array is not automatically a tensor in the mechanical sense. A tensor requires declared vector spaces and a transformation law. In continuum mechanics, the Cauchy stress tensor is specifically the linear map that sends an oriented plane normal to the traction on that plane:

\[
t(n) = \sigma n.
\]

MIT's [stress and momentum-balance derivation](https://ocw.mit.edu/courses/16-21-techniques-for-structural-analysis-and-design-spring-2005/466ce40eea21d45e1093edccbc9f276f_unit2_notes.pdf) obtains this relation from local balance on a shrinking tetrahedron and then shows how the components transform under a change of coordinates. Trace, determinant, eigenvalues, and principal stresses are meaningful because they are invariants of that operator, not because the data happens to be stored in a matrix.

For Gaia, tokens, seconds, dollars, queue entries, disk bytes, and failure counts are not commensurate. They may be retained as a typed resource vector, or normalized by separately declared capacities, but they must not be summed into a scalar “equivalent stress” without an explicit metric and calibration.

## 2. Verdict by concept

| Concept | Verdict | Agentic interpretation | Gate that prevents metaphor laundering |
|---|---|---|---|
| Balance laws | **ADOPT** | Inventory change equals inflow minus outflow plus sources minus sinks. | Persist and reconcile the residual for every actor/window. |
| Graph flow / incidence matrix | **ADOPT** | Message, work, evidence, and budget flows over directed edges. | Typed units and conservation semantics per resource. |
| Queue backpressure | **ADOPT** | Route or pause work based on differential backlog and service capacity. | Stability/load experiment; budget gates remain independent and fail-closed. |
| Graph Laplacian | **ADOPT** | Interface disagreement and roughness energy. | Invariant under actor relabeling; weights must be defined. |
| Hodge / cycle decomposition | **ADOPT where needed** | Separate source-to-sink progress from retry/handoff circulation. | Pairwise graph for edges; simplicial complex only for genuine group interactions. |
| Petri-net invariants | **ADOPT** | Boundedness, liveness, deadlock freedom, conserved tokens/resources. | Formal place/transition semantics, not prose labels. |
| Tensor mathematics | **ADAPT conditionally** | Multilinear operators with declared spaces, bases, units, and covariance laws. | A multidimensional array is insufficient; require invariance and held-out predictive gain. |
| Mechanical stress | **ADAPT, not literalize** | Prefer queue pressure, graph stress, or a calibrated directional response operator. | Name the actual observable; do not imply continuum balance where none exists. |
| Compression | **ADAPT** | Normalized demand/capacity or queue utilization. | Use queueing mathematics; do not import solid-mechanics formulas. |
| Shear | **ADAPT** | Normalized interface differential or incompatible progress directions. | Must be an edge gradient, not generic disagreement/message volume. |
| Strain | **ADAPT narrowly** | Relative displacement from a reference configuration or edge-state gradient. | Requires a stable reference state and metric. |
| Constitutive law | **ADAPT** | Runtime/version/workload-specific response of latency, errors, or recovery to load. | Fit and validate; never assume Hooke's law or linearity. |
| Buckling | **ADAPT strongly** | Abrupt loss of coordination stability under load before local failure. | Controlled load ramp, perturbation, hysteresis, and eigenmode evidence. |
| Fatigue | **ADAPT strongly** | Cumulative degradation over repeated handoffs, retries, rotations, or recoveries. | Repeated cycles plus retained damage state; survival/hazard validation. |
| Fracture | **ADAPT** | Persistent contract flaw and propagation across dependencies. | A single task failure is not fracture. |
| Stress centrality | **ADOPT as a baseline** | Count of shortest paths through a node; keep normalized betweenness as a separate feature. | Compare with observed flows; shortest-path routing may be false. |
| Rigidity stress matrix | **ADAPT cautiously** | Equilibrium of relative constraints in a stable task embedding. | Requires an actual configuration and fixed geometric constraints. |
| Cauchy stress tensor | **ADAPT for research only** | Local directional load-response operator. | Full-rank directions, acceptable conditioning, covariance, and held-out predictive gain before product use. |
| Torseur / wrench | **REJECT for Gaia v1** | No natural force/moment pair in SE(3). | Revisit only with a real frame, reference-point law, and invariant dual pairing. |
| Arbitrary tensor dashboard | **REJECT** | A multidimensional table mislabeled as mechanics. | Rename it matrix/cube/relation unless the tensor law is proved. |

## 3. The minimum mathematical corpus

### Required now

1. **Linear algebra and typed dimensional analysis** — vector spaces, duals, bilinear forms, rank, conditioning, eigensystems, singular values, units, and nondimensionalization.
2. **Graph flow** — oriented incidence matrix `B`, edge flow `f`, source/sink vector `b`, and conservation `Bf=b`.
3. **Graph Laplacian and spectral theory** — `L=BWB^T`, connectivity, modes, and Dirichlet energy.
4. **Discrete exterior calculus / Hodge theory** — gradients, curls/circulation, harmonic components, and boundary-aware discrete operators.
5. **Queueing and stochastic-network control** — arrival/service processes, utilization, tail latency, stability regions, max-weight/backpressure, and heavy-tail caveats.
6. **Dynamical systems and control** — state-space models, observability, local Jacobians, Lyapunov stability, eigenvalue/unit-circle crossings, bifurcation, and hysteresis.
7. **Reliability and survival analysis** — hazard, censoring, repeated-cycle damage, repairable systems, change points, and recovery-time distributions.
8. **Experimental design and causal inference** — randomized load/fault interventions, confounders, preregistered outcomes, calibration, and held-out evaluation.
9. **Information theory** — entropy, mutual information, channel capacity, coding overhead, and information loss. Shannon's original [mathematical theory of communication](https://reach.ieee.org/primary-sources/a-mathematical-theory-of-communication/) gives the correct vocabulary for message uncertainty and capacity; entropy is not mechanical stress.
10. **Information geometry** — Fisher information and divergences on statistical manifolds, but only when agent outputs are probability distributions. [Amari's invariant differential-geometric treatment](https://www.jstage.jst.go.jp/article/bjsiam/2/1/2_KJ00005767586/_article/-char/en) supplies a legitimate geometry for changing model distributions, not for arbitrary task records.
11. **Multi-agent organization theory** — task announcement, bidding, award, commitment, partial plans, and repair. The original [Contract Net protocol](https://www.reidgsmith.com/The_Contract_Net_Protocol_Dec-1980.pdf) and Harvard's open copy of [SharedPlans](https://dash.harvard.edu/server/api/core/bitstreams/7312037c-4ba2-6bd4-e053-0100007fdf3b/content) are stronger null explanations for many coordination failures than physical-load analogies.

### Conditional corpus

- **Continuum mechanics and homogenization** only if many local agents/interactions admit a stable coarse-graining scale.
- **Tensor calculus** only when bases and covariance laws are explicit.
- **Rigidity theory and stress matrices** only when agent states have a stable geometric configuration. Their genuine multi-agent use is formation control with geometric constraints, as in [affine formation maneuver control](https://eprints.whiterose.ac.uk/id/eprint/127409/), not general task queues.
- **Screw theory / SE(3)** only if a real pose, frame transform, moment reference, and twist-wrench power pairing appear. They do not exist in current Gaia coordination.

## 4. Discrete-first model for Gaia

### Variables, units, assumptions, and boundaries

| Symbol | Meaning | Unit / type |
|---|---|---|
| `q_i^k(t)` | Actor `i`'s inventory of resource `k` at time `t` | Typed count: messages, work items, bytes, tokens, or currency; never mixed |
| `f_ij^k` | Resource `k` transferred from `i` to `j` in one window | Same unit as `q^k` per window |
| `s_i^k`, `c_i^k` | Exogenous creation and terminal consumption | Same unit as `q^k` per window |
| `r_i^k` | Explicitly recorded reconciliation, expiry, or archival adjustment | Same unit as `q^k` per window |
| `Q_i^{k,max}` | Finite inventory or backlog limit | Same unit as `q^k` |
| `lambda_i^k`, `mu_i^k` | Arrival and service rates | Resource units per second |
| `x_i` | One declared normalized actor state | Dimensionless, or one common declared unit |
| `w_ij` | Edge confidence/capacity/interaction weight | Non-negative, with its semantic and normalization declared |

The model assumes a fixed actor/edge identity map inside each analysis window, monotonic event ordering after deterministic tie-breaking, explicit treatment of in-flight work, and no silent conversion between resource types. A window boundary is an accounting boundary, not a physical surface. External arrivals, cancellations, expiry, archive/compaction, and missing telemetry must be represented explicitly; otherwise the balance residual is uninterpretable.

Let `q_i^k(t)` be actor `i`'s inventory of resource `k` at time `t`, and `f_ij^k` the flow from actor `i` to actor `j` during a window. This is the discrete conservation structure used in [network-flow formulations](https://optimization.cbe.cornell.edu/index.php?title=Network_flow_problem):

\[
q_i^k(t+\Delta)-q_i^k(t)
= \sum_j f_{ji}^k - \sum_j f_{ij}^k + s_i^k-c_i^k+r_i^k.
\]

The measured balance residual is:

\[
R_i^k = \Delta q_i^k-(\mathrm{in}_i^k-\mathrm{out}_i^k+s_i^k-c_i^k+r_i^k).
\]

`R=0` is exact for countable protocol resources once known in-flight work is included. Nonzero unexplained residual means missing telemetry or inconsistent semantics; it is not something a model should smooth away.

For a normalized node state `x` (for example contract-version lag or evidence completeness), the edge differential is:

\[
g=B^T x,
\]

and the weighted disagreement energy is:

\[
E=x^T Lx=\sum_{(i,j)}w_{ij}(x_i-x_j)^2.
\]

This gives a rigorous “interface shear” candidate without pretending that the graph is a continuum. The graph-signal interpretation follows the [graph Laplacian and Dirichlet-energy framework](https://arxiv.org/abs/1211.0053); cycle/circulation separation can use a [Hodge 1-Laplacian](https://arxiv.org/abs/1807.05044) only when the underlying edge and higher-order interaction semantics are real.

For each separately typed resource, distinguish two ratios that must not be conflated. Inventory pressure is:

\[
p_i^k(t)=\frac{q_i^k(t)}{Q_i^{k,\max}(t)},
\]

when the finite inventory limit is measured and positive. Queue utilization is instead:

\[
\rho_i^k(t)=\frac{\lambda_i^k(t)}{\mu_i^k(t)},
\]

with arrival and service rates in the same units per time. Differential backlog `q_i^k-q_j^k` (or a separately justified normalized pressure difference) motivates [max-weight/backpressure scheduling](https://drum.lib.umd.edu/items/571fda52-aefb-4497-9a2d-69d8c7c907b9); neither ratio authorizes routing by itself. Budget pressure remains governed by the product's 70/85/100% circuit breakers, and the observer cannot grant permission to spend.

### When a tensor would become admissible

Suppose a future task/capability model supplies positions `z_i in R^d`, local directions `n_ij`, and directional traction observations `t_ij`. A local operator `sigma_i` may be estimated from:

\[
t_{ij}\approx \sigma_i n_{ij}.
\]

Promotion requires all of the following:

1. the task geometry and metric are versioned and stable;
2. the neighborhood directions span `R^d` and the fit is well-conditioned;
3. units are commensurate or explicitly normalized;
4. the fit holds on held-out directions and workloads;
5. under a basis change `Q`, the estimate transforms as `sigma' = Q sigma Q^T` (orthonormal case), while predictions remain unchanged;
6. it beats scalar queue pressure, centrality, and graph-gradient baselines.

If any gate fails, retain the edge-flow field or call the fitted object a response matrix/Jacobian.

A more plausible first-order object is often a response Jacobian \(J_{ab}=\partial y_a/\partial x_b\), or the dimensionless elasticity \(E_{ab}=\partial\ln y_a/\partial\ln x_b\), estimated under controlled perturbations. A buckling-like claim requires evidence that a local discrete dynamics Jacobian crosses the unit circle, with mode shape, hysteresis, and recovery; the continuum Euler-buckling formula is not transferable by analogy.

## 5. Local evidence reproduced

### Gaia live smoke

Source:

`C:\tmp\gaia-wayfinder-plus\gaia-claude-codex-interop-prototype\.gaia-interop-prototype-data\live-smoke\events.jsonl`

The fixed artifact contains 3 registrations, 5 sends, 2 acknowledgements, 3 heartbeats, 3 inbox polls, and 1 handoff. Joining `message.sent` to `message.acked` in DuckDB produced acknowledgement latencies of **123,189 ms** and **24,720 ms**. Three messages were unacknowledged at the end; reconstructed incoming backlog reached 3 for `act-0001`, while `act-0002` returned to 0.

This is **delivery pressure only**. Gaia explicitly defines acknowledgement as receipt, not agreement or completion. The sample is far too small for predictive inference.

### Synthetic 16-worker contention probe

Source:

`C:\tmp\gaia-wayfinder-plus\gaia-claude-codex-interop-prototype\.gaia-interop-prototype-data\concurrency-probe\events.jsonl`

The artifact has 96 sends and 96 acknowledgements. Per-worker medians were approximately 4–8 ms, but p95 reached 341 ms (`act-0007`), 700 ms (`act-0008`), and 263.25 ms (`act-0011`); the maximum was 931 ms. This observes heterogeneous tail latency in one synthetic contention condition. It does **not** establish amplification relative to a lower-load baseline, nor identify a tensor: the workers are identical Node processes, each actor has only six observations, and most edges are self-loops.

### IX checks

Using the actual weighted live-smoke message graph, `ix_graph(pagerank)` ranked Gaia and Claude Code at `0.475` each and Codex at `0.05`. `ix_eigen` on the undirected weighted Laplacian returned eigenvalues `[7, 3, 0]`; the nonzero Fiedler mode separates Codex from the Gaia/Claude pair. These are descriptive checks on a three-node graph, not production conclusions.

An attempted `ix_mesh_correlate` call was governance-blocked with `ApprovalRequired`. It was not bypassed. The six-observation actor series would not justify a correlation mesh anyway.

## 6. Reproducible DuckDB analysis

Environment: DuckDB `1.5.3` at `C:\Users\spare\AppData\Local\Microsoft\WinGet\Links\duckdb.exe`.

Event counts:

```sql
SELECT type, count(*) AS n
FROM read_json_auto(
  'C:/tmp/gaia-wayfinder-plus/gaia-claude-codex-interop-prototype/.gaia-interop-prototype-data/live-smoke/events.jsonl',
  format='newline_delimited'
)
GROUP BY type ORDER BY type;
```

Acknowledgement latency:

```sql
WITH e AS (
  SELECT * FROM read_json_auto(
    'C:/tmp/gaia-wayfinder-plus/gaia-claude-codex-interop-prototype/.gaia-interop-prototype-data/live-smoke/events.jsonl',
    format='newline_delimited'
  )
),
sent AS (
  SELECT message.messageId AS message_id,
         message."from" AS src,
         message."to" AS dst,
         e."at" AS sent_at
  FROM e WHERE type='message.sent'
),
acked AS (
  SELECT messageId AS message_id,
         actorId AS ack_actor,
         e."at" AS ack_at
  FROM e WHERE type='message.acked'
)
SELECT sent.*, ack_actor,
       date_diff('millisecond', sent_at, ack_at) AS ack_latency_ms
FROM sent LEFT JOIN acked USING (message_id)
ORDER BY sent_at;
```

The production tracer should normalize raw data into explicit typed relations. This is illustrative DDL, not a claim that the current JSONL already contains every field:

```sql
CREATE TABLE raw_event (
  source_file VARCHAR NOT NULL,
  source_sha256 VARCHAR NOT NULL,
  event_index UBIGINT NOT NULL,
  event_at TIMESTAMPTZ NOT NULL,
  event_type VARCHAR NOT NULL,
  actor_ref VARCHAR,
  message_id VARCHAR,
  source_actor_ref VARCHAR,
  destination_actor_ref VARCHAR,
  correlation_id VARCHAR,
  payload JSON NOT NULL
);

CREATE TABLE agent_window (
  window_start TIMESTAMPTZ NOT NULL,
  window_seconds UINTEGER NOT NULL,
  actor_ref VARCHAR NOT NULL,
  runtime VARCHAR,
  runtime_version VARCHAR,
  arrivals UBIGINT NOT NULL,
  acknowledgements UBIGINT NOT NULL,
  completions UBIGINT NOT NULL,
  backlog_items BIGINT NOT NULL,
  arrival_rate_items_per_s DOUBLE,
  service_rate_items_per_s DOUBLE,
  ack_p50_ms DOUBLE,
  ack_p95_ms DOUBLE,
  context_tokens UBIGINT,
  disk_bytes UBIGINT,
  budget_amount DECIMAL(18,6),
  budget_currency VARCHAR,
  outcome VARCHAR
);

CREATE TABLE edge_window (
  window_start TIMESTAMPTZ NOT NULL,
  window_seconds UINTEGER NOT NULL,
  source_actor_ref VARCHAR NOT NULL,
  destination_actor_ref VARCHAR NOT NULL,
  contract_id VARCHAR,
  resource_type VARCHAR NOT NULL,
  resource_unit VARCHAR NOT NULL,
  quantity DOUBLE NOT NULL,
  capacity DOUBLE,
  retry_count UBIGINT NOT NULL,
  handoff_count UBIGINT NOT NULL,
  source_state JSON,
  destination_state JSON
);
```

Do not collapse these typed columns into one stress scalar.

## 7. Where IX, DuckDB, and IXQL fit today

- **DuckDB is executable now:** it is the correct read-only composition layer over JSONL and existing loop ledgers.
- **ix-duck is executable now:** source inspection confirms `ix_centrality` (degree, closeness, eigenvector, betweenness), `ix_autocorrelation`, `ix_wavelet_denoise`, and `ix_kalman_smooth`.
- **ix-pipeline is executable now:** it is the governed DAG layer for real analysis stages.
- **IXQL is not an executor today:** IX ADR-0001 and ADR-0004 state that the IXQL executor is spec-only. DuckDB/IX and IXQL are joined by JSON-on-disk until that executor ships.
- **The current Prime Radiant driver is visualization:** it already emits commands shaped like `SELECT nodes WHERE ... SET ...`. A future advisory view may color high backlog or disagreement nodes, but it must not become a binding gate.

The closest existing GA seam is the implemented `loop_iteration` / `loop_convergence` ledger, which already classifies runs as improving, plateaued, oscillating, or misfiring and supports deterministic self-halt logic. The mechanics tracer should extend that evidence model, not invent a second control plane.

An advisory JSON-on-disk seam can be exact and replayable:

```json
{
  "schema": "gaia.coordination-shape.v0.1",
  "source_sha256": "<fixed-event-log-digest>",
  "window": { "start": "2026-08-09T18:00:00Z", "seconds": 300 },
  "actor_ref": "act-0001",
  "observables": {
    "backlog_items": 3,
    "utilization": null,
    "ack_p95_ms": null,
    "balance_residual_items": 0,
    "laplacian_energy": 1.75
  },
  "authority": "advisory-only"
}
```

The corresponding IXQL shape is deliberately **non-executable/spec-only** today:

```sql
FROM 'coordination-shape.json'
SELECT actor_ref, observables.backlog_items, observables.laplacian_energy
WHERE authority = 'advisory-only'
EMIT 'prime-radiant/coordination-overlay.json';
```

Execution remains DuckDB plus ix-duck/ix-pipeline through the documented JSON seam. This syntax must not be presented as a shipped IXQL capability.

## 8. Falsifiable experiment

### Outcome

Predict within the next fixed window:

- lane stall or forced handoff;
- p95 acknowledgement-latency breach;
- fail-closed event or verifier misfire;
- unresolved backlog at the daily fixed point.

### Models

1. **Null A:** lane count and event count only.
2. **Null B:** queue depth/utilization only.
3. **Null C:** degree/betweenness plus queue depth.
4. **Discrete mechanics:** balance residuals, edge gradients, Laplacian energy, cycle/churn exposure, and tail latency.
5. **Tensor candidate:** only if the admissibility gates in section 4 pass.

### Evaluation

- Time-split held-out runs, separated by runtime version and workload family.
- Brier score/calibration plus precision-recall for rare failures; report uncertainty.
- Actor relabeling, unit conversion, and basis-rotation invariance tests.
- Timestamp permutation, edge rewiring with preserved degree, and workload-label permutation nulls.
- Controlled lane/load ramp with injected slow acknowledgements, lane loss, version drift, and lock contention.
- Record hysteresis and recovery, not just peak load.

Reject the mechanics extension if it does not improve held-out calibration/prediction over Null C, if its conclusion changes under harmless unit/basis changes, or if its thresholds do not transfer across versions.

## 9. Smallest safe tracer bullet

1. Add **no bus verb and no writer**.
2. Read a copied/fixed Gaia JSONL event log into DuckDB.
3. Materialize `agent_window`, `edge_window`, `balance_residual`, and `coordination_shape` views.
4. Compute backlog/capacity, p95 latency, graph gradients, Laplacian energy, and cycle exposure.
5. Run the null models and a deterministic accelerated load/fault fixture.
6. Emit one advisory JSON artifact consumed by the existing GA quality/Prime Radiant surfaces.
7. Keep automatic routing disabled until an exact-head review approves a held-out predictive result.

This tracer is additive, read-only, reversible, budget-bounded, and compatible with the current Gaia Interagent append-only log.

## 10. Sources

### Mechanics and geometric control

- MIT OCW, [Stress and Momentum Balance](https://ocw.mit.edu/courses/16-21-techniques-for-structural-analysis-and-design-spring-2005/466ce40eea21d45e1093edccbc9f276f_unit2_notes.pdf).
- MIT OCW, [Elements of Continuum Elasticity](https://ocw.mit.edu/courses/2-002-mechanics-and-materials-ii-spring-2004/f7b561fbe8762ff73da9236e6c0fff73_lec7.pdf).
- MIT OCW, [Constitutive Equations](https://ocw.mit.edu/courses/16-21-techniques-for-structural-analysis-and-design-spring-2005/05953c9b99e07e647cb96c1fe17c721f_unit5_notes.pdf).
- Lynch and Park, Northwestern, [Modern Robotics: Wrenches](https://modernrobotics.northwestern.edu/nu-gm-book-resource/3-4-wrenches/).
- Zhao, [Affine formation maneuver control of multi-agent systems](https://eprints.whiterose.ac.uk/id/eprint/127409/), IEEE Transactions on Automatic Control, DOI `10.1109/TAC.2018.2798805`.
- NASA, [NASA-STD-5001B: Structural Design and Test Factors of Safety for Spaceflight Hardware](https://standards.nasa.gov/sites/default/files/standards/NASA/B-w/CHANGE-3/3/2022-10-24-NASA-STD-5001B-w-Change-3-Approved.pdf).

### Discrete mathematics, networks, and control

- Shuman et al., [The Emerging Field of Signal Processing on Graphs](https://arxiv.org/abs/1211.0053).
- Desbrun, Kanso, and Tong, [Discrete Differential Forms for Computational Modeling](https://geometry.caltech.edu/pubs/GSD06.pdf).
- Schaub et al., [Random Walks on Simplicial Complexes and the Normalized Hodge 1-Laplacian](https://arxiv.org/abs/1807.05044), journal DOI `10.1137/18M1201019`.
- Tassiulas and Ephremides, [Stability Properties of Constrained Queueing Systems and Scheduling Policies for Maximum Throughput](https://drum.lib.umd.edu/items/571fda52-aefb-4497-9a2d-69d8c7c907b9), DOI `10.1109/9.182479`.
- MIT OCW, [Lyapunov Analysis](https://ocw.mit.edu/courses/6-243j-dynamics-of-nonlinear-systems-fall-2003/resources/lec11_6243_2003/).
- MIT OCW, [Controllability and Observability](https://ocw.mit.edu/courses/16-30-feedback-control-systems-fall-2010/c2c336c787d150d55873a98dfbd75e0f_MIT16_30F10_rec07.pdf).
- Cornell Optimization Open Textbook, [Network Flow Problem](https://optimization.cbe.cornell.edu/index.php?title=Network_flow_problem).
- Desel and Esparza, Cambridge University Press, [Analysis Techniques for Petri Nets](https://www.cambridge.org/core/books/abs/free-choice-petri-nets/analysis-techniques-for-petri-nets/0DD3A7FBC7377DB7A001FAD007B074BA).
- Madhumitha et al., [Stress in Directed Graphs: A Generalization of Graph Stress](https://ideas.repec.org/a/hin/jnljam/4678415.html), DOI `10.1155/jama/4678415`.

### Reliability and inference

- NIST/SEMATECH, [Engineering Statistics Handbook](https://www.nist.gov/programs-projects/nistsematech-engineering-statistics-handbook).
- NIST, [Fatigue Life Model](https://www.itl.nist.gov/div898/handbook/apr/section2/apr214.htm).
- Pearl, UCLA, [Causal Inference](https://bayes.cs.ucla.edu/csl_papers.html).

### Information and multi-agent coordination

- Shannon, IEEE Reach primary-source edition, [A Mathematical Theory of Communication](https://reach.ieee.org/primary-sources/a-mathematical-theory-of-communication/), DOI `10.1002/bltj.1948.27.issue-3`.
- Amari, [Differential-Geometrical Methods in Statistics](https://www.jstage.jst.go.jp/article/bjsiam/2/1/2_KJ00005767586/_article/-char/en), DOI `10.11540/bjsiam.2.1_37`.
- Smith, [The Contract Net Protocol: High-Level Communication and Control in a Distributed Problem Solver](https://www.reidgsmith.com/The_Contract_Net_Protocol_Dec-1980.pdf), DOI `10.1109/TC.1980.1675516`.
- Grosz and Kraus, [Collaborative Plans for Complex Group Action](https://dash.harvard.edu/server/api/core/bitstreams/7312037c-4ba2-6bd4-e053-0100007fdf3b/content), DOI `10.1016/0004-3702(95)00103-4`.

## 11. Verdict

- **Answer:** partial, discrete-first.
- **Confidence:** medium. The mathematics and local seams are strong, and the local traces establish queue/tail phenomena. There is not yet enough real heterogeneous long-horizon data to show predictive gain or identify a continuum tensor.
- **Independent validation:** a separate primary-source review reached the same discrete-first conclusion. Coordinator reproduced the DuckDB event counts/latencies/backlog and the IX PageRank/Laplacian checks.
- **One-way-door check:** this research does not justify a schema, public API, automatic-routing, or tensor-dimension change. Any such change needs a tracer result, an ADR, and fresh Standards + Spec reviews.

## 12. Next

Build the read-only DuckDB tracer bullet against fixed event-log copies. Revisit a stress tensor only after the system has enough real Claude/Codex/Junie/Auggie/AGY/Jules runs to estimate a stable geometry and demonstrate held-out gain. Revisit torseurs only if an actual geometric pose/control problem appears; otherwise keep them out of Gaia's vocabulary.
