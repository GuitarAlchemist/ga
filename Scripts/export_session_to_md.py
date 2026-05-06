"""
Export a Claude Code session JSONL log to readable markdown.

Filters out tool calls, tool results, thinking blocks, system reminders, hooks
and attachment noise. Keeps the user/assistant text exchange — what a colleague
would want to read.

Usage:
    python Scripts/export_session_to_md.py <input.jsonl> <output.md>
"""
from __future__ import annotations

import json
import re
import sys
from datetime import datetime, timezone
from pathlib import Path


SYSTEM_REMINDER_RE = re.compile(
    r"<system-reminder>.*?</system-reminder>", re.DOTALL
)
COMMAND_TAG_RE = re.compile(
    r"<(command-name|command-message|command-args|local-command-stdout|"
    r"user-prompt-submit-hook|stdin|ide_selection|ide_opened_file)>.*?"
    r"</\1>",
    re.DOTALL,
)
SLASH_HEADER_RE = re.compile(r"^<.*?>\s*$", re.MULTILINE)


def clean_user_text(text: str) -> str:
    text = SYSTEM_REMINDER_RE.sub("", text)
    text = COMMAND_TAG_RE.sub("", text)
    text = SLASH_HEADER_RE.sub("", text)
    return text.strip()


def fmt_ts(iso: str) -> str:
    try:
        dt = datetime.fromisoformat(iso.replace("Z", "+00:00")).astimezone(timezone.utc)
        return dt.strftime("%Y-%m-%d %H:%M UTC")
    except Exception:
        return iso


def extract_text(message: dict) -> list[str]:
    """Pull text blocks out of a message. Skip thinking/tool_use/tool_result."""
    content = message.get("content", "")
    if isinstance(content, str):
        return [content] if content.strip() else []
    if not isinstance(content, list):
        return []
    out: list[str] = []
    for c in content:
        if not isinstance(c, dict):
            continue
        if c.get("type") == "text":
            t = c.get("text", "")
            if t.strip():
                out.append(t)
    return out


def main() -> int:
    if len(sys.argv) != 3:
        print(f"usage: {sys.argv[0]} <input.jsonl> <output.md>", file=sys.stderr)
        return 2
    src = Path(sys.argv[1])
    dst = Path(sys.argv[2])
    if not src.exists():
        print(f"FAIL: {src} not found", file=sys.stderr)
        return 2

    turns: list[tuple[str, str, str]] = []  # (role, ts, text)
    n_user = n_assistant = n_skipped = 0

    with src.open("r", encoding="utf-8") as f:
        for line in f:
            line = line.strip()
            if not line:
                continue
            try:
                d = json.loads(line)
            except json.JSONDecodeError:
                continue

            t = d.get("type")
            if t not in ("user", "assistant"):
                continue
            if d.get("isSidechain"):
                # Subagent transcript — skip; keeps the export focused on the
                # main user<->assistant thread.
                continue

            ts = d.get("timestamp", "")
            msg = d.get("message", {})
            texts = extract_text(msg)
            if not texts:
                n_skipped += 1
                continue

            for raw in texts:
                cleaned = clean_user_text(raw)  # Same cleaner is safe for both roles.
                if not cleaned:
                    n_skipped += 1
                    continue
                if t == "user":
                    turns.append(("user", ts, cleaned))
                    n_user += 1
                else:
                    turns.append(("assistant", ts, cleaned))
                    n_assistant += 1

    if not turns:
        print("FAIL: no user/assistant text found", file=sys.stderr)
        return 1

    first_ts = fmt_ts(turns[0][1])
    last_ts = fmt_ts(turns[-1][1])

    with dst.open("w", encoding="utf-8") as f:
        f.write(f"# Claude Code session — {src.name}\n\n")
        f.write(
            f"Span: **{first_ts}** → **{last_ts}**  \n"
            f"Turns: {n_user} user / {n_assistant} assistant "
            f"(skipped {n_skipped} empty/tool-only frames)  \n"
            f"Source: `{src}`\n\n"
        )
        f.write(
            "Tool calls, tool results, thinking blocks, system reminders, "
            "hooks, IDE selection markers, and subagent sidechains are filtered "
            "out. What remains is the user/assistant text exchange.\n\n"
        )
        f.write("---\n\n")

        for role, ts, text in turns:
            label = "User" if role == "user" else "Claude"
            f.write(f"### {label} — {fmt_ts(ts)}\n\n")
            # Quote user prose; leave assistant prose unquoted so its own
            # markdown (lists, code fences, headings) renders properly.
            if role == "user":
                quoted = "\n".join("> " + l if l else ">" for l in text.splitlines())
                f.write(quoted + "\n\n")
            else:
                f.write(text + "\n\n")
            f.write("---\n\n")

    print(f"wrote {dst}  ({n_user + n_assistant} turns, {dst.stat().st_size:,} bytes)")
    return 0


if __name__ == "__main__":
    sys.exit(main())
