import React, {useEffect, useState} from 'react';
import {AgGridReact} from 'ag-grid-react';
import 'ag-grid-community/styles/ag-grid.css'; // Core grid CSS, required
import 'ag-grid-community/styles/ag-theme-alpine.css';
import 'ag-grid-community/styles/ag-theme-material.css';
import {IForecast} from "./IForecast.tsx";
import VexTabDisplay from "./vextab.tsx";
import ChordDiagram from "./chord-diagram.tsx";

function App() {
    const [forecasts, setForecasts] = useState<IForecast[] | null>(null);
    const [errorMessage, setErrorMessage] = useState<string | null>(null);

    async function populateWeatherData() {
        try {
            const response = await fetch('http://localhost:5212/weatherforecast');
            console.log(response);
            if (!response.ok) {
                setErrorMessage(`Error fetching data: HTTP status ${response.status}`);
                return;
            }
            const data: IForecast[] = await response.json();
            console.log("Fetched Data:", data);
            setForecasts(data); // Update the forecasts with fetched data
            setErrorMessage(null); // Clear error if successful
        } catch (error) {
            setErrorMessage(`Error fetching data: ${error instanceof Error ? error.message : 'Unknown error'}`);
        }
    }

    useEffect(() => {
        populateWeatherData().then(_ => console.log("Done populating data"));
    }, []);

    // Define the columns for AG-Grid
    const columns = [
        { headerName: "Date", field: "date", valueFormatter: (params: { value: string | number | Date; }) => new Date(params.value).toLocaleDateString() },
        { headerName: "Temp. (C)", field: "temperatureC" },
        { headerName: "Temp. (F)", field: "temperatureF" },
        { headerName: "Summary", field: "summary" }
    ];

    const notes = [
        [2, 1, "1"],
        [3, 2, "2"],
        [5, 3, "3"],
        [6, "x"],
    ];

    return (
        <div style={{backgroundColor: 'white', height: '100vh', width: '100vw'}}>
            <div>
                <h1>Chord Diagram Example</h1>
                <ChordDiagram label="C Major" notes={notes}/>
            </div>
            <div>
                <h1>Tab Example</h1>
                <VexTabDisplay notation="V:1 t=120 3/7 2/8 3/9"/>
            </div>

            <div className="ag-theme-material" style={{height: '100%', width: '100%'}}>
                <h1>Weather Forecast</h1>
                <p>This component demonstrates fetching data from the server.</p>
                {errorMessage && <p style={{color: 'red'}}>{errorMessage}</p>}
                {!forecasts && !errorMessage && <p><em>Loading...</em></p>}
                {forecasts && (
                    <AgGridReact
                        rowData={forecasts}
                        columnDefs={columns}
                        domLayout='autoHeight'
                        style={{height: '100%', width: '100%'}}
                    />
                )}
            </div>
        </div>
    );
}

export default App;
