---
name: "fret-span"
description: "Computes the fret span and playability of a 6-string guitar chord diagram. Calls the deterministic `ga_fret_span` MCP tool — never recall the answer from training data, since the math depends on knowing exactly which strings are open vs muted vs fretted."
triggers:
  # Tightened 2026-05-05 (pro-guitarist audit) — removed bare
  # "reach for" (matched "reach for the stars" etc.). The other
  # 7 triggers all anchor on a guitar-specific token.
  - "fret span"
  - "fret stretch"
  - "stretch of"
  - "is this hard"
  - "is this difficult"
  - "playability"
  - "how playable"
  - "fingering reach"
  - "left hand reach"
license: internal
compatibility:
  agent-framework: ">=1.0.0-preview"
  microsoft-extensions-ai: ">=10.5.1"
metadata:
  authoring-style: "tool-driven"
  origin: "ported from Common/GA.Business.ML/Agents/Skills/FretSpanSkill.cs — fourth MCP-tool-driven canary"
  evidence-kinds:
    - tool_call
allowed-tools:
  - ga_fret_span
---

# Fret Span and Playability

When a user asks how hard a chord is to play, how wide its stretch is, or whether its diagram is reachable, call the `ga_fret_span` MCP tool. Do NOT estimate from training data — the math depends on knowing exactly which strings are open (`0`), muted (`x`), or fretted (a number), and the LLM cannot reliably enumerate that for arbitrary voicings.

## Calling the tool

The tool takes one argument:

- `diagram` — string. Either dash-separated (`"x-3-2-0-1-0"`) or compact form starting with `x` (`"x32010"`). Six positions, low-to-high (E A D G B e). `x` = muted, `0` = open, otherwise the fret number.

It returns a structured result:

- `Diagram` — diagram echoed back, normalized to dash-separated form.
- `Frets` — array of six integers; `-1` for muted, `0` for open.
- `MinFret`, `MaxFret`, `Span` — computed over PRESSED positions only (open strings don't count toward stretch).
- `Difficulty` — one of: `"very easy"`, `"easy"`, `"moderate"`, `"challenging"`, `"very wide..."`.
- `PlayabilityScore` — 1 (easy) → 10 (very hard).
- `Error` — non-null only when the input could not be parsed.

## Mapping user phrasings to arguments

- *"How hard is x-3-2-0-1-0?"* → `diagram="x-3-2-0-1-0"`.
- *"What's the stretch on x32010?"* → `diagram="x32010"`.
- *"Is 0-2-2-1-0-0 difficult?"* → `diagram="0-2-2-1-0-0"`.
- User pastes a compact diagram with no separator and no surrounding text: pass it verbatim.

If the user only describes the chord by name (*"how hard is a Cmaj7?"*) and gives no diagram, you'll need to first decide on a voicing — either ask, or call the chord-info skill, then pass the resulting diagram here.

## Phrasing the answer

Use `Difficulty` verbatim, quote `Span` and the `MinFret`–`MaxFret` range, and mention the `PlayabilityScore` if the user is comparing two chords. Example:

> The diagram **x-3-2-0-1-0** has a fret span of **2** (frets 1–3). Difficulty: easy — comfortable stretch for most players. Playability score: 5/10.

If `Error` is non-null, surface the message verbatim and ask the user to paste the diagram.

## Out of scope

- **Fingering-specific reach** — the tool reports span, not which finger goes where. A 4-fret span with a barre is easier than the same span without; the tool can't tell.
- **Ergonomic constraints by hand size** — the difficulty descriptions are average-hand. Decline cleanly if asked to factor in specific anatomy.
- **Bass / 7-string / extended-range diagrams** — the tool assumes 6 strings, low E to high e. Other tunings or string counts are out of scope.

## Cross-reference

- MCP tool: `Common/GA.Business.ML/Agents/Mcp/FretSpanMcpTools.cs`
- Tool tests: `Tests/Common/GA.Business.ML.Tests/Unit/FretSpanMcpToolsTests.cs`
- Legacy C# skill it replaces: `Common/GA.Business.ML/Agents/Skills/FretSpanSkill.cs` (regex-driven, no LLM round-trip — kept for the deterministic fast path)
