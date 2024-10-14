import React from 'react';

interface KeyboardDiagramProps {
    scale: number;
    width?: number;
}

const KeyboardDiagram: React.FC<KeyboardDiagramProps> = ({ scale, width = 350 }) => {
    const height = width * 0.4;
    const whiteKeyWidth = width / 10;
    const whiteKeyHeight = height;
    const blackKeyWidth = whiteKeyWidth * 0.65;
    const blackKeyHeight = height * 0.65;

    const scaleArray = scale.toString(2).padStart(12, '0').split('').reverse().map(Number);

    const whiteKeys = [0, 2, 4, 5, 7, 9, 11];
    const blackKeys = [1, 3, 6, 8, 10];

    const colors = [
        '#000080', '#800080', '#FF0000', '#FF4500', '#FFA500', '#FFFF00',
        '#9ACD32', '#008000', '#00FFFF', '#0000FF', '#4B0082', '#EE82EE'
    ];

    const getBlackKeyX = (index: number) => {
        const whiteKeyIndex = Math.floor(index / 2);
        return whiteKeyIndex * whiteKeyWidth + whiteKeyWidth * 0.7;
    };

    const Indicator = ({ x, y, size, color }: { x: number; y: number; size: number; color: string }) => (
        <g>
            <circle cx={x} cy={y} r={size} fill="green" />
            <rect x={x - size} y={y - size * 3} width={size * 2} height={size * 2} fill={color} />
        </g>
    );

    return (
        <svg width={width} height={height}>
            <rect x={0} y={0} width={width} height={height} fill="#ebebeb" />

            {/* White keys */}
            {whiteKeys.map((index, i) => (
                <g key={`white-${index}`}>
                    <rect
                        x={i * whiteKeyWidth}
                        y={0}
                        width={whiteKeyWidth}
                        height={whiteKeyHeight}
                        fill="white"
                        stroke="#555"
                        strokeWidth={0.5}
                    />
                    {scaleArray[index] && (
                        <Indicator
                            x={i * whiteKeyWidth + whiteKeyWidth / 2}
                            y={whiteKeyHeight - whiteKeyWidth / 4}
                            size={whiteKeyWidth / 10}
                            color={colors[index]}
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
                        stroke="#000"
                        strokeWidth={0.5}
                    />
                    {scaleArray[index] && (
                        <Indicator
                            x={getBlackKeyX(index) + blackKeyWidth / 2}
                            y={blackKeyHeight - blackKeyWidth / 4}
                            size={blackKeyWidth / 10}
                            color={colors[index]}
                        />
                    )}
                </g>
            ))}
        </svg>
    );
};

export default KeyboardDiagram;