# Guitar Body & Headstock Features - Visual Guide

## What You Should See

### Classical Guitar (Full View)
When you select a classical guitar model (e.g., `classical_yamaha_cg`), you should see:

```
[Headstock]--[Nut]----[Frets]----[Bridge]--[Body with Rosette]
   ⊙ ⊙                                           ___
   ⊙ ⊙                                         /  O  \
   ⊙ ⊙                                        |  |||  |
                                               \ ___ /
```

**Headstock Features:**
- **Slotted Headstock**: Traditional classical guitar design
- **3x3 Tuning Pegs**: 3 pegs on each side
- **String Slots**: Visible slots for strings to pass through
- **Tuning Buttons**: Bone/plastic colored buttons on each peg

**Body Features:**
- **Sound Hole**: Black circle in the center
- **Rosette**: 5 concentric decorative rings around the sound hole
  - Alternating brown (0x8b4513) and gold (0xffd700) colors
  - Ring widths: 2px, 1px, 2px, 1px, 2px
- **Mosaic Pattern**: 24 small dots arranged in a circle
  - Alternating gold and brown colors
- **Body Wood Grain**: Horizontal grain lines on the body
- **Curved Body Shape**: Natural classical guitar body curve

### Acoustic Guitar (Full View)
When you select an acoustic guitar model (e.g., `acoustic_martin_d28`), you should see:

```
[Headstock]--[Nut]----[Frets]----[Bridge]--[Body with Pickguard]
   ⊙                                             ___
   ⊙                                           /     \
   ⊙                                          | [PG]  |
⊙                                              \ ___ /
⊙
⊙
```

**Headstock Features:**
- **3x3 Configuration**: 3 tuning pegs on each side (Martin/Gibson style)
- **Angled Headstock**: Traditional acoustic guitar headstock angle
- **Tuning Buttons**: Bone/plastic colored buttons

**Body Features:**
- **Pickguard**: Semi-transparent black curved shape
- **Body Shape**: Steel-string acoustic proportions
- **No Sound Hole**: (Would be on top of guitar, not visible from this angle)

### Electric Guitar (Full View)
When you select an electric guitar model (e.g., `electric_ibanez_rg`), you should see:

```
[Headstock]--[Nut]----[Frets]----[Bridge]--[Body with Pickups]
⊙                                              _____
⊙                                             |[PU1]|
⊙                                             | o o |
⊙                                             |[PU2]|
⊙                                             | ⊙ ⊙ |
⊙                                              ‾‾‾‾‾
```

**Headstock Features (varies by brand):**
- **Fender/Ibanez**: 6-inline configuration (all 6 tuners on one side)
- **Gibson**: 3x3 configuration (3 tuners per side)
- **Angled Design**: Modern headstock shape
- **Tuning Buttons**: Bone/plastic colored buttons with center dots

**Body Features:**
- **Neck Pickup**: Black rectangle with 6 silver pole pieces
  - Position: 20px from body start
  - Size: 50px wide
- **Bridge Pickup**: Black rectangle with 6 silver pole pieces
  - Position: 80px from body start
  - Size: 50px wide
- **Control Knobs**: 2 knobs (volume and tone)
  - Dark gray circles with white highlights
  - Indicator lines showing knob position
- **Glossy Highlight**: White curved line for painted finish effect
- **Angular Body Shape**: Modern electric guitar contour

## Headstock Styles

### Classical Slotted (`classical-slotted`)
- **Configuration**: 3 pegs per side
- **Design**: Rectangular with slight taper
- **String Slots**: Visible vertical slots for strings
- **Tuning Pegs**: Positioned on both sides with buttons extending outward
- **Used On**: All classical guitars (Yamaha, Torres, Alhambra)

### Fender 6-Inline (`fender-6-inline`)
- **Configuration**: All 6 tuners on one side
- **Design**: Elongated headstock with curved edges
- **Tuning Pegs**: Evenly spaced along one side
- **Buttons**: Extend to the left side
- **Used On**: Fender Stratocaster, Telecaster

### Ibanez 6-Inline (`ibanez-6-inline`)
- **Configuration**: All 6 tuners on one side
- **Design**: Similar to Fender but more angular
- **Tuning Pegs**: Evenly spaced along one side
- **Used On**: Ibanez RG, S, JEM series

### Gibson 3x3 (`gibson-3x3`)
- **Configuration**: 3 tuners per side
- **Design**: Angled headstock with distinctive shape
- **Tuning Pegs**: 3 on left, 3 on right
- **Used On**: Gibson Les Paul, SG, ES-335

