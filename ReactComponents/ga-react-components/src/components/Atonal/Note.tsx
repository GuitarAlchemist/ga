// Note.tsx
import React from "react";
import { angleToCoordinates } from "../Common/geometryUtils";

interface NoteProps {
    angle: number;
    radius: number;
    centerX: number;
    centerY: number;
    r: number;
    filled: boolean;
    stroke: string;
    strokeWidth: number;
}

/**
 * A utility component for rendering a `circle` element at a given angle and
 * radius around a center point.
 *
 * @param {number} angle - The angle of the circle in degrees, relative to the
 *   positive x-axis.
 * @param {number} radius - The distance of the circle from the center point.
 * @param {number} centerX - The x-coordinate of the center point.
 * @param {number} centerY - The y-coordinate of the center point.
 * @param {number} r - The radius of the circle.
 * @param {boolean} filled - Whether the circle is filled with the stroke color
 *   or hollow.
 * @param {string} stroke - The color of the circle's stroke.
 * @param {number} strokeWidth - The width of the stroke.
 * @param {React.SVGProps<SVGCircleElement>} props - Any additional props to
 *   be passed to the `circle` element.
 * @returns {React.ReactElement} The rendered `circle` element.
 */
export const Note: React.FC<NoteProps> = ({
                                              angle,
                                              radius,
                                              centerX,
                                              centerY,
                                              r,
                                              filled,
                                              stroke,
                                              strokeWidth,
                                              ...props
                                          }) => {
    const { x, y } = angleToCoordinates(angle, radius, centerX, centerY);

    return (
        <circle
            cx={x}
            cy={y}
            r={r}
            fill={filled ? stroke : 'white'}
            stroke={stroke}
            strokeWidth={strokeWidth}
            {...props}
        />
    );
};

export type { NoteProps };