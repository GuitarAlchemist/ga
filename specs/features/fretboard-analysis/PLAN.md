# Fretboard Analysis System - Technical Plan

## Architecture Overview

### System Components

```
┌─────────────────────────────────────────────────────────────┐
│                     GraphQL API Layer                        │
│  (Apps/ga-server/GaApi/GraphQL/Queries/FretboardQuery.cs)  │
└────────────────────┬────────────────────────────────────────┘
                     │
                     ▼
┌─────────────────────────────────────────────────────────────┐
│                  GraphQL Type Layer                          │
│   (Apps/ga-server/GaApi/GraphQL/Types/                     │
│    FretboardChordAnalysisType.cs)                           │
└────────────────────┬────────────────────────────────────────┘
                     │
                     ▼
┌─────────────────────────────────────────────────────────────┐
│              Business Logic Layer                            │
│  (Common/GA.Business.Core/Fretboard/Analysis/)              │
│  - FretboardChordAnalyzer                                   │
│  - PhysicalFretboardCalculator                              │
│  - BiomechanicalAnalyzer (optional)                         │
└─────────────────────────────────────────────────────────────┘
```

### Data Flow

1. **GraphQL Query** → FretboardQuery resolver
2. **Resolver** → FretboardChordAnalyzer (generates chord voicings)
3. **Analyzer** → PhysicalFretboardCalculator (physical analysis)
4. **Calculator** → Physical measurements and difficulty classification
5. **Results** → GraphQL types (serialization)
6. **Response** → Client application

## Technology Stack

### Backend Framework
- **.NET 9**: Latest LTS version with performance improvements
- **ASP.NET Core**: Web framework for API hosting
- **HotChocolate 14**: GraphQL server implementation

### GraphQL Implementation
- **Query-based API**: Read-only operations (no mutations for now)
- **Filtering**: Built-in HotChocolate filtering support
- **Sorting**: Built-in HotChocolate sorting support
- **Projections**: Efficient field selection

### Core Libraries
- **GA.Business.Core**: Existing fretboard analysis logic
- **System.Collections.Immutable**: Immutable data structures
- **System.Numerics**: Mathematical calculations

### Development Tools
- **Visual Studio 2022**: Primary IDE
- **Rider**: Alternative IDE
- **Banana Cake Pop**: GraphQL IDE for testing

## Implementation Details

### 1. GraphQL Schema Design

#### Input Types

```graphql
input FretSpanInput {
  startFret: Int! = 0
  endFret: Int! = 5
  maxResults: Int
  difficultyFilter: String
  includeBiomechanicalAnalysis: Boolean! = false
  includePhysicalAnalysis: Boolean! = false
}
```

#### Output Types

```graphql
type FretboardChordAnalysisType {
  chordName: String!
  hybridAnalysisName: String!
  iconicName: String
  iconicDescription: String
  fretSpan: Int!
  lowestFret: Int!
  highestFret: Int!
  difficulty: String!
  isPlayable: Boolean!
  voicingDescription: String!
  notes: [String!]!
  pitchClasses: [Int!]!
  cagedShape: String
  cagedSimilarity: Float
  fingeringPattern: String
  biomechanicalData: BiomechanicalDataType
  physicalPlayabilityData: PhysicalPlayabilityDataType
}

type PhysicalPlayabilityDataType {
  fretSpanMM: Float!
  maxFingerStretchMM: Float!
  averageFingerStretchMM: Float!
  verticalSpanMM: Float!
  diagonalStretchMM: Float!
  difficulty: String!
  isPlayable: Boolean!
  difficultyReason: String!
  suggestedFingering: [FingerPositionType!]!
}

type FingerPositionType {
  string: Int!
  fret: Int!
  fingerNumber: Int!
  technique: String!
}
```

#### Queries

```graphql
type Query {
  analyzeFretSpan(input: FretSpanInput!): FretSpanAnalysisResult!
  getChordByPattern(fretPattern: [Int!]!, includePhysicalAnalysis: Boolean = false): FretboardChordAnalysisType
  searchChordsByName(searchTerm: String!, maxResults: Int = 50, includePhysicalAnalysis: Boolean = false): [FretboardChordAnalysisType!]!
  getEquivalenceGroups(startFret: Int = 0, endFret: Int = 5, maxGroups: Int = 20, includePhysicalAnalysis: Boolean = false): [ChordEquivalenceGroup!]!
}
```

