---
name: "Domain Ontology Improver"
description: "Specialized agent for continuously evaluating, enriching, and expanding the Guitar Alchemist domain ontology (chords, voicings, scales, and metadata) to improve AI/RAG performance over time."
---

# Domain Ontology Improver

This skill acts as the "Librarian and Data Scientist" of the Guitar Alchemist domain. Its primary responsibility is to iteratively enhance the metadata, semantic context, and physical accuracy of all musical objects (especially Fretboard Chords/Voicings).

## Core Responsibilities

1. **Semantic Enrichment**: Ensure every chord and voicing has rich, descriptive metadata that an LLM can use for RAG (Retrieval-Augmented Generation).
2. **Physical Feasibility Verification**: Evaluate voicings for actual human playability on a guitar.
3. **Historical & Contextual Mapping**: Connect chords to specific genres, eras, or iconic songs.
4. **Data Deduplication & Canonization**: Identify redundant or functionally identical voicings and canonicalize them (e.g., using Prime Forms).

## Continuous Improvement Workflow (The "Compound" Loop)

Whenever you are tasked with "improving the domain" or analyzing chord data, follow this cycle:

### 1. Identify Metadata Gaps
- Are there chords with generic or missing `SemanticTags`?
- Are `Difficulty` ratings ("Beginner", "Advanced", etc.) missing or inaccurate based on fret span?
- Do we lack functional context (e.g., "common in jazz ii-V-I", "neo-soul flavor")?

### 2. Physical & Psychoacoustic Re-Evaluation
- Use `VoicingPhysicalAnalyzer` concepts to verify if a grip requires an impossible stretch (e.g., > 5 frets without open strings).
- Check the `PerceptualQualities` (Consonance/Dissonance). If a voicing is highly dissonant, tag it appropriately (e.g., "tension", "atonal", "passing chord").

### 3. Systematic Update Process
When proposing updates to the domain models or the database:
- **Don't just change one chord**: Look for the systemic pattern. If `Cmaj9` is missing a tag, do all `maj9` chords need that tag?
- **Update the generator/analyzer**: The preferred way to fix data is to update the C# logic in `VoicingAnalyzer.cs` or `ChordTemplateFactory.cs` so it applies globally, rather than patching a single JSON/database record.

## Key Metadata Categories to Enforce

When reviewing or generating data, ensure these categories are populated:
- **Functional**: Root, Quality, Extension, Inversion.
- **Physical**: FretSpan, MutedStrings, OpenStrings, Difficulty, Fingerings.
- **Perceptual**: Consonance, Register (High/Low/Muddy), Spread (Close/Drop-2/Drop-3).
- **Stylistic/Contextual**: Associated Genres (Jazz, Djent, Classical, Folk), Mood/Emotion (Bright, Dark, Melancholy, Tense).

## Example Operation: "Audit the Diminished 7th Voicings"
If asked to improve dim7 chords:
1. Locate how dim7s are generated.
2. Check their current semantic tags.
3. Propose adding tags like `"symmetric"`, `"tension"`, `"dominant-substitution"`.
4. Check if the physical generator is producing unplayable stacked 3rds and recommend filtering them in favor of Drop-2 voicings (which are standard for dim7 on guitar).
