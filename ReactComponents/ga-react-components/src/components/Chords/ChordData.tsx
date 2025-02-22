import {Barre} from "../Barre.tsx";
import {ChordNote} from "./ChordNote.tsx";

export interface ChordData {
    chordNotes: ChordNote[];
    position?: number;
    barres?: Barre[];
}