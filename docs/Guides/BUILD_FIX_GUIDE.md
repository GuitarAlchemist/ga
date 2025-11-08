# Build Fix Guide - Quick Reference

**Last Updated:** 2025-01-08

This guide provides step-by-step instructions for fixing the current build errors.

---

## 🚀 Quick Start: Fix in Order

Fix these in order to minimize cascading failures:

1. **GA.Data.SemanticKernel.Embeddings** (easiest, 1-2 hours)
2. **GA.Data.MongoDB** (medium, 2-4 hours)
3. **GA.BSP.Core** (hardest, 8-16 hours)

---

## 1. Fix GA.Data.SemanticKernel.Embeddings

### Issue

- Duplicate `OllamaEmbeddingResponse` class
- Missing namespace `GA.Business`
- Missing type `OptimizedSemanticFretboardService`

### Steps

**Step 1: Find and remove duplicate**

```bash
# Search for OllamaEmbeddingResponse
rg "class OllamaEmbeddingResponse" GA.Data.SemanticKernel.Embeddings/
```

Remove the duplicate definition (keep only one).

**Step 2: Add missing project reference**

```xml
<!-- Add to GA.Data.SemanticKernel.Embeddings.csproj -->
<ItemGroup>
  <ProjectReference Include="..\Common\GA.Business.Fretboard\GA.Business.Fretboard.csproj" />
</ItemGroup>
```

**Step 3: Update namespace reference**

```csharp
// In BatchOllamaEmbeddingService.cs
// Change:
using GA.Business;

// To:
using GA.Business.Fretboard;
// OR create a stub if the type doesn't exist yet
```

**Step 4: Verify**

```bash
dotnet build GA.Data.SemanticKernel.Embeddings/GA.Data.SemanticKernel.Embeddings.csproj
```

---

## 2. Fix GA.Data.MongoDB

### Issue

- References old `GA.Business.Core.Data` namespace
- Missing `InstrumentsRepository` reference
- Missing Assets3D types

### Steps

**Step 1: Add project reference**

```xml
<!-- Add to GA.Data.MongoDB/GA.Data.MongoDB.csproj -->
<ItemGroup>
  <ProjectReference Include="..\Common\GA.Data.EntityFramework\GA.Data.EntityFramework.csproj" />
</ItemGroup>
```

**Step 2: Update namespace references**

Find all files with errors:

```bash
rg "using GA.Business.Core.Data" GA.Data.MongoDB/
rg "using GA.Business.Core.Assets3D" GA.Data.MongoDB/
```

Update them:

```csharp
// Change:
using GA.Business.Core.Data;

// To:
using GA.Data.EntityFramework.Data.Instruments;

// Change:
using GA.Business.Core.Assets3D;

// To:
using GA.Business.Core.Assets3D; // If it exists
// OR comment out if Assets3D is not needed yet
```

**Step 3: Handle missing Assets3D types**

Option A - If Assets3D exists somewhere:

```bash
# Find where Assets3D types are defined
rg "class AssetCategory" --type cs
rg "class AssetMetadata" --type cs
rg "class BoundingBox" --type cs
```

Option B - Create stubs temporarily:

```csharp
// In GA.Data.MongoDB/Stubs/Assets3DStubs.cs
namespace GA.Business.Core.Assets3D;

public enum AssetCategory { Unknown }
public class AssetMetadata { }
public class BoundingBox { }
public record Vector3(float X, float Y, float Z);
```

**Step 4: Verify**

```bash
dotnet restore GA.Data.MongoDB/GA.Data.MongoDB.csproj
dotnet build GA.Data.MongoDB/GA.Data.MongoDB.csproj
```

---

## 3. Fix GA.BSP.Core

### Issue

- Missing logging using statements
- Missing fretboard shape namespaces
- Missing many analysis types

### Steps

**Step 1: Add logging using statements**

Files that need `using Microsoft.Extensions.Logging;`:

- `Common/GA.BSP.Core/BSP/IntelligentBSPGenerator.cs`
- `Common/GA.BSP.Core/BSP/IntelligentBSPGenerator.Optimized.cs`
- `Common/GA.BSP.Core/Spatial/TonalBSPAnalyzer.cs`
- `Common/GA.BSP.Core/Spatial/TonalBSPService.cs`

```csharp
// Add to top of each file:
using Microsoft.Extensions.Logging;
```

**Step 2: Identify missing types**

Run this to see what types are missing:

```bash
dotnet build Common/GA.BSP.Core/GA.BSP.Core.csproj 2>&1 | grep "error CS0246" | sort -u
```

**Step 3: Locate or create missing types**

For each missing type, search the codebase:

```bash
# Example for ShapeGraph
rg "class ShapeGraph" --type cs

# Example for HarmonicAnalysisReport
rg "class HarmonicAnalysisReport" --type cs
```

**Step 4: Decision Matrix**

For each missing type, decide:

