"""Normalize SKILL.md frontmatter to camelCase (Anthropic Claude Code spec).

Why
---
Historically GA SKILL.md files use PascalCase top-level keys (`Name:`,
`Description:`, `Triggers:`). PR #113 made the parser accept both, but a
shared convention is better. Anthropic's published spec is camelCase, and
that's the convention every other YAML-based plugin ecosystem (GitHub
Actions, Helm, etc.) uses. This script migrates existing files in-place
without risking touching body markdown that incidentally starts with an
uppercase word followed by a colon (e.g. `User:`, `Steps:`, `Verify:`).

Algorithm
---------
For each `SKILL.md` under `skills/` and `.agent/skills/`:

1. Read the file as lines.
2. Find the FIRST line that is exactly `---`. The file must start with that
   delimiter (otherwise it has no frontmatter and is left alone).
3. Find the SECOND line that is exactly `---`. Everything between is the
   frontmatter; everything after is body markdown.
4. Within the frontmatter only, lowercase the first character of any of
   these top-level keys (no leading whitespace, key followed by `:`):
   `Name`, `Description`, `Triggers`. These are the only PascalCase keys
   GA's parser specifically reads; other top-level keys are already
   lowercase (`license`, `compatibility`, `metadata`).
5. Write the file back. Idempotent.

Run
---
    python Scripts/normalize_skill_md_casing.py            # dry-run by default
    python Scripts/normalize_skill_md_casing.py --apply    # actually rewrite
"""

from __future__ import annotations

import argparse
import re
import sys
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parent.parent
SKILL_DIRS = [REPO_ROOT / "skills", REPO_ROOT / ".agent" / "skills"]
TARGET_KEYS = {"Name", "Description", "Triggers"}
FRONTMATTER_DELIMITER = "---"
KEY_RE = re.compile(r"^([A-Z][a-zA-Z0-9_-]*)(\s*:.*)$")


def normalize_file(path: Path) -> tuple[str, str] | None:
    """Return (before, after) when changes are needed, else None."""
    original = path.read_text(encoding="utf-8")
    lines = original.splitlines(keepends=True)

    # No frontmatter → nothing to do.
    if not lines or lines[0].rstrip() != FRONTMATTER_DELIMITER:
        return None

    # Find closing delimiter.
    closing_idx = -1
    for i in range(1, len(lines)):
        if lines[i].rstrip() == FRONTMATTER_DELIMITER:
            closing_idx = i
            break
    if closing_idx < 0:
        return None

    changed = False
    for i in range(1, closing_idx):
        line = lines[i]
        # Only touch lines with no leading whitespace — top-level keys.
        if line and line[0].isspace():
            continue
        match = KEY_RE.match(line)
        if not match:
            continue
        key = match.group(1)
        if key not in TARGET_KEYS:
            continue
        # Lowercase first character. e.g. "Name" -> "name".
        new_key = key[0].lower() + key[1:]
        new_line = new_key + match.group(2)
        if line.endswith("\r\n"):
            new_line += "\r\n"
        elif line.endswith("\n"):
            new_line += "\n"
        if new_line != line:
            lines[i] = new_line
            changed = True

    if not changed:
        return None
    return original, "".join(lines)


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--apply", action="store_true",
                        help="Rewrite files in-place. Without this flag the "
                             "script only reports what would change.")
    args = parser.parse_args()

    rewrites: list[Path] = []
    for skill_dir in SKILL_DIRS:
        if not skill_dir.exists():
            continue
        for path in sorted(skill_dir.rglob("SKILL.md")):
            result = normalize_file(path)
            if result is None:
                continue
            rel = path.relative_to(REPO_ROOT)
            print(f"  {'apply' if args.apply else 'would change'}: {rel}")
            rewrites.append(path)
            if args.apply:
                path.write_text(result[1], encoding="utf-8", newline="")

    if not rewrites:
        print("No SKILL.md files needed normalisation.")
        return 0

    if args.apply:
        print(f"\nRewrote {len(rewrites)} file(s).")
    else:
        print(f"\n{len(rewrites)} file(s) would change. Re-run with --apply.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
