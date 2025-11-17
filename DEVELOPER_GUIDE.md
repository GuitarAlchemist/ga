# Guitar Alchemist - Developer Guide

## 🚀 Quick Start

### First Time Setup

```powershell
# 1. Clone the repository
git clone https://github.com/GuitarAlchemist/ga.git
cd ga

# 2. Run one-command setup
.\Scripts\setup-dev-environment.ps1

# 3. Install Git hooks (optional but recommended)
.\Scripts\install-git-hooks.ps1

# 4. Start all services
.\Scripts\start-all.ps1 -Dashboard

# 5. Run tests to verify everything works
.\Scripts\run-all-tests.ps1
```

That's it! You're ready to develop. 🎸

---

## 📋 Available Scripts

### Development

| Script | Description | Usage |
|--------|-------------|-------|
| **setup-dev-environment.ps1** | One-command environment setup | `.\Scripts\setup-dev-environment.ps1` |
| **start-all.ps1** | Start all services (Aspire) | `.\Scripts\start-all.ps1 -Dashboard` |
| **health-check.ps1** | Verify all services are healthy | `.\Scripts\health-check.ps1` |
| **run-all-tests.ps1** | Run all tests (backend + frontend) | `.\Scripts\run-all-tests.ps1` |
| **install-git-hooks.ps1** | Install pre-commit hooks | `.\Scripts\install-git-hooks.ps1` |

### Testing

| Script | Description | Usage |
|--------|-------------|-------|
| **run-all-tests.ps1** | Run all tests | `.\Scripts\run-all-tests.ps1` |
| **run-all-tests.ps1 -BackendOnly** | Backend tests only | `.\Scripts\run-all-tests.ps1 -BackendOnly` |
| **run-all-tests.ps1 -PlaywrightOnly** | Playwright tests only | `.\Scripts\run-all-tests.ps1 -PlaywrightOnly` |

### Deployment

| Script | Description | Usage |
|--------|-------------|-------|
| **docker-compose up** | Start with Docker Compose | `docker-compose up -d` |
| **docker-compose down** | Stop Docker services | `docker-compose down` |

---

## 🏗️ Architecture

### Project Structure

```
ga/
├── Apps/
│   ├── ga-server/GaApi/          # Main REST API (.NET 9)
│   ├── GuitarAlchemistChatbot/   # Blazor chatbot
│   └── ga-client/                # React frontend (Vite)
├── Common/
│   ├── GA.Business.Core/         # Core business logic
│   ├── GA.Core/                  # Core types and utilities
│   └── ...
├── GA.Data.MongoDB/              # MongoDB integration
├── Tests/
│   ├── GA.Business.Core.Tests/   # Unit tests (NUnit)
│   ├── AllProjects.AppHost.Tests/ # Integration tests (xUnit)
│   └── GuitarAlchemistChatbot.Tests.Playwright/ # E2E tests
├── AllProjects.AppHost/          # Aspire orchestration
├── Scripts/                      # PowerShell scripts
└── .github/workflows/            # CI/CD workflows
```

### Service Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                    Aspire AppHost                           │
│              (Orchestrates all services)                    │
└─────────────────────────────────────────────────────────────┘
                              │
        ┌─────────────────────┼─────────────────────┬─────────┐
        ↓                     ↓                     ↓         ↓
    MongoDB               GaApi                Chatbot    ga-client
    (Docker)              (.NET)               (Blazor)   (React)
        ↓                     ↓                     ↓
    Redis              GaMcpServer          ScenesService
    (Docker)           (MCP Tools)          (GLB Builder)
                                                  ↓
                                            FloorManager
                                            (BSP Viewer)
```

**Note:** GaMcpServer is now managed by Aspire and will survive restarts. See [MCP Server Aspire Integration](docs/MCP_SERVER_ASPIRE_INTEGRATION.md) for details.

---

## 🔧 Development Workflow

### Daily Development

```powershell
# 1. Start services (skip build for faster startup)
.\Scripts\start-all.ps1 -NoBuild -Dashboard

# 2. Make code changes in your editor

# 3. Services hot-reload automatically:
#    - GaApi: Hot reload enabled
#    - Chatbot: Hot reload enabled
#    - React: Vite HMR

# 4. Run tests
.\Scripts\run-all-tests.ps1 -BackendOnly

# 5. Check health
.\Scripts\health-check.ps1

# 6. When done, press Ctrl+C to stop services
```

### Before Committing

```powershell
# 1. Format code
dotnet format AllProjects.sln

