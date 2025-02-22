import React from 'react';
import { FontAwesomeIcon } from '@fortawesome/react-fontawesome';
import { faExternalLinkAlt } from '@fortawesome/free-solid-svg-icons';
import { NoteGroup } from './NoteGroup';
import { angleToCoordinates } from '../Common/geometryUtils';

const MARGIN_RATIO = 0.2;
const NOTE_RADIUS_RATIO = 0.10;
const LINE_WIDTH_RATIO = 0.005;
const LABEL_RADIUS_OFFSET = 2.2;

interface BraceletNotationProps {
    scale: number;
    size?: number;
}

const useBraceletNotation = (scale: number, size: number) => {
    const margin = size * MARGIN_RATIO;
    const effectiveSize = size - 2 * margin;
    const radius = effectiveSize * 0.5;
    const center = size / 2;
    const noteRadius = effectiveSize * NOTE_RADIUS_RATIO;
    const lineWidth = effectiveSize * LINE_WIDTH_RATIO;
    const labelRadius = radius + noteRadius * LABEL_RADIUS_OFFSET;

    const scaleArray = Array.from({length: 12}, (_, i) => (scale & (1 << i)) !== 0 ? 1 : 0);

    const findSymmetryAxes = (): number[] => {
        const axes: number[] = [];
        for (let i = 0; i < 6; i++) {
            if (Array.from({ length: 6 }).every((_, j) =>
                scaleArray[(i + j) % 12] === scaleArray[(i - j + 12) % 12]
            )) {
                axes.push(i);
                if (i !== 0) axes.push((i + 6) % 12);
            }
        }
        return axes;
    };

    return {
        center, radius, noteRadius, lineWidth, labelRadius,
        scaleArray, findSymmetryAxes
    };
};

const SymmetryAxis: React.FC<{angle: number, radius: number, center: number, lineWidth: number, noteRadius: number}> =
    ({angle, radius, center, lineWidth, noteRadius}) => {
        const start = angleToCoordinates(angle, radius, center, center);
        const end = angleToCoordinates(angle + 180, radius, center, center);
        return (
            <line
                x1={start.x}
                y1={start.y}
                x2={end.x}
                y2={end.y}
                stroke="#333"
                strokeWidth={lineWidth}
                strokeDasharray={`${noteRadius * 0.5},${noteRadius * 0.5}`}
            />
        );
    };

const BraceletNotation: React.FC<BraceletNotationProps> = ({ scale, size = 200 }) => {
    const {
        center, radius, noteRadius, lineWidth, labelRadius,
        scaleArray, findSymmetryAxes
    } = useBraceletNotation(scale, size);

    const symmetryAxes = findSymmetryAxes();
    const scaleLink = `https://ianring.com/musictheory/scales/${scale}`;

    console.log('Circle values:', { center, radius, lineWidth });

    return (
        <div style={{display: 'flex', flexDirection: 'column', alignItems: 'center'}}>
            <svg width={size} height={size} viewBox={`0 0 ${size} ${size}`}>
                <circle
                    cx={center}
                    cy={center}
                    r={radius}
                    fill="none"
                    stroke="#333"
                    strokeWidth={lineWidth}
                />
                {symmetryAxes.map((axis) => (
                    <SymmetryAxis
                        key={`symmetry-${axis}`}
                        angle={axis * 30}
                        radius={radius}
                        center={center}
                        lineWidth={lineWidth}
                        noteRadius={noteRadius}
                    />
                ))}
                {scaleArray.map((note, index) => (
                    <NoteGroup
                        key={index}
                        index={index}
                        angle={index * 30}
                        note={note}
                        radius={radius}
                        center={center}
                        noteRadius={noteRadius}
                        labelRadius={labelRadius}
                        lineWidth={lineWidth}
                    />
                ))}
            </svg>
            <div>
                <text>{scale}</text>
                <a href={scaleLink} target="_blank" rel="noopener noreferrer">
                    <FontAwesomeIcon icon={faExternalLinkAlt} style={{marginLeft: '5px', fontSize: '0.8em'}}/>
                </a>
            </div>
        </div>
    );
};

export default BraceletNotation;