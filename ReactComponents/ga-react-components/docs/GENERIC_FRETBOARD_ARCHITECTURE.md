# Generic Fretboard Component Architecture

## 📋 Executive Summary

This document proposes a **unified, generic fretboard component architecture** that can render any stringed instrument (guitar, bass, ukulele, banjo, mandolin, etc.) without creating separate components for each instrument type.

## 🔍 Analysis of Instruments.yaml

### Structure Overview

The `Instruments.yaml` file contains **60+ stringed instruments** with **200+ tuning variations**:

```yaml
InstrumentFamily:
  DisplayName: "Human-readable name"
  TuningVariant:
    DisplayName: "Variant name"
    FullName: "Optional full name"
    Tuning: "Note1 Note2 Note3 ..."  # Space-separated pitch notation
```

### Key Findings

#### 1. **Common Properties Across All Instruments**

| Property | Description | Example Values |
|----------|-------------|----------------|
| **String Count** | Number of strings | 3-16 strings |
| **Tuning** | Pitch for each string | `E2 A2 D3 G3 B3 E4` |
| **DisplayName** | Human-readable name | "Standard", "Drop D", "Baritone" |
| **FullName** | Optional detailed name | "5 strings Bluegrass" |

#### 2. **Instrument Categories by String Count**

```
3 strings:  Balalaika (Alto, Prima), Panduri, Phin
4 strings:  Bass Guitar, Banjo (Tenor, Plectrum), Ukulele (Baritone), Mandolin
5 strings:  Bass Guitar (5-string), Banjo (Bluegrass)
6 strings:  Guitar (Standard, Drop D, DADGAD), Baritone Guitar
7 strings:  Russian Guitar, Guitar (Renaissance)
8 strings:  Mandola, Mandocello
10 strings: Guitar (Baroque), English Guittar, Bordonua
12 strings: Guitar (12-string), Lute (Medieval)
16 strings: Bandurria (Peruvian)
```

#### 3. **Scale Length Variations** (Not in YAML, but needed)

Different instruments have different scale lengths:
- **Ukulele**: 330-380mm (soprano), 430mm (tenor)
- **Guitar**: 650mm (classical), 648mm (electric)
- **Bass**: 860mm (34"), 914mm (36")
- **Mandolin**: 330-350mm

#### 4. **Fret Count Variations**

- **Ukulele**: 12-15 frets
- **Classical Guitar**: 19 frets
- **Electric Guitar**: 21-24 frets
- **Bass**: 20-24 frets
- **Banjo**: 22 frets

## 🏗️ Proposed Generic Architecture

### Option A: Single Unified Component (RECOMMENDED)

Create **ONE** generic `<StringedInstrumentFretboard>` component that handles all instruments and rendering modes.

```typescript
// Generic instrument configuration
interface InstrumentConfig {
  // From YAML
  family: string;              // "Guitar", "Bass", "Ukulele", etc.
  variant: string;             // "Standard", "Drop D", "Baritone", etc.
  tuning: string[];            // ["E2", "A2", "D3", "G3", "B3", "E4"]
  displayName: string;         // "Standard Guitar"
  
  // Physical properties (from instrument database or defaults)
  scaleLength: number;         // mm (e.g., 650 for classical guitar)
  nutWidth: number;            // mm (e.g., 52 for classical, 43 for electric)
  bridgeWidth: number;         // mm
  fretCount: number;           // 12-24
  
  // Visual properties
  bodyStyle?: 'classical' | 'acoustic' | 'electric' | 'bass' | 'ukulele' | 'banjo';
  woodColor?: number;          // Hex color
  hasRosette?: boolean;        // For acoustic instruments
  hasPickguard?: boolean;      // For acoustic/electric
}

// Generic fretboard component
interface StringedInstrumentFretboardProps {
  // Instrument configuration
  instrument: InstrumentConfig;
  
  // Rendering mode
  renderMode: '2d-svg' | '2d-canvas' | '3d-webgl' | '3d-webgpu';
  
  // Position markers
  positions?: FretboardPosition[];
  
  // Common features
  capoFret?: number;
  leftHanded?: boolean;
  showFretNumbers?: boolean;
  showStringLabels?: boolean;
  
  // Rendering options
  width?: number;
  height?: number;
  spacingMode?: 'realistic' | 'schematic';
  enableOrbitControls?: boolean;  // For 3D modes
  
  // Callbacks
  onPositionClick?: (string: number, fret: number) => void;
}
```

### Component Structure

