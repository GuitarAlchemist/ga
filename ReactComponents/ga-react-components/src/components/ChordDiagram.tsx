import React, { useEffect, useRef } from 'react';
import * as Vex from 'vexchords';

export interface Barre {
    fromString: number;
    toString: number;
    fret: number;
}

export type ChordNote = [number, number | 'x', string?];

export interface ChordData {
    chordNotes: ChordNote[];
    position?: number;
    barres?: Barre[];
}

export interface ChordProps {
    chord: ChordData;
    width?: number;
    height?: number;
}

const ChordDiagram: React.FC<ChordProps> = ({ chord, width = 90, height = 110 }) => {
    const chordRef = useRef<HTMLDivElement>(null);

    useEffect(() => {
        if (!chordRef.current) return;

        // Validate chord data before rendering
        if (!isValidChordData(chord)) {
            console.error('Invalid chord data:',JSON.stringify(chord));
            chordRef.current.innerHTML = 'Invalid chord data';
            return;
        }

        // Clear previous renderings
        chordRef.current.innerHTML = '';

        try {
            // Render the new chord
            const chordBox = new Vex.ChordBox(chordRef.current, {
                width,
                height,
            });

            chordBox.draw({
                chord: chord.chordNotes.map(([string, fret, label]) => [string, fret]),
                position: chord.position ?? 1,
                barres: chord.barres ?? [],
            });
        } catch (error) {
            console.error('Error rendering chord:', error);
            chordRef.current.innerHTML = 'Error rendering chord';
        }
    }, [chord, width, height]);

    return <div ref={chordRef}></div>;
};

// Utility function to validate chord data
function isValidChordData(chord: ChordData): boolean {
    if (!chord || !Array.isArray(chord.chordNotes)) return false;

    return chord.chordNotes.every(
        ([string, fret, label]) =>
            typeof string === 'number' &&
            (typeof fret === 'number' || fret === 'x') &&
            (label === undefined || typeof label === 'string')
    ) && (
        !chord.barres || chord.barres.every(
            barre =>
                barre.fromString > 0 &&
                barre.toString > 0 &&
                barre.fromString <= 6 &&
                barre.toString <= 6 &&
                barre.fret > 0
        )
    );
}

export default ChordDiagram;