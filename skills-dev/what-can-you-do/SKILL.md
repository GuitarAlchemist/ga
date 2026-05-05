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

## What you can ask today

The chatbot ships exactly the capabilities below. (See "Roadmap" for
items being designed but not yet wired — those will return a
"feature not yet available" response if asked today.)

### 🎸 Chord and scale lookup
- *"What notes are in Cmaj7?"* — chord intervals and tones.
- *"Notes in A natural minor"* — scale degrees.
- *"What are the modes of the major scale?"* — modal pedagogy (catalog).
- *"Show me beginner chords"* — open-position starter catalogue.

### 🎼 Progressions
- *"Identify the key of Em D C G"* — key inference.
- *"Suggest next chords for Cmaj7 Dm7 Em7"* — completion.
- *"How do I make this progression sound darker?"* — modal mood-shifting (catalog).

### 🎚️ Fingerings
- *"Compute the fret span of [diagram]"* — playability scoring.

### 🔄 Movement & relationships
- *"Compute the interval from C to G"* — interval pair-lookup.
- *"Substitutes for V7 in C major"* — chord substitutions (Grothendieck-ICV).
- *"Compare Cmaj7 with Am7"* — chord-pair classification.

### 🎼 Pedagogy
- *"Explain the circle of fifths"* — key signature walkthrough (catalog).

## How answers are produced

Every chord, scale, key, interval, fret-span, and substitution answer
is computed by **deterministic MCP tools** that call into the Guitar
Alchemist symbolic engine. The chatbot doesn't recall theory from
training data — it asks the engine and synthesizes the answer.

That means:
- Spellings are correct (Db major vs C# major won't get flipped).
- Roman numeral / function / cadence claims are from set-class arithmetic, not pattern-matching.

## Roadmap (not yet shipped)

These capabilities are **designed but not yet wired** — the chatbot
will not answer them correctly today. Drafts exist at
`skills-dev/_pending-tools/` awaiting their backing MCP tools:

- Transpose chord by interval.
- List diatonic chords of a key (currently routed indirectly via scale-info).
- Voice leading between two chords.
- Relative / parallel key lookup.
- Full progression analysis (Roman numerals + cadences + modulations).
- Voicing search by natural-language query (semantic over OPTIC-K).
- Easier-voicings substitution.
- Arpeggio suggestions for soloing.
- Common-tone identification.
- Progression generation by mood / style.
- Polychord analysis.
- Set-class substitutions and ICV-neighbour lookup.

If you ask one of those today, the chatbot may attempt to answer from
training-data fallback — flag the answer as **non-authoritative** and
verify against a textbook.

## Things outside scope

- **Lyric writing / songwriting prompts** — not a creative-writing assistant.
- **Audio analysis** — can't listen to a recording. Send chord symbols or note names instead.
- **Rhythm / time-signature analysis** — currently chord-symbol level; rhythm is roadmap.
- **Production / mixing / EQ advice** — out of scope.

## When to call other skills directly

If you already know what you want, jump straight to it. **Live skills
today** (canonical, in `skills/`):
- Theory: `chord-info`, `scale-info`, `interval`, `modes`, `circle-of-fifths`.
- Progression: `progression-completion`, `progression-mood`, `key-identification`.
- Fingerings: `fret-span`, `beginner-chords`.
- Substitutions: `chord-substitution`.
