#!/usr/bin/env python3
"""
Script to add simple SVG icons to all instruments in Instruments.yaml
"""

import re
from pathlib import Path

# Define SVG icons for different instrument categories
ICONS = {
    # String instruments - fretted
    "guitar": '<svg viewBox="0 0 24 24" xmlns="http://www.w3.org/2000/svg"><path d="M12 2L8 6v12l4 4 4-4V6z" fill="currentColor"/><line x1="8" y1="10" x2="16" y2="10" stroke="currentColor" stroke-width="0.5"/><line x1="8" y1="14" x2="16" y2="14" stroke="currentColor" stroke-width="0.5"/></svg>',
    
    # Bass instruments
    "bass": '<svg viewBox="0 0 24 24" xmlns="http://www.w3.org/2000/svg"><path d="M11 2L7 6v12l4 4 4-4V6z" fill="currentColor"/><line x1="7" y1="11" x2="15" y2="11" stroke="currentColor" stroke-width="0.8"/><line x1="7" y1="15" x2="15" y2="15" stroke="currentColor" stroke-width="0.8"/></svg>',
    
    # Mandolin family
    "mandolin": '<svg viewBox="0 0 24 24" xmlns="http://www.w3.org/2000/svg"><ellipse cx="12" cy="16" rx="5" ry="6" fill="currentColor"/><rect x="10.5" y="4" width="3" height="12" fill="currentColor"/><line x1="9" y1="8" x2="15" y2="8" stroke="currentColor" stroke-width="0.5"/></svg>',
    
    # Banjo
    "banjo": '<svg viewBox="0 0 24 24" xmlns="http://www.w3.org/2000/svg"><circle cx="12" cy="16" r="6" fill="none" stroke="currentColor" stroke-width="1.5"/><rect x="10.5" y="2" width="3" height="14" fill="currentColor"/><line x1="8" y1="6" x2="16" y2="6" stroke="currentColor" stroke-width="0.5"/></svg>',
    
    # Ukulele
    "ukulele": '<svg viewBox="0 0 24 24" xmlns="http://www.w3.org/2000/svg"><path d="M13 4L10 7v10l3 3 3-3V7z" fill="currentColor"/><line x1="10" y1="11" x2="16" y2="11" stroke="currentColor" stroke-width="0.5"/><line x1="10" y1="14" x2="16" y2="14" stroke="currentColor" stroke-width="0.5"/></svg>',
    
    # Lute family
    "lute": '<svg viewBox="0 0 24 24" xmlns="http://www.w3.org/2000/svg"><ellipse cx="12" cy="15" rx="6" ry="7" fill="currentColor"/><rect x="10.5" y="2" width="3" height="13" fill="currentColor"/><line x1="8" y1="7" x2="16" y2="7" stroke="currentColor" stroke-width="0.5"/></svg>',
    
    # Balalaika (triangular)
    "balalaika": '<svg viewBox="0 0 24 24" xmlns="http://www.w3.org/2000/svg"><path d="M12 22L6 14L18 14z" fill="currentColor"/><rect x="10.5" y="2" width="3" height="12" fill="currentColor"/><line x1="8" y1="8" x2="16" y2="8" stroke="currentColor" stroke-width="0.5"/></svg>',
    
    # Harp
    "harp": '<svg viewBox="0 0 24 24" xmlns="http://www.w3.org/2000/svg"><path d="M6 22Q6 12 12 2Q18 12 18 22" fill="none" stroke="currentColor" stroke-width="1.5"/><line x1="8" y1="10" x2="16" y2="10" stroke="currentColor" stroke-width="0.5"/><line x1="7" y1="14" x2="17" y2="14" stroke="currentColor" stroke-width="0.5"/></svg>',
    
    # Dulcimer
    "dulcimer": '<svg viewBox="0 0 24 24" xmlns="http://www.w3.org/2000/svg"><rect x="6" y="8" width="12" height="10" rx="1" fill="currentColor"/><line x1="6" y1="11" x2="18" y2="11" stroke="white" stroke-width="0.5"/><line x1="6" y1="14" x2="18" y2="14" stroke="white" stroke-width="0.5"/></svg>',
    
    # Sitar
    "sitar": '<svg viewBox="0 0 24 24" xmlns="http://www.w3.org/2000/svg"><ellipse cx="12" cy="17" rx="4" ry="5" fill="currentColor"/><rect x="10.5" y="2" width="3" height="15" fill="currentColor"/><circle cx="12" cy="6" r="2" fill="none" stroke="currentColor" stroke-width="1"/></svg>',
    
    # Bouzouki
    "bouzouki": '<svg viewBox="0 0 24 24" xmlns="http://www.w3.org/2000/svg"><ellipse cx="12" cy="16" rx="5" ry="6" fill="currentColor"/><rect x="10.5" y="3" width="3" height="13" fill="currentColor"/><line x1="9" y1="9" x2="15" y2="9" stroke="currentColor" stroke-width="0.5"/></svg>',
    
    # Charango
    "charango": '<svg viewBox="0 0 24 24" xmlns="http://www.w3.org/2000/svg"><path d="M13 5L10 8v9l3 3 3-3V8z" fill="currentColor"/><line x1="10" y1="12" x2="16" y2="12" stroke="currentColor" stroke-width="0.5"/><line x1="10" y1="15" x2="16" y2="15" stroke="currentColor" stroke-width="0.5"/></svg>',
    
    # Cittern
    "cittern": '<svg viewBox="0 0 24 24" xmlns="http://www.w3.org/2000/svg"><ellipse cx="12" cy="15" rx="5.5" ry="6.5" fill="currentColor"/><rect x="10.5" y="2" width="3" height="13" fill="currentColor"/><line x1="8.5" y1="8" x2="15.5" y2="8" stroke="currentColor" stroke-width="0.5"/></svg>',
    
    # Vihuela
    "vihuela": '<svg viewBox="0 0 24 24" xmlns="http://www.w3.org/2000/svg"><path d="M12 3L9 6v11l3 3 3-3V6z" fill="currentColor"/><line x1="9" y1="10" x2="15" y2="10" stroke="currentColor" stroke-width="0.5"/><line x1="9" y1="14" x2="15" y2="14" stroke="currentColor" stroke-width="0.5"/></svg>',
    
    # Generic string instrument (default)
    "default": '<svg viewBox="0 0 24 24" xmlns="http://www.w3.org/2000/svg"><path d="M12 3L9 6v12l3 3 3-3V6z" fill="currentColor"/><line x1="9" y1="11" x2="15" y2="11" stroke="currentColor" stroke-width="0.5"/><line x1="9" y1="15" x2="15" y2="15" stroke="currentColor" stroke-width="0.5"/></svg>',
}

