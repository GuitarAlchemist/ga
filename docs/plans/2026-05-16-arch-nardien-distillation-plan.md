---
title: "arch: distill a small student model for GA chatbot (Nardien-style)"
type: arch
status: draft-pending-signoff
version: 0.1
date: 2026-05-16
origin: BACKLOG task #195 — "Distill a small student model for GA — Nardien agent-distillation"
related_plans:
  - docs/plans/2026-05-14-arch-cherny-adoption-and-tribunal-ci-plan-v2.md
  - docs/plans/2026-05-07-chatbot-roadmap.md
related_contracts: []
reversibility: Phase 0 = two-way (plan + dataset shape); Phase 1 = two-way (teacher-rollout collection adds a dataset, removable); Phase 2 = two-way at the student-checkpoint level (LoRA adapters are detachable); Phase 3 = one-way once a student model is wired into AI:CascadeProvider chain in production
revisit_trigger: end of Phase 2 — if student-vs-teacher pass_pct delta exceeds 5% on prompts.yaml OR p50 latency >2x current Ollama, abort + revisit teacher/student selection
---

# Nardien-style distillation for GA chatbot

## Problem frame

GA's chatbot answers 47/50 prompts.yaml prompts correctly **on local Ollama** (`llama3.2:3b-instruct`). The remaining 3 are LLM-bound wording-variance failures — invariants like `contains_any: ["minor third", "third"]` that fail because the model phrased the answer slightly differently. After PR #225 (Mistral cascade), Mistral Medium picks these up reliably (~1s latency, ~$$$ per 1M tokens).

The tension:

- **Cloud (Mistral / Claude / GPT)** — high quality, ~1s latency, $$ per call, requires internet, third-party data egress.
- **Local Ollama (`llama3.2:3b-instruct`)** — free, no egress, but 3 of 50 prompts fail invariants. Quality is the bottleneck.

A **distilled student** trained on cloud-teacher rollouts could close the quality gap for the GA-specific corpus while keeping the inference local. The Nardien paper's pattern is: collect (prompt → teacher reasoning trace → teacher answer) tuples on the actual task distribution; fine-tune a small open base model on those tuples; measure if the student matches the teacher on held-out task instances.

## Decisions to make (drafted; signoff required)

