---
id: 2026-09-25-ga-performance-formal-testing-leverage
date: 2026-09-25
status: active
domain: code
question: Which GA chatbot-facing C# workload and invariant should receive the first measured performance and formal-testing slice?
hypotheses:
  - claim: The typed voicing-retrieval path is the first useful C# workload to measure, and a valid-diagram metamorphic property will add coverage beyond the current examples.
    refuted_if: A fixed-corpus trace shows that another C# stage dominates user-visible latency, or the new property finds no distinct failure class after its mutation check.
tools: [git, gh, PowerShell, FsCheck, BenchmarkDotNet]
artifacts: []
validators: []
confidence: low
supersedes: null
superseded_by: null
---

# Measure the chatbot voicing path before selecting formal machinery

**Date:** 2026-09-25  
**Type:** bounded planning research, not an optimization result  
**Question:** Which GA chatbot-facing C# workload and invariant should receive the first measured performance and formal-testing slice?

## TL;DR

The `VoicingAgent` retrieval path is the best *candidate* because it is user-facing, has an explicit search timer, and can answer structured questions without an LLM. It has **no representative measured chatbot latency baseline in the inspected local telemetry**: all 28 retained voicing records came from other sources. Start by recording a fixed-corpus baseline and result-quality guardrail. Use a generated six-string diagram property next; reserve a state-sequence model for gate/history concurrency and TLA+ for a concrete cross-process protocol that cannot be covered with the simpler model.

## 1. Question

The GA chatbot team needs to know where C# work can improve user-visible voicing answers without changing musical meaning. The open [cross-repo research ticket](https://github.com/GuitarAlchemist/.github/issues/81) asks for a baseline, a minimized counterexample strategy, and a criterion for rejecting unnecessary formal machinery.

This assessment is from checkout `dc2e74cbd992a298f67efb928b5b3754615e1924` plus a **dirty working tree** on 2026-09-25. In particular, `Voicing.cs`, `PitchClassSet.cs`, and other domain files had uncommitted edits. This note does not attribute those edits to a commit or treat them as a released baseline.

The research branch publishes only this note and its companion chatbot study. Source links identify tracked files at the recorded commit; dirty-worktree observations and local telemetry aggregates still require independent reproduction before a delivery decision.

## 2. Hypothesis and prior art