# 2. Run all tests
.\Scripts\run-all-tests.ps1

# 3. Check health
.\Scripts\health-check.ps1

# 4. Commit (pre-commit hook will run automatically)
git add .
git commit -m "feat: add new feature"
```

### Creating a Pull Request

```powershell
# 1. Create feature branch
git checkout -b feature/my-feature

# 2. Make changes and commit
git add .
git commit -m "feat: add my feature"

# 3. Run full test suite
.\Scripts\run-all-tests.ps1

# 4. Push to remote
git push origin feature/my-feature

# 5. Create PR on GitHub
# CI/CD will run automatically
```

---

## 🧪 Testing

### Test Coverage Requirements

**Minimum Coverage Targets**:
- Unit Tests: **80% code coverage**
- Integration Tests: All critical paths
- E2E Tests: All user workflows

**Current Coverage**:
- Grothendieck Service: ~90% (45 tests)
- Shape Graph Builder: ~85% (38 tests)
- Markov Walker: ~90% (42 tests)
- **Overall**: ~88% (125 tests)

See [TESTING_GUIDE.md](docs/TESTING_GUIDE.md) for comprehensive testing documentation.

### Test Types

1. **Unit Tests** (NUnit)
   - Location: `Tests/GA.Business.Core.Tests/`
   - Run: `dotnet test Tests/GA.Business.Core.Tests/`
   - Coverage: ~88% average
   - Examples:
     - `GrothendieckServiceTests.cs` - ICV computation, delta calculation
     - `ShapeGraphBuilderTests.cs` - Shape generation, graph construction
     - `MarkovWalkerTests.cs` - Walk generation, heat maps

2. **Integration Tests** (xUnit)
   - Location: `Tests/AllProjects.AppHost.Tests/`
   - Run: `dotnet test Tests/AllProjects.AppHost.Tests/`
   - Tests service-to-service communication

3. **E2E Tests** (Playwright)
   - Location: `Tests/GuitarAlchemistChatbot.Tests.Playwright/`
   - Run: `dotnet test Tests/GuitarAlchemistChatbot.Tests.Playwright/`
   - Tests complete user workflows

### Running Tests

```powershell
# All tests (recommended before committing)
.\Scripts\run-all-tests.ps1

# Backend only (faster for development)
.\Scripts\run-all-tests.ps1 -BackendOnly

# Playwright only
.\Scripts\run-all-tests.ps1 -PlaywrightOnly

# Verbose output
.\Scripts\run-all-tests.ps1 -Verbose

# Skip build (when code hasn't changed)
.\Scripts\run-all-tests.ps1 -SkipBuild

# Specific test class
dotnet test --filter "FullyQualifiedName~GrothendieckServiceTests"

# Specific test method
dotnet test --filter "FullyQualifiedName~GrothendieckServiceTests.ComputeICV.ShouldComputeICV_ForCMajorScale"
```

### Test Coverage Reports

```powershell
# Generate coverage report
dotnet test /p:CollectCoverage=true /p:CoverletOutputFormat=opencover

# Install ReportGenerator (one-time)
dotnet tool install -g dotnet-reportgenerator-globaltool

# Generate HTML report
reportgenerator -reports:coverage.opencover.xml -targetdir:coverage-report

# View report
start coverage-report/index.html
```

### Testing Best Practices

1. **Arrange-Act-Assert Pattern**
   ```csharp
   [Test]
   public void ShouldDoSomething_WhenCondition()
   {
       // Arrange: Set up test data
       var input = CreateTestInput();

       // Act: Execute the method under test
       var result = _service.DoSomething(input);

       // Assert: Verify the result
       Assert.That(result, Is.EqualTo(expectedValue));
   }
   ```

2. **Test Naming Convention**
   - `ShouldDoSomething_WhenCondition`
   - `ShouldThrowException_WhenInvalidInput`
   - `ShouldReturnNull_WhenNotFound`

3. **Mock External Dependencies**
   ```csharp
   private Mock<ILogger<MyService>> _loggerMock;

   [SetUp]
   public void SetUp()
   {
       _loggerMock = new Mock<ILogger<MyService>>();
       _service = new MyService(_loggerMock.Object);
   }
   ```

4. **Test Edge Cases**
   - Empty inputs
   - Null inputs
   - Large inputs
   - Invalid inputs
   - Boundary conditions

5. **Performance Tests**
   ```csharp
   [Test]
   public void ShouldComplete_InLessThan100ms()
   {
       var stopwatch = Stopwatch.StartNew();
       _service.DoSomething();
       stopwatch.Stop();
       Assert.That(stopwatch.ElapsedMilliseconds, Is.LessThan(100));
   }
   ```

---

## 🔍 Debugging

### Debugging Backend (.NET)

1. **Visual Studio / Rider**
   - Open `AllProjects.sln`
   - Set `AllProjects.AppHost` as startup project
   - Press F5

2. **VS Code**
   - Open folder in VS Code
   - Use `.vscode/launch.json` configuration
   - Press F5

### Debugging Frontend (React)

1. **Browser DevTools**
   - Open http://localhost:5173
   - Press F12
   - Use React DevTools extension

2. **VS Code**
   - Install "Debugger for Chrome" extension
   - Use launch configuration
   - Set breakpoints in TypeScript files

### Debugging Tests

```powershell
# Run specific test
dotnet test --filter "TestName=MyTest"

