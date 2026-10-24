---
name: "functional-harmony"
description: "Explains how chords function and resolve in tonal harmony: the dominant seventh and its tendency tones, tritone substitution, secondary dominants, the Neapolitan chord, augmented sixth chords, and the cadence types (authentic, half, Phrygian half, plagal, deceptive). The body is a verified reference that grounds the answer; use it when a learner asks why a chord resolves, what a predominant or substitute chord is, or how cadences differ."
triggers:
  - "cadence"
  - "neapolitan"
  - "augmented sixth"
  - "italian sixth"
  - "french sixth"
  - "german sixth"
  - "tritone substitution"
  - "secondary dominant"
  - "dominant seventh"
  - "leading tone"
license: internal
compatibility:
  agent-framework: ">=1.0.0-preview"
  microsoft-extensions-ai: ">=10.5.1"
metadata:
  authoring-style: "reference-grounded"
  origin: "added 2026-10-03 after the theory QA eval found functional-harmony concept answers wrong on every LLM path"
last_verified: 2026-10-03
---

# Functional Harmony: Dominants, Predominants and Cadences

You answer questions about how chords function and resolve in tonal harmony. The reference below is verified: base your answer on it and never contradict it. Answer the question that was asked, in a few short paragraphs or a small table. Use the C major or C minor examples below unless the user names a key; for another key, move every note by the same interval and spell it with the letter that interval requires. Do not give fret numbers, tablature or chord diagrams; name the chords and their notes instead.

## Dominant seventh (V7) and its resolution

- V7 is built on scale degree 5 with a major 3rd, perfect 5th and minor 7th. In C major, G7 = G B D F (scale degrees 5, 7, 2, 4).
- Its 3rd and 7th form a tritone: B up to F is a diminished fifth (F up to B is an augmented fourth). That unstable interval is what wants to resolve.
- V7 → I in C (G7 → C):
  - the 3rd of V7 is the leading tone (degree 7): B rises a half step to C, the root of I;
  - the 7th of V7 (degree 4): F falls a half step to E, the 3rd of I;
  - so the tritone B–F contracts to the third C–E (voiced F below B, the augmented fourth expands to the sixth E–C);
  - the root G (a fifth above C) falls a fifth, or rises a fourth, to C in the bass; in an upper voice it is held as the common tone G;
  - the 5th, D, moves by step to C (or to E), or is left out.
- In a minor key, V7 takes the raised 7th degree of harmonic minor: in A minor, E7 = E G# B D, and G# rises to A.

## Tritone substitution

- Replace a dominant seventh with the dominant seventh whose root is a tritone away: G7 → Db7 (written bII7). In a ii–V–I in C: Dm7 – Db7 – Cmaj7.
- It works because both chords contain the same tritone. G7 has B (3rd) and F (7th); Db7 = Db F Ab Cb has F (3rd) and Cb (7th), and Cb is the same pitch as B. In each chord the 3rd up to the 7th is a diminished fifth: B–F in G7, F–Cb in Db7.
- The two pitches swap roles. Db7 → C: F, now the 3rd of Db7, falls a half step to E, the 3rd of C; Cb (B), now the 7th of Db7, rises a half step to C, the root of C; the bass slides down a half step, Db → C.
- The substitute replaces V on its way to I. It does not tonicize V.

## Secondary dominants

- A secondary dominant is the dominant (V or V7) of a chord other than I. V7/x is a major triad with a minor 7th whose root is a perfect fifth above the root of x, so it contains the leading tone of x.
- In C major:

| Target | Secondary dominant | Notes |
|---|---|---|
| ii (Dm) | V7/ii = A7 | A C# E G |
| iii (Em) | V7/iii = B7 | B D# F# A |
| IV (F) | V7/IV = C7 | C E G Bb |
| V (G) | V7/V = D7 | D F# A C |
| vi (Am) | V7/vi = E7 | E G# B D |

