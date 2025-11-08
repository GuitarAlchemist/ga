# Testing Gaps and Improvement Plan

**Purpose**: Identify testing gaps and create a plan to add missing tests for GraphQL, MCP servers, and React components.

---

## Current Test Coverage Summary

### ✅ Well-Covered Areas:

1. **Backend Tests** (~150 tests)
   - `GA.Business.Core.Tests` (NUnit) - Core music theory, chords, scales, fretboard
   - `AllProjects.AppHost.Tests` (xUnit) - Aspire orchestration, health checks, rate limiting

2. **Blazor Chatbot Tests** (~172 tests)
   - `GuitarAlchemistChatbot.Tests.Playwright` - UI, API, function calling, context, demo mode
   - Comprehensive coverage of chatbot features

### ❌ Testing Gaps:

1. **GraphQL** - No tests found
2. **MCP Servers** - No automated tests (manual testing only)
3. **React Components** - Minimal automated tests
4. **GaApi** - Limited integration tests
5. **MongoDB Integration** - Basic tests only
6. **Vector Search** - No tests
7. **Semantic Indexing** - No tests

---

## Gap 1: GraphQL Tests

### Current State:
- ❌ No GraphQL tests found
- GraphQL endpoint exists in GaApi
- HotChocolate framework used
- No schema validation tests
- No query/mutation tests

### Required Tests:

#### Unit Tests (HotChocolate):

```csharp
// Tests/GaApi.GraphQL.Tests/SchemaTests.cs
[TestFixture]
public class GraphQLSchemaTests
{
    [Test]
    public async Task Schema_ShouldBeValid()
    {
        // Verify schema compiles without errors
    }
    
    [Test]
    public async Task Schema_ShouldIncludeChordQueries()
    {
        // Verify chord queries are available
    }
}

// Tests/GaApi.GraphQL.Tests/ChordQueryTests.cs
[TestFixture]
public class ChordQueryTests
{
    [Test]
    public async Task Query_SearchChords_ShouldReturnResults()
    {
        var query = @"
            query {
                chords(search: ""Cmaj7"") {
                    name
                    notes
                }
            }
        ";
        // Execute and verify
    }
    
    [Test]
    public async Task Query_GetChordById_ShouldReturnChord()
    {
        // Test by ID lookup
    }
}
```

#### Integration Tests:

```csharp
// Tests/GaApi.GraphQL.Tests/GraphQLIntegrationTests.cs
[TestFixture]
public class GraphQLIntegrationTests
{
    [Test]
    public async Task GraphQL_Endpoint_ShouldBeAccessible()
    {
        // Test /graphql endpoint
    }
    
    [Test]
    public async Task GraphQL_Playground_ShouldBeAvailable()
    {
        // Test /graphql/playground (dev only)
    }
}
```

### Estimated Effort: 8-12 hours

---

## Gap 2: MCP Server Tests

### Current State:
- ❌ No automated tests for `GaMcpServer`
- ❌ No tests for `mcp-servers/` experimental agents
- Manual testing only (documented in USAGE_EXAMPLES.md)
- Web integration tools exist but untested

### Required Tests:

#### Unit Tests (MCP Tools):

```csharp
// Tests/GaMcpServer.Tests/WebScraperToolTests.cs
[TestFixture]
public class WebScraperToolTests
{
    [Test]
    public async Task FetchWebPage_ValidUrl_ShouldReturnContent()
    {
        var tool = new WebScraperTool(/* dependencies */);
        var result = await tool.FetchWebPage("https://example.com", false);
        
        Assert.That(result, Is.Not.Empty);
    }
    
    [Test]
    public async Task FetchWebPage_BlockedDomain_ShouldThrowException()
    {
        // Test domain blocking
    }
    
    [Test]
    public async Task ExtractLinks_ShouldReturnLinks()
    {
        // Test link extraction
    }
}

// Tests/GaMcpServer.Tests/FeedReaderToolTests.cs
[TestFixture]
public class FeedReaderToolTests
{
    [Test]
    public async Task ReadFeed_KnownFeed_ShouldReturnItems()
    {
        var tool = new FeedReaderTool(/* dependencies */);
        var result = await tool.ReadFeed("musictheory", 5);
        
        Assert.That(result, Has.Count.LessThanOrEqualTo(5));
    }
    
    [Test]
    public async Task SearchFeed_WithKeyword_ShouldFilterResults()
    {
        // Test feed search
    }
}
```

#### Integration Tests:

```csharp
// Tests/GaMcpServer.Tests/McpServerIntegrationTests.cs
[TestFixture]
public class McpServerIntegrationTests
{
    [Test]
    public async Task McpServer_ShouldStartSuccessfully()
    {
        // Test server startup
    }
    
    [Test]
    public async Task McpServer_ShouldListTools()
    {
        // Test tool discovery
    }
    
    [Test]
    public async Task McpServer_ShouldExecuteTool()
    {
        // Test tool execution
    }
}
```

