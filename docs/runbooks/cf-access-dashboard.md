# Cloudflare Access for the GA Development Dashboard

**Status:** draft — apply when ready to enable.
**Audience:** operator (the only person with admin on the
`guitaralchemist.com` Cloudflare account).
**Time to apply:** ~10 minutes in the CF dashboard + 1 round-trip test.

## Why

The Development dashboard at `https://demos.guitaralchemist.com/test#dev`
exposes two state-mutating endpoints over the Cloudflare tunnel:

| Endpoint                                         | Effect                                              |
| ------------------------------------------------ | --------------------------------------------------- |
| `POST /actions/harness/skill/<name>`             | Queues a skill invocation for the agent pool        |
| `POST /actions/algedonic/ack/<id>`               | Acknowledges a VSM algedonic signal                 |

Read paths (`/dev-data/*`, `/test/*`, static assets) stay **public** —
the data behind them is already on `github.com/GuitarAlchemist/ga`.

The PR shipping this runbook (`feat(dashboard): Cloudflare Access auth
for harness actions`) put a CF-Access-aware UI in place:

- `useCfIdentity()` polls `/cdn-cgi/access/get-identity` for the
  operator's email.
- `AuthChip` in the dashboard header shows the current sign-in state.
- `SkillActionButton` and `AlgedonicCard`'s Ack button refuse to fire
  (and redirect to login) when no identity is returned.

Until CF Access is configured per this runbook, the chip will show
"Sign in" and clicking it 404s — that's expected. After configuration
below, identity flows end-to-end.

## Steps (Cloudflare Zero Trust dashboard)

1. **Open Zero Trust.** Cloudflare dashboard →
   <https://one.dash.cloudflare.com/> → select the account that owns
   `guitaralchemist.com`.

2. **Access → Applications → Add an application → Self-hosted.**

3. **Application configuration.**
   - **Application name:** `GA Dev Dashboard — Actions`
   - **Session duration:** `24 hours` (matches the dashboard's
     ergonomic loop — one PIN per work day)
   - **Application domain:** `demos.guitaralchemist.com`
   - **Path (Option A — recommended):** `/actions/*`
     - Gates the two action endpoints. Read paths stay public.
   - **Path (Option B — most paranoid):** `/test/*`
     - Gates the entire `/test/` dashboard area, including read paths.
     - Choose this if you want even the dashboard chrome behind auth.
   - Leave the rest at defaults (App Launcher visibility off, no
     custom logo).

4. **Identity provider.**
   - Easiest: **One-time PIN** to the operator email (no third-party
     IdP needed; CF emails a 6-digit code per login).
   - Alternative: Google OAuth — already enabled on most CF accounts;
     restrict to `spareilleux@gmail.com` in the policy below.

5. **Allow policy.**
   - **Policy name:** `Operator only`
   - **Action:** `Allow`
   - **Include rule:** `Emails` → `spareilleux@gmail.com`
   - Do NOT add `Everyone`, `Authenticated users`, or wildcard email
     domains. Single-email allowlist only.

6. **Save.** CF deploys the rule to the edge in <60 s.

## Verification

Run from any machine — these one-liners exercise the gate from outside
the tunnel:

```bash
# Read path is public → 200, JSON payload.
curl -sS -o /dev/null -w "%{http_code}\n" https://demos.guitaralchemist.com/dev-data/manifest
# expected: 200

# Action path is gated → 302 (CF Access redirect to login).
curl -sS -o /dev/null -w "%{http_code}\n" -X POST \
  -H "Content-Type: application/json" \
  -d '{"source":"verify"}' \
  https://demos.guitaralchemist.com/actions/harness/skill/test-plan
# expected: 302 (Location: /cdn-cgi/access/login/…) — NOT 200
```

Browser round-trip:

1. Open `https://demos.guitaralchemist.com/test#dev/harness` in a
   private window (no cookies).
2. The Auth chip in the header reads **Sign in**.
3. Click the chip → CF Access prompt → enter PIN from email →
   redirected back to the same URL.
4. Chip now reads **Logged in as spareilleux@gmail.com**.
5. Click any SkillActionButton (e.g. `/test-plan`) → snackbar shows
   `Invocation queued`.
6. Wait 24 h; the cookie expires; the chip returns to **Sign in**.

## Rollback

Remove the application from CF Zero Trust → Access → Applications →
delete. The dashboard reverts to "anyone with the URL can fire actions"
within ~60 s. (Reminder: that's what we're trying to avoid; only roll
back during incident response.)

## Migration notes for the deprecated `/dev-data/*` POST endpoints

The PR added a `Deprecation: true` response header on the legacy
`POST /dev-data/harness/skill/<name>` and `POST /dev-data/algedonic/ack/<id>`
endpoints. They still work for local-only callers (the Vite middleware
still applies `gateLocal`), but new clients should use `/actions/*`.

Plan to remove the `/dev-data/*` POST handlers after one release cycle.
Until then, both surfaces share the same handler implementations, so
there is no behaviour drift.

## Cross-references

- Action endpoint implementations: `ReactComponents/ga-react-components/vite.config.ts`
  (search for `/actions/harness/skill` and `/actions/algedonic/ack`).
- Frontend hook: `ReactComponents/ga-react-components/src/hooks/useCfIdentity.ts`.
- UI components: `src/components/Auth/AuthChip.tsx`,
  `src/components/Harness/SkillActionButton.tsx`,
  `src/components/Algedonic/AlgedonicCard.tsx`.
- Test:
  `ReactComponents/ga-react-components/tests/dashboard/auth-gate.spec.ts`.
- Original tunnel security comment (read endpoints stay public by
  design): top-of-file comment in `vite.config.ts`.