# Mapping of instrument names to icon types
INSTRUMENT_ICON_MAP = {
    # Guitar family
    "guitar": "guitar",
    "bajo": "bass",
    "bass": "bass",
    
    # Mandolin family
    "mandolin": "mandolin",
    "mandola": "mandolin",
    "mandocello": "mandolin",
    "bandola": "mandolin",
    "bandolim": "mandolin",
    
    # Banjo family
    "banjo": "banjo",
    
    # Ukulele family
    "ukulele": "ukulele",
    "cavaquinho": "ukulele",
    
    # Lute family
    "lute": "lute",
    "oud": "lute",
    "theorbo": "lute",
    
    # Balalaika
    "balalaika": "balalaika",
    
    # Harp family
    "harp": "harp",
    
    # Dulcimer family
    "dulcimer": "dulcimer",
    "psaltery": "dulcimer",
    
    # Sitar family
    "sitar": "sitar",
    "sarod": "sitar",
    "veena": "sitar",
    "tanpura": "sitar",
    
    # Bouzouki family
    "bouzouki": "bouzouki",
    "baglama": "bouzouki",
    "saz": "bouzouki",
    "tzouras": "bouzouki",
    
    # Charango family
    "charango": "charango",
    "charangon": "charango",
    
    # Cittern family
    "cittern": "cittern",
    "cistre": "cittern",
    
    # Vihuela family
    "vihuela": "vihuela",
    "bandurria": "vihuela",
    
    # Other specific instruments
    "cuatro": "ukulele",
    "requinto": "guitar",
    "tiple": "guitar",
    "tres": "guitar",
    "timple": "ukulele",
    "rajao": "ukulele",
    "machete": "ukulele",
    "tahitian": "ukulele",
    "soprano": "ukulele",
    "concert": "ukulele",
    "tenor": "ukulele",
    "baritone": "ukulele",
}

def get_icon_for_instrument(instrument_name):
    """Get the appropriate SVG icon for an instrument"""
    name_lower = instrument_name.lower()
    
    # Check for exact matches first
    for key, icon_type in INSTRUMENT_ICON_MAP.items():
        if key in name_lower:
            return ICONS[icon_type]
    
    # Default icon
    return ICONS["default"]

def add_icons_to_yaml(input_file, output_file):
    """Add Icon field to all instruments in the YAML file"""
    with open(input_file, 'r', encoding='utf-8-sig') as f:
        content = f.read()
    
    lines = content.split('\n')
    new_lines = []
    current_instrument = None
    
    for i, line in enumerate(lines):
        new_lines.append(line)
        
        # Check if this is a top-level instrument definition (no leading spaces, ends with :)
        if line and not line.startswith(' ') and line.endswith(':'):
            instrument_name = line.rstrip(':')
            current_instrument = instrument_name
            
        # Check if this is the DisplayName line for a top-level instrument
        if line.strip().startswith('DisplayName:') and current_instrument:
            # Check if the next line is not an Icon line
            next_line_idx = i + 1
            if next_line_idx < len(lines):
                next_line = lines[next_line_idx].strip()
                if not next_line.startswith('Icon:'):
                    # Add the Icon field
                    icon_svg = get_icon_for_instrument(current_instrument)
                    # Escape the SVG for YAML (use | for multiline literal)
                    new_lines.append(f'  Icon: "{icon_svg}"')
            current_instrument = None  # Reset after adding icon
    
    # Write the modified content
    with open(output_file, 'w', encoding='utf-8') as f:
        f.write('\n'.join(new_lines))
    
    print(f"✅ Added icons to instruments in {output_file}")

if __name__ == "__main__":
    script_dir = Path(__file__).parent
    repo_root = script_dir.parent
    input_file = repo_root / "Common" / "GA.Business.Config" / "Instruments.yaml"
    output_file = input_file  # Overwrite the original file
    
    print(f"📝 Processing {input_file}...")
    add_icons_to_yaml(input_file, output_file)
    print("✨ Done!")

