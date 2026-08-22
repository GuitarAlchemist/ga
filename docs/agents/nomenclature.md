# Agentic engineering nomenclature

Canonical vocabulary for GA engineering agents and the Cloud Factory. Use these
terms in plans, issues, contracts, traces, and reviews so that similar-looking
mechanisms do not silently acquire each other's authority.

This glossary complements the music/domain vocabulary in `CONTEXT.md`. It does
not replace contract schemas; when a schema defines a field more narrowly, the
schema wins.

## Control and execution

| Term | GA meaning | Do not confuse with |
|---|---|---|
| **Agentic architecture** | A reusable control-flow shape around model calls, tools, state, and evaluators. Examples include ReAct, reflection, Plan–Execute–Verify, blackboard, and meta-controller. | An agent framework/library, a model, or an authorization system. |
| **Architecture family** | A coarse pattern class: reasoning/reflection, sampling/search, retrieval, memory, tools/actions, multi-agent, or safety/routing. | A product/module boundary. One product may use several families. |
| **Architecture catalog** | A discovery inventory of patterns and evidence about where each fits. | A mandate to install every implementation or expose a runtime picker. |
| **Architecture-fit router** | A deterministic or tightly bounded selector that chooses an approved workflow for a typed packet based on task shape and capability. | A meta-controller with permission to widen authority, invent tools, or bypass policy. |
| **Meta-controller** | An architecture/workflow router. Its output is a proposal or an enum naming an already-authorized path. | A supervisor, approver, lease owner, or merge authority. |
| **Work packet** | The typed, versioned unit of Cloud Factory work: exact repo/worktree/SHAs, classification, next action, authority, budget, verifiers, and evidence references. | A chat turn, issue, PR, branch, or model prompt by itself. |
| **Tracer bullet** | The smallest end-to-end vertical slice that crosses every required boundary and produces real evidence. | A horizontal skeleton, throwaway mock, or partial layer implementation. |
| **Bounded queue** | Durable packets advanced by explicit transitions, retry budgets, and terminal states. | An infinite autonomous prompt loop. |
| **Graph engineering** | Designing explicit task/state topology around bounded agent loops: real dependencies, typed handoffs, budgets, vetoes, verification, and stop rules. | A replacement for loop/harness engineering, or a reason to add agents to sequential work. Graphs contain loops. |
| **Agent loop** | One bounded goal executed against inspectable state, with an external verifier, a budget, and a terminal stop condition. | An unbounded autonomous session or an entire multi-agent graph. |
| **Task graph** | Jobs/loops as nodes and real execution or data dependencies as directed edges. | A knowledge graph, an org chart of agent personas, or a diagram of prompts with no operational dependency. |
| **Real edge** | A dependency required because one node consumes another node's typed output, authority transition, or verified state. | A **fake edge** added merely to serialize work, share narrative context, or make a diagram look coordinated. |
| **Diamond** | Split only decomposable work into independent branches, run them with separate contexts, verify them independently, then converge through one deterministic combiner or explicitly authorized owner. | Unbounded fan-out, shared-context consensus, or permission for an agent to merge Git changes. |
| **State handoff** | The minimum typed, versioned state required by the receiving node, with provenance and validation. | Copying an entire chat/context window between nodes. |
| **Stop rule** | The deterministic condition that ends, blocks, quarantines, or requeues a loop/graph when its goal, budget, verification, or authority boundary is reached. | A model deciding that it “feels done.” |

## Decisions and verification

| Term | GA meaning | Do not confuse with |
|---|---|---|
| **Typed feature proposal** | Model-produced categorical facts or bounded values parsed by BAML/JSON/schema. It is untrusted input to deterministic validation. | A decision, receipt, score, or authorization. |
| **Deterministic feature composition** | Code composes a decision signal from validated categorical features using a versioned rule. Adapted from the upstream “deterministic-picker” pattern. | Deterministic truth: model-proposed features can still be wrong. Safety/authority decisions also require direct facts and policy. |
| **Preflight simulation** | Before a side effect, resolve exact targets and predict/measure scope using read-only tools, then apply deterministic policy and any required human approval. | A model imagining effects and approving its own action; that is advisory only. |
| **Postflight verification** | After a side effect, run declared checks and bind outputs to packet generation, HEAD SHA, dirty digest, command, toolchain, and time. | A completion claim or telemetry event without exact-state evidence. |
| **Plan–Execute–Verify (PEV)** | A per-step loop: plan an atomic step, execute within authority, verify against its contract, retry within budget, then accept or block. GA's variant is fail-closed. | Upstream `fail-accepted` behavior. Exhausted or uncertain verification becomes `blocked`, never accepted completion. |
| **Independent review** | A fresh reviewer evaluates the exact verified SHA without sharing the implementer's mutable context or issuing its own evidence receipt. | Self-critique, same-agent reflection, CI alone, or review of an earlier SHA. |
| **Evidence manifest** | Immutable, schema-valid postflight facts bound to exact state and artifacts. | Observability traces, summaries, model prose, or a receipt issued only by the worker being audited. |
| **Architecture-fit benchmark** | A versioned task matrix testing candidate patterns on representative tasks, cost, failure modes, and guardrails. | A global leaderboard or a claim that one architecture is universally best. |
| **External evidence** | A fact produced outside the model graph, such as an exact-SHA test result, compiler output, money/customer outcome, protected-system state, or accountable human decision. | Agreement among agents or internally generated confidence. More agents can produce organized nonsense without an external oracle. |
| **Veto edge** | A fail-closed transition from an independent verifier, deterministic policy, or required human gate that can prevent a downstream side effect. | A model suggestion or majority vote. A task-graph edge does not itself confer authority. |