- **Candidate workload:** a typed request routed to [`VoicingAgent.ProcessAsync`](../../Common/GA.Business.ML/Agents/VoicingAgent.cs), whose retrieval span is timed around `EnhancedVoicingSearchService.SearchAsync` and optionally retried without a chord-name filter. The agent emits `LatencyMs` and result count through [`VoicingTelemetryLog`](../../Common/GA.Business.ML/Search/VoicingTelemetryLog.cs). That timer covers retrieval, **not** the whole HTTP response, routing, extraction, or LLM composition.
- **Falsifier:** an instrumented fixed corpus shows routing/extraction/provider/network time or another C# stage dominates the user-visible p95, while retrieval is a minor share; then measure that dominant stage instead.
- **Prior art in GA:** [`DomainCoreBenchmarks`](../../Benchmarks/GA.Domain.Core.Benchmarks/DomainCoreBenchmarks.cs) uses BenchmarkDotNet and its memory diagnoser. [`VoicingAnalyzerPerformanceTests`](../../Tests/Common/GA.Business.Core.Tests/Fretboard/Voicings/VoicingAnalyzerPerformanceTests.cs) has 6 ms/voicing steady-state and 25 ms single-call thresholds, but those thresholds are not measured chatbot latency. The older [`ADVANCED_OPTIMIZATION_OPPORTUNITIES.md`](../Performance/ADVANCED_OPTIMIZATION_OPPORTUNITIES.md) explicitly marks itself partially stale; its 30–50% opportunity estimate is not a current baseline.
- **Testing prior art:** [`FretDiagramTests`](../../Tests/Common/GA.Business.ML.Tests/FretDiagramTests.cs) and [`VoicingComfortFilterTests`](../../Tests/Common/GA.Business.ML.Tests/Unit/VoicingComfortFilterTests.cs) already pin compact and dash-separated diagrams, including a former compact-format comfort-filter no-op. [`ChordRecognitionRoundTripTests`](../../Tests/Common/GA.Business.Core.Tests/Voicings/ChordRecognitionRoundTripTests.cs) distinguishes exact names from equivalent pitch-class sets for ambiguous chords. [`EmbeddingInvariantsTests`](../../Tests/Common/GA.Business.Core.Tests/Voicings/EmbeddingInvariantsTests.cs) limits canonical-name invariance to the **same pitch-class set**; transposition is not promised.
- **External primary sources:** [FsCheck](https://github.com/fscheck/fscheck) generates properties and shrinks failures; its [runner documentation](https://fscheck.github.io/FsCheck/RunningTests.html) describes replay seeds and `FsCheck.NUnit`. [BenchmarkDotNet](https://github.com/dotnet/BenchmarkDotNet/blob/master/docs/articles/configs/diagnosers.md) can report allocations. [.NET Channels](https://learn.microsoft.com/en-us/dotnet/core/extensions/channels) distinguishes waiting for room from immediate `TryWrite` rejection. The [TLC paper](https://lamport.org/pubs/yuanyu-model-checking.pdf) describes checking invariants of finite-state TLA+ models.

## 3. Method

### What was inspected, reproducibly

From the GA repo root, at the stated checkout:

```powershell
git rev-parse HEAD
git status --short
gh issue view 81 --repo GuitarAlchemist/.github --json title,body,labels,url
gh issue view 701 --repo GuitarAlchemist/ga --json title,body,state,url
Get-Content Common/GA.Business.ML/Agents/VoicingAgent.cs
Get-Content Common/GA.Business.ML/Search/VoicingTelemetryLog.cs
Get-Content Common/GA.Business.Core.Orchestration/Services/LlmConcurrencyGate.cs
Get-Content Common/GA.Business.Core.Orchestration/Services/ConversationHistoryStore.cs
```

For local telemetry, parse `state/telemetry/voicing-search/*.jsonl` and aggregate only `src`, `empty`, and `ms`. Do not publish raw `q` values: the schema stores user query text. No test, benchmark, provider call, or deployment was run in this assessment.

### Smallest next experiments (not performed here)

1. **Baseline first.** Select a versioned, non-personal corpus of typed voicing requests spanning exact chord, fallback chord, instrument filter, and empty result. Pin code SHA, index digest, hardware/runtime, and filter semantics. Run repeated warm, in-process retrieval measurements and a separate end-to-end chatbot run. Report p50/p95, allocation/request, empty-result rate, and stable top-K IDs/scores. Stratify by fallback so an apparent speedup is not simply fewer correct results. Only then set a numerical improvement target; do not transplant the old 6 ms analyzer threshold into the chatbot.
2. **Generated/metamorphic diagrams.** Generate exactly six legal low-to-high string tokens (`x` or fret 0–9 for compact; also 10–24 for dash-only). For the common 0–9 domain, compare compact `x12333`-shaped inputs with their dash-separated representations through [`FretDiagram.TryParseFrets`](../../Common/GA.Business.ML/Agents/FretDiagram.cs) and the comfort-filter parser. Compare fret values, mute positions, fret span, and non-throw behavior; do not equate unlike difficulty policies. Add a custom shrinker that preserves six strings and at least one fretted note where span is asserted. Save the replay seed **and** the smallest valid diagram as a conventional regression example. Check that a deliberate parser mutation is caught; otherwise the property adds no useful oracle. Add an independent same-PC-set bass permutation property for canonical name, while allowing slash suffix to change.
3. **State-sequence model, if needed.** Define actions `Acquire`, `Reject`, `Dispatch`, `Cancel`, `Release`, `AddTurn`, `Read`, and `Evict`. Assert at most three owned gate permits, release exactly once after admission, no dispatch on rejection, per-session last 50 turns, snapshot isolation, and at most 1000 sessions once eviction has quiesced. [`ChatIntakeTests`](../../Tests/Common/GA.Business.Core.Tests/Orchestration/ChatIntakeTests.cs) and [`ChatIntakeStreamingTests`](../../Tests/Common/GA.Business.Core.Tests/Orchestration/ChatIntakeStreamingTests.cs) already cover individual rejection, forwarding, failure, cancellation, and release cases; sequence tests must target an interleaving these examples miss. The store uses wall-clock ticks and concurrent eviction, so exact victim order should not be asserted under tied timestamps without a controllable clock.
4. **TLA+ only against an observed protocol.** The open [GA #701](https://github.com/GuitarAlchemist/ga/issues/701) alleges a bounded-channel consumer-failure deadlock, but this checkout's `IndexVoicingsCommand` already completes the writer with an error, cancels the producer token, and awaits the consumer. Reproduce or dismiss that issue against the current target branch before modeling it. A minimal producer/consumer failure sequence test is cheaper than TLC for this local case. A finite TLA+ model becomes warranted for a specific GA↔IX artifact handoff, retry, duplicate, or lost-ack design only after its state transitions and safety/liveness claims are written down and ordinary sequence tests leave a meaningful gap.

## 4. Evidence observed

| Observation | Direct source | Limit |
|---|---|---|
| `VoicingAgent` times retrieval and writes `ms`, result count, source and dropped-filter fields | [`VoicingAgent.cs`](../../Common/GA.Business.ML/Agents/VoicingAgent.cs), [`VoicingTelemetryLog.cs`](../../Common/GA.Business.ML/Search/VoicingTelemetryLog.cs) | Raw queries may be personal; timer excludes other chat stages. |
| Local files dated May 12, May 30 and Sep 14 contain 25, 2 and 1 records respectively: **28 total, 0 `src=chatbot`** | Local `state/telemetry/voicing-search/*.jsonl`, aggregated on 2026-09-25 | Sparse, non-chatbot, different dates and environments; no credible chatbot p95 or speedup estimate. |
| Latest inspected chatbot QA snapshot has 52 prompts, 98.08% pass, `degraded=false` | [`2026-07-19.json`](../../state/quality/chatbot-qa/2026-07-19.json) | Quality snapshot, not latency or C# allocation evidence. |
| Shared orchestration gate has three permits and zero-timeout acquisition; `ChatIntake` releases in `finally` | [`LlmConcurrencyGate.cs`](../../Common/GA.Business.Core.Orchestration/Services/LlmConcurrencyGate.cs), [`ChatIntake.cs`](../../Common/GA.Business.Core.Orchestration/Services/ChatIntake.cs) | Existing per-host duplicate gate in `GaChatbot.Api`; do not assume process-wide coordination between hosts. |
| History trims to 50 turns; beyond 1000 sessions it evicts 100 by last-access ticks | [`ConversationHistoryStore.cs`](../../Common/GA.Business.Core.Orchestration/Services/ConversationHistoryStore.cs) | Concurrent observations may transiently differ from a sequential reference model. |
| GA #701 remains open, while this checkout's consumer catch completes the channel and cancels its token | [Issue #701](https://github.com/GuitarAlchemist/ga/issues/701), [`IndexVoicingsCommand.cs`](../../GaCLI/Commands/IndexVoicingsCommand.cs) | Source review only; no regression test or remote-target verification here. |

## 5. Provisional verdict

- **Answer:** Partial. Select chatbot voicing retrieval for the *measurement slice* and six-string format equivalence for the *first generative slice*. Neither performance gain nor new bug discovery has been demonstrated.
- **Confidence:** Low for performance priority until traffic and controlled measurements exist; medium that a generated property can cover more legal diagrams than the current finite examples.
- **Independent validation:** None for this note; do not mark the study `concluded`. Existing tests are evidence of current assertions, not proof that the proposed generator will find a defect.
- **Reject formal machinery when:** a simpler deterministic test reproduces and guards the failure; generated cases duplicate existing examples after a mutation check; or a finite TLA+ model lacks a documented transition/implementation mapping. Reject a C# optimization if same-corpus p95 improvement is below measurement noise, if allocations improve without user-visible effect, or if any result-quality guardrail regresses.
- **One-way-door check:** No schema, index dimension, public API, or host migration is authorized by this study. `GaApi` is the accepted future canonical chat host per [`ADR-0005`](../adr/0005-gaapi-single-canonical-chat-host.md), but deployed parity and ingress are separate verification gates.

## 6. Next

Capture the versioned baseline and preserve aggregate results without user text. Convert one former compact-format parser failure into a generated property plus saved counterexample. Reconcile GA #701 against the live target branch and a failure-injection test before using it as a TLA+ case. Return the metrics and test yield to [Wayfinder ticket #81](https://github.com/GuitarAlchemist/.github/issues/81); only then choose an optimization or formal-model implementation ticket.
