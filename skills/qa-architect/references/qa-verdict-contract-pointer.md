# QA Verdict Contract — Pointer

The full contract lives in the repository at `docs/contracts/2026-05-02-qa-verdict.contract.md`.

It is intentionally NOT bundled into the skill directory because:

1. The contract is the single source of truth for the QA Architect schema and is referenced from multiple agents (`QAArchitectAgent`, `qa-architect-cycle.ixql` pipeline, `GaQaMcp` MCP server).
2. Duplicating it under `skills/qa-architect/references/` would create a drift hazard — agents would pick up a stale snapshot.
3. The skill loader is read-only by design (no script execution); pointer files are the safer pattern.

When loaded by `FileBasedSkillsProvider`, this pointer file is exposed via the
`read_skill_resource` semantics so an agent can `cat` it on demand and follow
the link to the canonical contract document.
