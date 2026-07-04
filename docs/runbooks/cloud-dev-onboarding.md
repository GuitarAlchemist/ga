# Cloud dev onboarding — first steps (layer 1 + prerequisites)

**Status:** in progress — the reversible, zero-cost first steps of
[docs/plans/2026-07-04-arch-cloud-development-plan.md](../plans/2026-07-04-arch-cloud-development-plan.md).
Layer 2 (always-on runtime, metered) stays gated on an operator cost sign-off.

Legend: ✅ done in-repo · 🤝 delegatable (Jules/agent) · 👤 operator-only
(needs your machine / account / a secret).

## 1. Dev environment (layer 1) — the box where code is edited/built/tested

- ✅ **Devcontainer covers the family.** `.devcontainer/devcontainer.json` now
  installs dotnet 10 + node LTS + **Rust** + pwsh + gh + the Claude Code
  extension. One container builds ga *and* the Rust peers (ix / hari / sentrux)
  when they're cloned as sibling peers. No system duckdb needed — the invariant
  sweep uses DuckDB.NET (bundled native lib), so dotnet suffices.
- 👤 **Use it as the canonical surface.** Open the repo in a **Codespace** (or
  VS Code + Dev Containers) → the toolchain is there, no local install.
- 👤 **Make Claude Code *web* sessions carry the toolchain.** Web sessions do
  **not** boot the devcontainer automatically; a fresh web session has no dotnet
  (that's why this session had to delegate the discovery harness to CI). Fix:
  configure the web environment's **setup script** to restore the toolchain per
  <https://code.claude.com/docs/en/claude-code-on-the-web> (the environment's
  network policy + setup script are chosen when the environment is created).
  Cheapest honest version: a setup script that installs dotnet 10 + rust so
  web-session agents build/test locally instead of round-tripping to CI.

## 2. MCP portability — kill the "one Windows desktop" single point of failure

Today `.mcp.json` hard-codes `C:/Users/spare/...` for **5 servers**; three of
them (`tars`, `sentrux`, `hari`) point at *built binaries* that live only on the
operator's desktop, so no cloud session can reach those peers. This is pure
hygiene worth doing regardless of layer 2.

The live `.mcp.json` is **not** rewritten here on purpose — it is your working
config and only you can confirm it still loads in Antigravity. Recommended
migration, safe swaps first:

- 🤝 **Relative sibling paths (safe, strictly better).** `ga`
  `C:/Users/spare/source/repos/ga/GaMcpServer/...` → `GaMcpServer/...`
  (relative to the repo root where `.mcp.json` lives); `ix` `cwd`
  `C:/Users/spare/source/repos/ix` → `../ix` (the documented "siblings are
  peers" convention). Both work unchanged on Windows *and* any cloud clone.
- 👤 **Built-binary servers need their source committed first.** `hari-mcp` has
  **no source in the hari repo** (workspace lists only 4 crates — the binary is
  local/uncommitted); `sentrux.exe` lives outside repo scope; `tars` points at
  a `Release` build output. Step 1 is to **commit the `hari-mcp` and `sentrux`
  sources**, then switch each entry to a build-on-run form
  (`cargo run -p hari-mcp` / `dotnet run --project ../tars/...`) or an env var
  (`${HARI_MCP_BIN}`) with the value set per host. Until the source is
  committed, no cloud host can build these.

## 3. Secrets — the thing layer 2 forces

Moving the runtime off the desktop means secrets leave `.env`/machine and need a
real store (not files in the container). Doctrine holds: `CLAUDE_CODE_OAUTH_TOKEN`
stays human-only; a cloud runtime needs a secret manager. **Decide the store
before provisioning anything** (layer 2, operator sign-off).

## Not yet (layer 2, cost-gated)

Provisioning an always-on VM, managed DBs, the Cloudflare tunnel activation
(ga#493 + `cf-access-dashboard.md`), and the OpenHands fleet host — all wait on a
$/month envelope and your sign-off. See the plan doc §"Recommandation".
