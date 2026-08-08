---
id: 2026-08-02-langfuse-cloud-factory-evaluation
date: 2026-08-02
status: active
domain: code
question: Should GA adopt Langfuse for the Cloud Factory and chatbot, borrow selected mechanisms, or keep the current OpenTelemetry and evidence stack unchanged?
hypotheses:
  - claim: GA should pilot Langfuse as an optional OpenTelemetry observability and evaluation backend, while retaining Git, Agent Blackbox, Gaia, and checked-in prompts as the authoritative control and evidence planes.
    refuted_if: Langfuse requires a proprietary SDK in the .NET request path, cannot ingest GA's current OpenTelemetry traces, or must own prompts, approvals, leases, or completion evidence to provide useful observability and evaluation.
tools: [official-langfuse-source, official-langfuse-docs, local-code-inspection]
artifacts: null
validators: [primary-source-and-local-contract-analysis]
confidence: medium-high
supersedes: null
superseded_by: null
---

# Langfuse for GA Cloud Factory: adopt the lens, not the control plane

**Status caveat:** this is a primary-source and local-code conclusion. A live
OTLP export and an independent review have not yet been run, so the study stays
`active` until the tracer-bullet pilot produces evidence.

## TL;DR

Pilot Langfuse, but only as an **optional read/analysis plane** behind GA's
existing OpenTelemetry boundary. Do not replace the Cloud Factory's durable
packet/event store, Gaia's fenced leases, Agent Blackbox's SHA-bound evidence,
GitHub review state, or Git-versioned prompts with Langfuse state.

The lowest-risk pilot is an OpenTelemetry Collector fan-out: GA continues to
emit vendor-neutral OTLP; the collector forwards only selected `GA.Chatbot` and
GenAI spans to a disposable Langfuse project while preserving the current
Aspire/OTLP destination. This can add trace/session exploration, token/cost
analysis, datasets, scores, and human/automated evaluation without putting a
Langfuse SDK in the .NET application or making Langfuse a runtime dependency.

Do **not** adopt Langfuse Prompt Management yet. GA prompts, skills, BAML
schemas, governance rules, and review expectations are executable product
behavior and must remain commit-reviewed and SHA-bound. A later read-only prompt
mirror or explicitly approved deployment workflow can be evaluated separately.

## Question and pain

GA already produces several kinds of useful but fragmented evidence:

- W3C/OpenTelemetry traces for the chatbot pipeline;
- an AG-UI-facing `AgenticTrace` response with routing and fallback details;
- routing, retrieval, and shadow-model JSONL telemetry;
- on-disk quality snapshots and evaluation harnesses;
- Agent Blackbox postflight evidence for autonomous engineering work.

The pain is not an absence of telemetry. It is the lack of one convenient lens
for navigating an LLM interaction across model calls, routing, retrieval, tools,
quality scores, releases, and sessions. The decision is whether Langfuse can
fill that visualization/analysis gap without becoming a second source of truth.

## Method

Re-run the local inspection from the GA repository root:

```powershell
rg -n 'gen_ai\.|langfuse\.' AllProjects.ServiceDefaults Apps/GaChatbot.Api Common/GA.Business.ML --glob '!**/bin/**' --glob '!**/obj/**'
Get-Content AllProjects.ServiceDefaults/Extensions.cs
Get-Content Common/GA.Business.ML/Agents/ChatbotActivitySource.cs
Get-Content Apps/GaChatbot.Api/Services/AgenticTraceBuilder.cs
```

Then compare those facts with the first-party Langfuse sources linked below:
the repository README/license, OTLP ingestion and attribute mapping, data model,
prompt management, data retention, and security/self-hosting documentation.

## Evidence

### GA is already close to the right vendor-neutral seam

- `AllProjects.ServiceDefaults/Extensions.cs` registers `GA.Chatbot` with
  OpenTelemetry and enables `UseOtlpExporter()` when
  `OTEL_EXPORTER_OTLP_ENDPOINT` is configured.
- `ChatbotActivitySource` already defines stable operation names and bounded,
  privacy-conscious tags for routing, agents, tools, failures, and model IDs.
- Chat application services already emit some `gen_ai.*` attributes, and
  `AgenticTraceBuilder` identifies its wire protocol as
  `w3c-trace-context+otel-genai+ag-ui`.
