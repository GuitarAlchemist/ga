# World Models, JEPA & Diffusion: GA Architecture Evaluation

**Status: AI-drafted evaluation for human review** — issue #483 (P1, max_autonomy: draft). Verdicts are recommendations, not decisions.

Related: GuitarAlchemist/tars#104, GuitarAlchemist/ix#212, GuitarAlchemist/ix#205.
Companion: [`beyond-llm-ga-product-scenarios.md`](./beyond-llm-ga-product-scenarios.md) (the product-scenario map this builds on).

## Scope & guardrails

This is the architecture half of the #483 evaluation. It gives each candidate model family an explicit **adopt / watch / experiment / defer / reject** verdict, scores it against the issue's ten criteria, then proposes a concrete tracer bullet and documents dependency/cost/latency/integration risk.

**Non-goals respected (from #483):**

- Do **not** replace GA's current architecture (5-layer model; AI code stays in `GA.Business.ML`).
- Do **not** add large model dependencies before evaluation.
- Do **not** require cloud services — local-first is mandatory.
- Do **not** build generic AI infrastructure inside GA.
- Do **not** prioritize flashy demos over measurable product value.

OPTIC-K (240-dim, `OPTIC-K-v1.8`) is **not** under review for replacement. It remains the musical-embedding spine; anything proposed here either feeds it or sits beside it.

## Scoring legend

Each family is scored on the issue's ten criteria. Scale: **L / M / H** (for cost-like criteria — `training_data_need`, `runtime_cost`, `dependency_risk`, `integration_complexity` — **L is good**; for value-like criteria — `product_value`, `local_feasibility`, `latency` (= "can hit interactive latency locally"), `quality`, `controllability`, `measurable_tracer_bullet` — **H is good**).

## Family verdicts

### A. World models / generative simulation — **DEFER**

**Verdict: DEFER.** Generative world models (learned simulators of an environment's dynamics) are a poor fit for GA's most concrete surfaces because the surfaces GA owns — the Godot guitar scene, the R3F Prime Radiant — are *deterministic and hand-authored*. A learned model that predicts dynamics we already specify in code adds cost without product value. The only place a world model earns its keep is a genuinely stochastic/physical sub-problem (string vibration, hand-instrument contact), which is a large research lift with unclear payoff. Defer until a stochastic sub-problem with measurable user value is identified; revisit if the music-game environment (scenario #4) matures into something needing learned dynamics.

| Criterion | Score | Note |
|---|---|---|
| product_value | L | Predicts hand-authored, deterministic scene dynamics. |
| local_feasibility | L | World models are heavy; hard to run interactively on-device. |
| latency | L | Real-time learned simulation is the hardest latency target here. |
| quality | M | Achievable in research, but quality bar for a guitar scene is exacting. |
| controllability | L | World models are notoriously hard to steer precisely. |
| training_data_need | H | Needs large environment-interaction datasets GA does not have. |
| runtime_cost | H | Heavy inference per frame. |
| dependency_risk | H | Large, fast-moving research deps. |
| integration_complexity | H | New runtime, new data pipeline, new everything. |
| measurable_tracer_bullet | L | No small end-to-end slice with a crisp metric. |

### B. JEPA-style predictive representations — **EXPERIMENT**

**Verdict: EXPERIMENT.** JEPA (Joint-Embedding Predictive Architecture) predicts *representations* rather than pixels, which aligns well with GA: it could predict the *next hand-pose embedding* given the current pose + target chord, feeding the ghost-hand UX (scenario #1) without the cost of pixel-space generation. The hand-pose service already produces a structured 21-keypoint representation — a natural prediction target. Small, self-supervised, local-friendly. Worth a scoped experiment specifically on pose prediction; do not adopt blindly across all scenarios.

| Criterion | Score | Note |
|---|---|---|
| product_value | H | Directly powers ghost-hand chord-transition guidance. |
| local_feasibility | H | Representation prediction is far lighter than pixel generation. |
| latency | H | Embedding-space prediction can be sub-frame. |
| quality | M | Needs validation against musically-valid fingerings. |
| controllability | M | Conditioned on target chord; steerable via the target. |
| training_data_need | M | Needs pose-transition sequences; GA can capture these locally. |
| runtime_cost | L | Small predictor, local. |
| dependency_risk | M | Approach is well-understood; no single heavy dep mandated. |
| integration_complexity | M | Plugs into existing hand-pose output; new predictor in `GA.Business.ML`. |
| measurable_tracer_bullet | H | See tracer bullet below. |

### C. Diffusion / flow models (audio, video, image, pose, gesture) — **WATCH**

**Verdict: WATCH.** Diffusion/flow is the strongest fit for `audio_texture_or_soundbank_generation` (scenario #5), where GA *already* ships MusicGen. The opportunity is replacing a heavy general model with a *smaller, faster, more controllable* local diffusion/flow model for short guitar one-shots. But the sound-bank surface works today, so this is not urgent — watch the small-local-audio-model space (the cost/quality/controllability frontier is moving fast) and re-evaluate when a clearly-better local option appears. Diffusion for *pose/gesture* is interesting but JEPA-style representation prediction (Family B) is the lighter, better-fit path for GA's pose work.

| Criterion | Score | Note |
|---|---|---|
| product_value | M | Faster/controllable local timbres; improves an existing surface, not net-new. |
| local_feasibility | M | Small audio diffusion is feasible locally; quality varies. |
| latency | M | Sub-second one-shots plausible; full phrases slower. |
| quality | M | Competitive with MusicGen only for short textures, not arrangements. |
| controllability | M | Flow/diffusion conditioning gives better timbral knobs than MusicGen prompts. |
| training_data_need | H | Training a guitar-specialist model needs curated audio. |
| runtime_cost | M | Lighter than MusicGen if specialist + small. |
| dependency_risk | M | Diffusion tooling is large but mature; pin a small model. |
| integration_complexity | M | Slots into existing `sound-bank-service` job queue. |
| measurable_tracer_bullet | M | Possible (A/B latency vs MusicGen) but not the recommended first slice. |

### D. Small specialist local models — **ADOPT (as the default posture)**

**Verdict: ADOPT.** Not a single model but a *stance*: prefer small, task-specific, locally-runnable models over large general ones, everywhere a beyond-LLM capability is added. This directly satisfies #483's non-goals (local-first, no large deps, no cloud) and is the connective tissue under Families B and C — the pose predictor and the audio generator should both be small specialists. Adopt as the governing principle for any beyond-LLM work in GA; it is low-risk because it constrains *toward* GA's existing local-first values rather than introducing new infrastructure.

| Criterion | Score | Note |
|---|---|---|
| product_value | H | Enables the high-value scenarios within local constraints. |
| local_feasibility | H | By definition runs locally. |
| latency | H | Small models hit interactive latency. |
| quality | M | Task-specialized quality can match big models on narrow tasks. |
| controllability | H | Narrow scope = predictable behavior. |
| training_data_need | M | Per-task; often satisfiable with GA-captured data. |
| runtime_cost | L | Small footprint. |
| dependency_risk | L | Avoids large/fast-moving deps; aligns with non-goals. |
| integration_complexity | L-M | Fits the existing FastAPI sidecar + `GA.Business.ML` pattern. |
| measurable_tracer_bullet | H | Each specialist model is its own measurable slice. |

### E. Sequence models for music/audio/gesture timelines — **EXPERIMENT**

**Verdict: EXPERIMENT.** Sequence models over GA's *own* captured timelines are the best fit for `practice_session_replay_and_prediction` (scenario #7) and underpin the adaptive side of `music_game_environment_simulation` (scenario #4). Small training-data need (the user generates their own), local-friendly, and directly product-valuable: predict the next likely error / recommend the next drill from a session timeline of poses + notes + timing. Experiment scoped to session-replay prediction; it reuses the hand-pose + theory stack and needs no new heavy dependency.

| Criterion | Score | Note |
|---|---|---|
| product_value | H | Instruments practice; anticipates errors; recommends next drill. |
| local_feasibility | H | Sequence models over compact event timelines are light. |
| latency | H | Prediction is off the hot render path (between attempts). |
| quality | M | Needs enough captured sessions to be useful per-user. |
| controllability | M | Recommendations constrained by theory rules. |
| training_data_need | L-M | User-generated; cold-start mitigated by rule-based fallback. |
| runtime_cost | L | Compact model over event sequences. |
| dependency_risk | L | Standard sequence modeling; no exotic dep. |
| integration_complexity | M | Needs a session-timeline capture format GA doesn't have yet. |
| measurable_tracer_bullet | M | Capture format must exist first; slice slightly larger than B's. |

## Verdict summary

| Family | Verdict | One-line rationale |
|---|---|---|
| World models / generative simulation | **DEFER** | GA's 3D surfaces are hand-authored & deterministic; learned dynamics add cost without product value until a stochastic sub-problem appears. |
| JEPA-style predictive representations | **EXPERIMENT** | Predict the next pose *representation* (not pixels) for ghost-hand chord-transition guidance — light, local, high product fit. |
| Diffusion / flow models | **WATCH** | Best fit for local soundbank generation, but that surface already works; watch the small-local-audio frontier and re-evaluate. |
| Small specialist local models | **ADOPT** | Governing posture — prefer small task-specific local models everywhere; directly satisfies the local-first / no-large-dep non-goals. |
| Sequence models for timelines | **EXPERIMENT** | Predict next error / next drill from a practice-session timeline of GA's own data; small data need, directly product-valuable. |

## Recommended tracer bullet (smallest end-to-end slice)

**Ghost-hand next-chord pose hint (JEPA-style, Family B + Family D).**

The smallest end-to-end slice that touches every layer and proves the highest-value scenario (`hand_pose_prediction_for_chords`):

1. **Capture:** from the existing `Apps/hand-pose-service/`, log (current 21-keypoint pose) → (pose at the *next* chord) pairs for a handful of common transitions (e.g. C→F, G→D, Am→E). No new capture infrastructure — reuse the running service.
2. **Model:** train a *small specialist* predictor (Family D) that, given the current pose embedding + a target-chord one-hot, predicts the target keypoint configuration. Lives in `GA.Business.ML` (layer 4). No large dependency; no cloud.
3. **Render:** overlay the predicted target pose as a translucent "ghost hand" on the existing fretboard / R3F surface (layer 5 orchestration → React/R3F).
4. **Validate:** the predicted target fingering must be a musically-valid voicing per `GA.Business.Core` rules (gate against existing voicing validation; reject unreachable predictions).

**Success metric (measurable):** on a held-out set of the captured transitions, **≥ 80% of predicted target poses pass the `GA.Business.Core` fingering-validity gate** *and* the per-prediction overlay latency stays within the existing per-frame pose-render budget (no dropped frames vs. the reactive-only baseline). Baseline to beat: today's hand-pose service offers **zero** next-chord guidance, so any validated ghost-hand hint is a strict product improvement; the 80%-validity bar is what separates "useful" from "noise."

**Why this slice:** it is vertical (capture → model → render → validate), reuses two surfaces GA already runs (hand-pose service + fretboard/R3F), needs no large dependency or cloud, exercises both the highest-verdict families (EXPERIMENT JEPA + ADOPT small-specialist), and has a crisp pass/fail metric. A second-choice slice — *next-drill recommendation* (Family E) — is deferred because it first requires building a session-timeline capture format GA does not yet have.

## Dependency, cost, latency & integration risk

**Dependencies.**
- The tracer bullet adds **no new large dependency**: it reuses MediaPipe (already in `hand-pose-service`) for capture and trains a small predictor with whatever lightweight ML the team already vendors in `GA.Business.ML`. This is deliberate — #483 forbids large deps pre-evaluation.
- Family C (diffusion audio) *would* pull a heavier stack if pursued; that is a key reason it is WATCH not ADOPT. Family A (world models) carries the highest dependency risk and is DEFER.
- No family proposed for action requires a cloud service. Local-first is preserved throughout.

**Cost.**
- Budget for #483 is **free-local, max $0, ≤30 runner-min** (per the issue's `budget` block). The tracer bullet honors this: capture and small-model training run locally, no metered API calls.
- Ongoing runtime cost for the ghost-hand predictor is negligible (small model, on-device). The expensive families (A world models, C diffusion audio) are precisely the ones *not* recommended for immediate action.

**Latency.**
- The hard constraint is the **per-frame pose-render budget** the hand-pose overlay already lives within. The predictor must add its inference inside that budget; a small specialist model is chosen specifically to meet it. Prediction that can't be sub-frame is a fail condition, not a tuning issue.
- Family E (sequence/practice prediction) sidesteps latency risk by running *between* attempts, off the hot render path.

**Integration complexity & one-way-door check.**
- The tracer bullet is **additive and reversible**: a new predictor in `GA.Business.ML` + a new overlay in the R3F/fretboard layer. It does **not** touch OPTIC-K, the embedding schema, or the 5-layer boundaries — no one-way door, no coordinated re-index, no schema freeze.
- It respects the layer rule: AI/model code in layer 4 (`GA.Business.ML`), orchestration + render in layer 5. The FastAPI sidecar pattern (as used by `hand-pose-service` / `sound-bank-service`) is the established integration shape if the predictor needs to run as a service rather than in-process.
- Biggest *integration* risk is data: the capture step assumes the hand-pose service emits stable, repeatable keypoint coordinates across sessions/lighting. Mitigate by capturing in controlled conditions for the tracer bullet and treating per-user calibration as a follow-up, not part of the first slice.

## Open questions for human review

1. Is the ghost-hand tracer bullet the right *first* slice, or should practice-session prediction (Family E) lead despite needing a capture format first?
2. For the small-specialist posture (Family D, ADOPT): which lightweight training/inference runtime does GA standardize on inside `GA.Business.ML`?
3. Does the WATCH on diffusion audio (Family C) warrant a periodic re-evaluation trigger (e.g. when a sub-X-MB local guitar audio model appears), or revisit only on user demand?
