# Testing Guide

## Overview

Guitar Alchemist uses a comprehensive testing strategy with unit tests, integration tests, and end-to-end tests using NUnit, xUnit, and Playwright.

## Test Structure

```
Tests/
├── Apps/
│   ├── GA.TabConversion.Api.Tests/
│   ├── GaApi.Tests/
│   └── GuitarAlchemistChatbot.Tests/
├── Common/
│   ├── GA.Business.Core.Tests/
│   ├── GA.Business.Core.Graphiti.Tests/
│   ├── GA.Core.Tests/
│   ├── GA.InteractiveExtension.Tests/
│   └── GA.MusicTheory.DSL.Tests/
├── BSPIntegrationTests/
├── FloorManager.Tests.Playwright/
└── GuitarAlchemistChatbot.Tests.Playwright/
```

## Running Tests

### All Tests

```powershell
# Run all tests
dotnet test AllProjects.slnx

# Run with verbose output
dotnet test AllProjects.slnx --verbosity detailed

# Run with specific configuration
dotnet test AllProjects.slnx -c Release
```

### Backend Tests Only

```powershell
# Quick backend regression
pwsh Scripts/run-all-tests.ps1 -BackendOnly

# Skip build
pwsh Scripts/run-all-tests.ps1 -BackendOnly -SkipBuild
```

### Playwright UI Tests

```powershell
# Install Playwright browsers (first time only)
pwsh Tests/FloorManager.Tests.Playwright/bin/Debug/net9.0/playwright.ps1 install

# Run all Playwright tests
pwsh Scripts/run-all-tests.ps1 -PlaywrightOnly

# Run specific test file
dotnet test Tests/FloorManager.Tests.Playwright/FloorManager.Tests.Playwright.csproj

# Run with specific browser
dotnet test -- Playwright.BrowserName=firefox
dotnet test -- Playwright.BrowserName=webkit
```

### Specific Test Categories

```powershell
# Run tests by category
dotnet test AllProjects.slnx --filter "TestCategory=Unit"
dotnet test AllProjects.slnx --filter "TestCategory=Integration"
dotnet test AllProjects.slnx --filter "TestCategory=E2E"

# Run tests by namespace
dotnet test --filter "FullyQualifiedName~GA.Business.Core.Tests"

# Run specific test method
dotnet test --filter "FullyQualifiedName~GA.Business.Core.Tests.ChordTests.ShouldCreateMajorChord"
```

## Writing Tests

### Unit Test Example (NUnit)

```csharp
[TestFixture]
public class ChordTests
{
    [Test]
    public void ShouldCreateMajorChord()
    {
        // Arrange
        var root = PitchClass.C;
        
        // Act
        var chord = new Chord(root, ChordQuality.Major);
        
        // Assert
        Assert.That(chord.Root, Is.EqualTo(root));
        Assert.That(chord.Quality, Is.EqualTo(ChordQuality.Major));
    }
    
    [TestCase("C", "Major")]
    [TestCase("D", "Minor")]
    public void ShouldCreateChordWithParameters(string root, string quality)
    {
        // Arrange & Act
        var chord = new Chord(PitchClass.Parse(root), ChordQuality.Parse(quality));
        
        // Assert
        Assert.That(chord, Is.Not.Null);
    }
}
```

### Integration Test Example

```csharp
[TestFixture]
public class MongoDbIntegrationTests
{
    private IMongoClient _mongoClient = null!;
    private IMongoDatabase _database = null!;
    
    [SetUp]
    public void Setup()
    {
        _mongoClient = new MongoClient("mongodb://localhost:27017");
        _database = _mongoClient.GetDatabase("guitar-alchemist-test");
    }
    
    [Test]
    public async Task ShouldInsertAndRetrieveChord()
    {
        // Arrange
        var collection = _database.GetCollection<BsonDocument>("chords");
        var chord = new BsonDocument { { "name", "C Major" } };
        
        // Act
        await collection.InsertOneAsync(chord);
        var result = await collection.Find(Builders<BsonDocument>.Filter.Eq("name", "C Major")).FirstOrDefaultAsync();
        
        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result["name"].AsString, Is.EqualTo("C Major"));
    }
    
    [TearDown]
    public void Cleanup()
    {
        _mongoClient.DropDatabase("guitar-alchemist-test");
    }
}
```

