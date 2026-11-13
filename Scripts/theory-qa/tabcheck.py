"""Automatic tablature check for theory-qa: decode the guitar shapes an answer ties to a chord or a note,
in standard tuning (E A D G B E), and report confident mismatches.

It runs on run_eval.normalize()'d text and checks four kinds of claim:
  1. a six-string diagram attached to a chord label: "G7 (3-2-0-0-0-1)", "Bb x13331",
     "x02210 (open Am)", "F7 x 8 10 8 9 8" (low E first, as chord charts write them), a table row
     "| F7 (V) | 1 x 2 2 1 3 |", or a diagram alone on its line under a label line;
  2. a six-string diagram followed by the notes it claims to sound: "x-5-3-2-1-x (F-Ab-Db)";
  3. a VexTab chord "(3/6.2/5.0/4.0/3.0/2)" (fret/string, string 1 = high e), or a six-line ASCII chord
     block (e|---1--- ... E|---3---), labelled by a "%" or "//" comment on its line, by agreeing labels
     on the same line, or by the nearest label line above (blank, vextab/tabstave and diagram lines
     are skipped);
  4. a stated note at a string and fret: "5th string, 5th fret (C)", "string 1 fret 2 = E",
     "String 3 2nd fret = A", "B string fret 2 = B", "E = 4th string 5th fret", "B (2nd string, 2nd fret)",
     "4th string 1 = C", "2nd fret 5th string (A)", "E is on the 5th fret of the A string", "e|---1---  (A)".

Confidence rules (a correct shape must never fail):
  - only labels whose quality is in QUALITIES are checked; anything else (13, 7b9, "D-7", ...) is skipped;
  - a bare letter is a label only when it is clearly a chord, not a note list, a string name or a
    CAGED shape name; a table cell must hold a label with a quality ("F7 (V)", never a bare "C",
    which is as likely a key column);
  - labels taken from another line must all name the same chord;
  - a chord shape fails when it sounds a pitch class outside the chord and its tolerated colour tones
    (TOLERATED), or lacks the chord's third, its seventh (seventh chords) or its altered fifth
    (dim/aug/m7b5); root and plain fifth may be omitted;
  - a note list fails only when it both names a note the shape does not sound and omits one it does
    (a rootless voicing listed with its root, or a colour tone left out of the list, passes);
  - a six-string diagram that is right when read high e first (GA's storage order) passes;
  - shapes with fewer than three sounding strings, or any fret above 24, are skipped;
  - "E string" claims fail only when the note is wrong on both E strings.
"""
import re

OPEN = {6: 4, 5: 9, 4: 2, 3: 7, 2: 11, 1: 4}  # string number -> open pitch class
NAMES = "C C# D D# E F F# G G# A A# B".split()
LETTER = {"C": 0, "D": 2, "E": 4, "F": 5, "G": 7, "A": 9, "B": 11}

# quality -> (intervals, required intervals besides the root)
QUALITIES = {
    "": ({0, 4, 7}, {4}),
    "maj": ({0, 4, 7}, {4}),
    "m": ({0, 3, 7}, {3}),
    "min": ({0, 3, 7}, {3}),
    "7": ({0, 4, 7, 10}, {4, 10}),
    "maj7": ({0, 4, 7, 11}, {4, 11}),
    "M7": ({0, 4, 7, 11}, {4, 11}),
    "Δ": ({0, 4, 7, 11}, {4, 11}),
    "Δ7": ({0, 4, 7, 11}, {4, 11}),
    "m7": ({0, 3, 7, 10}, {3, 10}),
    "min7": ({0, 3, 7, 10}, {3, 10}),
    "mmaj7": ({0, 3, 7, 11}, {3, 11}),
    "mM7": ({0, 3, 7, 11}, {3, 11}),
    "m7b5": ({0, 3, 6, 10}, {3, 6, 10}),
    "ø": ({0, 3, 6, 10}, {3, 6, 10}),
    "ø7": ({0, 3, 6, 10}, {3, 6, 10}),
    "dim": ({0, 3, 6}, {3, 6}),
    "°": ({0, 3, 6}, {3, 6}),
    "dim7": ({0, 3, 6, 9}, {3, 6, 9}),
    "°7": ({0, 3, 6, 9}, {3, 6, 9}),
    "aug": ({0, 4, 8}, {4, 8}),
    "+": ({0, 4, 8}, {4, 8}),
    "6": ({0, 4, 7, 9}, {4, 9}),
    "m6": ({0, 3, 7, 9}, {3, 9}),
    "9": ({0, 2, 4, 7, 10}, {4, 10}),
    "m9": ({0, 2, 3, 7, 10}, {3, 10}),
    "maj9": ({0, 2, 4, 7, 11}, {4, 11}),
    "add9": ({0, 2, 4, 7}, {2, 4}),
    "sus2": ({0, 2, 7}, {2}),
    "sus4": ({0, 5, 7}, {5}),
    "sus": ({0, 5, 7}, {5}),
    "7sus4": ({0, 5, 7, 10}, {5, 10}),
    "5": ({0, 7}, set()),
}
# Colour tones a guitarist may add without the shape being a different chord in casual usage
# ("Bm" drawn as x20202 = Bm7, "C" as Cadd9, "F#dim" as F#m7b5). They never cause a failure.
TOLERATED = {"": {2, 9}, "maj": {2, 9}, "m": {2, 10}, "min": {2, 10}, "dim": {9, 10}, "°": {9, 10},
             "7": {2, 9}, "m7": {2, 5}, "min7": {2, 5}, "maj7": {2, 9}, "M7": {2, 9}, "Δ": {2, 9}, "Δ7": {2, 9}}