## State, memory, and observability

| Term | GA meaning | Do not confuse with |
|---|---|---|
| **Authoritative ledger** | Transactional packet, transition, lease/fence, budget, and approval state. It is the source used at mutation boundaries. | A blackboard, chat history, JSONL mirror, dashboard, or narrative digest. |
| **Blackboard** | Shared working context where agents publish partial results for other agents to consume. | Ownership, locking, authorization, or evidence. Blackboard entries are untrusted until validated. |
| **Knowledge graph product** | A scoped, ontology-governed, provenance-bearing graph built through entity/relation/event extraction, quality gates, fusion, and a supported query surface. | A pile of triples, a task graph, or an authoritative coordination ledger. |
| **Narrative memory** | Human/agent summaries, session digests, reflections, and retrieved context that help resume reasoning. | Authoritative state or proof that work is fresh, owned, approved, or complete. |
| **Reflection** | Generate → critique → refine within the current task. | **Reflexion**, which persists verbal lessons/feedback in episodic memory across attempts or calls. Neither substitutes for independent review. |
| **Observability plane** | Best-effort traces, metrics, sessions, costs, scores, and dashboards used to diagnose behavior. Langfuse may be one replaceable backend. | The authority/evidence plane. Telemetry may be sampled, delayed, dropped, or redacted. |
| **Authority plane** | Deterministic policy, operator grants, protected paths, leases/fences, budgets, and allowed side effects. | An LLM evaluator, meta-controller, Langfuse score, or GitHub topic/category. |

## Source-use terms

| Term | GA meaning |
|---|---|
| **Mechanism catalog** | An upstream implementation inspected for transferable mechanisms, failure modes, and tests. Hermes Agent, OpenClaw, Langfuse, and `all-agentic-architectures` are currently used this way; they are not embedded runtimes. |
| **Discovery feed** | A changing, community-tagged list used to find candidates. GitHub's `agent-architecture` topic is a discovery feed, not a vetted bibliography or adoption queue. |
| **Primary source** | The originating paper, official repository source/tests, or official product documentation used to substantiate a claim. Topic pages, listicles, stars, and repository descriptions are leads only. |

## Cloud Factory usage rules

1. Choose the smallest architecture that matches the packet; do not add a
   meta-controller when a typed `switch` is sufficient.
2. Model outputs may propose features, routes, critiques, or next actions. They
   may not grant authority, own a fence, debit a budget, validate Git state, or
   certify their own work.
3. Preflight and postflight are separate gates. Passing one does not imply the
   other.
4. Verification exhaustion, uncertain side effects, stale evidence, or uncertain
   ownership stop or requeue the packet. GA never `fail-accepts` them.
5. Architecture names in a trace describe control flow, not correctness. Record
   the exact architecture/version and evaluate it on the task class before wider
   rollout.
6. Use a graph only when real dependencies or parallelizable work require one.
   Sequential work stays in one loop; a diamond has bounded fan-out, separate
   verifier contexts, and one owned convergence step.
7. Every consequential graph path terminates in external evidence or an
   accountable human gate. Consensus inside the graph is not a receipt.

## Sources and adaptation notes

- Fareed Khan's [`all-agentic-architectures`](https://github.com/FareedKhan-dev/all-agentic-architectures)
  provides the family/catalog vocabulary, pattern-fit benchmark idea, and the
  upstream “deterministic-picker” term. GA uses the more precise
  **deterministic feature composition** because model-derived features remain
  probabilistic.
- The upstream Dry-Run and PEV implementations are educational references. GA
  strengthens them: direct read-only facts and policy precede side effects;
  approval cannot be self-issued; PEV blocks instead of `fail-accepted`.
- GitHub's [`agent-architecture` topic](https://github.com/topics/agent-architecture)
  is monitored only for discovery. Any candidate must pass license, maintenance,
  primary-source, architecture-fit, security, and duplication review before it
  influences a plan.
- [`codejunkie99/graph-engineering`](https://github.com/codejunkie99/graph-engineering)
  contributes the knowledge-graph/task-graph distinction, real-edge discipline,
  bounded diamond, and owned-convergence vocabulary. GA treats it as a compact
  design reference, not an orchestration runtime.
- Louis Bouchard's [graph-engineering explanation](https://www.louisbouchard.ai/graph-engineering-explained/)
  reinforces that graphs organize loops rather than replace them, and that
  probabilistic nodes require explicit state, budgets, vetoes, external
  verification, and hard stops. GA further binds verification to exact state and
  keeps authority outside the model graph.
