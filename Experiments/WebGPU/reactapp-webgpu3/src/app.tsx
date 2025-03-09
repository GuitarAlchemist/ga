import './App.css';
import WebGPUMonohedron2 from "./components/WebGpuMonohedron2.tsx";

function App() {
    return (
        <div className="App">
            <h1 style={{
                color: '#fff',
                margin: '0',
                padding: '20px 0',
                width: '100%',
                textAlign: 'center'
            }}>Monohedron</h1>
            <div style={{
                position: 'absolute',
                top: '80px', // Height of header + padding
                left: '20px',
                right: '20px',
                bottom: '20px',
                overflow: 'hidden',
                display: 'flex',
                justifyContent: 'center',
                alignItems: 'center'
            }}>
                <WebGPUMonohedron2 
                    width={window.innerWidth - 40} 
                    height={window.innerHeight - 100} 
                />
            </div>
        </div>
    );
}

export default App;