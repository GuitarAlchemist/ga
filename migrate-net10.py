#!/usr/bin/env python3
import os
import re
from pathlib import Path

def migrate_to_net10(root_dir="."):
    """Migrate all .csproj and .fsproj files to net10.0 and update FSharp.Core versions"""
    count = 0

    for ext in ["*.csproj", "*.fsproj"]:
        for proj_file in Path(root_dir).rglob(ext):
            try:
                content = proj_file.read_text(encoding='utf-8')
                original = content

                # Replace net9.0 with net10.0
                content = re.sub(r'net9\.0', 'net10.0', content)
                # Replace net8.0 with net10.0
                content = re.sub(r'net8\.0', 'net10.0', content)
                # Replace net6.0 with net10.0
                content = re.sub(r'net6\.0', 'net10.0', content)

                # Update FSharp.Core versions to RC2
                content = re.sub(r'FSharp\.Core.*Version="10\.0\.100-preview7\.\d+"',
                               'FSharp.Core" Version="10.0.100-rc.2.25502.107"', content)

                if content != original:
                    proj_file.write_text(content, encoding='utf-8')
                    print(f"✓ Updated: {proj_file}")
                    count += 1
            except Exception as e:
                print(f"✗ Error processing {proj_file}: {e}")

    print(f"\nTotal files updated: {count}")

if __name__ == "__main__":
    migrate_to_net10()