_QUAL_ALT = "|".join(re.escape(q) for q in sorted((q for q in QUALITIES if q), key=len, reverse=True))
# A chord label: root, accidental, optional quality, optional slash bass. "(?![\w#+°ø/-])" refuses
# partial reads such as "G7b9" (unknown quality) or "D-7" (jazz minor, ambiguous after normalize()).
LABEL = re.compile(
    r"(?<![A-Za-z0-9#/])(?P<root>[A-G])(?P<acc>#|b)?"
    r"(?:(?P<qual>" + _QUAL_ALT + r")|[ ](?P<squal>major|minor|maj7|maj|m7b5|m7|min7|min|dim7|dim|aug|sus4|sus2|m)(?![a-z]))?"
    r"(?:/(?P<bass>[A-G])(?P<bacc>#|b)?)?(?![\w#+°ø/-])")
NOT_A_CHORD_AFTER = re.compile(r"^\s*-?\s*(?i:strings?|fret|shape|form|position|note|notes|root|bass|"
                               r"scale|key|mode|flat|sharp|natural|string's|pentatonic|blues|harmonic|melodic|"
                               r"(?:major|minor)\s+(?:scale|key|mode|pentatonic|blues))\b")
NOTE_LIST_BEFORE = re.compile(r"[A-G][#b]?[\s,]*[-,][\s,]*$|[A-G][#b]?\s+$")
BARE_THEN_WORD = re.compile(r"^[ \t]+(?!(?:chords?|triad|barre|voicing|grip|open|at|on|is)\b)[a-z]")
# A label line above a shape: one bare label alone, optionally with a parenthetical, a colon or "chord".
BARE_LABEL_LINE = re.compile(r"^[#*_>\s]*(?P<l>[A-G][#b]?)(?:\s*\([^()\n]*\)|\s+(?i:chord|triad))?[\s:*_]*$")
NOTE_LIST_AFTER = re.compile(r"^[\s,]*[-,][\s,]*[A-G][#b]?(?![A-Za-z0-9])|^\s+[A-G][#b]?(?:\s+[A-G][#b]?)+\b")

FRET = r"(?:x|X|\d{1,2})"
DIAGRAM = re.compile(
    r"(?<![\w/.-])(?:"
    r"(?P<compact>[xX0-9]{6})"
    r"|(?P<dashed>" + FRET + r"(?:-" + FRET + r"){5})"
    r"|(?P<spaced>" + FRET + r"(?: " + FRET + r"){5})"
    r")(?![\w/-])")
GAP_OK = re.compile(r"^[\s:=(]*(?:(?i:barre|shape|voicing|chord|grip|form|at|on|the|fret|frets|is|open|position|"
                    r"\d{1,2}(?:st|nd|rd|th)?)[\s:=(,]*){0,4}$")
