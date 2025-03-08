import React from 'react';
import {WebGpuBraceletNotation} from './WebGpuBraceletNotation';

interface WebGPUModesGridProps {
    size?: number;
}

interface NextMode {
    scale: number;
    description: string;
    explanation: string;  // Added explanation property
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
    rootPosition: number; // Add this new property
    romanNumeral: string; // Add this new property
    tension: string; // Add this new property
    nextModes: NextMode[]; // Changed from string to NextMode[]
}

const WebGPUModesGrid: React.FC<WebGPUModesGridProps> = ({size = 800}) => {
    // Add the keyframes style at the beginning of the component
    const style = document.createElement('style');
    style.textContent = `
        @keyframes fadeIn {
            from {
                opacity: 0;
                transform: translateY(5px);
            }
            to {
                opacity: 1;
                transform: translateY(0);
            }
        }
    `;
    document.head.appendChild(style);

    // State for tracking the currently hovered mode's next scales
    const [hoveredFromScale, setHoveredFromScale] = React.useState<number | null>(null);
    const [hoveredNextScales, setHoveredNextScales] = React.useState<number[]>([]);
    
    // Add state for storing current explanations
    const [nextModeExplanations, setNextModeExplanations] = React.useState<{[key: number]: string}>({});

    // Helper to determine if a mode should be highlighted and get its explanation
    const shouldHighlightMode = (mode: Mode) => {
        if (!hoveredFromScale || hoveredNextScales.length === 0) return false;
        return hoveredNextScales.includes(mode.scale);
    };    
    
    const modes: Mode[] = [
        {
            name: "Locrian",
            scale: 1387,
            description: "Diminished scale",
            alterations: ["♭2", "♭3", "♭5", "♭6", "♭7"],
            brightness: "darkest",
            symmetryPoints: [],
            colorTones: {
                1: "♭2",
                4: "♭5"
            },
            rootPosition: 11,
            romanNumeral: "vii°",
            tension: "⚡⚡⚡⚡⚡",
            nextModes: [
                { 
                    scale: 1451, 
                    description: "resolve tension",
                    explanation: "Moving to Phrygian removes the ♭5, reducing tension while maintaining the dark character of ♭2 and ♭3"
                },
                { 
                    scale: 1453, 
                    description: "via ♮5",
                    explanation: "Moving to Aeolian naturalizes both ♭2 and ♭5, creating a more stable minor tonality"
                }
            ]
        },
        {
            name: "Phrygian",
            scale: 1451,
            description: "Spanish-flavored scale",
            alterations: ["♭2", "♭3", "♭6", "♭7"],
            brightness: "darker",
            symmetryPoints: [],
            colorTones: {
                1: "♭2",
                3: "♭3"
            },
            rootPosition: 8,
            romanNumeral: "iii",
            tension: "⚡⚡⚡⚡",
            nextModes: [
                { 
                    scale: 1453, 
                    description: "brighten",
                    explanation: "Moving to Aeolian raises the ♭2 to ♮2, creating a more conventional minor sound"
                },
                { 
                    scale: 1387, 
                    description: "darken",
                    explanation: "Moving to Locrian flattens the 5th, intensifying the dark character with maximum tension"
                }
            ]
        },
        {
            name: "Aeolian",
            scale: 1453,
            description: "Natural minor scale",
            alterations: ["♭3", "♭6", "♭7"],
            brightness: "dark",
            symmetryPoints: [],
            colorTones: {
                3: "♭3",
                8: "♭6"
            },
            rootPosition: 9,
            romanNumeral: "vi",
            tension: "⚡⚡⚡",
            nextModes: [
                { 
                    scale: 1709, 
                    description: "brighten",
                    explanation: "Moving to Dorian raises the ♭6 to ♮6, creating a lighter minor sound while maintaining the minor third"
                },
                { 
                    scale: 1451, 
                    description: "darken",
                    explanation: "Moving to Phrygian lowers the 2nd, adding Spanish/Moorish flavor and increased tension"
                }
            ]
        },
        {
            name: "Dorian",
            scale: 1709,
            description: "Minor scale with natural 6",
            alterations: ["♭3", "♭7"],
            brightness: "neutral",
            symmetryPoints: [],
            colorTones: {
                3: "♭3",
                9: "6"
            },
            rootPosition: 2,
            romanNumeral: "ii",
            tension: "⚡⚡",
            nextModes: [
                { 
                    scale: 2773, 
                    description: "brighten",
                    explanation: "Moving to Mixolydian raises the ♭3 to ♮3, creating a brighter sound while maintaining the ♭7"
                },
                { 
                    scale: 1453, 
                    description: "darken",
                    explanation: "Moving to Aeolian adds ♭6, darkening the sound while keeping the minor third character"
                }
            ]
        },
        {
            name: "Mixolydian",
            scale: 2773,
            description: "Dominant seventh scale",
            alterations: ["♭7"],
            brightness: "bright",
            symmetryPoints: [],
            colorTones: {
                10: "♭7"
            },
            rootPosition: 7,
            romanNumeral: "V",
            tension: "⚡",
            nextModes: [
                { 
                    scale: 2741, 
                    description: "resolve",
                    explanation: "Moving to Ionian raises the ♭7 to ♮7, creating a stable major tonality with no tension"
                },
                { 
                    scale: 1709, 
                    description: "darken",
                    explanation: "Moving to Dorian lowers the 3rd, shifting from major to minor while keeping the characteristic ♭7"
                }
            ]
        },
        {
            name: "Major (Ionian)",
            scale: 2741,
            description: "Bright major scale",
            alterations: [],
            brightness: "brighter",
            symmetryPoints: [],
            colorTones: {
                0: "1",
                4: "3",
                7: "5"
            },
            rootPosition: 0,
            romanNumeral: "I",
            tension: "○",
            nextModes: [
                { 
                    scale: 2901, 
                    description: "brighten",
                    explanation: "Moving to Lydian raises the 4th, adding an ethereal quality through the ♯4 characteristic tone"
                },
                { 
                    scale: 2773, 
                    description: "add tension",
                    explanation: "Moving to Mixolydian lowers the 7th, creating dominant tension while maintaining the major third"
                }
            ]
        },
        {
            name: "Lydian",
            scale: 2901,
            description: "Major scale with raised 4th",
            alterations: ["♯4"],
            brightness: "brightest",
            symmetryPoints: [],
            colorTones: {
                6: "♯4"
            },
            rootPosition: 5,
            romanNumeral: "IV",
            tension: "⚡",
            nextModes: [
                { 
                    scale: 2741, 
                    description: "ground",
                    explanation: "Moving to Ionian lowers the ♯4 to ♮4, returning to a stable major tonality"
                },
                { 
                    scale: 2773, 
                    description: "via ♮4",
                    explanation: "Moving to Mixolydian normalizes the ♯4 and lowers the 7th, creating dominant tension"
                }
            ]
        }
    ];

    // Add this utility function to calculate symmetry points
    const calculateSymmetryPoints = (scale: number): number[] => {
        const scaleArray = Array.from({length: 12}, (_, i) =>
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
                <div key={mode.scale} style={{
                    display: 'flex',
                    flexDirection: 'column',
                    alignItems: 'center',
                    padding: '10px',
                    border: '1px solid #ccc',
                    borderRadius: '8px',
                    backgroundColor: 'white',
                    position: 'relative',
                    transition: 'all 0.3s ease',
                    ...(shouldHighlightMode(mode) && {
                        transform: 'scale(1.02)',
                        boxShadow: '0 0 15px rgba(100, 108, 255, 0.4)',
                        borderColor: '#646cff'
                    })
                }}>
                    <a
                        href={`https://ianring.com/musictheory/scales/${mode.scale}`}
                        target="_blank"
                        rel="noopener noreferrer"
                        style={{
                            position: 'absolute',
                            top: '5px',
                            right: '5px',
                            fontSize: '11px',
                            color: '#666',
                            textDecoration: 'none',
                            padding: '2px 6px',
                            backgroundColor: '#f0f0f0',
                            borderRadius: '4px',
                            transition: 'background-color 0.2s',
                        }}
                        onMouseOver={(e) => e.currentTarget.style.backgroundColor = '#e0e0e0'}
                        onMouseOut={(e) => e.currentTarget.style.backgroundColor = '#f0f0f0'}
                    >
                        #{mode.scale}
                    </a>
                    <h3 style={{
                        marginBottom: '2px',
                        fontSize: '16px',
                        marginTop: '0'
                    }}>
                        {mode.name} ({mode.romanNumeral})
                    </h3>
                    <div style={{
                        fontSize: '14px',
                        marginBottom: '5px',
                        color: '#666',
                        display: 'flex',
                        gap: '8px',
                        alignItems: 'center'
                    }}>
                        <span>{mode.brightness}</span>
                        <span style={{
                            color: mode.tension === "○" ? "#00aa00" : "#ff6600",
                            fontSize: '12px'
                        }}>
                            {mode.tension}
                        </span>
                    </div>
                    <p style={{
                        fontSize: '12px',
                        color: '#666',
                        marginBottom: '3px',
                        marginTop: '0',
                        textAlign: 'center'
                    }}>
                        {mode.description}
                    </p>
                    {mode.alterations.length > 0 && (
                        <div style={{
                            display: 'flex',
                            gap: '4px',
                            flexWrap: 'wrap',
                            justifyContent: 'center',
                            marginBottom: '5px'
                        }}>
                            {mode.alterations.map((alt, index) => (
                                <span key={index} style={{
                                    padding: '1px 4px',
                                    backgroundColor: '#f0f0f0',
                                    borderRadius: '3px',
                                    fontSize: '11px'
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
                        rootPosition={mode.rootPosition}
                    />
                    {/* Move the explanation inside the card */}
                    {shouldHighlightMode(mode) && nextModeExplanations[mode.scale] && (
                        <div style={{
                            position: 'absolute',
                            bottom: '30px', // Position above the arrow button
                            left: '0',
                            right: '0',
                            backgroundColor: '#646cff',
                            color: 'white',
                            padding: '2px 8px',
                            fontSize: '11px',
                            textAlign: 'center',
                            animation: 'fadeIn 0.3s ease',
                            zIndex: 1
                        }}>
                            {nextModeExplanations[mode.scale]}
                        </div>
                    )}
                    <div 
                        style={{
                            position: 'absolute',
                            bottom: '5px',
                            right: '5px',
                            cursor: 'pointer',
                            padding: '2px 6px',
                            backgroundColor: '#f0f0f0',
                            borderRadius: '4px',
                            fontSize: '12px',
                            transition: 'all 0.2s',
                            ...(hoveredFromScale === mode.scale && {
                                backgroundColor: '#646cff',
                                color: 'white'
                            })
                        }}
                        onMouseEnter={() => {
                            setHoveredFromScale(mode.scale);
                            setHoveredNextScales(mode.nextModes.map(next => next.scale));
                            const explanations = mode.nextModes.reduce((acc, next) => ({
                                ...acc,
                                [next.scale]: next.description
                            }), {});
                            setNextModeExplanations(explanations);
                        }}
                        onMouseLeave={() => {
                            setHoveredFromScale(null);
                            setHoveredNextScales([]);
                            setNextModeExplanations({});
                        }}
                    >
                        <span>➜</span>
                    </div>
                </div>
            ))}
        </div>
    );
};

export default WebGPUModesGrid;
