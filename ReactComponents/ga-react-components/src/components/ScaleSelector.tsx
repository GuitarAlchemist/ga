// @ts-nocheck
import React, { useState, useEffect } from 'react';
import { Box } from '@mui/material';
import NotesSelector from "./NotesSelector";
import {BraceletNotation, KeyboardDiagram} from "./index.ts";

interface MusicNotationDisplayProps {
    onNotesChange: (notes: string[]) => void;
}

const ScaleSelector: React.FC<MusicNotationDisplayProps> = ({ onNotesChange }) => {
    const [selectedNotes, setSelectedNotes] = useState<string[]>([]);
    const [scale, setScale] = useState(0);

    useEffect(() => {
        onNotesChange(selectedNotes);
    }, [selectedNotes, onNotesChange]);

    const handleNotesChange = (notes: string[]) => {
        setSelectedNotes(notes);
        setScale(calculateScale(notes));
    };

    const calculateScale = (notes: string[]): number => {
        const allNotes = ['C', 'C#', 'D', 'D#', 'E', 'F', 'F#', 'G', 'G#', 'A', 'A#', 'B'];
        let scaleNumber = 0;
        notes.forEach(note => {
            const index = allNotes.indexOf(note);
            if (index !== -1) {
                scaleNumber |= (1 << index);
            }
        });
        return scaleNumber;
    };

    // Unused function - kept for future use
    // const generateVexTabNotation = (notes: string[]): string => {
    //     return notes.map((_, index) => `${6 - index}/2`).join(' ');
    // };

    return (
        <Box>
            <NotesSelector onNotesChange={handleNotesChange} />

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

export default ScaleSelector;
