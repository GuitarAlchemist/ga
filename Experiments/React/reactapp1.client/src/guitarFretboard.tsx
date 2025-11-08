const GuitarFretboard = () => {
    // Constants for fretboard dimensions
    const FRET_COUNT = 24;
    const STRING_COUNT = 6;
    const WIDTH = 1200;
    const HEIGHT = 200;
    const MARGIN = 30;

    // Playable area dimensions (excluding margins)
    const playableWidth = WIDTH - (2 * MARGIN);
    const playableHeight = HEIGHT - (2 * MARGIN);

    // Calculate fret positions using the 18th root of 2 (real guitar proportions)
    const getFretPosition = (fretNumber: number) => {
        return MARGIN + (playableWidth * (1 - Math.pow(2, -fretNumber / 12)));
    };

    // Calculate string positions
    const getStringPosition = (stringNumber: number) => {
        const stringSpacing = playableHeight / (STRING_COUNT - 1);
        return MARGIN + (stringNumber * stringSpacing);
    };

    // Standard fret markers
    const singleDotFrets = [3, 5, 7, 9, 15, 17, 19, 21];
    const doubleDotFrets = [12, 24];

    return (
        <svg width={WIDTH} height={HEIGHT}>
            {/* Fretboard background */}
            <rect
                x={MARGIN}
                y={MARGIN}
                width={playableWidth}
                height={playableHeight}
                fill="#2b1810"
                stroke="black"
            />

            {/* Frets */}
            {Array.from({length: FRET_COUNT + 1}).map((_, i) => (
                <line
                    key={`fret-${i}`}
                    x1={getFretPosition(i)}
                    y1={MARGIN}
                    x2={getFretPosition(i)}
                    y2={HEIGHT - MARGIN}
                    stroke={i === 0 ? "#ECD08C" : "silver"}
                    strokeWidth={i === 0 ? 8 : 2}
                />
            ))}

            {/* Strings */}
            {Array.from({length: STRING_COUNT}).map((_, i) => (
                <line
                    key={`string-${i}`}
                    x1={MARGIN}
                    y1={getStringPosition(i)}
                    x2={WIDTH - MARGIN}
                    y2={getStringPosition(i)}
                    stroke="#DDD"
                    strokeWidth={3 - (i * 0.4)}
                />
            ))}

            {/* Fret markers (dots) */}
            {singleDotFrets.map(fret => (
                <circle
                    key={`dot-${fret}`}
                    cx={(getFretPosition(fret) + getFretPosition(fret - 1)) / 2}
                    cy={HEIGHT / 2}
                    r={8}
                    fill="#DDD"
                />
            ))}

            {/* Double dots at 12th and 24th frets */}
            {doubleDotFrets.map(fret => (
                <g key={`double-dot-${fret}`}>
                    <circle
                        cx={(getFretPosition(fret) + getFretPosition(fret - 1)) / 2}
                        cy={HEIGHT / 2 - 30}
                        r={8}
                        fill="#DDD"
                    />
                    <circle
                        cx={(getFretPosition(fret) + getFretPosition(fret - 1)) / 2}
                        cy={HEIGHT / 2 + 30}
                        r={8}
                        fill="#DDD"
                    />
                </g>
            ))}
        </svg>
    );
};

export default GuitarFretboard;
