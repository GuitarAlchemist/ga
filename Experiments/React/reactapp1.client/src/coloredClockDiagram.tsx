import React from 'react';

interface ColoredClockDiagramProps {
    scale: number;
    size?: number;
}

const ColoredClockDiagram: React.FC<ColoredClockDiagramProps> = ({ scale, size = 200 }) => {
    const scaleArray = scale.toString(2).padStart(12, '0').split('').reverse().map(Number);
    const center = size / 2;
    const radius = size * 0.4;
    const dotRadius = size * 0.05;

    const notes = ['C', 'C♯', 'D', 'D♯', 'E', 'F', 'F♯', 'G', 'G♯', 'A', 'A♯', 'B'];
    const colors = [
        '#000080', // Navy (C)
        '#800080', // Purple (C♯/D♭)
        '#FF0000', // Red (D)
        '#FF4500', // OrangeRed (D♯/E♭)
        '#FFA500', // Orange (E)
        '#FFFF00', // Yellow (F)
        '#9ACD32', // YellowGreen (F♯/G♭)
        '#008000', // Green (G)
        '#00FFFF', // Cyan (G♯/A♭)
        '#0000FF', // Blue (A)
        '#4B0082', // Indigo (A♯/B♭)
        '#EE82EE', // Violet (B)
    ];

    const getDotPosition = (index: number) => {
        const angle = (index * 30 - 90) * (Math.PI / 180);
        const x = center + radius * Math.cos(angle);
        const y = center + radius * Math.sin(angle);
        return { x, y };
    };

    return (
        <svg width={size} height={size} viewBox={`0 0 ${size} ${size}`}>
            {/* Clock circle */}
            <circle cx={center} cy={center} r={radius} fill="none" stroke="#333" strokeWidth={2} />

            {/* Hour marks and note names */}
            {notes.map((note, i) => {
                const { x, y } = getDotPosition(i);
                const textAngle = i * 30 - 90;
                const textRadius = radius + dotRadius * 2;
                const tx = center + textRadius * Math.cos(textAngle * Math.PI / 180);
                const ty = center + textRadius * Math.sin(textAngle * Math.PI / 180);

                return (
                    <g key={i}>
                        <circle
                            cx={x}
                            cy={y}
                            r={dotRadius}
                            fill={scaleArray[i] ? colors[i] : 'white'}
                            stroke="#333"
                            strokeWidth={1}
                        />
                        <text
                            x={tx}
                            y={ty}
                            textAnchor="middle"
                            dominantBaseline="middle"
                            fontSize={size * 0.05}
                            transform={`rotate(${textAngle + 90}, ${tx}, ${ty})`}
                        >
                            {note}
                        </text>
                    </g>
                );
            })}
        </svg>
    );
};

export default ColoredClockDiagram;