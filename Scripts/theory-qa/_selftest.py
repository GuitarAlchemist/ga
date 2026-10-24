"""Offline grader check: the 12 earlier answers (known verdicts) plus hand-written correct answers,
which must all pass (a correct answer must never fail), known-wrong answers, which must fail, and
tab_check shapes both ways. Exit 1 on any false fail or false pass."""
import json
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).parent))
import tabcheck  # noqa: E402
from run_eval import grade, normalize  # noqa: E402

Q = {q["q"]: q for q in json.loads(Path(__file__).with_name("questions.json").read_text(encoding="utf-8"))["questions"]}
QID = {q["id"]: q for q in Q.values()}

print("--- earlier answers (fixtures/known-answers.jsonl)")
for line in Path(__file__).parent.joinpath("fixtures", "known-answers.jsonl").read_text(encoding="utf-8").splitlines():
    r = json.loads(line)
    f = grade(r["answer"], Q[r["q"]])
    print(("PASS" if not f else "FAIL"), Q[r["q"]]["id"],
          [(x["kind"], x.get("hit") or x.get("claim") or x.get("pattern", "")[:60]) for x in f])

GOOD = {
    "chord-bbmaj7": "A **B♭maj7** contains B♭, D, F and A (1, 3, 5, 7).",
    "chord-fsharp-m7b5": "F#m7b5 (half-diminished) is spelled F# – A – C – E.",
    "chord-db7": "D♭7 = D♭ F A♭ C♭ (the C♭ sounds like B).",
    "chord-ebm9": "E♭m9: E♭, G♭, B♭, D♭, F.",
    "chord-csharp-dim7": "C#dim7 is C#, E, G, B♭ — the B♭ is a diminished seventh above C#.",
    "interval-c-ab": "C up to A♭ is a minor 6th (8 semitones).",
    "interval-b-f": "B up to F is a diminished fifth — a tritone.",
    "key-a-major-sharps": "A major has three sharps: F#, C# and G#.",
    "key-relative-minor-e": "The relative minor of E major is C♯ minor.",
    "key-relative-major-gm": "The relative major of G minor is B♭ major (two flats).",
    "key-four-flats": "A♭ major has four flats: B♭, E♭, A♭, D♭.",
    "diatonic-g-major": "G major: G, Am, Bm, C, D, Em and F#°.",
    "diatonic-d-minor": "D minor: Dm, E°, F, Gm, Am, B♭, C (use A7 for a stronger V).",
    "scale-a-minor-pentatonic": "A minor pentatonic: A, C, D, E, G.",
    "mode-dorian-vs-aeolian": "Dorian has a natural 6th; Aeolian (natural minor) has a ♭6. Everything else is the same.",
    "scale-a-harmonic-minor": "A harmonic minor: A B C D E F G♯.",
    "scale-c-blues": "C blues: C, E♭, F, G♭, G, B♭.",
    "scale-melodic-minor-formula": "Ascending melodic minor: 1 – 2 – ♭3 – 4 – 5 – 6 – 7.",
    "mode-e-mixolydian": "E Mixolydian: E F# G# A B C# D.",
    "scale-harmonic-minor-why": "It raises the 7th to create a leading tone, so the V chord becomes major (E7 in A minor).",
    "harmony-v7-resolve": "G7 contains a tritone (B–F). The leading tone B rises to C, the root of I; the seventh F falls to E, the third of I.",
    "harmony-tritone-sub": "Replace G7 with D♭7, the dominant a tritone away (♭II7). Both share the tritone B/C♭–F.",
    "harmony-secondary-dominant": "V of V in C is D7 (D F# A C), which leads to G.",
    "harmony-neapolitan": "The Neapolitan is a major triad on ♭II, usually in first inversion (N6); it is a predominant that goes to V.",
    "harmony-augmented-sixth": "Italian, French and German sixths put ♭6 and ♯4 an augmented sixth apart; they expand outward to the octave on 5, the dominant (V).",
    "harmony-ii-v-i-bb": "In B♭: Cm7 – F7 – B♭maj7.",
    "cadence-half-vs-plagal": "A half cadence stops on V; a plagal cadence is IV → I (the Amen cadence).",
    "cadence-deceptive": "A deceptive cadence goes V → vi instead of V → I.",
    "voice-leading-g7-c": "B (the leading tone) goes up to C, and F (the seventh) goes down to E.",
    "guitar-standard-tuning": "Low to high: E (6th string), A, D, G, B, E (1st string).",
    "guitar-low-e-fret-5": "The 5th fret of the low E string is A — the same as the open A string.",
    "guitar-g-string-fret-7": "7th fret on the G string is a D.",
    "guitar-caged": "CAGED uses the five open chord shapes C, A, G, E and D, moved up the neck.",
    "guitar-c-barre-a-shape": "Barre the 3rd fret with the A-shape: x35553.",
}
print("--- hand-written correct answers (all must PASS)")
bad = 0
for qid, ans in GOOD.items():
    f = grade(ans, QID[qid])
    if f:
        bad += 1
        print("FALSE FAIL", qid, f)
