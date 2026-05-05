# `skills-dev/` — drafts workspace for the skill-stewards iteration loop

Drafts of new chatbot skills live here while authors iterate. Once a skill
stabilises (eval green, behaviour pinned, no open feedback), it graduates
to the canonical [`skills/`](../skills) directory via the
[`skill-graduator` agent](../.claude/agents/skill-graduator.md).

## How it works

`FileBasedSkillsProvider` (in `Common/GA.Business.ML/Agents/AgentFramework/`)
loads SKILL.md files from BOTH `skills/` and `skills-dev/`. On Name
collision, the draft (later in priority order) **shadows** the canonical
version. This means while you iterate on `skills-dev/foo/SKILL.md`, the
running chatbot uses the draft — even if `skills/foo/SKILL.md` still
exists. Live-reload (200 ms debounce) means edits land in the chatbot
within a fraction of a second; no restart.

## Authoring a new skill

1. **Scaffold**: invoke the `skill-author` agent with a description of
   what the skill should do. It generates a stub at
   `skills-dev/<name>/SKILL.md` with the correct camelCase frontmatter.
2. **Iterate**: edit triggers, body, and the orchestrator behaviour
   referenced in the body. Both the chatbot host and your Claude Code
   session see the changes immediately (the latter if symlinked into
   `~/.claude/skills/ga-dev/`).
3. **Graduate**: when stable, invoke the `skill-graduator` agent. It
   moves the draft to `skills/<name>/`, runs `SkillMdParserTests`,
   commits, and opens a PR.

## Convention

- Drafts MUST use camelCase frontmatter (`name:`, `description:`,
  `triggers:`) — matches Anthropic's published spec and the canonical
  GA convention.
- Drafts MAY shadow a canonical skill of the same Name. This is
  intentional during refactors: keep the canonical stable while
  iterating on a replacement.
- Drafts are committed to git like canonical skills (no `.gitignore`
  entry). The expectation is short-lived branches: scaffold → iterate
  → graduate within a few sessions, not months of stale drafts.

## What's tested

`Tests/Common/GA.Business.ML.Tests/Unit/FileBasedSkillsProviderReloadTests.cs`
contains five `MultiDir_*` cases pinning:

- Draft shadows canonical on Name collision.
- Non-overlapping skills from both dirs both load.
- One missing dir doesn't break loading from the other.
- Graduation flow: `git mv skills-dev/<name> skills/<name>` → no
  duplicate skill entries.
- Constructor rejects an all-empty directory list.

## See also

- [`docs/contracts/skill-md-schema.md`](../docs/contracts/skill-md-schema.md) — the SKILL.md format contract (camelCase canonical, PascalCase still parses).
- [`docs/plans/2026-05-05-skill-stewards-team.md`](../docs/plans/2026-05-05-skill-stewards-team.md) — full team blueprint, including the three background roles still to wire (skill-iterator, skill-maintainer, skill-archaeologist).
- [`.claude/agents/skill-author.md`](../.claude/agents/skill-author.md) — the on-demand author agent.
- [`.claude/agents/skill-graduator.md`](../.claude/agents/skill-graduator.md) — the on-demand graduator agent.