| Type                                                                   | Likely Location                                 | Action            |
|------------------------------------------------------------------------|-------------------------------------------------|-------------------|
| `ShapeGraph`, `ChordFamily`, `Attractor`, `LimitCycle`                 | `GA.Business.Fretboard`                         | Move or reference |
| `HarmonicAnalysisReport`, `HarmonicAnalysisEngine`, `HarmonicDynamics` | `GA.Business.Harmony` or `GA.Business.Analysis` | Move or reference |
| `ProgressionOptimizer`, `ProgressionAnalyzer`, `ProgressionInfo`       | `GA.Business.Orchestration`                     | Move or reference |
| `SpectralGraphAnalyzer`                                                | `GA.Business.Analysis`                          | Move or reference |
| `DynamicalSystemInfo`, `BSPLevelOptions`, `OptimizedProgression`       | `GA.Business.Orchestration`                     | Move or reference |

**Step 5: Add project references**

Based on where types are located:

```xml
<!-- Add to GA.BSP.Core/GA.BSP.Core.csproj -->
<ItemGroup>
  <ProjectReference Include="..\GA.Business.Fretboard\GA.Business.Fretboard.csproj" />
  <ProjectReference Include="..\GA.Business.Harmony\GA.Business.Harmony.csproj" />
  <ProjectReference Include="..\GA.Business.Analysis\GA.Business.Analysis.csproj" />
  <ProjectReference Include="..\GA.Business.Orchestration\GA.Business.Orchestration.csproj" />
</ItemGroup>
```

**Step 6: Update namespace references**

```csharp
// In IntelligentBSPGenerator.cs and IntelligentBSPGenerator.Optimized.cs

// Change:
using GA.Business.Core.Fretboard.Shapes.Applications;
using GA.Business.Core.Fretboard.Shapes.DynamicalSystems;

// To:
using GA.Business.Fretboard.Shapes.Applications;
using GA.Business.Fretboard.Shapes.DynamicalSystems;
// OR
using GA.Business.Fretboard;
using GA.Business.Harmony;
using GA.Business.Analysis;
using GA.Business.Orchestration;
```

**Step 7: Consider moving IntelligentBSPGenerator**

Per the modular architecture guidelines, orchestration code should be in `GA.Business.Orchestration`:

```bash
# Move files
git mv Common/GA.BSP.Core/BSP/IntelligentBSPGenerator.cs Common/GA.Business.Orchestration/BSP/
git mv Common/GA.BSP.Core/BSP/IntelligentBSPGenerator.Optimized.cs Common/GA.Business.Orchestration/BSP/
```

Update namespaces in moved files:

```csharp
// Change:
namespace GA.BSP.Core.BSP;

// To:
namespace GA.Business.Orchestration.BSP;
```

**Step 8: Verify**

```bash
dotnet restore Common/GA.BSP.Core/GA.BSP.Core.csproj
dotnet build Common/GA.BSP.Core/GA.BSP.Core.csproj
```

---

## 4. Fix GaCLI FSharp.Core Version Conflict

### Issue

Package downgrade from FSharp.Core 10.0.100-preview7 to 9.0.303

### Steps

**Step 1: Update FSharp.Core version**

```xml
<!-- In GaCLI/GaCLI.csproj -->
<ItemGroup>
  <PackageReference Include="FSharp.Core" Version="10.0.100-preview7.25451.107" />
</ItemGroup>
```

**Step 2: Verify**

```bash
dotnet restore GaCLI/GaCLI.csproj
dotnet build GaCLI/GaCLI.csproj
```

---

## 🔄 Verification Process

After each fix, run:

```bash
# Restore
dotnet restore <project>.csproj

# Build
dotnet build <project>.csproj -c Debug --no-restore

# Check for errors
dotnet build <project>.csproj -c Debug --no-restore 2>&1 | grep "error"
```

---

## 📊 Progress Tracking

Use this checklist:

- [ ] GA.Data.SemanticKernel.Embeddings builds
- [ ] GA.Data.MongoDB builds
- [ ] GA.BSP.Core builds
- [ ] GaCLI builds
- [ ] GA.Business.AI builds (cascading)
- [ ] GA.Business.Orchestration builds (cascading)
- [ ] GA.Business.Intelligence builds (cascading)
- [ ] GaApi builds (cascading)
- [ ] GuitarAlchemistChatbot builds (cascading)
- [ ] AllProjects.AppHost builds (cascading)
- [ ] All tests pass

---

## 🆘 Troubleshooting

### "Type not found" errors persist

1. Check if type exists: `rg "class TypeName" --type cs`
2. Check namespace: `rg "namespace.*TypeName" --type cs`
3. Check project references in .csproj
4. Run `dotnet restore` again

### "Namespace does not exist" errors

1. Verify project reference exists
2. Check namespace spelling
3. Ensure referenced project builds successfully
4. Clear obj/bin folders: `dotnet clean`

### Circular dependency errors

1. Review modular architecture rules in `AGENTS.md`
2. Ensure bottom-up dependency graph
3. Move types to lower layers if needed

---

## 📚 Additional Resources

- `docs/BUILD_STATUS_2025.md` - Detailed build status
- `docs/MODULAR_RESTRUCTURING_PLAN.md` - Architecture plan
- `AGENTS.md` - Repository guidelines

