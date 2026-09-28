# Cross-repository Wayfinder operating plan

Created: 2026-09-25  
Status: draft for issue grooming and decision-making; no implementation is authorized by this document.

## Objective

Make the active GuitarAlchemist and spareilleux portfolio navigable, evidence-backed, and useful to an AFK operator. The first product outcome is a trustworthy GA chatbot experience. Other work earns priority by proving that it improves that outcome, makes autonomous delivery safer, or produces reusable research and verified courses. A root-only GitHub view should let a reader find each decision program without scanning hundreds of execution issues.

## Verified starting point

The GitHub inventory on 2026-09-25 found 15 repositories under `GuitarAlchemist` and 11 under `spareilleux`. Eleven organization repositories contain 312 open issues in total; all eleven personal repositories currently have zero open issues. This is an issue-count snapshot, not a claim that every issue is ready or relevant. The repositories with open issues are:

| Repository | Open issues | Existing navigation to preserve |
| --- | ---: | --- |
| `GuitarAlchemist/ga` | 85 | GA roadmap issue 482; its native child hierarchy is incomplete |
| `GuitarAlchemist/gaia` | 39 | Gaia navigation issue 77 with native children; unattended-pump outcome 40 remains separate |
| `GuitarAlchemist/ix` | 34 | IX roadmap issue 188 with native children |
| `GuitarAlchemist/Demerzel` | 58 | Roadmap issue 499; declarative-pipeline issue 497 and its children |
| `GuitarAlchemist/tars` | 28 | Runtime roadmap issue 70 with native children |
| `GuitarAlchemist/hari` | 11 | Epistemic epic issue 12; three declared child links have now been reconciled natively |
| `GuitarAlchemist/.github` | 38 | Product-program Wayfinder map 57; AI-harness program 68; Project setup ticket 65 |
| `GuitarAlchemist/agent-blackbox` | 13 | Inventory only until its private scope and current owner are checked |
| `GuitarAlchemist/demerzel-bot` | 3 | Inventory only |
| `GuitarAlchemist/ga-godot` | 2 | Inventory only |
| `GuitarAlchemist/guitaralchemist.github.io` | 1 | Inventory only |

`spareilleux/learn` publishes the courses and has issues enabled but no open backlog. Its source language is English, with French and Spanish mirrors. The published Streeling pages say generated modules have not been author-reviewed or machine-tested; `learn` imports them from `GuitarAlchemist/Demerzel` through `scripts/sync-streeling.mjs`, preserving only the journals across syncs. This is a provenance and verification gap to manage, not a reason to mark generated pages complete. The current public site and repo instructions are authoritative for the course inventory and editorial contract.

## Portfolio model

1. **Wayfinder maps are decision roots.** One issue labeled `wayfinder:map` states a destination, settled decision pointers, live fog, and out-of-scope boundaries. Its native children are bounded decision tickets (`wayfinder:research`, `wayfinder:grilling`, `wayfinder:prototype`, or `wayfinder:task`). A map is complete when nothing material remains undecided before bounded delivery.
2. **Execution roadmaps remain execution roots.** GA 482, Gaia 77/40, IX 188, Demerzel 499/497, tars 70, and Hari 11/12 represent delivery scope. Do not relabel or mass-reparent them as Wayfinder decision maps. Native parentage means scope only; it does not imply dependency, accountability, readiness, evidence, or authority.
3. **The global root view contains maps only.** Prefix each displayed map title with its repository name (for example, `[ga]`, `[ix]`, `[learn]`, `[.github]`). The public GitHub search for `is:issue is:open label:wayfinder:map org:GuitarAlchemist` works as an interim read-only root index. A saved cross-owner Issues view or Project remains to be created after authenticated access and filter behavior are verified; the CLI token currently lacks `read:project`/`project` scope and the in-app GitHub browser is signed out. Avoid changing access just to claim the view is done.
4. **Cross-repository links are explicit contracts, not implied parentage.** GitHub native sub-issues may cross repositories under one owner, but must not be assumed across `GuitarAlchemist` and `spareilleux`. Use links and an index for cross-owner relationships.
5. **Groom by evidence.** For each open issue, record whether its outcome is still needed, whether a live PR supersedes it, exact owner/readiness, acceptance evidence, dependencies, and the next bounded slice. Preserve the original body and discussion. Do not close or merge from labels, activity, or an agent's claim alone.

