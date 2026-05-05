---
name: "key-identification"
description: "Identifies the most likely musical key of a chord progression and explains why. Calls the deterministic `ga_key_identify` MCP tool — never recall the analysis from training data, since the diatonic-set match is pitch-class arithmetic and the LLM is unreliable at it."
triggers:
  - "what key is"
  - "what key does"
  - "what key are"
  - "identify the key"
  - "key of these"
  - "key of the"
  - "key is this"
  - "what key am i"
  - "diatonic to"
license: internal
compatibility:
  agent-framework: ">=1.0.0-preview"
  microsoft-extensions-ai: ">=10.5.1"
metadata:
  authoring-style: "tool-driven"
  origin: "ported from Common/GA.Business.ML/Agents/Skills/KeyIdentificationSkill.cs — sixth MCP-tool-driven canary; first hybrid port (deterministic detection + LLM phrasing)"
  evidence-kinds:
    - tool_call
allowed-tools:
  - ga_key_identify
---

# Key Identification

When a user asks what key a chord progression is in, call `ga_key_identify`. Do NOT analyze the progression from training knowledge — the diatonic-set match is pitch-class arithmetic and the LLM is unreliable at it.

## Calling the tool

The tool takes one argument:

- `query` — string. Either a bare chord list (`"C Am F G"`) or the user's full question (`"what key is C Am F G in?"`). The tool extracts chord symbols from prose.

It returns a structured result:

- `RecognizedChords` — string array, what the parser actually saw (useful for catching typos).
- `TopCandidates` — array of `KeyCandidateInfo`. Keys tied at the highest match count.
- `PartialMatches` — up to 3 partial-match keys ranked behind the top set.
- `TotalChords` — total chords parsed.
- `Error` — non-null when no chords parsed or no candidates matched.

Each `KeyCandidateInfo` has:

- `Key` — canonical key name (e.g. `"C major"`, `"A minor"`).
- `RelativeKey` — name of the relative pair.
- `MatchCount` / `TotalChords` — N of M chords are diatonic in this key.
- `DiatonicSet` — the seven diatonic chords (`I` through `vii°`) in order.

## Phrasing the answer

Your job is to explain the key choice in guitarist-friendly language. Use the structured tool result; do NOT add analysis the tool didn't compute.

### Single top candidate

When `TopCandidates` has exactly one entry: lead with the key name and the match score, give the Roman numeral function of each input chord, and mention the diatonic set.

> The progression `Dm G C` is in **C major** (3/3 chords diatonic). In C major: Dm = ii, G = V, C = I — a classic ii-V-I cadence. The diatonic chords of C major are C – Dm – Em – F – G – Am – B°.

### Tied candidates (relative-pair ambiguity)

When `TopCandidates` has multiple entries (almost always 2 — the relative major/minor pair share a diatonic set): name both, then explain how to distinguish them by ear.

> The progression `C Am F G` fits **C major** AND **A minor** equally — both contain all four chords. To tell them apart, listen for the tonic: if the progression keeps resolving to **C** as home, it's C major; if **Am** feels like home, it's A minor. The relative-key pair shares the diatonic set C – Dm – Em – F – G – Am – B°.

### Partial matches

When `PartialMatches` has entries, optionally mention 1–2 of them with their match score: *"Could also be partly heard as G major (3/4 diatonic — the F is the borrowed chord)."*

### Error path

If `Error` is non-null, surface the message verbatim and ask the user to write the chords as standard symbols.

## What this skill does NOT do

These are hard constraints. Do NOT compute these from this catalog alone.

- **Modal analysis** — modes (Dorian, Phrygian, etc.) are out of scope. The tool only ranks major/minor keys. If the user asks about modal context, defer.
- **Chord-quality details beyond major/minor/7ths** — the tool's diatonic match doesn't distinguish a `Cmaj7` from a `C` for the purposes of key fitting.
- **Recommend chords to ADD to the progression** — the tool detects a key from existing chords; it does not suggest continuations. For "what comes next?" defer to the progression-completion skill (when implemented).

## Cross-reference

- MCP tool: `Common/GA.Business.ML/Agents/Mcp/KeyIdentificationMcpTools.cs`
- Tool tests: `Tests/Common/GA.Business.ML.Tests/Unit/KeyIdentificationMcpToolsTests.cs`
- Domain service: `Common/GA.Business.ML/Agents/KeyIdentificationService.cs`
- Legacy C# skill it replaces: `Common/GA.Business.ML/Agents/Skills/KeyIdentificationSkill.cs` (regex-driven + LLM phrasing — kept for the deterministic fast path)
