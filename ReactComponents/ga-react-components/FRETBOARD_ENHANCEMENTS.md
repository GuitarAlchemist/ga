# Fretboard Visualization Enhancements

## Recent Improvements

### 1. Enhanced Wood Grain Effect (RealisticFretboard.tsx)

The neck now features highly realistic wood grain rendering with:

- **Color Variation**: Subtle patches of lighter and darker wood tones
- **Realistic Grain Lines**: 150 wavy grain lines with natural variation
  - Variable density (some areas denser than others)
  - Natural waviness (not perfectly straight)
  - Randomized thickness and darkness
- **Wood Knots**: 3-7 realistic knots with concentric ring patterns
- **Vertical Grain**: Subtle vertical grain lines showing wood fiber direction
- **Seeded Randomness**: Consistent pattern that doesn't change on re-render

### 2. Neck Profile System (NeckProfiles.ts)

Comprehensive neck profile database inspired by real guitar manufacturers:

#### Ibanez Profiles
- **Wizard**: Thin, flat profile for fast playing (19mm @ 1st fret)
- **Wizard II**: Slightly thicker, more comfortable (20mm @ 1st fret)
- **Super Wizard**: Ultra-thin for maximum speed (17mm @ 1st fret)
- **Nitro Wizard**: Wizard with nitrocellulose finish

#### Fender Profiles
- **Modern C**: Flatter than vintage (21mm @ 1st fret)
- **Vintage C**: Classic rounded 1960s shape (22mm @ 1st fret)
- **Soft V**: Vintage-inspired V-shape (23mm @ 1st fret)
- **Chunky U**: Thick 1950s U-shape (24mm @ 1st fret)

#### Gibson Profiles
- **Slim Taper**: 60s slim taper (20mm @ 1st fret)
- **Rounded Profile**: 50s chunky rounded (22mm @ 1st fret)
- **Asymmetric Slim Taper**: Modern asymmetric (thicker on bass side)

#### Other Profiles
- **Classical Standard**: Wide, flat classical guitar neck
- **Ultra Thin**: Modern shred profile (18mm @ 1st fret)

Each profile includes:
- Thickness measurements (1st and 12th fret)
- Fretboard radius (7.25" to 20")
- Material (rosewood, maple, ebony, etc.)
- Finish type (gloss, satin, oil, nitro)
- Visual properties (curve, asymmetry)
- Common guitar models
- Era (vintage, modern, contemporary)
- Recommended play styles

### 3. Guitar Body Rendering

Added partial guitar body visualization for enhanced realism:

#### Classical Guitars
- Curved body shape with natural wood grain
- **Detailed Rosette**: Decorative circle around sound hole
  - Multiple concentric rings (gold and brown)
  - 24-segment mosaic pattern
  - Realistic sound hole (dark center)
- Wood grain lines on body

#### Acoustic Guitars
- Similar curved body to classical
- Pickguard detail (semi-transparent black)
- Appropriate proportions for steel-string acoustics

#### Electric Guitars
- More angular, modern body shape
- Glossy highlight for painted finish
- **Neck and Bridge Pickups**: Black pickups with silver pole pieces (6 per pickup)
- **Control Knobs**: Volume and tone knobs with indicator lines
- Realistic hardware details

### 4. Enhanced Bridge Rendering

Realistic bridge designs based on guitar type:

#### Classical/Acoustic Bridge
- Wooden bridge body (darker wood tone)
- Bone/plastic saddle (light colored)
- Saddle highlight for realism
- Wider, more substantial appearance

#### Electric Bridge
- Metallic appearance (dark gray)
- Individual saddles for each string
- Saddle adjustment screws (detail)
- Metallic highlights and shadows

## Usage

### Basic Usage

```typescript
import { RealisticFretboard } from './components/RealisticFretboard';

<RealisticFretboard
  title="My Guitar"
  config={{
    guitarModel: 'electric_ibanez_rg',  // Uses Wizard neck profile
    spacingMode: 'realistic',
    fretCount: 24,
  }}
  positions={[
    { string: 0, fret: 5, label: 'A', color: '#ff6b6b' }
  ]}
/>
```

### Available Guitar Models

Each model now includes a neck profile:

- **Classical**: `classical_yamaha_cg`, `classical_torres`, `classical_alhambra`
  - All use `classical-standard` neck profile
  - Features rosette and classical body
  
- **Acoustic**: `acoustic_martin_d28`, `acoustic_taylor_814`, `acoustic_gibson_j45`
  - Use `modern-c` or `rounded-profile` neck profiles
  - Features pickguard and acoustic body
  
- **Electric**: `electric_fender_strat`, `electric_gibson_les_paul`, `electric_ibanez_rg`
  - Use `modern-c`, `slim-taper`, or `wizard` neck profiles
  - Features electric body and individual saddles

### Accessing Neck Profile Information

```typescript
import { getNeckProfile, getProfilesForPlayStyle } from './components/NeckProfiles';

// Get specific profile
const wizardProfile = getNeckProfile('wizard');
console.log(wizardProfile.thickness1stFret); // 19mm
console.log(wizardProfile.description); // "Ibanez signature thin, flat profile..."

// Find profiles for play style
const shredProfiles = getProfilesForPlayStyle('shred');
// Returns: Wizard, Super Wizard, Ultra Thin, etc.
```

## Visual Features Summary

### Neck
- ✅ Realistic wood grain with natural variation
- ✅ Wood knots with concentric rings
- ✅ Horizontal and vertical grain patterns
- ✅ Color variation patches
- ✅ Trapezoid shape (wider at bridge)

### Body
- ✅ Partial body visible at bridge end (180px width)
- ✅ Classical: Detailed rosette with mosaic pattern and sound hole
- ✅ Acoustic: Pickguard detail
- ✅ Electric: Glossy highlights, pickups (neck & bridge), control knobs

### Bridge
- ✅ Type-specific bridge design
- ✅ Classical/Acoustic: Wooden bridge with bone saddle
- ✅ Electric: Metal bridge with individual saddles
- ✅ Realistic highlights and shadows

### Hardware
- ✅ Realistic nut (bone/plastic)
- ✅ Metal frets with highlights
- ✅ Fret markers (dots, blocks, trapezoids, etc.)
- ✅ Strings with cylindrical shading

## Future Enhancements

Potential additions:
- [ ] Headstock rendering (different shapes per brand)
- [ ] Tuning pegs/machine heads
- [ ] Binding on neck edges
- [ ] More inlay styles (tree of life, custom designs)
- [ ] Fretboard radius visualization (compound radius)
- [ ] Scalloped frets option
- [ ] Different wood types with unique grain patterns
- [ ] Wear and aging effects (vintage guitars)
- [ ] Position markers on side of neck
- [ ] Strap buttons
- [ ] Output jack (electric guitars)

## References

- [Ibanez Neck Types](https://ibanez.fandom.com/wiki/List_of_neck_types)
- [Fender Neck Profiles](https://www.fender.com/articles/tech-talk/neck-profiles-explained)
- [Gibson Neck Profiles](https://www.gibson.com/en-US/Guitars/Neck-Profiles)
- Classical Guitar Construction Standards