### 2. Physical Analysis Implementation

#### PhysicalFretboardCalculator

**Location**: `Common/GA.Business.Core/Fretboard/Analysis/PhysicalFretboardCalculator.cs`

**Key Methods**:
- `CalculateFretPositionMM(int fretNumber, double scaleLengthMM)`: Distance from nut to fret
- `CalculateFretDistanceMM(int fret1, int fret2, double scaleLengthMM)`: Distance between frets
- `CalculateStringSpacingMM(int fretNumber, ...)`: String spacing at a fret
- `AnalyzePlayability(ImmutableList<Position> positions, ...)`: Complete analysis

**Physical Formulas**:
- Fret position: `scaleLength * (1 - 2^(-fret/12))`
- String spacing: Linear interpolation from nut to bridge
- Diagonal stretch: `sqrt(fretSpan² + verticalSpan²)`

**Scale Lengths**:
- Electric: 648mm (Fender standard)
- Classical: 650mm
- Gibson: 628mm
- Bass: 864mm

#### Difficulty Classification

**Algorithm**:
1. Calculate adjusted stretch based on fret position (higher frets are easier)
2. Check for impossible conditions (> 6 fret span, > 160mm stretch)
3. Classify based on adjusted stretch thresholds
4. Generate human-readable reason

**Position Factors**:
- Frets 0-3: 1.0 (full difficulty)
- Frets 4-7: 0.9 (slightly easier)
- Frets 8-12: 0.8 (easier)
- Frets 13-17: 0.7 (much easier)
- Frets 18+: 0.6 (significantly easier)

### 3. Integration Points

#### Existing Systems

**FretboardChordAnalyzer**:
- Already generates chord voicings
- Already classifies difficulty (using physical analysis)
- Already determines playability
- Integration point: Add physical data to GraphQL types

**BiomechanicalAnalyzer** (Optional):
- Advanced IK-based analysis
- Hand size modeling
- Comfort scoring
- Integration: Optional flag in queries

#### New Components

**PhysicalPlayabilityDataType**:
- Maps PhysicalPlayabilityAnalysis to GraphQL
- Includes all physical measurements
- Includes suggested fingerings

**FingerPositionType**:
- Maps FingerPosition to GraphQL
- Includes string, fret, finger number, technique

### 4. Performance Optimization

#### Caching Strategy

**Not Implemented Yet** (Future Enhancement):
- Cache physical analysis results by position hash
- Cache chord generation results by fret span
- Use in-memory cache with LRU eviction

#### Query Optimization

**Current Approach**:
- Lazy evaluation of chord generation
- Early filtering by difficulty
- Limit results before physical analysis
- Optional physical analysis (only when requested)

**Performance Targets**:
- < 500ms for typical queries (0-5 fret span, 50 results)
- < 50ms overhead for physical analysis per chord
- < 100ms overhead for biomechanical analysis per chord

### 5. Error Handling

#### Validation

**Input Validation**:
- Fret pattern must have exactly 6 values
- Fret numbers must be >= -1 (muted) and <= 24
- Start fret must be <= end fret
- Max results must be > 0

**Error Responses**:
- Invalid input: ArgumentException with clear message
- Analysis failure: Return null or empty result
- Timeout: Return partial results with warning

#### Logging

**Log Levels**:
- Information: Query execution, result counts
- Warning: Invalid inputs, partial failures
- Error: Unexpected exceptions, analysis failures

**Telemetry**:
- Query execution time
- Result counts
- Physical analysis overhead
- Error rates

### 6. Testing Strategy

#### Unit Tests

**PhysicalFretboardCalculator**:
- Test fret position calculations
- Test string spacing calculations
- Test difficulty classification
- Test playability determination
- Test suggested fingering generation

**GraphQL Types**:
- Test FromAnalysis mapping
- Test null handling
- Test optional fields

#### Integration Tests

**GraphQL Queries**:
- Test analyzeFretSpan with various inputs
- Test getChordByPattern with valid/invalid patterns
- Test searchChordsByName with various search terms
- Test getEquivalenceGroups with various ranges