The new cross-repository decision map is [`.github` Wayfinder portfolio route](https://github.com/GuitarAlchemist/.github/issues/74). The existing [product-program Wayfinder](https://github.com/GuitarAlchemist/.github/issues/57) remains a separate, narrower product decision map. The portfolio map must not duplicate or silently overrule it.

Its first native decision frontier is [GA chatbot tracer](https://github.com/GuitarAlchemist/.github/issues/75), [root-only view and hierarchy](https://github.com/GuitarAlchemist/.github/issues/76), [Gaia AFK pump and rooms](https://github.com/GuitarAlchemist/.github/issues/77), [IX/DB/RAG comparison](https://github.com/GuitarAlchemist/.github/issues/78), [course and Streeling loop](https://github.com/GuitarAlchemist/.github/issues/79), [Jev quality/cost gates](https://github.com/GuitarAlchemist/.github/issues/80), and [measured C# and formal-testing leverage](https://github.com/GuitarAlchemist/.github/issues/81). These are open questions, not accepted solutions or scheduled implementation.

## Triage order and evidence thresholds

| Order | Outcome and candidate issues | Admission evidence | Rejection or stop rule |
| --- | --- | --- | --- |
| 1 | GA chatbot and guitarist coach: GA 623, 589, 560, 703, 328, 724, 726; architectural parity and ingress | Current public request path, exact host, deterministic music-intent corpus, routing-eval baseline, error/latency measurement, focused tests | Do not retire `GaChatbot.Api` until `GaApi` parity, ingress migration, and live verification are proved; old outages are not current proof |
| 2 | AI harness and Gaia AFK pump: organization 68; GA 629/630; Gaia 40, 74, 90, 93, 103, 104, 106, 117, 151 | One observed→owned→acted→reconciled unattended tracer, atomic claim, exact subject/SHA, durable receipts, independent review, bounded resource use | No agent self-merge, admin bypass, secret or paid-API assumption; issue 74's design still requires a digest-bound receipt, independent review, and explicit human acceptance |
| 3 | IXQL pipelines and retrieval: Demerzel 497/527/594/820; IX 188/205/208/211/281/293/331 | Reproducible workload, deterministic parser/executor behavior, truth signals, retrieval quality and latency, compare/no-adopt verdict | Do not conflate DuckDB 1.x tested pin with 2.0 planned release; LadybugDB remains a candidate until a measured graph workload beats simpler options |
| 4 | Jev and dogfooding | Pre-registered corpus, deterministic baseline, blinded semantic and decision-quality scoring, cost per accepted decision, false-accept/abstention rates, authority-safe shadow mode | No production routing or effect authorization from a probabilistic score; stop when quality or cost guardrail fails |
| 5 | GA C# performance and formal methods | Profiled user workload, baseline, regression guardrail, minimized counterexamples for music invariants, state-sequence or formal model only where risk warrants it | No speculative optimization or formal model without a failure it can find and a maintainer who will use it |
| 6 | Courses, Streeling, research lab | Pinned source commits, runnable examples, primary sources, journal QA/experiment tables, explicit unverified/generated labels, course-to-repo issue feedback | Do not turn generated prose into an author-verified claim or publish a code assertion without checking the exact referenced revision |

The order is a default WIP policy, not proof that lower rows are unimportant. A reliability failure can jump the queue when it blocks the first product outcome. Only one high-risk product or pump transition should own active implementation authority at a time; independent research can proceed in parallel without effects.

## Decision and delivery phases

### Phase A — Make the map trustworthy

- Reconcile repository scope (active/public, private, legacy, forks, zero-backlog), existing roots, labels, duplicate issues, closed parents with open children, and mismatches between body-declared and native parentage. The Hari 13/14/16 → 12 reconciliation is an example of a safe, evidence-backed repair; it is not license to reparent 312 issues automatically.
- Decide whether the canonical root index is a saved Issues view or an organization Project. Verify a filter that includes only `wayfinder:map` roots across both owners and a title-prefix convention. Keep the public search URL as a fallback, not as a claimed saved view.
- Run one repo-at-a-time grooming batch. For each issue: read body, native parent, linked PR, state, priority, blocker, and comments; write a dated, concise assessment only when it changes the reader's decision. Re-fetch after each mutation. Produce a per-repo count of keep, split, superseded, blocked, duplicate, or unclear, with links.
- Add a Wayfinder map to a repository only when its destination and decision frontier are distinct from existing maps; zero-open-issue repos need an inventory row, not an empty map by default.

### Phase B — Prove the GA chatbot path first

- Use the [dated GA chatbot baseline](../research/2026-09-25-ga-chatbot-baseline.md): public read-only GETs currently reach `GaChatbot.Api`, while today's CI QA snapshot is degraded with a null pass rate. Neither proves a complete chat answer. Verify the live ingress, route contracts, streaming, history and tool behavior against the canonical-host ADR before proposing retirement.
- Prefer one musician-visible public SSE progression-to-arpeggio tracer as the provisional first slice, not the entire coach. Pre-register route, answer-shape and musical oracles, terminal stream frame, measured latency/error baseline, negative counterexamples and human review. Reject or reorder it if the controlled public run reveals a smaller reliability blocker.
- Separate correctness from model eloquence: use deterministic music-theory and voicing oracles, an explicit routing baseline, property/metamorphic counterexamples, and live smoke evidence. Triage the currently reported 502/route failures by reprobing them; do not classify historical reports as live outages without evidence.
- Finish with a decision: ship a bounded user slice, repair an objective reliability blocker first, or reject the candidate. Link the finding back to both GA execution issues and the Wayfinder ticket.
- Follow the [C# performance and formal-testing research note](../research/2026-09-25-ga-performance-formal-testing-leverage.md) for a measurement-first slice: the 28 retained voicing telemetry records include no chatbot request, so no representative chatbot p95 is known. A six-string diagram metamorphic property is a bounded FsCheck candidate; state-sequence tests and TLA+ require a distinct failure/interleaving or cross-process protocol to justify them.

### Phase C — Bound the Gaia AFK pump and workgroups

- Map the pump state machine and exact ownership of claim, worker launch, review, PR promotion, merge and reconciliation. Distinguish a live process from progress and a receipt from authority.
- Compare restart/replay strategies and prove duplicate/reordered effects cannot silently double-act. Keep provider-specific resume identity bound to generation, subject and head SHA. Preserve normal review and merge gates.
- Use [Gaia coordination rooms](https://github.com/GuitarAlchemist/gaia/issues/151) as the proposed workgroup seam: one coordinator, one worker and one independent reviewer, bounded membership generation, replayable transcript and per-recipient acknowledgements. Room messages and presence are untrusted coordination, never approval or merge authority.
- Hold hosted pump implementation until Gaia's existing design-gate requirements and human acceptance are satisfied. A design-only receipt is not an implementation mandate.

### Phase D — Compare IX/DB/RAG boundaries with real workloads

- Select two or three existing workloads: one declarative evidence pipeline in Demerzel, one GA retrieval/ranking request, and one cross-repo issue/course evidence graph. Record exact inputs, correctness oracle, latency, memory and update cost before engine choice.
- Establish IXQL's parser/executor semantics and `when` behavior first. Run a deterministic DuckDB 1.x baseline at the pinned revision; treat 2.0's announced parser/storage/API changes as a future compatibility probe after stable release. Compare LadybugDB only where relationship traversals or graph retrieval create a measurable advantage.
- Evaluate agentic RAG against simple lexical/vector/hybrid baselines with answer grounding, retrieval recall, citation correctness, latency and incremental maintenance cost. A retrieval result may inform an agent but cannot itself become governance authority.
- Use the [bounded IX research note](https://github.com/GuitarAlchemist/ix/blob/research/wayfinder-20260925/docs/research/2026-09-25-ixql-duckdb-ladybug-rag-boundaries.md) as an experiment brief: its 12-question fixture and promotion thresholds are proposals, not benchmark results. It keeps IXQL coordination separate from DuckDB analysis and reserves Ladybug for a demonstrated multi-hop advantage.
- Record adopt, incubate or reject decisions in the owning repo. Avoid cross-repo schema or index changes without explicit coordinated sign-off.

### Phase E — Make Jev and the research lab falsifiable

- Reuse the published Jev/TypeSafe and repository-dogfooding lab methods as hypotheses, not endorsements. The journal already records 126 pre-registered live IX routing calls and a 506-call robustness batch; do not present live testing as still wholly pending. The first batch reported macro-F1 0.967 versus production 0.745 on its bounded corpus, while the French robustness gate reported KILL because of probability rounding and timeout behavior. These are not production-traffic or generalization proofs. Pre-register the next calibration corpus and scoring rubric before additional live calls. Keep model, prompt, state, cost cap and stop condition fixed per comparison, and require a privacy decision before using real user traffic.
- Score semantic preservation and decision consequence separately from token savings. A 50% token reduction is not a win if false acceptance, abstention or downstream rework rises beyond the guardrail. Include negative controls and held-out cases.
- Use the [Jev adoption-gates research note](https://github.com/spareilleux/learn/blob/research/wayfinder-20260925/docs/research/2026-09-25-jev-quality-cost-adoption-gates.md) to separate the completed 632 IX calls from the unrun calibration. Its reported rate-card cost is arithmetic, not an invoice, and its next live pilot is conditional on independent labels, a frozen protocol, privacy review, and an operator budget.
- Publish a dated journal row for confirmed, refuted and inconclusive outcomes, with the exact artifact digest, source revision, command/output or API receipt, and follow-up issue. Any repo adoption requires a narrow owner, independent review and a rollback path.

### Phase F — Restart Streeling as verified learning

- The [Streeling course-loop research note](https://github.com/spareilleux/learn/blob/research/wayfinder-20260925/docs/research/2026-09-25-streeling-course-journal-loop.md) found 31 published English modules still labeled generated/unreviewed, an empty study journal, and a Demerzel Seldon workflow whose agent step is currently disabled. Start with one pinned, reviewed module and a runnable example; decide separately whether to repair the manual or scheduled generation route. Do not interpret publication or a green site build as claim verification.
- Keep Demerzel `state/streeling/courses` as the generator source and `learn` as a reproducible publication consumer. The current scheduled route is disabled; its rationale and review capacity remain unknown. Do not edit generated `streeling/` pages directly.
- Choose one first module relevant to current repo decisions, then consider GA chatbot/music reasoning, Gaia distributed-state safety, IXQL/DuckDB/retrieval evaluation, C# performance, and agentic engineering. Require one research question, primary sources, runnable example or reason it cannot run, dated experiment, and open question per module.
- Run the three-locale sync at a pinned Demerzel commit. Mark generated but unreviewed content visibly; promote to studied/verified only after journal evidence and independent checks. Feed discrepancies into the owning repo issue, and link the issue/PR outcome back to the journal's QA row.

## Completion and review

The plan is successful when the global root view is accessible and contains only distinct, prefixed Wayfinder maps; every repository with active work has an honest root or an explicit no-map rationale; high-priority issues have dated evidence-based grooming; the GA chatbot's next user-visible tracer is decided; Gaia's AFK autonomy remains gated by exact authority and recovery proof; IX data/RAG and Jev experiments have baselines and rejection criteria; and Streeling's source-to-publication loop has a verified, repeatable small batch. Issue creation, labels, comments or a generated lesson alone satisfy none of these outcomes.

## Current blockers and assumptions

- The CLI can edit issues but lacks GitHub Project scopes; the in-app GitHub browser is signed out. A saved global view remains blocked pending authenticated access, though a public root-only search is usable.
- The phrase “Gaia chatbot” may mean GA's chatbot or a new Gaia conversational surface. No chatbot implementation was found in Gaia during the repository audit; do not assume a new UI is requested.
- Repository scope defaults to both owners, with private/legacy/fork inclusion decided separately. Open-issue counts are date-bound and need refresh before a bulk operation.
- No paid Jev calls, provider fanout, code edits, PR merge, deployment, credential change, or permanent deletion is authorized by this plan.
