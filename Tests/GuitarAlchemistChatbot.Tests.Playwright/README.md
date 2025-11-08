# Guitar Alchemist Chatbot - Playwright Tests

Comprehensive end-to-end testing suite for the Guitar Alchemist Chatbot using Playwright.

---

## 📋 **Test Coverage**

### **Test Suites**

1. **TabViewerTests** - Guitar tab visualization
    - VexTab rendering across browsers
    - ASCII tab format display
    - Graphical fretboard diagrams
    - Responsive behavior
    - Multiple tab handling
    - Error handling

2. **ContextPersistenceTests** - Conversation memory
    - Context persistence across messages
    - Context indicator display
    - Recent chord/scale tracking
    - Context clearing on new chat
    - Multiple reference handling
    - Long conversation persistence

3. **FunctionCallingTests** - AI function integration
    - Function call indicators
    - Structured result display
    - Multiple function calls
    - Loading states
    - Error handling
    - Result formatting

4. **McpIntegrationTests** - Web integration
    - Wikipedia search
    - Music theory site search
    - RSS feed reading
    - Web scraping
    - Caching behavior
    - Source attribution

---

## 🚀 **Getting Started**

### **Prerequisites**

- .NET 9.0 SDK
- Playwright browsers (installed automatically)

### **First-Time Setup**

```bash
# Navigate to test project
cd Tests/GuitarAlchemistChatbot.Tests.Playwright

# Restore packages
dotnet restore

# Install Playwright browsers
pwsh bin/Debug/net9.0/playwright.ps1 install

# Or on Linux/Mac
bash bin/Debug/net9.0/playwright.sh install
```

---

## 🧪 **Running Tests**

### **Run All Tests**

```bash
dotnet test
```

### **Run Specific Test Suite**

```bash
# Tab viewer tests
dotnet test --filter "FullyQualifiedName~TabViewerTests"

# Context persistence tests
dotnet test --filter "FullyQualifiedName~ContextPersistenceTests"

# Function calling tests
dotnet test --filter "FullyQualifiedName~FunctionCallingTests"

# MCP integration tests
dotnet test --filter "FullyQualifiedName~McpIntegrationTests"
```

### **Run Specific Test**

```bash
dotnet test --filter "FullyQualifiedName~TabViewer_ShouldRenderVexTabNotation"
```

### **Run with Specific Browser**

```bash
# Chromium (default)
dotnet test -- Playwright.BrowserName=chromium

# Firefox
dotnet test -- Playwright.BrowserName=firefox

# WebKit (Safari)
dotnet test -- Playwright.BrowserName=webkit
```

### **Run in Headed Mode (See Browser)**

```bash
dotnet test -- Playwright.LaunchOptions.Headless=false
```

### **Run with Slow Motion**

```bash
dotnet test -- Playwright.LaunchOptions.SlowMo=1000
```

---

## 📊 **Test Configuration**

### **playwright.runsettings**

Configure test behavior:

```xml
<Playwright>
  <BrowserName>chromium</BrowserName>
  <LaunchOptions>
    <Headless>false</Headless>
    <SlowMo>100</SlowMo>
  </LaunchOptions>
  <ExpectTimeout>5000</ExpectTimeout>
  <Timeout>30000</Timeout>
</Playwright>
```

### **Using Custom Settings**

```bash
dotnet test --settings playwright.runsettings
```

---

## 🎯 **Test Structure**

### **Base Class: ChatbotTestBase**

All test classes inherit from `ChatbotTestBase` which provides:

**Helper Methods:**

- `SendMessageAsync(message)` - Send a chat message
- `WaitForResponseAsync()` - Wait for AI response
- `WaitForFunctionCallAsync()` - Wait for function indicator
- `GetContextSummaryAsync()` - Get context indicator text
- `ClickNewChatAsync()` - Start new conversation
- `GetUserMessagesAsync()` - Get all user messages
- `GetAssistantMessagesAsync()` - Get all assistant messages
- `HasVexTabAsync()` - Check for VexTab elements
- `WaitForVexTabRenderAsync()` - Wait for tab rendering

**Configuration:**

