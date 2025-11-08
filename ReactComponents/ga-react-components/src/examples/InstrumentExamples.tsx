/**
 * Examples of using the generic StringedInstrumentFretboard component
 * with different instruments from the Instruments.yaml file
 */

import React from 'react';
import { Stack, Typography, Divider } from '@mui/material';
import { StringedInstrumentFretboard } from '../components/StringedInstrumentFretboard';
import type { InstrumentConfig, FretboardPosition } from '../types/InstrumentConfig';

/**
 * Example: Standard Guitar (6 strings)
 */
export const StandardGuitarExample: React.FC = () => {
  const instrument: InstrumentConfig = {
    family: 'Guitar',
    variant: 'Standard',
    displayName: 'Standard Guitar',
    tuning: ['E2', 'A2', 'D3', 'G3', 'B3', 'E4'],
    scaleLength: 650,
    nutWidth: 52,
    bridgeWidth: 70,
    fretCount: 19,
    bodyStyle: 'classical',
  };

  // C Major chord
  const cMajorChord: FretboardPosition[] = [
    { string: 0, fret: 0 },  // E (open)
    { string: 1, fret: 3 },  // C
    { string: 2, fret: 2 },  // E
    { string: 3, fret: 0 },  // G (open)
    { string: 4, fret: 1 },  // C
  ];

  return (
    <StringedInstrumentFretboard
      instrument={instrument}
      renderMode="3d-webgpu"
      positions={cMajorChord}
      title="Standard Guitar - C Major Chord"
    />
  );
};

/**
 * Example: Bass Guitar (4 strings)
 */
export const BassGuitarExample: React.FC = () => {
  const instrument: InstrumentConfig = {
    family: 'BassGuitar',
    variant: 'Standard',
    displayName: 'Standard Bass',
    tuning: ['E1', 'A1', 'D2', 'G2'],
    scaleLength: 860,
    nutWidth: 45,
    bridgeWidth: 60,
    fretCount: 24,
    bodyStyle: 'bass',
  };

  // Walking bass line
  const bassLine: FretboardPosition[] = [
    { string: 0, fret: 0, label: 'E' },
    { string: 0, fret: 3, label: 'G' },
    { string: 1, fret: 0, label: 'A' },
    { string: 1, fret: 2, label: 'B' },
  ];

  return (
    <StringedInstrumentFretboard
      instrument={instrument}
      renderMode="2d-canvas"
      positions={bassLine}
      title="4-String Bass - Walking Bass Line"
    />
  );
};

/**
 * Example: 5-String Bass
 */
export const FiveStringBassExample: React.FC = () => {
  const instrument: InstrumentConfig = {
    family: 'BassGuitar',
    variant: 'FiveStrings',
    displayName: '5-String Bass',
    fullName: '5 strings',
    tuning: ['B0', 'E1', 'A1', 'D2', 'G2'],
    scaleLength: 860,
    nutWidth: 48,
    bridgeWidth: 65,
    fretCount: 24,
    bodyStyle: 'bass',
  };

  return (
    <StringedInstrumentFretboard
      instrument={instrument}
      renderMode="3d-webgl"
      title="5-String Bass"
    />
  );
};

/**
 * Example: Ukulele (4 strings)
 */
export const UkuleleExample: React.FC = () => {
  const instrument: InstrumentConfig = {
    family: 'Ukulele',
    variant: 'SopranoConcertAndTenorC',
    displayName: 'Soprano Ukulele',
    fullName: 'Soprano C, Concert & Tenor C',
    tuning: ['G4', 'C4', 'E4', 'A4'],
    scaleLength: 330,
    nutWidth: 35,
    bridgeWidth: 40,
    fretCount: 12,
    bodyStyle: 'ukulele',
    hasRosette: true,
  };

  // C chord on ukulele
  const cChord: FretboardPosition[] = [
    { string: 0, fret: 0 },  // G (open)
    { string: 1, fret: 0 },  // C (open)
    { string: 2, fret: 0 },  // E (open)
    { string: 3, fret: 3 },  // C
  ];

  return (
    <StringedInstrumentFretboard
      instrument={instrument}
      renderMode="2d-svg"
      positions={cChord}
      title="Soprano Ukulele - C Chord"
    />
  );
};

/**
 * Example: Banjo (5 strings with drone)
 */
