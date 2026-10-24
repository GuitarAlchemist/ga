"""Theory QA eval engine: ask every question in questions.json, grade each answer with its regexes, and
write answers.jsonl + summary.json.

Targets:
  deployed (default)  POST {endpoint} with {"message", "conversationHistory": [], "source"}; the source
                      tag ("theory-qa" by default) keeps these rows out of real-traffic analysis.
  ollama              POST {ollama}/api/chat with --model and an optional --system-file.

Grading is fail-closed: an empty answer, a request error or a regex error is a fail. Besides each
question's regexes, every answer goes through tabcheck.check(), which decodes the chord diagrams, VexTab
chords and string/fret note claims in standard tuning and fails confident mismatches ("tab_check").
Exit code: 0 when every question got an answer (graded fails included), 2 on infrastructure failure
(unreadable questions file, or any request that errored or timed out).

Usage:
  python run_eval.py                                     # deployed chatbot, all questions
  python run_eval.py --samples 3                         # each question 3 times: per-question pass counts
  python run_eval.py --target ollama --model gpt-oss:120b-cloud --system-file prompt.txt
  python run_eval.py --only chord-db7,scale-c-blues --out-dir runs/try
  python run_eval.py --regrade runs/baseline-deployed/answers.jsonl --out-dir runs/baseline-regraded
"""
import argparse
import hashlib
import json
import re
import statistics
import sys
import time
import urllib.error
import urllib.request
from datetime import datetime, timezone
from pathlib import Path

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE))
import tabcheck  # noqa: E402

SUPERSCRIPTS = str.maketrans("⁰¹²³⁴⁵⁶⁷⁸⁹", "0123456789")
DASHES = re.compile("[‐‑‒–—―−]")
SPACES = re.compile("[     ]")


def normalize(text):
    t = text.replace("♭", "b").replace("♯", "#").replace("𝄫", "bb").replace("𝄪", "##").replace("♮", "")
    t = DASHES.sub("-", t)
    t = SPACES.sub(" ", t).translate(SUPERSCRIPTS)
    t = t.replace("‘", "'").replace("’", "'").replace("“", '"').replace("”", '"')
    t = t.replace("**", "").replace("__", "").replace("`", "")
    t = re.sub(r"\b([A-G])[\s-]?flat\b", r"\1b", t)
    t = re.sub(r"\b([A-G])[\s-]?sharp\b", r"\1#", t)
    return t


def grade(answer, q):
    """Return a list of failure records; empty means pass."""
    if not answer or not answer.strip():
        return [{"kind": "empty"}]
    text = normalize(answer)
    failed = []
    for p in q.get("must_match", []):
        try:
            if not re.search(p, text, re.I):
                failed.append({"kind": "must_match", "pattern": p})
        except re.error as e:
            failed.append({"kind": "regex_error", "pattern": p, "detail": str(e)})
    for p in q.get("must_not_match", []):
        try:
            m = re.search(p, text, re.I)
            if m:
                failed.append({"kind": "must_not_match", "pattern": p, "hit": m.group(0)[:120]})
        except re.error as e:
            failed.append({"kind": "regex_error", "pattern": p, "detail": str(e)})
    failed.extend(tabcheck.check(text))
    return failed


def post_json(url, body, timeout):
    req = urllib.request.Request(url, data=json.dumps(body).encode(), headers={"Content-Type": "application/json"})
    return json.loads(urllib.request.urlopen(req, timeout=timeout).read())


def get_json(url, timeout=20):
    return json.loads(urllib.request.urlopen(url, timeout=timeout).read())


def extract_result(content):
    """Agent-style system prompts ask for a JSON object with a "result" field; grade that text when present."""
    m = re.search(r"\{.*\}", content or "", re.S)
    if m:
        try:
            r = json.loads(m.group(0)).get("result")
            if isinstance(r, str) and r.strip():
                return r
        except (json.JSONDecodeError, AttributeError):
            pass
    return content


def route_of(agent, target):
    if target == "ollama":
        return "ollama"
    if not agent:
        return "unknown"
    if agent.startswith("skill."):
        return "skill"
    if agent.startswith("fallback"):
        return "fallback"
    return "agent"


def latency_stats(xs):
    xs = [x for x in xs if x is not None]
    if not xs:
        return None
    s = sorted(xs)
    return {"n": len(s), "mean": round(statistics.mean(s), 2), "p50": round(statistics.median(s), 2),
            "p90": round(s[min(len(s) - 1, int(0.9 * len(s)))], 2), "max": round(s[-1], 2)}


