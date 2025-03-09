import WebGPUModesGrid from "./components/WebGPUModesGrid.tsx";
import {WebGPUMonohedron} from "./components/WebGpuMonohedron.tsx";
import {WebGPUMonohedron2} from "./components/WebGpuMonohedron2.tsx";

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
            <WebGPUMonohedron2 />
        </div>
    );
}

export default App;