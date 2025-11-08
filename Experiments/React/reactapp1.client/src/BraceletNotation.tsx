import React from 'react';

interface IBraceletNotationProps {
    scale: number;
    size?: number;
}

const braceletNotation: React.FC<IBraceletNotationProps> = ({scale, size = 200}) => {
    const margin = size * 0.2;
    const effectiveSize = size - 2 * margin;
    const radius = effectiveSize * 0.5;
    const center = size / 2;
    const noteRadius = effectiveSize * 0.08;
    const lineWidth = effectiveSize * 0.005;
    const labelRadius = radius + noteRadius * 3; // Increased label distance

    const scaleArray = scale.toString(2).padStart(12, '0').split('').reverse().map(Number);
    const pitchClasses = ['0', '1', '2', '3', '4', '5', '6', '7', '8', '9', 'T', 'E'];

    const getNotePosition = (index: number, r: number = radius): { x: number; y: number } => {
        const angle = (index * 30 - 90) * (Math.PI / 180); // Adjust to start at top
        const x = center + r * Math.cos(angle);
        const y = center + r * Math.sin(angle);
        return {x, y};
    };

    const findSymmetryAxes = (): number[] => {
        const axes: number[] = [];
        for (let i = 0; i < 12; i++) {
            let isSymmetric = true;
            for (let j = 0; j < 6; j++) {
                if (scaleArray[(i + j) % 12] !== scaleArray[(i - j + 12) % 12]) {
                    isSymmetric = false;
                    break;
                }
            }
            if (isSymmetric) axes.push(i);
        }
        return axes;
    };

    const symmetryAxes = findSymmetryAxes();

    return (
        <div>
            Hello
            <svg width={size} height={size} viewBox={`0 0 ${size} ${size}`}>
                <circle
                    cx={center}
                    cy={center}
                    r={radius}
                    fill="none"
                    stroke="#333"
                    strokeWidth={lineWidth}
                />
                {symmetryAxes.map((axis) => {
                    const start = getNotePosition(axis);
                    const end = getNotePosition((axis + 6) % 12);
                    return (
                        <line
                            key={`symmetry-${axis}`}
                            x1={start.x}
                            y1={start.y}
                            x2={end.x}
                            y2={end.y}
                            stroke="#333"
                            strokeWidth={lineWidth}
                            strokeDasharray={`${noteRadius * 0.5},${noteRadius * 0.5}`}
                        />
                    );
                })}
                {scaleArray.map((note, index) => {
                    const {x, y} = getNotePosition(index);
                    const labelPos = getNotePosition(index, labelRadius);
                    return (
                        <g key={index}>
                            <circle
                                cx={x}
                                cy={y}
                                r={noteRadius}
                                fill={note ? '#333' : 'white'}
                                stroke="#333"
                                strokeWidth={lineWidth}
                            />
                            <text
                                x={labelPos.x}
                                y={labelPos.y}
                                textAnchor="middle"
                                dominantBaseline="middle"
                                fontSize={noteRadius}
                                fill="#333"
                            >
                                {pitchClasses[index]}
                            </text>
                        </g>
                    );
                })}
            </svg>
        </div>
    );
};

export default braceletNotation;