### Playwright E2E Test Example

```csharp
[TestFixture]
public class ChatbotTests
{
    private IPage _page = null!;
    private IBrowser _browser = null!;
    
    [SetUp]
    public async Task Setup()
    {
        var playwright = await Playwright.CreateAsync();
        _browser = await playwright.Chromium.LaunchAsync();
        _page = await _browser.NewPageAsync();
    }
    
    [Test]
    public async Task ShouldDisplayChatInterface()
    {
        // Arrange & Act
        await _page.GotoAsync("https://localhost:7100");
        
        // Assert
        var chatInput = await _page.QuerySelectorAsync("input[placeholder='Type your message...']");
        Assert.That(chatInput, Is.Not.Null);
    }
    
    [TearDown]
    public async Task Cleanup()
    {
        await _page.CloseAsync();
        await _browser.CloseAsync();
    }
}
```

## Test Naming Conventions

Follow behavior-driven naming:

```csharp
// ✅ Good
public void ShouldReturnMajorChordWhenQualityIsMajor()
public void ShouldThrowExceptionWhenRootIsNull()
public void ShouldCalculateIntervalCorrectly()

// ❌ Avoid
public void TestChord()
public void ChordTest()
public void Test1()
```

## Test Categories

Use `[Category]` attribute to organize tests:

```csharp
[TestFixture]
public class ChordTests
{
    [Test]
    [Category("Unit")]
    public void ShouldCreateChord() { }
    
    [Test]
    [Category("Integration")]
    public void ShouldQueryDatabase() { }
    
    [Test]
    [Category("E2E")]
    public void ShouldDisplayInUI() { }
}
```

## Debugging Tests

### Visual Studio

1. Set breakpoint in test
2. Right-click test → Debug Test
3. Use Debug toolbar to step through

### Command Line

```powershell
# Run with debugging
dotnet test --no-build --verbosity detailed

# Run single test with output
dotnet test --filter "FullyQualifiedName~TestName" --logger "console;verbosity=detailed"
```

## Performance Testing

```csharp
[Test]
public void ShouldCompleteWithinTimeLimit()
{
    var stopwatch = Stopwatch.StartNew();
    
    // Act
    var result = ExpensiveOperation();
    
    stopwatch.Stop();
    
    // Assert
    Assert.That(stopwatch.ElapsedMilliseconds, Is.LessThan(1000));
}
```

## Mocking and Stubbing

```csharp
[Test]
public void ShouldCallEmbeddingService()
{
    // Arrange
    var mockEmbeddingService = new Mock<IEmbeddingService>();
    mockEmbeddingService
        .Setup(x => x.GenerateEmbeddingAsync(It.IsAny<string>()))
        .ReturnsAsync(new float[] { 0.1f, 0.2f, 0.3f });
    
    var service = new SearchService(mockEmbeddingService.Object);
    
    // Act
    var result = service.Search("test");
    
    // Assert
    mockEmbeddingService.Verify(x => x.GenerateEmbeddingAsync("test"), Times.Once);
}
```

## CI/CD Integration

Tests run automatically on:
- Pull requests
- Commits to main branch
- Scheduled nightly builds

## Coverage Goals

- **Unit Tests**: 80%+ coverage
- **Integration Tests**: 60%+ coverage
- **E2E Tests**: Critical user paths

## Troubleshooting

### Tests Fail Locally but Pass in CI

- Check .NET SDK version
- Verify MongoDB is running
- Clear NuGet cache
- Check environment variables

### Playwright Tests Timeout

- Increase timeout: `await page.GotoAsync(url, new() { Timeout = 30000 })`
- Check browser installation: `playwright.ps1 install`
- Verify network connectivity

### Database Tests Fail

- Ensure MongoDB is running
- Check connection string
- Verify test database cleanup in TearDown

