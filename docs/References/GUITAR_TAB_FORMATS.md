# Guitar Tab Notation Formats - Comprehensive Guide

## Overview

This document catalogs all major guitar tablature notation formats and their specifications for implementation in Guitar Alchemist.

---

## 1. ASCII Tab (Plain Text)

**File Extension:** `.txt`, `.tab`  
**Popularity:** ⭐⭐⭐⭐⭐ (Most common, universal)  
**Complexity:** Low  
**Status:** ✅ Implement First

### Format Description
Plain text representation using ASCII characters to show fret numbers on string lines.

### Example
```
E|---0---3---5---|
B|---1---0---5---|
G|---0---0---6---|
D|---2---0---7---|
A|---3---2---7---|
E|-------3---5---|
```

### Features
- String lines (E, B, G, D, A, E)
- Fret numbers (0-24)
- Timing markers (`|` for bars)
- Techniques:
  - `h` = hammer-on
  - `p` = pull-off
  - `b` = bend
  - `r` = release
  - `/` or `\` = slide
  - `~` = vibrato
  - `x` = muted string
  - `PM` = palm mute
  - `^` = harmonic

### Grammar Complexity
**Low** - Simple line-based parsing with regex patterns

---

## 2. VexTab

**File Extension:** `.vextab`  
**Popularity:** ⭐⭐⭐⭐ (Growing, web-based)  
**Complexity:** Medium  
**Status:** ✅ Already Implemented!

### Format Description
Text-based DSL that compiles to VexFlow (music notation rendering library).

### Example
```
tabstave notation=true tablature=true
notes :q 5/1 7/2 9/3 | :8 10/4 12/5 14/6
```

### Features
- Stave configuration
- Standard notation + tablature
- Duration markers (`:q`, `:8`, `:16`)
- Guitar techniques (bends, slides, hammer-ons)
- Articulations
- Text annotations

### Grammar Complexity
**Medium** - Already implemented in `VexTab.ebnf` and `VexTabParser.fs`

---

## 3. Guitar Pro (.gp, .gp3, .gp4, .gp5, .gpx, .gp7)

**File Extension:** `.gp`, `.gp3`, `.gp4`, `.gp5`, `.gpx`, `.gp7`  
**Popularity:** ⭐⭐⭐⭐⭐ (Industry standard)  
**Complexity:** Very High  
**Status:** 🔄 Implement (Binary format)

### Format Description
Proprietary binary format used by Guitar Pro software. Multiple versions with different structures.

### Features
- Multi-track support
- Full notation + tablature
- All guitar techniques
- Tempo/time signature changes
- Lyrics
- RSE (Realistic Sound Engine) data
- MIDI data

### Implementation Notes
- **Binary format** - requires binary parser
- **Multiple versions** - need version detection
- **Compression** - GPX uses ZIP compression
- **Existing libraries:**
  - `alphaTab` (JavaScript/C#) - can read GP files
  - `PyGuitarPro` (Python) - GP file parser
  - Consider using existing library vs implementing from scratch

### Grammar Complexity
**Very High** - Binary format with complex structure

---

## 4. PowerTab (.ptb)

**File Extension:** `.ptb`  
**Popularity:** ⭐⭐⭐ (Legacy, still used)  
**Complexity:** High  
**Status:** 🔄 Implement (Binary format)

### Format Description
Binary format used by PowerTab Editor (free, open-source).

### Features
- Multi-track support
- Standard notation + tablature
- Guitar techniques
- Tempo/time signature changes
- Chord diagrams

### Implementation Notes
- **Binary format**
- **Open specification** available
- **Existing libraries:**
  - `powertabeditor` (C++) - original implementation
  - Can reference for format details

### Grammar Complexity
**High** - Binary format

---

## 5. TuxGuitar (.tg)

**File Extension:** `.tg`  
**Popularity:** ⭐⭐⭐ (Open-source community)  
**Complexity:** Medium  
**Status:** 🔄 Implement (XML-based)

### Format Description
XML-based format used by TuxGuitar (free, open-source, cross-platform).

### Features
- Multi-track support
- Standard notation + tablature
- Guitar techniques
- Can import/export GP, MIDI, MusicXML

### Implementation Notes
- **XML format** - easier to parse than binary
- **Open specification**
- **Good intermediate format** for conversions

### Grammar Complexity
**Medium** - XML parsing with music-specific schema

---

## 6. MusicXML (.musicxml, .xml, .mxl)

**File Extension:** `.musicxml`, `.xml`, `.mxl`  
**Popularity:** ⭐⭐⭐⭐⭐ (Universal standard)  
**Complexity:** Very High  
**Status:** 🔄 Implement (XML-based)

### Format Description
Open XML standard for music notation interchange. Supported by most notation software.

### Features
- Universal music notation
- Multi-instrument support
- Guitar-specific elements (tablature, techniques)
- Comprehensive metadata
- Compressed format (.mxl)

### Implementation Notes
- **XML format**
- **Very comprehensive** - supports all music notation
- **Existing libraries:**
  - `MusicXML.NET` (C#) - full implementation
  - Consider using existing library

### Grammar Complexity
**Very High** - Comprehensive XML schema

---

## 7. MIDI (.mid, .midi)

**File Extension:** `.mid`, `.midi`  
**Popularity:** ⭐⭐⭐⭐⭐ (Universal audio standard)  
**Complexity:** Medium  
**Status:** 🔄 Implement (Binary format)

### Format Description
Binary format for musical performance data (notes, timing, velocity).

### Features
- Note on/off events
- Timing information
- Velocity (dynamics)
- Multi-track
- **No tablature** - only pitch information

### Implementation Notes
- **Binary format**
- **Well-documented** standard
- **Existing libraries:**
  - `NAudio` (C#) - MIDI support
  - `Melanchall.DryWetMidi` (C#) - comprehensive MIDI library
- **Conversion challenge:** MIDI → Tab requires pitch-to-fret mapping

### Grammar Complexity
**Medium** - Binary format with clear specification

---

## 8. TablEdit (.tef, .tg)

**File Extension:** `.tef`, `.tg`  
**Popularity:** ⭐⭐ (Niche)  
**Complexity:** High  
**Status:** ⏸️ Low Priority

### Format Description
Proprietary format for TablEdit software.

### Features
- Multi-instrument tablature
- Standard notation
- Guitar techniques

### Implementation Notes
- **Proprietary format**
- **Limited documentation**
- **Low priority** - less common

---

## 9. Songsterr (.gp5, web format)

**File Extension:** Uses GP5 format  
**Popularity:** ⭐⭐⭐⭐ (Popular website)  
**Complexity:** N/A  
**Status:** ⏸️ Uses GP5

### Notes
Songsterr uses Guitar Pro 5 format internally. No separate format needed.

---

## 10. Ultimate Guitar (.crd, .tab, .pro)

**File Extension:** `.crd` (chords), `.tab` (ASCII tab), `.pro` (Guitar Pro)  
**Popularity:** ⭐⭐⭐⭐⭐ (Largest tab database)  
**Complexity:** Low-Medium  
**Status:** ⏸️ Uses existing formats

### Notes
Ultimate Guitar uses:
- ASCII tab for simple tabs
- Guitar Pro format for "Pro" tabs
- Custom chord format for chord sheets

---

## Implementation Priority

### Phase 1: Text-Based Formats (Weeks 1-2)
1. ✅ **VexTab** - Already done!
2. 🔄 **ASCII Tab** - Simple, universal
3. 🔄 **TuxGuitar (.tg)** - XML-based, open

### Phase 2: Binary Formats with Libraries (Weeks 3-4)
4. 🔄 **MIDI** - Use existing C# library
5. 🔄 **MusicXML** - Use existing C# library
6. 🔄 **Guitar Pro** - Use alphaTab library

### Phase 3: Advanced Binary Formats (Weeks 5-6)
7. 🔄 **PowerTab** - Binary, open spec
8. ⏸️ **TablEdit** - Low priority

---

## Conversion Matrix

| From/To | ASCII | VexTab | GP | PTB | TG | MusicXML | MIDI |
|---------|-------|--------|----|----|----|---------| -----|
| ASCII   | -     | ✅     | ✅ | ✅ | ✅ | ✅      | ✅   |
| VexTab  | ✅    | -      | ✅ | ✅ | ✅ | ✅      | ✅   |
| GP      | ✅    | ✅     | -  | ✅ | ✅ | ✅      | ✅   |
| PTB     | ✅    | ✅     | ✅ | -  | ✅ | ✅      | ✅   |
| TG      | ✅    | ✅     | ✅ | ✅ | -  | ✅      | ✅   |
| MusicXML| ✅    | ✅     | ✅ | ✅ | ✅ | -       | ✅   |
| MIDI    | ⚠️    | ⚠️     | ⚠️ | ⚠️ | ⚠️ | ✅      | -    |

**Legend:**
- ✅ = Conversion possible
- ⚠️ = Lossy conversion (MIDI has no tablature info)
- `-` = Same format

---

## Next Steps

1. Create EBNF grammar for ASCII Tab
2. Implement ASCII Tab parser
3. Create conversion service architecture
4. Integrate existing libraries (MIDI, MusicXML, Guitar Pro)
5. Build microservice API
6. Create React demo page
7. Write comprehensive tests

---

**Last Updated:** 2025-11-01  
**Status:** Planning Complete, Implementation Starting