AFTER_LABEL = re.compile(r"^\s*\(\s*(?i:open\s+|barre\s+)?(?P<label>[^()\n]{1,12}?)\s*\)")
NOTE_SET_AFTER = re.compile(r"^\s*\(\s*(?P<notes>[A-G][#b]?(?:\s*[-,\s]\s*[A-G][#b]?){2,6})\s*\)")
CELL_TAIL = re.compile(r"\s*(?:\([^()\n]*\))?\s*")
# Lines between a label and its shape that carry no label of their own: blank, VexTab headers, fences,
# ASCII tab rows, bare fret/caret/string-number rows, string-name rows.
SKIP_ABOVE = re.compile(
    r"^\s*(?:"
    r"|(?i:vextab|tabstave)(?:\s+[\w-]+=[\w-]+)*"
    r"|`{3}.*"
    r"|[eBGDAE]\s*\|.*"
    r"|(?=[^\n]*[\dxX^])[\s^xX\d-]*(?:\(?(?i:strings?)\)?)?"
    r"|[EADGBe](?:\s+[EADGBe]){2,}"
    r")\s*$")

VEX_GROUP = re.compile(r"\((\d{1,2}/[1-6](?:\.\d{1,2}/[1-6])*)\)")
VEX_COMMENT = re.compile(r"%|//")
_ROW = r"[ \t]*{}[ \t]*\|[- \t]*(x|X|\d{{1,2}})[- \t|]*\r?(?:\n|$)"
ASCII_BLOCKS = [  # six-line ASCII chord blocks, one fret per string; groups in string order 1..6
    (re.compile(r"(?m)^" + "".join(_ROW.format(n) for n in ("[eE]", "B", "G", "D", "A", "E"))), [1, 2, 3, 4, 5, 6]),
    (re.compile(r"(?m)^" + "".join(_ROW.format(n) for n in ("E", "A", "D", "G", "B", "[eE]"))), [6, 5, 4, 3, 2, 1]),
]

STRING_NAMES = {"e": [1], "high e": [1], "b": [2], "g": [3], "d": [4], "a": [5], "low e": [6], "E": [6, 1],
                "B": [2], "G": [3], "D": [4], "A": [5]}
ORD = r"(?P<{0}>[1-6])(?:st|nd|rd|th)"
NOTE = r"(?P<note>[A-G][#b]?)(?![\w#])"
SNAME = r"(?P<sname>(?i:low\s+E|high\s+e)|[EADGBe])"
# Claim patterns never cross a line break ("- 5th string, 1st fret - Bb\n- 4th string, 3rd fret - D" is two
# claims, and "Bb\n- 4th string, 3rd fret" is none), so they use SP (space or tab) instead of \s.
SP = r"[ \t]"
SAYS = SP + r"*(?:\(|=|:|-|(?i:is|gives))" + SP + r"*(?:(?i:the|a|an)" + SP + r"+)?(?:(?i:note)" + SP + r"+)?"
STR = r"[ \t-]*(?i:string)"
FRT = r"[ \t-]*(?i:fret)"
THE = r"(?:(?i:the)" + SP + r"+)?"
CLAIMS = [
    # 5th string, 5th fret (the C ...) / 3rd string, 3rd fret: C / 5th-string 5th-fret = F
    re.compile(ORD.format("s") + STR + r",?" + SP + "*" + THE + ORD.format("f") + FRT + SAYS + NOTE),
    # 5th fret on the 6th string (C) / 4th fret on the B string = D# / 2nd fret 5th string (A)
    re.compile(ORD.format("f") + FRT + r",?" + SP + r"*(?:(?i:on|of)" + SP + r"+)?" + THE + r"(?:" + ORD.format("s")
               + "|" + SNAME + r")" + STR + SAYS + NOTE),
    # string 1 fret 2 = E / String 3 2nd fret = A
    re.compile(r"(?i:string)" + SP + r"*(?P<s>[1-6])" + SP + r"*,?" + SP + r"*(?:(?i:fret)" + SP + r"*(?P<f>\d{1,2})|"
               + ORD.format("f2") + FRT + r")" + SAYS + NOTE),
    # B string fret 2 = B / A string, 3rd fret (C)
    re.compile(SNAME + STR + r",?" + SP + r"*(?:(?i:at)" + SP + r"+)?" + THE + r"(?:(?i:fret)" + SP + r"*(?P<f>\d{1,2})|"
               + ORD.format("f2") + FRT + r")" + SAYS + NOTE),
    # E = 4th string 5th fret / Major 3rd (E) - 4th string, 5th fret / B (2nd string, 2nd fret) /
    # Bb (b6) on the 4th string, 1st fret
    re.compile(r"(?<![\w#])" + NOTE + r"(?:" + SP + r"*\([^()\n]{1,12}\))?\)?" + SP + r"*(?:=|:|-|\(|(?i:is|at|on))" + SP
               + r"*" + THE + ORD.format("s")
               + STR + r",?" + SP + r"*" + ORD.format("f") + FRT),
    # 4th string 1 = C
    re.compile(ORD.format("s") + STR + SP + r"+(?P<f>\d{1,2})" + SP + r"*=" + SP + r"*" + NOTE),
    # Root E is on the 5th fret of the A string
    re.compile(r"(?<![\w#])" + NOTE + SP + r"+(?:(?i:is)" + SP + r"+)?(?i:on|at)" + SP + r"+(?i:the)" + SP + r"+"
               + ORD.format("f") + FRT + SP + r"+(?i:of|on)" + SP + r"+(?i:the)" + SP + r"+(?:" + ORD.format("s") + "|"
               + SNAME + r")" + STR),
    # 6th string, open (G)
    re.compile(ORD.format("s") + STR + r",?" + SP + r"*(?i:open)" + SP + r"*(?:\(|=|:|-|(?i:is))" + SP + r"*" + THE
               + NOTE),
]
ASCII_LINE = re.compile(r"(?m)^[ \t]*(?P<sname>[eBGDAE])[ \t]*\|[-| \t]*(?P<f>\d{1,2})[-| \t]*\(\s*" + NOTE + r"\s*\)")


