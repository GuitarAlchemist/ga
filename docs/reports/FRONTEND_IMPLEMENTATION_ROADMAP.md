# Frontend Implementation Roadmap
## Contextual Chord & Voicing System

### Overview

This roadmap outlines the implementation of a **contextual chord and voicing system** that addresses the legacy Delphi system's problems of too many chords, wrong naming, and too many voicings.

---

## Phase 1: Backend Services (2 weeks)

### Week 1: Core Services

#### 1.1 ContextualChordService
**File**: `Apps/ga-server/GaApi/Services/ContextualChordService.cs`

```csharp
public class ContextualChordService
{
    private readonly IChordTemplateRepository _chordRepository;
    private readonly IKeyAwareChordNamingService _namingService;
    
    public async Task<IEnumerable<ChordInContext>> GetChordsForKeyAsync(
        Key key, 
        ChordFilters filters)
    {
        // 1. Get diatonic chords for key
        var diatonicChords = GenerateDiatonicChords(key);
        
        // 2. Add common borrowed chords
        var borrowedChords = GetCommonBorrowedChords(key);
        
        // 3. Filter by user preferences
        var filtered = ApplyFilters(diatonicChords.Concat(borrowedChords), filters);
        
        // 4. Rank by commonality
        return RankByCommonality(filtered, key);
    }
    
    public async Task<IEnumerable<ChordInContext>> GetChordsForScaleAsync(
        Scale scale, 
        ChordFilters filters)
    {
        // Generate modal chords for each degree
        var modalChords = new List<ChordInContext>();
        
        for (int degree = 1; degree <= scale.Notes.Count; degree++)
        {
            var chords = GenerateModalChords(scale, degree, filters.Extension);
            modalChords.AddRange(chords);
        }
        
        return ApplyFilters(modalChords, filters);
    }
    
    public async Task<IEnumerable<ChordInContext>> GetChordsForModeAsync(
        ScaleMode mode, 
        ChordFilters filters)
    {
        // Generate chords for mode degrees
        var chords = ChordTemplateFactory.CreateModalChords(
            mode, 
            filters.Extension, 
            filters.StackingType);
        
        // Convert to ChordInContext with modal information
        return chords.Select(c => CreateChordInContext(c, mode));
    }
}
```

**Tasks**:
- [ ] Create `ChordInContext` record
- [ ] Implement `GetChordsForKeyAsync`
- [ ] Implement `GetChordsForScaleAsync`
- [ ] Implement `GetChordsForModeAsync`
- [ ] Add unit tests

#### 1.2 VoicingFilterService
**File**: `Apps/ga-server/GaApi/Services/VoicingFilterService.cs`

```csharp
public class VoicingFilterService
{
    private readonly IFretboardChordsGenerator _generator;
    private readonly IPsychoacousticVoicingAnalyzer _analyzer;
    
    public async Task<IEnumerable<VoicingWithAnalysis>> GetVoicingsForChordAsync(
        ChordTemplate template,
        PitchClass root,
        VoicingFilters filters)
    {
        // 1. Generate all possible voicings
        var allVoicings = _generator.GetChordPositions(template.PitchClassSet);
        
        // 2. Analyze each voicing
        var analyzed = allVoicings.Select(v => AnalyzeVoicing(v));
        
        // 3. Apply filters
        var filtered = ApplyFilters(analyzed, filters);
        
        // 4. Rank by utility
        return RankByUtility(filtered, filters.Context);
    }
    
    private IEnumerable<VoicingWithAnalysis> ApplyFilters(
        IEnumerable<VoicingWithAnalysis> voicings,
        VoicingFilters filters)
    {
        var result = voicings;
        
        if (filters.MaxDifficulty.HasValue)
            result = result.Where(v => v.Analysis.Physical.Playability <= filters.MaxDifficulty);
        
        if (filters.FretRange.HasValue)
            result = result.Where(v => IsInFretRange(v, filters.FretRange.Value));
        
        if (filters.CAGEDShape.HasValue)
            result = result.Where(v => MatchesCAGEDShape(v, filters.CAGEDShape.Value));
        
        if (filters.NoOpenStrings)
            result = result.Where(v => !v.Analysis.Physical.HasOpenStrings);
        
        if (filters.NoMutedStrings)
            result = result.Where(v => !v.Analysis.Physical.HasMutedStrings);
        
        return result;
    }
}
```

**Tasks**:
- [ ] Create `VoicingWithAnalysis` record
- [ ] Implement `GetVoicingsForChordAsync`
- [ ] Implement filter methods
- [ ] Implement CAGED shape detection
- [ ] Add unit tests

### Week 2: API Endpoints

#### 1.3 ContextualChordsController
**File**: `Apps/ga-server/GaApi/Controllers/ContextualChordsController.cs`

