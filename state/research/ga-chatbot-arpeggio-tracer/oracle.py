#!/usr/bin/env python3
"""Controlled public tracer for the GA chatbot progression-arpeggio answer.

Stdlib only. `run` POSTs each case in cases.json to the public SSE endpoint,
records every frame with a monotonic timestamp, and writes raw captures.
`grade` applies the deterministic music oracle to those captures.

The oracle derives expectations from interval arithmetic, never from the
system's own output: a chord's tones come from its written quality, a scale's
pitch classes from its interval pattern, and the key from the case manifest.

Usage:
  python oracle.py run   [--out runs/<stamp>]
  python oracle.py grade  --out runs/<stamp>
"""
import json, os, re, sys, time, urllib.request, datetime

HERE = os.path.dirname(os.path.abspath(__file__))
NOTE_PC = {"C": 0, "D": 2, "E": 4, "F": 5, "G": 7, "A": 9, "B": 11}

# Written-quality suffix -> chord-tone intervals (only the qualities the cases use,
# plus the labels the skill can emit, so an unexpected label is visible, not ignored).
CHORD = {
    "": [0, 4, 7], "m": [0, 3, 7], "7": [0, 4, 7, 10], "maj7": [0, 4, 7, 11],
    "m7": [0, 3, 7, 10], "mMaj7": [0, 3, 7, 11], "m7b5": [0, 3, 6, 10],
    "dim": [0, 3, 6], "dim7": [0, 3, 6, 9], "aug": [0, 4, 8], "7sus4": [0, 5, 7, 10],
}

# Scale name (as rendered, longest first) -> interval pattern.
SCALES = [
    ("Aeolian (natural minor)", [0, 2, 3, 5, 7, 8, 10]),
    ("Aeolian (minor)", [0, 2, 3, 5, 7, 8, 10]),
    ("Altered (Super Locrian)", [0, 1, 3, 4, 6, 8, 10]),
    ("Ionian (major)", [0, 2, 4, 5, 7, 9, 11]),
    ("Half-Whole Diminished", [0, 1, 3, 4, 6, 7, 9, 10]),
    ("Whole-Half Diminished", [0, 2, 3, 5, 6, 8, 9, 11]),
    ("Phrygian Dominant", [0, 1, 4, 5, 7, 8, 10]),
    ("Lydian Dominant", [0, 2, 4, 6, 7, 9, 10]),
    ("Lydian Augmented", [0, 2, 4, 6, 8, 9, 11]),
    ("Major Pentatonic", [0, 2, 4, 7, 9]),
    ("Minor Pentatonic", [0, 3, 5, 7, 10]),
    ("Mixolydian b6", [0, 2, 4, 5, 7, 8, 10]),
    ("Melodic Minor", [0, 2, 3, 5, 7, 9, 11]),
    ("Locrian #2", [0, 2, 3, 5, 6, 8, 10]),
    ("Whole Tone", [0, 2, 4, 6, 8, 10]),
    ("Mixolydian", [0, 2, 4, 5, 7, 9, 10]),
    ("Phrygian", [0, 1, 3, 5, 7, 8, 10]),
    ("Locrian", [0, 1, 3, 5, 6, 8, 10]),
    ("Lydian", [0, 2, 4, 6, 7, 9, 11]),
    ("Dorian", [0, 2, 3, 5, 7, 9, 10]),
]

NOTE_NAMES = ["C", "C#", "D", "Eb", "E", "F", "F#", "G", "Ab", "A", "Bb", "B"]
UNCERTAINTY = ["couldn't", "could not", "can't", "cannot", "not a valid", "invalid",
               "unrecognized", "not recognize", "doesn't look like", "isn't a",
               "no chord", "not sure", "unclear", "name one chord", "not a standard"]


def root_pc(sym):
    pc = NOTE_PC[sym[0]]
    if len(sym) > 1 and sym[1] in "#b":
        pc += 1 if sym[1] == "#" else -1
    return pc % 12


def split_symbol(sym):
    n = 2 if len(sym) > 1 and sym[1] in "#b" else 1
    return sym[:n], sym[n:]


def chord_pcs(sym):
    root, suffix = split_symbol(sym)
    if suffix not in CHORD:
        return None
    return sorted({(root_pc(root) + i) % 12 for i in CHORD[suffix]})