def pc(root, acc=None):
    return (LETTER[root] + (1 if acc == "#" else -1 if acc == "b" else 0)) % 12


def chord_of(m):
    """(root pc, quality key, bass pc or None) from a LABEL match, or None when not checkable."""
    qual = m.group("qual") if m.group("qual") is not None else (m.group("squal") or "")
    qual = {"major": "", "minor": "m"}.get(qual, qual)
    if qual not in QUALITIES:
        return None
    bass = pc(m.group("bass"), m.group("bacc")) if m.group("bass") else None
    return pc(m.group("root"), m.group("acc")), qual, bass


def is_chord_label(text, m):
    """Reject note lists, string names and CAGED shape names; a bare letter needs chord context."""
    before, after = text[:m.start()], text[m.end():]
    if NOT_A_CHORD_AFTER.match(after) or after.startswith("|"):  # "E|---3---" is an ASCII tab string name
        return False
    bare = not (m.group("qual") or m.group("squal") or m.group("bass"))
    if NOTE_LIST_BEFORE.search(before[-6:]) or NOTE_LIST_AFTER.match(after[:12]):
        return False
    if bare and re.search(r"(?i:\b(?:key|scale|mode|tonic|root|note|notes|string|of|in)\s*)$", before[-12:]):
        return False  # "in C", "key of G", "the root A": a key or a note, not a chord
    if bare and BARE_THEN_WORD.match(after):
        return False  # "A common shape", "A handy voicing": the article
    return True


def pitch_classes(frets_by_string):
    return {(OPEN[s] + f) % 12 for s, f in frets_by_string}


def describe(frets_by_string):
    return " ".join(NAMES[(OPEN[s] + f) % 12] for s, f in sorted(frets_by_string, reverse=True))


def shape_problem(chord, frets_by_string):
    """Return a reason string when the sounding notes cannot be the labelled chord, else None."""
    if len(frets_by_string) < 3 or any(f > 24 for _, f in frets_by_string):
        return None
    root, qual, bass = chord
    intervals, required = QUALITIES[qual]
    allowed = ({(root + i) % 12 for i in intervals | TOLERATED.get(qual, set())}
               | ({bass} if bass is not None else set()))
    sounding = pitch_classes(frets_by_string)
    extra = sorted(sounding - allowed)
    missing = sorted({(root + i) % 12 for i in required} - sounding)
    if not extra and not missing:
        return None
    parts = []
    if extra:
        parts.append("adds " + " ".join(NAMES[x] for x in extra))
    if missing:
        parts.append("lacks " + " ".join(NAMES[x] for x in missing))
    return ", ".join(parts)


