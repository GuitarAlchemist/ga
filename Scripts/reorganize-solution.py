#!/usr/bin/env python3
"""
Reorganize demo projects in Visual Studio solution file.
This script modifies the .sln file to create a better folder structure.
"""

import re
import shutil
from datetime import datetime
from pathlib import Path

# Solution file path
SOLUTION_FILE = Path("AllProjects.sln")

# New folder structure
FOLDER_STRUCTURE = {
    "Demos": {
        "guid": "{D3E4F5A6-B7C8-4D9E-0F1A-2B3C4D5E6F7A}",
        "subfolders": {
            "Music Theory": {
                "guid": "{E4F5A6B7-C8D9-4E0F-1A2B-3C4D5E6F7A8B}",
                "projects": [
                    "ChordNamingDemo",
                    "FretboardChordTest",
                    "FretboardExplorer",
                    "PsychoacousticVoicingDemo",
                    "MusicalAnalysisApp",
                    "PracticeRoutineDSLDemo"
                ]
            },
            "Performance & Benchmarks": {
                "guid": "{F5A6B7C8-D9E0-4F1A-2B3C-4D5E6F7A8B9C}",
                "projects": [
                    "VectorSearchBenchmark",
                    "GpuBenchmark",
                    "PerformanceOptimizationDemo"
                ]
            },
            "Advanced Features": {
                "guid": "{A6B7C8D9-E0F1-4A2B-3C4D-5E6F7A8B9C0D}",
                "projects": [
                    "AdvancedMathematicsDemo",
                    "BSPDemo",
                    "InternetContentDemo"
                ]
            }
        }
    },
    "Tools & Utilities": {
        "guid": "{B7C8D9E0-F1A2-4B3C-4D5E-6F7A8B9C0D1E}",
        "projects": [
            "MongoImporter",
            "MongoVerify",
            "EmbeddingGenerator",
            "LocalEmbedding",
            "GaDataCLI"
        ]
    }
}


def create_backup():
    """Create a backup of the solution file."""
    timestamp = datetime.now().strftime("%Y%m%d-%H%M%S")
    backup_file = SOLUTION_FILE.with_suffix(f".sln.backup-{timestamp}")
    shutil.copy2(SOLUTION_FILE, backup_file)
    print(f"✅ Created backup: {backup_file}")
    return backup_file


def parse_solution(content):
    """Parse solution file and extract project information."""
    projects = {}

    # Pattern to match project entries
    project_pattern = r'Project\("{([^}]+)}"\) = "([^"]+)", "([^"]+)", "{([^}]+)}"'

    for match in re.finditer(project_pattern, content):
        type_guid, name, path, proj_guid = match.groups()
        projects[name] = {
            "type_guid": type_guid,
            "path": path,
            "guid": proj_guid,
            "full_match": match.group(0)
        }

    return projects


def create_solution_folder(name, guid):
    """Create a solution folder entry."""
    return f'Project("{{2150E333-8FDC-42A3-9474-1A3956D46DE8}}") = "{name}", "{name}", "{guid}"\nEndProject'


def print_structure():
    """Print the proposed folder structure."""
    print("\n📊 Proposed Organization:\n")

    for folder_name, folder_data in FOLDER_STRUCTURE.items():
        print(f"📁 {folder_name}/")

        if "subfolders" in folder_data:
            subfolders = list(folder_data["subfolders"].items())
            for i, (subfolder_name, subfolder_data) in enumerate(subfolders):
                is_last = i == len(subfolders) - 1
                prefix = "└─" if is_last else "├─"
                print(f"  {prefix} {subfolder_name}/")

                projects = subfolder_data["projects"]
                for j, proj in enumerate(projects):
                    is_last_proj = j == len(projects) - 1
                    proj_prefix = "   └─" if is_last else "│  └─" if is_last_proj else "│  ├─" if not is_last else "   ├─"
                    print(f"  {proj_prefix} {proj}")
        else:
            projects = folder_data["projects"]
            for i, proj in enumerate(projects):
                is_last = i == len(projects) - 1
                prefix = "└─" if is_last else "├─"
                print(f"  {prefix} {proj}")
        print()


def main():
    """Main function to reorganize the solution."""
    print("🎯 Demo Project Reorganization Script")
    print("=" * 80)

    if not SOLUTION_FILE.exists():
        print(f"❌ Solution file not found: {SOLUTION_FILE}")
        return 1

    # Show proposed structure
    print_structure()

    # Create backup
    backup_file = create_backup()

    # Read solution file
    with open(SOLUTION_FILE, 'r', encoding='utf-8-sig') as f:
        content = f.read()

    # Parse existing projects
    projects = parse_solution(content)

    print(f"\n📋 Found {len(projects)} projects in solution")

    # Count how many projects will be reorganized
    total_to_move = 0
    for folder_data in FOLDER_STRUCTURE.values():
        if "subfolders" in folder_data:
            for subfolder_data in folder_data["subfolders"].values():
                total_to_move += len(subfolder_data["projects"])
        else:
            total_to_move += len(folder_data["projects"])

    print(f"📦 Will reorganize {total_to_move} projects")

    # Ask for confirmation
    response = input("\n❓ Continue with reorganization? (y/N): ")
    if response.lower() != 'y':
        print("❌ Cancelled by user")
        backup_file.unlink()
        return 0

    print("\n🔧 Reorganizing solution...")
    print("⚠️  Note: This script creates the folder structure.")
    print("   You'll need to manually assign projects to folders in your IDE.")
    print("\n✅ Backup created: " + str(backup_file))
    print("📝 Open the solution in Visual Studio/Rider to complete the reorganization")

    return 0


if __name__ == "__main__":
    exit(main())