def scale_pcs(root, name):
    for label, ivs in SCALES:
        if name.startswith(label):
            return label, sorted({(root_pc(root) + i) % 12 for i in ivs})
    return None, None


def names(pcs):
    return " ".join(NOTE_NAMES[p] for p in pcs)


# ---------------------------------------------------------------- run
def run_case(endpoint, prompt):
    body = json.dumps({"message": prompt, "conversationHistory": []}).encode()
    req = urllib.request.Request(endpoint, data=body, method="POST",
                                 headers={"Content-Type": "application/json",
                                          "Accept": "text/event-stream",
                                          "User-Agent": "ga-arpeggio-tracer/1"})
    t0 = time.monotonic()
    frames, status, err = [], None, None
    try:
        with urllib.request.urlopen(req, timeout=60) as resp:
            status = resp.status
            data_lines = []
            while True:
                raw = resp.readline()
                if not raw:
                    break
                line = raw.decode("utf-8").rstrip("\r\n")
                if line.startswith("data: "):
                    data_lines.append(line[6:])
                elif line == "" and data_lines:
                    frames.append({"t_ms": round((time.monotonic() - t0) * 1000, 1),
                                   "data": "\n".join(data_lines)})
                    data_lines = []
    except Exception as e:  # recorded, never swallowed silently
        err = f"{type(e).__name__}: {e}"
    return {"prompt": prompt, "status": status, "transport_error": err,
            "total_ms": round((time.monotonic() - t0) * 1000, 1), "frames": frames}


def cmd_run(out):
    cases = json.load(open(os.path.join(HERE, "cases.json"), encoding="utf-8"))
    os.makedirs(out, exist_ok=True)
    started = datetime.datetime.now(datetime.timezone.utc).isoformat(timespec="seconds")
    captures = []
    for case in cases["cases"]:
        for i in range(case["repeats"]):
            cap = run_case(cases["endpoint"], case["prompt"])
            cap.update({"case": case["id"], "repeat": i + 1,
                        "at_utc": datetime.datetime.now(datetime.timezone.utc).isoformat(timespec="seconds")})
            captures.append(cap)
            print(f"{case['id']}#{i + 1} status={cap['status']} frames={len(cap['frames'])} "
                  f"total_ms={cap['total_ms']} err={cap['transport_error']}")
            time.sleep(2)  # keep the public service unloaded
    json.dump({"started_utc": started, "endpoint": cases["endpoint"], "captures": captures},
              open(os.path.join(out, "captures.json"), "w", encoding="utf-8"), indent=2)


# ---------------------------------------------------------------- grade
LINE = re.compile(r"\*\*(?P<chord>[^*]+)\*\*\s*→\s*arpeggio\s*\*\*(?P<arp>[^*]+)\*\*,\s*play\s*\*\*(?P<root>[A-G][#b]?)\s+(?P<scale>[^*]+)\*\*")


def decode(cap):
    routing, text, done, errors = None, "", False, []
    for f in cap["frames"]:
        d = f["data"]
        if d == "[DONE]":
            done = True
            continue
        if d.startswith("{"):
            try:
                obj = json.loads(d)
                if obj.get("type") == "routing" and routing is None:
                    routing = {**obj, "t_ms": f["t_ms"]}
                    continue
                if "error" in obj:
                    errors.append(obj["error"])
                    continue
            except json.JSONDecodeError:
                pass
        text += d
    first_text = next((f["t_ms"] for f in cap["frames"]
                       if f["data"] != "[DONE]" and not f["data"].startswith("{")), None)
    return routing, text, done, errors, first_text


