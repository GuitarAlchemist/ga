# Data Layer Unification Strategy

**Purpose**: Create clear boundaries between domain models, data models, and DTOs with mapping strategies to eliminate duplication and confusion.

---

## Current State Analysis

### 1. Domain Models (GA.Business.Core)

**Location**: `Common/GA.Business.Core/`

**Purpose**: Pure business logic, music theory, immutable value objects

**Examples**:
- `Note` - Chromatic, Sharp, Flat, Accidented (discriminated union)
- `Chord` - Chord structure with notes and intervals
- `Scale` - Scale structure with notes and intervals
- `PitchClass` - Atonal pitch class (0-11)
- `Interval` - Musical interval
- `Fretboard` - Fretboard structure
- `FretboardShape` - Playable fingering pattern

**Characteristics**:
- ✅ Immutable (records)
- ✅ Rich behavior (methods, operators)
- ✅ Music theory logic
- ✅ No persistence concerns
- ✅ No serialization attributes

### 2. Data Models (MongoDB)

**Location**: `Apps/ga-server/GaApi/Models/`, `Common/GA.Data.MongoDB/`

**Purpose**: Persistence, MongoDB-specific attributes

**Examples**:
- `MusicRoomDocument` - MongoDB document with `[BsonId]`
- `RoomGenerationJob` - MongoDB document
- `Chord` (in MongoDbService) - Persisted chord data

**Characteristics**:
- ✅ Mutable (classes)
- ✅ MongoDB attributes (`[BsonId]`, `[BsonRepresentation]`)
- ✅ Persistence-focused
- ❌ Mixed with domain logic (problem!)

### 3. DTOs / API Models

**Location**: `Apps/ga-server/GaApi/GraphQL/Types/`

**Purpose**: API contracts, serialization, client communication

**Examples**:
- `FretboardChordAnalysisType` - GraphQL type
- `BiomechanicalDataType` - GraphQL type
- `PhysicalPlayabilityDataType` - GraphQL type
- `FingerPositionType` - GraphQL type

**Characteristics**:
- ✅ Mutable (classes)
- ✅ Serialization-friendly
- ✅ API-focused
- ✅ Mapping from domain models

### 4. EF Core Models

**Location**: `Common/GA.Business.Core/Data/`

**Purpose**: SQL database caching (EF Core)

**Examples**:
- `CachedIconicChord`
- `CachedChordProgression`
- `CachedGuitarTechnique`
- `CachedSpecializedTuning`
- `UserProfile`
- `UserPreference`
- `LearningPath`

**Characteristics**:
- ✅ Mutable (classes)
- ✅ EF Core attributes
- ✅ SQL-focused
- ❌ Located in GA.Business.Core (should be in separate project!)

---

## Problems Identified

### Problem 1: Mixed Concerns

**Issue**: Domain models, data models, and DTOs are mixed together

**Examples**:
- `Chord` used as both domain model and MongoDB document
- EF Core models in `GA.Business.Core` (should be in data layer)
- GraphQL types manually mapping from domain models

**Impact**:
- Confusion about which model to use
- Tight coupling between layers
- Difficult to change persistence strategy
- Hard to test domain logic

### Problem 2: Duplication

**Issue**: Same concepts represented multiple times

**Examples**:
- `Chord` (domain) vs `Chord` (MongoDB) vs `ChordType` (GraphQL)
- `Note` (domain) vs string representation in DTOs
- `FretboardShape` (domain) vs serialized representation

**Impact**:
- Maintenance burden
- Inconsistencies
- Mapping code everywhere

### Problem 3: No Clear Mapping Strategy

**Issue**: Manual mapping scattered throughout codebase

**Examples**:
- `FretboardChordAnalysisType.FromAnalysis()` - Manual mapping
- `BiomechanicalDataType.FromAnalysis()` - Manual mapping
- No AutoMapper or similar tool

**Impact**:
- Repetitive code
- Error-prone
- Hard to maintain

### Problem 4: Persistence Leakage

**Issue**: Persistence concerns leak into domain models

**Examples**:
- MongoDB attributes on domain models
- EF Core models in business logic project
- Database-specific logic in domain layer

**Impact**:
- Violates Clean Architecture
- Hard to test
- Tight coupling to database

