# Guitar Alchemist - Comprehensive Test Suite

## Overview

The `run-all-tests.ps1` script provides a unified way to run all tests in the Guitar Alchemist project, including:

- **Backend Tests** (NUnit + xUnit)
    - Core business logic tests
    - Aspire integration tests
    - Service tests

- **Frontend Tests** (Playwright)
    - Blazor chatbot UI tests
    - End-to-end integration tests

## Quick Start

### Run All Tests

```powershell
# From repository root
.\Scripts\run-all-tests.ps1
```

This will:

1. Build the entire solution
2. Run all backend tests (NUnit + xUnit)
3. Run all Playwright tests
4. Display a comprehensive summary

### Common Usage Patterns

```powershell
# Skip build (tests only)
.\Scripts\run-all-tests.ps1 -SkipBuild

# Backend tests only
.\Scripts\run-all-tests.ps1 -BackendOnly

# Playwright tests only
.\Scripts\run-all-tests.ps1 -PlaywrightOnly

# Verbose output (show all test details)
.\Scripts\run-all-tests.ps1 -Verbose

# Combine options
.\Scripts\run-all-tests.ps1 -SkipBuild -BackendOnly -Verbose
```

## Test Suites

### Backend Tests

#### 1. **GA.Business.Core.Tests** (NUnit)

- Core music theory tests
- Chord generation tests
- Scale and mode tests
- Fretboard logic tests

**Location:** `Tests/GA.Business.Core.Tests/`

**Run individually:**

```powershell
dotnet test Tests/GA.Business.Core.Tests/GA.Business.Core.Tests.csproj
```

#### 2. **AllProjects.AppHost.Tests** (xUnit)

- Aspire orchestration tests
- Service startup tests
- Health check tests
- Service-to-service communication tests
- Rate limiting tests

**Location:** `Tests/AllProjects.AppHost.Tests/`

**Run individually:**

```powershell
dotnet test Tests/AllProjects.AppHost.Tests/AllProjects.AppHost.Tests.csproj
```

**Tests included:**

- `AppHost_StartsSuccessfully` - Verifies Aspire orchestration
- `GaApi_StartsAndRespondsToHealthCheck` - Tests API health
- `MongoDB_StartsAndIsAccessible` - Verifies database
- `GaApi_CanQueryChordCount` - Tests API functionality
- `GaApi_RateLimiting_WorksCorrectly` - Verifies rate limiting
- `Chatbot_StartsAndRespondsToHealthCheck` - Tests Blazor app
- `AllServices_StartInCorrectOrder` - Tests dependencies

### Frontend Tests

#### 3. **GuitarAlchemistChatbot.Tests.Playwright** (Playwright)

- UI interaction tests
- Chord diagram rendering tests
- Chord progression tests
- Dark mode tests
- MCP integration tests
- Tab viewer tests

**Location:** `Tests/GuitarAlchemistChatbot.Tests.Playwright/`

**Run individually:**

```powershell
cd Tests/GuitarAlchemistChatbot.Tests.Playwright
dotnet test
```

**Test suites:**

- `ChordDiagramTests.cs` - Chord diagram rendering
- `ChordProgressionTests.cs` - Chord progression functionality
- `ContextPersistenceTests.cs` - Context persistence
- `DarkModeTests.cs` - Dark mode toggle
- `FunctionCallingTests.cs` - Function calling
- `McpIntegrationTests.cs` - MCP integration
- `TabViewerTests.cs` - Tab viewer functionality

## Output Format

The script provides color-coded output:

```
========================================
Guitar Alchemist - Comprehensive Test Suite
========================================

ℹ Repository: C:/Users/spare/source/repos/ga
ℹ Started: 2025-10-18 14:30:00

▶ Building solution...
✓ Build succeeded in 12.34s

========================================
Backend Tests
========================================

▶ Running all .NET tests (NUnit + xUnit)...
✓ Backend tests passed: 150 passed, 0 skipped in 45.67s

ℹ Test Projects:
  - GA.Business.Core.Tests (NUnit)
  - AllProjects.AppHost.Tests (xUnit - Aspire Integration)

========================================
Playwright Tests
========================================

▶ Running Playwright tests for Blazor Chatbot...
✓ Playwright tests passed: 25 passed, 0 skipped in 30.12s

ℹ Playwright Test Suites:
  - Chord Diagram Tests
  - Chord Progression Tests
  - Context Persistence Tests
  - Dark Mode Tests
  - Function Calling Tests
  - MCP Integration Tests
  - Tab Viewer Tests

========================================
Test Summary
========================================

Build:       ✓ SUCCESS

Backend:     ✓ 150 passed
             Duration: 45.67s

Playwright:  ✓ 25 passed
             Duration: 30.12s

----------------------------------------
Total Tests: 175
  Passed:    175
  Failed:    0
  Skipped:   0

Total Duration: 88.13s
Completed: 2025-10-18 14:31:28
========================================

✓ All tests passed!
```