def grade_capture(case, cap):
    routing, text, done, errors, first_text = decode(cap)
    checks = []

    def check(name, ok, detail=""):
        checks.append({"check": name, "pass": bool(ok), "detail": detail})

    check("transport", cap["status"] == 200 and not cap["transport_error"],
          f"status={cap['status']} err={cap['transport_error']}")
    check("terminal [DONE]", done)
    check("no error frame", not errors, "; ".join(errors))
    check("nonempty answer", len(text.strip()) >= 40, f"{len(text)} chars")
    agent = routing.get("agentId") if routing else None
    if case.get("expect_route"):
        check("route", agent == case["expect_route"], f"agentId={agent}")
    for bad in case.get("forbidden", []):
        check(f"forbidden '{bad}' absent", bad not in text)

    lines = [m.groupdict() for m in LINE.finditer(text)]
    if case.get("invalid_input"):
        check("no fabricated per-chord arpeggio line", not lines, f"{len(lines)} lines")
        low = text.lower()
        check("declines or states uncertainty (heuristic)", any(u in low for u in UNCERTAINTY))
        return routing, text, first_text, checks, []

    got = [l["chord"].strip() for l in lines]
    check("one line per chord, in order", got == case["chords"], f"got={got}")
    per_chord = []
    for l in lines:
        chord, arp, root, scale = l["chord"].strip(), l["arp"].strip(), l["root"], l["scale"].strip()
        want = chord_pcs(chord)
        arp_pcs = chord_pcs(arp)
        label, spcs = scale_pcs(root, scale)
        row = {"chord": chord, "arpeggio": arp, "lead_scale": f"{root} {scale}",
               "chord_tones": names(want) if want else None,
               "arpeggio_tones": names(arp_pcs) if arp_pcs else None,
               "scale_notes": names(spcs) if spcs else None}
        check(f"{chord}: arpeggio preserves written quality", want is not None and arp_pcs == want,
              f"{arp} -> {row['arpeggio_tones']} vs {row['chord_tones']}")
        check(f"{chord}: lead scale contains every chord tone",
              spcs is not None and want is not None and set(want) <= set(spcs),
              f"{root} {scale} -> {row['scale_notes']}")
        if chord not in case.get("non_diatonic_chords", []) and spcs is not None:
            outside = sorted(set(spcs) - set(case["key_pcs"]))
            row["outside_key"] = names(outside)
            check(f"{chord}: lead scale stays in the progression's key", not outside,
                  f"outside key: {names(outside) or '-'}")
        guard = case.get("secondary_dominant_guard")
        if guard and guard["chord"] == chord and spcs is not None:
            check(f"{chord}: secondary-dominant guard (keeps {NOTE_NAMES[guard['must_contain_pc']]}, "
                  f"no {NOTE_NAMES[guard['must_not_contain_pc']]})",
                  guard["must_contain_pc"] in spcs and guard["must_not_contain_pc"] not in spcs)
        per_chord.append(row)
    return routing, text, first_text, checks, per_chord


def cmd_grade(out):
    cases = {c["id"]: c for c in json.load(open(os.path.join(HERE, "cases.json"), encoding="utf-8"))["cases"]}
    caps = json.load(open(os.path.join(out, "captures.json"), encoding="utf-8"))
    results = []
    for cap in caps["captures"]:
        case = cases[cap["case"]]
        routing, text, first_text, checks, per_chord = grade_capture(case, cap)
        results.append({"case": cap["case"], "repeat": cap["repeat"], "at_utc": cap["at_utc"],
                        "agentId": routing.get("agentId") if routing else None,
                        "routingMethod": routing.get("routingMethod") if routing else None,
                        "routing_ms": routing.get("t_ms") if routing else None,
                        "first_text_ms": first_text, "total_ms": cap["total_ms"],
                        "passed": sum(c["pass"] for c in checks), "checks_total": len(checks),
                        "checks": checks, "per_chord": per_chord, "answer": text})
        fails = [c for c in checks if not c["pass"]]
        print(f"{cap['case']}#{cap['repeat']}: {len(checks) - len(fails)}/{len(checks)} "
              f"route={results[-1]['agentId']} first_text_ms={first_text} total_ms={cap['total_ms']}")
        for c in fails:
            print(f"    FAIL {c['check']}  {c['detail']}")
    json.dump({"endpoint": caps["endpoint"], "started_utc": caps["started_utc"], "results": results},
              open(os.path.join(out, "graded.json"), "w", encoding="utf-8"), indent=2, ensure_ascii=False)


if __name__ == "__main__":
    args = sys.argv[1:]
    out = args[args.index("--out") + 1] if "--out" in args else os.path.join(
        HERE, "runs", datetime.datetime.now(datetime.timezone.utc).strftime("%Y%m%dT%H%M%SZ"))
    {"run": cmd_run, "grade": cmd_grade}[args[0]](out)
