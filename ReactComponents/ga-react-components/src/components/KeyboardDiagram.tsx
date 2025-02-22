import React from 'react';

interface KeyboardDiagramProps {
    scale: number;
    width?: number;
}

const KeyboardDiagram: React.FC<KeyboardDiagramProps> = ({ scale, width = 200 }) => {
    const whiteKeyWidth = width / 7; // 7 white keys in an octave
    const height = whiteKeyWidth * 4;
    const blackKeyWidth = whiteKeyWidth * 0.65;
    const blackKeyHeight = height * 0.65;

    const scaleArray = scale.toString(2).padStart(12, '0').split('').reverse().map(Number);
    const whiteKeys = [0, 2, 4, 5, 7, 9, 11];
    const blackKeys = [1, 3, 6, 8, 10];

    const getBlackKeyX = (index: number) => {
        const whiteKeyIndex = Math.floor(index / 2);
        return whiteKeyIndex * whiteKeyWidth + whiteKeyWidth * 0.7;
    };

    const playedKeyColor = "#1E90FF"; // Dodger Blue
    const starSize = whiteKeyWidth * 0.24; // Size of the star

    const Star = ({ x, y, size, fill }: { x: number; y: number; size: number; fill: string }) => (
        <path
            d="M0,-1 L0.588,0.809 -0.951,-0.309 0.951,-0.309 -0.588,0.809Z"
            transform={`translate(${x},${y}) scale(${size})`}
            fill={fill}
        />
    );

    return (
        <svg width={whiteKeyWidth * 7} height={height}>
            {/* White keys */}
            {whiteKeys.map((index, i) => (
                <g key={`white-${index}`}>
                    <rect
                        x={i * whiteKeyWidth}
                        y={0}
                        width={whiteKeyWidth}
                        height={height}
                        fill="white"
                        stroke={scaleArray[index] ? playedKeyColor : "#555"}
                        strokeWidth={scaleArray[index] ? 2 : 0.5}
                    />
                    {scaleArray[index] && (
                        <Star
                            x={i * whiteKeyWidth + whiteKeyWidth / 2}
                            y={height - starSize}
                            size={starSize}
                            fill={playedKeyColor}
                        />
                    )}
                </g>
            ))}

            {/* Black keys */}
            {blackKeys.map((index) => (
                <g key={`black-${index}`}>
                    <rect
                        x={getBlackKeyX(index)}
                        y={0}
                        width={blackKeyWidth}
                        height={blackKeyHeight}
                        fill="#333"
                        stroke={scaleArray[index] ? playedKeyColor : "#000"}
                        strokeWidth={scaleArray[index] ? 2 : 0.5}
                    />
                    {scaleArray[index] && (
                        <Star
                            x={getBlackKeyX(index) + blackKeyWidth / 2}
                            y={blackKeyHeight - starSize}
                            size={starSize}
                            fill={playedKeyColor}
                        />
                    )}
                </g>
            ))}
        </svg>
    );
};

export default KeyboardDiagram;