import { useState } from 'react';
import './App.css';
import WebGPUMonohedron2 from "./components/WebGpuMonohedron2.tsx";

function App() {
    const [size, setSize] = useState(0.25);
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
                            Size: {size.toFixed(2)}
                        </label>
                        <input
                            id="size-slider"
                            type="range"
                            min="0.1"
                            max="0.5"
                            step="0.01"
                            value={size}
                            onChange={(e) => setSize(parseFloat(e.target.value))}
                            style={{
                                width: '150px',
                                cursor: 'pointer'
                            }}
                        />
                    </div>
                    <button
                        onClick={toggleFullScreen}
                        style={{
                            padding: '8px 16px',
                            backgroundColor: '#646cff',
                            color: 'white',
                            border: 'none',
                            borderRadius: '4px',
                            cursor: 'pointer',
                            fontSize: '14px',
                            transition: 'background-color 0.2s'
                        }}
                        onMouseOver={(e) => e.currentTarget.style.backgroundColor = '#4a50bf'}
                        onMouseOut={(e) => e.currentTarget.style.backgroundColor = '#646cff'}
                    >
                        {isFullScreen ? 'Exit Fullscreen' : 'Fullscreen'}
                    </button>
                </div>
            </div>
            <div style={{
                position: 'absolute',
                top: '80px',
                left: '20px',
                right: '20px',
                bottom: '20px',
                overflow: 'hidden',
                display: 'flex',
                justifyContent: 'center',
                alignItems: 'center'
            }}>
                <div className="chrome-container">
                    <WebGPUMonohedron2 
                        width={window.innerWidth - 40} 
                        height={window.innerHeight - 100}
                        size={size}
                        fullScreen={isFullScreen}
                    />
                </div>
            </div>
        </div>
    );
}

export default App;