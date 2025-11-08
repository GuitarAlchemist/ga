import React from 'react';

interface TonnetzDiagramProps {
    scale: number;
    size?: number;
}

const TonnetzDiagram: React.FC<TonnetzDiagramProps> = ({scale, size = 300}) => {
    const notes: string[] = ['C', 'G', 'D', 'A', 'E', 'B', 'F#', 'C#', 'G#', 'D#', 'A#', 'F'];
    const scaleNotes: string[] = scale.toString(2).padStart(12, '0').split('').reverse();

    const cellSize: number = size / 4;
    const radius: number = cellSize / 3;

    const drawHexagon = (cx: number, cy: number): string => {
        const points: string[] = [];
        for (let i = 0; i < 6; i++) {
            const angle: number = (60 * i - 30) * Math.PI / 180;
            points.push(`${cx + radius * Math.cos(angle)},${cy + radius * Math.sin(angle)}`);
        }
        return points.join(' ');
    };

    return (
        <svg width={size} height={size} viewBox={`0 0 ${size} ${size}`}>
            {notes.map((note: string, index: number) => {
                const row: number = Math.floor(index / 4);
                const col: number = index % 4;
                const cx: number = cellSize * (col + 0.5 + (row % 2) * 0.5);
                const cy: number = cellSize * (row * 0.866 + 0.5);

                return (
                    <g key={note}>
                        <polygon
                            points={drawHexagon(cx, cy)}
                            fill={scaleNotes[index] === '1' ? '#b3cde0' : 'white'}
                            stroke="#333"
                        />
                        <text
                            x={cx}
                            y={cy}
                            textAnchor="middle"
                            dominantBaseline="central"
                            fontSize={radius}
                        >
                            {note}
                        </text>
                    </g>
                );
            })}
        </svg>
    );
};

export default TonnetzDiagram;
