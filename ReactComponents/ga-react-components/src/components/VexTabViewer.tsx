import React, { useRef, useEffect } from 'react';
import Vex from 'vexflow';

interface VexTabDisplayProps {
    notation: string;
    showStandardNotation?: boolean;
}

const VexTabViewer: React.FC<VexTabDisplayProps> = ({ notation, showStandardNotation = false }) => {
    const divRef = useRef<HTMLDivElement>(null);

    useEffect(() => {
        if (divRef.current) {
            const VF = Vex.Flow;
            divRef.current.innerHTML = ''; // Clear previous content
            const renderer = new VF.Renderer(divRef.current, VF.Renderer.Backends.SVG);

            // Configure the rendering context
            const width = 500;
            const height = showStandardNotation ? 250 : 150;
            renderer.resize(width, height);
            const context = renderer.getContext();
            context.setFont("Arial", 10, "").setBackgroundFillStyle("#eed");

            // Create a tab stave
            const tabStave = new VF.TabStave(10, showStandardNotation ? 100 : 40, 450);
            tabStave.addClef("tab").setContext(context).draw();

            // Create a standard notation stave if needed
            let stave;
            if (showStandardNotation) {
                stave = new VF.Stave(10, 0, 450);
                stave.addClef("treble").setContext(context).draw();
            }

            // Parse the notation string
            const notes = notation.split(' ').map(noteString => {
                const [str, fret] = noteString.split('/');
                const tabNote = new VF.TabNote({
                    positions: [{ str: parseInt(str), fret: parseInt(fret) }],
                    duration: 'q',
                });

                if (showStandardNotation) {
                    // Convert tab position to pitch
                    const pitch = getPitchFromTabPosition(parseInt(str), parseInt(fret));
                    const staveNote = new VF.StaveNote({ keys: [pitch], duration: 'q' });
                    return { tabNote, staveNote };
                }

                return { tabNote };
            });

            // Create voices and add notes
            const tabVoice = new VF.Voice({ num_beats: notes.length, beat_value: 4 });
            tabVoice.addTickables(notes.map(n => n.tabNote));

            let staveVoice;
            if (showStandardNotation) {
                staveVoice = new VF.Voice({ num_beats: notes.length, beat_value: 4 });
                staveVoice.addTickables(notes.map(n => n.staveNote!));
            }

            // Format and justify the notes
            const formatter = new VF.Formatter();
            formatter.joinVoices([tabVoice]);
            if (showStandardNotation && staveVoice) {
                formatter.joinVoices([staveVoice]);
            }
            formatter.format([tabVoice, ...(staveVoice ? [staveVoice] : [])], 400);

            // Draw the voices
            tabVoice.draw(context, tabStave);
            if (showStandardNotation && stave && staveVoice) {
                staveVoice.draw(context, stave);
            }
        }
    }, [notation, showStandardNotation]);

    return <div ref={divRef}></div>;
};

// Helper function to convert tab position to pitch (simplified)
function getPitchFromTabPosition(string: number, fret: number): string {
    // Standard guitar tuning (low E to high E)
    // String numbering: 1 = high E, 6 = low E (standard guitar notation)
    const openStrings = ['E/3', 'A/3', 'D/4', 'G/4', 'B/4', 'E/5'];

    // Validate string number (1-6)
    const stringIndex = 6 - string;
    if (stringIndex < 0 || stringIndex >= openStrings.length) {
        console.warn(`Invalid string number: ${string}. Expected 1-6.`);
        return 'E/3'; // Default to low E
    }

    const openString = openStrings[stringIndex];
    if (!openString) {
        console.warn(`No open string found at index ${stringIndex}`);
        return 'E/3'; // Default to low E
    }

    const [note, octave] = openString.split('/');
    const notes = ['C', 'C#', 'D', 'D#', 'E', 'F', 'F#', 'G', 'G#', 'A', 'A#', 'B'];
    const noteIndex = notes.indexOf(note);
    const newNoteIndex = (noteIndex + fret) % 12;
    const newOctave = parseInt(octave) + Math.floor((noteIndex + fret) / 12);
    return `${notes[newNoteIndex]}/${newOctave}`;
}

export default VexTabViewer;
