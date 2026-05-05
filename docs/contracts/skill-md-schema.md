# SKILL.md schema — GA / Claude Code parity contract

A `SKILL.md` is a markdown file with a YAML frontmatter block (delimited by
`---` lines) that declares a skill the chatbot orchestrator can dispatch to,
optionally also loadable as a Claude Code plugin skill.

This contract describes the union of fields read by both ecosystems so a
single `SKILL.md` authored once works in both. It is the source of truth for
`Common/GA.Business.ML/Skills/SkillMdParser.cs` (GA loader) and Anthropic's
[Claude Code plugin skill format][claude-skill-spec].

## Required fields

| Field | Casings accepted | Read by | Purpose |
| --- | --- | --- | --- |
| `name` / `Name` | both | GA, Claude Code | Identifier surfaced to users; selector for `Skill` invocations. |
| `description` / `Description` | both | GA, Claude Code | One-line capability summary. Claude Code matches this against user intent for routing; GA surfaces it in `/api/chatbot/examples` and trace metadata. |

A file missing **both** casings of `name` fails to load on GA (returns `null`
from `SkillMdParser.TryParseContent`) and is ignored by Claude Code.

## GA-only fields

Read by `SkillMdParser` and consumed by `IOrchestratorSkill.CanHandle`,
`SkillMdLoader`, and downstream telemetry. Claude Code ignores them.

| Field | Type | Purpose |
| --- | --- | --- |
| `triggers` / `Triggers` | `string[]` | Keyword patterns matched (case-insensitive) by the chatbot's deterministic dispatcher. Triggers shorter than `MinTriggerLength` (3 chars) are silently dropped to avoid `"a"`/`"an"` shadowing. A skill with no triggers is loadable but is **not auto-dispatched**; it must be invoked via semantic-intent routing or as a Claude Code guide. |
| `license` | `string` | Surfaced in audit reports. |
| `compatibility` | `map` | Versioned dependency hints, e.g. `agent-framework: ">=1.0.0-preview"`. Currently advisory — not enforced. |
| `metadata` | `map` | Free-form authoring metadata: `authoring-style`, `origin`, `evidence-kinds`, etc. |

## Claude Code-only fields

Read by Claude Code's plugin runtime. GA's `SkillMdParser` ignores unmatched
properties (`IgnoreUnmatchedProperties()`), so these coexist without errors
on the GA side.

| Field | Type | Purpose |
| --- | --- | --- |
| `user-invocable` | `bool` | Whether the user can run the skill via slash command (`/<plugin>:<name>`). |
| `allowed-tools` | `string[]` | Permission allowlist for tools the skill may call. |

## Body

Markdown content after the closing `---`. Length is capped at
`MaxBodyCharacters` (32 KB) on the GA side to bound prompt injection
amplification. The body is injected verbatim as the system prompt for
`SkillMdDrivenSkill` and is presented to Claude when the skill is invoked
in Claude Code.

## Casing rule

**camelCase is canonical for the GA repo.** All in-tree SKILL.md files use
lowercase top-level keys (`name:`, `description:`, `triggers:`) — matching
Anthropic's published spec for Claude Code plugin skills. The 13 files
that originally used PascalCase were migrated by
`Scripts/normalize_skill_md_casing.py` (idempotent; runnable by any author
who pulls in an externally-authored PascalCase file).

The parser still **accepts** PascalCase as a fallback so externally-sourced
skills (e.g. an older copy of a GA skill someone pulled into another repo)
continue to load without an explicit migration step. Pick one casing per
file; mixing within a single file is unsupported (the chosen deserializer
wins, the other case's keys are silently dropped under
`IgnoreUnmatchedProperties`).

## Dual-compatibility example

Drop this file into either `skills/<name>/SKILL.md` (GA) **or**
`~/.claude/plugins/.../skills/<name>/SKILL.md` (Claude Code) — both
ecosystems load it.

```markdown
---
name: chord-info
description: |
  Returns interval recipe, MIDI numbers, common voicings, and inversions
  for a named chord. Use when the user asks "what is X" / "how do you spell X"
  / "intervals in X" for chord literals.
triggers:
  - "what is"
  - "how do you spell"
  - "intervals in"
  - "chord tones"
user-invocable: false
allowed-tools:
  - Read
license: internal
compatibility:
  agent-framework: ">=1.0.0-preview"
metadata:
  authoring-style: deterministic-catalog
  origin: "Common/GA.Business.ML/Agents/Skills/ChordInfoSkill.cs"
  evidence-kinds:
    - catalog_lookup
    - mcp_tool_call
---

# Chord info

You answer "what is X" questions about named chords. Output structure:

1. Spelling — root + interval recipe + accidentals
2. MIDI — concrete pitches in a default voicing
3. Common voicings — at most 3, ranked by playability
4. Inversions — root, 1st, 2nd (and 3rd if applicable)

Refuse to invent voicings outside the catalog.
```

The first six lines (`name`, `description`, `triggers`) are read by both
ecosystems. Everything else is GA-side metadata Claude Code skips, plus
two Claude-side fields (`user-invocable`, `allowed-tools`) GA skips.

## Limitations / known gaps

- **No live-reload on the GA side**: `SkillMdLoader` reads files at chatbot
  startup. Edits require a `GaChatbot.Api` restart to take effect. Tracking:
  no issue filed yet — would land as a `SkillMdReloadService` watcher.
- **No bidirectional discoverability**: Claude Code can invoke the GA chatbot
  via the `mcp__plugin_ga_ga-dsl__ask_chatbot` MCP tool, but each individual
  skill is **not** surfaced as its own tool/slash command. A future bridge
  could expose each skill as `/ga-skill:<name>` for fast iteration.
- **No shared eval harness**: regression tests live separately
  (`SkillMdParserTests` for GA, no equivalent on the Claude side). A shared
  golden-prompt runner would close the loop.

## Tests

The parity contract is pinned by the following test cases in
`Tests/Common/GA.Business.ML.Tests/Unit/SkillMdParserTests.cs`:

- `TryParseContent_LowercaseFrontmatter_ParsesIdenticallyToPascalCase`
- `TryParseContent_ClaudeCodeStyleMinimal_Parses`
- `TryParseContent_ClaudeStyleExtraFields_AreIgnored_NotErrors`
- `TryParseContent_PascalCaseStillTakesPrecedence_WhenBothPresent`
- `TryParseContent_NoNameInEitherCase_ReturnsNull`

Plus the existing PascalCase happy-path and edge-case tests, which still
pass unchanged to confirm backwards compatibility.

[claude-skill-spec]: https://docs.anthropic.com/claude-code/skills
