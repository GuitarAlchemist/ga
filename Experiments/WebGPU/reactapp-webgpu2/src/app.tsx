import {WebGPUCanvas2} from "./components/WebGPUCanvas2.tsx";

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
            <WebGPUCanvas2 scale={2741}/>
        </div>
    );
}

export default App;