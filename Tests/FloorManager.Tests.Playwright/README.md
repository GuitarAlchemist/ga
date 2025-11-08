# FloorManager - Playwright Tests

End-to-end testing suite for the FloorManager Blazor application using Playwright.

---

## 📋 **Test Coverage**

### **Test Suites**

1. **FloorViewerTests** - Multi-floor viewer (/floors)
    - Page loading
    - Floor statistics display
    - Room list with categories and item counts
    - Master/detail room selection
    - Music items display in detail panel
    - Floor switching
    - 3D view links

2. **Floor3DViewerTests** - 3D floor viewer (/floor/{id})
    - Page loading for different floors
    - Floor statistics (Music Items, Rooms, Corridors)
    - Room list panel
    - Room details on click
    - All music items display (not just "0 items")
    - Category display
    - View mode toggle
    - Generation summary

---

## 🚀 **Getting Started**

### **Prerequisites**

- .NET 9.0 SDK
- FloorManager application running on http://localhost:5233
- GaApi running on http://localhost:5232

### **First-Time Setup**

```bash
# Navigate to test project
cd Tests/FloorManager.Tests.Playwright

# Restore packages
dotnet restore

# Build the project
dotnet build

# Install Playwright browsers
pwsh bin/Debug/net9.0/playwright.ps1 install
```

---

## 🧪 **Running Tests**

### **Start Required Services**

Before running tests, ensure services are running:

```powershell
# Terminal 1: Start FloorManager
dotnet run --project Apps/FloorManager/FloorManager.csproj

# Terminal 2: Start GaApi (if not already running)
dotnet run --project Apps/ga-server/GaApi/GaApi.csproj
```

### **Run All Tests**

```bash
cd Tests/FloorManager.Tests.Playwright
dotnet test
```

### **Run Specific Test Suite**

```bash
# FloorViewer tests only
dotnet test --filter "FullyQualifiedName~FloorViewerTests"

# Floor3DViewer tests only
dotnet test --filter "FullyQualifiedName~Floor3DViewerTests"
```

### **Run Specific Test**

```bash
# Test floor statistics
dotnet test --filter "FullyQualifiedName~ShouldShowCorrectFloorStatistics"

# Test music items display
dotnet test --filter "FullyQualifiedName~ShouldDisplayAllMusicItems"
```

### **Run with Visible Browser (Headed Mode)**

```bash
dotnet test -- Playwright.LaunchOptions.Headless=false
```

### **Run with Slow Motion**

```bash
dotnet test -- Playwright.LaunchOptions.Headless=false Playwright.LaunchOptions.SlowMo=500
```

---

## 📊 **Test Configuration**

### **Default Settings**

- Browser: Chromium
- Headless: false (visible browser)
- Timeout: 30 seconds
- Base URL: http://localhost:5233

### **Custom Settings**

Create `playwright.runsettings`:

```xml
<RunSettings>
  <Playwright>
    <BrowserName>chromium</BrowserName>
    <LaunchOptions>
      <Headless>false</Headless>
      <SlowMo>100</SlowMo>
    </LaunchOptions>
    <ExpectTimeout>5000</ExpectTimeout>
    <Timeout>30000</Timeout>
  </Playwright>
</RunSettings>
```

Run with custom settings:

```bash
dotnet test --settings playwright.runsettings
```

---

## 🎯 **Test Scenarios**

### **FloorViewer Tests (10 tests)**

1. ✅ Page loads successfully
2. ✅ Shows 6 floor buttons
3. ✅ Generates floor with music items
4. ✅ Shows room list with categories
5. ✅ Shows room details on click
6. ✅ Displays all music items in detail panel
7. ✅ Highlights selected room
8. ✅ Shows empty state when no room selected
9. ✅ Switches between floors
10. ✅ Shows 3D view links

### **Floor3DViewer Tests (14 tests)**

1. ✅ Page loads successfully
2. ✅ Shows correct floor statistics
3. ✅ Shows room list panel
4. ✅ Displays rooms with item counts
5. ✅ Shows room details on click
6. ✅ Displays all music items
7. ✅ Does not show "0 items"
8. ✅ Shows category for room
9. ✅ Has regenerate button
10. ✅ Has back to floors link
11. ✅ Shows generation summary
12. ✅ Loads different floors (4 test cases)
13. ✅ Shows view mode toggle
14. ✅ Room list matches statistics

---

## 🐛 **Troubleshooting**

### **Tests Fail with "Page not found"**

Ensure FloorManager is running:

```bash
dotnet run --project Apps/FloorManager/FloorManager.csproj
```

### **Tests Fail with "No music items"**

Ensure GaApi is running and returning data:

```bash
# Test API directly
curl http://localhost:5232/api/music-rooms/floor/5?floorSize=80
```

### **Playwright Browsers Not Installed**

```bash
cd Tests/FloorManager.Tests.Playwright
pwsh bin/Debug/net9.0/playwright.ps1 install
```

### **Tests Timeout**

Increase timeout in test or runsettings:

```csharp
protected const int DefaultTimeout = 60000; // 60 seconds
```

---

## 📚 **Resources**

- [Playwright for .NET](https://playwright.dev/dotnet/)
- [NUnit Documentation](https://docs.nunit.org/)
- [FloorManager Documentation](../../Apps/FloorManager/README.md)

---

## 🎯 **Best Practices**

1. **Always start services before running tests**
2. **Use headed mode for debugging** (`Headless=false`)
3. **Use slow motion to see what's happening** (`SlowMo=500`)
4. **Run specific tests during development** (faster feedback)
5. **Check test output for detailed error messages**
6. **Verify data model changes don't break tests**

---

## 📝 **Adding New Tests**

1. Add test method to appropriate test class
2. Use `[Test]` attribute
3. Follow AAA pattern (Arrange, Act, Assert)
4. Use descriptive test names
5. Run test to verify it works
6. Update this README with new test count

Example:

```csharp
[Test]
public async Task FloorViewer_ShouldDoSomething()
{
    // Arrange
    await NavigateToFloorsAsync();
    
    // Act
    await Page.ClickAsync("button:has-text('Floor 0')");
    
    // Assert
    var result = await GetTextAsync(".some-selector");
    Assert.That(result, Does.Contain("expected"));
}
```

