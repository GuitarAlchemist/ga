import React, {useEffect, useRef} from 'react';
import {ChordBox as ChordBoxClass, ChordBoxParams, ChordData} from 'vexchords'; // Assuming the type is exported
import './chord-diagram.scss';

// Interface for the props of the ChordDiagram component
interface ChordDiagramProps {
    label: string;
    notes: Array<[number, number, string?]>;
}

const ChordDiagram: React.FC<ChordDiagramProps> = ({label, notes}) => {
    const chordRef = useRef<HTMLDivElement>(null);

    useEffect(() => {
        // Check if chordRef.current is not null
        if (chordRef.current) {
            const chordBoxParams: ChordBoxParams = {
                width: 200,
                height: 240,
                // Add more parameters as needed
            };

            const chord = new ChordBoxClass(chordRef.current, chordBoxParams);

            const chordData: ChordData = {
                chord: notes, // Assuming 'notes' is an array like [[1, 2, 'label'], [2, 3]]
            };

            chord.draw(chordData);
        }
    }, [notes]); // Depend on notes to redraw when they change

    return (
        <div className="chord-diagram">
            <h3>{label}</h3>
            <div ref={chordRef}></div>
        </div>
    );
};

export default React.memo(ChordDiagram);
