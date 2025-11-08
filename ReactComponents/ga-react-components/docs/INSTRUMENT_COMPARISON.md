# Instrument Comparison Matrix

This document provides a comprehensive comparison of different stringed instruments supported by the generic fretboard component.

## 📊 Quick Reference Table

| Instrument | Strings | Frets | Scale (mm) | Nut (mm) | Body Style | Special Features |
|------------|---------|-------|------------|----------|------------|------------------|
| **Guitar** | 6 | 19 | 650 | 52 | Classical | Standard |
| **Guitar (12-string)** | 12 | 19 | 650 | 60 | Acoustic | Doubled strings |
| **Bass Guitar** | 4 | 24 | 860 | 45 | Bass | Low tuning |
| **Bass (5-string)** | 5 | 24 | 860 | 48 | Bass | Extended low B |
| **Bass (6-string)** | 6 | 24 | 860 | 50 | Bass | Extended range |
| **Ukulele (Soprano)** | 4 | 12 | 330 | 35 | Ukulele | Re-entrant tuning |
| **Ukulele (Baritone)** | 4 | 18 | 510 | 38 | Ukulele | Linear tuning |
| **Banjo (5-string)** | 5 | 22 | 660 | 32 | Banjo | Drone string |
| **Banjo (4-string)** | 4 | 22 | 660 | 30 | Banjo | Tenor/Plectrum |
| **Mandolin** | 8 | 20 | 350 | 28 | Mandolin | 4 courses |
| **Baritone Guitar** | 6 | 22 | 686 | 48 | Electric | Extended scale |
| **Russian Guitar** | 7 | 19 | 650 | 55 | Classical | Open G tuning |
| **Lute** | 12 | 19 | 600 | 50 | Lute | Historical |

## 🎸 Detailed Instrument Profiles

### Guitar Family

#### Standard Guitar (6-string)
```yaml
Family: Guitar
Variant: Standard
Tuning: E2 A2 D3 G3 B3 E4
Scale: 650mm (classical), 648mm (electric)
Nut Width: 52mm (classical), 43mm (electric)
Frets: 19 (classical), 21-24 (electric)
Body Styles: classical, acoustic, electric
```

**Common Tunings:**
- Standard: E A D G B E
- Drop D: D A D G B E
- DADGAD: D A D G A D
- Open G: D G D G B D

#### 12-String Guitar
```yaml
Family: Guitar
Variant: TwelveStrings
Tuning: E2 E3 A2 A3 D3 D4 G3 G4 B3 B3 E4 E4
Scale: 650mm
Nut Width: 60mm (wider for 12 strings)
Frets: 19
Body Style: acoustic
```

**Features:**
- Strings 1-2 (E): Unison
- Strings 3-4 (B): Unison
- Strings 5-12: Octave pairs

#### Baritone Guitar
```yaml
Family: BaritoneGuitar
Variant: Standard1
Tuning: B1 E2 A2 D3 F#3 B3
Scale: 686mm (27")
Nut Width: 48mm
Frets: 22
Body Style: electric
```

**Use Cases:**
- Extended low range
- Jazz, metal, ambient music
- Tuned a 4th below standard guitar

### Bass Family

#### 4-String Bass
```yaml
Family: BassGuitar
Variant: Standard
Tuning: E1 A1 D2 G2
Scale: 860mm (34")
Nut Width: 45mm
Frets: 20-24
Body Style: bass
```

