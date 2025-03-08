import {WebGpuBraceletNotation} from "./components/WebGpuBraceletNotation.tsx";
import WebGPUModesGrid from "./components/WebGPUModesGrid.tsx";

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
        </div>
    );
}

export default App;