```
<StringedInstrumentFretboard>
  ├── InstrumentConfigProvider (context)
  ├── RenderModeSelector
  │   ├── SVGRenderer (GuitarFretboard logic)
  │   ├── CanvasRenderer (RealisticFretboard logic)
  │   ├── WebGLRenderer (ThreeFretboard logic)
  │   └── WebGPURenderer (WebGPUFretboard logic)
  ├── CommonControls
  │   ├── CapoSelector
  │   ├── LeftHandedToggle
  │   └── InstrumentSelector
  └── PositionMarkers
```

### Usage Examples

```typescript
// Guitar - Standard tuning
<StringedInstrumentFretboard
  instrument={{
    family: 'Guitar',
    variant: 'Standard',
    tuning: ['E2', 'A2', 'D3', 'G3', 'B3', 'E4'],
    scaleLength: 650,
    nutWidth: 52,
    fretCount: 19,
    bodyStyle: 'classical'
  }}
  renderMode="3d-webgpu"
  positions={cMajorChord}
  capoFret={0}
/>

// Bass - 5-string
<StringedInstrumentFretboard
  instrument={{
    family: 'BassGuitar',
    variant: 'FiveStrings',
    tuning: ['B0', 'E1', 'A1', 'D2', 'G2'],
    scaleLength: 860,
    nutWidth: 45,
    fretCount: 24,
    bodyStyle: 'bass'
  }}
  renderMode="2d-canvas"
  positions={bassLine}
/>

// Ukulele - Soprano
<StringedInstrumentFretboard
  instrument={{
    family: 'Ukulele',
    variant: 'SopranoConcertAndTenorC',
    tuning: ['G4', 'C4', 'E4', 'A4'],
    scaleLength: 330,
    nutWidth: 35,
    fretCount: 12,
    bodyStyle: 'ukulele'
  }}
  renderMode="2d-svg"
  positions={ukuleleChord}
/>

// Banjo - Bluegrass 5-string
<StringedInstrumentFretboard
  instrument={{
    family: 'Banjo',
    variant: 'Bluegrass5Strings',
    tuning: ['G4', 'D3', 'G3', 'B3', 'D4'],
    scaleLength: 660,
    nutWidth: 32,
    fretCount: 22,
    bodyStyle: 'banjo'
  }}
  renderMode="3d-webgl"
  positions={banjoRoll}
/>
```

## 📦 Implementation Plan

### Phase 1: Core Infrastructure (Week 1)

1. **Create Instrument Configuration System**
   ```typescript
   // src/config/instruments.ts
   export const INSTRUMENT_DEFAULTS: Record<string, Partial<InstrumentConfig>> = {
     Guitar: {
       scaleLength: 650,
       nutWidth: 52,
       bridgeWidth: 70,
       fretCount: 19,
       bodyStyle: 'classical'
     },
     BassGuitar: {
       scaleLength: 860,
       nutWidth: 45,
       bridgeWidth: 60,
       fretCount: 24,
       bodyStyle: 'bass'
     },
     Ukulele: {
       scaleLength: 330,
       nutWidth: 35,
       bridgeWidth: 40,
       fretCount: 12,
       bodyStyle: 'ukulele'
     },
     // ... more instruments
   };
   ```

2. **Parse Instruments.yaml**
   ```typescript
   // src/config/instrumentLoader.ts
   export async function loadInstruments(): Promise<InstrumentDatabase> {
     const yaml = await fetch('/config/Instruments.yaml');
     const data = parseYAML(yaml);
     return buildInstrumentDatabase(data);
   }
   ```

3. **Create Generic Fretboard Math**
   ```typescript
   // src/utils/fretboardMath.ts
   export function calculateFretPosition(
     fretNumber: number,
     scaleLength: number
   ): number {
     return scaleLength * (1 - Math.pow(2, -fretNumber / 12));
   }
   
   export function calculateStringSpacing(
     stringIndex: number,
     stringCount: number,
     nutWidth: number,
     bridgeWidth: number,
     position: number  // 0 = nut, 1 = bridge
   ): number {
     const width = nutWidth + (bridgeWidth - nutWidth) * position;
     return (stringIndex / (stringCount - 1) - 0.5) * width;
   }
   ```

### Phase 2: Unified Renderer (Week 2)

1. **Create Renderer Interface**
   ```typescript
   interface FretboardRenderer {
     render(
       container: HTMLElement,
       instrument: InstrumentConfig,
       options: RenderOptions
     ): void;
     
     updatePositions(positions: FretboardPosition[]): void;
     updateCapo(fret: number): void;
     dispose(): void;
   }
   ```

2. **Implement Renderers**
   - `SVGRenderer` - Refactor from GuitarFretboard
   - `CanvasRenderer` - Refactor from RealisticFretboard
   - `WebGLRenderer` - Refactor from ThreeFretboard
   - `WebGPURenderer` - Refactor from WebGPUFretboard

### Phase 3: Generic Component (Week 3)

