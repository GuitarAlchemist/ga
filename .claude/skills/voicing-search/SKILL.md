---
name: voicing-search
description: "Search the OPTIC-K guitar voicing index by chord, mode, or style. Returns top-K matching voicings from a 313k-voicing mmap corpus using partition-weighted musical cosine similarity — not text embeddings. Invoke when the user asks to find, suggest, or list voicings for a chord (Cmaj7, F#m7b5), mode (Lydian, Dorian), or tag (drop2, shell, jazz, rootless)."
metadata:
  domain: music
  triggers: voicing, chord voicing, find voicings, suggest voicings, OPTIC-K, optick, drop2, shell voicing, rootless, barre, chord search
  role: retrieval
  scope: music-theory
  output-format: structured
---

# OPTIC-K Voicing Search

Retrieval over a 313,000-voicing corpus using **musical geometry**, not text embeddings. The index is a pre-built 112-dim OPTK v4 mmap file; similarity is weighted partition cosine across STRUCTURE (pitch-class invariants), MORPHOLOGY (fretboard geometry), CONTEXT (harmonic function), SYMBOLIC (technique/style tags), and MODAL (mode flavor) partitions.

## When to Use

- User names a chord and wants playable fingerings: "show me Cmaj7 voicings", "drop-2 Dm7", "rootless G7 shapes".
- User asks for voicings in a mode or style: "Lydian on guitar", "jazz voicings", "shell voicings".
- User wants similar-sounding alternatives: "voicings with a similar quality to F#m7b5".

## When NOT to Use

- Pure music theory questions ("what's a Lydian mode?") — use `TheoryAgent` or the `GaScaleByName` tool.
- Chord progression analysis ("what key is C-F-G?") — use `GaKeyFromProgression`.
- Purely descriptive queries without any chord/mode/tag ("something warm and dreamy"). The typed extractor returns empty; search yields arbitrary results. Ask the user to name a chord or style first.

## How to Call

**Before the first search this session, call `ga_voicing_vocabulary` once** — it returns the canonical lists of modes, chord-quality suffixes, roots, and style/technique tags that the search tool understands. Cache the result for the rest of the session. When the user's query uses descriptive language ("warm", "jazzy", "moody") or non-canonical phrasing, map their words to the returned tags *client-side* before calling search. That keeps retrieval fully deterministic — the search tool has zero LLM dependency.

Example flow:
- User: "something jazzy in F minor for a ballad"
- You call `ga_voicing_vocabulary` once (if not yet cached). See "jazz" is a canonical tag.
- You formulate `ga_search_voicings { query: "Fm jazz" }` — canonical tokens only.
- Retrieval returns Fm voicings ranked by OPTIC-K musical geometry + jazz-tag boost.

**Canonical-form gotchas found in live testing (2026-04-18):**
- `drop2` does NOT match — use `drop-2-voicings` (dashes, plural). Same for `drop-3-voicings`.
- `shell` DOES substring-match `shell-voicing` — partial match works on this one.
- `rootless` is its own canonical tag (not `rootless-voicing`).
- Register hints like `register:high`, `register:mid`, `register:low` exist and are useful for "voicings that sit high on the neck" type queries.
- Many mood words ARE canonical: `dreamy`, `melancholy`, `bright`, `soulful`, `aggressive`, `tense`, `resonant`, `stable`, `sad`. Call `ga_voicing_vocabulary` once to see the full list before guessing.
- Famous-chord names are indexed: `hendrix-chord`, `mystic-chord`, `petrushka-chord`, `james-bond-chord`, `blackbird-chord`, `tristan-chord`, etc.

**Known corpus-side limitation (tracked 2026-04-18 diagnostics):**
Adding style tags like `jazz` to a chord query currently *lowers* the score vs. the bare chord query. Reason: the top-ranked corpus voicings for common chords don't have style bits set in their SYMBOLIC partition, so adding the tag introduces zero-cosine contribution that drags the weighted total down. This is a corpus-write-path issue (`OptickIndexWriter` / SYMBOLIC tag density), not a retrieval bug. Workaround until the index is rebuilt: prefer chord + technique tags (`drop-2-voicings`, `shell-voicing`) over chord + style tags (`jazz`, `blues`), OR call with chord alone and mention style in the conversational wrapper.

Primary MCP tool: **`ga_search_voicings`** (exposed by the `ga` MCP server).

```json
{
  "query": "Cmaj7 drop2 jazz",
  "limit": 10,
  "instrument": "guitar"
}
```

Parameters:
- `query` (required): natural-language string. The server extracts chord symbols, mode names, and technique/style tags.
- `limit` (optional, default 10, max 50): top-K.
- `instrument` (optional): `"guitar"` | `"bass"` | `"ukulele"`. Filters to that slice of the mmap.

Secondary tool: **`ga_voicing_index_info`** — liveness + count check.

## Output Shape

```json
{
  "query": "Cmaj7 drop2 jazz",
  "interpreted": {
    "chord": "Cmaj7",
    "rootPitchClass": 0,
    "pitchClasses": [0, 4, 7, 11],
    "mode": null,
    "tags": ["drop2", "jazz"]
  },
  "resultCount": 10,
  "results": [
    {
      "score": 0.9831,
      "diagram": "x-3-2-0-0-0",
      "chordName": "Cmaj7",
      "instrument": "guitar",
      "midiNotes": [48, 52, 55, 59, 64],
      "pitchClasses": [0, 4, 7, 11]
    }
  ]
}
```

The `interpreted` block is the reverse-engineered query — use it to confirm the parser understood what the user wanted. If `interpreted` is mostly null and `resultCount` is 0, the typed extractor couldn't find a chord/mode/tag and should prompt the user to rephrase.

## Why Scores Cluster 0.4–0.98

The underlying cosine is partition-weighted: STRUCTURE (0.45) + MORPHOLOGY (0.25) + CONTEXT (0.20) + SYMBOLIC (0.10) + MODAL (0.10). A perfect match on STRUCTURE + MODAL alone already yields ~0.55. Near-perfect matches across all partitions approach 1.0.

Meaningful score spread across top-20 (> 0.005) distinguishes OPTIC-K from the previous text-embedding path, which clustered at 0.489 ± 0.002.

## Composition With Other Tools

- Pair with `GaParseChord` to pre-validate the user's chord input before searching.
- Pair with `GaChordSubstitutions` to search for voicings of each substitute chord.
- Use `ga_generate_voicing_embedding` (separate tool) if you need the raw 228-dim vector for a specific voicing.

## Limitations

- No cloud LLM fallback in the MCP path. Fuzzy queries ("warm", "sparkly") return empty unless the word is in the symbolic tag vocabulary — which is why you should call `ga_voicing_vocabulary` first and translate the user's words to canonical tags yourself.
- `FindSimilarVoicingsAsync` by voicing-id is not implemented — the OPTK v4 format indexes by position, not by id.
- First query after server start incurs a ~30–400 ms mmap cold-cache penalty on a fresh page cache (mitigated by `PrefetchVirtualMemory` at startup on Windows).