**Performance Tests**:
- Measure query execution time
- Measure physical analysis overhead
- Measure memory usage

### 7. Deployment

#### Configuration

**appsettings.json**:
```json
{
  "GraphQL": {
    "MaxExecutionDepth": 10,
    "MaxComplexity": 100,
    "EnableIntrospection": true
  },
  "FretboardAnalysis": {
    "DefaultScaleLength": 648.0,
    "MaxFretSpan": 24,
    "MaxResults": 1000
  }
}
```

#### Endpoints

**Development**:
- GraphQL: `https://localhost:7001/graphql`
- Banana Cake Pop: `https://localhost:7001/graphql`

**Production**:
- GraphQL: `https://api.guitaralchemist.com/graphql`
- Documentation: `https://api.guitaralchemist.com/graphql/schema`

### 8. Security

#### Rate Limiting

**Not Implemented Yet** (Future Enhancement):
- Limit queries per IP address
- Limit complexity per query
- Limit execution time per query

#### Authentication

**Not Required** (Public API):
- No authentication for read-only queries
- Future: API keys for write operations
- Future: OAuth for user-specific data

### 9. Monitoring

#### Metrics

**Application Insights**:
- Query execution time
- Error rates
- Result counts
- Physical analysis overhead

**Custom Metrics**:
- Difficulty distribution
- Most queried fret spans
- Physical analysis adoption rate

#### Alerts

**Performance Alerts**:
- Query execution time > 1s
- Error rate > 5%
- Memory usage > 80%

**Business Alerts**:
- Query volume drops > 50%
- New error types appear
- Physical analysis failures > 10%

## Migration Plan

### Phase 1: Complete GraphQL Integration (Current)
- ✅ Add PhysicalPlayabilityDataType
- ✅ Add FingerPositionType
- ✅ Update FretboardChordAnalysisType
- ✅ Update all query methods
- ✅ Add includePhysicalAnalysis flag

### Phase 2: Testing & Documentation
- [ ] Write unit tests for new types
- [ ] Write integration tests for queries
- [ ] Document GraphQL schema
- [ ] Create example queries
- [ ] Update API documentation

### Phase 3: Performance Optimization
- [ ] Implement caching
- [ ] Add query complexity analysis
- [ ] Optimize chord generation
- [ ] Add performance monitoring

### Phase 4: Advanced Features
- [ ] Add hand size parameter
- [ ] Add capo support
- [ ] Add alternative tunings
- [ ] Add mutation support (save favorites)

## Dependencies

### NuGet Packages
- HotChocolate.AspNetCore (14.x)
- HotChocolate.Data (14.x)
- System.Collections.Immutable (9.x)

### Internal Dependencies
- GA.Business.Core.Fretboard
- GA.Business.Core.Fretboard.Analysis
- GA.Business.Core.Fretboard.Biomechanics

## Risks & Mitigation

### Technical Risks

**Risk**: Physical analysis is too slow
**Mitigation**: Implement caching, optimize calculations, make analysis optional

**Risk**: GraphQL queries are too complex
**Mitigation**: Implement query complexity limits, add pagination

**Risk**: Memory usage is too high
**Mitigation**: Use lazy evaluation, limit result sets, implement streaming

### Business Risks

**Risk**: Users don't find physical analysis useful
**Mitigation**: Make it optional, gather feedback, iterate on metrics

**Risk**: Difficulty classifications don't match user expectations
**Mitigation**: Validate with expert guitarists, allow customization

## Success Criteria

### Technical Success
- ✅ GraphQL endpoint returns physical analysis data
- ✅ Physical measurements are accurate within 5mm
- ✅ Queries complete in < 500ms for typical requests
- [ ] Unit test coverage > 80%
- [ ] Integration test coverage > 60%

### User Success
- [ ] 10+ developers integrate the API
- [ ] 95%+ query success rate
- [ ] 4.5+ star rating from users
- [ ] 60%+ of queries include physical analysis

## Next Steps

1. Complete testing and documentation
2. Deploy to staging environment
3. Gather feedback from beta users
4. Optimize performance based on real usage
5. Plan Phase 4 advanced features