# Debug test in Visual Studio
# Right-click test → Debug Test
```

---

## 📊 Monitoring

### Aspire Dashboard

Access at https://localhost:15001

Features:
- **Service Status** - See which services are running
- **Logs** - Centralized logging from all services
- **Metrics** - CPU, memory, request rates
- **Traces** - Distributed tracing with OpenTelemetry
- **Endpoints** - Quick links to all service URLs

### Health Checks

```powershell
# Check all services
.\Scripts\health-check.ps1

# Check specific service
curl https://localhost:7001/health
```

### Performance Metrics

Access at https://localhost:7001/api/Metrics/system

Shows:
- Request counts (regular vs semantic)
- Response times
- Error rates
- Cache statistics
- Split recommendations

---

## 🎨 Code Style

### .NET

- **Formatting**: Use `dotnet format`
- **Naming**: PascalCase for types/methods, camelCase for parameters
- **Indentation**: 4 spaces
- **File-scoped namespaces**: Preferred

```csharp
namespace GA.Business.Core;

public class MyClass
{
    private readonly IService _service;
    
    public MyClass(IService service)
    {
        _service = service;
    }
    
    public async Task<Result> DoSomethingAsync(string parameter)
    {
        // Implementation
    }
}
```

### React/TypeScript

- **Formatting**: Use ESLint
- **Components**: Functional components with hooks
- **Naming**: PascalCase for components, camelCase for functions
- **Indentation**: 2 spaces

```typescript
import React from 'react';

interface MyComponentProps {
  title: string;
  onAction: () => void;
}

export const MyComponent: React.FC<MyComponentProps> = ({ title, onAction }) => {
  return (
    <div>
      <h1>{title}</h1>
      <button onClick={onAction}>Click me</button>
    </div>
  );
};
```

---

## 🔐 Security

### User Secrets

```powershell
# Set OpenAI API key
cd Apps/ga-server/GaApi
dotnet user-secrets set "OpenAI:ApiKey" "your-key-here"

# List secrets
dotnet user-secrets list

# Remove secret
dotnet user-secrets remove "OpenAI:ApiKey"
```

### Environment Variables

```powershell
# Set environment variable (PowerShell)
$env:OPENAI_API_KEY = "your-key-here"

# Set in appsettings.Development.json (not committed)
{
  "OpenAI": {
    "ApiKey": "your-key-here"
  }
}
```

---

## 📦 Dependencies

### Adding NuGet Packages

```powershell
# Add package
dotnet add package PackageName

# Add specific version
dotnet add package PackageName --version 1.2.3

# Update package
dotnet add package PackageName --version 2.0.0
```

### Adding npm Packages

```powershell
cd Apps/ga-client

# Add package
npm install package-name

# Add dev dependency
npm install --save-dev package-name