def tally(rows, key):
    out = {}
    for r in rows:
        b = out.setdefault(r[key] or "unknown", {"pass": 0, "fail": 0, "latency": []})
        b["pass" if r["pass"] else "fail"] += 1
        b["latency"].append(r["elapsed_s"])
    for b in out.values():
        b["latency_s"] = latency_stats(b.pop("latency"))
    return out


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--questions", default=str(HERE / "questions.json"))
    ap.add_argument("--out-dir", default=None, help="default: runs/<UTC timestamp>-<target> next to this script")
    ap.add_argument("--target", choices=["deployed", "ollama"], default="deployed")
    ap.add_argument("--endpoint", default="http://localhost:5252/api/chatbot/chat")
    ap.add_argument("--source", default="theory-qa", help="traffic tag sent with deployed requests")
    ap.add_argument("--ollama", default="http://localhost:11434")
    ap.add_argument("--model", default=None, help="required for --target ollama")
    ap.add_argument("--system-file", default=None, help="system prompt for --target ollama")
    ap.add_argument("--only", default=None, help="comma-separated question ids")
    ap.add_argument("--timeout", type=float, default=180)
    ap.add_argument("--samples", type=int, default=1,
                    help="ask each question N times (LLM answers vary run to run); summary.json then gives "
                         "per-question pass counts")
    ap.add_argument("--regrade", default=None,
                    help="answers.jsonl of an earlier run: regrade its stored answers with the current questions "
                         "(no requests); its summary.json supplies the original window/target/model")
    args = ap.parse_args()

    try:
        qpath = Path(args.questions)
        qbytes = qpath.read_bytes()
        questions = json.loads(qbytes)["questions"]
    except (OSError, json.JSONDecodeError, KeyError) as e:
        print(f"infrastructure failure: cannot read questions: {e}", file=sys.stderr)
        return 2
    if args.only:
        wanted = {i.strip() for i in args.only.split(",") if i.strip()}
        unknown = wanted - {q["id"] for q in questions}
        if unknown:
            # an empty selection would grade nothing and still exit 0
            print(f"infrastructure failure: --only names unknown question ids: {', '.join(sorted(unknown))}",
                  file=sys.stderr)
            return 2
        questions = [q for q in questions if q["id"] in wanted]
    if args.target == "ollama" and not args.model:
        print("infrastructure failure: --target ollama needs --model", file=sys.stderr)
        return 2
    if args.samples < 1:
        print("infrastructure failure: --samples must be at least 1", file=sys.stderr)
        return 2
    system = Path(args.system_file).read_text(encoding="utf-8") if args.system_file else None

    start = datetime.now(timezone.utc)
    kind = "regrade" if args.regrade else args.target
    out_dir = Path(args.out_dir) if args.out_dir else HERE / "runs" / f"{start:%Y%m%dT%H%M%SZ}-{kind}"
    out_dir.mkdir(parents=True, exist_ok=True)
    if args.regrade:
        return regrade(Path(args.regrade), questions, qpath, qbytes, out_dir)

    status, model, endpoint = None, args.model, args.endpoint
    if args.target == "deployed":
        try:
            status = get_json(args.endpoint.rsplit("/chat", 1)[0] + "/status")
            model = status.get("chatModel")
        except (urllib.error.URLError, OSError, json.JSONDecodeError) as e:
            status = {"error": str(e)}
    else:
        endpoint = f"{args.ollama}/api/chat"

    rows = []
    with (out_dir / "answers.jsonl").open("w", encoding="utf-8") as f:
        for q, sample in ((q, k) for q in questions for k in range(args.samples)):
            t0 = time.time()
            row = {"id": q["id"], "sample": sample, "topic": q["topic"], "q": q["q"], "agent": None, "method": None,
                   "infra_error": None}
            try:
                if args.target == "deployed":
                    r = post_json(args.endpoint, {"message": q["q"], "conversationHistory": [], "source": args.source},
                                  args.timeout)
                    row.update(agent=r.get("agentId"), method=r.get("routingMethod"))
                    answer = r.get("naturalLanguageAnswer") or ""
                    row["answer_raw"] = None
                else:
                    msgs = ([{"role": "system", "content": system}] if system else []) + [{"role": "user", "content": q["q"]}]
                    r = post_json(endpoint, {"model": args.model, "stream": False, "messages": msgs}, args.timeout)
                    raw = r["message"]["content"]
                    answer = extract_result(raw)
                    row["answer_raw"] = raw if answer != raw else None
            except (urllib.error.URLError, OSError, json.JSONDecodeError, KeyError, TimeoutError) as e:
                answer = ""
                row["infra_error"] = f"{type(e).__name__}: {e}"
            row["elapsed_s"] = round(time.time() - t0, 2)
            row["route"] = route_of(row["agent"], args.target)
            row["answer"] = answer
            failed = grade(answer, q)
            if row["infra_error"]:
                failed = [{"kind": "infra", "detail": row["infra_error"]}]
            row["pass"] = not failed
            row["failed"] = failed
            row["answer_normalized"] = normalize(answer) if answer else ""
            f.write(json.dumps(row, ensure_ascii=False) + "\n")
            f.flush()
            rows.append(row)
            mark = "PASS" if row["pass"] else "FAIL"
            tag = f"{q['id']}#{sample + 1}" if args.samples > 1 else q["id"]
            print(f"{mark} {tag:<30} {row['route']:<8} {row['agent'] or '-':<22} {row['elapsed_s']:>6.1f}s"
                  + ("" if row["pass"] else "  " + "; ".join(x["kind"] for x in failed)), flush=True)

    end = datetime.now(timezone.utc)
    meta = {"window": {"start": start.isoformat(timespec="seconds"), "end": end.isoformat(timespec="seconds")},
            "target": args.target, "endpoint": endpoint, "source": args.source if args.target == "deployed" else None,
            "model": model, "system_file": args.system_file, "samples": args.samples, "status": status}
    return write_summary(rows, meta, qpath, qbytes, out_dir)


