# GA.Business.Core Code Coverage Plan

## Goal
Achieve **80% code coverage** for all classes in `Common/GA.Business.Core`

## Current Status

### Test Discovery
- **Total Test Cases**: 769 tests discovered
- **Test Project**: `Tests/Common/GA.Business.Core.Tests/GA.Business.Core.Tests.csproj`
- **Test Framework**: NUnit

### Known Issues
1. **Integration Tests Failing**: ChatbotIntegrationTests require external API (localhost:7001)
2. **IK Solver Tests Failing**: InverseKinematicsSolverTests have assertion failures
   - `Solve_RestPoseTarget_ShouldConverge`: Expected 100 generations, got 1
   - `Solve_ShouldPopulateSolutionMetadata`: Expected 20 generations, got 1

## Coverage Analysis Strategy

### Phase 1: Baseline Coverage Report (CURRENT)
**Objective**: Generate initial coverage report to identify gaps

**Commands**:
```powershell
# Run tests excluding integration tests
dotnet test Tests/Common/GA.Business.Core.Tests/GA.Business.Core.Tests.csproj `
  --collect:"XPlat Code Coverage" `
  --filter "TestCategory!=Integration" `
  --logger "console;verbosity=minimal" `
  --results-directory coverage-results

# Generate HTML coverage report
reportgenerator `
  -reports:coverage-results/**/coverage.cobertura.xml `
  -targetdir:coverage-report `
  -reporttypes:Html;Cobertura;TextSummary
