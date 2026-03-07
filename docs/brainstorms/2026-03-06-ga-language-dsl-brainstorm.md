---
date: 2026-03-06
topic: ga-language-dsl
---

# GA Language (GAL) — Brainstorm

## What We're Building

A composable, multi-modal language for Guitar Alchemist that serves **both human developers and AI agents simultaneously**, with the LSP as the bridge. The language replaces (subsumes) the existing `GA.Business.DSL` and `GaMusicTheoryLsp`, reimagining them with F# computation expressions as the foundation.

Primary use cases (co-equal priority):
1. **Data pipelines** — pull BSP rooms, run OPTIC-K embeddings, store in Qdrant, report failures
2. **Domain extension** — define new chord categories, tuning variants, technique taxonomies

Scripts live in **markdown files** as `ga` code blocks (literate programming style, inspired by TARS `.tars.md`). Claude naturally writes markdown; tools render it; blocks are executable.

Execution is **hybrid**: interpreted (FSI) for dev/Claude mode, compiled to F# for production pipelines.

## Why This Approach

- **Approach A** (F# dialect, FSI-hosted) is the immediate path — no custom parser, computation expressions are native F#, type-safe from day one. Markdown embedding + FSI execution works in weeks.
- **Approach B** (FParsec custom syntax, clean surface language) is the north star — surfaced as sugar over the CE core once usage patterns are well-understood.
- **Approach C** (MCP-first, language-second) was rejected as it foregoes the markdown/LSP story.

## Key Decisions

- **Both grammar and domain level for EBNF type providers**: one `.ebnf` file simultaneously extends the GA language's AST/parser (grammar level) and the F# domain type system (domain level). Agents write EBNF → types + parser combinators appear automatically, compile-time safe.
- **Closure Factory (from TARS v1)**: named, typed, discoverable F# closures (`GaClosure<'In,'Out>`) with categories (domain, pipeline, agent, IO). Adaptive memoization, performance tracking. GA already has `TarsGrammarAdapter` bridging TARS patterns.
- **`ga { }` computation expression**: monadic over `GaResult<'T>` (aligned with existing `GA.Core.Functional` ROP stack). `let!`, `do!`, `yield!` handle async + errors + agent fan-out transparently.
- **TARS .trsx node/edge graph** (`NODE "name" REASON|WORK op; EDGE "a" -> "b"`) as inspiration for the declarative pipeline/workflow syntax within `ga` blocks.
- **Extends existing infrastructure**: LSP extends `GaMusicTheoryLsp`, MCP tools extend `GaMcpServer`, computation expressions extend `GA.Core.Functional`.
- **Markdown-first**: scripts are `.md` files with ```ga blocks — readable, shareable, Claude-friendly, renderable by any markdown tool.

## Open Questions

- Erased vs. generative type provider: erased is simpler but types don't survive to IL; generative generates real .NET types. Recommendation: start with EBNF → F# source generator (build task), graduate to proper type provider.
- How does the LSP detect `ga` blocks in arbitrary `.md` files? (Register as `markdown` language server, intercept embedded language ranges.)
- Versioning of EBNF grammars — how are breaking changes to grammar surfaced without breaking existing pipelines?

## Next Steps

→ `/ce:plan` for implementation details
