import React from 'react';
import { WebGpuBraceletNotation } from './WebGpuBraceletNotation';

interface WebGPUModesGridProps {
    size?: number;
}

interface Mode {
    name: string;
    scale: number;
    description: string;
    alterations: string[];
    colorTones?: { [key: number]: string };
    color?: [number, number, number, number];
    brightness: string; // Add brightness indicator
    symmetryPoints: number[];
}

const WebGPUModesGrid: React.FC<WebGPUModesGridProps> = ({ size = 800 }) => {


    const modes: Mode[] = [
        {
            name: "Locrian",
            scale: 1387,      // Binary: 010101101011 (C Db Eb F Gb Ab Bb)
            description: "Diminished scale",
            alterations: ["♭2", "♭3", "♭5", "♭6", "♭7"],
            brightness: "darkest",
            symmetryPoints: []
        },
        {
            name: "Phrygian",
            scale: 1451,      // Binary: 010110101011 (C Db Eb F G Ab Bb)
            description: "Spanish-flavored scale",
            alterations: ["♭2", "♭3", "♭6", "♭7"],
            brightness: "dark",
            symmetryPoints: []
        },
        {
            name: "Aeolian",
            scale: 1453,      // Binary: 010110101101 (C D Eb F G Ab Bb)
            description: "Natural minor scale",
            alterations: ["♭3", "♭6", "♭7"],
            brightness: "dark",
            symmetryPoints: []
        },
        {
            name: "Dorian",
            scale: 1709,      // Binary: 011010110101 (C D Eb F G A Bb)
            description: "Minor scale with natural 6",
            alterations: ["♭3", "♭7"],
            brightness: "neutral",
            symmetryPoints: []
        },
        {
            name: "Mixolydian",
            scale: 2773,      // Binary: 101011010101 (C D E F G A Bb)
            description: "Dominant seventh scale",
            alterations: ["♭7"],
            brightness: "bright",
            symmetryPoints: []
        },
        {
            name: "Major (Ionian)",
            scale: 2741,      // Binary: 101010110101 (C D E F G A B)
            description: "Bright major scale",
            alterations: [],
            brightness: "brightest",
            symmetryPoints: []
        }
    ];

    // Add this utility function to calculate symmetry points
    const calculateSymmetryPoints = (scale: number): number[] => {
        const scaleArray = Array.from({ length: 12 }, (_, i) => 
            (scale & (1 << i)) !== 0 ? 1 : 0
        );
        
        const symmetryPoints: number[] = [];
        for (let i = 0; i < 12; i++) {
            let isSymmetric = true;
            for (let j = 0; j < 6; j++) {
                if (scaleArray[(i + j) % 12] !== scaleArray[(i - j + 12) % 12]) {
                    isSymmetric = false;
                    break;
                }
            }
            if (isSymmetric) symmetryPoints.push(i);
        }
        return symmetryPoints;
    };

    // Update symmetry points for each mode
    modes.forEach(mode => {
        mode.symmetryPoints = calculateSymmetryPoints(mode.scale);
    });

    const notationSize = Math.min(size / 3 - 40, 300);

    return (
        <div style={{
            display: 'grid',
            gridTemplateColumns: 'repeat(3, 1fr)',
            gap: '20px',
            padding: '20px',
            maxWidth: `${size}px`,
            margin: '0 auto'
        }}>
            {modes.map(mode => (
                <div key={mode.name} style={{
                    display: 'flex',
                    flexDirection: 'column',
                    alignItems: 'center',
                    padding: '15px',
                    border: '1px solid #ccc',
                    borderRadius: '8px',
                    backgroundColor: 'white',
                    position: 'relative'  // Added for absolute positioning of scale number
                }}>
                    <a 
                        href={`https://ianring.com/musictheory/scales/${mode.scale}`}
                        target="_blank"
                        rel="noopener noreferrer"
                        style={{
                            position: 'absolute',
                            top: '10px',
                            right: '10px',
                            fontSize: '12px',
                            color: '#666',
                            textDecoration: 'none',
                            padding: '4px 8px',
                            backgroundColor: '#f0f0f0',
                            borderRadius: '4px',
                            transition: 'background-color 0.2s',
                        }}
                        onMouseOver={(e) => e.currentTarget.style.backgroundColor = '#e0e0e0'}
                        onMouseOut={(e) => e.currentTarget.style.backgroundColor = '#f0f0f0'}
                    >
                        #{mode.scale}
                    </a>
                    <h3 style={{ marginBottom: '5px' }}>{mode.name}</h3>
                    <div style={{
                        fontSize: '16px',
                        marginBottom: '10px',
                        color: '#666'
                    }}>
                        {mode.brightness}
                    </div>
                    <p style={{ 
                        fontSize: '14px', 
                        color: '#666',
                        marginBottom: '5px',
                        textAlign: 'center'
                    }}>
                        {mode.description}
                    </p>
                    {mode.alterations.length > 0 && (
                        <div style={{
                            display: 'flex',
                            gap: '8px',
                            flexWrap: 'wrap',
                            justifyContent: 'center',
                            marginBottom: '10px'
                        }}>
                            {mode.alterations.map((alt, index) => (
                                <span key={index} style={{
                                    padding: '2px 6px',
                                    backgroundColor: '#f0f0f0',
                                    borderRadius: '4px',
                                    fontSize: '12px'
                                }}>
                                    {alt}
                                </span>
                            ))}
                        </div>
                    )}
                    <WebGpuBraceletNotation 
                        scale={mode.scale} 
                        size={notationSize}
                        colorTones={mode.colorTones}
                    />
                </div>
            ))}
        </div>
    );
};

export default WebGPUModesGrid;