---

## Proposed Architecture

### Layer 1: Domain Models (Pure)

**Location**: `Common/GA.Business.Core/`

**Purpose**: Pure business logic, no persistence

**Rules**:
- ✅ Immutable (records preferred)
- ✅ Rich behavior
- ✅ No serialization attributes
- ✅ No database attributes
- ✅ Framework-agnostic

**Examples**:
```csharp
// Common/GA.Business.Core/Notes/Note.cs
public abstract record Note : IPitchClass
{
    public abstract PitchClass PitchClass { get; }
    public abstract Interval.Simple GetInterval(Note other);
    // ... pure domain logic
}

// Common/GA.Business.Core/Chords/Chord.cs
public record Chord
{
    public required string Name { get; init; }
    public required ImmutableList<Note> Notes { get; init; }
    public required PitchClassSet PitchClassSet { get; init; }
    // ... pure domain logic
}
```

### Layer 2: Data Models (Persistence)

**Location**: `Common/GA.Data.MongoDB/Models/`, `Common/GA.Data.EntityFramework/Models/`

**Purpose**: Persistence-specific models

**Rules**:
- ✅ Mutable (classes)
- ✅ Database attributes
- ✅ Persistence-focused
- ❌ No business logic

**Examples**:
```csharp
// Common/GA.Data.MongoDB/Models/ChordDocument.cs
public class ChordDocument
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? Id { get; set; }
    
    public string Name { get; set; } = "";
    public List<string> Notes { get; set; } = [];
    public List<int> PitchClasses { get; set; } = [];
    public string Quality { get; set; } = "";
    
    // MongoDB-specific fields
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

// Common/GA.Data.EntityFramework/Models/CachedChord.cs
public class CachedChord
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string NotesJson { get; set; } = "";
    public string PitchClassesJson { get; set; } = "";
    
    // EF Core navigation properties
    public List<CachedChordProgression> Progressions { get; set; } = [];
}
```

### Layer 3: DTOs / API Models

**Location**: `Apps/ga-server/GaApi/Models/`, `Apps/ga-server/GaApi/GraphQL/Types/`

**Purpose**: API contracts, serialization

**Rules**:
- ✅ Mutable (classes)
- ✅ Serialization-friendly
- ✅ API-focused
- ❌ No business logic
- ❌ No persistence logic

**Examples**:
```csharp
// Apps/ga-server/GaApi/Models/ChordDto.cs
public class ChordDto
{
    public string Name { get; set; } = "";
    public List<string> Notes { get; set; } = [];
    public List<int> PitchClasses { get; set; } = [];
    public string Quality { get; set; } = "";
}

// Apps/ga-server/GaApi/GraphQL/Types/ChordType.cs
public class ChordType
{
    public string Name { get; set; } = "";
    public List<string> Notes { get; set; } = [];
    public List<int> PitchClasses { get; set; } = [];
    public string Quality { get; set; } = "";
}
```

### Layer 4: Mapping (Adapters)

**Location**: `Common/GA.Business.Core.Mapping/`, `Apps/ga-server/GaApi/Mapping/`

**Purpose**: Convert between layers

**Rules**:
- ✅ One-way mappings (domain → DTO, domain → data)
- ✅ Explicit mapping methods
- ✅ AutoMapper profiles (optional)
- ❌ No business logic

**Examples**:
```csharp
// Common/GA.Business.Core.Mapping/ChordMapper.cs
public static class ChordMapper
{
    // Domain → DTO
    public static ChordDto ToDto(this Chord chord)
    {
        return new ChordDto
        {
            Name = chord.Name,
            Notes = chord.Notes.Select(n => n.ToString()).ToList(),
            PitchClasses = chord.PitchClassSet.Select(pc => pc.Value).ToList(),
            Quality = chord.Quality.ToString()
        };
    }
    
    // Domain → MongoDB
    public static ChordDocument ToDocument(this Chord chord)
    {
        return new ChordDocument
        {
            Name = chord.Name,
            Notes = chord.Notes.Select(n => n.ToString()).ToList(),
            PitchClasses = chord.PitchClassSet.Select(pc => pc.Value).ToList(),
            Quality = chord.Quality.ToString(),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }
    
    // MongoDB → Domain
    public static Chord ToDomain(this ChordDocument document)
    {
        var notes = document.Notes
            .Select(Note.Accidented.Parse)
            .ToImmutableList();
            
        return new Chord
        {
            Name = document.Name,
            Notes = notes,
            PitchClassSet = notes.ToPitchClassSet()
        };
    }
}
```

