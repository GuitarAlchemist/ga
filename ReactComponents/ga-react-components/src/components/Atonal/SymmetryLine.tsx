import React from "react";

interface SymmetryLineProps {
    start: { x: number; y: number };
    end: { x: number; y: number };
}

/**
 * A utility component for rendering a `line` element that indicates the symmetry of a scale.
 *
 * @param {SymmetryLineProps} props - The props for the component.
 * @param {Object} props.start - The start point of the line as an object with `x` and `y` properties.
 * @param {Object} props.end - The end point of the line as an object with `x` and `y` properties.
 * @param {*} [props.rest] - Any additional props to be passed to the `line` element.
 * @returns {React.ReactElement} The rendered `line` element.
 */
 const SymmetryLine: React.FC<SymmetryLineProps> = ({ start, end, ...props }) => (
    <line x1={start.x} y1={start.y} x2={end.x} y2={end.y} {...props} />
);

export type { SymmetryLineProps };

export default SymmetryLine;
