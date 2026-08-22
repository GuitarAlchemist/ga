import React from 'react';
import { TonnetzGraphProps, ParsimoniousTriad } from './TonnetzGraph.types';

export interface TonnetzGraphProps {
    scaleMask: number;
    size?: number;
}

export interface TriadNode {
    id: string;
    root: number; // 0..11
    type: 'Major' | 'Minor' | 'Diminished' | 'Augmented';
    pitchClasses: number[];
    x: number;
    y: number;
}

export interface ConnectionEdge {
    from: string;
    to: string;
    movingVoiceFrom: number;
    movingVoiceTo: number;
    shift: number;
}

const NOTE_NAMES = ['C', 'C#', 'D', 'D#', 'E', 'F', 'F#', 'G', 'G#', 'A', 'A#', 'B'];

export const TonnetzGraph: React.FC<TonnetzGraphProps> = ({ scaleMask, size = 320 }) => {
    const pcs = Array.from({ length: 12 }, (_, i) => ((scaleMask & (1 << i)) !== 0 ? i : -1)).filter((p) => p !== -1);
    const pcSet = new Set(pcs);

    // Compute triads contained in scale
    const triads: TriadNode[] = [];
    pcs.forEach((root) => {
        const maj3 = (root + 4) % 12;
        const min3 = (root + 3) % 12;
        const perf5 = (root + 7) % 12;
        const dim5 = (root + 6) % 12;
        const aug5 = (root + 8) % 12;

        if (pcSet.has(maj3) && pcSet.has(perf5)) {
            triads.push({ id: `${NOTE_NAMES[root]}`, root, type: 'Major', pitchClasses: [root, maj3, perf5], x: 0, y: 0 });
        }
        if (pcSet.has(min3) && pcSet.has(perf5)) {
            triads.push({ id: `${NOTE_NAMES[root]}m`, root, type: 'Minor', pitchClasses: [root, min3, perf5], x: 0, y: 0 });
        }
        if (pcSet.has(min3) && pcSet.has(dim5)) {
            triads.push({ id: `${NOTE_NAMES[root]}°`, root, type: 'Diminished', pitchClasses: [root, min3, dim5], x: 0, y: 0 });
        }
        if (pcSet.has(maj3) && pcSet.has(aug5)) {
            triads.push({ id: `${NOTE_NAMES[root]}+`, root, type: 'Augmented', pitchClasses: [root, maj3, aug5], x: 0, y: 0 });
        }
    });

    // Layout nodes in a circle
    const center = size / 2;
    const radius = size * 0.35;
    triads.forEach((triad, i) => {
        const angle = (i * 2 * Math.PI) / (triads.length || 1) - Math.PI / 2;
        triad.x = center + radius * Math.cos(angle);
        triad.y = center + radius * Math.sin(angle);
    });

    // Find parsimonious connections (2 common tones)
    const connections: ConnectionEdge[] = [];
    for (let i = 0; i < triads.length; i++) {
        for (let j = i + 1; j < triads.length; j++) {
            const t1 = triads[i];
            const t2 = triads[j];
            const common = t1.pitchClasses.filter((pc) => t2.pitchClasses.includes(pc));

            if (common.length === 2) {
                const diff1 = t1.pitchClasses.find((pc) => !common.includes(pc))!;
                const diff2 = t2.pitchClasses.find((pc) => !common.includes(pc))!;
                let shift = Math.abs((diff2 - diff1 + 12) % 12);
                if (shift > 6) shift = 12 - shift;

                if (shift <= 2) {
                    connections.push({
                        from: t1.id,
                        to: t2.id,
                        movingVoiceFrom: diff1,
                        movingVoiceTo: diff2,
                        shift,
                    });
                }
            }
        }
    }

    const getNodeColor = (type: TriadNode['type']) => {
        switch (type) {
            case 'Major': return '#4ade80';
            case 'Minor': return '#60a5fa';
            case 'Diminished': return '#f87171';
            case 'Augmented': return '#c084fc';
        }
    };

    return (
        <div style={{ display: 'flex', flexDirection: 'column', alignItems: 'center' }}>
            <svg width={size} height={size} viewBox={`0 0 ${size} ${size}`}>
                <circle cx={center} cy={center} r={radius} fill="none" stroke="#333" strokeDasharray="3 3" strokeWidth="1" />
                {connections.map((conn, idx) => {
                    const fromNode = triads.find((t) => t.id === conn.from)!;
                    const toNode = triads.find((t) => t.id === conn.to)!;
                    return (
                        <line
                            key={`edge-${idx}`}
                            x1={fromNode.x}
                            y1={fromNode.y}
                            x2={toNode.x}
                            y2={toNode.y}
                            stroke={conn.shift === 1 ? '#38bdf8' : '#a855f7'}
                            strokeWidth={conn.shift === 1 ? 2.5 : 1.5}
                        />
                    );
                })}
                {triads.map((node) => (
                    <g key={node.id} transform={`translate(${node.x}, ${node.y})`}>
                        <circle r={18} fill="#18181b" stroke={getNodeColor(node.type)} strokeWidth="2.5" />
                        <text
                            textAnchor="middle"
                            dy="0.35em"
                            fill="#f4f4f5"
                            fontSize="12"
                            fontWeight="bold"
                            fontFamily="monospace"
                        >
                            {node.id}
                        </text>
                    </g>
                ))}
            </svg>
            <div style={{ marginTop: '8px', fontSize: '12px', color: '#a1a1aa', textAlign: 'center' }}>
                <span style={{ color: '#38bdf8', fontWeight: 'bold' }}>─</span> 1 semitone move &nbsp;&nbsp;
                <span style={{ color: '#a855f7', fontWeight: 'bold' }}>─</span> 2 semitones move
            </div>
        </div>
    );
};

export default TonnetzGraph;