- `BaseUrl` - Chatbot URL (default: https://localhost:7001)
- `DefaultTimeout` - Default timeout in milliseconds (30000)

---

## 📝 **Writing New Tests**

### **Example Test**

```csharp
[Test]
public async Task MyNewTest()
{
    // Arrange
    var query = "Show me C major chord";

    // Act
    await SendMessageAsync(query);
    var response = await WaitForResponseAsync();

    // Assert
    Assert.That(response, Is.Not.Empty);
    Assert.That(response.ToLower(), Does.Contain("c major"));
}
```

### **Test Attributes**

```csharp
[TestFixture]
[Parallelizable(ParallelScope.Self)]
public class MyTests : ChatbotTestBase
{
    [Test]
    public async Task MyTest() { }
    
    [TestCase("chromium")]
    [TestCase("firefox")]
    [TestCase("webkit")]
    public async Task CrossBrowserTest(string browser) { }
}
```

---

## 🐛 **Debugging Tests**

### **View Browser During Tests**

```bash
dotnet test -- Playwright.LaunchOptions.Headless=false
```

### **Slow Down Test Execution**

```bash
dotnet test -- Playwright.LaunchOptions.SlowMo=1000
```

### **Take Screenshots on Failure**

Add to test:

```csharp
[TearDown]
public async Task TearDown()
{
    if (TestContext.CurrentContext.Result.Outcome.Status == TestStatus.Failed)
    {
        await Page.ScreenshotAsync(new() 
        { 
            Path = $"screenshot-{TestContext.CurrentContext.Test.Name}.png" 
        });
    }
}
```

### **Enable Verbose Logging**

```bash
dotnet test --logger "console;verbosity=detailed"
```

---

## 📈 **Continuous Integration**

### **GitHub Actions Example**

```yaml
name: Playwright Tests

on: [push, pull_request]

jobs:
  test:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v3
      - uses: actions/setup-dotnet@v3
        with:
          dotnet-version: '9.0.x'
      
      - name: Install dependencies
        run: dotnet restore
      
      - name: Build
        run: dotnet build --no-restore
      
      - name: Install Playwright
        run: pwsh Tests/GuitarAlchemistChatbot.Tests.Playwright/bin/Debug/net9.0/playwright.ps1 install
      
      - name: Run tests
        run: dotnet test --no-build --verbosity normal
      
      - name: Upload screenshots
        if: failure()
        uses: actions/upload-artifact@v3
        with:
          name: screenshots
          path: "**/*.png"
```

---

## 🔍 **Test Scenarios**

### **Tab Viewer Tests**

- ✅ Render VexTab notation
- ✅ Display example tabs
- ✅ Handle multiple tabs
- ✅ Cross-browser compatibility
- ✅ Responsive design
- ✅ Invalid notation handling
- ✅ Auto-scroll behavior

### **Context Persistence Tests**

- ✅ Persist context across messages
- ✅ Display context indicator
- ✅ Track recent chords
- ✅ Clear on new chat
- ✅ Handle multiple references
- ✅ Track music theory concepts
- ✅ Long conversation persistence
- ✅ Ambiguous reference handling

### **Function Calling Tests**

- ✅ Show function indicators
- ✅ Display structured results
- ✅ Handle multiple calls
- ✅ Show loading states
- ✅ Handle errors gracefully
- ✅ Format results readably
- ✅ Allow cancellation
- ✅ Sequential calls

### **MCP Integration Tests**

- ✅ Wikipedia search
- ✅ Wikipedia summaries
- ✅ Music theory site search
- ✅ Latest lessons (RSS)
- ✅ Article fetching
- ✅ Function indicators
- ✅ Multiple sources
- ✅ Inline display
- ✅ Error handling
- ✅ Caching behavior

---

## 📚 **Resources**

### **Documentation**

- [Playwright for .NET](https://playwright.dev/dotnet/)
- [NUnit Documentation](https://docs.nunit.org/)
- [Microsoft.Playwright.NUnit](https://www.nuget.org/packages/Microsoft.Playwright.NUnit)

### **Related Files**

- `ChatbotTestBase.cs` - Base test class
- `playwright.runsettings` - Test configuration
- `../Apps/GuitarAlchemistChatbot/FEATURES_DOCUMENTATION.md` - Feature docs

---

## 🎯 **Best Practices**

1. **Use Descriptive Test Names**
    - `TabViewer_ShouldRenderVexTabNotation` ✅
    - `Test1` ❌

2. **Follow AAA Pattern**
    - Arrange - Set up test data
    - Act - Perform action
    - Assert - Verify results

3. **Keep Tests Independent**
    - Each test should work standalone
    - Don't rely on test execution order

4. **Use Appropriate Timeouts**
    - Default: 30 seconds
    - Adjust for slow operations

5. **Clean Up After Tests**
    - Use `[TearDown]` for cleanup
    - Reset state between tests

6. **Test Edge Cases**
    - Invalid input
    - Network errors
    - Empty responses

---

## 🚨 **Troubleshooting**

### **Browsers Not Installed**

```bash
pwsh bin/Debug/net9.0/playwright.ps1 install
```

### **Tests Timing Out**

- Increase timeout in `playwright.runsettings`
- Check if chatbot is running
- Verify network connection

### **Element Not Found**

- Check selector syntax
- Wait for element to appear
- Verify page loaded correctly

### **Tests Failing Intermittently**

- Add explicit waits
- Check for race conditions
- Increase timeouts

---

**Happy Testing! 🧪✅**

