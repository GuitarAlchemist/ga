import React, { useState } from 'react';
import reactLogo from './assets/react.svg';
import viteLogo from '/vite.svg';
import '@mantine/core/styles.css';
import {
    MantineProvider,
    AppShell,
    Container,
    Group,
    Button,
    Text,
    Image,
    createTheme,
    Grid
} from '@mantine/core';
import MusicNotationDisplay from "ga-react-components/src/components/MusicNotationDisplay";
import FretboardPositionsGrid from "ga-react-components/src/components/FretboardPositionsGrid";
import ChordDiagram, {ChordData} from "ga-react-components/src/components/ChordDiagram";

const theme = createTheme({
    primaryColor: 'cyan',
    colors: {
        brand: ['#F0BBDD', '#ED9BCF', '#EC7CC3', '#ED5DB8', '#F13EAF', '#F71FA7', '#FF00A1', '#E00890', '#C50E82', '#AD1374'],
    },
    fontFamily: 'Verdana, sans-serif',
    fontFamilyMonospace: 'Monaco, Courier, monospace',
    headings: { fontFamily: 'Greycliff CF, sans-serif' },
});

function App() {
    const [count, setCount] = useState(0);
    const [selectedNotes, setSelectedNotes] = useState<string[]>([]);

    const handleNotesChange = (notes: string[]) => {
        setSelectedNotes(notes);
    };

    const eMajorChord: ChordData = {
        chordNotes: [
            [1, 0],     // Open high E string
            [2, 0],     // Open B string
            [3, 1],     // G string at fret 1
            [4, 2],     // D string at fret 2
            [5, 2],     // A string at fret 2
            [6, 0]      // Open low E string
        ],
        position: 1,  // This chord starts at the first fret
    };

    console.log(JSON.stringify(eMajorChord));

    return (
        <MantineProvider theme={theme}>
            <AppShell
                header={{ height: 60 }}
                padding="md">
                <AppShell.Header>
                    <div style={{ padding: '1rem' }}>
                        <Group justify="space-between" h="100%">
                            <Text size="xl" fw={800}>Guitar Alchemist</Text>
                            <Group>
                                <Image src={viteLogo} width={30} alt="Vite logo" />
                                <Image src={reactLogo} width={30} alt="React logo" />
                            </Group>
                        </Group>
                    </div>
                </AppShell.Header>

                <AppShell.Main style={{ height: 'calc(100vh - 60px)' }}>
                    <Grid style={{ height: '100%'}}>
                        <Grid.Col span={4}>
                            <MusicNotationDisplay onNotesChange={handleNotesChange} />
                            <ChordDiagram chord={eMajorChord} />
                            <Button onClick={() => setCount((count) => count + 1)}>
                                Count is {count}
                            </Button>
                        </Grid.Col>
                        <Grid.Col span={8} style={{ display: 'flex', flexDirection: 'column', height: '100%' }}>
                            <div style={{ flex: 1, overflowY: 'auto' }}>
                                <FretboardPositionsGrid notes={selectedNotes.join(' ')} showDetails={false} />
                            </div>
                        </Grid.Col>
                    </Grid>
                </AppShell.Main>
            </AppShell>
        </MantineProvider>
    );
}

export default App;