---

## Migration Plan

### Phase 1: Create New Projects (2-4 hours)

**Step 1.1**: Create `GA.Data.MongoDB` project (if not exists)
```bash
dotnet new classlib -n GA.Data.MongoDB -o Common/GA.Data.MongoDB -f net9.0
dotnet sln add Common/GA.Data.MongoDB/GA.Data.MongoDB.csproj
```

**Step 1.2**: Create `GA.Data.EntityFramework` project
```bash
dotnet new classlib -n GA.Data.EntityFramework -o Common/GA.Data.EntityFramework -f net9.0
dotnet sln add Common/GA.Data.EntityFramework/GA.Data.EntityFramework.csproj
```

**Step 1.3**: Create `GA.Business.Core.Mapping` project
```bash
dotnet new classlib -n GA.Business.Core.Mapping -o Common/GA.Business.Core.Mapping -f net9.0
dotnet sln add Common/GA.Business.Core.Mapping/GA.Business.Core.Mapping.csproj
```

### Phase 2: Move EF Core Models (2-3 hours)

**Step 2.1**: Move models from `GA.Business.Core/Data/` to `GA.Data.EntityFramework/Models/`
- `CachedIconicChord.cs`
- `CachedChordProgression.cs`
- `CachedGuitarTechnique.cs`
- `CachedSpecializedTuning.cs`
- `UserProfile.cs`
- `UserPreference.cs`
- `LearningPath.cs`

**Step 2.2**: Move `MusicalKnowledgeDbContext.cs` to `GA.Data.EntityFramework/`

**Step 2.3**: Update namespaces and references

### Phase 3: Create MongoDB Models (4-6 hours)

**Step 3.1**: Create MongoDB document models in `GA.Data.MongoDB/Models/`
- `ChordDocument.cs`
- `ScaleDocument.cs`
- `ProgressionDocument.cs`
- `MusicRoomDocument.cs` (move from GaApi)
- `RoomGenerationJobDocument.cs` (move from GaApi)

**Step 3.2**: Update `MongoDbService` to use new models

### Phase 4: Create Mapping Layer (6-8 hours)

**Step 4.1**: Create mapper classes in `GA.Business.Core.Mapping/`
- `ChordMapper.cs`
- `ScaleMapper.cs`
- `ProgressionMapper.cs`
- `NoteMapper.cs`
- `FretboardMapper.cs`

**Step 4.2**: Update GraphQL types to use mappers

**Step 4.3**: Update API controllers to use mappers

### Phase 5: Update References (2-3 hours)

**Step 5.1**: Update `GaApi` to reference new projects

**Step 5.2**: Update `GuitarAlchemistChatbot` to reference new projects

**Step 5.3**: Update tests to reference new projects

### Phase 6: Clean Up (1-2 hours)

**Step 6.1**: Remove old data models from `GA.Business.Core`

**Step 6.2**: Remove old mapping code

**Step 6.3**: Update documentation

---

## Total Estimated Effort: 17-26 hours (2-3 weeks)

---

## Benefits

### ✅ Clear Separation of Concerns
- Domain logic separate from persistence
- API contracts separate from domain
- Easy to understand and maintain

### ✅ Testability
- Domain models testable without database
- Mapping testable in isolation
- API contracts testable independently

### ✅ Flexibility
- Easy to change persistence strategy
- Easy to add new API endpoints
- Easy to support multiple clients

### ✅ Maintainability
- Single source of truth for each concern
- Explicit mapping reduces errors
- Clear boundaries reduce confusion

---

## Next Steps

1. Review and approve this strategy
2. Create new projects (Phase 1)
3. Move EF Core models (Phase 2)
4. Create MongoDB models (Phase 3)
5. Create mapping layer (Phase 4)
6. Update references (Phase 5)
7. Clean up (Phase 6)

