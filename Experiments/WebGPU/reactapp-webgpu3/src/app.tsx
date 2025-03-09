import { useState } from 'react';
import './App.css';
import WebGPUMonohedron2 from "./components/WebGpuMonohedron2.tsx";

function App() {
    const [size, setSize] = useState(0.15);
    const [bumpiness, setBumpiness] = useState(0.01);
    const [isFullScreen, setIsFullScreen] = useState(false);

    const toggleFullScreen = async () => {
        if (!document.fullscreenElement) {
            await document.documentElement.requestFullscreen();
            setIsFullScreen(true);
        } else {
            await document.exitFullscreen();
            setIsFullScreen(false);
        }
    };

    return (
        <div className="App">
            <div style={{
                color: '#fff',
                margin: '0',
                padding: '20px 0',
                width: '100%',
                textAlign: 'center',
                display: 'flex',
                justifyContent: 'center',
                alignItems: 'center',
                gap: '20px'
            }}>
                <h1 style={{ margin: 0 }}>Monohedron</h1>
                <div style={{
                    display: 'flex',
                    alignItems: 'center',
                    gap: '20px',
                }}>
                    {/* Size Control */}
                    <div style={{
                        display: 'flex',
                        alignItems: 'center',
                        gap: '10px',
                        backgroundColor: 'rgba(245, 245, 245, 0.1)',
                        padding: '8px 16px',
                        borderRadius: '8px',
                    }}>
                        <label htmlFor="size-slider" style={{ 
                            fontSize: '14px',
                            color: '#fff',
                            whiteSpace: 'nowrap'
                        }}>
                            Zoom: {size.toFixed(2)}
                        </label>
                        <input
                            id="size-slider"
                            type="range"
                            min="0.2"
                            max="16.0"    // Changed from 1.0 to 16.0
                            step="0.01"
                            value={size}
                            onChange={(e) => setSize(parseFloat(e.target.value))}
                            style={{
                                width: '150px',
                                cursor: 'pointer'
                            }}
                        />
                    </div>

                    {/* Bumpiness Control */}
                    <div style={{
                        display: 'flex',
                        alignItems: 'center',
                        gap: '10px',
                        backgroundColor: 'rgba(245, 245, 245, 0.1)',
                        padding: '8px 16px',
                        borderRadius: '8px',
                    }}>
                        <label htmlFor="bump-slider" style={{ 
                            fontSize: '14px',
                            color: '#fff',
                            whiteSpace: 'nowrap'
                        }}>
                            Bump: {bumpiness.toFixed(3)}
                        </label>
                        <input
                            id="bump-slider"
                            type="range"
                            min="0.0"
                            max="0.05"
                            step="0.001"
                            value={bumpiness}
                            onChange={(e) => setBumpiness(parseFloat(e.target.value))}
                            style={{
                                width: '150px',
                                cursor: 'pointer'
                            }}
                        />
                    </div>

                    <button
                        onClick={toggleFullScreen}
                        style={{
                            backgroundColor: 'rgba(245, 245, 245, 0.1)',
                            border: 'none',
                            color: '#fff',
                            padding: '8px 16px',
                            borderRadius: '8px',
                            cursor: 'pointer',
                            fontSize: '14px'
                        }}
                    >
                        {isFullScreen ? 'Exit Fullscreen' : 'Fullscreen'}
                    </button>
                </div>
            </div>
            <div style={{
                width: '100%',
                height: 'calc(100vh - 100px)',
                overflow: 'hidden',
                display: 'flex',
                justifyContent: 'center',
                alignItems: 'center'
            }}>
                <WebGPUMonohedron2 
                    width={window.innerWidth - 40} 
                    height={window.innerHeight - 100}
                    size={size}
                    bumpiness={bumpiness}
                    fullScreen={isFullScreen}
                />
            </div>
        </div>
    );
}

export default App;