```csharp
[ApiController]
[Route("api/contextual-chords")]
public class ContextualChordsController : ControllerBase
{
    private readonly IContextualChordService _chordService;
    private readonly IVoicingFilterService _voicingService;
    
    [HttpGet("keys/{keyName}")]
    [ProducesResponseType(typeof(IEnumerable<ChordInContext>), 200)]
    public async Task<IActionResult> GetChordsForKey(
        string keyName,
        [FromQuery] ChordFilters filters)
    {
        var key = Key.Parse(keyName);
        var chords = await _chordService.GetChordsForKeyAsync(key, filters);
        return Ok(chords);
    }
    
    [HttpGet("scales/{scaleName}")]
    [ProducesResponseType(typeof(IEnumerable<ChordInContext>), 200)]
    public async Task<IActionResult> GetChordsForScale(
        string scaleName,
        [FromQuery] ChordFilters filters)
    {
        var scale = Scale.Parse(scaleName);
        var chords = await _chordService.GetChordsForScaleAsync(scale, filters);
        return Ok(chords);
    }
    
    [HttpGet("modes/{modeName}")]
    [ProducesResponseType(typeof(IEnumerable<ChordInContext>), 200)]
    public async Task<IActionResult> GetChordsForMode(
        string modeName,
        [FromQuery] ChordFilters filters)
    {
        var mode = ScaleMode.Parse(modeName);
        var chords = await _chordService.GetChordsForModeAsync(mode, filters);
        return Ok(chords);
    }
    
    [HttpGet("voicings/{chordName}")]
    [ProducesResponseType(typeof(IEnumerable<VoicingWithAnalysis>), 200)]
    public async Task<IActionResult> GetVoicingsForChord(
        string chordName,
        [FromQuery] VoicingFilters filters)
    {
        var (template, root) = ParseChordName(chordName);
        var voicings = await _voicingService.GetVoicingsForChordAsync(template, root, filters);
        return Ok(voicings);
    }
}
```

**Tasks**:
- [ ] Create controller
- [ ] Implement all endpoints
- [ ] Add Swagger documentation
- [ ] Add rate limiting
- [ ] Add caching
- [ ] Integration tests

---

## Phase 2: Frontend Components (2 weeks)

### Week 3: Core Components

#### 2.1 Hierarchical Navigation
**File**: `Apps/ga-client/src/components/HierarchicalNavigation.tsx`

```typescript
export const HierarchicalNavigation: React.FC = () => {
  const [context, setContext] = useState<MusicalContext>({
    level: 'none'
  });
  
  return (
    <div className="hierarchical-navigation">
      <Breadcrumb>
        <BreadcrumbItem>
          <KeySelector 
            onSelect={(key) => setContext({ level: 'key', key })}
          />
        </BreadcrumbItem>
        
        {context.level !== 'none' && (
          <BreadcrumbItem>
            <ScaleSelector 
              keyContext={context.key}
              onSelect={(scale) => setContext({ ...context, level: 'scale', scale })}
            />
          </BreadcrumbItem>
        )}
        
        {context.level === 'scale' && (
          <BreadcrumbItem>
            <ModeSelector 
              scale={context.scale}
              onSelect={(mode) => setContext({ ...context, level: 'mode', mode })}
            />
          </BreadcrumbItem>
        )}
      </Breadcrumb>
      
      <ContextualChordDisplay context={context} />
    </div>
  );
};
```

**Tasks**:
- [ ] Create `KeySelector` component
- [ ] Create `ScaleSelector` component
- [ ] Create `ModeSelector` component
- [ ] Add breadcrumb navigation
- [ ] Add keyboard shortcuts
- [ ] Add responsive design

#### 2.2 Contextual Chord Display
**File**: `Apps/ga-client/src/components/ContextualChordDisplay.tsx`

```typescript
export const ContextualChordDisplay: React.FC<{ context: MusicalContext }> = ({ context }) => {
  const [filters, setFilters] = useState<ChordFilters>({
    extension: 'Seventh',
    maxDifficulty: 'Intermediate'
  });
  
  const { data: chords, isLoading } = useQuery(
    ['chords', context, filters],
    () => fetchChordsForContext(context, filters)
  );
  
  return (
    <div className="contextual-chord-display">
      <ChordFilters filters={filters} onChange={setFilters} />
      
      <ChordGrid>
        {chords?.map(chord => (
          <ChordCard 
            key={chord.id}
            chord={chord}
            onClick={() => showVoicings(chord)}
          />
        ))}
      </ChordGrid>
    </div>
  );
};
```

**Tasks**:
- [ ] Create `ChordFilters` component
- [ ] Create `ChordGrid` component
- [ ] Create `ChordCard` component
- [ ] Add loading states
- [ ] Add error handling
- [ ] Add animations

