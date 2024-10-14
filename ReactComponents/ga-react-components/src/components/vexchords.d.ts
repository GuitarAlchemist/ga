declare module 'vexchords' {
    export interface ChordBoxOptions {
        width?: number;
        height?: number;
        x?: number;
        y?: number;
        index?: number;
        stringCount?: number;
        fretCount?: number;
        fretWidth?: number;
        stringWidth?: number;
        fontFamily?: string;
        fontSize?: number;
        fontWeight?: string;
        fontStyle?: string;
        labelColor?: string;
        strokeColor?: string;
        backgroundColor?: string;
        barColor?: string;
        circleRadius?: number;
    }

    export interface ChordDefinition {
        name?: string;
        position?: number;
        barres?: Array<{ fromString: number; toString: number; fret: number }>;
        fingers?: number[];
        fingerings?: number[];
    }

    export class ChordBox {
        constructor(element: HTMLElement, options?: ChordBoxOptions);
        draw(chord: ChordDefinition): void;
        setChord(chord: ChordDefinition): void;
    }
}