### Estimated Effort: 12-16 hours

---

## Gap 3: React Component Tests

### Current State:
- ✅ Playwright config exists (`ReactComponents/ga-react-components/playwright.config.ts`)
- ❌ No test files in `ReactComponents/ga-react-components/tests/`
- 40+ test pages exist but no automated tests
- Manual testing only

### Required Tests:

#### Component Tests (Playwright):

```typescript
// ReactComponents/ga-react-components/tests/fretboard.spec.ts
import { test, expect } from '@playwright/test';

test.describe('GuitarFretboard Component', () => {
  test('should render fretboard', async ({ page }) => {
    await page.goto('/test/guitar-fretboard');
    
    const fretboard = page.locator('[data-testid="guitar-fretboard"]');
    await expect(fretboard).toBeVisible();
  });
  
  test('should display chord positions', async ({ page }) => {
    await page.goto('/test/guitar-fretboard');
    
    const positions = page.locator('[data-testid="fret-position"]');
    await expect(positions).toHaveCount(3); // C major chord
  });
  
  test('should handle click events', async ({ page }) => {
    await page.goto('/test/guitar-fretboard');
    
    const fret = page.locator('[data-testid="fret-0-0"]');
    await fret.click();
    
    // Verify click handler was called
  });
});

// ReactComponents/ga-react-components/tests/three-fretboard.spec.ts
test.describe('ThreeFretboard Component', () => {
  test('should render 3D fretboard', async ({ page }) => {
    await page.goto('/test/three-fretboard');
    
    const canvas = page.locator('canvas');
    await expect(canvas).toBeVisible();
  });
  
  test('should support orbit controls', async ({ page }) => {
    await page.goto('/test/three-fretboard');
    
    // Test mouse drag for rotation
    const canvas = page.locator('canvas');
    await canvas.dragTo(canvas, {
      sourcePosition: { x: 100, y: 100 },
      targetPosition: { x: 200, y: 200 }
    });
    
    // Verify camera position changed
  });
});

// ReactComponents/ga-react-components/tests/webgpu-fretboard.spec.ts
test.describe('WebGPUFretboard Component', () => {
  test('should render with WebGPU or fallback to WebGL', async ({ page }) => {
    await page.goto('/test/webgpu-fretboard');
    
    const canvas = page.locator('canvas');
    await expect(canvas).toBeVisible();
    
    // Check for WebGPU or WebGL context
  });
});
```

#### Visual Regression Tests:

```typescript
// ReactComponents/ga-react-components/tests/visual.spec.ts
test.describe('Visual Regression Tests', () => {
  test('fretboard should match snapshot', async ({ page }) => {
    await page.goto('/test/guitar-fretboard');
    await expect(page).toHaveScreenshot('fretboard.png');
  });
  
  test('3d-fretboard should match snapshot', async ({ page }) => {
    await page.goto('/test/three-fretboard');
    await page.waitForTimeout(2000); // Wait for 3D render
    await expect(page).toHaveScreenshot('three-fretboard.png');
  });
});
```

### Estimated Effort: 16-20 hours

---

## Gap 4: GaApi Integration Tests

### Current State:
- ✅ Basic health check tests exist
- ❌ Limited endpoint coverage
- ❌ No authentication tests
- ❌ No rate limiting tests (except in AppHost tests)

### Required Tests:

```csharp
// Tests/GaApi.Tests/ChordEndpointTests.cs
[TestFixture]
public class ChordEndpointTests
{
    [Test]
    public async Task GET_Chords_ShouldReturnChords()
    {
        // Test /api/chords endpoint
    }
    
    [Test]
    public async Task GET_Chords_Search_ShouldFilterResults()
    {
        // Test /api/chords/search?query=Cmaj7
    }
    
    [Test]
    public async Task POST_Chords_ShouldCreateChord()
    {
        // Test chord creation (if supported)
    }
}

// Tests/GaApi.Tests/VectorSearchEndpointTests.cs
[TestFixture]
public class VectorSearchEndpointTests
{
    [Test]
    public async Task POST_VectorSearch_ShouldReturnSimilarChords()
    {
        // Test semantic search endpoint
    }
}
```

### Estimated Effort: 8-12 hours

---

## Gap 5: MongoDB Integration Tests

### Current State:
- ✅ Basic connection test exists
- ❌ No CRUD operation tests
- ❌ No vector search tests
- ❌ No aggregation pipeline tests

### Required Tests:

```csharp
// Tests/GA.Data.MongoDB.Tests/ChordRepositoryTests.cs
[TestFixture]
public class ChordRepositoryTests
{
    [Test]
    public async Task Insert_Chord_ShouldPersist()
    {
        // Test chord insertion
    }
    
    [Test]
    public async Task Find_Chord_ByName_ShouldReturnChord()
    {
        // Test chord lookup
    }
    
    [Test]
    public async Task VectorSearch_ShouldReturnSimilarChords()
    {
        // Test MongoDB vector search
    }
}
```

