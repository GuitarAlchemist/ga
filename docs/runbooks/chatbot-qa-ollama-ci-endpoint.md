# Runbook: expose Ollama to the Chatbot-QA CI job (securely)

**Goal:** let `.github/workflows/chatbot-qa-snapshot.yml` (GitHub-hosted runner)
reach a real Ollama so the daily snapshot records an actual `pass_pct` instead of
degrading. The index half is already done (public release `optick-index-v1.8` →
`OPTICK_INDEX_URL` secret). This covers the **Ollama** half.

**Why it needs auth:** Ollama has no native authentication, and GitHub-hosted
runners have dynamic public IPs (no usable allowlist). So a raw public tunnel is
unsafe — we put **Cloudflare Access (service token)** in front, and the chatbot
sends the token headers. Precedent: [`cf-access-dashboard.md`](cf-access-dashboard.md).

```
GitHub Actions runner ──HTTPS+CF-Access headers──▶ Cloudflare edge (Access: service-token gate)
                                                          │ (valid token only)
                                                          ▼
                                              cloudflared tunnel (demos box)
                                                          ▼
                                              http://localhost:11434  (Ollama)
```

## 1. Tunnel route (on the demos box that runs cloudflared)

Add a hostname → Ollama ingress rule. Dashboard: Zero Trust → Networks → Tunnels →
your tunnel → Public Hostname → Add:
- **Subdomain/domain:** `ollama-ci.guitaralchemist.com`
- **Service:** `HTTP` → `localhost:11434`

Or in `config.yml`:
```yaml
ingress:
  - hostname: ollama-ci.guitaralchemist.com
    service: http://localhost:11434
  # ... existing rules ...
  - service: http_status:404
```
Restart cloudflared after editing.

## 2. Cloudflare Access — service token (machine-to-machine)

Zero Trust → Access → **Service Auth** → Create Service Token → name it
`chatbot-qa-ci`. **Copy the Client ID and Client Secret now** (secret is shown once).

Then Access → Applications → Add → **Self-hosted**:
- **Application domain:** `ollama-ci.guitaralchemist.com`
- **Policy:** Action **Service Auth**, Include → **Service Token** → `chatbot-qa-ci`.

That makes the hostname **default-deny**: only requests carrying the matching
`CF-Access-Client-Id` / `CF-Access-Client-Secret` headers reach Ollama.

## 3. Code change (required — the chatbot must send the token headers)

Today neither Ollama path sends auth headers, so they'd get a 403 from Access:
- `Apps/GaChatbot/Services/QueryUnderstandingService.cs` — `httpClientFactory.CreateClient("ollama")` then `POST /api/generate`.
- `Common/GA.Business.ML/Providers/OllamaProvider.cs` — `CreateChatClient(...)` builds an `HttpClient` for the `IChatClient`.

Add optional headers from config (`Ollama:AccessClientId` / `Ollama:AccessClientSecret`,
i.e. env `Ollama__AccessClientId` / `Ollama__AccessClientSecret`); when set, attach
`CF-Access-Client-Id` / `CF-Access-Client-Secret` as default request headers on both
clients. When unset, behaviour is unchanged (local dev hits localhost, no headers).
*(Tracked as the follow-up to GA #407 — ask Claude to ship this; it touches the
chatbot LLM path so it should go through the QA-tribunal / multi-LLM review.)*

## 4. GitHub repo secrets

```bash
gh secret set OLLAMA_BASE_URL              --repo GuitarAlchemist/ga --body "https://ollama-ci.guitaralchemist.com"
gh secret set OLLAMA_CF_ACCESS_CLIENT_ID   --repo GuitarAlchemist/ga --body "<service-token client id>"
gh secret set OLLAMA_CF_ACCESS_CLIENT_SECRET --repo GuitarAlchemist/ga --body "<service-token client secret>"
```
The workflow's **Configure backend** step then exports `OLLAMA_BASE_URL` +
`Ollama__BaseUrl`/`Ollama__Endpoint` (already wired in #407) and, once the code
change lands, `Ollama__AccessClientId`/`Ollama__AccessClientSecret`.

> ⚠️ Pass values with `--body` (or pipe via stdin). `gh secret set NAME` with no
> value over a non-interactive `!` channel stores an **empty** secret — which is
> exactly why the first attempt fell back to `localhost:11434` (connection refused).

## 5. Verify

From any machine (simulating the runner), the token headers must succeed and a
header-less request must fail:
```bash
curl -s -o /dev/null -w "%{http_code}\n" https://ollama-ci.guitaralchemist.com/api/version            # expect 403 (no token)
curl -s -H "CF-Access-Client-Id: $ID" -H "CF-Access-Client-Secret: $SECRET" \
     https://ollama-ci.guitaralchemist.com/api/version                                                # expect 200 + version JSON
```
Then dispatch and watch:
```bash
gh workflow run chatbot-qa-snapshot.yml --repo GuitarAlchemist/ga
```
Success = preflight logs `ollama_ok=True optic_ok=True`, the snapshot has a real
`pass_pct` (no `degraded`), and `quality_latest` shows a live number.

## Alternative (no code change, one header): bearer proxy

If you'd rather not touch the chatbot's Access-header path, run a tiny reverse
proxy (Caddy/nginx) in front of Ollama that requires `Authorization: Bearer <token>`
and tunnel *that*. You still need the chatbot to send one `Authorization` header —
so it's the same code surface, just one header instead of two. CF Access is
preferred because it's already in use here and centrally revocable.

## Security notes

- The service token scopes access to this one hostname; revoke/rotate in Zero Trust
  → Access → Service Auth without redeploying anything.
- Treat the Client Secret like a credential — GitHub secret only, never logged.
- Optionally pin a short `mTLS`/WARP layer later; the service token is sufficient
  for CI-to-Ollama.
```