```typescript
export const StringedInstrumentFretboard: React.FC<StringedInstrumentFretboardProps> = ({
  instrument,
  renderMode,
  positions = [],
  capoFret = 0,
  leftHanded = false,
  ...options
}) => {
  const [renderer, setRenderer] = useState<FretboardRenderer | null>(null);
  const containerRef = useRef<HTMLDivElement>(null);
  
  useEffect(() => {
    if (!containerRef.current) return;
    
    // Create appropriate renderer
    const newRenderer = createRenderer(renderMode, instrument, options);
    newRenderer.render(containerRef.current, instrument, options);
    setRenderer(newRenderer);
    
    return () => newRenderer.dispose();
  }, [renderMode, instrument]);
  
  useEffect(() => {
    renderer?.updatePositions(positions);
  }, [positions, renderer]);
  
  useEffect(() => {
    renderer?.updateCapo(capoFret);
  }, [capoFret, renderer]);
  
  return (
    <Stack spacing={2}>
      <Typography variant="h6">
        {instrument.displayName}
      </Typography>
      
      <CommonControls
        instrument={instrument}
        capoFret={capoFret}
        leftHanded={leftHanded}
        onCapoChange={...}
        onLeftHandedChange={...}
      />
      
      <div ref={containerRef} />
    </Stack>
  );
};
```

### Phase 4: Migration & Testing (Week 4)

1. **Create Compatibility Wrappers**
   ```typescript
   // Backward compatibility
   export const ThreeFretboard = (props) => (
     <StringedInstrumentFretboard
       {...props}
       renderMode="3d-webgpu"
       instrument={guitarConfigFromProps(props)}
     />
   );
   ```

2. **Update Tests**
   - Test all instrument types
   - Test all render modes
   - Test feature parity

## 🎯 Benefits

### 1. **Maintainability**
- ✅ Single source of truth for fretboard logic
- ✅ Bug fixes apply to all instruments
- ✅ New features automatically available for all instruments

### 2. **Flexibility**
- ✅ Easy to add new instruments (just add YAML entry)
- ✅ Easy to add new tunings
- ✅ Easy to switch rendering modes

### 3. **Performance**
- ✅ Shared code = smaller bundle size
- ✅ Optimized rendering pipeline
- ✅ Lazy loading of renderers

### 4. **Developer Experience**
- ✅ Consistent API across all instruments
- ✅ TypeScript type safety
- ✅ Comprehensive documentation

## 📊 Comparison: Current vs. Proposed

| Aspect | Current | Proposed |
|--------|---------|----------|
| Components | 4 separate | 1 unified |
| Instruments | Guitar only | 60+ instruments |
| Code duplication | High | Minimal |
| Maintainability | Low | High |
| Bundle size | ~200KB | ~150KB (estimated) |
| Type safety | Partial | Complete |

## 🚀 Next Steps

1. ✅ **Approve architecture** - Review and approve this proposal
2. 📝 **Create detailed specs** - Define all interfaces and types
3. 🔨 **Implement Phase 1** - Core infrastructure
4. 🎨 **Implement Phase 2** - Unified renderers
5. 🧩 **Implement Phase 3** - Generic component
6. 🧪 **Implement Phase 4** - Migration and testing
7. 📚 **Documentation** - Update all docs and examples

## 💡 Future Enhancements

- **Instrument Presets**: Pre-configured popular instruments
- **Custom Tunings**: User-defined tunings
- **Tablature Support**: Render tablature notation
- **Audio Playback**: Play notes when clicked
- **MIDI Integration**: Connect to MIDI devices
- **Chord Library**: Built-in chord diagrams
- **Scale Visualization**: Show scales on fretboard

## 📝 Migration Guide

### Step 1: Install Dependencies (if needed)

```bash
# If using a YAML parser library (recommended for production)
npm install js-yaml
npm install -D @types/js-yaml
```

### Step 2: Copy Instruments.yaml to Public Directory

```bash
# Create config directory in public folder
mkdir -p public/config

# Copy Instruments.yaml
cp ../../Common/GA.Business.Config/Instruments.yaml public/config/
```

### Step 3: Update Existing Components (Gradual Migration)

#### Option A: Keep Existing Components, Add Generic Component

```typescript
// Old code still works
import { ThreeFretboard } from './components/ThreeFretboard';
import { RealisticFretboard } from './components/RealisticFretboard';

// New code uses generic component
import { StringedInstrumentFretboard } from './components/StringedInstrumentFretboard';
import { PRESET_INSTRUMENTS } from './utils/instrumentLoader';

// Use old component
<ThreeFretboard guitarStyle={{ category: 'classical' }} />

// Use new component with same result
<StringedInstrumentFretboard
  instrument={PRESET_INSTRUMENTS.standardGuitar()}
  renderMode="3d-webgpu"
/>
```

