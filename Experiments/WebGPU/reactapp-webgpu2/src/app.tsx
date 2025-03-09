import WebGPUModesGrid from "./components/WebGPUModesGrid.tsx";
import WebGPUSurface from "./components/WebGpuMonohedron2.tsx";

function App() {
    return (
        <div className="App" style={{ 
            backgroundColor: '#000', 
            minHeight: '100vh',
            display: 'flex',
            flexDirection: 'column',
            alignItems: 'center',
            padding: '20px'
        }}>
            <h1 style={{ color: '#fff', marginBottom: '20px' }}>Guitar stuff</h1>
            <WebGPUModesGrid />
            <WebGPUSurface />
        </div>
    );
}

export default App;