### Week 4: Voicing Components

#### 2.3 Smart Voicing Display
**File**: `Apps/ga-client/src/components/SmartVoicingDisplay.tsx`

```typescript
export const SmartVoicingDisplay: React.FC<{ chord: ChordInContext }> = ({ chord }) => {
  const [filters, setFilters] = useState<VoicingFilters>({
    maxDifficulty: 'Intermediate',
    fretRange: { min: 0, max: 12 }
  });
  
  const { data: voicings, isLoading } = useQuery(
    ['voicings', chord, filters],
    () => fetchVoicingsForChord(chord, filters)
  );
  
  const groupedVoicings = useMemo(() => 
    groupBy(voicings, v => v.analysis.physical.playability),
    [voicings]
  );
  
  return (
    <div className="smart-voicing-display">
      <VoicingFilters filters={filters} onChange={setFilters} />
      
      <Tabs>
        <Tab label="Beginner">
          <VoicingList voicings={groupedVoicings['Beginner']} />
        </Tab>
        <Tab label="Intermediate">
          <VoicingList voicings={groupedVoicings['Intermediate']} />
        </Tab>
        <Tab label="Advanced">
          <VoicingList voicings={groupedVoicings['Advanced']} />
        </Tab>
        <Tab label="Expert">
          <VoicingList voicings={groupedVoicings['Expert']} />
        </Tab>
      </Tabs>
    </div>
  );
};
```

**Tasks**:
- [ ] Create `VoicingFilters` component
- [ ] Create `VoicingList` component
- [ ] Create `VoicingCard` component with fretboard diagram
- [ ] Add audio playback
- [ ] Add favorite/bookmark functionality
- [ ] Add sharing functionality

---

## Phase 3: Testing & Refinement (1 week)

### Week 5: Testing

#### 3.1 Backend Tests
- [ ] Unit tests for `ContextualChordService`
- [ ] Unit tests for `VoicingFilterService`
- [ ] Integration tests for API endpoints
- [ ] Performance tests (response time < 200ms)
- [ ] Load tests (1000 concurrent users)

#### 3.2 Frontend Tests
- [ ] Component tests (React Testing Library)
- [ ] E2E tests (Playwright)
- [ ] Accessibility tests (axe-core)
- [ ] Performance tests (Lighthouse)
- [ ] Cross-browser tests

#### 3.3 User Testing
- [ ] Usability testing with 10 users
- [ ] A/B testing (old vs new system)
- [ ] Collect feedback
- [ ] Iterate based on feedback

---

## Success Metrics

### Quantitative
- **Chord Count Reduction**: From 427,254 to ~50-100 per context
- **Voicing Count Reduction**: From thousands to ~10-20 per difficulty level
- **Response Time**: < 200ms for chord queries
- **User Satisfaction**: > 4.5/5 stars

### Qualitative
- **Correct Naming**: Enharmonic spelling matches key context
- **Musical Relevance**: Chords are appropriate for the context
- **Playability**: Voicings are physically achievable
- **Educational Value**: Users learn chord-scale relationships

---

## Timeline

| Week | Phase | Deliverables |
|------|-------|--------------|
| 1 | Backend Core | ContextualChordService, VoicingFilterService |
| 2 | Backend API | ContextualChordsController, Swagger docs |
| 3 | Frontend Core | HierarchicalNavigation, ContextualChordDisplay |
| 4 | Frontend Voicings | SmartVoicingDisplay, VoicingFilters |
| 5 | Testing | All tests, user feedback |

**Total Duration**: 5 weeks

---

## Dependencies

### Backend
- ✅ ChordTemplateFactory (existing)
- ✅ HybridChordNamingService (existing)
- ✅ KeyAwareChordNamingService (existing)
- ✅ PsychoacousticVoicingAnalyzer (existing)
- ✅ FretboardChordsGenerator (existing)

### Frontend
- ✅ React 18
- ✅ TypeScript
- ✅ React Query (data fetching)
- ✅ Material-UI (components)
- ⏳ VexFlow (music notation) - needs integration
- ⏳ Tone.js (audio playback) - needs integration

---

## Next Steps

1. **Review** this roadmap with the team
2. **Approve** the architecture and timeline
3. **Start Phase 1** (Backend Services)
4. **Weekly check-ins** to track progress
5. **Iterate** based on feedback

---

## References

- [FRONTEND_CONTEXTUALIZATION_ANALYSIS.md](FRONTEND_CONTEXTUALIZATION_ANALYSIS.md)
- [Harmonious App](https://harmoniousapp.net/)
- [Legacy Delphi Code](Guitar Alchemist - legacy (Delphi)/Delphi/Common/uChordVoicings.pas)

