import { useEffect, useState } from 'react';

interface IForecast {
    date: string;
    temperatureC: number;
    temperatureF: number;
    summary: string;
}

function App() {
    const [forecasts, setForecasts] = useState<IForecast[] | null>(null);
    const [errorMessage, setErrorMessage] = useState<string | null>(null);

    async function populateWeatherData() {
        try {
            // Using the native Fetch API for the browser
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
        populateWeatherData();
    }, []);

    return (
        <div>
            <h1>Weather Forecast</h1>
            <p>This component demonstrates fetching data from the server.</p>

            {errorMessage && <p style={{ color: 'red' }}>{errorMessage}</p>}

            {forecasts && (
                <table className="table table-striped" aria-labelledby="tableLabel">
                    <thead>
                        <tr>
                            <th>Date</th>
                            <th>Temp. (C)</th>
                            <th>Temp. (F)</th>
                            <th>Summary</th>
                        </tr>
                    </thead>
                    <tbody>
                        {forecasts.map((forecast, index) => (
                            <tr key={index}>
                                <td>{new Date(forecast.date).toLocaleDateString()}</td>
                                <td>{forecast.temperatureC}</td>
                                <td>{forecast.temperatureF}</td>
                                <td>{forecast.summary}</td>
                            </tr>
                        ))}
                    </tbody>
                </table>
            )}

            {!forecasts && !errorMessage && <p><em>Loading...</em></p>}
        </div>
    );
}

export default App;
