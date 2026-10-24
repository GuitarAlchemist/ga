import React, { useState, useEffect, useLayoutEffect, useMemo, useRef } from 'react';
import { Box } from '@mui/material';
import NotesSelector from "./NotesSelector";
import {BraceletNotation, KeyboardDiagram} from "./index.ts";

interface MusicNotationDisplayProps {
    onNotesChange: (notes: string[]) => void;
}

const ScaleSelector: React.FC<MusicNotationDisplayProps> = ({ onNotesChange }) => {
    const [selectedNotes, setSelectedNotes] = useState<string[]>([]);
    // Derived from the notes, so it is computed rather than stored.
    const scale = useMemo(() => calculateScale(selectedNotes), [selectedNotes]);

    // Report when the notes change, not when the parent passes a new handler.
    const onNotesChangeRef = useRef(onNotesChange);
    useLayoutEffect(() => {
        onNotesChangeRef.current = onNotesChange;
    }, [onNotesChange]);

    useEffect(() => {
        onNotesChangeRef.current(selectedNotes);
    }, [selectedNotes]);

    // Unused function - kept for future use
    // const generateVexTabNotation = (notes: string[]): string => {
    //     return notes.map((_, index) => `${6 - index}/2`).join(' ');
    // };

    return (
        <Box>
            <NotesSelector onNotesChange={setSelectedNotes} />

            <Box sx={{ display: 'flex', justifyContent: 'center', gap: 2, mt: 2 }}>
                <Box>
                    <BraceletNotation scale={scale} size={175}/>
                </Box>
                <Box sx={{ ml: 'auto' }}>
                    <KeyboardDiagram scale={scale} />
                </Box>
            </Box>
        </Box>
    );
};

function calculateScale(notes: string[]): number {
    const allNotes = ['C', 'C#', 'D', 'D#', 'E', 'F', 'F#', 'G', 'G#', 'A', 'A#', 'B'];
    let scaleNumber = 0;
    notes.forEach(note => {
        const index = allNotes.indexOf(note);
        if (index !== -1) {
            scaleNumber |= (1 << index);
        }
    });
    return scaleNumber;
}

export default ScaleSelector;