### Estimated Effort: 6-8 hours

---

## Implementation Plan

### Phase 1: GraphQL Tests (Week 1)
- [ ] Create `Tests/GaApi.GraphQL.Tests` project
- [ ] Add schema validation tests
- [ ] Add query tests (chords, scales, progressions)
- [ ] Add mutation tests (if applicable)
- [ ] Add integration tests
- [ ] Document GraphQL testing patterns

**Estimated Time**: 8-12 hours

### Phase 2: MCP Server Tests (Week 2)
- [ ] Create `Tests/GaMcpServer.Tests` project
- [ ] Add WebScraperTool tests
- [ ] Add FeedReaderTool tests
- [ ] Add WikipediaTool tests
- [ ] Add SearchTool tests
- [ ] Add integration tests
- [ ] Document MCP testing patterns

**Estimated Time**: 12-16 hours

### Phase 3: React Component Tests (Week 3-4)
- [ ] Create test files in `ReactComponents/ga-react-components/tests/`
- [ ] Add GuitarFretboard tests
- [ ] Add ThreeFretboard tests
- [ ] Add WebGPUFretboard tests
- [ ] Add RealisticFretboard tests
- [ ] Add visual regression tests
- [ ] Add accessibility tests
- [ ] Document React testing patterns

**Estimated Time**: 16-20 hours

### Phase 4: GaApi Integration Tests (Week 5)
- [ ] Create `Tests/GaApi.Tests` project
- [ ] Add chord endpoint tests
- [ ] Add scale endpoint tests
- [ ] Add progression endpoint tests
- [ ] Add vector search tests
- [ ] Add authentication tests
- [ ] Document API testing patterns

**Estimated Time**: 8-12 hours

### Phase 5: MongoDB Integration Tests (Week 6)
- [ ] Create `Tests/GA.Data.MongoDB.Tests` project
- [ ] Add repository tests
- [ ] Add vector search tests
- [ ] Add aggregation tests
- [ ] Document MongoDB testing patterns

**Estimated Time**: 6-8 hours

---

## Testing Tools and Frameworks

### Backend (.NET):
- **NUnit** - Unit tests
- **xUnit** - Integration tests (Aspire)
- **Moq** - Mocking framework
- **FluentAssertions** - Assertion library
- **TestContainers** - Docker containers for integration tests

### Frontend (React):
- **Playwright** - E2E and component tests
- **Jest** - Unit tests (if needed)
- **React Testing Library** - Component tests (if needed)
- **Storybook** - Component documentation and visual testing

### GraphQL:
- **HotChocolate.Testing** - GraphQL schema and query tests
- **Snapshooter** - Snapshot testing for GraphQL responses

### MCP:
- **Custom test harness** - MCP protocol testing
- **Integration tests** - Full server testing

---

## Success Criteria

### Coverage Targets:
- **GraphQL**: 80%+ coverage of queries and mutations
- **MCP Servers**: 70%+ coverage of tools and services
- **React Components**: 60%+ coverage of critical components
- **GaApi**: 70%+ coverage of endpoints
- **MongoDB**: 70%+ coverage of repositories

### Quality Metrics:
- All tests pass in CI/CD
- No flaky tests
- Fast test execution (<5 minutes for unit tests)
- Clear test documentation
- Easy to run locally

---

## Continuous Integration

### GitHub Actions Workflow:

```yaml
name: Tests

on: [push, pull_request]

jobs:
  backend-tests:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v3
      - uses: actions/setup-dotnet@v3
        with:
          dotnet-version: '9.0.x'
      - run: dotnet test --filter "Category!=Integration"
      
  integration-tests:
    runs-on: ubuntu-latest
    services:
      mongodb:
        image: mongo:latest
    steps:
      - uses: actions/checkout@v3
      - uses: actions/setup-dotnet@v3
      - run: dotnet test --filter "Category=Integration"
      
  react-tests:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v3
      - uses: actions/setup-node@v3
        with:
          node-version: '18'
      - run: npm ci
      - run: npx playwright install
      - run: npm test
```

---

## Summary

### Total Estimated Effort: 50-68 hours (6-8 weeks)

### Priority Order:
1. **High**: GraphQL tests (critical for API)
2. **High**: MCP server tests (new feature, needs validation)
3. **Medium**: React component tests (manual testing works for now)
4. **Medium**: GaApi integration tests (basic coverage exists)
5. **Low**: MongoDB tests (basic coverage exists)

### Recommended Approach:
- Start with GraphQL tests (Week 1)
- Add MCP server tests (Week 2)
- Gradually add React tests (Weeks 3-4)
- Fill in remaining gaps as time permits

### Benefits:
- ✅ Increased confidence in deployments
- ✅ Faster bug detection
- ✅ Better documentation through tests
- ✅ Easier refactoring
- ✅ Improved code quality