# Update package
npm update package-name
```

---

## 🚢 Deployment

### Development (Aspire)

```powershell
.\Scripts\start-all.ps1 -Dashboard
```

### Production (Docker Compose)

```bash
docker-compose up -d
```

See [DOCKER_DEPLOYMENT.md](DOCKER_DEPLOYMENT.md) for details.

---

## 🐛 Troubleshooting

### Common Issues

1. **Port already in use**
   ```powershell
   netstat -ano | findstr :7001
   taskkill /PID <PID> /F
   ```

2. **MongoDB connection failed**
   ```powershell
   docker ps  # Check if MongoDB is running
   docker restart <container-id>
   ```

3. **Build failed**
   ```powershell
   dotnet clean AllProjects.sln
   dotnet restore AllProjects.sln
   dotnet build AllProjects.sln
   ```

4. **Tests failed**
   ```powershell
   .\Scripts\run-all-tests.ps1 -Verbose
   ```

---

## 📚 Resources

### Documentation

- [Start Services](Scripts/START_SERVICES_README.md)
- [Testing](Scripts/TEST_SUITE_README.md)
- [Docker Deployment](DOCKER_DEPLOYMENT.md)
- [AGENTS.md](AGENTS.md) - Repository guidelines

### External Links

- [.NET 9 Documentation](https://docs.microsoft.com/dotnet/)
- [Aspire Documentation](https://learn.microsoft.com/dotnet/aspire/)
- [React Documentation](https://react.dev/)
- [MongoDB Documentation](https://docs.mongodb.com/)

---

## 🤝 Contributing

1. Fork the repository
2. Create a feature branch
3. Make your changes
4. Run tests
5. Create a pull request

See [CONTRIBUTING.md](CONTRIBUTING.md) for details.

---

## 📞 Support

- **Issues**: https://github.com/GuitarAlchemist/ga/issues
- **Discussions**: https://github.com/GuitarAlchemist/ga/discussions

---

Happy coding! 🎸


### Using IChordNamingService (Unified Modes + Legacy Naming)

This guide shows how to use the DI‑friendly `IChordNamingService` for both:
- Unified Roman‑numeral chord naming in modal context (via `UnifiedModeInstance`).
- Legacy naming from chord templates, formulas, or raw interval lists.

Prerequisites
- Register the service (already done in hosts):
  - API host (`GaApi/Program.cs`): `services.AddScoped<IChordNamingService, ChordNamingService>();`
  - Blazor host (`GA.WebBlazorApp/Program.cs`): same registration.

Unified Roman numerals (modal context)
```csharp
// Build a UnifiedModeInstance from a pitch‑class set and root
var pcs = new PitchClassSet(new [] { PitchClass.FromValue(0), PitchClass.FromValue(2), PitchClass.FromValue(4),
                                     PitchClass.FromValue(5), PitchClass.FromValue(7), PitchClass.FromValue(9),
                                     PitchClass.FromValue(11) }); // Ionian
var unified = new UnifiedModeService().FromPitchClassSet(pcs, PitchClass.C);

// Injected service usage
var numeral = chordNamingService.GenerateModalChordName(unified, degree: 5, ChordExtension.Seventh);
// Example output: "V7"
```

Legacy naming (ChordTemplate)
```csharp
// Example: C major triad from a pitch‑class set
var template = ChordTemplate.Analytical.FromPitchClassSet(
    new PitchClassSet(new [] { PitchClass.FromValue(0), PitchClass.FromValue(4), PitchClass.FromValue(7) }),
    "C Major Triad");

var best = chordNamingService.GetBestChordName(template, PitchClass.C); // e.g., "C" or "Cmaj"
var options = chordNamingService.GetAllNamingOptions(template, PitchClass.C);
var comprehensive = chordNamingService.GenerateComprehensiveNames(template, PitchClass.C);
```

Legacy naming (ChordFormula)
```csharp
var bestMaj7 = chordNamingService.GetBestChordName(CommonChordFormulas.Major7, PitchClass.C);   // "Cmaj7" or "CM7"
var bestMin7 = chordNamingService.GetBestChordName(CommonChordFormulas.Minor7, PitchClass.C);   // "Cm7" or "Cmin7"
var bestDom7 = chordNamingService.GetBestChordName(CommonChordFormulas.Dominant7, PitchClass.C); // "C7" (some variants tolerated)
```

Legacy naming (Intervals list)
```csharp
var intervals = new List<ChordFormulaInterval>
{
    new(new Interval.Chromatic(Semitones.FromValue(3)), ChordFunction.Third),
    new(new Interval.Chromatic(Semitones.FromValue(7)), ChordFunction.Fifth)
};
var bestMinor = chordNamingService.GetBestChordName(intervals, "Minor", PitchClass.C); // contains "Cm" or starts with "C"
```

Conventions and tolerated variants
- Roman numerals: upper case for major/dominant/aug; lower for minor/diminished; suffixes `maj7`, `7`, `ø7`, `°7`.
- Diminished/half‑diminished: tests accept `°7`/`o7` and `ø7`/`m7b5`.
- Augmented: accept `aug` or `+` markers.
- Sixth vs 13th family: when both 6 and 9 are present, some outputs may render as a related 13th symbol; tests accept either `6/9` or a `13` family variant.
- Outputs may vary slightly depending on heuristics; tests are tolerant to common, musically equivalent variants.
