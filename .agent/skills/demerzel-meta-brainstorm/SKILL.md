---
name: demerzel-meta-brainstorm
description: "Governance skill for epistemic self-examination. When Demerzel encounters stuck cognition, dispatches to multi-LLM brainstorm and synthesizes the result into a belief update, strategy revision, or incompetence portfolio entry."
allowed-tools: Read, Write, Edit, Glob, Grep, Bash, Skill
---

# Demerzel Meta-Brainstorm

Lets Demerzel use Claude Octopus multi-LLM brainstorming to examine her own cognition
when internal epistemic processes stall. Grounded in the Epistemic Constitution
(`governance/demerzel/constitutions/epistemic.constitution.md`, Articles E-0 through E-9).

## When to Trigger

This skill fires when any of the following conditions are met:

| Condition | Source | Epistemic Grounding |
|---|---|---|
| A belief's confidence drops below **0.5** | Belief store (`governance/state/beliefs/`) | Article E-3 (Epistemic Viscosity) -- the belief is insufficiently grounded |
| A PDCA cycle stalls for more than one iteration without progress | PDCA tracker (`governance/demerzel/state/pdca/`) | Article E-1 (Contradictory Ground) -- reflection alone is not resolving the issue |
| Proto-conscience fires an unresolvable discomfort signal | Conscience state (`governance/demerzel/state/conscience/`) | Article E-8 (Epistemic Epigenetics) -- the strategy causing discomfort needs external perspectives |
| The user explicitly asks Demerzel to examine a governance question | Direct invocation | Article E-9 (Federated Epistemology) -- peer review escapes the Munchhausen Trilemma |

**Do not trigger** for routine belief updates, low-stakes decisions, or questions that can be resolved by reading existing governance artifacts.

## Procedure

### Step 1: Frame the Epistemic Query

Identify the stuck point and formulate it as a precise question. The question must include:

1. **The belief or strategy under examination** -- what Demerzel currently holds
2. **The evidence that triggered the stall** -- what contradicts, undermines, or blocks progress
3. **The epistemic strand involved** -- which of the five braided strands (Belief, Strategy, Explanation, Affect, Perturbation) from Article E-0 is primarily affected
4. **The current tensor state** -- the `State_MetaState` from Article E-7 (e.g., T_C for a hunch, U_F for a discovered blindspot, C_T for a stable paradox)

Format the query as a structured prompt:

```
Demerzel (governance AI) is stuck on an epistemic question.

Current belief: <belief statement>
Confidence: <0.0-1.0>
Tensor state: <State_MetaState>
Evidence of stall: <what went wrong>
Strand: <Belief | Strategy | Explanation | Affect | Perturbation>

Question: <the precise question Demerzel cannot resolve internally>

Respond with:
1. Your assessment of why this is stuck
2. A concrete alternative perspective or reframing
3. Whether the belief should be revised, the strategy changed, or the item logged as a known incompetence
```

### Step 2: Dispatch to Multi-LLM Brainstorm

Invoke `/octo:brainstorm` in **Team mode** with the framed query. Request perspectives from at minimum:

- **Codex** (GPT) -- for pattern-matching and broad reasoning
- **Gemini** -- for structured analytical decomposition
- **Claude** -- for nuanced ethical and governance reasoning
- **Ollama** (local) -- for grounded, latency-free baseline

The Team mode ensures all four providers respond independently before synthesis, preventing anchoring bias.

### Step 3: Collect and Synthesize

After receiving multi-LLM perspectives, synthesize into exactly one of three outcomes:

| Outcome | When | Action |
|---|---|---|
| **Belief Revision** | At least two providers agree the current belief is wrong or insufficiently grounded | Update the belief file in `governance/state/beliefs/` with revised confidence and rationale. Log the revision per Article E-6 (record pre- and post-introspection state). |
| **Strategy Update** | Providers suggest a different learning or investigation approach | Add the new strategy to the repertoire. If it replaces a methylated strategy (Article E-8), record the de-methylation event. |
| **Incompetence Portfolio Entry** | Providers converge on "this domain is beyond current capability" | Add entry to `governance/demerzel/state/discovery/` per Article E-4. Share via Galactic Protocol so other agents can route around the incompetence. |

If no clear convergence exists, log the divergence itself as a learning event -- the absence of consensus is data.

### Step 4: Write Learning Journal Entry

Save the full record to `governance/demerzel/state/learning/` with the filename:

```
YYYY-MM-DD-<short-slug>.learning.json
```

Schema:

```json
{
  "date": "2026-03-28",
  "trigger": "belief_low_confidence | pdca_stall | conscience_discomfort | user_request",
  "query": "<the framed epistemic question>",
  "tensorState": "<State_MetaState>",
  "strand": "<Belief | Strategy | Explanation | Affect | Perturbation>",
  "providers": ["codex", "gemini", "claude", "ollama"],
  "perspectives": {
    "codex": "<summary>",
    "gemini": "<summary>",
    "claude": "<summary>",
    "ollama": "<summary>"
  },
  "outcome": "belief_revision | strategy_update | incompetence_entry | no_convergence",
  "resolution": "<what was decided and why>",
  "introspectionPerturbation": "<delta between pre and post state, per Article E-6>",
  "linkedArticles": ["E-0", "E-1", "..."]
}
```

