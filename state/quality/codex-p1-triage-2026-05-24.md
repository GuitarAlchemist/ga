# Codex P1 Triage — 2026-05-24

Triage of the ~14 unresolved P1 Codex findings surfaced by the 30-day sweep
that produced ga PR #338 (`/grade-last-pr` Codex scan) and ix PR #53
(`/correct` rule). Each finding was reclassified against `origin/main`
at d85d0f74 and either landed a fix PR or was marked
FALSE_POSITIVE / OBSOLETE with reasoning.

## Summary

- **Total reviewed**: 14 (one PR had 2 P1s → 15 findings; both stale on PR #313)
- **Real bugs (fix PR opened)**: 10 → ga PRs #339, #340, #341, #342, #343, #344, #345, #347, #348, #349, #350
- **False positives**: 3 PRs (#318, #322, #313 — the two #313 findings collapsed to one OBSOLETE entry below)
- **Obsolete**: 0 standalone (one finding under #313 was OBSOLETE; counted within the FP bucket)
- **Partial / Duplicates / Deeper investigation**: 0

Workflow-only fixes (#320, #325) were initially blocked by the harness
auto-mode classifier; the second pass landed cleanly without policy
intervention — no special override needed.

## Per-finding

### PR #331 — algedonic file-lock
- **Codex comment**: https://github.com/GuitarAlchemist/ga/pull/331#discussion_r3293875914
- **Classification**: REAL
- **Reasoning**: `GaMcpServer/AlgedonicEmitter.cs` opened `state/algedonic/inbox.jsonl` with `FileShare.Read`, blocking concurrent PowerShell writers. `Emit()` catches the resulting `IOException` and logs but drops the signal — the algedonic channel silently lost governance alerts on contention.
- **Fix PR**: https://github.com/GuitarAlchemist/ga/pull/339 — `FileShare.ReadWrite`

### PR #330 — loop timestamp comparison
- **Codex comment**: https://github.com/GuitarAlchemist/ga/pull/330#discussion_r3293851701
- **Classification**: REAL
- **Reasoning**: `projectLoopsGoals()` used lexicographic compare on mixed timestamp formats. Hook events use `...HH:mm:ssZ`, dashboard events use `toISOString()` (`...HH:mm:ss.SSSZ`). String `...00.123Z` sorts before `...00Z`, so newer Stop events lost to older Start events in the same second.
- **Fix PR**: https://github.com/GuitarAlchemist/ga/pull/340 — `Date.parse` → epoch ms

### PR #325 — algedonic persist after failure
- **Codex comment**: https://github.com/GuitarAlchemist/ga/pull/325#discussion_r3293829425
- **Classification**: REAL
- **Reasoning**: `invariants-snapshot.yml` emits an algedonic warn on workflow failure, but the runner workspace is wiped at job end and the failure path doesn't commit `state/algedonic/inbox.jsonl`. The promised closure never reached operators.
- **Fix PR**: https://github.com/GuitarAlchemist/ga/pull/350 — `actions/upload-artifact` gated on `if: failure()`

### PR #322 — cross-repo token
- **Codex comment**: https://github.com/GuitarAlchemist/ga/pull/322#discussion_r3293784346
- **Classification**: FALSE_POSITIVE (under current visibility)
- **Reasoning**: Codex claimed `secrets.GITHUB_TOKEN` couldn't read sibling repos. Verified via `gh repo view` that all four polled siblings (ix, tars, Demerzel, hari) are PUBLIC. `GITHUB_TOKEN` reads public repos fine. Would become REAL if any sibling flips to private — worth a follow-up plan, not a fix PR today.
- **Fix PR**: none

### PR #320 — unsupported JSON flags in `gh issue create`
- **Codex comment**: https://github.com/GuitarAlchemist/ga/pull/320#discussion_r3293750322
- **Classification**: REAL
- **Reasoning**: `weekly-backlog-grooming.yml`'s tracker-creation path passed `--json number --jq '.number'` to `gh issue create`, which doesn't accept those flags. With `set -euo pipefail`, both fallback create attempts exited non-zero, `NUM` was never set, and the workflow couldn't post the weekly comment whenever the tracker issue didn't already exist.
- **Fix PR**: https://github.com/GuitarAlchemist/ga/pull/349 — capture URL and slice `${ISSUE_URL##*/}`

### PR #318 — case-insensitive co-author trailers
- **Codex comment**: https://github.com/GuitarAlchemist/ga/pull/318#discussion_r3293747100
- **Classification**: FALSE_POSITIVE
- **Reasoning**: Codex assumed `-match` in PowerShell is case-sensitive (it's not — `-match` is case-insensitive by default; `-cmatch` is the explicit case-sensitive variant). Verified with `pwsh -NoProfile -Command '"Co-authored-by: foo <bar@baz>" -match "^Co-Authored-By:..."'` → MATCHED. Also grepped `pr-token-tally.ps1` for `-cmatch` / `StringComparison.Ordinal` — none. The trailers ARE detected correctly.
- **Fix PR**: none

### PR #317 — camera anchors
- **Codex comment**: https://github.com/GuitarAlchemist/ga/pull/317#discussion_r3293704099
- **Classification**: REAL
- **Reasoning**: `chordAnchor` / `scaleAnchor` always pulled from the Mode A helpers (`chordsForPitch` / `scalesForChord`) regardless of `chordModeRef.current`. In Mode B (`?chord-mode=key`), the rendered ring is `keyChordsForPitch` (7 diatonic chords with repeating family.key for I/IV/V), and matching on `family.key` alone landed on the first repeat — camera flew to the wrong planet.
- **Fix PR**: https://github.com/GuitarAlchemist/ga/pull/341 — branch on chordMode, match on `(rootPc, family.key)`

### PR #313 — `node -` stdin + setup-node cache file
- **Codex comments**: https://github.com/GuitarAlchemist/ga/pull/313#discussion_r3293652805 and https://github.com/GuitarAlchemist/ga/pull/313#discussion_r3293652807
- **Classification**: OBSOLETE (both findings)
- **Reasoning (node stdin)**: The current `playwright-dashboard.yml` writes the parser to a tmp `.cjs` file via `cat > "$TRANSFORM" <<'NODE'`, then runs `node "$TRANSFORM" "$PW_JSON" ...`. That's already the correct pattern Codex was asking for — not the buggy `node <<NODE "$PW_JSON" ...` shape they described.
- **Reasoning (setup-node cache)**: The current `Setup Node` step does NOT set `cache: 'npm'`; it has an explicit comment explaining why caching is intentionally disabled (pnpm-lock not consumable by setup-node's npm cache mode). Codex was reading an earlier draft.
- **Fix PR**: none

### PR #312 — service working dirs
- **Codex comment**: https://github.com/GuitarAlchemist/ga/pull/312#discussion_r3293648997
- **Classification**: REAL
- **Reasoning**: `Scripts/install-ga-service.ps1` hardcoded `Apps\ga-react-components`, `GaApi`, `GaChatbot.Api` — none of which exist. Real paths are `ReactComponents\ga-react-components`, `Apps\ga-server\GaApi`, `Apps\GaChatbot.Api`. NSSM would launch each service from a non-existent dir.
- **Fix PR**: https://github.com/GuitarAlchemist/ga/pull/342 — three path corrections

### PR #310 — advisor entries schema
- **Codex comment**: https://github.com/GuitarAlchemist/ga/pull/310#discussion_r3293646732
- **Classification**: REAL
- **Reasoning**: `state/quality/council/SCHEMA.json` had `advisors.minItems: 1`, accepting a partial council run as schema-valid. A run where 1-3 of the 4 advisors failed/timed-out would still pass schema and be treated as merge-gating-complete.
- **Fix PR**: https://github.com/GuitarAlchemist/ga/pull/343 — `minItems: 4`, `maxItems: 4`, dropped `other` from role enum

### PR #307 — camera focus on selected ring
- **Codex comment**: https://github.com/GuitarAlchemist/ga/pull/307#discussion_r3293611172
- **Classification**: REAL
- **Reasoning**: `updateCameraTarget()` computed the look-at as `addVectors(p, c).add(s)`, but chord/scale meshes sit on rings centered at the **origin** — not nested around the parent pitch. Summing added an extra ring-radius offset per drill level; drill-down framed empty space ~one pitch-ring out from the actual selected body.
- **Fix PR**: https://github.com/GuitarAlchemist/ga/pull/344 — aim at deepest anchor directly (pairs cleanly with #341 but stands alone)

### PR #304 — shadow camera projection
- **Codex comment**: https://github.com/GuitarAlchemist/ga/pull/304#discussion_r3293556394
- **Classification**: REAL
- **Reasoning**: `ModalMeadow` set `sun.shadow.camera.left/right/top/bottom/near/far` for SHADOW_HALF=90, but never called `updateProjectionMatrix()`. Three.js OrthographicCamera caches its projection matrix; without the recompute, the shadow camera stayed at its default 1×1 projection, clipping shadows everywhere outside that tiny footprint.
- **Fix PR**: https://github.com/GuitarAlchemist/ga/pull/345 — single `updateProjectionMatrix()` call

### PR #294 — yaml dependency
- **Codex comment**: https://github.com/GuitarAlchemist/ga/pull/294#discussion_r3293432409
- **Classification**: REAL
- **Reasoning**: `scripts/gen-theme-from-design.mjs` imports `yaml`, but `ReactComponents/ga-react-components/package.json` never declared it. Worked locally via transitive hoisting (yaml@1.10.2 present from another dep); clean installs would `ERR_MODULE_NOT_FOUND`.
- **Fix PR**: https://github.com/GuitarAlchemist/ga/pull/347 — add `yaml ^2.6.0` to devDependencies

### PR #289 — ga-client dev script
- **Codex comment**: https://github.com/GuitarAlchemist/ga/pull/289#discussion_r3293390871
- **Classification**: REAL
- **Reasoning**: PR #289 sabotaged `Apps/ga-client`'s `dev` script with `process.exit(1)` to stop port-5176 collisions, but `Apps/ga-client/playwright.config.ts:73` and `Scripts/start-chatbot-dev.ps1:54,98` still call `npm run dev` — both now hard-fail at boot.
- **Fix PR**: https://github.com/GuitarAlchemist/ga/pull/348 — switch both callers to `npm run dev:legacy`

## Surprises

- **PowerShell semantics**: PR #318's "case-sensitive trailer" claim looked obvious until you remember that `-match` is case-insensitive in PowerShell by default. Codex was applying Bash / JS regex intuition.
- **Repo visibility**: PR #322's "cross-repo private repos" worry is technically correct but currently moot — all four sibling repos are public. Codex doesn't probe runtime state.
- **Stale snapshot reading**: PR #313's two findings both looked actionable until you read the *current* `playwright-dashboard.yml` — both fixes were either already there (no `cache:'npm'` set) or implemented via a different mechanism (tmp `.cjs` file vs `node -`). Codex was scanning the diff context, not the final state.
- **Camera bugs stack**: PRs #307 and #317 are independent but compose well — #341 fixes anchor *computation* per-mode, #344 fixes anchor *consumption* (don't sum). Either order of merge works.

## Recommendation

P1 Codex findings are **reliable enough to require gating but should stay advisory** with a hard-stop-on-merge for unresolved P1s. ~10/14 (71%) were real bugs the operator missed at merge time; the false-positive rate (~21%) and OBSOLETE rate (~7%) are non-trivial — flipping to P0-style "block merge until each P1 has a green X / dismissal reason" would generate a lot of dismissal noise for findings that don't apply to the merged state of the code. A lighter touch: `/grade-last-pr` already surfaces them post-merge; pair that with a pre-merge bot comment that bumps unresolved P1s into a checkbox the operator has to tick (dismiss with reason) before squash-merge.