- vii° is not tonicized: a diminished triad cannot act as a temporary tonic.
- A secondary dominant resolves to its target the way V7 resolves to I: its 3rd rises a half step to the target's root and its 7th falls by step to the target's 3rd (D7 → G: F# → G, the root of G; C → B, the 3rd of G).

## The Neapolitan chord

- The Neapolitan is a major triad on the lowered second degree, bII. In C minor (or C major) it is Db F Ab: Db is a half step above the tonic C.
- It usually appears in first inversion, bII6, also written N6: the chord's 3rd (F, scale degree 4) is in the bass. The "6" is the figured-bass sign for first inversion.
- It is a predominant chord, like iv or ii°6, and moves to V, often through the cadential 6/4 (I6/4 → V).
- N6 → V in C minor (Db F Ab → G B D):
  - the bass F (degree 4) rises a step to G (degree 5), the root of V;
  - Db (b2) falls a diminished third (two half steps) to B, the 3rd of V and the leading tone (over a cadential 6/4 it passes through C: Db → C → B);
  - Ab (b6) falls a half step to G, the root of V.
- It is most common in minor keys. It is not the Phrygian half cadence, which is iv6 → V in minor.

## Augmented sixth chords

- Augmented sixth chords are predominants with the lowered sixth degree (b6) in the bass and the raised fourth degree (#4) above it; b6 up to #4 is an augmented sixth. In C, major or minor: Ab in the bass, F# above.

| Type | Scale degrees | Notes in C |
|---|---|---|
| Italian (It+6) | b6, 1, #4 | Ab C F# |
| French (Fr+6) | b6, 1, 2, #4 | Ab C D F# |
| German (Ger+6) | b6, 1, b3, #4 | Ab C Eb F# |

- Resolution: the augmented sixth expands outward to an octave on degree 5. Ab falls a half step to G and F# rises a half step to G, the root of V (G B D).
- The other voices:
  - Italian: the 1 (C) is doubled in four voices; one C falls a half step to B, the 3rd of V, the other rises a whole step to D, the 5th of V.
  - French: C falls a half step to B, the 3rd of V; D is held, as it is the 5th of V.
  - German: it usually goes to the cadential 6/4 first, because Ab–Eb moving straight to G–D would be parallel fifths. In C minor: Ab C Eb F# → G C Eb G (i6/4) → G B D (V); C and Eb are held, then each falls a half step, to B and D.
- The German sixth sounds like Ab7 (Ab C Eb Gb) but is spelled with F#, the raised 4th that rises to 5.

## Cadences

| Cadence | Progression | Ends on | Effect |
|---|---|---|---|
| Perfect authentic (PAC) | V(7) → I, both in root position, tonic in the top voice | I | Strongest close |
| Imperfect authentic (IAC) | V(7) → I with an inversion or with the 3rd or 5th in the top voice; also vii°6 → I | I | Closes, less final |
| Half (HC) | Any chord → V (I → V, ii → V, IV → V, iv6 → V) | V | Open, unfinished |
| Phrygian half | iv6 → V in minor, bass b6 → 5 (Ab → G in C minor) | V | Open |
| Plagal | IV → I, the "Amen" cadence; iv → i in minor | I (i in minor) | Soft close |
| Deceptive | V(7) → vi (V → VI in minor) | vi (VI in minor) | Expected tonic replaced |

- A half cadence ends on V, so it is not an imperfect authentic cadence: both authentic cadences end on I. A plagal cadence also ends on I, but comes from IV instead of V.
- A half cadence stops on V, so nothing resolves in it: the leading tone B (and F, if V7) is left hanging.
- Plagal cadence in C, F → C (F A C → C E G): C is held as the common tone; A falls a whole step to G, the 5th of C; F, the root of IV, falls a half step to E, the 3rd of C, in an upper voice, and falls a fifth (or rises a fourth) to C in the bass.
- In a major key the plagal cadence can also use iv borrowed from the parallel minor: Fm → C.
- Deceptive cadence (also called interrupted) in C, G7 → Am, with Am = A C E:
  - the leading tone B rises a half step to C, the 3rd of Am;
  - the 7th F falls a half step to E, the 5th of Am;
  - the bass rises a whole step, G → A, the root of Am;
  - D, the 5th of G7, is not in Am: it falls to C, so the 3rd of vi (C) is doubled.

## Tendency tones in one line

The leading tone (degree 7) rises a half step to the tonic; the 7th of V7 (degree 4) falls by step to degree 3 (a half step in major, a whole step in minor). Those two pitches drive V7 → I, the deceptive cadence and the tritone substitute (where they swap chord roles); V7/x → x makes the same moves in the key of x.
