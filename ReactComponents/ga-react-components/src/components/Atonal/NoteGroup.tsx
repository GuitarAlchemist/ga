import React from "react";
import { Note } from "./Note";
import { NoteLabel } from "./NoteLabel";

interface NoteGroupProps {
    index: number;
    angle: number;
    note: number;
    radius: number;
    center: number;
    noteRadius: number;
    labelRadius: number;
    lineWidth: number;
}

/**
 * A component that renders a musical note and its label in a circular layout.
 *
 * @param {number} index - The index of the note in the scale.
 * @param {number} angle - The angle of the note in degrees.
 * @param {number} note - The note value (0 or 1).
 * @param {number} radius - The radius of the circle.
 * @param {number} center - The x and y coordinates of the center of the circle.
 * @param {number} noteRadius - The radius of the note circle.
 * @param {number} labelRadius - The radius of the label circle.
 * @param {number} lineWidth - The width of the line stroke.
 * @returns {React.ReactElement} The rendered `svg` element.
 */
export const NoteGroup: React.FC<NoteGroupProps> = ({
                                                        index,
                                                        angle,
                                                        note,
                                                        radius,
                                                        center,
                                                        noteRadius,
                                                        labelRadius,
                                                        lineWidth
                                                    }) => {
    return (
        <>
            <line
                x1={center}
                y1={center}
                x2={center + radius * Math.cos(angle * Math.PI / 180)}
                y2={center + radius * Math.sin(angle * Math.PI / 180)}
                stroke="#333"
                strokeWidth={lineWidth}
            />
            <Note
                angle={angle}
                radius={radius}
                centerX={center}
                centerY={center}
                r={noteRadius}
                filled={note === 1}
                stroke="#333"
                strokeWidth={lineWidth}
            />
            <NoteLabel
                angle={angle}
                radius={labelRadius}
                centerX={center}
                centerY={center}
                label={getPitchClass(index)}
            />
        </>
    );
};

function getPitchClass(index: number): string {
    const pitchClasses = ['C', 'C#', 'D', 'D#', 'E', 'F', 'F#', 'G', 'G#', 'A', 'A#', 'B'];
    return pitchClasses[index];
}

export type { NoteGroupProps };