import React from 'react';

interface RadialLayoutProps {
    children: (props: RadialSectionProps) => React.ReactNode;
    count: number;
    radius: number;
    centerX: number;
    centerY: number;
}

interface RadialSectionProps {
    index: number;
    angle: number;
    radius: number;
    centerX: number;
    centerY: number;
}

/**
 * A component that arranges its children in a radial layout around a center point.
 *
 * @param {function} children - A function that receives `RadialSectionProps` and returns a React node.
 * @param {number} count - The number of sections or children to arrange radially.
 * @param {number} radius - The radius of the circular layout.
 * @param {number} centerX - The x-coordinate of the center point.
 * @param {number} centerY - The y-coordinate of the center point.
 * @returns {React.ReactElement} A set of children arranged in a radial pattern.
 */
export const RadialLayout: React.FC<RadialLayoutProps> = ({ children, count, radius, centerX, centerY }) => {
    return (
        <>
            {Array.from({ length: count }, (_, index) => {
                const angle = (index * 360) / count;
                return children({
                    index,
                    angle,
                    radius,
                    centerX,
                    centerY,
                });
            })}
        </>
    );
};

export type { RadialLayoutProps, RadialSectionProps };