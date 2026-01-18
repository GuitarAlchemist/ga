# Workflow & Development Guide

## Prerequisites
- .NET 9 SDK
- Node.js 20+
- Docker Desktop
- Python 3.11+
- PowerShell 7

## Environment Setup
Run the unified setup script to install dependencies (NuGet, npm, pip):
```powershell
pwsh Scripts/setup-dev-environment.ps1
```

## Running the Platform
The project uses **.NET Aspire** for orchestration.
1. **Start Command:**
   ```powershell
   pwsh Scripts/start-all.ps1 -Dashboard
   ```
2. **Access Points:**
   - **Aspire Dashboard:** `http://localhost:18888` (Check console output for exact port)
   - **Frontend (ga-client):** `http://localhost:5173`
   - **API Gateway:** `https://localhost:7000` (approx)
   - **MongoExpress:** `http://localhost:8081`

## Testing Strategy
- **Unit Tests (Backend):** NUnit/xUnit.
  ```powershell
  pwsh Scripts/run-all-tests.ps1 -BackendOnly
  ```
- **E2E Tests (Frontend):** Playwright.
  ```powershell
  pwsh Scripts/run-all-tests.ps1 -PlaywrightOnly
  ```

## Code Quality
- **C# Style:** Enforced via `.editorconfig`.
- **Pre-commit:** Check `.githooks` for automated checks.
- **Secrets:** Manage via `dotnet user-secrets` for local dev. **NEVER COMMIT SECRETS.**

## Contribution Flow
1. **Select a Track:** Check `conductor/tracks.md`.
2. **Create Branch:** `git checkout -b feature/track-name-description`.
3. **Commit:** Use Conventional Commits (e.g., `feat(core): add new scale mode`).