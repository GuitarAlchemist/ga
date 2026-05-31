# Algedonic Channel — Cross-Repo Contract

**Version:** 0.1.0 (draft, pending sign-off)
**Schema:** `algedonic-signal-v0.1.0` — JSON Schema at `docs/contracts/algedonic-signal.schema.json`
**Status:** Draft (Phase 0 of the VSM algedonic-channel rollout, 2026-05-24)
**Producers:** any repo in the GuitarAlchemist ecosystem — `ga`, `ix`, `Demerzel`, `tars`, `sentrux`, `hari` — plus the GA-side `.github/workflows/ecosystem-health.yml` pull-mode bridge
**Consumers:** GA dashboard `/dev-data/algedonic` middleware (Heartbeat algedonic tile); `.claude/skills/council/SKILL.md` (critical-severity auto-invoke trigger, documented here, wired in a follow-up); future on-call pager
**Storage:** `state/algedonic/inbox.jsonl` (append-only, one signal per line)

---

## 1. Why This Contract Exists

Stafford Beer's Viable System Model defines an **algedonic channel** as the alarm
path that bypasses normal communication channels to deliver pleasure/pain signals
directly to higher management. Normal channels carry routine telemetry (quality
snapshots, PR comments, commit feed). The algedonic channel carries the things
the operator *needs to know now* — and that today are mostly invisible because
each repo is an island:

- IX corrupts the OPTIC-K index → GA voicing search silently degrades.
- tars flags grammar/theory drift → no surface in GA.
- Demerzel Articles trigger → only visible if the operator opens the Demerzel
  repo's logs.
- Sentrux regression gate fails → known only to whoever was running it.

Today the operator discovers these by accident or by browsing each repo's CI
results manually. The algedonic channel turns those discoveries into a single
push-mode inbox that GA's dashboard renders.

