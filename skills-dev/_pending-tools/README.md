# `skills-dev/_pending-tools/` — drafts blocked on missing MCP tools

These are SKILL.md drafts that **cannot be graduated** because the
deterministic MCP tools they reference don't exist yet in
`Common/GA.Business.ML/Agents/Mcp/*.cs`.

They live here as design specs for the future tool work. When a tool
lands, the corresponding draft becomes graduation-ready; rename
`DRAFT.md` → `SKILL.md` and run `skill-graduator`.

## Why DRAFT.md instead of SKILL.md

`SkillMdLoader` globs files named exactly `SKILL.md`. Renaming to
`DRAFT.md` makes these files **invisible** to the chatbot's
file-system watcher, so the dispatcher cannot route to a skill that
calls a non-existent tool. No risk of orchestrator crashes from the
fictional tool calls.

## Why these drafts exist

The skill-stewards 2026-05-05 batch (PR #118) authored 17 SKILL.md
candidates. Adversarial QA review found that 13 of them referenced
MCP tools that don't exist — `ga_transpose_chord`, `ga_diatonic_chords`,
`ga_voice_leading_pair`, etc. The cause was conflating the GA-DSL
plugin's MCP catalog (visible in Claude Code session reminders as
`mcp__plugin_ga_ga-dsl__ga_*`) with the chatbot's tool registry
(`Common/GA.Business.ML/Agents/Mcp/*.cs`). The plugin exposes ~30
tools to Claude Code; the chatbot has only 7. Conflating the two led
to the broken drafts.

The drafts are kept rather than deleted because they encode useful
design intent — argument shapes, prompt mappings, refusal rules, and
out-of-scope boundaries — that should inform the eventual tool
implementations.

## What would unblock each draft

| Draft | Tool needed | Notes |
| --- | --- | --- |
| `transpose` | `ga_transpose_chord` | Most-requested daily skill |
| `diatonic-chords` | `ga_diatonic_chords` | Pedagogy staple |
| `voice-leading` | `ga_voice_leading_pair` | Theory bread-and-butter |
| `relative-key` | `ga_relative_key` | Scale-info partially covers; could be a body-only catalog |
| `progression-analysis` | `ga_analyze_progression` | The headline skill — highest user-impact when shipped |
| `arpeggio` | `ga_arpeggio_suggestions` | Soloing context |
| `easier-voicings` | `ga_easier_voicings` | Beginner help |
| `voicing-search` | `ga_search_voicings_by_query` | Semantic search over OPTIC-K |
| `common-tones` | `ga_common_tones` | Foundational theory tool |
| `progression-generator` | `ga_generate_progression` | Creative composition |
| `polychord` | `ga_polychord` | Specialist atonal/jazz harmony |
| `set-class-subs` | `ga_set_class_subs` | Atonal theory |
| `icv-neighbors` | `ga_icv_neighbors` | Post-tonal similarity |

## Cross-reference

- Skill team blueprint: [`docs/plans/2026-05-05-skill-stewards-team.md`](../../docs/plans/2026-05-05-skill-stewards-team.md)
- Real MCP tool catalog: `Common/GA.Business.ML/Agents/Mcp/*.cs`
- Iteration loop architecture: [`skills-dev/README.md`](../README.md)
- Active drafts (no missing tools): the parent `skills-dev/` directory.