#### Option B: Replace Components with Compatibility Wrappers

```typescript
// src/components/ThreeFretboard.tsx (new version)
export { ThreeFretboardCompat as ThreeFretboard } from './StringedInstrumentFretboard';

// src/components/RealisticFretboard.tsx (new version)
export { RealisticFretboardCompat as RealisticFretboard } from './StringedInstrumentFretboard';
```

### Step 4: Update Tests

```typescript
// tests/fretboard.spec.ts
import { test, expect } from '@playwright/test';

test('Generic fretboard - Guitar', async ({ page }) => {
  await page.goto('/test/generic-fretboard?instrument=Guitar&variant=Standard');

  const canvas = page.locator('canvas');
  await canvas.waitFor({ state: 'visible' });

  // Test capo
  const capoSelect = page.getByLabel('Capo Position');
  await capoSelect.click();
  await page.getByRole('option', { name: 'Fret 3' }).click();

  await expect(capoSelect).toContainText('Fret 3');
});

test('Generic fretboard - Bass', async ({ page }) => {
  await page.goto('/test/generic-fretboard?instrument=BassGuitar&variant=Standard');

  // Verify 4 strings
  const stringChips = page.locator('text=4 strings');
  await expect(stringChips).toBeVisible();
});

test('Generic fretboard - Ukulele', async ({ page }) => {
  await page.goto('/test/generic-fretboard?instrument=Ukulele&variant=SopranoConcertAndTenorC');

  // Verify 4 strings, 12 frets
  await expect(page.locator('text=4 strings')).toBeVisible();
  await expect(page.locator('text=12 frets')).toBeVisible();
});
```

### Step 5: Create Test Page for Generic Component

```typescript
// src/pages/GenericFretboardTest.tsx
import React, { useState, useEffect } from 'react';
import { useSearchParams } from 'react-router-dom';
import { StringedInstrumentFretboard } from '../components/StringedInstrumentFretboard';
import { getInstrument } from '../utils/instrumentLoader';
import type { InstrumentConfig } from '../types/InstrumentConfig';

export const GenericFretboardTest: React.FC = () => {
  const [searchParams] = useSearchParams();
  const [instrument, setInstrument] = useState<InstrumentConfig | null>(null);

  useEffect(() => {
    const family = searchParams.get('instrument') || 'Guitar';
    const variant = searchParams.get('variant') || 'Standard';

    getInstrument(family, variant).then(setInstrument);
  }, [searchParams]);

  if (!instrument) {
    return <div>Loading...</div>;
  }

  return (
    <StringedInstrumentFretboard
      instrument={instrument}
      renderMode="3d-webgpu"
      showControls={true}
    />
  );
};
```

## 🔧 Implementation Checklist

### Phase 1: Core Infrastructure ✅
- [x] Create `InstrumentConfig.ts` type definitions
- [x] Create `fretboardMath.ts` utilities
- [x] Create `instrumentLoader.ts` YAML parser
- [x] Create example configurations
- [ ] Copy Instruments.yaml to public directory
- [ ] Test YAML loading in browser

### Phase 2: Unified Renderer 🚧
- [ ] Create `FretboardRenderer` interface
- [ ] Implement `SVGRenderer` (refactor from GuitarFretboard)
- [ ] Implement `CanvasRenderer` (refactor from RealisticFretboard)
- [ ] Implement `WebGLRenderer` (refactor from ThreeFretboard)
- [ ] Implement `WebGPURenderer` (refactor from ThreeFretboard)
- [ ] Add renderer factory function
- [ ] Test all renderers with different instruments

### Phase 3: Generic Component 🚧
- [x] Create `StringedInstrumentFretboard.tsx` component
- [x] Create compatibility wrappers
- [ ] Implement actual rendering (currently placeholder)
- [ ] Add position markers
- [ ] Add capo rendering
- [ ] Add left-handed mode
- [ ] Test with all instrument types

### Phase 4: Migration & Testing 📋
- [ ] Create test page for generic component
- [ ] Update existing tests
- [ ] Add new tests for different instruments
- [ ] Update documentation
- [ ] Create migration guide for users
- [ ] Performance testing
- [ ] Bundle size analysis

### Phase 5: Production Deployment 🚀
- [ ] Code review
- [ ] Performance optimization
- [ ] Accessibility audit
- [ ] Browser compatibility testing
- [ ] Deploy to staging
- [ ] User acceptance testing
- [ ] Deploy to production

---

**Status**: 🟡 Proposal - Awaiting Approval
**Author**: AI Assistant
**Date**: 2025-01-20
**Last Updated**: 2025-01-20