### Martin 3x3 (`martin-3x3`)
- **Configuration**: 3 tuners per side
- **Design**: Similar to Gibson but with acoustic proportions
- **Used On**: Martin D-28, Taylor 814ce

### PRS 3x3 (`prs-3x3`)
- **Configuration**: 3 tuners per side
- **Design**: Distinctive PRS headstock shape
- **Used On**: PRS Custom, SE series

## Canvas Dimensions

- **Total Width**: 1700px (increased to show headstock and body)
- **Total Height**: 250px
- **Left Margin**: 150px (space for headstock)
- **Right Margin**: 200px (space for guitar body)
- **Headstock Width**: 100px (visible portion)
- **Headstock Height**: 1.5x nut height
- **Body Width**: 180px (visible portion of guitar body)
- **Body Height**: 1.4x neck height at bridge

## Positioning

### Headstock Positioning
- **X Position**: `leftX` (nut position, left edge of fretboard)
- **Y Position**: `centerY` (vertically centered)
- **Extends**: 100px to the left of the nut

### Body Positioning
- **X Position**: `rightX` (bridge position, right edge of fretboard)
- **Y Position**: `centerY` (vertically centered)
- **Extends**: 180px to the right of the bridge

## Color Codes

### Headstock (All Types)
- **Wood**: Same as fretboard wood color (guitarStyle.woodColor)
- **Tuning Pegs**: 0x2a2a2a (dark metal)
- **Tuning Buttons**: 0xf5f5dc (bone/plastic)
- **String Slots**: 0x000000 (black)
- **Button Dots**: 0x808080 (gray, for electric guitars)

### Classical/Acoustic Body
- **Body Wood**: Same as fretboard wood color (guitarStyle.woodColor)
- **Rosette Rings**: 0x8b4513 (brown), 0xffd700 (gold)
- **Sound Hole**: 0x000000 (black)
- **Mosaic Dots**: Alternating gold/brown

### Electric Body
- **Body**: guitarStyle.woodColor (varies by model)
- **Pickups**: 0x1a1a1a (black)
- **Pole Pieces**: 0xc0c0c0 (silver)
- **Knobs**: 0x2a2a2a (dark gray)
- **Highlight**: 0xffffff (white, 20% opacity)

## Troubleshooting

### "I don't see the headstock"
1. Check canvas width is 1700px (not 1400px or 1600px)
2. Check labelWidth is 150px (not 40px)
3. Scroll horizontally if needed
4. Check that drawHeadstock() is called before fretboard wood is drawn
5. Verify guitarStyle.headstockStyle is set correctly

### "I don't see the guitar body"
1. Check canvas width is 1700px (not 1400px)
2. Check rightMargin is 200px (not 10px)
3. Scroll horizontally if needed
4. Check that drawGuitarBody() is called before fretboard wood is drawn

### "I don't see the rosette/sound hole"
1. Make sure you selected a classical guitar model
2. Check that rosetteX = bodyX + bodyWidth * 0.6 (should be ~108px from bridge)
3. Check rosetteRadius = bodyHeight * 0.25
4. Verify sound hole is drawn before rosette rings

### "I don't see the pickups"
1. Make sure you selected an electric guitar model
2. Check pickup positions:
   - Neck pickup: bodyX + 20
   - Bridge pickup: bodyX + 80
3. Verify pickups are drawn after body background
4. Check pole pieces are visible (6 silver dots per pickup)

### "The body is cut off"
1. Increase canvas width to 1600px or more
2. Increase rightMargin to 200px or more
3. Check that bodyWidth is 180px
4. Ensure horizontal scrolling is enabled

## Testing

To test each guitar type:

```typescript
// Classical - should show rosette
<RealisticFretboard config={{ guitarModel: 'classical_yamaha_cg' }} />

// Acoustic - should show pickguard
<RealisticFretboard config={{ guitarModel: 'acoustic_martin_d28' }} />

// Electric - should show pickups and knobs
<RealisticFretboard config={{ guitarModel: 'electric_ibanez_rg' }} />
```

## Layer Order (Z-Index)

From back to front:
1. Canvas background (dark gray)
2. **Guitar body** (drawn first, appears behind neck)
3. **Headstock** (drawn second, appears behind neck)
4. Fretboard wood (trapezoid)
5. Wood grain effects
6. Frets and nut
7. Fret markers/inlays
8. Bridge
9. Strings
10. Position markers
11. Labels and text

The guitar body and headstock MUST be drawn before the fretboard wood to appear behind the neck.