```

**Expected Output**:
- `coverage-report/index.html` - Interactive coverage report
- `coverage-report/Summary.txt` - Text summary with percentages
- Identification of untested/under-tested classes

### Phase 2: Priority Areas for Testing

Based on the codebase structure, prioritize testing in this order:

#### **High Priority** (Core Business Logic - Target 90%+)
1. **Chords** (`Chords/`)
   - ChordNamingService
   - ChordAnalyzer
   - ChordBuilder
   - ChordTemplateFactory
   - Existing: ChordNamingTests, ChordStackingTypesTests, IconicChordTests

2. **Fretboard** (`Fretboard/`)
   - FretboardAnalyzer
   - PositionCalculator
   - BiomechanicsEngine
   - FretboardShapeAnalyzer
   - Existing: FretboardTests, TuningTests, PositionTests, BiomechanicsTests

3. **Notes & Intervals** (`Notes/`, `Intervals/`)
   - Note, PitchClass, Pitch
   - Interval, IntervalFormula
   - Existing: NoteTests, PitchClassTests

4. **Scales** (`Scales/`)
   - ScaleTemplateFactory
   - ScaleAnalyzer
   - Existing: Some scale tests

#### **Medium Priority** (Music Theory - Target 80%+)
5. **Atonal Theory** (`Atonal/`)
   - ForteNumber
   - PitchClassSet
   - SetClass
   - Existing: ForteNumberTests, PitchClassSetTests, SetClassTests

6. **Tonal Theory** (`Tonal/`)
   - Key, KeySignature
   - MajorKey, MinorKey
   - ModalFamily
   - Existing: KeyTests, KeySignatureTests, MajorKeyTests, MinorKeyTests, ModalFamilyTests

7. **Configuration** (`Configuration/`)
   - MusicalKnowledgeService
   - YAML config loaders
   - Existing: ModesConfigTests, ScalesConfigTests, MusicalKnowledgeServiceTests

#### **Lower Priority** (Support Services - Target 70%+)
8. **Services** (`Services/`)
   - InvariantValidationService
   - InvariantAnalyticsService
   - Existing: InvariantValidationServiceTests, InvariantAnalyticsServiceTests

9. **Spatial** (`Spatial/`)
   - TonalBSP
   - Existing: TonalBSPTests

10. **AI** (`AI/`)
    - InvariantAIService
    - RedisVectorService
    - Existing: InvariantAIServiceTests, RedisVectorServiceTests (may require mocking)

### Phase 3: Test Implementation Plan

#### **Step 1: Fix Existing Failing Tests**
- [ ] Fix InverseKinematicsSolverTests
  - Investigate why GenerationCount is 1 instead of expected values
  - Check solver configuration and convergence criteria
- [ ] Skip or mock ChatbotIntegrationTests
  - Add `[Category("Integration")]` attribute
  - Create mock-based unit tests for chatbot logic

#### **Step 2: Identify Coverage Gaps**
After generating the baseline report:
- [ ] List all classes with <50% coverage
- [ ] List all classes with 0% coverage
- [ ] Prioritize based on business criticality

#### **Step 3: Write Missing Tests**
For each under-tested class:
1. **Constructor Tests**: Verify object initialization
2. **Property Tests**: Test getters/setters, validation
3. **Method Tests**: Test core business logic
   - Happy path scenarios
   - Edge cases (null, empty, boundary values)
   - Error conditions
4. **Integration Tests**: Test interactions between classes

#### **Step 4: Continuous Monitoring**
- [ ] Run coverage after each test batch
- [ ] Track progress toward 80% goal
- [ ] Update this document with progress

## Test Categories

### Existing Test Categories
Based on the test project structure:
- **AI**: ChatbotIntegrationTests, InvariantAIServiceTests, RedisVectorServiceTests
- **Analytics**: InvariantAnalyticsServiceTests, Spectral tests
- **Atonal**: ForteNumberTests, PitchClassSetTests, SetClassTests, Grothendieck tests
- **Chords**: ChordNamingTests, ChordStackingTypesTests, IconicChordTests
- **Config**: InstrumentsConfigTests, ModesConfigTests, ScalesConfigTests
- **Configuration**: MusicalKnowledgeServiceTests
- **Fretboard**: FretboardTests, TuningTests, PositionTests, Biomechanics, Invariants, Primitives, Shapes
- **Integration**: BSPIntegrationTests, InvariantSystemIntegrationTests
- **Invariants**: IconicChordInvariantsTests
- **Microservices**: MonadicServiceTests
- **Notes**: NoteTests, PitchClassTests
- **Performance**: InvariantPerformanceBenchmarks
- **Services**: InvariantValidationServiceTests
- **Spatial**: TonalBSPTests
- **Tonal**: KeyTests, KeySignatureTests, MajorKeyTests, MinorKeyTests, ModalFamilyTests

### Recommended Test Categories to Add
- `[Category("Unit")]` - Fast, isolated unit tests
- `[Category("Integration")]` - Tests requiring external services
- `[Category("Performance")]` - Performance benchmarks
- `[Category("Slow")]` - Tests that take >1 second

## Coverage Targets by Namespace

| Namespace | Target Coverage | Priority | Notes |
|-----------|----------------|----------|-------|
| `Chords` | 90% | High | Core business logic |
| `Fretboard` | 90% | High | Core business logic |
| `Notes` | 95% | High | Fundamental primitives |
| `Intervals` | 95% | High | Fundamental primitives |
| `Scales` | 85% | High | Core business logic |
| `Atonal` | 80% | Medium | Music theory |
| `Tonal` | 80% | Medium | Music theory |
| `Configuration` | 75% | Medium | Config loaders |
| `Services` | 70% | Lower | Support services |
| `Spatial` | 70% | Lower | BSP analysis |
| `AI` | 60% | Lower | May require mocking |
| `Analytics` | 70% | Lower | Analytics services |
| `Invariants` | 75% | Medium | Business rules |
| `Microservices` | 70% | Lower | Infrastructure |

## Next Steps

1. **Run baseline coverage** (see Phase 1 commands above)
2. **Analyze coverage report** to identify specific gaps
3. **Fix failing tests** (IK Solver, Chatbot Integration)
4. **Write missing tests** for under-covered classes
5. **Monitor progress** toward 80% goal

## Success Criteria

- [ ] Overall code coverage ≥ 80%
- [ ] All high-priority namespaces ≥ 85%
- [ ] All medium-priority namespaces ≥ 75%
- [ ] No critical business logic classes with <50% coverage
- [ ] All tests passing (excluding skipped integration tests)
- [ ] Coverage report generated and reviewed

## Tools & Resources

### Coverage Tools
- **coverlet**: .NET code coverage library (already integrated via `--collect:"XPlat Code Coverage"`)
- **ReportGenerator**: Generates HTML reports from coverage data
  ```powershell
  dotnet tool install -g dotnet-reportgenerator-globaltool
  ```

### Running Tests
```powershell
# All tests
dotnet test Tests/Common/GA.Business.Core.Tests/GA.Business.Core.Tests.csproj

# Exclude integration tests
dotnet test Tests/Common/GA.Business.Core.Tests/GA.Business.Core.Tests.csproj --filter "TestCategory!=Integration"

# Specific test class
dotnet test Tests/Common/GA.Business.Core.Tests/GA.Business.Core.Tests.csproj --filter "FullyQualifiedName~ChordNamingTests"

# With coverage
dotnet test Tests/Common/GA.Business.Core.Tests/GA.Business.Core.Tests.csproj --collect:"XPlat Code Coverage"
```

### Viewing Coverage
```powershell
# Generate HTML report
reportgenerator -reports:**/coverage.cobertura.xml -targetdir:coverage-report -reporttypes:Html

# Open in browser
Start-Process coverage-report/index.html
```

## Notes

- **Integration tests** require external services (MongoDB, Redis, Chatbot API) - consider mocking or using TestContainers
- **Performance tests** may be slow - consider running separately
- **IK Solver tests** need investigation - may have configuration issues
- Focus on **business logic coverage** first, then infrastructure code

