#!/usr/bin/env python3
"""Patch broken internal markdown links in LIVE docs (not frozen history).

For each broken relative link:
  - if its raw target is in REDIRECTS, rewrite to the new target;
  - otherwise DELINKIFY: turn [text](dead-target) into plain `text`,
    so the 404 link becomes readable text (no broken navigation).

Operates on the working tree. Skips frozen point-in-time records
(archive/plans/reports/solutions/history/brainstorms) — their decayed links
are expected. Prints a summary; re-run audit-doc-links.py to verify.
"""
import os, re, posixpath, sys

FROZEN_PREFIXES = ("docs/archive/", "docs/plans/", "docs/reports/",
                   "docs/solutions/", "docs/history/", "docs/brainstorms/")

# Known moves (raw link target as it appears in markdown -> new target).
REDIRECTS = {
    "../plans/2026-05-06-skills-orchestration-architecture.md":
        "../plans/parked/2026-05-06-skills-orchestration-architecture.md",
    "./Architecture/": "./architecture/",
}

# Build existence sets from the working tree.
tracked = set()
for root, _dirs, files in os.walk("."):
    for f in files:
        p = os.path.relpath(os.path.join(root, f), ".").replace("\\", "/")
        tracked.add(p)
dirs = set()
for p in tracked:
    parts = p.split("/")
    for i in range(1, len(parts)):
        dirs.add("/".join(parts[:i]))

def exists(resolved):
    return resolved in tracked or resolved in dirs

LINK = re.compile(r"\[([^\]]*)\]\(\s*([^)\s]+?)\s*(?:\"[^\"]*\")?\)")

def resolve(docdir, target):
    path_part = target.split("#", 1)[0]
    if not path_part:
        return None  # pure anchor
    if path_part.startswith("/"):
        return path_part.lstrip("/")
    return posixpath.normpath(posixpath.join(docdir, path_part))

docs = sorted(p for p in tracked if p.startswith("docs/") and p.endswith(".md")
              and not p.startswith(FROZEN_PREFIXES))

total_redirect = total_delink = 0
changed_files = []
for doc in docs:
    docdir = posixpath.dirname(doc)
    with open(doc, encoding="utf-8") as fh:
        text = fh.read()

    def repl(m):
        global total_redirect, total_delink
        label, target = m.group(1), m.group(2)
        if target.startswith(("http://", "https://", "mailto:", "#", "//")):
            return m.group(0)
        resolved = resolve(docdir, target)
        if resolved is None or exists(resolved):
            return m.group(0)  # valid (or same-page anchor) -> keep
        if target in REDIRECTS:
            new_resolved = resolve(docdir, REDIRECTS[target])
            if new_resolved and exists(new_resolved):
                total_redirect += 1
                return f"[{label}]({REDIRECTS[target]})"
        # Dead target -> delinkify (keep the label as plain text).
        total_delink += 1
        return label

    new_text = LINK.sub(repl, text)
    if new_text != text:
        with open(doc, "w", encoding="utf-8", newline="\n") as fh:
            fh.write(new_text)
        changed_files.append(doc)

print(f"redirected={total_redirect}  delinkified={total_delink}  files_changed={len(changed_files)}")
for f in changed_files:
    print("  M", f)
