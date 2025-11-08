// @ts-nocheck
import React, { useState, useEffect } from 'react';
import { TextField, FormControlLabel, Checkbox, Box, Button } from '@mui/material';

interface NoteSelectorProps {
    onNotesChange: (notes: string[]) => void;
}

const allNotes = ['C', 'C#', 'D', 'D#', 'E', 'F', 'F#', 'G', 'G#', 'A', 'A#', 'B'];

const NotesSelector: React.FC<NoteSelectorProps> = ({ onNotesChange }) => {
    const [textNotes, setTextNotes] = useState('');
    const [toggledNotes, setToggledNotes] = useState<string[]>([]);
    const [useTextInput, setUseTextInput] = useState(true);

    useEffect(() => {
        const notes = useTextInput ? textNotes.split(' ').filter(note => allNotes.includes(note)) : toggledNotes;
        onNotesChange(notes);
    }, [textNotes, toggledNotes, useTextInput, onNotesChange]);

    const handleTextChange = (event: React.ChangeEvent<HTMLInputElement>) => {
        setTextNotes(event.target.value);
    };

    const handleToggle = (note: string) => {
        setToggledNotes(prev =>
            prev.includes(note) ? prev.filter(n => n !== note) : [...prev, note]
        );
    };

    return (
        <Box>
            <Box sx={{ mb: 2 }}>
                <FormControlLabel
                    control={
                        <Checkbox
                            checked={useTextInput}
                            onChange={(event) => setUseTextInput(event.target.checked)}
                        />
                    }
                    label="Use text input"
                />
            </Box>

            {useTextInput ? (
                <TextField
                    fullWidth
                    placeholder="Enter notes (e.g., C E G)"
                    value={textNotes}
                    onChange={handleTextChange}
                    sx={{ mb: 2 }}
                />
            ) : (
                <Box sx={{ mb: 2, display: 'flex', gap: 1, flexWrap: 'wrap' }}>
                    {allNotes.map(note => (
                        <Button
                            key={note}
                            variant={toggledNotes.includes(note) ? 'contained' : 'outlined'}
                            onClick={() => handleToggle(note)}
                        >
                            {note}
                        </Button>
                    ))}
                </Box>
            )}
        </Box>
    );
};

export default NotesSelector;