BAD = {  # known-wrong answers seen in production; each must FAIL
    "harmony-v7-resolve": "Major third – a half‑step below the tonic (the leading tone). The interval between the 3rd "
                          "(♭3 of V⁷) and the 7th (♭7 of V⁷) is a tritone.\n   - ♭3 (the leading tone) → 3rd of I "
                          "(a half‑step up)\n   - ♭7 → 5th of I (a half‑step down)",
}
false_pass = 0
for qid, ans in BAD.items():
    f = grade(ans, QID[qid])
    false_pass += not f
    print(("ok, fails" if f else "FALSE PASS"), qid, [(x["kind"], x.get("hit")) for x in f])
missing = set(QID) - set(GOOD)
print(f"{len(GOOD) - bad}/{len(GOOD)} correct answers pass; ids without a sample: {sorted(missing)}")

print("--- tab_check (standard tuning)")
TAB_GOOD = [  # right shapes and claims, each must pass
    "G7 (3-2-0-0-0-1)", "C x32010", "Bb x13331", "D xx0232", "F7 131211", "A string fret 3 = C",
    "C major (0-1-0-2-3-x)",  # written high e first, as GA stores diagrams
    "Bb 1-3-3-3-1-1",  # wrong low E first, but a real Bb/F read high e first: not a confident mismatch
    "Bm x20202",  # Bm7 grip under a Bm label: tolerated colour tone
    "| Cm7 (ii) | x 3 5 3 4 3 | C - Eb - G - Bb |",
    "| C | x-4-3-1-x-x (Db-F-Ab) |",  # bare first cell is the key column, not the chord
    "Root (C) - 5th string, 3rd fret",
    "Common voicing for D7 (open shape)\n\nvextab\ntabstave\nnotes :w (0/4.2/3.1/2.2/1)",
    "G7 (open shape)\n\ne|-1-\nB|-0-\nG|-0-\nD|-0-\nA|-2-\nE|-3-\n",
    "A common open-position voicing is x13231:\n\nvextab\ntabstave\nnotes :w (1/5.3/4.2/3.3/2.1/1)",
    "Rootless C9 x-x-2-3-3-3 (C E G Bb D)",  # lists the omitted root: one-way mismatch only
    "notes :w (3/6.2/5.0/4.0/3.0/2.1/1)  // G7: 3-2-0-0-0-1",
]
TAB_BAD = [  # wrong shapes and claims seen in answers, each must fail
    "Edim 7-10-11-9-8-7", "Db7 x45464", "D x-0-2-3-2-0", "B string fret 2 = B",
    "Bb x13321",
    "| F7 (V) | 1 x 2 2 1 3 | F - A - C - Eb |",
    "x-5-3-2-1-x (F-Ab-Db)",
    "Major 3rd (E) - 4th string, 5th fret",
    "- 4th string 1 = C",
    "Root E is on the 5th fret of the A string",
    "String 1 2nd fret = E",
    "#### Full-root voicing (Db7)\n\nx 4 5 4 6 4\n  ^ ^ ^ ^ ^\n  5 4 3 2 1 (strings)",
    "F7\n\nvextab\ntabstave\nnotes :w (1/6.2/4.2/3.1/2.3/1)",
    "notes :q (3/6.2/5.0/4.0/3.0/2)  // G7: root-less",
]
tab_bad = 0
for ans in TAB_GOOD:
    f = tabcheck.check(normalize(ans))
    if f:
        tab_bad += 1
        print("FALSE FAIL", repr(ans), f)
for ans in TAB_BAD:
    f = tabcheck.check(normalize(ans))
    if not f:
        false_pass += 1
        print("FALSE PASS", repr(ans))
print(f"tab_check: {len(TAB_GOOD) - tab_bad}/{len(TAB_GOOD)} right shapes pass, "
      f"{len(TAB_BAD) - sum(not tabcheck.check(normalize(a)) for a in TAB_BAD)}/{len(TAB_BAD)} wrong shapes fail")
sys.exit(1 if bad or tab_bad or false_pass else 0)
