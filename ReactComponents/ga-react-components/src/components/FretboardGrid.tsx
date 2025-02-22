import React, { useState, useEffect, useMemo, useCallback } from 'react';
import { AgGridReact } from 'ag-grid-react';
import 'ag-grid-community/styles/ag-grid.css';
import 'ag-grid-community/styles/ag-theme-alpine.css';
import { ColDef, ICellRendererParams } from 'ag-grid-community';
import VexChordDiagram from './Chords/VexChordDiagram.tsx';
import {ChordData} from "./Chords/ChordData.tsx";
import {ChordNote} from "./Chords/ChordNote.tsx";

interface RowData {
    id: number;
    positions: string; // Positions string could be something like "2, x, 1, 0, 3, x" for fret/string definitions.
}

interface FretboardGridProps {
    notes: string;
    showDetails: boolean;
    debug?: boolean; // Debug flag to control the display of chord data JSON
}

const FretboardGrid: React.FC<FretboardGridProps> = ({ notes = '', showDetails = false, debug = false }) => {
    const [rowData, setRowData] = useState<RowData[]>([]);
    const [error, setError] = useState<string | null>(null);
    const [loading, setLoading] = useState<boolean>(false);

    useEffect(() => {
        const fetchData = async () => {
            setLoading(true);
            setError(null);
            try {
                const url = `http://localhost:5232/Fretboard?notes=${encodeURIComponent(notes)}&showDetails=${showDetails}`;
                const response = await fetch(url);
                if (!response.ok) {
                    throw new Error(`HTTP error! status: ${response.status}`);
                }
                const data: string[] = await response.json();
                const formattedData: RowData[] = data.map((item, index) => ({
                    id: index + 1,
                    positions: item
                }));
                setRowData(formattedData);
            } catch (error) {
                console.error('Error fetching data:', error);
                setError(error instanceof Error ? error.message : 'An unknown error occurred');
                setRowData([]);
            } finally {
                setLoading(false);
            }
        };

        fetchData().then(() => console.log('Done fetching data'));
    }, [notes, showDetails]);

    const ChordDiagramRenderer = useCallback((props: ICellRendererParams) => {
        const { value } = props;

        const parseChordData = (value: string): ChordData | null => {
            try {
                // Split positions string into an array of fret values
                const fretValues = value.split(' ').map(fret => fret.trim().toUpperCase());
                const chordNotes: ChordNote[] = fretValues.map((fret, index) => {
                    const stringNumber = index + 1;

                    let fretValue: number | 'x';
                    if (fret === 'X') {
                        fretValue = 'x';
                    } else if (fret === '0' || fret === 'O') {
                        fretValue = 0;  // Handle open strings
                    } else {
                        const parsedFret = parseInt(fret, 10);
                        if (isNaN(parsedFret) || parsedFret < 0) {
                            throw new Error(`Invalid fret value for string ${stringNumber}: "${fret}"`);
                        }
                        fretValue = parsedFret;
                    }

                    return [stringNumber, fretValue] as ChordNote;
                });

                if (debug) {
                    console.log("Parsed Chord Data:", chordNotes);
                }

                return {
                    chordNotes: chordNotes,
                    position: 1,
                    barres: []
                } as ChordData;
            } catch (error) {
                console.error('Error parsing chord data:', error);
                return null;
            }
        };

        const chordData = parseChordData(value);

        if (!chordData) {
            return <div style={{ color: 'red' }}>Invalid chord data</div>;
        }

        return (
            <div>
                <VexChordDiagram chord={chordData} width={100} height={120} />
                {debug && (
                    <pre style={{ fontSize: '10px', color: '#888' }}>
                        {JSON.stringify(chordData, null, 2)}
                    </pre>
                )}
            </div>
        );
    }, [debug]);

    const columnDefs: ColDef[] = useMemo(() => [
        { headerName: 'ID', field: 'id', sortable: true, filter: true, width: 100 },
        { headerName: 'Positions', field: 'positions', sortable: true, filter: true, flex: 1 },
        {
            headerName: 'Chord Diagram',
            field: 'positions',
            cellRenderer: ChordDiagramRenderer,
            flex: 2
        }
    ], [ChordDiagramRenderer]);

    return (
        <div>
            {loading && <div>Loading...</div>}
            {error && (
                <div className="error-message" style={{ color: 'red', padding: '10px' }}>
                    Error: {error}
                </div>
            )}
            <div className="ag-theme-alpine" style={{ height: 1000, width: '100%' }}>
                <AgGridReact
                    columnDefs={columnDefs}
                    rowData={rowData}
                    pagination={true}
                    paginationPageSize={10}
                    rowHeight={debug ? 150 : 110} // Adjust row height if debug information is displayed
                />
            </div>
        </div>
    );
};

export default FretboardGrid;
