import {Container} from '@svgdotjs/svg.js';

// @ts-ignore
/**
 * Interface for ChordBox parameters.
 * This interface is part of the public API for the ChordBox module.
 *
 * @param {number} numStrings - The number of strings on the chord diagram.
 * @param {number} numFrets - The number of frets on the chord diagram.
 * @param {number} x - The x-coordinate for the chord diagram.
 * @param {number} y - The y-coordinate for the chord diagram.
 * @param {number} width - The width of the chord diagram.
 * @param {number} height - The height of the chord diagram.
 * @param {number} strokeWidth - The stroke width for lines.
 * @param {boolean} showTuning - Whether to show tuning indicators.
 * @param {string} defaultColor - Default color for diagram elements.
 * @param {string} bgColor - Background color of the diagram.
 * @param {string} labelColor - Color for labels.
 * @param {string} fontFamily - Font family for text elements.
 * @param {number} [fontSize] - Font size for text elements.
 * @param {string} fontStyle - Font style for text elements.
 * @param {string} fontWeight - Font weight for text elements.
 * @param {string} labelWeight - Weight for label text.
 * @param {string} bridgeColor - Color for the bridge.
 * @param {string} stringColor - Color for strings.
 * @param {string} fretColor - Color for frets.
 * @param {string} strokeColor - Color for stroke elements.
 * @param {string} textColor - Color for text elements.
 * @param {number} stringWidth - Width for strings.
 * @param {number} fretWidth - Width for frets.
 */
interface ChordBoxParams {
    numStrings?: number;
    numFrets?: number;
    x?: number;
    y?: number;
    width?: number;
    height?: number;
    strokeWidth?: number;
    showTuning?: boolean;
    defaultColor?: string;
    bgColor?: string;
    labelColor?: string;
    fontFamily?: string;
    fontSize?: number;
    fontStyle?: string;
    fontWeight?: string;
    labelWeight?: string;
    bridgeColor?: string;
    stringColor?: string;
    fretColor?: string;
    strokeColor?: string;
    textColor?: string;
    stringWidth?: number;
    fretWidth?: number;
}

interface ChordData {
    chord: Array<[number, number, string?]>;
    position?: number;
    barres?: Array<{ fromString: number; toString: number; fret: number }>;
    positionText?: number;
    tuning?: string[];
}

interface LightUpData {
    string: number;
    fret: number | 'x';
    label?: string;
}

class ChordBox {
    private canvas: Container;
    private width: number;
    private height: number;
    private numStrings: number;
    private numFrets: number;
    private spacing: number;
    private fretSpacing: number;
    private x: number;
    private y: number;
    private metrics: {
        circleRadius: number;
        barreRadius: number;
        fontSize: number;
        barShiftX: number;
        bridgeStrokeWidth: number;

