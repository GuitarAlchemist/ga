import React, {useEffect, useRef} from 'react';
import * as Vex from 'vexchords';
import {ChordData} from "./ChordData.tsx";
import {ChordProps} from "./ChordProps.tsx";

const VexChordDiagram: React.FC<ChordProps> = ({ chord, width = 90, height = 100 }) => {
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

            const chordDefinition: Vex.ChordDefinition = {
                chord: chord.chordNotes,
                position: chord.position,
                barres: chord.barres,
            };
            chordBox.draw(chordDefinition);
        } catch (error) {
            console.error('Error rendering chord:', error);
            chordRef.current.innerHTML = 'Error rendering chord';
        }
    }, [chord, width, height]);

    return <div ref={chordRef}></div>;
};

function isValidChordData(chord: ChordData): boolean {
    if (!chord || !Array.isArray(chord.chordNotes)) return false;

    return !chord.barres || chord.barres.every(
            barre =>
         barre.fromString > 0 &&
                barre.toString > 0 &&
                barre.fromString <= 6 &&
                barre.toString <= 6 &&
                barre.fret > 0
    );
}

export default VexChordDiagram;