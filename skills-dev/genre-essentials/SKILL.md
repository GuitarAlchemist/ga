---
name: "genre-essentials"
description: "Explains what makes a genre sound like itself — the harmonic, melodic, rhythmic, and timbral signatures of blues, jazz, pop, modal, folk, and others. Pure catalog skill. Use when a learner asks 'what makes blues blues' / 'what defines jazz harmony' / 'how do I write a country progression'."
triggers:
  - "what makes blues"
  - "what makes jazz"
  - "what defines"
  - "blues progression"
  - "jazz progression"
  - "country progression"
  - "modal sound"
  - "pop progression"
  - "folk chord"
  - "genre essentials"
license: internal
compatibility:
  agent-framework: ">=1.0.0-preview"
  microsoft-extensions-ai: ">=10.5.1"
metadata:
  authoring-style: "deterministic-catalog"
  origin: "drafted in skills-dev/ as Tier 4 catalog skill (skill-stewards 2026-05-05)"
  evidence-kinds:
    - catalog_lookup
---

# Genre Essentials — What Makes Each Genre Sound Like Itself

Catalog skill. When a user asks what defines a genre's sound, match to one of the templates below. Each template lists the harmonic / melodic / rhythmic / timbral signatures so the answer covers more than just "play these chords".

## 🎷 Blues

**Harmony**: 12-bar blues form (I7 – I7 – I7 – I7 – IV7 – IV7 – I7 – I7 – V7 – IV7 – I7 – V7), all dominant-7 chords (no triads, no major-7). Quick-change variant adds IV7 in bar 2.
**Melody**: blues scale (1, b3, 4, b5, 5, b7), heavy use of bent and slid notes, b3-and-3 ambiguity.
**Rhythm**: shuffle / swing eighth notes, often a strong backbeat on 2 and 4.
**Timbral**: tube-amp grit, slide guitar, harmonica, expressive vibrato.

## 🎺 Jazz

**Harmony**: extended chords (9ths, 11ths, 13ths, alterations), tritone substitution, backdoor cadences, modal interchange. ii-V-I is the signature cadence (replace V-I with ii-V-I anywhere). Heavy use of secondary dominants (V7/ii, V7/V).
**Melody**: chromatic approach tones, target chord tones on strong beats, "out" playing over chord changes.
**Rhythm**: swung eighth notes (long-short triplet feel), syncopation, phrase ends on weak beats.
**Timbral**: hollow-body / archtop guitar, walking bass, brushed drums, muted brass.

## 🎵 Pop

**Harmony**: 4-chord loops are king — *axis* (I-V-vi-IV) is the most-recorded progression of the 21st century. Diatonic only; minimal chromaticism. Borrowed iv from parallel minor adds emotional lift.
**Melody**: hooks that fit the chord changes; minimal range, repeatable rhythms.
**Rhythm**: 4/4, strong backbeat, quantized to 16ths.
**Timbral**: clean tones, layered synths or guitars, tight low end.

## 🎻 Folk / singer-songwriter

**Harmony**: open-position chords (C, G, D, A, Em, Am, Dm), modal-mixture borrowed chords (bVII, bVI). Often capo-driven so the same shapes work in many keys.
**Melody**: pentatonic, often vocal; melodic ornaments on phrase ends.
**Rhythm**: 4/4 or 6/8 (waltz), strummed or fingerpicked.
**Timbral**: acoustic guitar, banjo, mandolin, fiddle — natural / unprocessed.

## 🌀 Modal (jazz-modal, post-bop, Norah Jones-y)

**Harmony**: STATIC harmony — one chord (or two) for many bars. Vamps in Dorian, Phrygian, Lydian. Avoid V-I cadences (they imply tonal motion).
**Melody**: emphasises modal characteristic intervals — Dorian's natural 6, Lydian's #4, Phrygian's b2.
**Rhythm**: often spacious, less rhythmic urgency than tonal jazz.
**Timbral**: open voicings (quartal stacks), pedal-tone bass.

## 🤠 Country

**Harmony**: I-IV-V dominates, with vi as the relative-minor variation. Often quick changes between I, IV, V; dominant-7 versions of each are common.
**Melody**: pentatonic with chromatic passing tones; pedal-steel-style bends.
**Rhythm**: 2-feel ("boom-chick" alternating bass), shuffle in honky-tonk subgenre.
**Timbral**: acoustic + Telecaster, pedal steel, fiddle, twangy reverb.

## 🎸 Rock (classic / hard / prog)

**Harmony**: I-bVII-IV ("mixolydian rock") and i-bIII-bVII (minor) common. Power chords (root + fifth, no third). Modal-mixture borrowing prevalent.
**Melody**: minor pentatonic + blues scale; Phrygian for darker subgenres.
**Rhythm**: 4/4 with strong backbeat; often syncopated guitar parts.
**Timbral**: distorted electric guitar, big drums, prominent bass.

## When to call other skills

- *"What chords are in [genre] in key X?"* → `diatonic-chords` first, then map genre constraints.
- *"Generate a [genre] progression"* → `progression-generator` with `style=` set.
- *"Voicing for jazz"* → `voicing-search` with `"jazz"` in the query.
- *"Modal harmony"* deeper dive → `modes` skill.

## When to refuse / clarify

- Highly subgenre-specific requests (synthwave, vaporwave, math rock) — pick the parent genre and note that subgenre adds further constraints (often timbral / production rather than harmonic).
- *"What's the BEST genre"* — refuse the value judgment; offer the descriptive comparison instead.

## Out of scope

- **Production / mixing** advice — out of scope.
- **Lyric / song-form** advice — out of scope.
- **Detailed historical evolution** — defer to dedicated music-history sources.
