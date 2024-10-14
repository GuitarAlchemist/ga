import React from 'react';

interface GuitarFretboardProps {
    frets: number[];
    numFrets?: number;
    tuning?: string[];
}

const GuitarFretboard: React.FC<GuitarFretboardProps> = ({
                                                             frets,
                                                             numFrets = 12,
                                                             tuning = ['E', 'A', 'D', 'G', 'B', 'E']
                                                         }) => {
    const stringHeight = 30;
    const fretWidth = 50;
    const nutWidth = 10;
    const dotFrets = [3, 5, 7, 9, 12];

    return (
        <svg width={(numFrets + 1) * fretWidth + nutWidth} height={tuning.length * stringHeight}>
            {/* Fretboard */}
            <rect x={0} y={0} width={(numFrets + 1) * fretWidth + nutWidth} height={tuning.length * stringHeight} fill="#f2e6d9" />

            {/* Frets */}
            {Array.from({ length: numFrets + 1 }).map((_, i) => (
                <line
                    key={`fret-${i}`}
                    x1={i * fretWidth + nutWidth}
                    y1={0}
                    x2={i * fretWidth + nutWidth}
                    y2={tuning.length * stringHeight}
                    stroke="#8b4513"
                    strokeWidth={i === 0 ? 5 : 2}
                />
            ))}

            {/* Strings */}
            {tuning.map((_, i) => (
                <line
                    key={`string-${i}`}
                    x1={0}
                    y1={(i + 0.5) * stringHeight}
                    x2={(numFrets + 1) * fretWidth + nutWidth}
                    y2={(i + 0.5) * stringHeight}
                    stroke="#8b4513"
                    strokeWidth={1}
                />
            ))}

            {/* Fret dots */}
            {dotFrets.map(fret => (
                <circle
                    key={`dot-${fret}`}
                    cx={(fret - 0.5) * fretWidth + nutWidth}
                    cy={tuning.length * stringHeight / 2}
                    r={5}
                    fill="#8b4513"
                />
            ))}

            {/* Highlighted frets */}
            {frets.map((fret, stringIndex) => {
                if (fret >= 0) {
                    return (
                        <circle
                            key={`highlight-${stringIndex}`}
                            cx={(fret - 0.5) * fretWidth + nutWidth}
                            cy={(stringIndex + 0.5) * stringHeight}
                            r={10}
                            fill="#ff0000"
                        />
                    );
                }
                return null;
            })}

            {/* Tuning labels */}
            {tuning.map((note, i) => (
                <text
                    key={`tuning-${i}`}
                    x={5}
                    y={(i + 0.7) * stringHeight}
                    fontSize={14}
                    fill="#000000"
                >
                    {note}
                </text>
            ))}
        </svg>
    );
};

export default GuitarFretboard;