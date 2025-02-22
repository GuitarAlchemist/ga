// NoteLabel.tsx
import React from "react";
import { angleToCoordinates } from "../Common/geometryUtils";

interface NoteLabelProps {
    angle: number;
    radius: number;
    centerX: number;
    centerY: number;
    label: string;
    fontSize?: number;
}

/**
 * A utility component for rendering a `text` element at a given angle and
 * radius around a center point.
 *
 * @param {number} angle - The angle of the text in degrees, relative to the
 *   positive x-axis.
 * @param {number} radius - The distance of the text from the center point.
 * @param {number} centerX - The x-coordinate of the center point.
 * @param {number} centerY - The y-coordinate of the center point.
 * @param {string} label - The text to be rendered.
 * @param {number} fontSize - The size of the text. Defaults to 12.
 * @returns {React.ReactElement} The rendered `text` element.
 */
export const NoteLabel: React.FC<NoteLabelProps> = ({
                                                        angle,
                                                        radius,
                                                        centerX,
                                                        centerY,
                                                        label,
                                                        fontSize = 12  // default font size
                                                    }) => {
    const { x, y } = angleToCoordinates(angle, radius, centerX, centerY);

    return (
        <text
            x={x}
            y={y}
            textAnchor="middle"
            dominantBaseline="middle"
            fontSize={fontSize}
            fill="#333"
        >
            {label}
        </text>
    );
};

export type { NoteLabelProps };