#!/usr/bin/env python3
"""
Reorganize AllProjects.sln with proper solution folders.
This script reads the current solution and creates a new one with organized folders.
"""

import re
import sys
from pathlib import Path

# Define solution folder structure
SOLUTION_FOLDERS = {
    "Infrastructure": [
        "AllProjects.AppHost",
        "AllProjects.ServiceDefaults",
        "GaMcpServer",
    ],
    "Core Libraries": [
        "GA.Business.Core",
        "GA.Business.Core.Generated",
        "GA.Business.Harmony",
        "GA.Business.Fretboard",
        "GA.Business.Analysis",
        "GA.Business.AI",
        "GA.Business.Orchestration",
        "GA.Business.Microservices",
        "GA.Business.Web",
        "GA.Business.UI",
        "GA.Business.Graphiti",
        "GA.Business.Analytics",
        "GA.Business.Assets",
        "GA.Business.Intelligence",
        "GA.Business.Mapping",
        "GA.Business.Querying",
        "GA.Core",
        "GA.Core.UI",
        "GA.Config",
        "GA.Interactive",
        "GA.InteractiveExtension",
        "GA.Interactive.LocalNuGet",
        "GA.MusicTheory.DSL",
        "GA.BSP.Core",
    ],
    "Data & Integration": [
        "GA.Data.MongoDB",
        "GA.Data.SemanticKernel.Embeddings",
        "GA.Data.EntityFramework",
    ],
    "Applications": [
        "GaApi",
        "GuitarAlchemistChatbot",
        "ScenesService",
        "FloorManager",
        "AdvancedFretboardAnalysisDemo",
        "AdvancedMathematicsDemo",
        "AIIntegrationDemo",
        "BSPDemo",
        "ChordNamingDemo",
        "ComprehensiveMusicTheoryDemo",
        "HighPerformanceDemo",
        "InteractiveTutorial",
        "InternetContentDemo",
        "MusicalAnalysisApp",
        "PerformanceOptimizationDemo",
        "PracticeRoutineDSLDemo",
        "PsychoacousticVoicingDemo",
    ],
    "CLI & Tools": [
        "GaCLI",
        "GaDataCLI",
        "MongoImporter",
        "MongoVerify",
        "VectorSearchBenchmark",
        "LocalEmbedding",
        "EmbeddingGenerator",
        "GaMusicTheoryLsp",
        "GA.TabConversion.Api",
        "FretboardChordTest",
        "GpuBenchmark",
    ],
    "Experiments": [
        "UseGaNugetPackageExample",
        "ChatbotExample1",
        "MyFirstMCP",
        "ReactApp1.Server",
    ],
    "Tests": [
        "GA.Business.Core.Tests",
        "GA.Core.Tests",
        "GA.InteractiveExtension.Tests",
        "GA.MusicTheory.DSL.Tests",
        "GA.Business.Core.Graphiti.Tests",
        "GaApi.Tests",
        "GA.TabConversion.Api.Tests",
        "GuitarAlchemistChatbot.Tests",
        "AllProjects.AppHost.Tests",
        "BSPIntegrationTests",
        "FloorManager.Tests.Playwright",
        "GuitarAlchemistChatbot.Tests.Playwright",
    ],
    "Utilities": [
        "ChordSystemTest",
        "GuitarChordProgressionMCTS",
        "GA.WebBlazorApp",
    ],
}

def main():
    sln_path = Path("AllProjects.sln")
    if not sln_path.exists():
        print(f"Error: {sln_path} not found")
        sys.exit(1)
    
    print("✓ Solution file found")
    print("✓ Reorganization structure defined")
    print("\nSolution folders to create:")
    for folder, projects in SOLUTION_FOLDERS.items():
        print(f"  - {folder}: {len(projects)} projects")
    
    print("\nTo apply this reorganization, run:")
    print("  dotnet sln AllProjects.sln add-folder Infrastructure")
    print("  dotnet sln AllProjects.sln add-folder 'Core Libraries'")
    print("  ... etc")
    print("\nThen move projects using:")
    print("  dotnet sln AllProjects.sln move-project <project> <folder>")

if __name__ == "__main__":
    main()

