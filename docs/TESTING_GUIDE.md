# Testing Guide

## Overview

This guide covers testing strategies, best practices, and how to run tests for the Guitar Alchemist project.

## Test Coverage Requirements

### Minimum Coverage Targets
- **Unit Tests**: 80% code coverage
- **Integration Tests**: All critical paths
- **E2E Tests**: All user workflows

### Current Test Coverage

| Module | Unit Tests | Integration Tests | Coverage |
|--------|------------|-------------------|----------|
| Grothendieck Service | ✅ Complete | ⏳ Pending | ~90% |
| Shape Graph Builder | ✅ Complete | ⏳ Pending | ~85% |
| Markov Walker | ✅ Complete | ⏳ Pending | ~90% |
| Redis Vector Service | ⏳ Pending | ⏳ Pending | 0% |
| Asset Management | ⏳ Pending | ⏳ Pending | 0% |

## Test Structure

### Unit Tests
Located in `Tests/Common/GA.Business.Core.Tests/`

**Structure**:
```
Tests/Common/GA.Business.Core.Tests/
├── Atonal/
│   ├── Grothendieck/
│   │   ├── GrothendieckServiceTests.cs
│   │   └── MarkovWalkerTests.cs
│   ├── PitchClassSetTests.cs
│   └── SetClassTests.cs
├── Fretboard/
│   └── Shapes/
│       └── ShapeGraphBuilderTests.cs
└── AI/
    └── RedisVectorServiceTests.cs (pending)
```

### Integration Tests
Located in `Tests/GaApi.Tests/` and `Tests/BSPIntegrationTests/`

### E2E Tests (Playwright)
Located in `Tests/GuitarAlchemistChatbot.Tests.Playwright/` and `Tests/FloorManager.Tests.Playwright/`

## Running Tests

### Run All Tests
```powershell
# Using the automated script (recommended)
.\Scripts\run-all-tests.ps1

# Or manually
dotnet test AllProjects.sln
```

### Run Specific Test Projects
```powershell
# Backend unit tests only
.\Scripts\run-all-tests.ps1 -BackendOnly

# Playwright E2E tests only
.\Scripts\run-all-tests.ps1 -PlaywrightOnly

# Specific test project
dotnet test Tests/Common/GA.Business.Core.Tests/GA.Business.Core.Tests.csproj
```

### Run Specific Test Classes
```powershell
# Run all tests in GrothendieckServiceTests
dotnet test --filter "FullyQualifiedName~GrothendieckServiceTests"

# Run specific test method
dotnet test --filter "FullyQualifiedName~GrothendieckServiceTests.ComputeICV.ShouldComputeICV_ForCMajorScale"
```

### Run with Code Coverage
```powershell
# Generate coverage report
dotnet test AllProjects.sln /p:CollectCoverage=true /p:CoverletOutputFormat=opencover

# View coverage in Visual Studio
# Tools -> Code Coverage -> Show Code Coverage Results
```

## Test Categories

### 1. Grothendieck Service Tests

**File**: `Tests/Common/GA.Business.Core.Tests/Atonal/Grothendieck/GrothendieckServiceTests.cs`

**Test Classes**:
- `ComputeICV` - ICV computation for pitch-class sets
- `ComputeDelta` - Delta calculation between ICVs
- `ComputeHarmonicCost` - L1 norm calculation
- `FindNearby` - Finding nearby pitch-class sets
- `FindShortestPath` - BFS shortest path
- `Performance` - Performance benchmarks

**Key Tests**:
```csharp
[Test]
public void ShouldComputeICV_ForCMajorScale()
{
    var cMajor = PitchClassSet.Parse("024579B");
    var icv = _service.ComputeICV(cMajor);
    
    Assert.That(icv.Ic1, Is.EqualTo(2)); // 2 semitones
    Assert.That(icv.Ic2, Is.EqualTo(5)); // 5 whole tones
    // ...
}
```

**Coverage**: ~90% (45 tests)

### 2. Shape Graph Builder Tests

**File**: `Tests/Common/GA.Business.Core.Tests/Fretboard/Shapes/ShapeGraphBuilderTests.cs`

**Test Classes**:
- `GenerateShapes` - Shape generation for pitch-class sets
- `BuildGraphAsync` - Graph construction with transitions
- `ShapeProperties` - Diagness and ergonomics computation
- `Performance` - Performance benchmarks

**Key Tests**:
```csharp
[Test]
public void ShouldGenerateShapes_ForCMajorTriad()
{
    var cMajorTriad = PitchClassSet.Parse("047");
    var shapes = _builder.GenerateShapes(_standardTuning, cMajorTriad, options);
    
    Assert.That(shapes, Is.Not.Empty);
    Assert.That(shapes.All(s => s.Span <= options.MaxSpan), Is.True);
}
```

**Coverage**: ~85% (38 tests)

### 3. Markov Walker Tests

**File**: `Tests/Common/GA.Business.Core.Tests/Atonal/Grothendieck/MarkovWalkerTests.cs`

