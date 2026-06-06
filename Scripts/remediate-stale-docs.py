#!/usr/bin/env python3
"""Apply the docs-staleness-audit remediation (run from repo root).

Routes each STALE/SUPERSEDED finding by its recommended_action:
  delete         -> remove the file
  archive        -> move under docs/archive/<original-subpath>
  mark-superseded-> prepend a SUPERSEDED banner
  reverify       -> prepend a STALE (pending re-verification) banner
  update         -> prepend a PARTIALLY STALE banner (except hand-fixed docs)

Skips anything already under docs/archive/ for banner/update actions
(frozen history). Hand-fixed docs are left for precise manual edits.
"""
import json, os, posixpath

FINDINGS = "docs-staleness-findings.json"
HANDFIX = {"docs/AGENTS.md", "docs/runbooks/non-admin-service-install.md"}
DATE = "2026-05-31"

findings = json.load(open(FINDINGS, encoding="utf-8"))

def first_evidence(d):
    ev = d.get("evidence") or []
    return (ev[0] if ev else "References code/state that no longer matches the repo.").strip()

def banner(kind, reason):
    if kind == "superseded":
        return (f"> ⚠️ **SUPERSEDED (audited {DATE}).** {reason} "
                f"Treat as historical; see [architecture/README.md](architecture/README.md) "
                f"(or the topic's current doc) for the up-to-date picture.\n\n")
    if kind == "reverify":
        return (f"> ⚠️ **STALE — pending re-verification (audited {DATE}).** {reason} "
                f"Verify against the current code before relying on this doc.\n\n")
    return (f"> ⚠️ **PARTIALLY STALE (audited {DATE}).** {reason} "
            f"Some specifics below no longer match the code.\n\n")

def prepend_banner(path, text):
    with open(path, encoding="utf-8") as f:
        c = f.read()
    # Insert after a leading YAML frontmatter block if present.
    if c.startswith("---"):
        end = c.find("\n---", 3)
        if end != -1:
            nl = c.find("\n", end + 1)
            head, rest = c[:nl + 1], c[nl + 1:]
            new = head + "\n" + text + rest.lstrip("\n")
            with open(path, "w", encoding="utf-8", newline="\n") as f:
                f.write(new)
            return
    with open(path, "w", encoding="utf-8", newline="\n") as f:
        f.write(text + c)

deleted, moved, bannered, skipped = [], [], [], []
for d in findings:
    p = d.get("path")
    if not p or d.get("verdict") not in ("STALE", "SUPERSEDED"):
        continue
    act = d.get("recommended_action")
    in_archive = p.startswith("docs/archive/")
    if act == "delete":
        if os.path.exists(p):
            os.remove(p); deleted.append(p)
    elif act == "archive":
        if in_archive:
            skipped.append(p); continue
        rel = p[len("docs/"):]
        dst = posixpath.join("docs/archive", rel)
        os.makedirs(posixpath.dirname(dst), exist_ok=True)
        if os.path.exists(p):
            os.replace(p, dst); moved.append((p, dst))
    elif act in ("mark-superseded", "reverify", "update"):
        if in_archive or p in HANDFIX:
            skipped.append(p); continue
        if not os.path.exists(p):
            skipped.append(p); continue
        kind = {"mark-superseded": "superseded", "reverify": "reverify"}.get(act, "update")
        prepend_banner(p, banner(kind, first_evidence(d)))
        bannered.append((p, kind))
    else:
        skipped.append(p)

print(f"deleted={len(deleted)} moved={len(moved)} bannered={len(bannered)} skipped={len(skipped)}")
print("\n-- DELETED --");      [print("  ", x) for x in deleted]
print("-- MOVED --");         [print(f"   {s} -> {dst}") for s, dst in moved]
print("-- BANNERED --");      [print(f"   [{k}] {p}") for p, k in bannered]
print("-- SKIPPED (archive/handfix/missing) --"); [print("  ", x) for x in skipped]
