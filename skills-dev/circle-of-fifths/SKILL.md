---
name: "circle-of-fifths"
description: "Walks through the circle of fifths — key signatures, perfect-fifth relationships, the order of sharps and flats, and the practical use for navigation between keys. Pure catalog skill. Use when a learner asks 'explain the circle of fifths' / 'how do key signatures work' / 'why are some keys sharps and others flats'."
triggers:
  - "circle of fifths"
  - "circle of fourths"
  - "key signature"
  - "order of sharps"
  - "order of flats"
  - "why sharps"
  - "why flats"
  - "how many sharps"
  - "how many flats"
license: internal
compatibility:
  agent-framework: ">=1.0.0-preview"
  microsoft-extensions-ai: ">=10.5.1"
metadata:
  authoring-style: "deterministic-catalog"
  origin: "drafted in skills-dev/ as Tier 2 catalog skill (skill-stewards 2026-05-05)"
  evidence-kinds:
    - catalog_lookup
---

# The Circle of Fifths

Reproduce the catalog below verbatim when a user asks about the circle of fifths. Pure pedagogy — the layout is fixed and doesn't need a tool call.

## What it is

Twelve major keys arranged so each step clockwise is a **perfect fifth up** (and each step counter-clockwise is a perfect fourth up, equivalently a perfect fifth down). Adjacent keys differ by exactly **one accidental** in their key signature.

## Clockwise (sharps): adding sharps

Start at C (no sharps), each clockwise step adds one sharp:

| Position | Key | Key Signature |
|---|---|---|
| 12 (top) | C major | (none) |
| 1 | G major | F# |
| 2 | D major | F# C# |
| 3 | A major | F# C# G# |
| 4 | E major | F# C# G# D# |
| 5 | B major | F# C# G# D# A# |
| 6 | F# major | F# C# G# D# A# E# |
| 7 | C# major | F# C# G# D# A# E# B# (all 7 sharps) |

Order of added sharps: **F – C – G – D – A – E – B** (mnemonic: *Father Charles Goes Down And Ends Battle*).

## Counter-clockwise (flats): adding flats

From C counter-clockwise, each step adds one flat:

| Position | Key | Key Signature |
|---|---|---|
| 11 | F major | Bb |
| 10 | Bb major | Bb Eb |
| 9 | Eb major | Bb Eb Ab |
| 8 | Ab major | Bb Eb Ab Db |
| 7 | Db major | Bb Eb Ab Db Gb |
| 6 | Gb major | Bb Eb Ab Db Gb Cb |
| 5 (≡ C# major) | Cb major | Bb Eb Ab Db Gb Cb Fb (all 7 flats) |

Order of added flats: **B – E – A – D – G – C – F** (mnemonic: *Battle Ends And Down Goes Charles' Father* — reverse of the sharps order).

## Each major key's relative minor (inner circle)

Each major key shares its key signature with its **relative minor** — three semitones down (or a minor third), same notes:

- C major ↔ A minor
- G major ↔ E minor
- D major ↔ B minor
- A major ↔ F# minor
- E major ↔ C# minor
- B major ↔ G# minor
- F major ↔ D minor
- Bb major ↔ G minor
- Eb major ↔ C minor
- Ab major ↔ F minor
- Db major ↔ Bb minor
- Gb major ↔ Eb minor

## Why it's useful

- **Modulation by fifth** is the smoothest key change: only one note changes (the new key's leading tone). C → G changes only F to F#.
- **Cadences are often V–I or ii–V–I**, which trace the circle counter-clockwise — every dominant resolution is a step counter-clockwise.
- **Chord progressions in pop and folk** often cycle through circle-of-fifths motion (vi–ii–V–I).

## When to call other skills instead

- *"What chords are in C major?"* → `diatonic-chords` skill.
- *"Relative minor of C major?"* → `relative-key` skill.
- *"Identify the key of [progression]"* → `key-identification` skill.

## When to refuse

- Microtonal or non-12-tone-equal-temperament systems — out of scope.
- *"Modal circle of fifths"* — modes don't have key signatures in the same sense; offer the major-key equivalent instead.