def regrade(answers_path, questions, qpath, qbytes, out_dir):
    try:
        rows = [json.loads(line) for line in answers_path.read_text(encoding="utf-8").splitlines() if line.strip()]
    except (OSError, json.JSONDecodeError) as e:
        print(f"infrastructure failure: cannot read {answers_path}: {e}", file=sys.stderr)
        return 2
    by_id = {q["id"]: q for q in questions}
    old = answers_path.with_name("summary.json")
    meta = json.loads(old.read_text(encoding="utf-8")) if old.exists() else {}
    meta = {k: meta.get(k) for k in ("window", "target", "endpoint", "source", "model", "system_file", "samples",
                                     "status")}
    meta["regraded_from"] = str(answers_path)
    kept = []
    with (out_dir / "answers.jsonl").open("w", encoding="utf-8") as f:
        for row in rows:
            q = by_id.get(row["id"])
            if q is None:
                continue  # question retired since that run
            failed = ([{"kind": "infra", "detail": row["infra_error"]}] if row.get("infra_error")
                      else grade(row.get("answer", ""), q))
            row.update(topic=q["topic"], q=q["q"], failed=failed, **{"pass": not failed})
            f.write(json.dumps(row, ensure_ascii=False) + "\n")
            kept.append(row)
    return write_summary(kept, meta, qpath, qbytes, out_dir)


def per_question(rows):
    """Pass count per question id over its samples, plus how many questions always/never/sometimes pass."""
    counts = {}
    for r in rows:
        b = counts.setdefault(r["id"], {"pass": 0, "samples": 0})
        b["samples"] += 1
        b["pass"] += bool(r["pass"])
    stability = {"always_pass": sum(b["pass"] == b["samples"] for b in counts.values()),
                 "never_pass": sum(b["pass"] == 0 for b in counts.values()),
                 "mixed": sum(0 < b["pass"] < b["samples"] for b in counts.values())}
    return counts, stability


def write_summary(rows, meta, qpath, qbytes, out_dir):
    infra = sum(1 for r in rows if r.get("infra_error"))
    passed = sum(1 for r in rows if r["pass"])
    counts, stability = per_question(rows)
    summary = {
        "schema": "theory-qa-summary/v1",
        **meta,
        "questions_file": str(qpath), "questions_sha256": hashlib.sha256(qbytes).hexdigest(),
        # totals count answers: with --samples N each question contributes N rows
        "totals": {"questions": len(counts), "answers": len(rows), "pass": passed, "fail": len(rows) - passed,
                   "infra_errors": infra, "pass_rate": round(passed / len(rows), 3) if rows else None},
        "stability": stability,
        "per_question": counts,
        "latency_s": latency_stats([r["elapsed_s"] for r in rows]),
        "by_topic": tally(rows, "topic"),
        "by_route": tally(rows, "route"),
        "by_agent": tally(rows, "agent"),
        "questions": [{"id": r["id"], "sample": r.get("sample", 0), "topic": r["topic"], "route": r["route"],
                       "agent": r["agent"], "method": r["method"], "pass": r["pass"], "failed": r["failed"],
                       "elapsed_s": r["elapsed_s"]}
                      for r in rows],
    }
    (out_dir / "summary.json").write_text(json.dumps(summary, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    w = summary.get("window") or {}
    print(f"\n{passed}/{len(rows)} pass, {infra} infra errors; window {w.get('start')} .. {w.get('end')}; out: {out_dir}")
    if len(rows) > len(counts):
        print(f"{len(counts)} questions: {stability['always_pass']} always pass, {stability['never_pass']} never pass, "
              f"{stability['mixed']} mixed")
    return 2 if infra else 0


if __name__ == "__main__":
    sys.exit(main())
