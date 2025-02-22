import React, { useState, useEffect } from 'react';
import axios from 'axios';

interface TuningInfo {
    name: string;
    tuning: string;
}

interface InstrumentInfo {
    name: string;
    tunings: TuningInfo[];
}

const InstrumentSelector: React.FC = () => {
    const [instruments, setInstruments] = useState<InstrumentInfo[]>([]);
    const [selectedInstrument, setSelectedInstrument] = useState<string | null>(null);
    const [selectedTuning, setSelectedTuning] = useState<string | null>(null);
    const [availableTunings, setAvailableTunings] = useState<TuningInfo[]>([]);
    const [loading, setLoading] = useState<boolean>(true);
    const [error, setError] = useState<string | null>(null);

    useEffect(() => {
        // Fetch instruments from the server
        const fetchInstruments = async () => {
            try {
                setLoading(true);
                const response = await axios.get<InstrumentInfo[]>('/api/instruments');
                setInstruments(response.data);
                setLoading(false);
            } catch (err) {
                setError('Failed to load instruments');
                setLoading(false);
            }
        };

        fetchInstruments();
    }, []);

    const handleInstrumentChange = (event: React.ChangeEvent<HTMLSelectElement>) => {
        const instrumentName = event.target.value;
        setSelectedInstrument(instrumentName);

        const selectedInstrumentInfo = instruments.find((instrument) => instrument.name === instrumentName);
        if (selectedInstrumentInfo) {
            setAvailableTunings(selectedInstrumentInfo.tunings);
            setSelectedTuning(selectedInstrumentInfo.tunings[0]?.name || null); // Select the first tuning by default
        }
    };

    const handleTuningChange = (event: React.ChangeEvent<HTMLSelectElement>) => {
        setSelectedTuning(event.target.value);
    };

    if (loading) {
        return <div>Loading instruments...</div>;
    }

    if (error) {
        return <div>{error}</div>;
    }

    return (
        <div>
            <h2>Select an Instrument and Tuning</h2>

            {/* Instrument Selection */}
            <div>
                <label htmlFor="instrument-select">Instrument:</label>
                <select id="instrument-select" onChange={handleInstrumentChange} value={selectedInstrument || ''}>
                    <option value="" disabled>Select an instrument</option>
                    {instruments.map((instrument) => (
                        <option key={instrument.name} value={instrument.name}>
                            {instrument.name}
                        </option>
                    ))}
                </select>
            </div>

            {/* Tuning Selection */}
            {availableTunings.length > 0 && (
                <div>
                    <label htmlFor="tuning-select">Tuning:</label>
                    <select id="tuning-select" onChange={handleTuningChange} value={selectedTuning || ''}>
                        {availableTunings.map((tuning) => (
                            <option key={tuning.name} value={tuning.name}>
                                {tuning.name} - {tuning.tuning}
                            </option>
                        ))}
                    </select>
                </div>
            )}

            {/* Display Selected Instrument and Tuning */}
            {selectedInstrument && selectedTuning && (
                <div>
                    <h3>Selected Instrument: {selectedInstrument}</h3>
                    <h4>Selected Tuning: {selectedTuning}</h4>
                </div>
            )}
        </div>
    );
};

export default InstrumentSelector;