This contract is a **one-way door**: cross-repo schema with multiple producers
in multiple languages (C#, F#, Rust, Python, PowerShell). Once a sibling repo
emits against `algedonic-signal-v0.1.0`, renaming a field requires coordinated
migration across every emitter. We keep the schema at v0.1.x while emitters
land; we freeze at v1.0.0 at the Phase 4 milestone (Section 9).

The `supersedes` field below is the non-breaking baseline-shift escape valve:
new versions of long-lived signals can chain back through prior IDs without
forcing schema bumps.

---

## 2. JSON Shape

```json
{
  "id": "01HZ8YQ9V3K7XW2N4M8P1F5R6T",
  "schema": "algedonic-signal-v0.1.0",
  "emitted_at": "2026-05-24T15:04:05Z",
  "repo": "ix",
  "source": "ix_governance_check",
  "severity": "fail",
  "summary": "Article 14 violated by PR #43 — install-audit drift > 7 deductions",
  "details": "## What\n\nArticle 14 caps deductions at 7. The post-merge audit reports **8**.\n\n```\n- install-audit: 71 → 110 (Δ+39)\n```\n\n## Why it matters\n\nGate is a Demerzel one-way door. Reverting requires re-running the install audit baseline on every consumer repo.\n",
  "evidence_url": "https://github.com/GuitarAlchemist/ix/actions/runs/12345678",
  "affected_artifacts": [
    "ix/state/audit/install-audit-2026-05-24.json",
    "governance/demerzel/articles/14-install-audit.md"
  ],
  "ttl_hours": 24,
  "escalation": {
    "on_unack_after_hours": 4,
    "route_to": "operator"
  },
  "ack": {
    "acked": false,
    "acked_by": null,
    "acked_at": null,
    "resolution": null
  },
  "supersedes": []
}
```

---

## 3. Field Reference

| Field | Type | Required | Notes |
|---|---|---|---|
| `id` | string | yes | UUIDv7 (preferred; sortable by time) or ULID. ≤ 36 chars. Used as the URL segment for `/dev-data/algedonic/ack/<id>` — must be filename-safe (URL-safe base32/hex). |
| `schema` | const string | yes | Always `algedonic-signal-v0.1.0` for v0.1.x. Bump only at Phase 4 (see §9). |
| `emitted_at` | RFC3339 string | yes | UTC. Truncate to seconds. |
| `repo` | enum | yes | One of `ga`, `ix`, `demerzel`, `tars`, `sentrux`, `hari`. New repos added by PR amending this contract + the schema enum. |
| `source` | string | yes | Free-form producer slug. Examples: `ix_governance_check`, `qa-architect-tribunal`, `ecosystem-health-poller`, `sentrux_rescan`, `manual`. ≤ 64 chars. |
| `severity` | enum | yes | `info` \| `warn` \| `fail` \| `critical`. See §4. |
| `summary` | string | yes | ≤ 140 chars. Human-readable single-line; the *what*. Renders in the dashboard tile and in any future pager message. |
| `details` | string | yes (may be empty `""`) | Markdown. The *why* + reproduction + remediation hint. Can include fenced code blocks. No size cap, but consumers may truncate to ~4 KB for display. |
| `evidence_url` | string | optional | Git URL, dashboard URL, CI run URL, log URL. Used to deep-link from the dashboard tile. |
| `affected_artifacts` | array<string> | yes (may be empty `[]`) | Repo-relative paths, git refs, or stable artifact IDs. The `/council` auto-invoke trigger (§7) uses this as the "PR diff" surrogate. |
| `ttl_hours` | int | yes | Default `24`. Consumers MAY garbage-collect acked signals older than `acked_at + ttl_hours`. Unacked signals are never garbage-collected. |
| `escalation` | object | yes | See §5. |
| `ack` | object | yes | See §6. New signals always emit with `ack.acked = false`. |
| `supersedes` | array<string> | yes (may be empty `[]`) | Other signal IDs this one replaces. Consumers SHOULD hide superseded signals from the unacked list. See §8 for the chain pattern. |

### 3.1 `escalation`

| Field | Type | Notes |
|---|---|---|
| `on_unack_after_hours` | int \| null | If non-null and the signal stays unacked for this many hours past `emitted_at`, the consumer SHOULD escalate via `route_to`. `null` disables escalation (use for `info` signals). |
| `route_to` | enum | `operator` \| `council` \| `qa-architect` \| `on-call`. The dashboard treats all routes as "surface harder" until per-route delivery integrations exist. |

### 3.2 `ack`

| Field | Type | Notes |
|---|---|---|
| `acked` | bool | `false` at emit time. Flipped to `true` by the consumer (dashboard ack button, CLI ack helper, or remediation PR landing). |
| `acked_by` | string \| null | Free-form: GitHub handle, agent slug, automation slug. |
| `acked_at` | RFC3339 string \| null | UTC. |
| `resolution` | string \| null | ≤ 280 chars. Free-form note: "fixed in #320", "false positive, investigating", "deferred to next sprint". |

---

## 4. Severity Semantics

| Severity | Operator action expected | Dashboard treatment |
|---|---|---|
| `info` | FYI; surfaces but doesn't escalate. | Counted in badge; not in top-3 list unless nothing higher exists. |
| `warn` | Should look at it before next session. | Counted in badge; eligible for top-3 list. |
| `fail` | Should look at it within the hour; blocks dependent work. | Counted in badge; bumped to top of top-3 list. |
| `critical` | Drop everything. Auto-fires `/council` skill (§7); pages on-call when delivery is wired. | Red banner across the top of the Heartbeat (impossible to miss). |

Severity is the producer's call. If you're not sure, err one notch lower — false
`critical` signals burn down the operator's attention budget faster than missed
`warn` signals.

---

## 5. Writer Convention

Every producer:

1. Generates a UUIDv7 (or ULID) for `id`.
2. Constructs the signal per §2.
3. Validates against `docs/contracts/algedonic-signal.schema.json` (helpers in
   §10 do this for you).
4. Appends a single line of compact JSON (no embedded newlines; use `\n` in
   `details` markdown) to `state/algedonic/inbox.jsonl`.
5. Never rewrites existing lines. Acks and supersedes are themselves new lines
   (the inbox is event-sourced; the dashboard projects the latest state).

**Concurrency.** Multiple writers may append simultaneously. POSIX/NTFS append
mode on small writes (< 4 KB) is line-atomic in practice on every supported
platform. For larger payloads (rare for an algedonic signal), use a lock file
at `state/algedonic/.inbox.lock`.

**Acks are signals.** An ack is a new line with the same `id` and `ack.acked = true`,
populated `ack.acked_by` / `ack.acked_at`, and an empty `supersedes`. The
projector takes the latest line per `id` as the current state.

---

## 6. Reader Convention (Projection)

To compute "the current state of the inbox":

1. Read `state/algedonic/inbox.jsonl` line by line. Skip blank lines and lines
   that fail JSON parse (log a warn; don't crash).
2. Group by `id`. The *latest* line per `id` (latest `emitted_at` among lines
   that share an `id`, or last-write-wins on ties) is the canonical record.
3. Drop records where the canonical `id` appears in any other record's
   `supersedes`.
4. Apply TTL: records with `ack.acked == true` and `acked_at + ttl_hours <= now`
   are garbage-collectable but not yet deleted (the inbox is append-only).
   Consumers MAY filter them out.

The dashboard `/dev-data/algedonic` endpoint implements this projection.

---

## 7. `/council` Auto-Invocation Trigger

When a signal with `severity == "critical"` lands in the inbox, the `/council`
skill SHOULD auto-fire with the signal's `affected_artifacts` standing in for
the PR diff. This contract documents the integration point; the auto-invocation
wiring is a follow-up.

The skill's `Auto-invocation triggers` section (in
`.claude/skills/council/SKILL.md`) carries the operational details. This PR
ships the documentation; the wiring is a small follow-up because it requires
deciding *where* the watcher runs (Vite middleware, a sidecar process, or
inside the next session-start hook).

When the wiring lands, the contract between the watcher and `/council` is:

- Watcher invokes `/council --signal <id>` instead of `/council <PR#>`.
- The skill reads the signal from `state/algedonic/inbox.jsonl`, treats
  `affected_artifacts` as the door-touch set, and proceeds from Step 3
  (advisor convening).
- The synthesized verdict is appended back to the inbox as an `info` signal
  with `supersedes: [<original-id>]` and the council's `resolution` text.

---

## 8. Supersedes Pattern (Non-Breaking Baseline Shifts)

Long-lived signals (e.g., "OPTIC-K dim drift > 0.02") can re-emit with the same
*content* but different *baseline expectations* without bumping the schema:

```text
id=A : "OPTIC-K cosine drift = 0.024 vs baseline 0.000" — severity=warn
id=B : "OPTIC-K cosine drift = 0.024 vs new-baseline 0.020" — severity=info,
       supersedes=[A]
```

The dashboard hides `A` once `B` lands. The audit trail is preserved in the
JSONL.

When the time comes to redesign the signal shape, the new-shape signals can
`supersedes` the old-shape ones to migrate consumers without losing history.
That's our cheaper alternative to a full schema bump until Phase 4.

---

## 9. Versioning + Phase Plan

- **v0.1.x — draft (Phase 0–3).** Cross-repo schema; emitters may land. May
  break with coordinated migration.
- **v1.0.0 — frozen (Phase 4).** Requires sign-off from GA, IX, Demerzel
  maintainers. After freeze, only additive changes (new optional fields, new
  enum variants) until v2.0.0.
- **v2.0.0 — breaking.** Coordinated migration of `state/algedonic/inbox.jsonl`
  archives + every producer.

Phase milestones (target dates indicative, not committed):

| Phase | Milestone | Owner |
|---|---|---|
| 0 | Contract + schema + GA writer/reader + dashboard tile + pull-mode poller (this PR) | GA |
| 1 | IX-side emitter (`scripts/algedonic_emit.py` + governance hooks) | IX |
| 2 | Demerzel emitter on QA-Architect Tribunal `block` verdicts (per §11) | Demerzel |
| 3 | tars + sentrux + hari emitters; `/council` auto-invocation wired | tars / sentrux / hari / GA |
| 4 | Schema freeze at v1.0.0 | All |

---

## 10. Producer Helper Stubs

This PR ships the PowerShell helper only:

- **`Scripts/algedonic-emit.ps1`** (GA-side, PowerShell 7+) — UUIDv7
  generation, schema validation, append-with-retry to
  `state/algedonic/inbox.jsonl`. Used by the GA pull-mode poller and by any
  PowerShell-side script (e.g., quality snapshot post-processors).

The sibling-language helpers are deliberately NOT in this PR (each is a one-PR
job in its host repo, owned by that repo's maintainer):

- **`scripts/algedonic_emit.py`** (IX, sentrux, hari) — same contract, Python.
- **`scripts/algedonic_emit.fsx`** (tars) — same contract, F# script.
- **Demerzel emitter** — language TBD (Rust or IXQL helper).

Each helper MUST:

1. Generate a UUIDv7 / ULID for `id`.
2. Validate against the JSON Schema.
3. Append a single compact-JSON line to the inbox.
4. Exit 0 on success; non-zero with a stderr message on validation failure.
5. Be safe to call concurrently (use line-atomic append or a lock file).

---

## 11. Demerzel QA Verdict Integration

The companion contract `docs/contracts/2026-05-02-qa-verdict.contract.md`
defines the QA Architect Tribunal's verdict shape. To close the loop with the
algedonic channel:

> When the QA Architect Tribunal issues a `verdict: "block"` verdict, the
> dispatcher MUST also emit an algedonic signal via this contract with:
>
> - `repo: "demerzel"`
> - `source: "qa-architect-tribunal"`
> - `severity: "critical"` (block is by definition drop-everything)
> - `summary`: the verdict's `narrative` truncated to 140 chars
> - `details`: a markdown rendering of the verdict's `followups` array
> - `evidence_url`: the verdict's `links.pr` (or the verdict file path if no PR)
> - `affected_artifacts`: the verdict's `blast_radius.components_reached`

The Demerzel-side emit lands in a follow-up PR in the Demerzel repo. This
contract documents the integration so both sides can land independently.

---

## 12. Storage + Retention

- **Inbox file.** `state/algedonic/inbox.jsonl` — append-only, one JSON object
  per line, no trailing newline required.
- **Initial state.** This PR seeds the inbox with one synthetic `info` signal
  so the dashboard tile renders something on first paint. The seed signal
  carries `source: "manual"`, `summary` starting with `[seed]`, and is acked
  the first time the operator clicks it.
- **Retention.** Acked signals older than `acked_at + ttl_hours` are garbage-
  collectable. The collector is not in this PR — the inbox can grow to ~10 MB
  / ~50,000 records before it becomes inconvenient (line-based reads stay
  fast). A future maintenance script will compact into
  `state/algedonic/archive/<year>/<month>.jsonl.gz`.
- **Security.** The inbox is public-by-design (same as `state/quality/`). Never
  put secrets, PII, or private user data in any field. The dashboard endpoint
  enforces `isLocalOrigin` (Cloudflare tunnel blocks remote acks; the read
  endpoint is left local-only because writes are local-only).

---

## 13. Open Questions (resolve before v1.0.0)

- **Q1.** Does `id` allow UUIDv4 as a fallback, or strictly UUIDv7/ULID? (Phase 0
  accepts any string ≤ 36 chars that's filename-safe; Phase 4 may tighten.)
- **Q2.** Should `details` support a structured `attachments[]` array for
  log/trace excerpts, or stay markdown? (Markdown wins on simplicity until a
  producer proves otherwise.)
- **Q3.** Per-route delivery: when does `escalation.route_to == "on-call"`
  actually page a phone? (Out of scope for v0.1.x; documented as a hook.)
- **Q4.** Should acks themselves carry a `severity` so an operator can mark a
  signal as "false positive — downgrade to info"? (Add as an optional
  `ack.downgrade_severity` enum in v0.2.x if real demand emerges.)
- **Q5.** Where do the language helpers live for cross-repo reuse — vendored
  per repo (current plan) or a `guitar-alchemist/contracts-tools` repo?

---

## 14. Rollout Plan (other-repo PRs, follow-ups)

This PR ships GA-side only. The follow-up PRs:

- **IX PR.** Add `scripts/algedonic_emit.py`; wire `ix_governance_check`,
  `ix-quality-trend`, and the OPTIC-K rebuild post-step to emit on failure.
  Estimated 1 day.
- **Demerzel PR.** Wire the QA Architect Tribunal dispatcher to emit per §11.
  Estimated 1 day.
- **tars PR.** Add `scripts/algedonic_emit.fsx`; wire the grammar-drift
  detector. Estimated 0.5 day.
- **sentrux PR.** Add Python emitter; wire the regression gate. Estimated
  0.5 day.
- **hari PR.** Add Python emitter; wire the belief-state contradiction
  detector. Estimated 0.5 day.
- **GA follow-up.** Wire `/council` auto-invocation per §7. Estimated 0.5 day.

Total: ~4 person-days across 5 repos. The GA-side pull-mode poller
(`.github/workflows/ecosystem-health.yml`) keeps the dashboard useful even
before any sibling PR lands.