| # | Decision | Default | Alternatives | Why |
|---|---|---|---|---|
| **D-teacher** | Which teacher generates training data? | **Mistral Medium** (`mistral-medium-latest`) | Claude Opus 4.7; GPT-4.1 mini; ensemble | Cheapest per-token of the cloud options ($2/M in, $6/M out); already wired via cascade (PR #225); proven against GA corpus during cascade-validation probes. Claude Opus is overkill; GPT-4.1 adds a third vendor. Ensemble is over-engineering for v0.1. |
| **D-student** | Which base model gets fine-tuned? | **Qwen2.5-3B-Instruct** | Phi-3.5-mini; Llama3.2-3B-Instruct (current Ollama default) | Qwen2.5 wins MMLU and theory-of-mind benchmarks at the 3B size class; permissive license; runs on Ollama via `ollama pull qwen2.5:3b`. Phi-3.5 is close but Microsoft-licensed; Llama3.2 is the incumbent — distilling onto the same family risks "no observable improvement" from too-similar a base. |
| **D-skills** | Which skills get distilled? | **Only LLM-bound skills** — `freeform_qa`, `theorycomparison` (LLM tier), `composer` agent | All chatbot LLM calls; per-tool sub-distillation | Deterministic music-theory skills (Mode, Scale, Chord, etc.) already run in C# without an LLM. Distilling those would be pure cost with no quality lift. The student's job is the freeform / explanation paths. |
| **D-method** | Distillation technique | **LoRA fine-tune** (rank 16-32) over the teacher rollouts | Full fine-tune; SFT-then-DPO; knowledge distillation via logits | LoRA is reversible (adapter is a separate file; detach by deleting). Full fine-tune is one-way once we commit. DPO needs preference pairs we don't have. Logit-distillation requires teacher logit access — cloud APIs don't expose. |
| **D-dataset-size** | Rollout corpus size | **2× the prompts.yaml size = ~100 prompts × 4 variations = 400 training examples** | 50 ex (too small); 1k+ ex (cloud-cost spike) | Nardien-style targets task distribution density, not breadth. 4 variations per prompt (rephrase / longer / shorter / adversarial) gives the student enough margin without burning the cloud budget. |
| **D-success-bar** | When is the student "done"? | **pass_pct ≥ 0.96 on prompts.yaml-v1 + p50 latency ≤ 2× current Ollama** | match-teacher-exactly; +5% over Ollama baseline | The pass-rate bar is the contract we ship on. Latency is the cost of switching to a 3B-on-Ollama from a 3B-on-Ollama (no net change at the base). Stricter bars are scope creep. |
| **D-deployment** | How does student get into the chatbot? | **New Ollama model tag `ga-student-v1`; cascade prepended** | Replace `llama3.2:3b` outright; serve via vLLM/llamafile | Ollama is the existing local serving layer. Adding a model is one `ollama create` command. Replacing the default is reversible (`ollama pull` the old one back). Switching servers is scope creep. |

All seven decisions are reversible per the table; the only one-way door is **D-deployment** at Phase 3 (production cutover) — but the previous model stays available locally for instant rollback.

## Phases

### Phase 0 — Plan + dataset shape (this doc + 1 PR)

- Sign off decisions D-teacher through D-deployment.
- Define `state/distillation/dataset.schema.json` — JSON-line records `{prompt, teacher_response, teacher_trace, source_invariants, generated_at, teacher_model, temperature}`.
- Add `state/distillation/` to `.gitignore` for runtime artifacts; check in the schema.
- Reversibility: pure docs/JSON. No model weights yet.

### Phase 1 — Teacher rollout collection

- New script: `Scripts/distill-collect-rollouts.ps1`. Reads `Tests/Apps/GaChatbot.Api.Tests/Corpus/prompts.yaml`, expands each entry into N=4 variations (a small `Scripts/distill-augment.ps1` does the expansion via a deterministic template + Mistral wording-variation), POSTs each to Mistral Medium via the existing `MistralChatClient`, captures the response + agentic trace, writes a JSONL record per (prompt, variation).
- Quality gate: every (prompt, variation) where the teacher's answer fails the prompts.yaml invariant gets flagged. Flag rate > 10% means teacher is the wrong choice — revisit D-teacher.
- Cost budget: 400 × ~500 tokens × $2/M in + 400 × ~1500 tokens × $6/M out ≈ **$4 in cloud bills**. Operator caps the run with a `MAX_COST_USD=10` env var.
- Output: `state/distillation/rollouts-<date>.jsonl` (gitignored — committable summary in `state/distillation/summaries/<date>.json`).
- Reversibility: data, no model. Detachable.

### Phase 2 — Student training

- Pick the LoRA framework. Default: **Unsloth** (Apache-2.0, fast, integrates with HF transformers + Ollama export). Alternatives: HF PEFT directly (more control, slower wall-clock).
- Train on a single consumer GPU (operator's box has RTX 4090 — 24GB VRAM, ample for 3B + rank-32 LoRA).
- Eval split: hold out 20% of `state/distillation/rollouts-<date>.jsonl`, plus the 50 production prompts.yaml entries verbatim. Student must pass the held-out invariants AND the production prompts.yaml.
- Output: LoRA adapter at `state/distillation/checkpoints/ga-student-v1.gguf` (~50MB, gitignored). Quantize to Q4_K_M before exporting to Ollama.
- Reversibility: adapter file is detachable. Base model unchanged.
- One-way checkpoint: once an adapter is "blessed", reproducing it requires the exact rollout JSONL + Unsloth version + training hyperparams. Capture all three in `state/distillation/summaries/<date>.json`.

### Phase 3 — Cascade integration + production cutover

- Add `Common/GA.Business.ML/Providers/Ollama/OllamaStudentChatClient.cs` (thin wrapper using the existing OllamaChatClient with `model: "ga-student-v1"`).
- Wire into the cascade: `Ollama (llama3.2) → Ollama (ga-student-v1) → Mistral Medium`. Three tiers, cheapest first. The student gets first-line traffic after the deterministic skill chain misses.
- Re-record golden traces from a chatbot environment running the student to confirm canonical-signature stability.
- Gate cutover behind `AI:Student:Enabled=true` config; off by default. Operator flips it after Phase 2 eval is clean.
- Roll back: `AI:Student:Enabled=false` + Ollama still has the previous model. ~30s recovery window.
- Reversibility: feature flag + parallel model availability. Two-way at the config level; one-way at the operator-ergonomics level once users adapt to the new latency profile.

### Phase 4 — Distillation v2 (out of scope for this plan)

If Phase 3 ships clean and stays stable for 4 weeks:

- Re-train on a larger rollout corpus including user-thumbs-down events (when feedback infra exists).
- Try replacing Mistral Medium teacher with an ensemble of Mistral + Claude (vote-of-two for low-confidence answers).
- Consider self-distillation: student-vN's outputs become teacher input for vN+1 on prompts the production chatbot got wrong.

Phase 4 is named to mark the trajectory; not a commitment.

## One-way doors

| # | Door | When it closes | Revisit trigger |
|---|---|---|---|
| **OWD-1** | D-deployment: `ga-student-v1` becomes default in cascade | Phase 3 cutover | If `pass_pct` drops > 0.02 below current baseline for 2 consecutive days, automatic rollback via the AI:Student:Enabled flag |
| **OWD-2** | Training-pipeline reproducibility | Phase 2 — once the operator deletes the rollout JSONL or doesn't capture Unsloth version | Reproducing the same student requires data + tooling provenance; without it, this becomes a one-way commitment |
| **OWD-3** | Teacher data egress | Phase 1 — every prompt sent to Mistral becomes a third-party log entry | Once shipped, can't be unshipped. Document in privacy-stance doc before Phase 1. |

## Success criteria

1. `pass_pct` on prompts.yaml with student-only routing (no Mistral cascade) ≥ 0.96 (vs current 0.94 baseline on llama3.2).
2. p50 latency on Ollama-served student ≤ 2× p50 on Ollama-served llama3.2 (basically: same hardware, same latency band).
3. Cascade flag flip + flip-back exercised in ≤ 60 seconds end-to-end (operator hands-on).
4. No `orchestration.fallback` step in golden traces for the 3 currently-LLM-bound failure prompts (recorded post-Phase-3 cutover).
5. Cloud cost during Phase 1 collection ≤ $10.

## Out of scope

- Distilling the deterministic skills (already C#, no LLM).
- Replacing Ollama as the serving runtime.
- Multi-language distillation (English only for v0.1).
- DPO / RLHF / preference-data collection.
- Cross-repo: the Nardien pattern applied to ix or Demerzel is its own plan.

## Open questions

1. Should the rollout dataset include the agentic trace (skill routing, MCP tool calls) or just the final answer? Including the trace teaches structure; excluding keeps it simpler. **Default: include trace, capped to top-3 steps to limit context.**
2. Should distillation also produce a tiny **router** model (replacing the embedding-similarity semantic router)? Maybe. Out of scope for v0.1; revisit in Phase 4.
3. Where does the GPU live for training? Operator's box (RTX 4090) for v0.1; cloud (Lambda Labs / Runpod) if the rollout corpus grows past 5k examples.

## Related

- `feedback_multi_llm_review_pays_off` — distillation PRs touch new agents + cascade routing; must go through Demerzel tribunal + /octo:review.
- `project_chatbot_skills_migration_2026_05_03` — context for why 11 skills are deterministic and 4 are LLM-bound.
- `reference_mistral_agent` — the cross-model theory validator. Could double as the teacher rollout source.
- `Common/GA.Business.ML/Providers/Mistral/MistralChatClient.cs` — the wire we'll use for teacher collection.
- Nardien paper: <https://arxiv.org/abs/2410.21533> (Liu et al., 2024) — agent-distillation via teacher-trace rollouts.
