import React, { useState, useEffect } from 'react';
import { Container } from '@mantine/core';
import NotesSelector from "./NotesSelector";
import {BraceletNotation, KeyboardDiagram} from "./index.ts";

interface MusicNotationDisplayProps {
    onNotesChange: (notes: string[]) => void;
}

const MusicNotationDisplay: React.FC<MusicNotationDisplayProps> = ({ onNotesChange }) => {
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

    // @ts-expect-error Not used
    const generateVexTabNotation = (notes: string[]): string => {
        return notes.map((_, index) => `${6 - index}/2`).join(' ');
    };

    return (
        <div>
            <NotesSelector onNotesChange={handleNotesChange} />

            <div style={{display: 'flex', justifyContent: 'center'}}>
                <div>
                    <BraceletNotation scale={scale} size={175}/>
                </div>
                <div style={{marginLeft: "auto"}}>
                    <KeyboardDiagram scale={scale} />
                </div>
            </div>
            <Container size="sm">

            </Container>
        </div>
    );
};

export default MusicNotationDisplay;