#!/usr/bin/env python3
import re
from pathlib import Path

def fix_vulnerable_packages(root_dir="."):
    """Fix vulnerable package versions"""
    count = 0
    
    for ext in ["*.csproj", "*.fsproj"]:
        for proj_file in Path(root_dir).rglob(ext):
            try:
                content = proj_file.read_text(encoding='utf-8')
                original = content
                
                # Update Newtonsoft.Json 9.0.1 to 13.0.3
                content = re.sub(
                    r'<PackageReference Include="Newtonsoft\.Json" Version="9\.0\.1"',
                    '<PackageReference Include="Newtonsoft.Json" Version="13.0.3"',
                    content
                )
                
                # Update OpenTelemetry.Api 1.10.0 to 1.11.0
                content = re.sub(
                    r'<PackageReference Include="OpenTelemetry\.Api" Version="1\.10\.0"',
                    '<PackageReference Include="OpenTelemetry.Api" Version="1.11.0"',
                    content
                )
                
                if content != original:
                    proj_file.write_text(content, encoding='utf-8')
                    print(f"✓ Fixed: {proj_file}")
                    count += 1
            except Exception as e:
                print(f"✗ Error processing {proj_file}: {e}")
    
    print(f"\nTotal files fixed: {count}")

if __name__ == "__main__":
    fix_vulnerable_packages()