- Langfuse accepts native OTLP/HTTP traces at `/api/public/otel`; for languages
  without a Langfuse SDK, its own guidance is to use the language's native
  OpenTelemetry API ([OTLP integration](https://langfuse.com/integrations/native/opentelemetry)).
- Langfuse is built around traces, observations, sessions, scores, token/cost
  metrics, datasets, and evaluations, and can coexist with another telemetry
  backend through exporter fan-out ([data model](https://langfuse.com/docs/observability/data-model),
  [repository](https://github.com/langfuse/langfuse)).

This supports a no-SDK .NET pilot. Because Langfuse currently accepts OTLP over
HTTP rather than gRPC, a Collector is preferable to pointing GA directly at it:
the collector can receive GA's existing protocol, redact/filter/fan out, and
export `otlphttp` to Langfuse with the v4 ingestion header.

### The useful Langfuse mechanisms

| Langfuse mechanism | GA use | Boundary |
|---|---|---|
| Trace/session exploration | Correlate routing, retrieval, tool, fallback, model, latency, token, and release behavior across a user session. | Observational only; it cannot determine packet or request success. |
| Scores and evaluations | Attach existing deterministic music-theory/routing/retrieval scores and later human labels to the exact trace/release. | GA evaluators remain canonical; Langfuse stores/query-displays a copy of the score. |
| Datasets and experiments | Curate held-out routing/RAG/chat cases and compare releases before rollout. | Dataset export must be versioned or reproducibly materialized; Langfuse UI state alone is not release evidence. |
| Token/cost/latency metrics | Add budget visibility for model-backed chatbot paths and, later, Cloud Factory workers. | Budget debits and kill switches remain in Gaia/Agent Blackbox; telemetry can lag or drop. |
| Release/environment dimensions | Compare local/staging/production behavior by commit/release. | Values must be derived from immutable build/Git metadata, not typed manually in the UI. |
| Human annotation queues | Let maintainers label bad responses and convert them into regression cases. | A label is input to triage, not an approval, merge verdict, or trusted fact. |

### The mechanisms not to adopt now

1. **Prompt Management as the live authority.** It intentionally decouples
   prompt changes from code deployment ([docs](https://langfuse.com/docs/prompt-management/overview)).
   That is valuable for some product teams but conflicts with GA's reviewed,
   reproducible prompts/skills/BAML schemas and Agent Blackbox's SHA-bound
   evidence. Keep prompts in Git; consider a read-only mirror later.
2. **Langfuse traces as completion evidence.** OpenTelemetry is best-effort,
   sampled, buffered, and may be delayed. It cannot certify lease ownership,
   repository HEAD, dirty digest, verifier exit, independent review, or
   authority. Agent Blackbox evidence remains canonical.
3. **Langfuse as the Cloud Factory queue or state machine.** Its data model is
   an observability/evaluation model, not a transactional fenced-lease or
   authorization system.
4. **Raw prompt/response capture by default.** GA already avoids exception
   messages and truncates queries in activity tags. The pilot should export
   metadata first; any content capture requires explicit redaction, consent,
   retention, and deletion policy. Langfuse retention is not automatic on all
   plans and self-hosted retention management is an Enterprise feature
   ([retention](https://langfuse.com/docs/administration/data-retention)).
5. **A production self-host immediately.** The OSS core is MIT outside its
   enterprise directories ([license](https://github.com/langfuse/langfuse/blob/main/LICENSE)),
   but production self-hosting adds ClickHouse/Postgres/Redis/object-storage,
   upgrades, backups, privacy, and operational ownership. Prove value with a
   disposable, bounded pilot first.

## Tracer-bullet pilot

Timebox the pilot to one day and one chatbot surface:

1. Start a disposable local or isolated Langfuse project; do not send real user
   content.
2. Put an OpenTelemetry Collector between GA and telemetry backends. Receive
   existing OTLP, retain the current destination, and add an `otlphttp` Langfuse
   exporter with `x-langfuse-ingestion-version: 4`.
3. Filter to the `GA.Chatbot` instrumentation scope / selected `gen_ai.*` spans.
   Keep input/output absent; allow only model ID, operation, route, confidence,
   tool/failure taxonomy, duration, token counts, release SHA, and environment.
4. Add the minimum standard/Langfuse attributes needed for useful filtering:
   trace name, pseudonymous session ID, release SHA, environment, observation
   type/model/usage. Never put credentials, raw user identity, or personal data
   in OpenTelemetry baggage.
5. Replay a fixed non-sensitive corpus and attach existing deterministic
   routing/RAG correctness scores. Compare whether Langfuse shortens diagnosis
   versus Aspire traces plus JSONL snapshots.
6. Export or reproduce the pilot's dataset/score definitions in Git. Destroy
   the disposable project after the retention/deletion check.

### Success criteria

- At least 95% of replayed requests join the expected trace/session and release.
- Existing production behavior and latency are unchanged within measurement
  noise; loss of Langfuse is invisible to callers.
- No raw prompt, response, secret, or direct user identifier leaves the test
  boundary.
- One known routing/fallback failure can be diagnosed materially faster than
  with the current fragmented views.
- Every displayed quality score can be traced back to a deterministic GA
  evaluator version and input corpus.
- Disabling the Langfuse exporter removes the integration completely; no app
  code, queue, lease, approval, evidence, or prompt path depends on it.

## Verdict

**Adopt conditionally as an optional observability/evaluation backend; do not
adopt it as infrastructure authority.** The OpenTelemetry seam makes the pilot
cheap and reversible, and Langfuse's trace/session/evaluation UX directly fits
GA's current visibility gap. The Cloud Factory itself should emit the same
metadata-only lifecycle spans later, but its durable events, budgets, leases,
evidence, and approval decisions stay in Gaia and Agent Blackbox.

Recommended timing: run the one-day pilot after PRs #632 and #633 are reviewed
and after the canonical Agent Blackbox contract stack is accepted. Until then,
borrow the design vocabulary—trace/session/release/score/dataset—and keep the
runtime dependency graph unchanged.
