# Common Folder Reorganization Plan - Detailed Execution Guide

## Overview

This plan consolidates 41 projects in `Common/` into a clean, layered architecture with no duplicates.

**Estimated Time**: 4-6 hours
**Risk Level**: Medium (requires careful file migration and namespace updates)
**Rollback**: Git branch before starting

## Phase 1: Consolidate Duplicate Projects (2-3 hours)

### 1.1 GA.Business.Harmony → GA.Business.Core.Harmony

**Current State**:
- `Common/GA.Business.Harmony/` - Empty project
- `Common/GA.Business.Core.Harmony/` - Empty project

**Action**:
1. Check if GA.Business.Harmony has any content
2. If empty: Delete GA.Business.Harmony
3. If has content: Move to GA.Business.Core.Harmony
4. Update solution file to remove GA.Business.Harmony
5. Update all project references

**Files to Update**:
- AllProjects.sln (remove GA.Business.Harmony project reference)
- AllProjects.slnx (remove GA.Business.Harmony project reference)
- Any .csproj files referencing GA.Business.Harmony

### 1.2 GA.Business.Fretboard → GA.Business.Core.Fretboard

**Current State**:
- `Common/GA.Business.Fretboard/` - Empty project
- `Common/GA.Business.Core.Fretboard/` - Empty project

**Action**: Same as 1.1

### 1.3 GA.Business.Analysis → GA.Business.Core.Analysis

**Current State**:
- `Common/GA.Business.Analysis/` - Empty project
- `Common/GA.Business.Core.Analysis/` - Empty project

**Action**: Same as 1.1

### 1.4 GA.Business.Orchestration → GA.Business.Core.Orchestration

**Current State**:
- `Common/GA.Business.Orchestration/` - Empty project
- `Common/GA.Business.Core.Orchestration/` - Empty project

**Action**: Same as 1.1

### 1.5 GA.Business.UI → GA.Business.Core.UI

**Current State**:
- `Common/GA.Business.UI/` - Has content (Blazor components)
- `Common/GA.Business.Core.UI/` - Has content (Blazor components)

**Action**:
1. Compare content of both projects
2. Merge unique files from GA.Business.UI into GA.Business.Core.UI
3. Update namespaces in merged files
4. Delete GA.Business.UI
5. Update solution file
6. Update all project references

### 1.6 GA.Business.Graphiti → GA.Business.Core.Graphiti

**Current State**:
- `Common/GA.Business.Graphiti/` - Has content (Models, Services)
- `Common/GA.Business.Core.Graphiti/` - Has content

**Action**: Same as 1.5

### 1.7 GA.Business.Orchestration (if different from 1.4)

Check if there's a separate GA.Business.Orchestration with content.

## Phase 2: Rename Misplaced Projects (1-2 hours)

### 2.1 GA.Business.AI → GA.Business.Core.AI

**Current State**:
- `Common/GA.Business.AI/` - Has content (AI models, services)
- `Common/GA.Business.Core.AI/` - Empty project

**Action**:
1. Move all content from GA.Business.AI to GA.Business.Core.AI
2. Update namespaces: `GA.Business.AI.*` → `GA.Business.Core.AI.*`
3. Delete GA.Business.AI folder
4. Update solution file
5. Update all project references (GaApi, Chatbot, etc.)

**Files to Update**:
- AllProjects.sln
- AllProjects.slnx
- All .csproj files referencing GA.Business.AI

### 2.2 GA.Business.Web → GA.Business.Core.Web

**Current State**:
- `Common/GA.Business.Web/` - Has content (Web services)
- `Common/GA.Business.Core.Web/` - Has content

**Action**: Same as 1.5 (merge and consolidate)

## Phase 3: Clarify Unclear Projects (1-2 hours)

Review and categorize:
- GA.Business.Analytics - Belongs in Layer 3 (Analysis)?
- GA.Business.Intelligence - Belongs in Layer 4 (AI)?
- GA.Business.Mapping - Purpose unclear, needs review
- GA.Business.Performance - Utility layer?
- GA.Business.Personalization - Separate domain?
- GA.Business.Validation - Utility layer?
- GA.Business.Configuration - Merge with GA.Business.Config?
- GA.Business.Core.Services - Purpose unclear?
- GA.Business.Assets - Asset management utility?
- GA.Business.Microservices - Microservice patterns?

**Decision Matrix**:
| Project | Decision | Reason |
|---------|----------|--------|
| GA.Business.Analytics | Keep/Move to Layer 3 | Depends on analysis needs |
| GA.Business.Intelligence | Move to Layer 4 | Semantic indexing = AI |
| GA.Business.Mapping | Review | Unclear purpose |
| GA.Business.Performance | Keep as Utility | Performance utilities |
| GA.Business.Personalization | Review | Unclear scope |
| GA.Business.Validation | Keep as Utility | Validation services |
| GA.Business.Configuration | Merge with Config | Consolidate config |
| GA.Business.Core.Services | Review | Unclear purpose |
| GA.Business.Assets | Keep as Utility | Asset management |
| GA.Business.Microservices | Keep as Utility | Microservice patterns |

## Phase 4: Update Solution File (30 minutes)

1. Remove all deleted project references
2. Update folder structure in solution
3. Verify all projects are properly organized
4. Test solution loads in Visual Studio

## Phase 5: Verify & Test (1 hour)

1. Build entire solution: `dotnet build AllProjects.sln`
2. Run tests: `dotnet test AllProjects.sln`
3. Verify no broken references
4. Commit changes

## Execution Checklist

- [ ] Phase 1.1: GA.Business.Harmony consolidation
- [ ] Phase 1.2: GA.Business.Fretboard consolidation
- [ ] Phase 1.3: GA.Business.Analysis consolidation
- [ ] Phase 1.4: GA.Business.Orchestration consolidation
- [ ] Phase 1.5: GA.Business.UI consolidation
- [ ] Phase 1.6: GA.Business.Graphiti consolidation
- [ ] Phase 2.1: GA.Business.AI rename
- [ ] Phase 2.2: GA.Business.Web consolidation
- [ ] Phase 3: Clarify unclear projects
- [ ] Phase 4: Update solution file
- [ ] Phase 5: Verify & test
- [ ] Commit: "refactor: reorganize Common folder into clean layered architecture"

## Rollback Plan

If issues occur:
```bash
git reset --hard HEAD~1
```

## Success Criteria

✅ All duplicate projects consolidated
✅ No GA.Business.X projects (all GA.Business.Core.X)
✅ Solution builds with 0 errors
✅ All tests pass
✅ No broken project references
✅ Clear layer separation maintained

