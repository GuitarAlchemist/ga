# Guitar Alchemist — Documentation Index

Entry point for the GA documentation tree. Browse by area below; each
directory has its own docs.

> **Note (2026-05-31):** this index was rebuilt after a sweep found that the
> previous version linked to a generation of `*_COMPLETE.md` / `*_SUMMARY.md`
> docs that have since been deleted (every link 404'd). It now points only to
> directories and files that exist. Run `python Scripts/audit-doc-links.py`
> from the repo root to re-check `docs/` for broken internal links.
> (`INDEX.md` is an older, stale second index pending the same treatment — use
> this file as the canonical entry point.)

## Start here

- [QUICK_START.md](QUICK_START.md) — prerequisites, install, run the stack.
- [AGENTS.md](AGENTS.md) — repository guidelines for humans and coding agents.
- [architecture/README.md](architecture/README.md) — **authoritative architecture index**: what runs, what stores data, what serves chat, and the five-layer model.

## Browse by area

| Area | Directory | What's there |
|---|---|---|
| Architecture | [architecture/](architecture/) | System design, chat surfaces, app/process inventory, audit |
| Guides | [Guides/](Guides/) | How-to guides and feature walkthroughs |
| API | [API/](API/) | API documentation |
| Configuration | [Configuration/](Configuration/) | Config and settings docs |
| Testing | [Testing/](Testing/) | Test strategy and guides |
| Integration | [Integration/](Integration/) | Service/MCP integration docs |
| Performance | [Performance/](Performance/) | Performance and optimization notes |
| Chatbot | [chatbot/](chatbot/) | Chatbot runtime + roadmap |
| Contracts | [contracts/](contracts/) | Cross-repo JSON-on-disk contracts |
| Methodology | [methodology/](methodology/) | Invariants and methodology |
| Runbooks | [runbooks/](runbooks/) | Operational runbooks |
| Solutions | [solutions/](solutions/) | Compounded solution write-ups (dated records) |
| Plans | [plans/](plans/) | Dated implementation plans (point-in-time) |
| Reports | [reports/](reports/) | Dated reports (point-in-time) |
| References | [References/](References/) | Reference material |
| Archive | [archive/](archive/) | Retired docs and historical tracks |

## Conventions

- **Canonical docs use kebab-case** (`chat-surfaces.md`). Many root-level
  `SCREAMING_SNAKE.md` files are legacy and pending an archival sweep; prefer
  the kebab-case docs and the `architecture/` index when they overlap.
- **Dated records** (`plans/`, `reports/`, `solutions/`, `archive/`,
  `brainstorms/`) are point-in-time — read them as history, not current state.
- See [PROJECT_DOCUMENTATION_STANDARD.md](PROJECT_DOCUMENTATION_STANDARD.md)
  for the documentation standard and [architecture/README.md](architecture/README.md#conventions)
  for the per-doc frontmatter convention.