export const BanjoExample: React.FC = () => {
  const instrument: InstrumentConfig = {
    family: 'Banjo',
    variant: 'Bluegrass5Strings',
    displayName: 'Bluegrass Banjo',
    fullName: '5 strings Bluegrass',
    tuning: ['G4', 'D3', 'G3', 'B3', 'D4'],
    scaleLength: 660,
    nutWidth: 32,
    bridgeWidth: 35,
    fretCount: 22,
    bodyStyle: 'banjo',
    hasDroneString: true,
    droneStringPosition: 0.2,
  };

  // G chord
  const gChord: FretboardPosition[] = [
    { string: 0, fret: 0 },  // G (drone, open)
    { string: 1, fret: 0 },  // D (open)
    { string: 2, fret: 0 },  // G (open)
    { string: 3, fret: 0 },  // B (open)
    { string: 4, fret: 0 },  // D (open)
  ];

  return (
    <StringedInstrumentFretboard
      instrument={instrument}
      renderMode="3d-webgpu"
      positions={gChord}
      title="5-String Bluegrass Banjo - G Chord"
    />
  );
};

/**
 * Example: Mandolin (8 strings, 4 courses)
 */
export const MandolinExample: React.FC = () => {
  const instrument: InstrumentConfig = {
    family: 'Mandolin',
    variant: 'Standard',
    displayName: 'Standard Mandolin',
    tuning: ['G3', 'G3', 'D4', 'D4', 'A4', 'A4', 'E5', 'E5'],
    scaleLength: 350,
    nutWidth: 28,
    bridgeWidth: 32,
    fretCount: 20,
    bodyStyle: 'mandolin',
  };

  return (
    <StringedInstrumentFretboard
      instrument={instrument}
      renderMode="2d-canvas"
      title="Standard Mandolin (8 strings, 4 courses)"
    />
  );
};

/**
 * Example: 12-String Guitar
 */
export const TwelveStringGuitarExample: React.FC = () => {
  const instrument: InstrumentConfig = {
    family: 'Guitar',
    variant: 'TwelveStrings',
    displayName: '12-String Guitar',
    fullName: '12-String',
    tuning: ['E2', 'E3', 'A2', 'A3', 'D3', 'D4', 'G3', 'G4', 'B3', 'B3', 'E4', 'E4'],
    scaleLength: 650,
    nutWidth: 60,
    bridgeWidth: 80,
    fretCount: 19,
    bodyStyle: 'acoustic',
    hasPickguard: true,
  };

  return (
    <StringedInstrumentFretboard
      instrument={instrument}
      renderMode="3d-webgl"
      title="12-String Acoustic Guitar"
    />
  );
};

/**
 * Example: Baritone Guitar
 */
export const BaritoneGuitarExample: React.FC = () => {
  const instrument: InstrumentConfig = {
    family: 'BaritoneGuitar',
    variant: 'Standard1',
    displayName: 'Baritone Guitar',
    tuning: ['B1', 'E2', 'A2', 'D3', 'F#3', 'B3'],
    scaleLength: 686,
    nutWidth: 48,
    bridgeWidth: 68,
    fretCount: 22,
    bodyStyle: 'electric',
  };

  return (
    <StringedInstrumentFretboard
      instrument={instrument}
      renderMode="3d-webgpu"
      title='Baritone Guitar (27" scale)'
    />
  );
};

/**
 * Example: Russian Guitar (7 strings)
 */
export const RussianGuitarExample: React.FC = () => {
  const instrument: InstrumentConfig = {
    family: 'RussianGuitar',
    variant: 'Standard',
    displayName: 'Russian Guitar',
    tuning: ['D2', 'G2', 'B2', 'D3', 'G3', 'B3', 'D4'],
    scaleLength: 650,
    nutWidth: 55,
    bridgeWidth: 75,
    fretCount: 19,
    bodyStyle: 'classical',
  };

  return (
    <StringedInstrumentFretboard
      instrument={instrument}
      renderMode="2d-canvas"
      title="Russian 7-String Guitar"
    />
  );
};

/**
 * Demo page showing all instrument examples
 */
export const AllInstrumentsDemo: React.FC = () => {
  return (
    <Stack spacing={4} sx={{ p: 3 }}>
      <Typography variant="h3">
        Generic Fretboard Component - Instrument Examples
      </Typography>
      
      <Typography variant="body1" color="text.secondary">
        This page demonstrates the generic StringedInstrumentFretboard component
        rendering different instruments from the Instruments.yaml configuration.
      </Typography>

      <Divider />

      <StandardGuitarExample />
      <Divider />

      <BassGuitarExample />
      <Divider />

      <FiveStringBassExample />
      <Divider />

      <UkuleleExample />
      <Divider />

      <BanjoExample />
      <Divider />

      <MandolinExample />
      <Divider />

      <TwelveStringGuitarExample />
      <Divider />

      <BaritoneGuitarExample />
      <Divider />

      <RussianGuitarExample />
    </Stack>
  );
};

export default AllInstrumentsDemo;