def parse_diagram(m):
    raw = m.group("compact") or m.group("dashed") or m.group("spaced")
    if m.group("compact"):
        tokens = list(raw)
    else:
        tokens = re.split(r"[- ]", raw)
    if m.group("spaced") and not any(t.lower() == "x" for t in tokens):
        return None  # "1 2 3 4 5 6" is more likely a degree list than a shape
    if m.group("compact") and raw.isdigit() and len(set(raw)) == 1:
        return None
    out = []
    for i, t in enumerate(tokens):
        if t.lower() != "x":
            out.append((6 - i, int(t)))
    return out


def agreed_label(s):
    """(chord, label text) when every chord label in s names the same checkable chord, else (None, None)."""
    found = [x for x in LABEL.finditer(s) if is_chord_label(s, x)]
    chords = {chord_of(x) for x in found}
    if found and len(chords) == 1 and None not in chords:
        return chords.pop(), max((x.group(0) for x in found), key=len)
    return None, None


def label_above(text, line_start):
    """Label of the nearest line above line_start that is not blank, a VexTab header or a diagram row.

    That line must read as a label: a table row never does (it labels its own cells), and a bare letter
    counts only on a line of its own ("C", "### C (open shape)", "G chord:"), never inside prose or a
    list item ("- 1st string, 1st fret - Bb")."""
    end = line_start - 1
    for _ in range(8):
        if end <= 0:
            break
        start = text.rfind("\n", 0, end) + 1
        prev = text[start:end].strip()
        end = start - 1
        if SKIP_ABOVE.match(prev):
            continue
        if prev.startswith("|"):
            return None, None
        chord, label = agreed_label(prev)
        if chord and re.fullmatch(r"[A-G][#b]?", label) and not BARE_LABEL_LINE.match(prev):
            return None, None
        return chord, label
    return None, None


def label_for_diagram(text, m):
    line_start = max(text.rfind("\n", 0, m.start()), text.rfind("|", 0, m.start())) + 1
    segment = text[line_start:m.start()]
    labels = [x for x in LABEL.finditer(segment) if is_chord_label(segment, x)]
    if labels:
        last = labels[-1]
        if GAP_OK.match(segment[last.end():]):
            return chord_of(last), last.group(0)
    after = AFTER_LABEL.match(text[m.end():m.end() + 24])
    if after:
        inner = after.group("label")
        lm = LABEL.fullmatch(inner.strip())
        if lm:
            return chord_of(lm), lm.group(0)
    if segment.strip():
        return None, None
    if line_start > 0 and text[line_start - 1] == "|":
        # table row "| F7 (V) | 1 x 2 2 1 3 |": the previous cell must hold one label with a quality
        cell_end = line_start - 1
        cell_start = max(text.rfind("|", 0, cell_end), text.rfind("\n", 0, cell_end)) + 1
        cell = text[cell_start:cell_end].strip()
        lm = LABEL.match(cell)
        if (lm and (lm.group("qual") or lm.group("squal") or lm.group("bass"))
                and CELL_TAIL.fullmatch(cell[lm.end():])):
            return chord_of(lm), lm.group(0)
        return None, None
    line_end = text.find("\n", m.end())
    rest = text[m.end():line_end if line_end >= 0 else len(text)]
    if not rest.strip() or NOTE_SET_AFTER.match(rest):
        return label_above(text, line_start)  # diagram alone on its line, under its label
    return None, None


def vex_label(text, line_start, line, group_count):
    split = VEX_COMMENT.search(line)
    code, comment = (line[:split.start()], line[split.end():]) if split else (line, "")
    if comment:
        found = [x for x in LABEL.finditer(comment) if is_chord_label(comment, x)]
        if found:
            return chord_of(found[0]), found[0].group(0)
    if group_count != 1:
        return None, None
    chord, label = agreed_label(code)
    if chord:
        return chord, label
    return label_above(text, line_start)