**Test Classes**:
- `GenerateWalk` - Probabilistic walk generation
- `GenerateHeatMap` - Heat map generation
- `GeneratePracticePath` - Practice path with gradual difficulty
- `TemperatureControl` - Temperature-controlled exploration
- `Performance` - Performance benchmarks

**Key Tests**:
```csharp
[Test]
public void ShouldGenerateHeatMap_With6x24Grid()
{
    var heatMap = _walker.GenerateHeatMap(_testGraph, currentShape, options);
    
    Assert.That(heatMap.GetLength(0), Is.EqualTo(6)); // 6 strings
    Assert.That(heatMap.GetLength(1), Is.EqualTo(24)); // 24 frets
}
```

**Coverage**: ~90% (42 tests)

## Testing Best Practices

### 1. Arrange-Act-Assert Pattern
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

### 2. Test Naming Convention
```
ShouldDoSomething_WhenCondition
ShouldThrowException_WhenInvalidInput
ShouldReturnNull_WhenNotFound
```

### 3. Use Test Fixtures for Grouping
```csharp
[TestFixture]
public class MyServiceTests
{
    [TestFixture]
    public class MethodName : MyServiceTests
    {
        [Test]
        public void ShouldDoSomething_WhenCondition() { }
    }
}
```

### 4. Mock External Dependencies
```csharp
private Mock<ILogger<MyService>> _loggerMock;
private Mock<IGrothendieckService> _grothendieckMock;

[SetUp]
public void SetUp()
{
    _loggerMock = new Mock<ILogger<MyService>>();
    _grothendieckMock = new Mock<IGrothendieckService>();
    
    _grothendieckMock
        .Setup(g => g.ComputeDelta(It.IsAny<ICV>(), It.IsAny<ICV>()))
        .Returns(new GrothendieckDelta { /* ... */ });
}
```

### 5. Test Edge Cases
```csharp
[Test]
public void ShouldHandleEmptyInput() { }

[Test]
public void ShouldHandleNullInput() { }

[Test]
public void ShouldHandleLargeInput() { }

[Test]
public void ShouldHandleInvalidInput() { }
```

### 6. Performance Tests
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

## Test Data

### Test Pitch-Class Sets
```csharp
// Major triad
var cMajor = PitchClassSet.Parse("047");

// Minor triad
var cMinor = PitchClassSet.Parse("037");

// Major scale
var cMajorScale = PitchClassSet.Parse("024579B");

// Chromatic scale
var chromatic = PitchClassSet.Parse("0123456789AB");
```

### Test Tunings
```csharp
// Standard tuning (E A D G B E)
var standard = Tuning.Default;

// Drop D (D A D G B E)
var dropD = new Tuning(/* ... */);
```

## Continuous Integration

### GitHub Actions
Tests run automatically on:
- Push to `main` or `develop`
- Pull requests
- Manual workflow dispatch

**Workflow**: `.github/workflows/ci.yml`

**Jobs**:
1. **Build** - Compile solution
2. **Backend Tests** - Run NUnit + xUnit tests
3. **Playwright Tests** - Run E2E tests
4. **Code Quality** - Check formatting
5. **Summary** - Aggregate results

### Pre-commit Hooks
Tests run locally before commit:
- Code formatting validation
- Build verification
- Fast unit tests (< 30 seconds)

**Install**: `.\Scripts\install-git-hooks.ps1`

## Debugging Tests

### Visual Studio
1. Open Test Explorer (Test -> Test Explorer)
2. Right-click test -> Debug
3. Set breakpoints in test or source code

### VS Code
1. Install C# extension
2. Set breakpoints
3. Run -> Start Debugging (F5)

### Command Line
```powershell
# Run with verbose output
dotnet test --logger "console;verbosity=detailed"

# Run with diagnostics
dotnet test --diag:log.txt
```

## Test Coverage Reports

### Generate Coverage Report
```powershell
# Install ReportGenerator
dotnet tool install -g dotnet-reportgenerator-globaltool

# Generate coverage
dotnet test /p:CollectCoverage=true /p:CoverletOutputFormat=opencover

# Generate HTML report
reportgenerator -reports:coverage.opencover.xml -targetdir:coverage-report
```

### View Coverage Report
```powershell
# Open in browser
start coverage-report/index.html
```

## Next Steps

### Pending Tests
1. **Redis Vector Service Tests** - Unit + integration tests
2. **Asset Management Tests** - Unit tests for asset service
3. **API Integration Tests** - Test Grothendieck API endpoints
4. **Frontend Tests** - React component tests (Jest + React Testing Library)

### Test Improvements
1. Add mutation testing (Stryker.NET)
2. Add property-based testing (FsCheck)
3. Add load testing (NBomber)
4. Add contract testing (Pact)

## Resources

- [NUnit Documentation](https://docs.nunit.org/)
- [Moq Documentation](https://github.com/moq/moq4)
- [Playwright Documentation](https://playwright.dev/dotnet/)
- [Coverlet Documentation](https://github.com/coverlet-coverage/coverlet)