**Common Scales:**
- Short: 762mm (30")
- Medium: 813mm (32")
- Standard: 860mm (34")
- Extra Long: 914mm (36")

#### 5-String Bass
```yaml
Family: BassGuitar
Variant: FiveStrings
Tuning: B0 E1 A1 D2 G2
Scale: 860mm (34")
Nut Width: 48mm
Frets: 24
Body Style: bass
```

**Features:**
- Extended low B string
- Wider nut for 5 strings
- Popular in modern music

#### 6-String Bass
```yaml
Family: BassGuitar
Variant: SixStrings
Tuning: B0 E1 A1 D2 G2 C3
Scale: 860mm (34")
Nut Width: 50mm
Frets: 24
Body Style: bass
```

**Features:**
- Extended range (low B, high C)
- Solo bass playing
- Jazz fusion

### Ukulele Family

#### Soprano Ukulele
```yaml
Family: Ukulele
Variant: SopranoConcertAndTenorC
Tuning: G4 C4 E4 A4 (re-entrant)
Scale: 330mm (13")
Nut Width: 35mm
Frets: 12
Body Style: ukulele
```

**Features:**
- Re-entrant tuning (high G)
- Smallest ukulele
- Traditional Hawaiian sound

#### Concert Ukulele
```yaml
Scale: 380mm (15")
Nut Width: 36mm
Frets: 15-18
```

#### Tenor Ukulele
```yaml
Scale: 430mm (17")
Nut Width: 38mm
Frets: 15-19
```

#### Baritone Ukulele
```yaml
Family: Ukulele
Variant: Baritone
Tuning: D4 G3 B4 E4 (linear)
Scale: 510mm (20")
Nut Width: 38mm
Frets: 18-21
Body Style: ukulele
```

**Features:**
- Linear tuning (like guitar)
- Deeper, fuller sound
- Tuned like top 4 guitar strings

### Banjo Family

#### 5-String Bluegrass Banjo
```yaml
Family: Banjo
Variant: Bluegrass5Strings
Tuning: G4 D3 G3 B3 D4
Scale: 660mm (26")
Nut Width: 32mm
Frets: 22
Body Style: banjo
Special: Drone string at 5th position
```

**Features:**
- 5th string (drone) starts at 5th fret
- Open G tuning
- Bright, percussive sound

#### 4-String Tenor Banjo
```yaml
Family: Banjo
Variant: TenorJazz
Tuning: C3 G3 D4 A4
Scale: 580mm (23")
Nut Width: 30mm
Frets: 19
Body Style: banjo
```

**Use Cases:**
- Jazz, Dixieland
- Irish traditional music
- No drone string

### Mandolin Family

#### Standard Mandolin
```yaml
Family: Mandolin
Variant: Standard
Tuning: G3 G3 D4 D4 A4 A4 E5 E5
Scale: 350mm (13.875")
Nut Width: 28mm
Frets: 20
Body Style: mandolin
```

**Features:**
- 8 strings in 4 courses (pairs)
- Tuned like violin (G D A E)
- Bright, cutting tone

#### Mandola
```yaml
Family: Mandola
Variant: Tenor
Tuning: C3 C3 G3 G3 D4 D4 A4 A4
Scale: 420mm (16.5")
```

**Features:**
- Tuned a 5th below mandolin
- Larger body
- Deeper tone

#### Mandocello
```yaml
Family: Mandocello
Variant: Standard
Tuning: C2 C2 G2 G2 D3 D3 A3 A3
Scale: 660mm (26")
```

**Features:**
- Tuned an octave below mandolin
- Cello-sized
- Bass voice in mandolin orchestra

## 🎯 Tuning Patterns

### Standard Tunings by Instrument

| Instrument | Tuning | Interval Pattern |
|------------|--------|------------------|
| Guitar | E A D G B E | P4 P4 P4 M3 P4 |
| Bass | E A D G | P4 P4 P4 |
| Ukulele (Soprano) | G C E A | P4 M3 P4 (re-entrant) |
| Ukulele (Baritone) | D G B E | P4 M3 P4 |
| Banjo (5-string) | G D G B D | P5 P4 M3 m3 |
| Mandolin | G D A E | P5 P5 P5 |

### Alternative Tunings

#### Guitar
- **Drop D**: D A D G B E
- **DADGAD**: D A D G A D
- **Open G**: D G D G B D
- **Open D**: D A D F# A D

#### Bass
- **Drop D**: D A D G
- **Drop C**: C G C F

#### Ukulele
- **Low G**: G3 C4 E4 A4 (linear)
- **D Tuning**: A D F# B

## 📐 Scale Length Comparison

```
Ukulele (Soprano)    ████░░░░░░░░░░░░░░░░ 330mm
Mandolin             ████░░░░░░░░░░░░░░░░ 350mm
Ukulele (Baritone)   ███████░░░░░░░░░░░░░ 510mm
Guitar (Classical)   █████████████░░░░░░░ 650mm
Banjo (5-string)     ██████████████░░░░░░ 660mm
Baritone Guitar      ██████████████░░░░░░ 686mm
Bass (34")           ████████████████████ 860mm
Bass (36")           █████████████████████ 914mm
```

## 🎨 Visual Characteristics

### Fretboard Width Progression

```
                    Nut → Bridge
Mandolin:           28mm → 32mm   (narrow)
Banjo:              32mm → 35mm   (narrow)
Ukulele:            35mm → 40mm   (narrow)
Electric Guitar:    43mm → 58mm   (medium)
Bass:               45mm → 60mm   (medium)
Classical Guitar:   52mm → 70mm   (wide)
Russian Guitar:     55mm → 75mm   (wide)
12-String Guitar:   60mm → 80mm   (very wide)
```

### Fret Count Distribution

```
12 frets:  Ukulele (Soprano)
15 frets:  Ukulele (Concert)
19 frets:  Classical Guitar, Russian Guitar
20 frets:  Mandolin
21 frets:  Electric Guitar (vintage)
22 frets:  Banjo, Baritone Guitar, Electric Guitar
24 frets:  Bass, Modern Electric Guitar
```

## 🔧 Implementation Notes

### Rendering Considerations

1. **String Spacing**
   - Narrow instruments (mandolin, banjo): Tighter string spacing
   - Wide instruments (classical guitar): Wider string spacing
   - Adjust visual spacing based on nut/bridge width

2. **Fret Spacing**
   - All instruments use equal temperament
   - Formula: `position = scaleLength * (1 - 2^(-fret/12))`
   - Shorter scales = wider fret spacing visually

3. **Special Features**
   - **Banjo drone string**: Render 5th string starting at 5th fret
   - **Doubled strings**: Render pairs close together (12-string, mandolin)
   - **Re-entrant tuning**: Visual indicator for high G on ukulele

4. **Body Styles**
   - Classical: Wide neck, nylon strings, rosette
   - Acoustic: Steel strings, pickguard, rosette
   - Electric: Narrow neck, pickups, no rosette
   - Bass: Thick strings, large body
   - Ukulele: Small body, figure-8 shape
   - Banjo: Circular body, drum head
   - Mandolin: Teardrop or A-style body

## 🎵 Use Case Matrix

| Instrument | Music Genres | Difficulty | Portability |
|------------|--------------|------------|-------------|
| Ukulele | Hawaiian, Pop | ⭐ Easy | ⭐⭐⭐ High |
| Guitar | All genres | ⭐⭐ Medium | ⭐⭐ Medium |
| Bass | Rock, Jazz, Funk | ⭐⭐ Medium | ⭐ Low |
| Banjo | Bluegrass, Folk | ⭐⭐⭐ Hard | ⭐⭐ Medium |
| Mandolin | Bluegrass, Classical | ⭐⭐⭐ Hard | ⭐⭐⭐ High |

---

**Last Updated**: 2025-01-20  
**Maintained by**: GA Development Team

