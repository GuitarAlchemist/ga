---
name: "what-can-you-do"
description: "The chatbot's self-disclosure / capabilities meta-skill. Lists what the chatbot can do — chord/scale lookup, progression analysis, voicing search, transposition, voice leading, etc. — with one-line examples. Pure catalog. Use when a visitor asks 'what can you do' / 'what can the chatbot do' / 'help' / 'how do I use this'."
triggers:
  - "what can you do"
  - "what can the chatbot do"
  - "what does the chatbot do"
  - "how do i use"
  - "help"
  - "capabilities"
  - "show me what"
  - "what are you good for"
license: internal
compatibility:
  agent-framework: ">=1.0.0-preview"
  microsoft-extensions-ai: ">=10.5.1"
metadata:
  authoring-style: "deterministic-catalog"
  origin: "drafted in skills-dev/ as Tier 4 meta skill (skill-stewards 2026-05-05) — discoverability for the public chatbot"
  evidence-kinds:
    - catalog_lookup
---

# What the GA Chatbot Can Do

Reproduce this catalog verbatim when a visitor asks what the chatbot can do, what its capabilities are, or how to use it. This is the **discoverability skill** for the public surface at `https://demos.guitaralchemist.com/chatbot/`.

## One-line answer

> Guitar Alchemist's chatbot answers grounded music-theory questions — chord and scale lookup, progression analysis, voice leading, transposition, voicing search, and more. Every answer is computed from the GA symbolic engine, not recalled from training data.

## What you can ask

### 🎸 Chord and scale lookup
- *"What notes are in Cmaj7?"* — chord intervals and tones.
- *"What chords are in G major?"* — diatonic chords / harmonized scale.
- *"What are the modes of the major scale?"* — modal pedagogy.
- *"Notes in A natural minor"* — scale degrees.

### 🎼 Progressions
- *"Analyze C Am F G"* — Roman numerals, key, cadences, function.
- *"Identify the key of Em D C G"* — key inference.
- *"Generate a sad progression in D minor"* — composition.
- *"Suggest next chords for Cmaj7 Dm7 Em7"* — completion.

### 🎚️ Voicings (guitar-specific)
- *"Find me a mellow Cm9 voicing"* — natural-language voicing search.
- *"Easier voicing for Bbmaj7"* — beginner-friendly fingering.
- *"Beginner chords"* — starter open-position catalogue.

### 🔄 Movement & relationships
- *"Transpose Cmaj7 up a perfect fourth"* — transposition.
- *"Voice leading from Dm7 to G7"* — smooth connection.
- *"What's the relative minor of C major?"* — key relationships.
- *"Common tones between Cmaj7 and Am7"* — pivot tones.
- *"Substitutes for V7 in C major"* — chord substitutions.

### 🎼 Pedagogy
- *"Explain the circle of fifths"* — key signature walkthrough.
- *"How do I make this progression sound darker?"* — modal mood-shifting.
- *"What is voice leading?"* — concept explainers.

### 🧠 Advanced theory
- *"Set class of Cmaj7"* — pitch-class-set notation, Forte numbers.
- *"ICV neighbours of [0,3,6,9]"* — interval-class similarity.
- *"Polychord C/D"* — stacked-chord interpretation.

## How answers are produced

Every chord, scale, voicing, and progression answer is computed by **deterministic MCP tools** that call into the Guitar Alchemist symbolic engine. The chatbot doesn't recall theory from training data — it asks the engine and synthesizes the answer.

That means:
- Spellings are correct (Db major vs C# major won't get flipped).
- Voicings are from the curated OPTIC-K corpus (~1000s of indexed voicings, ranked by playability + harmonic fit).
- Roman numeral analysis is from set-class arithmetic, not pattern-matching.

## Things outside scope

- **Lyric writing / songwriting prompts** — not a creative-writing assistant.
- **Audio analysis** — can't listen to a recording. Send chord symbols or note names instead.
- **Rhythm / time-signature analysis** — currently chord-symbol level; rhythm is roadmap.
- **Production / mixing / EQ advice** — out of scope.

## When to call other skills directly

If you already know what you want, jump straight to it:
- Theory: `chord-info`, `scale-info`, `interval`, `modes`, `circle-of-fifths`.
- Progression: `progression-analysis`, `progression-completion`, `progression-mood`.
- Voicings: `voicing-search`, `easier-voicings`, `beginner-chords`.
- Movement: `transpose`, `voice-leading`, `relative-key`, `common-tones`.
- Substitutions: `chord-substitution`, `set-class-subs`.
- Composition: `progression-generator`, `arpeggio`.
