# Issue tracker: GitHub

Issues and PRDs for this repo live as GitHub issues in `GuitarAlchemist/ga`. Use the `gh` CLI for all operations.

## Conventions

- **Create an issue**: `gh issue create --title "..." --body "..."`. Use a heredoc for multi-line bodies.
- **Read an issue**: `gh issue view <number> --comments`.
- **List issues**: `gh issue list --state open --json number,title,body,labels,comments --jq '[.[] | {number, title, body, labels: [.labels[].name], comments: [.comments[].body]}]'`.
- **Comment**: `gh issue comment <number> --body "..."`
- **Apply / remove labels**: `gh issue edit <number> --add-label "..."` / `--remove-label "..."`
- **Close**: `gh issue close <number> --comment "..."`

Infer the repo from `git remote -v` — `gh` does this automatically inside a clone.

> **Before merging any PR**, read Codex bot comments per CLAUDE.md — Codex P0/P1
> are not surfaced in the standard merge flow and must be addressed.

## When a skill says "publish to the issue tracker"
Create a GitHub issue.

## When a skill says "fetch the relevant ticket"
Run `gh issue view <number> --comments`.