## Exit Codes

- `0` - All tests passed
- `1` - Build failed or tests failed

This makes the script suitable for CI/CD pipelines.

## CI/CD Integration

### GitHub Actions

```yaml
name: Test Suite

on: [push, pull_request]

jobs:
  test:
    runs-on: windows-latest
    steps:
      - uses: actions/checkout@v3
      
      - name: Setup .NET
        uses: actions/setup-dotnet@v3
        with:
          dotnet-version: '9.0.x'
      
      - name: Run all tests
        run: .\Scripts\run-all-tests.ps1
        shell: pwsh
```

### Azure DevOps

```yaml
trigger:
  - main

pool:
  vmImage: 'windows-latest'

steps:
  - task: UseDotNet@2
    inputs:
      version: '9.0.x'
  
  - pwsh: .\Scripts\run-all-tests.ps1
    displayName: 'Run all tests'
```

## Prerequisites

### Backend Tests

- .NET 9 SDK
- MongoDB (for integration tests)
- OpenAI API key (for vector search tests)

### Playwright Tests

- .NET 9 SDK
- Playwright browsers (auto-installed on first run)
- Running Blazor chatbot application

## Troubleshooting

### Playwright Installation Issues

If Playwright browsers are not installed:

```powershell
cd Tests/GuitarAlchemistChatbot.Tests.Playwright
dotnet build
pwsh bin/Debug/net9.0/playwright.ps1 install
```

### MongoDB Connection Issues

Ensure MongoDB is running:

```powershell
# Using Docker
docker run -d -p 27017:27017 --name mongodb mongo:latest

# Or using Aspire
dotnet run --project AllProjects.AppHost
```

### Test Failures

Run with verbose output to see details:

```powershell
.\Scripts\run-all-tests.ps1 -Verbose
```

## Test Results

Test results are saved in TRX format:

- Backend: `TestResults/test-results.trx`
- Playwright: `Tests/GuitarAlchemistChatbot.Tests.Playwright/TestResults/playwright-results.trx`

View results in Visual Studio or convert to HTML:

```powershell
# Install trx2html
dotnet tool install -g trx2html

# Convert to HTML
trx2html TestResults/test-results.trx
```

## Performance Benchmarks

Typical test durations on a modern development machine:

| Test Suite       | Duration | Tests    |
|------------------|----------|----------|
| Build            | ~15s     | N/A      |
| Backend Tests    | ~45s     | ~150     |
| Playwright Tests | ~30s     | ~25      |
| **Total**        | **~90s** | **~175** |

## Best Practices

1. **Run tests before committing**
   ```powershell
   .\Scripts\run-all-tests.ps1
   ```

2. **Run specific suites during development**
   ```powershell
   # Backend only (faster)
   .\Scripts\run-all-tests.ps1 -BackendOnly -SkipBuild
   ```

3. **Use verbose mode for debugging**
   ```powershell
   .\Scripts\run-all-tests.ps1 -Verbose
   ```

4. **Run full suite before PR**
   ```powershell
   .\Scripts\run-all-tests.ps1
   ```

## Adding New Tests

### Backend Tests (NUnit)

1. Add test class to `Tests/GA.Business.Core.Tests/`
2. Use `[TestFixture]` and `[Test]` attributes
3. Run `.\Scripts\run-all-tests.ps1 -BackendOnly` to verify

### Backend Tests (xUnit)

1. Add test class to `Tests/AllProjects.AppHost.Tests/`
2. Use `[Fact]` or `[Theory]` attributes
3. Run `.\Scripts\run-all-tests.ps1 -BackendOnly` to verify

### Playwright Tests

1. Add test class to `Tests/GuitarAlchemistChatbot.Tests.Playwright/`
2. Inherit from `ChatbotTestBase`
3. Use `[Test]` attribute
4. Run `.\Scripts\run-all-tests.ps1 -PlaywrightOnly` to verify

## Related Scripts

- `Scripts/test-chord-api.ps1` - Test chord API endpoints
- `Scripts/test-vector-search.ps1` - Test vector search
- `Scripts/test-enhanced-vector-search.ps1` - Test enhanced vector search
- `Tests/GuitarAlchemistChatbot.Tests.Playwright/run-tests.ps1` - Run Playwright tests only

## Support

For issues or questions:

1. Check the troubleshooting section above
2. Review test output with `-Verbose` flag
3. Check individual test project READMEs
4. Review test logs in `TestResults/` directory

