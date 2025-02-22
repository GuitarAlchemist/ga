import { ThemeProvider, createTheme } from '@mui/material';
import {
    AppBar,
    Box,
    Button,
    Container,
    CssBaseline,
    Grid,
    Typography
} from '@mui/material';
import FretboardPositionsGrid from "ga-react-components/src/components/FretboardGrid.tsx";
import VexChordDiagram from "ga-react-components/src/components/Chords/VexChordDiagram.tsx";
import {ChordData} from "ga-react-components/src/components/Chords/ChordData.tsx";
import ScaleSelector from "ga-react-components/src/components/ScaleSelector.tsx";
import {useState} from "react";

const defaultTheme = createTheme({
    palette: {
        mode: 'light'
    }
});

function App() {
    const [count, setCount] = useState(0);
    const [selectedNotes, setSelectedNotes] = useState<string[]>([]);

    const handleNotesChange = (notes: string[]) => {
        setSelectedNotes(notes);
    };

    const eMajorChord: ChordData = {
        chordNotes: [
            [1, 0], // Open high E string
            [2, 0], // Open B string
            [3, 1], // G string at fret 1
            [4, 2], // D string at fret 2
            [5, 2], // A string at fret 2
            [6, 0]  // Open low E string
        ],
        position: 1,  // This chord starts at the first fret
    };

    return (
        <ThemeProvider theme={defaultTheme}>
            <div>Hello</div>
            <CssBaseline />
            <Box sx={{ 
                display: 'flex',
                flexDirection: 'column',
                minHeight: '100vh',
                bgcolor: 'background.default',
                color: 'text.primary'
            }}>
                <Container maxWidth="lg" sx={{ mt: 4, mb: 4 }}>
                    <Grid container spacing={3}>
                        <Grid item xs={12}>
                            <Typography variant="h3" component="h1" gutterBottom>
                                Guitar Alchemist
                            </Typography>
                        </Grid>
                        <Grid item xs={12} md={6}>
                            <Box sx={{ mb: 2 }}>
                                <Button variant="contained" color="primary" onClick={() => setCount((count) => count + 1)}>
                                    Count is {count}
                                </Button>
                            </Box>
                        </Grid>
                        <Grid item xs={12} md={6}>
                            <ScaleSelector onNotesChange={handleNotesChange} />
                            <VexChordDiagram chord={eMajorChord} />
                        </Grid>
                        <Grid item xs={12}>
                            <FretboardPositionsGrid notes={selectedNotes.join(' ')} showDetails={false} debug={true} />
                        </Grid>
                    </Grid>
                </Container>
            </Box>
        </ThemeProvider>
    );
}

export default App;