def note_set_problem(listed, frets_by_string):
    """A diagram's claimed notes vs its sound, in both string orders: fail only on a two-way mismatch."""
    if len(frets_by_string) < 3 or any(f > 24 for _, f in frets_by_string):
        return None
    for reading in (frets_by_string, [(7 - s, f) for s, f in frets_by_string]):
        sounding = pitch_classes(reading)
        if not (listed - sounding and sounding - listed):
            return None
    sounding = pitch_classes(frets_by_string)
    return ("lists " + " ".join(NAMES[x] for x in sorted(listed - sounding)) + " it does not sound, sounds "
            + " ".join(NAMES[x] for x in sorted(sounding - listed)) + " it does not list")


def check(text):
    """Return tab_check failure records for confident mismatches in normalized answer text."""
    failures = []
    seen = set()
    for m in DIAGRAM.finditer(text):
        frets = parse_diagram(m)
        if not frets:
            continue
        notes = NOTE_SET_AFTER.match(text[m.end():m.end() + 40])
        if notes:
            listed = {pc(n[0], n[1:] or None) for n in re.findall(r"[A-G][#b]?", notes.group("notes"))}
            why = note_set_problem(listed, frets)
            key = (notes.group(0).strip(), m.group(0))
            if why and key not in seen:
                seen.add(key)
                failures.append({"kind": "tab_check", "claim": f"{m.group(0)} {notes.group(0).strip()}",
                                 "decoded": describe(frets), "detail": why})
        chord, label = label_for_diagram(text, m)
        if not chord:
            continue
        why = shape_problem(chord, frets)
        if why and shape_problem(chord, [(7 - s, f) for s, f in frets]) is None:
            why = None  # written high e first (GA stores diagrams that way): right when read in that order
        key = (label, m.group(0))
        if why and key not in seen:
            seen.add(key)
            failures.append({"kind": "tab_check", "claim": f"{label} = {m.group(0)}",
                             "decoded": describe(frets), "detail": why})
    for lm in re.finditer(r"(?im)^[^\n]*\bnotes\b[^\n]*$", text):
        line = lm.group(0)
        split = VEX_COMMENT.search(line)
        groups = VEX_GROUP.findall(line[:split.start()] if split else line)
        if not groups:
            continue
        chord, label = vex_label(text, lm.start(), line, len(groups))
        if not chord or len(groups) != 1:
            continue
        frets = []
        for part in groups[0].split("."):
            f, s = part.split("/")
            frets.append((int(s), int(f)))
        if len({s for s, _ in frets}) != len(frets):
            continue  # two notes on one string: not a playable chord, not ours to judge
        why = shape_problem(chord, frets)
        if why:
            failures.append({"kind": "tab_check", "claim": f"{label} = VexTab ({groups[0]})",
                             "decoded": describe(frets), "detail": why})
    for rx, strings in ASCII_BLOCKS:
        for m in rx.finditer(text):
            frets = [(s, int(f)) for s, f in zip(strings, m.groups()) if f.lower() != "x"]
            chord, label = label_above(text, m.start())
            why = shape_problem(chord, frets) if chord else None
            if why:
                failures.append({"kind": "tab_check", "claim": f"{label} = ASCII tab "
                                 + "-".join(dict(zip(strings, m.groups()))[s] for s in (6, 5, 4, 3, 2, 1)),
                                 "decoded": describe(frets), "detail": why})
    for rx in CLAIMS:
        for m in rx.finditer(text):
            failures.extend(_note_claim(m))
    for m in ASCII_LINE.finditer(text):
        failures.extend(_note_claim(m))
    return failures


def _note_claim(m):
    gd = m.groupdict()
    if gd.get("s"):
        strings = [int(gd["s"])]
    else:
        name = gd.get("sname") or ""
        key = name if name in STRING_NAMES else re.sub(r"\s+", " ", name.lower())
        strings = STRING_NAMES.get(key)
        if not strings:
            return []
    fret = gd.get("f") or gd.get("f2")
    fret = 0 if fret is None and "open" in m.group(0).lower() else fret
    if fret is None:
        return []
    fret = int(fret)
    if fret > 24:
        return []
    claimed = pc(gd["note"][0], gd["note"][1:] or None)
    actual = [(OPEN[s] + fret) % 12 for s in strings]
    if claimed in actual:
        return []
    return [{"kind": "tab_check", "claim": m.group(0).strip()[:80],
             "decoded": " / ".join(f"string {s} fret {fret} = {NAMES[a]}" for s, a in zip(strings, actual)),
             "detail": f"says {gd['note']}"}]
