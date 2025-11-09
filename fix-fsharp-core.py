#!/usr/bin/env python3
import re
from pathlib import Path

def fix_fsharp_core_versions(root_dir="."):
    """Fix FSharp.Core version inconsistencies"""
    count = 0
    
    for ext in ["*.csproj", "*.fsproj"]:
        for proj_file in Path(root_dir).rglob(ext):
            try:
                content = proj_file.read_text(encoding='utf-8')
                original = content
                
                # Replace 10.0.100-rc.2.25502.107 with 10.0.100-rc2.25502.107 (remove dot after rc)
                content = re.sub(r'10\.0\.100-rc\.2\.25502\.107', '10.0.100-rc2.25502.107', content)
                
                if content != original:
                    proj_file.write_text(content, encoding='utf-8')
                    print(f"✓ Fixed: {proj_file}")
                    count += 1
            except Exception as e:
                print(f"✗ Error processing {proj_file}: {e}")
    
    print(f"\nTotal files fixed: {count}")

if __name__ == "__main__":
    fix_fsharp_core_versions()

