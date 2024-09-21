import React, { useEffect, useRef } from "react";
// @ts-ignore
import { ChordBox } from "vexchords";

import "./chord-diagram.scss";

const ChordDiagram = ({ label, notes }) => {
    const chordRef = useRef(null);

    useEffect(() => {
        const chord = new ChordBox(chordRef.current, {
            width: 200,
            height: 240,
            // See the docs for more available options.
            // https://github.com/0xfe/vexchords
        });

        chord.draw({
            chord: notes,
        });
    });

    return (
        <div className="chord-diagram">
            <h3>{label}</h3>
            <div ref={chordRef}></div>
        </div>
    );
};

export default ChordDiagram;