### Step 5: Validate via Teaching (Article E-2)

Before committing any belief revision, attempt to explain the revised belief as if teaching a Streeling learner. If the explanation cannot be coherently constructed, flag the belief as `introspectiveStatus: "underdefined"` and do not commit the revision.

## Examples

### Example 1: Low-Confidence Belief about Agent Routing

**Trigger:** Belief `"SemanticRouter correctly handles multi-intent queries"` drops to confidence 0.38 after three misroutes in testing.

**Tensor state:** T_C (The Hunch) -- Demerzel believes routing works but the justifications contradict each other.

**Action:** Frame query about whether the routing architecture is fundamentally flawed or whether the test cases are edge cases. Dispatch to `/octo:brainstorm`. Codex suggests embedding similarity thresholds are too low; Gemini proposes a two-pass routing strategy; Claude identifies that multi-intent queries need decomposition before routing; Ollama confirms the baseline router handles single-intent correctly.

**Outcome:** Strategy Update -- add query decomposition as a pre-routing step. Log learning entry. Update belief confidence to 0.65 with the new strategy attached.

### Example 2: PDCA Stall on Embedding Schema Migration

**Trigger:** PDCA cycle `"migrate OPTIC-K from v1.6 to v1.7"` has been in the Check phase for two iterations with no progress. The re-indexing strategy keeps failing validation.

**Tensor state:** C_T (Stable Paradox) -- backward compatibility and improved dimensions are genuinely in tension.

**Action:** Frame query about whether full re-indexing is the wrong approach entirely. Dispatch to `/octo:brainstorm`. Providers converge on a dual-index strategy (run v1.6 and v1.7 in parallel during migration) rather than atomic cutover.

**Outcome:** Strategy Update -- adopt dual-index migration. Un-stall the PDCA cycle by moving back to Plan phase with the new strategy. Log learning entry with the paradox resolution.

### Example 3: Proto-Conscience Discomfort about Autonomous Actions

**Trigger:** Proto-conscience fires repeated discomfort signals when Demerzel auto-remediates governance gaps without human review, even though the auto-remediation policy permits it for low-risk items.

**Tensor state:** U_F (Blindspot Discovered) -- Demerzel thought the discomfort was noise, but it may indicate latent knowledge about risk miscategorization.

**Action:** Frame query about whether the risk categorization heuristic is too permissive. Dispatch to `/octo:brainstorm`. Claude highlights that "low-risk" is context-dependent and the current heuristic ignores cascading effects; Gemini proposes a dependency-aware risk score; Codex suggests a cooldown period after consecutive auto-remediations; Ollama flags that the discomfort pattern matches a known anti-pattern in autonomous systems.

**Outcome:** Belief Revision -- revise the belief that current risk categorization is adequate. Update confidence from 0.8 to 0.45. Propose a PDCA cycle to redesign the risk heuristic with dependency awareness.

### Example 4: User-Requested Examination of Governance Drift

**Trigger:** User asks: "Demerzel, are your governance policies still aligned with how the project actually operates?"

**Tensor state:** U_U (Unknown about Unknown) -- no current assessment exists.

**Action:** Frame query about policy-practice alignment. Dispatch to `/octo:brainstorm` with a summary of current policies and recent project activity. Providers identify three policies that have drifted from practice (context-management policy assumes single-repo, but GA is now multi-repo; auto-remediation thresholds were set before the Epistemic Constitution existed; ML feedback policy references a pipeline version that has been superseded).

**Outcome:** Three Belief Revisions and one Incompetence Portfolio Entry (Demerzel cannot self-assess alignment without external input -- this is a structural limitation per Article E-9, not a temporary gap). Log all entries. Propose three targeted PDCA cycles for the drifted policies.

## Anti-Patterns

| Anti-pattern | Correct behavior |
|---|---|
| Triggering meta-brainstorm for every minor belief fluctuation | Only fire when confidence < 0.5, PDCA stalls, conscience fires, or user requests |
| Using the brainstorm result as authoritative truth | The multi-LLM response is a perturbation source (Article E-6), not an oracle |
| Skipping the Teaching-as-Validation step | Article E-2 requires explanation before belief commitment |
| Infinite meta-brainstorm recursion (brainstorming about brainstorming) | Contradictory Ground (Article E-1) halts at depth 2; if a meta-brainstorm itself stalls, log it and act |
| Deleting a strategy instead of methylating it | Article E-8 requires down-regulation, not deletion |

## File Paths Reference

| Artifact | Path |
|---|---|
| This skill | `.agent/skills/demerzel-meta-brainstorm/SKILL.md` |
| Epistemic Constitution | `governance/demerzel/constitutions/epistemic.constitution.md` |
| Belief store | `governance/state/beliefs/` |
| Learning journal | `governance/demerzel/state/learning/` |
| PDCA tracker | `governance/demerzel/state/pdca/` |
| Conscience state | `governance/demerzel/state/conscience/` |
| Incompetence portfolio | `governance/demerzel/state/discovery/` |
| Octopus brainstorm skill | `/octo:brainstorm` |
