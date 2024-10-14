import React, { useState, useEffect } from 'react';
import { TextInput, Checkbox, Group, Button } from '@mantine/core';

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
        <>
            <Group mb="md">
                <Checkbox
                    label="Use text input"
                    checked={useTextInput}
                    onChange={(event) => setUseTextInput(event.currentTarget.checked)}
                />
            </Group>

            {useTextInput ? (
                <TextInput
                    placeholder="Enter notes (e.g., C E G)"
                    value={textNotes}
                    onChange={handleTextChange}
                    mb="md"
                />
            ) : (
                <Group mb="md">
                    {allNotes.map(note => (
                        <Button
                            key={note}
                            variant={toggledNotes.includes(note) ? 'filled' : 'outline'}
                            onClick={() => handleToggle(note)}
                        >
                            {note}
                        </Button>
                    ))}
                </Group>
            )}
        </>
    );
};

export default NotesSelector;